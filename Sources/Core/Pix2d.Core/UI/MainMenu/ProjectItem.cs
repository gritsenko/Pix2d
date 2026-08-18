using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.Common;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.Project;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.MainMenu;

public partial class ProjectItem : ViewBase<ProjectItem.State>
{
    public ProjectItem(PreloadedProject project, IMessenger messenger, IDialogService dialogService, IProjectService projectService, ICommandService commandService)
        : base(new State(project, messenger, dialogService, projectService, commandService))
    {
    }

    protected override object Build(State state)
    {
        return new Button()
            // The card shows only the (elided) file name, and two recents can share it — the same project
            // saved in Documents and in Downloads look identical. The full path is what tells them apart.
            .ToolTip_Tip(state.FullPath)
            .BorderThickness(4)
            .Padding(0)
            .Height(128)
            .Width(128)
            .Margin(new Thickness(0, 0, 8, 8))
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .OnClick(_ => state.LoadProject())
            .Content(new Grid().Rows("*").Cols("*").Children(
                new SKImageView()
                    .Width(120)
                    .Height(120)
                    .Source(state, x => x.Preview),
                new Border()
                    .Background(StaticResources.Brushes.MainBackgroundBrush)
                    .CornerRadius(4)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Padding(4)
                    .Margin(4)
                    .Child(
                        new TextBlock()
                            .Text(state, x => x.ProjectName)),
                new Button()
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .CornerRadius(12)
                    .Height(24)
                    .Width(24)
                    .Background(StaticResources.Brushes.MainBackgroundBrush)
                    .OnClick(state.OnDeleteClick)
                    .Content(
                        new TextBlock()
                            .Classes("FontIcon")
                            .FontSize(12)
                            .Text("\xe107")
                    )

            ));
    }
    public sealed partial class State : ObservableObject
    {
        private readonly IMessenger _messenger;
        private readonly IDialogService _dialogService;
        private readonly IProjectService _projectService;
        private readonly ViewCommands _viewCommands;

        public State(PreloadedProject project, IMessenger messenger, IDialogService dialogService, IProjectService projectService, ICommandService commandService)
        {
            _messenger = messenger;
            _dialogService = dialogService;
            _projectService = projectService;
            _viewCommands = commandService.GetCommandList<ViewCommands>()!;

            Project = project;
            ProjectName = string.IsNullOrWhiteSpace(project.Name) ? L("Loading...") : project.Name;
            FullPath = string.IsNullOrWhiteSpace(project.Path) ? ProjectName : project.Path;
            LoadPreview();
        }

        public PreloadedProject Project { get; }

        /// <summary>
        /// Tooltip text: the file's full path, or its name for a source that has none (an Android SAF
        /// <c>content://</c> item, a browser file handle).
        /// </summary>
        public string FullPath { get; }

        public SKBitmapObservable Preview { get; } = new()
        {
            Bitmap = StaticResources.NoPreview.ToSKBitmap()
        };

        [ObservableProperty]
        public partial string ProjectName { get; set; } = string.Empty;

        public async void OnDeleteClick(RoutedEventArgs ev)
        {
            try
            {
                ev.Handled = true;

                if (await _dialogService.ShowYesNoDialog($"Do you want to delete project \"{Project.Name}\" from disc?", "Delete project", "Yes"))
                {
                    Project.File.Delete();
                    _messenger.Send(new MruChangedMessage());
                }
            }
            catch (Exception e)
            {
                Logger.LogException(e);
                await _dialogService.ShowAlert("Error while trying to delete project", "Error");
            }
        }

        public void LoadProject()
        {
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                _viewCommands.HideMainMenuCommand.Execute();
                await _projectService.OpenFilesAsync([Project.File]);
            });
        }

        private void LoadPreview()
        {
            Task.Run(async () =>
            {
                // Fire-and-forget: nothing awaits this task, so an exception escaping it is an unhandled
                // crash rather than a missing thumbnail. A recent-projects entry can be any file the user
                // ever opened — since deleted, replaced, or corrupt — so failing to read one is expected.
                try
                {
                    var preview = await Project.LoadPreviewAsync();
                    if (preview != null)
                    {
                        Preview.SetBitmap(preview);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                }
            });
        }
    }
}