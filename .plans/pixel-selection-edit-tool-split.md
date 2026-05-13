# Pixel selection / edit tool split

## Goal

Разделить «выделение» и «редактирование выделения» на два независимых инструмента,
чтобы:

1. Selection tools (Rect / Lasso / Color) **никогда не модифицируют** содержимое
   `DrawingTarget`. Они только формируют marquee (`_selectionLayer` + `_pixelSelector`)
   и могут двигать/менять его контур.
2. Перемещение и трансформация выделенных пикселей выполняется в отдельном
   инструменте `PixelTransformTool`. Только он «поднимает» пиксели (`_pixelsLifted = true`),
   только он пушит операции, изменяющие `DrawingTarget` из контекста выделения.
3. Undo/redo восстанавливают активный инструмент в момент создания операции, поэтому
   состояние UI и `DrawingLayerNode` после Ctrl+Z всегда согласовано.

## Текущее состояние (что сломано)

- `PixelSelectToolBase.Deactivate()` ([PixelSelectToolBase.cs:89-100]) тихо коммитит
  выделение через `DrawingLayer.ApplySelection()` — без отдельной операции в undo стеке.
- `SelectionOperation.OnPerformUndo` ([SelectionOperation.cs:51-56]) восстанавливает
  selection layer и вызывает `SetSelection`, который повторно активирует редактор.
  Сценарий: «выделил → подвинул → переключился на кисть → Ctrl+Z» → редактор выделения
  снова виден, но активный инструмент — кисть. Рассинхрон.
- `PixelSelectToolBase.OnOperationInvoked` ([PixelSelectToolBase.cs:71-87]) гасит
  selection editor на любую операцию, которая не SelectionOperation / PasteOperation /
  MoveOperation — это hack, который маскирует часть проблем и сам по себе создаёт
  странные эффекты при undo.

## Новая архитектура

### Состояния и режимы DrawingLayerNode

`DrawingLayerNode` остаётся центральным, но получает чёткие, **взаимоисключающие** режимы
`SelectionPhase` (новый enum) на основе уже существующего `DrawingLayerState`:

- `Idle` — ничего не делаем.
- `Drawing` — кисть/перо рисуют (как сейчас).
- `MarqueeDefining` — пользователь активно рисует marquee (был
  `DrawingLayerState.DrawingSelectionArea`).
- `MarqueeReady` — marquee создан, contour-only, **пиксели НЕ подняты**.
  Это финальное состояние работы selection-инструментов.
- `Transforming` — пиксели подняты (`_pixelsLifted = true`), активен
  `FrameEditorNode` в полном режиме. В этот режим попадаем только из
  `PixelTransformTool`.

Переходы:
- `Idle → MarqueeDefining → MarqueeReady` — внутри selection-инструмента.
- `MarqueeReady → Transforming` — на `Activate()` `PixelTransformTool`.
- `Transforming → Idle` (commit) или `Transforming → MarqueeReady` (cancel) — на
  `Deactivate()` `PixelTransformTool`.
- `MarqueeReady → Idle` — пользователь начал новое marquee или подтвердил «снять
  выделение».

### Новые/изменённые операции

1. `BeginSelectionOperation` (новая) — push в `FinishSelection` / `SelectAll`.
   - Состояние: `DrawingTarget.GetData()` ДО создания выделения, описание marquee
     (path / bitmap / position).
   - Undo → снимает marquee, восстанавливает `DrawingTarget` к initial state.
   - Redo → ставит marquee обратно (без поднятия пикселей), переключает на тот
     selection-инструмент, что был активен (см. ниже про tool restoration).

2. `TransformSelectionOperation` — преемник текущего `SelectionOperation`,
   но push'ит только `PixelTransformTool`.
   - Initial / final state: `SKNodeTransformState` selection layer.
   - На Undo восстанавливает прежний `SKNodeTransformState`; **не активирует** editor
     сам — это сделает tool restoration.

