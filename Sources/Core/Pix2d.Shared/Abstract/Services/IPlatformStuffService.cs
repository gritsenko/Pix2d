using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using SkiaNodes.Interactive;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Defines a service for interacting with platform-specific features and services,
/// such as opening URLs, managing the window, accessing memory information, etc.
/// </summary>
public interface IPlatformStuffService
{
    /// <summary>
    /// Current platform 
    /// </summary>
    PlatformType CurrentPlatform { get; }

    /// <summary>
    /// Opens the specified URL in the default web browser.
    /// </summary>
    /// <param name="url">The URL string to open.</param>
    void OpenUrlInBrowser(string url);

    /// <summary>
    /// Sets the title of the main application window.
    /// </summary>
    /// <param name="title">The string to set as the window title.</param>
    void SetWindowTitle(string title);

    /// <summary>
    /// Retrieves information about the application's memory usage.
    /// </summary>
    /// <returns>A MemoryInfo structure containing details about memory usage.</returns>
    MemoryInfo GetMemoryInfo();

    /// <summary>
    /// Converts a virtual key code to its string representation.
    /// </summary>
    /// <param name="key">The VirtualKeys enum value.</param>
    /// <returns>A string representing the key.</returns>
    string KeyToString(VirtualKeys key);

    /// <summary>
    /// Gets the version string of the running application.
    /// </summary>
    /// <returns>A string representing the application version.</returns>
    string GetAppVersion();

    /// <summary>
    /// Toggles the topmost state of the main application window (whether it stays on top of other windows).
    /// </summary>
    public void ToggleTopmostWindow();

    /// <summary>
    /// Gets a value indicating whether the current platform has a physical keyboard available.
    /// </summary>
    public bool HasKeyboard { get; }

    /// <summary>
    /// Gets a value indicating whether the current platform supports sharing content.
    /// </summary>
    public bool CanShare { get; }

    /// <summary>
    /// Initiates the platform's sharing mechanism with content exported by the provided exporter.
    /// </summary>
    /// <param name="exporter">The IStreamExporter to use to get the content to share.</param>
    /// <param name="scale">The scaling factor to apply during export for sharing (default is 1).</param>
    public void Share(IStreamExporter exporter, double scale = 1);

    /// <summary>
    /// Toggles the fullscreen mode of the main application window.
    /// </summary>
    public void ToggleFullscreenMode();

    /// <summary>
    /// Gets the path to the application's data folder where settings and other files are stored.
    /// </summary>
    /// <returns>A string representing the application data folder path.</returns>
    string GetAppFolderPath();

    /// <summary>
    /// Opens the application's data folder in the platform's default file explorer.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    Task OpenAppDataFolder();
}
