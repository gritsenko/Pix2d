using Avalonia.Controls.Shapes;
using Mvvm.Messaging;
using Pix2d.Messages;
using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Views.MainMenu;

public class OpenDocumentView : ComponentBase
{
    private FuncDataTemplate<MruItem> RecentProjectTemplate = new((itemVm, ns) =>
        new Button()
            .HorizontalContentAlignment(HorizontalAlignment.Left)
            .Content(itemVm.Title)
            .OnClick(_ => itemVm.OpenAsync())
    );

    protected override object Build() =>
        new Border()
            .Padding(32, 0, 0, 0)
            .Child(
                new StackPanel()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        new TextBlock()
                            .FontSize(24)
                            .Text("Open"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Open Pix2d project"),

                        new Button()
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Margin(0, 8, 0, 8)
                            .Padding(16)
                            .Background(Brushes.Gray)
                            .OnClick(OnOpenButtonClick)
                            .Content(
                                new Grid()
                                    .Rows("*,Auto")
                                    .Children(
                                        new Path()
                                            .Data(Geometry.Parse("M0.933594,0.359375C0.421875,0.359375,0,0.78125,0,1.292969L0,12.386719C0,12.898438,0.421875,13.320313,0.933594,13.320313L9.898438,13.320313 9.179688,12.601563 0.933594,12.601563C0.8125,12.601563,0.71875,12.507813,0.71875,12.386719L0.71875,1.292969C0.71875,1.171875,0.8125,1.078125,0.933594,1.078125L17.066406,1.078125C17.1875,1.078125,17.28125,1.171875,17.28125,1.292969L17.28125,12.386719C17.28125,12.507813,17.1875,12.601563,17.066406,12.601563L14.941406,12.601563 15.660156,13.320313 17.066406,13.320313C17.578125,13.320313,18,12.898438,18,12.386719L18,1.292969C18,0.78125,17.578125,0.359375,17.066406,0.359375z M1.800781,1.800781L1.800781,4.679688 3.238281,4.679688 3.238281,1.800781z M4.320313,1.800781L4.320313,11.878906 8.460938,11.878906 7.738281,11.160156 5.039063,11.160156 5.039063,2.519531 15.839844,2.519531 15.839844,11.160156 13.5,11.160156 14.21875,11.878906 16.558594,11.878906 16.558594,1.800781z M2.519531,5.398438C1.726563,5.398438 1.078125,6.046875 1.078125,6.839844 1.078125,7.632813 1.726563,8.28125 2.519531,8.28125 3.3125,8.28125 3.960938,7.632813 3.960938,6.839844 3.960938,6.046875 3.3125,5.398438 2.519531,5.398438z M2.519531,6.121094C2.921875,6.121094 3.238281,6.4375 3.238281,6.839844 3.238281,7.242188 2.921875,7.558594 2.519531,7.558594 2.117188,7.558594 1.800781,7.242188 1.800781,6.839844 1.800781,6.4375 2.117188,6.121094 2.519531,6.121094z M2.519531,6.480469C2.320313,6.480469 2.160156,6.640625 2.160156,6.839844 2.160156,7.039063 2.320313,7.199219 2.519531,7.199219 2.71875,7.199219 2.878906,7.039063 2.878906,6.839844 2.878906,6.640625 2.71875,6.480469 2.519531,6.480469z M7.796875,7.921875C7.734375,7.921875 7.675781,7.941406 7.628906,7.988281 7.535156,8.082031 7.535156,8.242188 7.628906,8.335938 7.640625,8.351563 7.75,8.457031 7.851563,8.5625L7.808594,8.605469C7.703125,8.707031,7.671875,8.863281,7.730469,9L8.671875,11.328125C8.71875,11.429688 8.804688,11.503906 8.910156,11.53125 8.910156,11.53125 8.917969,11.542969 8.921875,11.542969 8.929688,11.542969 8.9375,11.542969 8.945313,11.542969 8.960938,11.546875 8.976563,11.558594 9,11.566406 9.054688,11.582031 9.128906,11.597656 9.214844,11.632813 9.382813,11.699219 9.578125,11.816406 9.664063,11.902344L14.671875,16.898438C14.785156,17.011719 14.960938,17.085938 15.121094,17.078125 15.1875,17.074219 15.246094,17.0625 15.300781,17.042969L15.605469,17.347656C15.984375,17.730469 16.605469,17.730469 16.988281,17.347656 17.367188,16.964844 17.367188,16.34375 16.988281,15.964844L16.683594,15.660156C16.703125,15.605469 16.714844,15.546875 16.71875,15.480469 16.722656,15.324219 16.652344,15.132813 16.539063,15.019531L11.542969,10.023438C11.351563,9.832031 11.183594,9.28125 11.183594,9.28125 11.167969,9.222656 11.136719,9.167969 11.09375,9.125 11.085938,9.117188 11.078125,9.109375 11.070313,9.101563 11.039063,9.074219 11.007813,9.050781 10.96875,9.035156L8.640625,8.089844C8.582031,8.0625 8.523438,8.050781 8.460938,8.054688 8.378906,8.066406 8.300781,8.105469 8.246094,8.167969L8.203125,8.210938 7.976563,7.988281C7.929688,7.941406,7.859375,7.921875,7.796875,7.921875z M8.582031,8.84375L10.191406,9.496094 9.136719,10.550781 8.484375,8.945313z M1.800781,9L1.800781,11.878906 3.238281,11.878906 3.238281,9z M10.699219,10C10.78125,10.179688,10.863281,10.367188,11.023438,10.53125L15.976563,15.480469C15.972656,15.488281 15.980469,15.480469 15.976563,15.492188 15.960938,15.503906 15.949219,15.519531 15.941406,15.535156 15.871094,15.628906 15.734375,15.765625 15.570313,15.929688 15.4375,16.0625 15.324219,16.167969 15.234375,16.246094 15.1875,16.257813 15.144531,16.28125 15.109375,16.3125L10.171875,11.386719C10.003906,11.21875,9.820313,11.140625,9.640625,11.058594z M11.519531,11.566406C11.429688,11.554688 11.347656,11.582031 11.285156,11.644531 11.160156,11.769531 11.179688,11.988281 11.328125,12.140625 11.378906,12.1875 12.097656,12.910156 12.148438,12.960938 12.300781,13.109375 12.519531,13.128906 12.644531,13.003906 12.773438,12.878906 12.75,12.660156 12.601563,12.511719L11.777344,11.6875C11.703125,11.613281,11.609375,11.574219,11.519531,11.566406z M16.265625,16.257813L16.480469,16.46875C16.585938,16.574219 16.585938,16.734375 16.480469,16.839844 16.375,16.945313 16.214844,16.945313 16.109375,16.839844L15.894531,16.628906C15.960938,16.570313 16.023438,16.511719 16.085938,16.449219 16.152344,16.382813 16.207031,16.320313 16.265625,16.257813Z"))
                                            .Fill(Brushes.White)
                                            .Width(48)
                                            .Height(48)
                                            .Stretch(Stretch.Uniform),
                                        new TextBlock()
                                            .Text("Browse files")
                                            .Row(1)
                                            .Foreground(Brushes.White)
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                    )
                            ), // Open Button
                        
                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Recent projects"),

                        new ItemsControl()
                            .Margin(0, 8, 0, 8)
                            .ItemsSource(Bind(RecentProjects))
                            .ItemTemplate(RecentProjectTemplate)

                    ) //StackPanel.Children
            );