3. `ApplyTransformOperation` (новая) — push при commit'е (`Apply` button / переход на
   другой tool / Enter).
   - Состояние: `DrawingTarget.GetData()` до и после применения working bitmap.
   - Undo → восстанавливает данные target'а и возвращает в `Transforming` с теми же
     pre-apply координатами selection layer.

4. `PasteOperation` — оставляем, корректируем, чтобы в `OnPerform` активировался
   `PixelTransformTool` (а не `PixelSelectToolBase`, как сейчас в [PasteOperation.cs:49]).

5. `SelectionOperation` (старый) — удаляем после миграции.

Все три операции реализуют новый интерфейс `IToolAwareOperation`:

```csharp
interface IToolAwareOperation : IEditOperation
{
    string ToolKeyBeforeOperation { get; }   // tool который был активен ДО push
    string ToolKeyAfterOperation { get; }    // tool который должен быть после redo / push
}
```

`ToolKeyBeforeOperation` снимается в момент создания операции, `ToolKeyAfterOperation` —
в момент `SetFinalState()`. `OperationService` после undo/redo проверяет
`IToolAwareOperation` и через `IToolService.ActivateTool(key)` переключает инструмент.

### Новый инструмент: PixelTransformTool

Файл: `Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelSelect/PixelTransformTool.cs`.

```
[Pix2dTool(EditContextType = EditContextType.Sprite,
           HasSettings = true,
           SettingsViewType = typeof(SelectionTransformToolSettingsView),
           DisplayName = "Transform selection",
           Group = "Pixel Select",
           HotKey = "T")]
```

Поведение:
- `Activate()`:
  - Если `DrawingLayer.HasSelection == false` — fallback: активирует
    `PixelSelectRectTool` (или последний selection-tool). Защита от случайного
    переключения «в пустоту».
  - Иначе: `DrawingLayer.LiftSelectionFromCanvas()` + `ActivateEditor(contourOnly: false)`
    (если ещё не подняты), переходит в фазу `Transforming`. Если редактор уже виден в
    contour mode — лифтит, переходит в full mode.
- Не реагирует на `OnPointerPressed` напрямую — все жесты по marquee обрабатывает
  `FrameEditorNode`. Inputs тула обслуживают только клики **вне** selection bounds:
  один такой клик = commit + deactivate.
- `Deactivate()`:
  - Если переключение на selection-инструмент И marquee не двигали — cancel
    (`CommitWorkingBitmapToCanvas` нужен только если есть изменения).
  - Если переключение на drawing-инструмент или другой не-selection — commit,
    `ApplyTransformOperation` идёт в стек.
- Settings panel: Flip H/V, Rotate 90°, Apply, Cancel, Crop. Большая часть кнопок
  уезжает из текущего `ClipboardActionsView` сюда.

### Изменения в PixelSelectToolBase

- Убираем `OnOperationInvoked` целиком — больше не нужно.
- `Activate()`: ставит `BrushDrawingMode.Select`, но **не** подписывается на
  `PixelsSelected`/`SelectionStarted` для побочных эффектов — это просто marquee tool.
- `Deactivate()`:
  - Если фаза `MarqueeReady` и переключаемся на `PixelTransformTool` — НИЧЕГО не
    коммитим, просто отпускаем (фаза остаётся).
  - Иначе — `DiscardMarquee()` (новый метод на `IDrawingLayer`, аналог
    `DeactivateSelectionEditor`, но без коммита — он и сейчас не коммитит в
    contour mode, см. [DrawingLayerNode.cs:1208-1227]). Это просто скрывает marquee.
- `AutoEnterTransformMode` removed: пользователь сам выбирает, заходить ли в
  трансформацию. Альтернатива (опциональная) — оставить флаг с новым смыслом: на
  `FinishSelection` автоматически вызвать `ToolService.ActivateTool<PixelTransformTool>()`.
  Я бы оставил флаг с этим смыслом и default = true, чтобы сохранить текущий UX по
  умолчанию.

### Изменения в DrawingLayerNode

