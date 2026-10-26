using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Stoat's live state to <see cref="StoatHUDView"/> (design: <c>R_VesselActions/STOAT_DIPOLE.md</c> §1d).
    ///
    /// <para>Every signal is POLLED per frame from this vessel's OWN executors — serialized references on its prefab,
    /// never reached for by type through the hierarchy (vessel rule 14), so an unwired HUD shows in the inspector. The
    /// quantities are continuous with no event behind them (a separation, an eased mode, a boost), so polling is the
    /// honest drive.</para>
    ///
    /// <para>The pilot gate runs every <see cref="Initialize"/>, above anything that depends on it, so a re-init that
    /// hands this vessel to an AI or a remote owner stops driving the HUD (rule 5).</para>
    /// </summary>
    public class StoatHUDController : VesselHUDController
    {
        [Header("Stoat")]
        [SerializeField] StoatHUDView view;

        [Tooltip("This vessel's mode switch (Mass / Time / Drive).")]
        [SerializeField] StoatModeExecutor mode;
        [Tooltip("This vessel's field dipole (the ripple a let-go would throw; Space-5 Accretion).")]
        [SerializeField] StoatDipoleExecutor dipole;
        [Tooltip("This vessel's orbit slingshot (Time mode's timing).")]
        [SerializeField] StoatSlingExecutor sling;
        [Tooltip("This vessel's pathfinder (the warp boost).")]
        [SerializeField] StoatPathfinderExecutor pathfinder;

        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = GetComponentInChildren<StoatHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / ScarabHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view) return;
            if (dipole)
            {
                view.SetRipple(dipole.RippleCharge01);
                view.SetAccretion(dipole.Accretion01);
            }
            if (mode)
            {
                view.SetMode(mode.MassMode01);
                view.SetModeIcon(mode.Current);
            }

            // Time: progress to the sweet spot while the orbit holds the hull. Drive: the warp pair's warp as a share of its
            // top, ready past half. Mass: the pathfinder's boost.
            bool timeMode = mode && mode.IsTimeMode;
            if (timeMode && sling && sling.IsOrbiting)
                view.SetTime(sling.SweetProgress01, sling.Timing01 >= 0.999f);
            else if (mode && mode.IsDriveMode && pathfinder)
                view.SetTime(pathfinder.DriveWarp01, pathfinder.IsWarped);
            else if (!timeMode && pathfinder)
            {
                float top = pathfinder.BoostTarget;
                view.SetTime(top > 1.001f ? (pathfinder.BoostMultiplier - 1f) / (top - 1f) : 0f, pathfinder.IsWarped);
            }
            else view.SetTime(0f, false);
        }
    }
}
