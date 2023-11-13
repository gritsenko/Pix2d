using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;

namespace Pix2d.Desktop.Services;

public static class SingleInstancePipeService
{
    private static string PipeName = "Pix2dSingleInstancePipe";
    private static string MutexName = "Pix2dMutex";
    public static Mutex? AppMutex;

    public static void CheckSingleInstance(string mutexName, string pipeName)
    {
        MutexName = mutexName;
        PipeName = pipeName;
        CheckSingleInstance();
    }

    public static void CheckSingleInstance()
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsCheckSingleInstance();
        }
        else if (OperatingSystem.IsLinux())
        {
            LinuxCheckSingleInstance();
        }
        else
        {
            throw new Exception("Check Single Instance: Unsupported OS");
        }
    }

    private static void WindowsCheckSingleInstance()
    {
        AppMutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            Listen();
        }
        else
        {
            SendArgs();
            Environment.Exit(0);
        }
    }

    private static void LinuxCheckSingleInstance()
    {
        AppMutex = new Mutex(true, $"Global\\{MutexName}", out var createdNew);
        if (createdNew)
        {
            Listen();
        }
        else
        {
            SendArgs();
            Environment.Exit(0);
        }
    }

    private static void SendArgs()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(PipeName);
            pipe.Connect();
            using var sw = new StreamWriter(pipe);
            sw.WriteLine(JsonSerializer.Serialize(Environment.GetCommandLineArgs()));
        }
        catch
        {
#if DEBUG
            Debugger.Break();
#endif
        }
    }

    static async void Listen()
    {
        try
        {
            while (true)
            {
                await using var pipe = new NamedPipeServerStream(PipeName);
                using var sr = new StreamReader(pipe);
                await pipe.WaitForConnectionAsync();
                var result = await sr.ReadLineAsync();
                MessageReceived?.Invoke(null, result);
            }
        }
        catch
        {
#if DEBUG
            Debugger.Break();
#endif
        }
    }

    public static event EventHandler<string>? MessageReceived;
}