- Ввести `SelectionPhase` (см. выше) — наблюдаемое property для тулов.
- Метод `DiscardMarquee()` — sibling к `ApplySelection`, для случая когда мы хотим
  убрать marquee без коммита (используется в `PixelSelectToolBase.Deactivate`).
- `ApplySelection(bool saveToUndo)` остаётся, но вызывается **только** из
  `PixelTransformTool`.
- Если marquee находится в `MarqueeReady` и пользователь начал новое — старый marquee
  просто исчезает (вызовом `DiscardMarquee`), новой операцией это не оформляется
  (старая операция в стеке остаётся валидной, поскольку она не изменяла target).
- `SetSelection(...)` (вход для undo / paste) больше **не вызывает** `ActivateEditor`
  сам. Это делает tool restoration: операция возвращает активный инструмент, инструмент
  в своём `Activate()` восстанавливает editor.

### Изменения в ClipboardActionsView

- Убираем `AutoEnterTransformMode` тогглбатон (либо переименовываем смысл — см. выше).
- Кнопка «Transform» (или нажатие на selection target) переключает на
  `PixelTransformTool`.
- Кнопки Flip / Rotate / Apply / Cancel переезжают в `SelectionTransformToolSettingsView`
  (новый файл) и видны только когда активен `PixelTransformTool`.
- Copy / Cut / Paste / Crop остаются в `ClipboardActionsView` — они работают на любом
  selection-tool/transform-tool.

### Изменения в OperationService

- При `Undo()` и `Redo()`: если `_currentOperation is IToolAwareOperation toolOp`,
  после вызова `OnPerformUndo` / `OnPerform` дернуть
  `IToolService.ActivateTool(toolKey)` (для Undo — `ToolKeyBeforeOperation`, для
  Redo — `ToolKeyAfterOperation`).
- `IToolService` нужно прокинуть в `OperationService` — добавить в конструктор.
  `OperationService` уже singleton в DI, регистрируется в `Pix2dBootstrapperDI`.

## Файлы, которые правим

Новые:
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelSelect/PixelTransformTool.cs`
- `Sources/Core/Pix2d.Core/Plugins/Drawing/UI/SelectionTransformToolSettingsView.cs`
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/BeginSelectionOperation.cs`
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/TransformSelectionOperation.cs`
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/ApplyTransformOperation.cs`
- `Sources/Core/Pix2d.Shared/Abstract/Operations/IToolAwareOperation.cs`

Меняем:
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Nodes/DrawingLayerNode.cs`
   - Ввести `SelectionPhase`, метод `DiscardMarquee`, разделить `ApplySelection` /
     `DiscardMarquee`, убрать авто `ActivateEditor` из `SetSelection`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelSelect/PixelSelectToolBase.cs`
   - Убрать `OnOperationInvoked`, поправить `Activate`/`Deactivate`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Services/DrawingService.cs`
   - `DrawingLayerSelectionTransformed` пушит `TransformSelectionOperation` только
     если активный tool — `PixelTransformTool`. Иначе игнорируем (не должно случаться).
   - `FinishSelection` пушит `BeginSelectionOperation`.
- `Sources/Core/Pix2d.Core/Services/OperationService.cs`
   - Tool restoration после Undo/Redo.
- `Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs`
   - Инжект `IToolService` в `OperationService`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/DrawingPlugin.cs`
   - Регистрация `PixelTransformTool`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/UI/ClipboardActionsView.cs`
   - Перенос Flip/Rotate/Apply, удаление `AutoEnterTransformMode`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/PasteOperation.cs`
   - В `OnPerform` активирует `PixelTransformTool`.
- `Sources/Core/Pix2d.Core/Plugins/Drawing/Operations/SelectionOperation.cs`
   - Удалить после миграции.
- `Sources/Core/Pix2d.Shared/State/SpriteEditorState.cs`
   - Либо удалить `AutoEnterTransformMode`, либо переосмыслить (см. выше).
- `Sources/Core/Pix2d.Shared/Abstract/Drawing/IDrawingLayer.cs`
   - Добавить `DiscardMarquee`, `SelectionPhase`.

