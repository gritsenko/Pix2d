using System.Collections.Generic;
using System.Linq;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.Extensions;

public static class EventExtensions
{
    public static Event AddParameters(this Event gaEvent, string name, object value)
    {
        
        //Events can have a maximum of 25 parameters.
        if (gaEvent.EventParameters.Count == 25) return gaEvent;  // Need to thrown an error here.
        
        gaEvent.EventParameters.Add(name, value);
        return gaEvent;
    }

    public static List<KeyValuePair<string, object>> GetItems(this Event gaEvent)
    {

        return (List<KeyValuePair<string, object>>)gaEvent.EventParameters["items"];
    }

    public static Event AddItems(this Event gaEvent, string name, object value)
    {

        var itemsKeyName = "items";
        
        // If we dont have any items yet.
        if (!gaEvent.EventParameters.ContainsKey(itemsKeyName))
        {
            gaEvent.EventParameters.Add(itemsKeyName, new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>(name,value)
            });
            return gaEvent;
        }

        // rewrite them all back
        var items = (List<KeyValuePair<string, object>>)gaEvent.EventParameters[itemsKeyName];

        // if this item already exists we remove it and add a new one.

        var found = items.FirstOrDefault(a=> a.Key.Equals(name));
        if (found.Key == name) items.Remove(found);
        items.Add(new KeyValuePair<string, object>(name,value));
        
        // rest items.
        gaEvent.EventParameters[itemsKeyName] = items;

        return gaEvent;
    }
}