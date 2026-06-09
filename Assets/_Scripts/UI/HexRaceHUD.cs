using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    public class HexRaceHUD : MultiplayerHUD
    {
        [Header("Mass / Phase Indicator (Skim Race)")]
        [Tooltip("Show the concentric-hexagon volume indicator (the main-menu volume gauge in phase-ring mode). " +
                 "Rings light up in the dominant domain's color as the cell's summed trail mass crosses each phase " +
                 "threshold (Quiet/Settled/Restless/Frozen/Rabid) — a live read on when the food web ramps up.")]
        [SerializeField] bool showMassPhaseIndicator = true;
        [Tooltip("Optional pre-placed indicator. Leave null to auto-create a host in the HUD corner at the position/size below.")]
        [SerializeField] DomainVolumeIndicator massPhaseIndicator;
        [Tooltip("Anchored position of the auto-created indicator (top-left anchored). Ignored when a pre-placed indicator is wired.")]
        [SerializeField] Vector2 indicatorAnchoredPos = new(150f, -150f);
        [Tooltip("Size of the auto-created indicator host rect.")]
        [SerializeField] Vector2 indicatorSize = new(220f, 220f);

        protected override void Start()
        {
            base.Start();
            EnsureMassPhaseIndicator();
        }

        /// <summary>
        /// Brings the main-menu volume gauge into Skim Race in concentric-phase-ring mode.
        /// Reuses the same <see cref="DomainVolumeIndicator"/> + <see cref="DomainVolumeHexGraphic"/>
        /// the menu pause button uses; the only difference is the layout flag. AddComponent'd
        /// components never get Reflex injection, so GameDataSO is handed over explicitly.
        /// </summary>
        void EnsureMassPhaseIndicator()
        {
            if (!showMassPhaseIndicator) return;

            if (!massPhaseIndicator)
                massPhaseIndicator = CreateIndicatorHost();

            if (!massPhaseIndicator) return;

            massPhaseIndicator.SetGameData(gameData);
            massPhaseIndicator.SetConcentricPhaseMode(true);
        }

        DomainVolumeIndicator CreateIndicatorHost()
        {
            // Parent under the HUD's own canvas so the gauge renders with the rest of
            // the in-game UI. The indicator self-constructs its DomainVolumeHexGraphic
            // child to fill this host rect (ElementalBarsView zero-authoring idiom).
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas ? canvas.transform : transform;

            var go = new GameObject("MassPhaseIndicator (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f); // top-left
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = indicatorAnchoredPos;
            rt.sizeDelta = indicatorSize;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            return go.AddComponent<DomainVolumeIndicator>();
        }

        protected override int GetInitialCardValue(IRoundStats stats)
        {
            return stats.OmniCrystalsCollected;
        }

        protected override void SubscribeToPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged += HandleCrystalStatChanged;
        }

        protected override void UnsubscribeFromPlayerStats(IRoundStats stats)
        {
            stats.OnOmniCrystalsCollectedChanged -= HandleCrystalStatChanged;
        }

        private void HandleCrystalStatChanged(IRoundStats updatedStats)
        {
            HandlePlayerStatChanged(updatedStats);
        }
    }
}
