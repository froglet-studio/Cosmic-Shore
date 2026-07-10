// PORT Deviation — type-preserving SHELL of
// Assets/_Scripts/System/Playfab/PlayerData/PlayerDataController.cs (186 lines of
// legacy PlayFab profile loading, deprecated + inert upstream — PlayerDataService over
// UGS Cloud Save is the live profile path). The shell preserves the one surface still
// consumed by live code: the static OnProfileLoaded event (LeaderboardsMenu refreshes
// its leaderboard on it; LeaderboardManager flushes offline stats on it). With PlayFab
// inert the event simply never fires upstream — the internal raise hook below exists
// for tests to exercise the subscribers.
using System;

namespace CosmicShore.Core
{
    public class PlayerDataController
    {
        public static event Action OnProfileLoaded;

        internal static void RaiseProfileLoadedForTest() => OnProfileLoaded?.Invoke();
    }
}
