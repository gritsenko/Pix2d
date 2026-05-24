# Extract `SelectionController` from `DrawingLayerNode`

## Goal

`DrawingLayerNode` ([Sources/Core/Pix2d.Core/Plugins/Drawing/Nodes/DrawingLayerNode.cs](../Sources/Core/Pix2d.Core/Plugins/Drawing/Nodes/DrawingLayerNode.cs))
сейчас совмещает ~5 ответственностей: pointer routing, рендер штрихов (line/rect/oval +
mirror + pixel-perfect), flood-fill, управление селекшеном (marquee + lift/commit +
FrameEditor + operations + flip/rotate/fill) и оркестрацию трёх битмапов
(`_backgroundBitmap` / `_workingBitmap` / `_swapBitmap`). Файл — 1630 строк, и в нём же
сидит вся «кривоватая» логика transform-режима ([pixel-selection-edit-tool-split.md](pixel-selection-edit-tool-split.md)).

Этот план описывает первый — и самый ценный — шаг: вынести **селекшен-блок** в отдельный
класс `SelectionController`. Цели:

1. Сжать `DrawingLayerNode` до ~700 строк за счёт миграции 18 методов и 6 полей.
2. Сделать инварианты `SelectionPhase` локальными: переходы `None ↔ MarqueeReady ↔ Transforming`
   живут в одном классе, и компилятор/ревьювер видит их единым куском.
3. Получить чистый шов, на котором можно ловить и чинить баги transform-режима — сейчас
   они размыты по всему файлу.
4. Подготовить почву для следующих рефакторингов (`StrokeRenderer`, `PointerInputRouter`)
   — после извлечения селекшена их границы становятся очевидными.

**Это НЕ цели этого плана:**
- Менять публичный `IDrawingLayer` (внешние потребители не трогаем).
- Менять поведение / логику undo-redo / порядок событий. Это чистый move-method, без
  смены семантики.
- Фиксить баги transform-режима — для этого отдельный план уже есть
  ([pixel-selection-edit-tool-split.md](pixel-selection-edit-tool-split.md)). Этот рефакторинг
  только готовит почву.
- Извлекать `StrokeRenderer` и `PointerInputRouter` — это следующие итерации.

## Текущая карта связности

Внешние потребители селекшен-методов / событий
(найдены `Explore`-агентом по ключевым именам):

| Потребитель | Что вызывает / на что подписан |
|---|---|
| [DrawingService](../Sources/Core/Pix2d.Core/Plugins/Drawing/Services/DrawingService.cs) | задаёт `AspectSnapper`, `ActiveToolKeyProvider`; подписан на `SelectionStarted`, `SelectionTransformed` |
| [SelectionOperation](../Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/SelectionOperation.cs) | `GetSelectionLayer`, `GetSelectionBackground`, `SelectionPhase`, `SetSelection(layer, bg, contourOnly)` |
| [PasteOperation](../Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/PasteOperation.cs) | `ApplySelection`, `SetSelectionFromExternal` |
| [PixelSelectToolBase](../Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelSelect/PixelSelectToolBase.cs) | `SelectionMode`, `SelectionPhase`, `SelectionSize`, `ApplySelection`, `SetSelectionTransformMode`, `GetSelectionLayer`; подписан на `PixelsBeforeSelected`, `SelectionStarted`, `SelectionRemoved` |
| [PixelTransformTool](../Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelSelect/PixelTransformTool.cs) | `HasSelection`, `SelectionPhase`, `ActivateEditor(contourOnly)`, `ApplySelection` |
| [ExtractObjectTool (AI)](../Sources/Plugins/Pix2d.Plugins.Ai/ExtractObjectTool.cs) | подписан на `PixelsBeforeSelected`, `SelectionStarted`, `SelectionRemoved`, `PixelsSelected` |
| [SpriteEditCommands](../Sources/Core/Pix2d.Core/Plugins/Sprite/Commands/SpriteEditCommands.cs) | `ApplySelection`, `GetSelectionLayer`, `EnterTransformMode`, `RotateSelection` |
| [SpritePlugin](../Sources/Core/Pix2d.Core/Plugins/Sprite/SpritePlugin.cs) | `GetSelectionLayer`, `FillSelection` |
| ShapeBuilders | читают `AspectSnapper` (для рисования, не селекшен — но сидит на той же ноде) |

