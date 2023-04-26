using System;
using System.Net;
using Pix2d.Plugins.HttpHost.Core;
using SkiaNodes.Serialization;

namespace Pix2d.Plugins.HttpHost;

public class HttpHostPlugin : IPix2dPlugin, IDisposable
{
    public AppState AppState { get; }
    private WebServer _ws;

    public HttpHostPlugin(AppState appState)
    {
        AppState = appState;
    }

    public string SendResponse(HttpListenerRequest request)
    {
        using var serializer = new NodeSerializer();
        return serializer.Serialize(AppState.CurrentProject.SceneNode);
    }

    public void Initialize()
    {
        _ws = new WebServer(SendResponse);
        _ws.StartWebServer();
        Logger.Log("Http module initialized");
    }


    public void Dispose()
    {
        _ws.StopWebServer();
    }
}