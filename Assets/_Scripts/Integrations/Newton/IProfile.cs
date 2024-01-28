using System;

namespace CosmicShore.Integrations.Newton
{
    public interface IProfile
    {
        string Id { get; set; }
        bool IsLoggedIn { get; set; }
        DateTime LoginDate { get; set; }
        string LoginContext { get; set; }
    }
}