## Этапы реализации

Каждый этап оставляет проект в собираемом и работоспособном виде.

### Этап 1. Подготовка фундамента (без изменений UX)
- Ввести `SelectionPhase` enum + property на `DrawingLayerNode`, обновлять его в
  существующих переходах. Не использовать пока в логике.
- Ввести `IToolAwareOperation`, реализовать в существующем `SelectionOperation` и
  `PasteOperation` (snapshot активного tool). Tool restoration в `OperationService`.
- **Проверка:** undo транзакции trasnform-операций после переключения инструмента
  возвращает корректный инструмент. Прочее не сломано.

### Этап 2. Новый инструмент + миграция операций
- Создать `PixelTransformTool` и `SelectionTransformToolSettingsView`. Зарегистрировать.
- Создать `BeginSelectionOperation`, `TransformSelectionOperation`,
  `ApplyTransformOperation`. Помечаем `SelectionOperation` как `[Obsolete]`.
- `DrawingService.FinishSelection`-флоу: push `BeginSelectionOperation`.
- `DrawingService.DrawingLayerSelectionTransformed`: push
  `TransformSelectionOperation` вместо старой.
- `PixelTransformTool.Deactivate(commit)`: push `ApplyTransformOperation`.
- `PasteOperation.OnPerform`: переключение на `PixelTransformTool`.
- **Проверка:** базовые сценарии selection → transform → undo/redo работают
  стабильно; selection-tools остаются без побочных эффектов на target.

### Этап 3. Чистка
- Удалить `SelectionOperation`, `AutoEnterTransformMode` (или переосмыслить).
- Убрать `OnOperationInvoked` из `PixelSelectToolBase`.
- Удалить `ActivateEditor` из `SetSelection` (теперь это работа tool restoration).
- Перенести Flip/Rotate/Apply из `ClipboardActionsView` в settings нового tool.
- **Проверка:** прогон всех сценариев — Rect / Lasso / Color selection, paste,
  copy/cut, fill, transform, mass undo/redo, переключения tool в любой момент.

## Тест-сценарии (manual QA — нет automated test project)

1. Rect select → drag marquee в contour mode → переключение на кисть → Ctrl+Z.
   Ожидание: marquee исчезает, активный tool — Rect Select.
2. Rect select → transform → resize → переключение на кисть → Ctrl+Z (×3).
   Ожидание: первое Z отменяет apply (marquee опять «поднят»), второе Z отменяет
   resize, третье Z снимает marquee. На каждом шаге активный tool корректен.
3. Lasso → переключение на Rect → переключение на Transform.
   Ожидание: marquee, созданный лассо, поднимается в transform; ничего не теряется.
4. Paste → marquee автоматически в transform tool → переместить → Ctrl+Z.
   Ожидание: undo paste возвращает к pre-paste состоянию, transform tool деактивирован.
5. Rect select → transform → Esc.
   Ожидание: marquee исчезает, target вернулся в исходное.
6. Mass undo/redo (10+ операций смешанных типов).
   Ожидание: история стабильна, инструмент в каждый момент совпадает с состоянием.

## Риски

- Hot-reload в DEBUG может маскировать ошибки в DI; убедиться, что `PixelTransformTool`
  и операции корректно резолвятся.
- `NodeSerializer.ExtraAssemblies` — если новый tool кладёт в проект какие-то новые
  типы нод, добавить assembly. Сейчас не предвидится (`SpriteSelectionNode` уже
  существует).
- Touch-флоу (`_deferredTouchSelectionStart`) — проверить, что не ломается, поскольку
  он живёт в `DrawingLayerNode.OnPointerPressed` и завязан на `_drawingMode == Select`.
  В новом раскладе он остаётся в selection-tools.

## Out of scope (отдельные задачи)

- Кропу (`SpriteEditCommands.CropPixels`) текущее поведение оставляем как есть; он не
  трогает transform-флоу.
- Pixel-text plugin — не затрагиваем.
