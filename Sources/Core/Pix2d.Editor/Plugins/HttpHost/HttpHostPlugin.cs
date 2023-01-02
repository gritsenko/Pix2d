using System;
using System.Diagnostics;
using System.Net;
using Pix2d.Abstract;
using Pix2d.Abstract.State;
using Pix2d.Plugins.HttpHost.Core;
using SkiaNodes.Serialization;

namespace Pix2d.Plugins.HttpHost
{
    public class HttpHostPlugin : IPix2dPlugin, IDisposable
    {
        public IAppState AppState { get; }
        private WebServer _ws;

        public HttpHostPlugin(IAppState appState)
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
}
