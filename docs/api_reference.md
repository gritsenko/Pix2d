# Pix2D API Reference

## Core Interfaces

### IPix2dPlugin
Core interface for implementing plugins:
```csharp
public interface IPix2dPlugin
{
    void Initialize();
}
```

### IFileContentSource
Interface for file system operations:
```csharp
public interface IFileContentSource
{
    string Path { get; }
    bool Exists { get; }
    DateTime LastModified { get; }
    string Extension { get; }
    string Title { get; set; }
    Task SaveAsync(Stream sourceStream);
    Task SaveAsync(string textContent);
    Task<Stream> OpenRead();
    Task<Stream> OpenWriteAsync();
    void Delete();
}
```

### IProjectService
Project management interface:
```csharp
public interface IProjectService
{
    Task SaveCurrentProjectAsync();
    Task SaveCurrentProjectAsAsync(ExportImportProjectType saveAsType);
    Task OpenFilesAsync();
    Task OpenFilesAsync(IFileContentSource[] file);
    Task CreateNewProjectAsync(SKSize newProjectSize);
    Task<ProjectsCollection> GetProjectsListAsync();
    Task RenameCurrentProjectAsync();
}
```

### IDrawingService
Drawing operations interface:
```csharp
public interface IDrawingService
{
    IDrawingLayer GetDrawingLayer();
}
```

## Core Services

### Pix2DApp
Main application class implementing:
- IViewPortService
- IAppStateService<AppState>
- IPlatformTypeProvider

Key responsibilities:
- Plugin management
- Scene management
- Application state
- Platform-specific functionality

### BaseTool
Base class for implementing tools:
```csharp
public abstract class BaseTool : ITool
{
    protected SKNode? RootNode { get; }
    public bool IsActive { get; }
    
    public virtual Task Activate();
    public virtual void Deactivate();
    
    protected virtual void OnPointerMoved(object? sender, PointerActionEventArgs e);
    protected virtual void OnPointerReleased(object? sender, PointerActionEventArgs e);
    protected virtual void OnPointerPressed(object? sender, PointerActionEventArgs e);
    protected virtual void OnPointerDoubleClicked(object? sender, PointerActionEventArgs e);
}
```

## Plugin Development

### Creating a Plugin
1. Implement IPix2dPlugin interface
2. Register plugin in application bootstrap
3. Initialize plugin resources

Example:
```csharp
public class CustomPlugin : IPix2dPlugin
{
    public void Initialize()
    {
        // Initialize plugin resources
    }
}
```

### File Format Plugin
Example structure for implementing a custom file format:
```csharp
public class CustomFormatPlugin : IPix2dPlugin
{
    public void Initialize()
    {
        // Register importers/exporters
    }
}

public class CustomImporter : IImporter
{
    public async Task<IEnumerable<SKNode>> ImportFromFiles(
        IEnumerable<IFileContentSource> files)
    {
        // Import implementation
    }
}
```

## UI Components

### ComponentBase
Base class for UI components with:
- Dependency injection support
- Message handling
- MVU pattern support

### LayersView
Example of a complex UI component:
```csharp
public class LayersView : ComponentBase
{
    // Layer management UI implementation
}
```

## Platform-Specific Services

### IPlatformStuffService
Interface for platform-specific operations:
```csharp
public interface IPlatformStuffService
{
    void OpenUrlInBrowser(string url);
    void SetWindowTitle(string title);
    MemoryInfo GetMemoryInfo();
    string KeyToString(VirtualKeys key);
    string GetAppVersion();
    void ToggleTopmostWindow();
    bool HasKeyboard { get; }
    bool CanShare { get; }
    void Share(IStreamExporter exporter, double scale = 1);
    void ToggleFullscreenMode();
    string GetAppFolderPath();
}
```