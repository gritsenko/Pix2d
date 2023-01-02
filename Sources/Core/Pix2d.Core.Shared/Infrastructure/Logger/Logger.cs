using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Pix2d
{
    public class Logger
    {
        private static Logger _instance = new Logger();

        private List<LogEntry> _logs = new List<LogEntry>();


        private List<ILoggerTarget> _targets = new List<ILoggerTarget>();

        public IExtraInfoProvider ExtraInfoProvider { get; set; }

        private readonly Dictionary<string, string> _paramDict = new Dictionary<string, string>();


        public static void RegisterLoggerTarget(ILoggerTarget target)
        {
            _instance._targets.Add(target);
        }

        public static void ClearExtraParam()
        {
            _instance._paramDict.Clear();
        }
        public static void SetExtraParam(string name, string value)
        {
            _instance._paramDict[name] = value;
        }

        public static void RegisterExtraInfoProvider(IExtraInfoProvider extraInfoProvider)
        {
            _instance.ExtraInfoProvider = extraInfoProvider;
        }

        public static void LogException(Exception ex, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            _instance.AddLogEntry(ex, $"Exception in {callerFilePath}.{callerMemberName} ");
        }


        public static void LogException<TCallerType>(Exception ex, string message = "", params object[] args)
        {
            LogException(ex, "Error in " + typeof (TCallerType).Name + " :: " + message, args);
        }

//        [DefaultOverload]
        public static void LogException(Exception ex, string message, params object[] args)
        {
            _instance.AddLogEntry(ex, message, args);
        }

        public static void Log(string message, params object[] args)
        {
            _instance.AddLogEntry(null, message, args);
        }

        [Conditional("TRACE")]
        public static void Trace(string message, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMemberName = null)
        {
            var msg = Path.GetFileName(CallerFilePath.Replace(".cs", "")) + "." + CallerMemberName + ": " + message;
            _instance.AddLogEntry(null, msg);
        }

        [Conditional("TRACE")]
        public static void Trace<T>(string message, params object[] args)
        {
            Trace(typeof(T).Name + ": " + message, args);
        }

//        [DefaultOverload]
        [Conditional("TRACE")]
        public static void Trace(string message, params object[] args)
        {
            _instance.AddLogEntry(null, message, args);
        }


        private void AddLogEntry(Exception ex, string message, params object[] args)
        {
//            var extraInfo = ExtraInfoProvider != null ? ExtraInfoProvider.GetExtraInfo() : "";
            var entry = new LogEntry(message, args) {Exception = ex};
           // _instance._logs.Add(entry);

            if (entry.ExtraParams == null)
            {
                entry.ExtraParams = _paramDict;
            }
            else
            {
                entry.ExtraParams = entry.ExtraParams.Concat(_paramDict).ToDictionary(x => x.Key, x => x.Value);
            }

            foreach (var loggerTarget in _targets)
            {
                loggerTarget.OnLogged(entry);
            }

#if DEBUG
            Debug.WriteLine(entry.ToString());
#endif
        }

        public static void LogEvent(string eventName, params object[] args)
        {
            var entry = new LogEntry(eventName, args) { IsEvent = true };

            foreach (var loggerTarget in _instance._targets)
                loggerTarget.OnLogged(entry);

            //без параметров, что бы логгировать класс событий
            if (args != null && args.Length > 0)
            {
                var entryBase = new LogEntry(eventName) { IsEvent = true };
                foreach (var loggerTarget in _instance._targets)
                    loggerTarget.OnLogged(entryBase);
            }

#if DEBUG
            Debug.WriteLine(entry.ToString());
#endif
        }

        /**********************************************************************************/

        public static void LogEventWithParams(string eventName, IDictionary<string,string> extraParams, IDictionary<string,double> metrics = null)
        {
            var entry = new LogEntry(eventName) {IsEvent = true, ExtraParams = extraParams, Metrics = metrics};

            foreach (var loggerTarget in _instance._targets)
                loggerTarget.OnLogged(entry);
#if DEBUG
            Debug.WriteLine(entry.ToString());
#endif
        }


    }

    public class LogEntry
    {
        public DateTime Time { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
        public bool IsEvent { get; set; }
        public IDictionary<string, string> ExtraParams { get; set; }
        public IDictionary<string, double> Metrics { get; set; }

        public LogEntry(string message, params object[] args)
        {
            Time = DateTime.Now;

            if (args.Length == 0)
            {
                Message = message;
                return;
            }

            Message = String.Format(message, args);
        }

        public override string ToString()
        {
            var sb = new StringBuilder()
                .Append(Time.ToString("t"))
                .Append(": ")
                .Append(Message);

            if (Exception != null)
            {
                sb.AppendLine("exception:")
                    .AppendLine(Exception.Message)
                    .AppendLine(Exception.StackTrace);
            }
            return sb.ToString();
        }
    }
}
