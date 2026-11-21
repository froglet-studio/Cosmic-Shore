using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// One row of the weekly leaderboard's inline rewards table ("4th - 20th  10"). The band the
    /// local pilot's rank falls in is outlined and tagged "You" (decision Q4 fixed the bands so they
    /// no longer overlap at 20). The amounts are authored on the row; the panel only says which
    /// band is yours.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Leaderboard Reward Band")]
    public class LeaderboardRewardBand : MonoBehaviour
    {
        [Tooltip("Best rank the band pays (1 for the winner's band).")]
        [SerializeField, Min(1)] private int minRank = 1;
        [Tooltip("Worst rank the band pays. 0 = open-ended (\"21st and below\").")]
        [SerializeField, Min(0)] private int maxRank = 1;
        [Tooltip("The outline and the \"You\" tag, shown on the local pilot's band.")]
        [SerializeField] private CanvasGroup localMark;

        public bool Contains(int rank) => rank >= minRank && (maxRank == 0 || rank <= maxRank);

        /// <summary>Mark this band when <paramref name="localRank"/> (0 = not ranked) falls in it.</summary>
        public void SetLocalRank(int localRank) =>
            KitVisibility.SetShown(localMark, localRank > 0 && Contains(localRank));
    }
}
