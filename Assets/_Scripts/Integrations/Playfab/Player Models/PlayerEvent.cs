using System;
using System.Collections.Generic;

namespace CosmicShore._Core.Playfab_Models.Player_Models
{
    public class PlayerEvent
    {
        public Dictionary<string, object> Body { get; set; }
        public Dictionary<string, string> CustomTags { get; set; }
        public string EventName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}