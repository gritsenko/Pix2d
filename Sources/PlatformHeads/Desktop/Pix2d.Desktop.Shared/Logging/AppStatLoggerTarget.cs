using System;
using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;
using Pix2d.Abstract.Services;

namespace Pix2d.Desktop.Logging
{
    public class AppStatLoggerTarget : ILoggerTarget
    {
        private bool _initialized;
        public bool EventsOnly => false;
        private static IPlatformStuffService? _pps;
        private string _ram = "";
        private string? _license;

        public void OnLogged(LogEntry logEntry)
        {
            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }

            if (logEntry.Exception == null)
            {
                logEntry.IsEvent = true;
            }

            var pars = new Dictionary<string, string>();
            _license ??= CoreServices.AppStateService.AppState.LicenseType.ToString();
            pars["lic"] = _license;

#if FULL_MODE
            pars["lic"] = "full";
#endif

            pars["ram"] = _ram;

            if (logEntry.ExtraParams != null && logEntry.ExtraParams.Any())
                foreach (var param in logEntry.ExtraParams)
                {
                    pars.Add(param.Key, param.Value);
                }
#if false
            if (!logEntry.IsEvent)
            {
                ErrorAttachmentLog[]? attaches = null;
                try
                {
                    attaches = new[]
                    {
                        ErrorAttachmentLog.AttachmentWithText(SessionLogger.Instance.GetSessionOperationLogText(), "operations.log")
                    };
                }
                catch
                {
                    //can't get attachments
                }

                if(attaches != null)
                    Crashes.TrackError(logEntry.Exception, pars, attaches);
                else 
                    Crashes.TrackError(logEntry.Exception, pars);
            }
            else
            {
                Analytics.TrackEvent(logEntry.Message, pars);
            }
#endif
        }

        private void Initialize()
        {
            _pps ??= ServiceLocator.Current?.GetInstance<IPlatformStuffService>();
            _ram = GetRamGroup(_pps.GetMemoryInfo().UsedRam);
        }

        private string GetRamGroup(ulong ramAmount)
        {
            var mbs = Math.Floor((double)(ramAmount / 104857600)) * 100;
            return mbs + "+MB";
        }
    }
}