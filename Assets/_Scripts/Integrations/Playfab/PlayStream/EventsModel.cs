using System.Collections.Generic;
using PlayFab.EventsModels;

namespace CosmicShore.Integrations.Playfab
{
    public class EventsModel
    {
        public List<EventContents> EventContents { get; set; }
        public Dictionary<string, string> CustomTags { get; set; }
    }
}