Все потребители работают через `IDrawingLayer` либо приводят к конкретному
`DrawingLayerNode` (только `SelectionOperation` — его конструктор принимает класс
напрямую). Меняем тип параметра у `SelectionOperation` — и снаружи **никто** про
`SelectionController` знать не должен.

## Архитектурное решение

### Кто чем владеет

```
DrawingLayerNode  (SKNode, узел сцены)
├── _backgroundBitmap / _workingBitmap / _swapBitmap   ← остаются здесь
├── _drawingMode / State / _strokePoints / _previewPos ← остаются
├── штрихи / fill / mirror / pixel-perfect            ← остаются
├── pointer events                                     ← остаются (см. ниже)
└── _selection : SelectionController                   ← новый агрегат
        ├── _selectionLayer / _selectionEditor
        ├── _pixelSelector / _customPixelSelector
        ├── _currentSelectionOperation
        ├── _pixelsLifted
        ├── SelectionMode / SelectionSize
        ├── события: SelectionStarted, SelectionRemoved,
        │             PixelsBeforeSelected, PixelsSelected,
        │             SelectionTransformed
        └── зависит от: ISelectionLayerHost (см. ниже)
```

### `ISelectionLayerHost` — узкий интерфейс между ними

Контроллер не должен напрямую трогать поля битмапов на ноде. Делаем maленький
интерфейс, который `DrawingLayerNode` реализует:

```text
ISelectionLayerHost  (internal, в неймспейсе Plugins.Drawing.Nodes)
- IDrawingTarget? DrawingTarget { get; }
- SKSize Size { get; }
- SKMatrix GetGlobalTransform()
- SKBitmap WorkingBitmap { get; }            // живой геттер, учитывает UseSwapBitmap
- bool UseSwapBitmap { get; set; }
- DrawingLayerState State { get; set; }
- float Opacity { get; set; }
- SKBitmap? BackgroundBitmap { get; set; }   // только селекшену нужен write-доступ
- void ClearWorkingBuffers()                 // = _backgroundBitmap?.Clear(); _workingBitmap?.Clear();
- void SwapWorkingBitmap()                   // существующий метод
- void ApplyWorkingBitmap()                  // существующий метод
- void RequestRefresh()                      // = Refresh() — поднимает LayerModified
- bool IsInBounds(SKPointI p)
- void SetPixel(int x, int y, SKColor c)
- IAspectSnapper? AspectSnapper { get; }
- Func<string?>? ActiveToolKeyProvider { get; }
- void RaiseDrawingApplied(bool saveToUndo)  // селекшен дёргает OnDrawingApplied
```

Почему интерфейс, а не передача ссылки на `DrawingLayerNode`:
- Явный список «что селекшену реально нужно от ноды» — это и есть документация шва.
- Контроллер становится unit-тестируемым через mock (тестов сейчас нет, но это
  открывает возможность).
- Сразу видно, какие поля **не** должны утечь обратно.

### События — где жить и как пробрасывать

5 событий (`SelectionStarted` / `SelectionRemoved` / `PixelsBeforeSelected` /
`PixelsSelected` / `SelectionTransformed`) переезжают на `SelectionController`. Но
внешние потребители подписаны на них через `IDrawingLayer` — значит,
`DrawingLayerNode` оставляет публичные события-фасады, которые просто реэкспортируют
сигналы контроллера:

```text
DrawingLayerNode:
    public event EventHandler? SelectionStarted {
        add    => _selection.SelectionStarted += value;
        remove => _selection.SelectionStarted -= value;
    }
    // и т.д. для остальных 4-х
```

