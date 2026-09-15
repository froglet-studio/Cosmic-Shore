using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// The daily-reward claims, minus PlayFab.
    ///
    /// <para>Every method here used to run a PlayFab CloudScript Azure Function through
    /// <c>CloudScriptRunner</c>, keyed on an <c>EntityKey</c> minted from the PlayFab auth context.
    /// That context was never populated — <c>AuthenticationManager.Awake()</c> early-returned — so
    /// the entity was always null and the functions never ran. What the player actually saw came
    /// from the LOCAL half of each method, which is kept here verbatim.</para>
    ///
    /// <para>The class survives PlayFab's removal because <c>DailyRewardCard</c> and
    /// <c>DailyChallengeSystem</c> call it; see <c>Docs/PLAYFAB_RETIREMENT.md</c> §2a. Whatever
    /// replaces the server-side claim belongs behind these same two methods.</para>
    /// </summary>
    public class DailyRewardHandler : SingletonPersistent<DailyRewardHandler>
    {
        /// <summary>
        /// Credits the daily reward locally. The server round trip that used to gate it
        /// (the "Claim" Azure Function, which also returned the next claim time) is gone with
        /// PlayFab, so this is now unconditional — which is what it already was in practice,
        /// since the function could never execute.
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

            CSDebug.LogVerbose(CSLogChannel.CloudData,
                $"[DailyRewardHandler] ClaimDailyChallengeReward - tier:{tier}, value:{rewardValue}");
        }

        /// <summary>
        /// Spends one daily-challenge ticket. The balance check used to live in a CloudScript
        /// function that could not run; the caller performs the local decrement either way.
        /// </summary>
        public void PlayDailyChallenge(System.Action onComplete = null)
        {
            onComplete?.Invoke();
        }
    }
}
