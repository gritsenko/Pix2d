using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Pix2d
{
    public class StopWatchLogger : IDisposable
    {
#if TRACE
        private Stopwatch _sw;
        private readonly string _message;
        private readonly string _callerFilePath;
        private readonly string _callerMemberName;
        private TimeSpan _lap;
#endif

        public StopWatchLogger(string message = "", [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
#if TRACE
            _message = message;
            _callerFilePath = callerFilePath;
            _callerMemberName = callerMemberName;
            _sw = new Stopwatch();
            _sw.Start();

            Logger.Trace(_message + "===Start===", _callerFilePath, _callerMemberName);
#endif
        }

        public void Dispose()
        {
#if TRACE
            _sw.Stop();
            Logger.Trace(" ===Finish=== [total time: " + _sw.Elapsed.ToString("g") + "]" + _message, _callerFilePath, _callerMemberName);
#endif
        }

        public void Log(string message)
        {
#if TRACE
            var time = _sw.Elapsed - _lap;
            _lap = _sw.Elapsed;
            Logger.Trace(message + "[elapsed: " + time.ToString("g") + "]", _callerFilePath, _callerMemberName);
#endif
        }
    }
}