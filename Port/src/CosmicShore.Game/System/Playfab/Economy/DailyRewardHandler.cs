// PORT Deviation — type-preserving SHELL of
// Assets/_Scripts/System/Playfab/Economy/DailyRewardHandler.cs (140 lines of PlayFab
// CloudScript execution: PlayDailyChallenge / daily reward claims through Azure
// functions, all inert upstream with PlayFab disabled). The shell preserves the one
// surface live code consumes: Instance.PlayDailyChallenge(callback) — like an
// unanswered cloud call, the callback never fires (identical to upstream today).
// The ExecuteFunctionResult payload type did not survive the port; the callback is
// object-typed at this seam.
using System;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class DailyRewardHandler : SingletonPersistent<DailyRewardHandler>
    {
        /// <summary>Shell: the PlayFab cloud function never answers (inert upstream too).</summary>
        public void PlayDailyChallenge(Action<object> playDailyChallengeSuccess)
        {
            CSDebug.Log("DailyRewardHandler.PlayDailyChallenge - PlayFab CloudScript not present (legacy lane)");
        }
    }
}
