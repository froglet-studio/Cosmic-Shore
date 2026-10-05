// PORT Deviation — type-preserving SHELL of Assets/_Scripts/System/DailyChallengeSystem.cs
// (the daily-challenge meta system: daily game selection, per-tier reward state over
// local persistence, DailyRewardHandler payouts). Landed as a shell in the Hangar unit
// because GameplayRewardButton's DailyChallenge branch calls Instance.ClaimReward(tier)
// — the hangar's training-modal path never takes that branch. The shell's ClaimReward
// returns false (nothing satisfied on a fresh install), so the claim flash simply never
// fires until the real system ports with the daily-challenge unit.
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    public class DailyChallengeSystem : SingletonPersistent<DailyChallengeSystem>
    {
        /// <summary>Shell: no reward state yet — no tier is ever claimable.</summary>
        public bool ClaimReward(int tier) => false;
    }
}