Это **обязательно** — иначе все подписки в `PixelSelectToolBase`, `ExtractObjectTool`
и `DrawingService` придётся переписывать на `_selection.…`, что выходит за рамки этого
рефакторинга и тянет за собой изменение DI (контроллер придётся доставать наружу).

`DrawingApplied` / `DrawingStarted` / `LayerModified` остаются на ноде как родные —
их контроллер дёргает через `ISelectionLayerHost.RaiseDrawingApplied` и
`.RequestRefresh()`.

### Граничный вопрос: `_backgroundBitmap`

Это самый запутанный шов. Семантика поля разная в зависимости от состояния:

- В `Drawing`-режиме (`BeginDrawing` → `ApplyWorkingBitmap`) — это снапшот таргета,
  поверх которого подмешивается working bitmap при превью.
- В `Transforming`-режиме (`_pixelsLifted = true`) — это таргет с **вырезанной**
  областью выделения, подставленный через `SetTargetBitmapSubstitute`.
- В `MarqueeReady`-режиме (contour-only) — он не используется (working bitmap пуст,
  таргет рисует сам себя).
- В `Paste`-state — туда копируется текущий таргет, чтобы paste-операция могла
  откатиться.

То есть `_backgroundBitmap` **дёргается обоими** — и drawing-блоком, и selection-блоком,
причём как читается, так и присваивается (`_backgroundBitmap = snapshot;` в
`LiftSelectionFromCanvas`, `_backgroundBitmap = tmpBitmap;` в `FinishSelection`).

**Решение:** оставляем владение `_backgroundBitmap` на `DrawingLayerNode`, но
`ISelectionLayerHost` даёт **сеттер**. Это некрасиво, но честно отражает текущее
устройство — и не вводит лишнюю абстракцию ради абстракции. Когда мы вернёмся
извлекать `LayerBuffers` (отдельный класс на три битмапа) — это вычистится одной
заменой реализации хоста.

### Pointer events

Остаются на `DrawingLayerNode` (это узел сцены, он один может перехватывать ввод).
В местах, где обработчики дёргают `BeginSelection` / `AddSelectionPoint` /
`FinishSelection` / `SetSelectionRect`, они теперь вызывают `_selection.BeginSelection(...)`
и т.п. Никакой логики из обработчиков в контроллер не уезжает — это следующая
итерация (`PointerInputRouter`).

Тонкий момент: `_deferredTouchSelectionStart` и `_deferredTouchStartViewportPos`
(линии 68-70) — это часть pointer routing, а не селекшена. Остаются на ноде.

## Финальный публичный API `SelectionController`

```text
class SelectionController
{
    ctor(ISelectionLayerHost host)

    // События
    event SelectionStarted, SelectionRemoved
    event PixelsBeforeSelected, PixelsSelected
    event SelectionTransformed

    // Состояние / запросы
    bool HasSelection
    bool HasSelectionChanges                // = _selectionEditor.IsChanged
    SelectionPhase SelectionPhase
    SKSize SelectionSize
    PixelSelectionMode SelectionMode { get; set; }
    SpriteSelectionNode? CurrentSelectionLayer       // для SelectionOperation, internal
    SKBitmap GetSelectionBackground()                // для SelectionOperation
    SKNode GetSelectionLayerNode()                   // для IDrawingLayer.GetSelectionLayer()

    // Marquee lifecycle
    void BeginSelection(SKPoint pos)
    void AddSelectionPoint(SKPoint p)
    void SetSelectionRect(SKPoint start, SKPoint end)
    void FinishSelection()
    void SelectAll()

    // Apply / cancel
    void ApplySelection(bool saveToUndo = false)
    void EraseSelection()
    void CancelSelect()

    // Внешние постановки селекшена
    void SetSelection(SpriteSelectionNode layer, SKBitmap? backgroundBitmap, bool contourOnly = false)
    void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position)

    // Editor / mode
    void ActivateEditor(bool contourOnly = false)
    void DeactivateSelectionEditor()
    void InvalidateSelectionEditor()
    void EnterTransformMode()
    void SetSelectionTransformMode(bool transformMode)

    // Manipulation
    void FillSelection(SKColor color)
    void FlipSelection(FlipMode mode)
    void RotateSelection(int angle)

    // Custom selector
    void SetCustomPixelSelector(IPixelSelector s)
    void ClearCustomPixelSelector()

    // Сервисные
    void OnPanModeChanged()                          // дернёт ничего, если нет deferred state (deferred state — на ноде)
    void OnTargetChanged(bool sameTarget)            // вызывается из SetTarget — пока ничего, но шов готов
}
```

