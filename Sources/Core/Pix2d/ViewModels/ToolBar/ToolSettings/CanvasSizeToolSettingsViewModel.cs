using System.Windows.Input;
using Pix2d.Abstract;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Services;
using SkiaNodes.Abstract;
using SkiaSharp;

namespace Pix2d.ViewModels.ToolSettings
{
    public class CanvasSizeToolSettingsViewModel : ToolSettingsBaseViewModel
    {
        private IContainerNode _container;
        public ISelectionService SelectionService { get; }
        public IEditService EditService { get; }

        public double Width
        {
            get => Get<double>();
            set => Set(value);
        }
        public double Height
        {
            get => Get<double>();
            set => Set(value);
        }

        public ICommand ApplySizeCommand => GetCommand(() => EditService.Resize(_container, new SKSize((int) Width, (int) Height)));

        public CanvasSizeToolSettingsViewModel(ISelectionService selectionService, IEditService editService)
        {
            SelectionService = selectionService;
            EditService = editService;
            LoadData();
        }

        private void LoadData()
        {
            _container = SelectionService.GetActiveContainer();
            Width = _container.Size.Width;
            Height = _container.Size.Height;
        }
    }
}