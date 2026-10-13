// Ported from Assets/_Scripts/System/Economy/DailyRewardHandler.cs — verbatim
// (replaces the earlier type-preserving shell; path follows upstream's move into
// System/Economy/). One difference: upstream's CSDebug.LogVerbose(CSLogChannel.CloudData)
// trace in ClaimDailyChallengeReward is omitted — the port's CSDebug has no log
// channels, and that channel is off by default upstream.
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// The daily-reward claims, local only.
    ///
    /// <para>Every method here used to run a server-side function on a backend the game no
    /// longer uses, keyed on an auth context that was never populated, so the functions never
    /// ran. What the player actually saw came from the LOCAL half of each method, which is kept
    /// here verbatim.</para>
    ///
    /// <para>The class survives that backend's removal because <c>DailyRewardCard</c> and
    /// <c>DailyChallengeSystem</c> call it. Whatever replaces the server-side claim belongs
    /// behind these same methods.</para>
    /// </summary>
    public class DailyRewardHandler : SingletonPersistent<DailyRewardHandler>
    {
        /// <summary>
        /// Credits the daily reward locally. The server round trip that used to gate it (which
        /// also returned the next claim time) is gone, so this is now unconditional — which is
        /// what it already was in practice, since the function could never execute.
        /// </summary>
        public void Claim()
        {
            CatalogManager.Instance.RewardClaimed(Element.Omni, CatalogManager.DailyRewardAmount);
        }

        /// <summary>
        /// Credits a daily-challenge tier reward locally. The local credit was always ahead of the
        /// server call here anyway — the original carried a "TODO: P1 need to do this in the on
        /// success callback" against exactly that.
        /// </summary>
        public void ClaimDailyChallengeReward(int tier, int rewardValue)
        {
            CatalogManager.Instance.RewardClaimed(Element.Omni, rewardValue);
        }

        /// <summary>
        /// Spends one daily-challenge ticket. The balance check used to live in a server-side
        /// function that could not run; the caller performs the local decrement either way.
        /// </summary>
        public void PlayDailyChallenge(System.Action onComplete = null)
        {
            onComplete?.Invoke();
        }
    }
}
