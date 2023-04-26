using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;
using SkiaSharp;

namespace Pix2d.ViewModels.MainMenu
{
    public class NewDocumentSettingsViewModel : MenuItemDetailsViewModelBase
    {
        public IProjectService ProjectService { get; }
        public IMenuController MenuController { get; }

        public int Width
        {
            get => Get<int>();
            set => Set(value);
        }

        public int Height
        {
            get => Get<int>();
            set => Set(value);
        }


        public ObservableCollection<NewDocumentSettingsPresetViewModel> AvailablePresets { get; set; } = new ObservableCollection<NewDocumentSettingsPresetViewModel>();

        public NewDocumentSettingsPresetViewModel SelectedPreset
        {
            get => Get<NewDocumentSettingsPresetViewModel>();
            set
            {
                if (Set(value))
                {
                    Width = value?.Width ?? 64;
                    Height = value?.Height ?? 64;
                }
            }
        }

        public ICommand CreateNewArtCommand => GetCommand(() =>
        {
            MenuController.ShowMenu = false;
            ProjectService.CreateNewProjectAsync(new SKSize(Width, Height));
        });

        public NewDocumentSettingsViewModel(IProjectService projectService, IMenuController menuController)
        {
            ProjectService = projectService;
            MenuController = menuController;
        }

        protected override void OnLoad()
        {
            var bounds = Pix2DApp.Instance.ViewPort?.Size ?? new SKSize(64, 64);
            var viewportWidth = (int)bounds.Width;
            var viewportHeight = (int)bounds.Height;

            if (!AvailablePresets.Any())
            {
                AddPreset();
                AddPreset(16, 16);
                AddPreset(32, 32);
                AddPreset(48, 48);
                AddPreset(64, 64);
                AddPreset(128, 128);
                AddPreset(256, 256);
                AddPreset(512, 512);
                AddPreset(viewportWidth, viewportHeight, $"{viewportWidth}x{viewportHeight} (Viewport size)");

                SelectedPreset = AvailablePresets[4];

                Width = SelectedPreset.Width;
                Height = SelectedPreset.Height;
            }
            //else
            //{
            //    SelectedPreset = AvailablePresets.FirstOrDefault(x => x.Width == Width && x.Height == Height) ?? AvailablePresets[2];
            //}
        }

        private void AddPreset(int width = default, int height = default, string title = default)
        {
            if (width == default)
                this.AvailablePresets.Add(new NewDocumentSettingsPresetViewModel());
            else
            {
                var p = new NewDocumentSettingsPresetViewModel(width, height);
                if (title != default)
                {
                    p.Title = title;
                }
                this.AvailablePresets.Add(p);
            }
        }
    }
}