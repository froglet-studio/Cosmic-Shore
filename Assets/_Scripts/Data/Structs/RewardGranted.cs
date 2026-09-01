using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// The announcement that a reward actually landed - raised once by <c>RewardService</c>
    /// after the wallet write succeeds, and the ONLY thing reward UI listens to.
    ///
    /// It carries the resulting balance rather than letting each display re-read it, so the
    /// end-game panel and the menu toast are showing the same number as each other and as the
    /// write that produced it. A display that re-reads is a display that can disagree.
    /// </summary>
    [Serializable]
    public struct RewardGranted
    {
        /// <summary>What was granted.</summary>
        public RewardGrant Grant;

        /// <summary>
        /// Balance BEFORE this grant, so a display can count up from it without having had to
        /// observe the wallet earlier.
        /// </summary>
        public int PreviousCrystalBalance;

        /// <summary>
        /// Crystal balance AFTER this grant. Meaningful for
        /// <see cref="RewardKind.Crystals"/>; for an entitlement it is the balance as it stood,
        /// unchanged.
        /// </summary>
        public int NewCrystalBalance;

        public RewardGranted(RewardGrant grant, int previousCrystalBalance, int newCrystalBalance)
        {
            Grant = grant;
            PreviousCrystalBalance = previousCrystalBalance;
            NewCrystalBalance = newCrystalBalance;
        }

        /// <summary>Crystals actually added by this grant (0 for an entitlement).</summary>
        public int CrystalDelta => NewCrystalBalance - PreviousCrystalBalance;
    }
}