Поле `AspectSnapper` уезжает на ноду как обычное свойство (его читает `FrameEditorNode`
через `AspectSnapperProviderFunc` — функцию задаём в ноде, она тянет из контроллера
или своего поля; см. шаг 4 ниже).

Поле `ActiveToolKeyProvider` — то же самое, остаётся на ноде (его задаёт
`DrawingService`), контроллер читает через `host.ActiveToolKeyProvider`.

## Шаги

Каждый шаг — отдельный коммит. После каждого ручной smoke-тест (см. ниже).

### Шаг 1 — Создать пустой `SelectionController` + интерфейс

Файлы:
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Nodes/SelectionController.cs` — пустой класс
  с конструктором, принимающим `ISelectionLayerHost`. Поля переезжают, но методы
  ещё внутри ноды.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Nodes/ISelectionLayerHost.cs` — интерфейс.

`DrawingLayerNode` реализует `ISelectionLayerHost` (это `internal` интерфейс — не
утечёт наружу). Создаёт `_selection = new SelectionController(this)` в конструкторе.

Поведение не меняется. Цель шага — каркас.

**Smoke:** запустить дескоп, нарисовать штрих, выделить прямоугольником, переместить.
Всё должно работать как раньше.

### Шаг 2 — Перенести «чистые» методы (без работы с битмапами)

Простой move-method, без участия `_backgroundBitmap`/`_workingBitmap`:
- `SelectAll`
- `InvalidateSelectionEditor`
- `SetCustomPixelSelector` / `ClearCustomPixelSelector`
- `FlipSelection` / `RotateSelection`
- `HasSelection` / `HasSelectionChanges` / `SelectionPhase` / `SelectionSize`
- `GetSelectionLayer` → `GetSelectionLayerNode`
- Свойство `SelectionMode`

`DrawingLayerNode` оставляет публичные методы-обёртки, которые делегируют:
```text
public bool HasSelection => _selection.HasSelection;
public void SelectAll() => _selection.SelectAll();
…
```

В этом же шаге уезжают поля:
- `_selectionLayer` → контроллер
- `_selectionEditor` → контроллер (создаётся в ctor контроллера, как сейчас в ctor ноды)
- `_pixelSelector`, `_customPixelSelector`
- `_currentSelectionOperation`
- `SelectionMode`, `SelectionSize`
- `_pixelsLifted`

Обработчики `SelectionEditor_SelectionEdited` / `_SelectionEditing` / `_SelectionEditStarted`
переезжают вместе.

**Тонкость с `AspectSnapper`:** `_selectionEditor.AspectSnapperProviderFunc` сейчас
читает `AspectSnapper` на ноде. После переноса контроллер задаёт
`AspectSnapperProviderFunc = () => _host.AspectSnapper!`.

**Smoke:**
1. Select All → rotate (R) → Ctrl+Z. Должна вернуться оригинальная картинка.
2. Flip H / Flip V на marquee.

### Шаг 3 — Перенести marquee lifecycle

- `BeginSelection`
- `AddSelectionPoint`
- `SetSelectionRect`
- `FinishSelection`

Все четыре читают / пишут битмапы через хост: `host.ClearWorkingBuffers()`,
`host.SwapWorkingBitmap()`, `host.SetPixel(...)`, `host.IsInBounds(...)`.

`SetSelectionDashColor` уезжает в контроллер как `private static`.

