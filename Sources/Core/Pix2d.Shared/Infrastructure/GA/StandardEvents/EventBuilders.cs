using System.Collections.Generic;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.StandardEvents;

public static class EventBuilders
{
    /// <summary>
    /// Custom event. 
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="eventParameters"></param>
    /// <returns></returns>
    public static Event BuildCustomEvent(string eventName, Dictionary<string, object> eventParameters)
    {
        var data = new Event(eventName);
        data.EventParameters = eventParameters;
        return data;
    }
    
   
    
    /*/// <summary>
    /// When a user clicks an ad Publisher events coming from AdMob via the Google Mobile Ads SDK
    /// This event is not exported to BigQuery.
    /// </summary>
    /// <param name="adEventId"></param>
    /// <returns></returns>
    public static Event BuildAdImpressionEvent(string adEventId)
    {
        var data = new Event("ad_impression");
        data.AddParameter("ad_event_id", adEventId);
        data.AddParameter("ad_event_id", adEventId);
        data.AddParameter("ad_event_id", adEventId);
        data.AddParameter("ad_event_id", adEventId);
        data.AddParameter("ad_event_id", adEventId);
        data.AddParameter("ad_event_id", adEventId);
        return data;
    }

    public static void AddCustomEvent(this EventPayload eventPayload,IEvent GaEvent)
    {
        eventPayload.Events[]
    }

    /// <summary>
    /// when at least one ad served by the Mobile Ads SDK is on screen
    /// This event is not exported to BigQuery.
    /// </summary>
    /// <param name="firebaseScreen"></param>
    /// <param name="firebaseScreenId"></param>
    /// <param name="firebaseScreenClass"></param>
    /// <param name="exposureTime"></param>
    /// <returns></returns>
    public static Event BuildAdExposureEvent(string firebaseScreen, string firebaseScreenId, string firebaseScreenClass, string exposureTime)
    {
        var data = new Event("ad_exposure");
        data.AddParameter("firebase_screen", firebaseScreen);
        data.AddParameter("firebase_screen_id", firebaseScreenId);
        data.AddParameter("firebase_screen_class", firebaseScreenClass);
        data.AddParameter("exposure_time", exposureTime);
        return data;
    }*/
    
}