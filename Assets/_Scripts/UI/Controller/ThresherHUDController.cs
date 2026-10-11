using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Thresher's live state to <see cref="ThresherHUDView"/> (design:
    /// <c>R_VesselActions/THRESHER.md</c>).
    ///
    /// <para>Both signals are POLLED per frame from this vessel's OWN executor, a serialized
    /// reference on its prefab — never reached for by type through the hierarchy (rule 14). Ball
    /// speed and chain length are continuous quantities with no event behind them, so polling is
    /// the honest drive. Colours come from the executor in LINEAR space and are converted to gamma
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

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<ThresherHUDView>(true);

            // The fleet's pilot gate (ScarabHUDController / ButterflyHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !thresher) return;
            view.SetBallHeat(thresher.Gauge01, thresher.BallColorNow.gamma);
            view.SetChainOut(thresher.ChainOut01, thresher.IsReady, thresher.ReadyColorNow.gamma);
        }
    }
}
