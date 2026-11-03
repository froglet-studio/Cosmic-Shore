using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Meerkat's abilities to its HUD (<c>R_VesselActions/MEERKAT.md</c>). Since 2026-11-03 the Meerkat has its own
    /// row (<see cref="MeerkatHUDView"/> in <c>MeerkatHUDVariant</c>, icons from <c>Tools/Build/icon_kit/specs/meerkat.py</c>);
    /// until then it wore the Stoat's HUD and placeholder icons. The LOCKED four-icon row, charge → mass → space → time,
    /// follows the element map (Garrett's kit, approved by Yash 2026-10-28):
    /// <list type="bullet">
    /// <item><b>Charge, Sentinel mode</b>: the sentinels standing, out of the pool (3, 6 at L5), lit while Y has it on.</item>
    /// <item><b>Mass, the exit portal</b>: how many of the last portal's six prisms still stand.</item>
    /// <item><b>Space, the soliton</b>: the reticle growing back after a ride, lit once RT would form.</item>
    /// <item><b>Time, the ride</b>: what is left of the ride through any soliton, lit while riding.</item>
    /// </list>
    ///
    /// <para>Polled per frame from this vessel's OWN executors (serialized references on its prefab, vessel rule 14). The
    /// pilot gate runs every <see cref="Initialize"/>, so a re-init that hands this vessel to an AI or a remote owner stops
    /// driving the HUD (rule 5).</para>
    /// </summary>
    public class MeerkatHUDController : VesselHUDController
    {
        [Header("Meerkat")]
        [SerializeField] MeerkatHUDView view;

        [Tooltip("This vessel's wormhole (the reticle's readiness and the ride).")]
        [SerializeField] MeerkatSolitonExecutor wormhole;

        [Tooltip("This vessel's soliton kit (the sentinels and the exit portal).")]
        [SerializeField] MeerkatSolitonKit kit;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!view) view = GetComponentInChildren<MeerkatHUDView>(true);
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view || !wormhole) return;
            bool riding = wormhole.IsRiding;
            view.SetSoliton(riding ? 0f : wormhole.Ready01, !riding && wormhole.CanForm);
            if (wormhole.IsInVoid) view.SetRide(wormhole.VoidLeft01, true);   // round 28: the void run's clock
            else view.SetRide(riding ? 1f - wormhole.Ride01 : 0f, riding);
            if (!kit) return;
            view.SetSentinels(kit.SentinelCount / (float)Mathf.Max(1, kit.SentinelPoolNow), kit.SentinelMode);
            view.SetPortal(kit.PortalStanding / (float)MeerkatSolitonMath.PortalCount);
        }
    }
}
