using System;

namespace CosmicShore.Integrations.Newton
{
    public class Profile : IProfile
    {
        public string Id { get; set; }
        public bool IsLoggedIn { get; set; }
        public DateTime LoginDate { get; set; }
        public string LoginContext { get; set; }
    }
}