Pointer-обработчики на ноде продолжают работать, но вызовы:
- `BeginSelection(StartPosI)` → `_selection.BeginSelection(StartPos)`
- `AddSelectionPoint(StartPosI)` → `_selection.AddSelectionPoint(StartPos)`
- `SetSelectionRect(StartPosI, EndPosI)` → `_selection.SetSelectionRect(StartPos, EndPos)`
- `FinishSelection()` → `_selection.FinishSelection()`

**Тонкость:** `BeginSelection` вызывает `ApplySelection()` в самом начале (строка
1186). После шага 4 это будет `_selection.ApplySelection()`. На этом шаге пока
`ApplySelection` ещё на ноде — контроллер вызывает её через `host`. Нет, лучше:
переносим `ApplySelection`/`EraseSelection`/`CancelSelect` в этом же шаге, иначе
циклическая зависимость через хост, который дёргает фасад на ноде, которая дёргает
контроллер.

Итого в шаге 3 переезжает блок marquee + apply:
- `BeginSelection`, `AddSelectionPoint`, `SetSelectionRect`, `FinishSelection`
- `ApplySelection`, `EraseSelection`, `CancelSelect`

**Smoke:**
1. Rect-выделение → drag → release без перемещения → marquee остаётся.
2. Rect-выделение → второй pointer-down рядом → старый marquee гасится, новый стартует.
3. Lasso-выделение замкнутое.
4. Same-color (magic wand).
5. Touch: палец на пустой области → tap → marquee гасится.
6. Touch: палец в области marquee → pinch (второй палец) → marquee сохраняется.

### Шаг 4 — Перенести selection-editor + lift/commit

Тяжелейший шаг. Переезжает:
- `ActivateEditor()` / `ActivateEditor(bool contourOnly)`
- `DeactivateSelectionEditor`
- `EnterTransformMode`
- `SetSelectionTransformMode`
- `LiftSelectionFromCanvas` (private)
- `CommitWorkingBitmapToCanvas` (private)
- `UpdateWorkingBitmapFromSelection` (private)
- `SetSelection`
- `SetSelectionFromExternal`
- `FillSelection`
- `GetSelectionBackground`

Все они дёргают `_backgroundBitmap` / `_workingBitmap` — через хост:
- читают: `host.BackgroundBitmap`, `host.WorkingBitmap`
- пишут: `host.BackgroundBitmap = …`, `host.ClearWorkingBuffers()`,
  `host.SwapWorkingBitmap()`, `host.ApplyWorkingBitmap()`
- управляют таргетом: `host.DrawingTarget.SetTargetBitmapSubstitute(…)`,
  `.ShowTargetBitmap()`, `.SetData(…)`

**Тонкость с `FlushCurrentEditing`:** оставляем на ноде, но его тело становится
`_selection.FlushCurrentEditing()` — переносим логику в контроллер. Функцию
`DrawingTarget.FlushRequestedAction = FlushCurrentEditing` ставим как обёртку.

**Smoke (это сценарии, где transform-режим «кривоватый» — особенно внимательно):**
1. Rect-выделить → переключиться на TransformTool → drag → release → drag ещё раз → Apply.
2. Rect-выделить → переключиться на TransformTool → drag → rotate → resize → Ctrl+Z (нужно вернуть позицию до resize).
3. Rect-выделить → переключиться на TransformTool → drag → переключиться на другой инструмент (например кисть) → должно сделать Apply неявно.
4. Paste (Ctrl+V) → должно создать selection в transform-режиме сразу с подсветкой.
5. Ctrl+V → Ctrl+Z → paste должен полностью откатиться (вернуть таргет, убрать marquee).
6. FillSelection (G по marquee) → marquee остаётся, пиксели залились.
7. AI ExtractObject → marquee должен появиться, `PixelsBeforeSelected` / `PixelsSelected` должны прилететь.

### Шаг 5 — Переориентировать `SelectionOperation` на контроллер

