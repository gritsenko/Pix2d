using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;
using Sentry;

namespace Pix2d.Logging;

public class SentryLoggerTarget : ILoggerTarget
{
    private readonly IPlatformStuffService _pps;
    private string? _license;
    public bool EventsOnly => false;

    public SentryLoggerTarget()
    {
        _pps = ServiceLocator.Current?.GetInstance<IPlatformStuffService>();
        
        SentrySdk.Init(o =>
        {
            o.Dsn = "https://9088a22c17385d098701c8059b42f460@o4505646237614080.ingest.sentry.io/4505646271692800";
            o.SendClientReports = false;
            o.AutoSessionTracking = true;
            o.StackTraceMode = StackTraceMode.Enhanced;
            o.IsGlobalModeEnabled = true;
        });
    }
    
    public void OnLogged(LogEntry logEntry)
    {
        if (logEntry.Exception == null)
        {
            logEntry.IsEvent = true;
        }

        var pars = new Dictionary<string, string>();
        _license ??= CoreServices.AppStateService.AppState.LicenseType.ToString();
        pars["lic"] = _license;
        pars["ram"] = GetRamGroup(_pps.GetMemoryInfo().UsedRam);

        if (logEntry.ExtraParams != null && logEntry.ExtraParams.Any())
            foreach (var param in logEntry.ExtraParams)
            {
                pars.Add(param.Key, param.Value);
            }

        if (!logEntry.IsEvent)
        {
            var attaches = "";
            try
            {
                attaches = SessionLogger.Instance.GetSessionOperationLogText();
            }
            catch
            {
                //can't get attachments
            }

            SentrySdk.CaptureException(logEntry.Exception, s =>
            {
                s.Contexts["Custom data"] = pars;
                s.Contexts["attachments"] = attaches;
            });
        }
        else
        {
            SentrySdk.CaptureMessage(logEntry.Message, s => s.Contexts["Custom data"] = pars);
        }
    }
    
    private string GetRamGroup(ulong ramAmount)
    {
        var mbs = Math.Floor((double) (ramAmount / 104857600)) * 100;
        return mbs + "+MB";
    }
}