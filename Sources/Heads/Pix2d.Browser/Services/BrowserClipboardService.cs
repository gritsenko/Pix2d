using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Browser.Services;

public class BrowserClipboardService(
    IDrawingService drawingService,
    IViewPortService viewPortService,
    IDialogService dialogService,
    AppState appState)
    : BaseAvaloniaClipboardService(drawingService, viewPortService, dialogService, appState)
{
    protected override IClipboard? Clipboard => EditorApp.TopLevel?.Clipboard;
}
