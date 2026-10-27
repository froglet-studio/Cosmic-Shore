using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Meerkat's wormhole to its HUD (<c>R_VesselActions/MEERKAT.md</c>). The Meerkat flies the Stoat's model and
    /// HUD as a stand-in until it has its own (<c>/vessel-model</c>, <c>/ability-icon</c>), so the view is
    /// <see cref="StoatHUDView"/>: its Time card shows the reticle growing back after a ride (ready once RT would form) and
    /// the ride's progress while a tunnel flies the hull. The other three cards are the open design slots of the element
    /// map and stay at rest.
    ///
    /// <para>Polled per frame from this vessel's OWN executor (a serialized reference on its prefab, vessel rule 14). The
    /// pilot gate runs every <see cref="Initialize"/>, so a re-init that hands this vessel to an AI or a remote owner
    /// stops driving the HUD (rule 5).</para>
    /// </summary>
    public class MeerkatHUDController : VesselHUDController
    {
        [Header("Meerkat")]
        [SerializeField] StoatHUDView view;

        [Tooltip("This vessel's wormhole (the reticle's readiness and the ride).")]
        [SerializeField] MeerkatWormholeExecutor wormhole;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!view) view = GetComponentInChildren<StoatHUDView>(true);
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !wormhole) return;
            if (wormhole.IsRiding) view.SetTime(1f - wormhole.Ride01, false);
            else view.SetTime(wormhole.Ready01, wormhole.CanForm);
        }
    }
}
