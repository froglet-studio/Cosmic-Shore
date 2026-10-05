// PORT Deviation — type-preserving SHELL of
// Assets/_Scripts/System/Playfab/Authentication/AuthenticationManager.cs (343 lines of
// legacy PlayFab auth, deprecated + inert upstream — UGS AuthenticationServiceFacade is
// the live auth path). The shell preserves the one surface still consumed by live code:
// the static PlayFabAccount (LeaderboardsMenu's WaitUntil gate + player-row highlight,
// LeaderboardManager's offline flush gate). Upstream initializes it non-null
// (`{ get; private set; } = new()`), so the gates pass immediately — identical here.
namespace CosmicShore.Core
{
    public class AuthenticationManager
    {
        public static PlayFabAccount PlayFabAccount { get; private set; } = new();
    }
}
