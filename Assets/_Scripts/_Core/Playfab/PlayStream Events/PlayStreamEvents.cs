using System.Collections.Generic;
using PlayFab.EventsModels;

namespace _Scripts._Core.Playfab_Models.Event_Models
{
    public class PlayStreamEvents
    {
        public List<EventContents> EventContents { get; set; }
        public Dictionary<string, string> CustomTags { get; set; }
    }
}