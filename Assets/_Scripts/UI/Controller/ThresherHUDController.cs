using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Thresher's live state to <see cref="ThresherHUDView"/> (design:
    /// <c>R_VesselActions/THRESHER.md</c>).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executor, a serialized
    /// reference on its prefab — never reached for by type through the hierarchy (rule 14). Ball
    /// speed and the chain's banked energy are continuous quantities with no event behind them, so
    /// polling is the honest drive. Colours come from the executor in LINEAR space and are converted to gamma
    /// here, at the UI consumer (contract §5.0).</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it,
    /// so a re-init that hands this vessel to an AI or a remote owner stops driving the HUD.</para>
    /// </summary>
    public class ThresherHUDController : VesselHUDController
    {
        [Header("Thresher")]
        [SerializeField] ThresherHUDView view;

        [Tooltip("This vessel's chain executor. Serialized rather than type-searched so an unwired " +
                 "HUD is visible in the inspector.")]
        [SerializeField] ThresherExecutor thresher;

        [Inject] GameDataSO _gameData;
        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view) view = GetComponentInChildren<ThresherHUDView>(true);

            // The fleet's pilot gate (ScarabHUDController / ButterflyHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !thresher) return;
            var domain = ResolveDomainSignalColor();
            view.SetBallHeat(thresher.Gauge01, thresher.BallColorNow.gamma);
            // The bank fills toward the pilot's domain, never the CTA lime (2026-11-07). Until the palette resolves
            // (alpha 0) it fills toward white.
            view.SetEnergy(thresher.EnergyRadius01, domain.a > 0f ? domain : Color.white);
            view.SetDomainColor(domain);
            view.SetBallState(thresher.IsHot, thresher.InMassMode, thresher.RightTriggerDepth, thresher.IsPivoting);
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the Manta's and
        /// the Brittle Star's HUDs read it; alpha 0 until the palette resolves (the view keeps its last colour).</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return new Color(0f, 0f, 0f, 0f);
            return colorSet.GetDomainSignalColor(_status.Domain);
        }
    }
}
