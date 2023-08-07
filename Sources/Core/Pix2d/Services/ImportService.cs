using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Importers;
using SkiaSharp;
using System;
using System.Linq;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Operations;
using Pix2d.Plugins.Sprite.Editors;
using SkiaNodes.Common;
using SkiaNodes.Extensions;

namespace Pix2d.Services
{
    public class ImportService : IImportService
    {
        private readonly Dictionary<string, Func<IImporter>> _importerProviders = new();
        

        private IFileService FileService { get; }
        public IObjectCreationService ObjectCreationService { get; }
        public IDrawingService DrawingService { get; }
        public IToolService ToolService { get; }
        public IDialogService DialogService { get; }
        public AppState AppState { get; }

        public ImportService(IFileService fileService, IObjectCreationService objectCreationService, IDrawingService drawingService,
            IToolService toolService, IDialogService dialogService, AppState appState)
        {
            FileService = fileService;
            ObjectCreationService = objectCreationService;
            DrawingService = drawingService;
            ToolService = toolService;
            DialogService = dialogService;
            AppState = appState;


            RegisterImporterProvider(".png", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".jpg", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".jpeg", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".gif", () => new GifImporter());
            RegisterImporterProvider(".PNG", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".JPG", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".JPEG", () => new ImageFileImporter(objectCreationService));
            RegisterImporterProvider(".GIF", () => new GifImporter());
        }

        public async void ImportToScene()
        {
            var extensions = _importerProviders.Keys.ToArray();
            var files = (await FileService.OpenFileWithDialogAsync(extensions, true, "import"));
            if(files?.Any() != true)
                return;
            
            await ImportToScene(files);

            CoreServices.ViewPortService.Refresh();
        }

        public async Task ImportToScene(IEnumerable<IFileContentSource> files)
        {
            SessionLogger.OpLog(string.Join(", ", files.Select(x => x.Path)));

            try
            {
                if (!files.Any())
                    return;

                if (AppState.CurrentProject.CurrentContextType == EditContextType.Sprite)
                {
                    await ImportToSprite(files);
                    return;
                }

                var importer = new ImageFileImporter(ObjectCreationService);
                await importer.ImportFromFiles(files);
            }
            catch (Exception ex)
            {
                var fileStr = "";
                var msg = "";
                if (files.Any())
                {
                    fileStr = string.Join(",", files.Select(x=>x.Title));
                    msg = "Can't import file(s) " + fileStr + ". Error: " + ex.Message;
                }
                Logger.LogException(ex, msg, fileStr);
                DialogService.Alert("Can't import file(s) " + fileStr + ". Error: " + ex.Message, "Error!");
            }
        }

        public void RegisterImporterProvider(string extension, Func<IImporter> importerProviderFunc)
        {
            _importerProviders[extension] = importerProviderFunc;
        }

        public async Task TryImportToSprite(IImportTarget targetSprite, params IFileContentSource[] files)
        {
            if (files != null)
                SessionLogger.OpLog(string.Join(", ", files.Select(x => x.Path)));
            
            try
            {
                if (_importerProviders.TryGetValue(files[0].Extension.ToLower(), out var importerProvider))
                {
                    var importer = importerProvider();
                    await importer.ImportToTargetNode(files, targetSprite);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public bool CanImport(string fileExtension)
        {
            return _importerProviders.Any(x => x.Key.EndsWith(fileExtension.ToLower()));
        }

        private async Task ImportToSprite(IEnumerable<IFileContentSource> files)
        {
            if(!(AppState.CurrentProject.CurrentNodeEditor is SpriteEditor spriteEditor))
                return;
            
            var bitmaps = new List<SKBitmap>();
            foreach (var fileContentSource in files)
            {
                using (var fileStream = await fileContentSource.OpenRead())
                {
                    var bitmap = fileStream.ToSKBitmap();
                    if (bitmap != null)
                        bitmaps.Add(bitmap);
                }
            }

            if (!bitmaps.Any())
                return;

            var importOperation = new BulkEditOperation();

            var maxW = bitmaps.Max(x => x.Width);
            var maxH = bitmaps.Max(x => x.Height);

            var bm = bitmaps.FirstOrDefault();

            var currentSprite = spriteEditor.CurrentSprite;

            if (currentSprite.Size.Width < maxW
                || currentSprite.Size.Height < maxH)
            {

                var result = await DialogService.ShowYesNoDialog(
                    "Imported image size is bigger then the current canvas size. Resize current canvas?", "Resize suggestion", "Yes", "No");
                if (result)
                {
                    if (currentSprite.Parent is ArtboardNode artboard)
                    {
                        var resizeOperation = new ResizeOperation(artboard.Yield());
                        artboard.Resize(new SKSize(maxW, maxH), 0, 0);
                        resizeOperation.SetFinalData();

                        importOperation.AddSubOperation(resizeOperation);
                    }

                    spriteEditor.Resize(maxW, maxH);

                    if (!importOperation.Empty)
                    {
                        importOperation.PushToHistory();
                    }
                }
            }

            new PasteOperation(bm, SKPoint.Empty).Invoke();
            //await spriteEditor?.ImportImages(bitmaps, SpriteImportMode.ToCurrentLayer);

            // DrawingService.UpdateDrawingTarget();
        }
    }
}