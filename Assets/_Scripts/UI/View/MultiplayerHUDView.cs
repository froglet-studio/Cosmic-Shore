using UnityEngine;

namespace CosmicShore.UI
{
    public class MultiplayerHUDView : MiniGameHUDView
    {
        [Header("Domain Score Panels (in-game team scores)")]
        [Tooltip("ONE centred row holding every domain's column side by side - score on top, that " +
                 "team's player icons underneath. Assign this and the ally/opposing split below is " +
                 "ignored: the local domain is simply the first column, so the layout reads as one " +
                 "divided block instead of two groups flanking a player card.")]
        [SerializeField] private Transform domainBarContainer;

        [Tooltip("LEGACY split layout - the LOCAL player's domain panel, left of a centred player " +
                 "score. Only used when Domain Bar Container is empty.")]
        [SerializeField] private Transform allyDomainContainer;

        [Tooltip("LEGACY split layout - the 1-2 opposing-domain panels, right of the centred " +
                 "player score. Only used when Domain Bar Container is empty.")]
        [SerializeField] private Transform opposingDomainsContainer;

        [Tooltip("Domain panel prefab (sum text on top, small avatar row underneath). Leave unassigned to fall back to per-player cards in PlayerScoreContainer.")]
        [SerializeField] private DomainScorePanel domainPanelPrefab;

        /// <summary>
        /// Where the LOCAL player's domain column goes. The single-bar layout answers both this
        /// and <see cref="OpposingDomainsContainer"/> with the same transform, so
        /// <c>MultiplayerHUD</c>'s build order (local first, then opposing in enum order) lays the
        /// columns out left-to-right in one row with no branch of its own.
        /// </summary>
        public Transform AllyDomainContainer => domainBarContainer != null ? domainBarContainer : allyDomainContainer;

        public Transform OpposingDomainsContainer => domainBarContainer != null ? domainBarContainer : opposingDomainsContainer;

        /// <summary>True when the columns share one centred row rather than flanking a player card.</summary>
        public bool UsesSingleDomainBar => domainBarContainer != null;

        public DomainScorePanel DomainPanelPrefab => domainPanelPrefab;

        /// <summary>
        /// True when the domain-grouped layout is fully wired in the inspector.
        /// MultiplayerHUD uses this to decide between the new (per-domain) and
        /// legacy (per-player) layouts at runtime.
        /// </summary>
        public bool HasDomainPanelWiring =>
            AllyDomainContainer != null
            && OpposingDomainsContainer != null
            && domainPanelPrefab != null;

        public void ClearDomainPanels()
        {
            // Both accessors resolve to the same transform in the single-bar layout; clearing it
            // twice is a no-op the second time, so this needs no branch.
            ClearChildren(AllyDomainContainer);
            ClearChildren(OpposingDomainsContainer);
        }

        static void ClearChildren(Transform container)
        {
            if (!container) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}