    [Inject] private IProjectService ProjectService { get; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;

    public ObservableCollection<MruItem> RecentProjects { get; set; } = new();

    protected override void OnAfterInitialized()
    {
        Messenger.Register<MruChangedMessage>(this, m => UpdateMruData());
        UpdateMruData(); // update recent files list on any open, file can be deleted by user while app is running
    }

    public async void UpdateMruData()
    {
        var recentProjects = await ProjectService.GetRecentProjectsAsync();
        RecentProjects.Clear();
        foreach (var fileContentSource in recentProjects)
            RecentProjects.Add(new MruItem(fileContentSource, ProjectService));
    }


    private async void OnOpenButtonClick(RoutedEventArgs obj)
    {
        Commands.View.HideMainMenuCommand.Execute();
        await ProjectService.OpenFilesAsync();
    }

    public sealed class MruItem
    {
        public IFileContentSource File { get; set; }

        public IProjectService ProjectService { get; }

        public string Path => File.Path;

        public string Title => File.Title;

        public MruItem(IFileContentSource fileContentSource, IProjectService projectService)
        {
            File = fileContentSource;
            ProjectService = projectService;
        }

        internal async void OpenAsync()
        {
            Commands.View.HideMainMenuCommand.Execute();
            await ProjectService.OpenFilesAsync(new[] { File });
        }
    }
}