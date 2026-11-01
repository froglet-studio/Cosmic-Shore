using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Meerkat's abilities to its HUD (<c>R_VesselActions/MEERKAT.md</c>). The Meerkat flies the Stoat's model and
    /// HUD as a stand-in until it has its own (<c>/vessel-model</c>, <c>/ability-icon</c>), so the view is
    /// <see cref="StoatHUDView"/>, and its icons are the Stoat's placeholders. The LOCKED four-icon row, charge → mass →
    /// space → time, follows the element map (Garrett's kit, approved by Yash 2026-10-28):
    /// <list type="bullet">
    /// <item><b>Charge, Sentinel mode</b>: the sentinels standing, out of the pool (3, 6 at L5).</item>
    /// <item><b>Mass, the exit portal</b>: how many of the last portal's six prisms still stand.</item>
    /// <item><b>Space, the soliton</b>: the reticle growing back after a ride (full once RT would form).</item>
    /// <item><b>Time, the ride</b>: the ride's progress through any soliton, in the ready colour while riding.</item>
    /// </list>
    /// The Stoat's view has no ready colour on its Charge and Space gauges, so Sentinel mode's on/off and RT's readiness read
    /// only as fills; a Meerkat HUD view of its own (with the Meerkat's icons) is a follow-up.
    ///
    /// <para>Polled per frame from this vessel's OWN executors (serialized references on its prefab, vessel rule 14). The
    /// pilot gate runs every <see cref="Initialize"/>, so a re-init that hands this vessel to an AI or a remote owner stops
    /// driving the HUD (rule 5).</para>
    /// </summary>
    public class MeerkatHUDController : VesselHUDController
    {
        [Header("Meerkat")]
        [SerializeField] StoatHUDView view;

        [Tooltip("This vessel's wormhole (the reticle's readiness and the ride).")]
        [SerializeField] MeerkatSolitonExecutor wormhole;

        [Tooltip("This vessel's soliton kit (the sentinels and the exit portal).")]
        [SerializeField] MeerkatSolitonKit kit;

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
            view.SetAccretion(wormhole.IsRiding ? 0f : wormhole.Ready01);   // the Space card: the soliton RT would form
            view.SetTime(wormhole.IsRiding ? 1f - wormhole.Ride01 : 0f, wormhole.IsRiding);
            if (!kit) return;
            view.SetRipple(kit.SentinelCount / (float)Mathf.Max(1, kit.SentinelPoolNow));
            view.SetMode(kit.PortalStanding / (float)MeerkatSolitonMath.PortalCount);
        }
    }
}
