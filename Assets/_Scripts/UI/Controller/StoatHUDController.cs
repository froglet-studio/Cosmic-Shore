using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
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

        [Tooltip("This vessel's mode (Time only since 2026-11-02).")]
        [SerializeField] StoatModeExecutor mode;
        [Tooltip("This vessel's field dipole (the ripple a let-go would throw; Space-5 Accretion).")]
        [SerializeField] StoatDipoleExecutor dipole;
        [Tooltip("This vessel's orbit slingshot (Time mode's timing).")]
        [SerializeField] StoatSlingExecutor sling;
        [Tooltip("This vessel's flight clock (the warp) and rings.")]
        [SerializeField] StoatPathfinderExecutor pathfinder;

        [Tooltip("Seconds after a let-go that the Mass and Time cards show the launch.")]
        [SerializeField, Min(0f)] float launchShownSeconds = 1.2f;

        [Inject] GameDataSO _gameData;
        IVesselStatus _status;
        bool _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _status = vesselStatus;

            if (!view) view = GetComponentInChildren<StoatHUDView>(true);

            // The fleet's pilot gate (ThresherHUDController / ScarabHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !view) return;
            // The orbit alone (2026-11-02): Charge and Space read the sling — the ripple the open pair would throw and
            // Space-5 Accretion on the orbit. The dipole only drives them if it is ever back in the kit.
            if (sling && (!dipole || !dipole.isActiveAndEnabled || !dipole.IsOpen))
            {
                view.SetRipple(sling.RippleCharge01);
                view.SetAccretion(sling.Accretion01);
            }
            else if (dipole)
            {
                view.SetRipple(dipole.RippleCharge01);
                view.SetAccretion(dipole.Accretion01);
            }
            // The Mass card is Heavy Launch now: it keeps its authored icon (no mode swap) and its gauge rests.
            if (mode) view.SetMode(0f);

            // Time: progress to the sweet spot while the orbit holds the hull, the domain in the window.
            if (sling && sling.IsOrbiting) view.SetTime(sling.SweetProgress01, sling.Timing01 >= 0.999f);
            else view.SetTime(0f, false);

            // The Skim Sling's run: SKIP ×N after each skip, held while that skip is ridden, then faded.
            if (sling && sling.UsesSkim && sling.Config && sling.Config.Skim)
            {
                float hold = sling.Config.Skim.SkipCounterSeconds;
                float age = sling.SkipFxAge;
                bool riding = sling.IsOrbiting && sling.SkipCount > 0;
                float alpha = riding ? 1f : age >= 0f && age < hold ? 1f - Mathf.Clamp01((age - 0.5f * hold) / (0.5f * hold)) : 0f;
                view.SetSkip(riding ? sling.SkipCount : sling.SkipFxCount, alpha, sling.SkipFxPerfect);
            }
            else view.SetSkip(0, 0f, false);

            view.SetDomainColor(ResolveDomainSignalColor());
            if (sling)
                view.SetOrbitState(sling.HasPair || sling.IsHoldingAny, sling.IsOrbiting, sling.Timing01 >= 0.999f,
                                   sling.SinceLaunch < launchShownSeconds);
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
