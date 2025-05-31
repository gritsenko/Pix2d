using Avalonia.Interactivity;
using Avalonia.Threading;
using Pix2d.Command;
using Pix2d.Common;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.Project;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.MainMenu;

public class ProjectItem : LocalizedComponentBase
{
    private PreloadedProject _project;
    public readonly SKBitmapObservable Preview = new()
    {
        Bitmap = StaticResources.NoPreview.ToSKBitmap()
    };

    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private IProjectService ProjectService { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;

    public string ProjectName => string.IsNullOrWhiteSpace(_project?.Name) ? L("Loading...")() : _project.Name;

    public ProjectItem(PreloadedProject project)
    {
        _project = project;
        LoadPreview();
        StateHasChanged();
    }

    protected override object Build()
    {
        return new Button()
            .BorderThickness(4)
            .Padding(0)
            .Height(128)
            .Width(128)
            .Margin(new Thickness(0, 0, 8, 8))
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .OnClick(LoadProject)
            .Content(new Grid().Rows("*").Cols("*").Children(
                new SKImageView()
                    .Width(120)
                    .Height(120)
                    .Source(Preview),
                new Border()
                    .Background(StaticResources.Brushes.MainBackgroundBrush)
                    .CornerRadius(4)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Padding(4)
                    .Margin(4)
                    .Child(
                        new TextBlock()
                            .Text(() => ProjectName)),
                new Button()
                    .VerticalAlignment(VerticalAlignment.Top)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .CornerRadius(12)
                    .Height(24)
                    .Width(24)
                    .Background(StaticResources.Brushes.MainBackgroundBrush)
                    .OnClick(OnDeleteClick)
                    .Content(
                        new TextBlock()
                            .Classes("FontIcon")
                            .FontSize(12)
                            .Text("\xe107")
                    )

            ));
    }

    private async void OnDeleteClick(RoutedEventArgs ev)
    {
        try
        {
            ev.Handled = true;

            if (await DialogService.ShowYesNoDialog($"Do you want to delete project \"{_project.Name}\" from disc?", "Delete project", "Yes"))
            {
                _project.File.Delete();
                Messenger.Send(new MruChangedMessage());
            }

        }
        catch (Exception e)
        {
            Logger.LogException(e);
            await DialogService.ShowAlert("Error while trying to delete project", "Error");
        }
    }

    private void LoadPreview()
    {
        Task.Run(async () =>
        {
            var preview = await _project.LoadPreviewAsync();
            if (preview != null)
            {
                Preview.SetBitmap(preview);
                OnPropertyChanged(nameof(Preview));
                StateHasChanged();
            }
        });
    }

    private void LoadProject(RoutedEventArgs _)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            CommandService.GetCommandList<ViewCommands>()?.HideMainMenuCommand.Execute();
            await ProjectService.OpenFilesAsync([_project.File]);
        });
    }
}