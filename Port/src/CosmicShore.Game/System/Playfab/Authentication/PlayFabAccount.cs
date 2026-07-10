// Ported from Assets/_Scripts/System/Playfab/Authentication/PlayFabAccount.cs
// (Leaderboards unit 2026-07-10) — verbatim shape; PlayFab.PlayFabAuthenticationContext
// has no engine equivalent (the PlayFab SDK is legacy/inert upstream), so AuthContext
// carries an object placeholder — only the disabled online lanes ever read it.
using CosmicShore.Engine;

namespace CosmicShore.Core
{
    public class PlayFabAccount{
        public string ID { get; set; }
        public string UniqueID => SystemInfo.deviceUniqueIdentifier;
        public bool IsHost { get; set; }
        // PORT Deviation (Leaderboards unit): PlayFabAuthenticationContext → object.
        public object AuthContext { get; set; }
    }
}
