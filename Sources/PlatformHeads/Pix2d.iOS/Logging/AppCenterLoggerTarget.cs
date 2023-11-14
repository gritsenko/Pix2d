using System;
using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Pix2d.Abstract.Services;
using Pix2d.Common;

namespace Pix2d.iOS.Logging;

public class AppCenterLoggerTarget : ILoggerTarget
{
    public bool EventsOnly => false;
    private IPlatformStuffService _pps;
    private string? _license;

    public void OnLogged(LogEntry logEntry)
    {
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

        if (_pps == null)
        {
            _pps = ServiceLocator.Current?.GetInstance<IPlatformStuffService>();
        }

        if (_pps != null)
        {
            pars["ram"] = GetRamGroup(_pps.GetMemoryInfo().UsedRam);
        }

        if (logEntry.ExtraParams != null && logEntry.ExtraParams.Any())
            foreach (var param in logEntry.ExtraParams)
            {
                pars.Add(param.Key, param.Value);
            }


#if !__WASM__
        if (!logEntry.IsEvent)
        {
            var attaches = new ErrorAttachmentLog[0];
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

            Crashes.TrackError(logEntry.Exception, pars, attaches);
        }
        else
        {
            Analytics.TrackEvent(logEntry.Message, pars);
        }
#endif
    }

    private string GetRamGroup(ulong ramAmount)
    {
        var mbs = Math.Floor((double) (ramAmount / 104857600)) * 100;
        return mbs + "+MB";
    }
}