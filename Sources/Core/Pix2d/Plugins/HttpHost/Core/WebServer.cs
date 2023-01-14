using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Pix2d.Plugins.HttpHost.Core;

public class WebServer
{
    private readonly Func<HttpListenerRequest, string> _jsonStateProviderFunc;
    private const int Port = 5085; 
    private readonly HttpListener Listener = new HttpListener { Prefixes = { $"http://localhost:{Port}/" } };
    private bool _keepGoing = true;
    private Task _mainLoop;

    public WebServer(Func<HttpListenerRequest, string> jsonStateProviderFunc)
    {
        _jsonStateProviderFunc = jsonStateProviderFunc;
    }

    public void StartWebServer()
    {
        if (_mainLoop != null && !_mainLoop.IsCompleted) return; //Already started
        _mainLoop = MainLoop();
    }
    public void StopWebServer()
    {
        _keepGoing = false;
        lock (Listener)
        {
            //Use a lock so we don't kill a request that's currently being processed
            Listener.Stop();
        }
        try
        {
            _mainLoop.Wait();
        }
        catch { /* je ne care pas */ }
    }

    private async Task MainLoop()
    {
        Listener.Start();
        while (_keepGoing)
        {
            try
            {
                var context = await Listener.GetContextAsync();
                lock (Listener)
                {
                    if (_keepGoing) ProcessRequest(context);
                }
            }
            catch (Exception e)
            {
                if (e is HttpListenerException) return; //this gets thrown when the listener is stopped
                Logger.LogException(e);
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        using var response = context.Response;

        if (context.Request.HttpMethod == "OPTIONS")
        {
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
            response.AddHeader("Access-Control-Max-Age", "1728000");
        }
        response.AppendHeader("Access-Control-Allow-Origin", "*");

        try
        {
            var handled = false;
            switch (context.Request.Url.AbsolutePath)
            {
                case "/pix2d":
                    switch (context.Request.HttpMethod)
                    {
                        case "GET":
                            //Get the current settings
                            response.ContentType = "application/json";
                            //This is what we want to send back
                            var responseBody = _jsonStateProviderFunc.Invoke(context.Request);
                            //Write it to the response stream
                            var buffer = Encoding.UTF8.GetBytes(responseBody);
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            handled = true;
                            break;
                    }
                    break;
            }
            if (!handled)
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception e)
        {
            //Return the exception details the client - you may or may not want to do this
            response.StatusCode = 500;
            response.ContentType = "application/json";
            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e));
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            Logger.LogException(e);
        }
    }

}