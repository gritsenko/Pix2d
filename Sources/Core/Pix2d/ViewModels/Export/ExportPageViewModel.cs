using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract.UI;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.ViewModels.MainMenu;
using Pix2d.ViewModels.Preview;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.ViewModels.Export
{
    public class ExportPageViewModel : Pix2dViewModelBase
    {
        private ExporterViewModel _selectedExporter;

        public IExportService ExportService { get; }
        public ILicenseService LicenseService { get; }
        public IProjectService ProjectService { get; }
        public IBusyController BusyController { get; }
        public AppState AppState { get; }
        public IViewModelService ViewModelService { get; }
        public ISettingsService SettingsService { get; }

        public bool IsSharing
        {
            get => Get<bool>();
            set => Set(value);
        }

        public SKBitmapObservable Preview { get; } = new SKBitmapObservable();
        public double PreviewWidth
        {
            get => Get<double>();
            set => Set(value);
        }
        public double PreviewHeight
        {
            get => Get<double>();
            set => Set(value);
        }

        public string Author
        {
            get => Get<string>();
            set
            {
                if (Set(value))
                {
                    SettingsService.Set(SettingsConstants.ShareToGalleryAuthor, value);
                }
            }
        }

        public string Title
        {
            get => Get<string>();
            set => Set(value);
        }

        public string Email
        {
            get => Get<string>();
            set
            {
                if (Set(value))
                {
                    SettingsService.Set(SettingsConstants.ShareToGalleryEmail, value);
                }
            }
        }

        public bool EnableWatermark => LicenseService != null && !LicenseService.IsPro && LicenseService.AllowBuyPro;

        public List<ExporterViewModel> Exporters { get; set; }

        public ExporterViewModel SelectedExporter
        {
            get => _selectedExporter;
            set
            {
                if (_selectedExporter is PngSpritesheetExporterViewModel oldSps)
                    oldSps.ColumnsChanged -= ExportSettingsViewModel_ColumnsChanged;

                _selectedExporter = value;

                if (_selectedExporter == null)
                    return;

                _selectedExporter.OnSelected();

                if (_selectedExporter is PngSpritesheetExporterViewModel sps)
                {
                    sps.Columns = ExportSettingsViewModel.CalculateColumns();
                    ExportSettingsViewModel.SetSpritesheetColumns(sps.Columns);
                    sps.ColumnsChanged += ExportSettingsViewModel_ColumnsChanged;
                }
                else
                {
                    ExportSettingsViewModel.SetSpritesheetColumns(0);
                }
                UpdatePreview();
                OnPropertyChanged();
            }
        }

        public ExportSettingsViewModel ExportSettingsViewModel { get; set; }

        public IRelayCommand ExportCommand => GetCommand(OnExportCommandExecute);

        [UpdateCanExecute(nameof(IsSharing))]
        public IRelayCommand ShareCommand => GetCommand(OnShareCommandExecute, () => !IsSharing);
        public IRelayCommand CancelCommand => GetCommand(OnCancelCommandExecute);
        public IRelayCommand RefreshCommand => GetCommand(UpdatePreview);

        public ICommand ShowLicenseInfoCommand => GetCommand(() =>
        {
            Logger.Log("$Pressed on watermark alert on export page");
            Commands.View.ShowMainMenuCommand.Execute();
            CloseDialog();
            var mainMenu = ViewModelService.GetViewModel<MainMenuViewModel>();
            var licenseItem = mainMenu.MenuItems.FirstOrDefault(x => x.DetailsViewModel == typeof(LicenseViewModel));
            mainMenu.ItemSelectCommand.Execute(licenseItem);
        });

        public ExportPageViewModel(ISettingsService settingsService, IExportService exportService, ILicenseService licenseService, IProjectService projectService, IBusyController busyController, AppState appState, IMessenger messenger, IViewModelService viewModelService)
        {
            ExportService = exportService;
            LicenseService = licenseService;
            ProjectService = projectService;
            BusyController = busyController;
            AppState = appState;
            ViewModelService = viewModelService;
            SettingsService = settingsService;

            if (LicenseService != null)
                LicenseService.LicenseChanged += LicenseService_LicenseChanged;

            messenger.Register<ProjectLoadedMessage>(this, m =>
            {
                ExportSettingsViewModel.Scale = 1;
                ExportSettingsViewModel.SetBounds(GetNodesToExport().ToArray().GetBounds());
            });

            messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(x => x.ShowExportDialog, () =>
            {
                if (AppState.UiState.ShowExportDialog)
                    Load();
            }));
        }

        private void LicenseService_LicenseChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(EnableWatermark));
            UpdatePreview();
        }

        protected override void OnLoad()
        {
            Author = SettingsService.Get<string>(SettingsConstants.ShareToGalleryAuthor);
            Email = SettingsService.Get<string>(SettingsConstants.ShareToGalleryEmail);
            Title = AppState.CurrentProject.Title;

            ExportSettingsViewModel = ViewModelService.GetViewModel<ExportSettingsViewModel>();
            ExportSettingsViewModel.SetBounds(GetNodesToExport().ToArray().GetBounds());
            ExportSettingsViewModel.SettingsChanged += ExportSettingsViewModel_SettingsChanged;
            ExportCommand.RaiseCanExecuteChanged();

            Exporters = new List<ExporterViewModel>()
            {
                ViewModelService.GetViewModel<PngExporterViewModel>(),
                ViewModelService.GetViewModel<PngSequenceExporterViewModel>(),
                ViewModelService.GetViewModel<GifExporterViewModel>(),
                ViewModelService.GetViewModel<PngSpritesheetExporterViewModel>(),
                ViewModelService.GetViewModel<SvgExporterViewModel>(),
            };

            SelectedExporter = Exporters.FirstOrDefault();

            if (AppState.UiState.ShowExportDialog)
            {
                CoreServices.ToolService.ActivateDefaultTool();
                UpdatePreview();
            }
        }

        private void ExportSettingsViewModel_SettingsChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void ExportSettingsViewModel_ColumnsChanged(object sender, EventArgs e)
        {
            if (_selectedExporter is PngSpritesheetExporterViewModel sps)
            {
                ExportSettingsViewModel.SetSpritesheetColumns(sps.Columns);
                UpdatePreview();
            }
        }

        private void OnCancelCommandExecute()
        {
            CloseDialog();
        }

        private async void OnExportCommandExecute()
        {
            await BusyController.RunLongTaskAsync(async () =>
            {
                try
                {
                    Logger.LogEventWithParams("Exporting image", new Dictionary<string, string> { { "Exporter", SelectedExporter.Name } });

                    var nodesToExport = GetNodesToExport();
                    await SelectedExporter.Export(nodesToExport, ExportSettingsViewModel.GetSettings());

                    CloseDialog();
                }
                catch (OperationCanceledException canceledException)
                {
                    //export canceled, just return to dialog
                }
            });
        }

        private async void OnShareCommandExecute()
        {
            try
            {
                IsSharing = true;

                var result = await ShareToGallery(ExportSettingsViewModel.GetSettings(), Author, Title, Email);

                if (result != null)
                {
                    GetService<IPlatformStuffService>().OpenUrlInBrowser(result);
                    //await Windows.System.Launcher.LaunchUriAsync(new Uri(result));

                    CloseDialog();
                }

                SettingsService.Set(SettingsConstants.ShareToGalleryAuthor, Author);
                SettingsService.Set(SettingsConstants.ShareToGalleryEmail, Email);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
            finally
            {
                IsSharing = false;
            }

        }

        private async Task<string> ShareToGallery(ImageExportSettings imageExportSettings, string author, string title, string email)
        {
            var nodesToExport = GetNodesToExport();

            using (var stream = await SelectedExporter.ExportToStream(nodesToExport, ExportSettingsViewModel.GetSettings()))
            {
                return await UploadFile(stream, ExportImageFormat.Png, author, title, email);
            }

        }

        //private async void Share(Stream fileContentStream, ExportImageFormat type, string author, string title, string email)
        //{
        //    var  tempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("title.png", CreationCollisionOption.ReplaceExisting);
        //    await rmbp.SaveStorageFile(tempFile);
        //    _tempExportFile = tempFile;

        //    DataTransferManager.ShowShareUI();
        //}

        private IEnumerable<SKNode> GetNodesToExport()
        {
            var node = CoreServices.EditService.CurrentEditedNode;
            if (node == null)
                yield break;

            yield return node;

            if (node.Size.Width * ExportSettingsViewModel.Scale < 64)
            {
                yield break;
            }

            if (EnableWatermark) yield return GetWatermarkNode(node);

            //yield return GetGridNode(node);
        }

        private SKNode GetGridNode(SKNode exportedNode)
        {
            var grid = new GridNode();

            var exportedNodeSize = exportedNode.Size;

            var w = exportedNodeSize.Width * ExportSettingsViewModel.Scale;
            var h = exportedNodeSize.Height * ExportSettingsViewModel.Scale;
            grid.Size = new SKSize(exportedNodeSize.Width, exportedNodeSize.Height);

            grid.CellSize = new SKSize(1, 1);
            return grid;
        }

        private SKNode GetWatermarkNode(SKNode exportedNode)
        {
            var watermark = new Pix2dWatermarkNode();

            var exportedNodeSize = exportedNode.Size;

            var w = exportedNodeSize.Width * ExportSettingsViewModel.Scale;
            var h = exportedNodeSize.Height * ExportSettingsViewModel.Scale;
            if (w >= 200 && h >= 200)
            {
                watermark.Size = new SKSize(64f / ExportSettingsViewModel.Scale, 64f / ExportSettingsViewModel.Scale);

                var offset = 8f / ExportSettingsViewModel.Scale;
                watermark.Position = new SKPoint(exportedNodeSize.Width - watermark.Size.Width - offset, exportedNodeSize.Height - watermark.Size.Height - offset);
            }
            else
            {
                watermark.FontSize = 14f / (ExportSettingsViewModel == null || ExportSettingsViewModel.Scale == 0 ? 1 : ExportSettingsViewModel.Scale);
                watermark.Size = new SKSize(exportedNodeSize.Width, watermark.FontSize + 2f);
                watermark.Position = new SKPoint(exportedNode.Position.X, exportedNode.Position.Y + exportedNodeSize.Height);
                watermark.Effects = null;
            }

            return watermark;
        }

        public async Task<string> UploadFile(Stream fileContentStream, ExportImageFormat type, string author, string title, string email)
        {

            //content.Add(new StringContent("Test art"), "title");
            //content.Add(new StreamContent(stream.AsStream()), "image", "test.png");

            //var result = await client.PostAsync("http://localhost:65210/api/Share", content);

            using (var client = new HttpClient())
            {
                using (var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    content.Add(new StringContent(author), "author");
                    content.Add(new StringContent(title), "title");
                    content.Add(new StringContent(email ?? "no@email.com"), "email");

                    //var isPro = await LicenseService.CheckIsPro();

                    content.Add(new StringContent("pix2d"), "license");

                    content.Add(new StreamContent(fileContentStream), "image", "upload." + (type == ExportImageFormat.Png ? "png" : "gif"));

                    //using (var message = await client.PostAsync("http://localhost:65210/api/Share", content))
                    //using (var message = await client.PostAsync("http://pixelart.studio/api/Share", content))
                    using (var message = await client.PostAsync("http://gallery.pix2d.com/api/Share", content))
                    {
                        var input = await message.Content.ReadAsStringAsync();
                        input = input.Trim("\"".ToCharArray());

                        return !string.IsNullOrWhiteSpace(input) ? Regex.Match(input, @"http://gallery.pix2d.com/.*").Value : null;
                    }
                }
            }
        }

        private void UpdatePreview()
        {
            var settings = ExportSettingsViewModel?.GetSettings();
            var editorService = CoreServices.EditService;

            if (!(editorService.GetCurrentEditor() is SpriteEditor spriteEditor) || settings == null)
                return;

            var nodesToExport = GetNodesToExport();
            var preview = nodesToExport.ToArray()
                .RenderToBitmap(
                    spriteEditor.CurrentSprite.UseBackgroundColor ? spriteEditor.CurrentSprite.BackgroundColor : SKColor.Empty,
                    settings.Scale);

            PreviewWidth = preview.Width;
            PreviewHeight = preview.Height;

            Preview.SetBitmap(preview);
        }

        public void CloseDialog()
        {
            Commands.View.HideExportDialogCommand.Execute();
        }

        public void SelectExporterByFileType(ExportImportProjectType fileType)
        {
            if (fileType == ExportImportProjectType.Png)
            {
                SelectedExporter = Exporters.FirstOrDefault(x => x is PngExporterViewModel);
            }

            if (fileType == ExportImportProjectType.Gif)
            {
                SelectedExporter = Exporters.FirstOrDefault(x => x is GifExporterViewModel);
            }
        }
    }

    public enum ExportImageFormat
    {
        Png,
        Gif
    }
}