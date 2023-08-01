using System;
using System.Collections.Generic;
using System.Linq;
using CommonServiceLocator;
using Pix2d.Abstract.Services;
using Pix2d.Infrastructure.GA.Extensions;
using Pix2d.Infrastructure.GA.Service.Client;
using Pix2d.Infrastructure.GA.Service.Request;
using Pix2d.Infrastructure.GA.Service;
using Pix2d.Infrastructure.GA.StandardEvents;

namespace Pix2d.Desktop.Logging
{
    public class GALoggerTarget : ILoggerTarget
    {
        private readonly string _id;
        private readonly string _key;
        private bool _initialized;
        public bool EventsOnly => false;
        private static IPlatformStuffService? _pps;
        private string _ram = "";
        private MeasurementService _mms;

        public GALoggerTarget(string id, string key)
        {
            _id = id;
            _key = key;

        }

        private void Initialize()
        {
            _mms = new MeasurementService(new BasicHttpClient(
                new GoogleAnalyticsClientSettings()
                {
                    AppSecret = _id,
                    MeasurementId = _key
                }
            ));

            _pps ??= ServiceLocator.Current?.GetInstance<IPlatformStuffService>();
            _ram = GetRamGroup(_pps.GetMemoryInfo().UsedRam);
        }

        public async void OnLogged(LogEntry logEntry)
        {
            if (string.IsNullOrWhiteSpace(logEntry.EventId)) return;

            if (!_initialized)
            {
                _initialized = true;
                Initialize();
            }

            if (logEntry.Exception == null)
            {
                logEntry.IsEvent = true;
            }

            var pars = new Dictionary<string, object>();
            //pars["lic"] = Pix2DApp.Instance.CurrentLicense;

#if FULL_MODE
            pars["lic"] = "full";
#endif
            if (logEntry.ExtraParams != null && logEntry.ExtraParams.Any())
                foreach (var param in logEntry.ExtraParams)
                {
                    pars.Add(param.Key, param.Value);
                }

            var gaEvent = EventBuilders.BuildCustomEvent(logEntry.EventId, new Dictionary<string, object>());
            var request = new EventRequest("ClientId");
            request.AddEvent(gaEvent);
            //var result = await _mms.CreateEventRequest(request).Send(true);
            var result = await _mms.CreateEventRequest(request).Execute();

        }

        private string GetRamGroup(ulong ramAmount)
        {
            var mbs = Math.Floor((double)(ramAmount / 104857600)) * 100;
            return mbs + "+MB";
        }
    }
}