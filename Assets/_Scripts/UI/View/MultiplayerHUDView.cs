using UnityEngine;

namespace CosmicShore.UI
{
    public class MultiplayerHUDView : MiniGameHUDView
    {
        [Header("Domain Score Panels (in-game team scores)")]
        [Tooltip("Container holding the LOCAL player's domain panel - placed to the LEFT of the centered player score (was the empty slot in the previous layout).")]
        [SerializeField] private Transform allyDomainContainer;

        [Tooltip("Container holding the 1-2 opposing-domain panels - placed to the RIGHT of the centered player score (was the per-player icon row).")]
        [SerializeField] private Transform opposingDomainsContainer;

        [Tooltip("Domain panel prefab (sum text on top, small avatar row underneath). Leave unassigned to fall back to per-player cards in PlayerScoreContainer.")]
        [SerializeField] private DomainScorePanel domainPanelPrefab;

        public Transform AllyDomainContainer => allyDomainContainer;
        public Transform OpposingDomainsContainer => opposingDomainsContainer;
        public DomainScorePanel DomainPanelPrefab => domainPanelPrefab;

        /// <summary>
        /// True when the domain-grouped layout is fully wired in the inspector.
        /// MultiplayerHUD uses this to decide between the new (per-domain) and
        /// legacy (per-player) layouts at runtime.
        /// </summary>
        public bool HasDomainPanelWiring =>
            allyDomainContainer != null
            && opposingDomainsContainer != null
            && domainPanelPrefab != null;

        public void ClearDomainPanels()
        {
            ClearChildren(allyDomainContainer);
            ClearChildren(opposingDomainsContainer);
        }

        static void ClearChildren(Transform container)
        {
            if (!container) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }
    }
}
