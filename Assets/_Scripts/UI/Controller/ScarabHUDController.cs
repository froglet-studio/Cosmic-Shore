using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Scarab's live state to <see cref="ScarabHUDView"/> (design:
    /// R_VesselActions/SCARAB.md §12).
    ///
    /// Two signals, both event-driven — no per-frame polling in this class:
    /// - <c>ResourceSystem.OnResourceChanged</c> → ball energy (index 0, the SPACE row's Ball
    ///   Forge) and switch charges (index 1, the MASS row). Subscribed on the vessel's OWN
    ///   ResourceSystem, never reached for by type through the hierarchy — a HUD controller that
    ///   hunts another vessel's component compiles, returns null on every vessel that isn't
    ///   carrying it, and leaves a dead gauge with no error (the Squirrel polled a Sparrow-only
    ///   executor for its heat gauge for the component's entire life that way).
    /// - <c>ScarabCavitationBlast.OnBlastReadyChanged</c> → the CHARGE row's ready/recharging
    ///   readout, from a serialized reference on this vessel's prefab so a missing wire is
    ///   visible in the inspector. The edge carries the live cooldown length, so the optional
    ///   sweep ring is one tween per use.
    ///
    /// The right-stick DASH has no readout because it has no cooldown — it is always available
    /// (SCARAB.md §3.4). Only the blast that rides it is paced, and that is what the Charge row
    /// shows.
    ///
    /// Bindings are ONE symmetric attach/detach pair. The detach in <see cref="Initialize"/> runs
    /// ABOVE the pilot gate and <see cref="OnDisable"/> is unconditional and idempotent, so a
    /// re-init that hands this vessel to an AI or a remote owner cannot strand the previous
    /// pilot's handlers.
    /// </summary>
    public class ScarabHUDController : VesselHUDController
    {
        [Header("Scarab")]
        [SerializeField] ScarabHUDView view;

        [Tooltip("This vessel's cavitation blast (root component). Serialized rather than " +
                 "type-searched so an unwired HUD is visible in the inspector.")]
        [SerializeField] ScarabCavitationBlast cavitationBlast;

        [Header("Resource indices")]
        [Tooltip("Meter that fills toward a ball (the Scarab authors index 0, 'Ball Energy').")]
        [SerializeField] int energyResourceIndex = 0;
        [Tooltip("Meter that holds switch charges (index 1, 'Switch Charges').")]
        [SerializeField] int switchResourceIndex = 1;
        [Tooltip("Charges per full switch meter — must match PlaceSwitchActionSO." +
                 "chargesPerFullMeter, so the pip count and the spend cost agree.")]
        [SerializeField, Min(1)] int switchChargesPerFullMeter = 3;

        ResourceSystem _resources;
        ScarabCavitationBlast _boundBlast;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as ScarabHUDView;

            // Detach FIRST and unconditionally — above the pilot gate below.
            Unbind();

            if (vesselStatus == null) return;
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser) return;

            _resources = vesselStatus.ResourceSystem;
            if (!cavitationBlast)
                cavitationBlast = vesselStatus.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponent<ScarabCavitationBlast>()
                    : GetComponent<ScarabCavitationBlast>();
            _boundBlast = cavitationBlast;

            if (_resources)
            {
                _resources.OnResourceChanged += HandleResourceChanged;
                SeedFromResources();
            }

            if (_boundBlast)
            {
                _boundBlast.OnBlastReadyChanged += HandleBlastReadyChanged;
                view?.SetBlastReady(_boundBlast.IsBlastReady, 0f);   // seed, don't wait for an edge
            }
        }

        void SeedFromResources()
        {
            if (_resources == null) return;
            var list = _resources.Resources;
            if (IsValid(energyResourceIndex))
                HandleResourceChanged(energyResourceIndex, list[energyResourceIndex].CurrentAmount,
                                      list[energyResourceIndex].MaxAmount);
            if (IsValid(switchResourceIndex))
                HandleResourceChanged(switchResourceIndex, list[switchResourceIndex].CurrentAmount,
                                      list[switchResourceIndex].MaxAmount);
        }

        bool IsValid(int index)
            => _resources != null && index >= 0 && index < _resources.Resources.Count
               && _resources.Resources[index] != null;

        void HandleResourceChanged(int index, float current, float max)
        {
            if (!view || max <= 0f) return;

            if (index == energyResourceIndex)
            {
                float normalized = Mathf.Clamp01(current / max);
                // FULL is the beat that matters: at or above it the next crystal forges a ball
                // instead of topping up. Compared with a small epsilon because the meter lands on
                // its ceiling through repeated float adds.
                view.SetBallEnergy(normalized, normalized >= 1f - 0.0001f);
            }
            else if (index == switchResourceIndex)
            {
                // Floor, not round: the count must never claim a charge the spend gate would
                // refuse (PlaceSwitchActionExecutor tests against the cost with an epsilon).
                int charges = Mathf.FloorToInt(current / max * switchChargesPerFullMeter + 0.0001f);
                view.SetSwitchCharges(charges);
            }
        }

        void HandleBlastReadyChanged(bool ready, float cooldownSeconds)
            => view?.SetBlastReady(ready, cooldownSeconds);

        void Unbind()
        {
            if (_resources) _resources.OnResourceChanged -= HandleResourceChanged;
            if (_boundBlast) _boundBlast.OnBlastReadyChanged -= HandleBlastReadyChanged;
            _resources = null;
            _boundBlast = null;
        }

        void OnDisable() => Unbind();
    }
}