`SelectionOperation` принимает `DrawingLayerNode`. После шага 4 он дёргает:
- `drawingLayer.GetSelectionLayer()` — это `IDrawingLayer.GetSelectionLayer()` (фасад).
- `drawingLayer.GetSelectionBackground()` — теперь живёт в `_selection`. Оставляем
  публичный метод на ноде, делегирующий в контроллер.
- `drawingLayer.SelectionPhase` — фасад.
- `drawingLayer.SetSelection(...)` — фасад → контроллер.

Менять сигнатуру `SelectionOperation(DrawingLayerNode)` на
`SelectionOperation(SelectionController)` **НЕ нужно** — нода всё ещё каноничный
владелец операции и фигурирует в `GetEditedNodes()`. Просто внутри `SelectionOperation`
работаем через публичный API ноды, как сейчас.

`ActiveToolKeyProvider` остаётся на ноде. Контроллер читает через хост.

**Smoke:** Ctrl+Z после любого селекшен-действия. Особенно — drag → Ctrl+Z (должен
вернуть selection layer на исходную позицию), drag → drag → Ctrl+Z (должен откатить
второй drag, marquee на месте первого drag'а).

### Шаг 6 — Чистка ноды

После шагов 2-5 в `DrawingLayerNode` остаются только:
- pointer routing (`OnPointerPressed/Released/Moved`, `OnPanModeChanged`)
- штрихи: `DrawStroke` (4 перегрузки), `DrawPoint`, `DrawPointStroke`, `DrawLine`,
  `DrawRect`, `DrawEllipse`, `EraseStroke`, `ErasePoint`
- `DrawPixelPerfect` + `PixelPerfect` + `_strokePoints`
- mirror: `GetMirroredPoint`, `MirrorX/Y`, `MirrorXOffset/MirrorYOffset`
- `DrawWithBitmap`, `DrawBitmap`, `FillRegion` + `FloodFillBitmap`
- `BeginDrawing` / `FinishDrawing` / `CancelDrawing` / `FinishCurrentDrawing` /
  `ApplyDrawing` / `ApplyWorkingBitmap`
- `SetTarget` / `SetPixel` / `InBounds` / `IsInBounds`
- `UpdateBrushPreview` / `RenderBrushPreview` / `IsShowingBrush` / `ShowBrushPreview`
- `Refresh`, `OnDraw`
- `OnPanModeChanged`, `CancelCurrentOperation`, `CancelActiveDrawing`,
  `ClearTarget`, `DrawLine` / `DrawRect` / `DrawEllipse` для shape-builders'ов
- `SnapPointToAngleGrid`, `ProjectAspectPoint`
- `AxisLockMode`, `AspectSnapper`, `ActiveToolKeyProvider`, `LockTransparentPixels`,
  `DrawingMode`, `State`, `IsPixelPerfectMode`, `UseSwapBitmap`, `DrawingColor`,
  `Brush`, три битмапа, `_drawingMode`, `_lastPos`, `_previewPos`
- фасады для всех селекшен-методов и 5 событий из контроллера

Ожидаемый размер: 700-800 строк. В отдельной заметке — TODO: следующая итерация —
вынести `StrokeRenderer` и затем `PointerInputRouter`.

**Smoke:** полный прогон (см. чек-лист ниже).

## Полный smoke чек-лист (после шага 6)

Делается руками на Desktop-head'е. Без этого не мерджим.

**Рисование:**
- [ ] Кисть: один тык — точка; drag — линия; pixel-perfect mode.
- [ ] Erase: один тык; drag.
- [ ] Mirror X / Mirror Y / оба.
- [ ] Axis-lock (Shift+drag): горизонталь, вертикаль.
- [ ] Fill (bucket).
- [ ] Line / Rect / Oval / Triangle shape-tools.
- [ ] Ctrl+Z / Ctrl+Y по серии штрихов.

**Touch (если есть на руках):**
- [ ] Tap по marquee → tap снаружи → marquee гасится.
- [ ] Pinch на канвасе → marquee не теряется.
- [ ] Drag tap-then-move → стартует rect select после порога.

**Selection — basic:**
- [ ] Rect select.
- [ ] Lasso select.
- [ ] Magic wand (same color).
- [ ] Select All (Ctrl+A).
- [ ] Снять выделение (клик вне).

**Selection — transform:**
- [ ] Rect select → TransformTool → move → release → move ещё раз → Apply.
- [ ] То же + rotate + resize.
- [ ] Ctrl+Z после каждого drag'а (по отдельности откатывает каждое движение).
- [ ] Drag → переключение на кисть → должен applied'нуться неявно, кисть рисует поверх.
- [ ] Drag → Esc → откатывается, marquee остаётся.

**Selection — fill / flip / rotate:**
- [ ] Marquee → G (FillSelection) текущим цветом.
- [ ] Marquee → Flip H, Flip V.
- [ ] Marquee → Rotate 90.

**Selection — clipboard / paste / AI:**
- [ ] Ctrl+C / Ctrl+V — paste → сразу transform-режим.
- [ ] Ctrl+V → Ctrl+Z — paste откатывается полностью.
- [ ] AI ExtractObject → создаёт marquee.

**Multi-target:**
- [ ] Активный marquee → переключение на другой слой / фрейм → marquee
      гасится / коммитится корректно (`SetTarget` с sameTarget=false).

## Риски и митигации

1. **`_backgroundBitmap` дёргают оба** (drawing-блок и selection-блок). Возможен
   рассинхрон, если порядок clear/assign перепутается. Митигация: сохраняем
   побитово идентичный порядок операций. Делать diff на behavior, не на структуру.

2. **`_pixelsLifted` — единственное место истины** для `SelectionPhase`. После
   переезда нода не знает, lifted/not. Митигация: `SelectionPhase` доступен через
   фасад, нода читает его, а не флаг.

3. **События** — если хоть одна обёртка-фасад потеряет подписку (например, забыли
   `remove`), будут утечки. Митигация: использовать паттерн `add => …; remove => …;`
   ровно — не лепить вручную делегаты.

4. **`SelectionOperation.GetEditedNodes`** возвращает `_drawingLayer`. Это **не**
   меняется — нода остаётся узлом сцены. Контроллер — не `SKNode`.

5. **Pointer routing остаётся на ноде**, и в нём логика типа «если в `Drawing` — то
   `DrawStroke`, иначе если `DrawingSelectionArea` — то `AddSelectionPoint`». Это
   нормально для данной итерации — главное чтобы вызовы шли в контроллер по фасаду.

6. **Hot-reload** (`MetadataUpdateHandler`) — добавление нового класса не должно
   ломать hot-reload, но если что-то сломается — это первая подозреваемая зона.
   Митигация: после шага 1 запустить debug-сборку и убедиться, что hot-reload
   подхватывает изменения в ноде.

7. **transform-tool ветка** в активной разработке. Если в неё мержатся ещё фиксы
   параллельно, рефакторинг даст конфликты в селекшен-блоке. Митигация: делать
   рефакторинг быстро (6 коммитов в один день), чтобы не плодить багажа.

## Acceptance

После шага 6:

- `DrawingLayerNode.cs` ≤ 800 строк.
- `SelectionController.cs` 600-700 строк.
- `IDrawingLayer` без изменений; ни один внешний потребитель не правится.
- `SelectionOperation` принимает `DrawingLayerNode` без изменений.
- Все пункты smoke-чек-листа проходят.
- `git log --stat` показывает 6 атомарных коммитов, каждый компилируется и проходит
  smoke по своему скоупу.

## Что дальше (не входит в этот план)

После того как селекшен изолирован — браться за **`StrokeRenderer`** (статический
класс над `IDrawingLayer` + `IPixelBrush`, чистые функции по `DrawStroke`/`DrawLine`/…),
и затем за **`PointerInputRouter`** (стратегии per `BrushDrawingMode` + deferred touch).
Эти два рефакторинга в отдельных планах, когда selection-controller осядет.
