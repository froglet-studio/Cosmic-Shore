using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Scarab's live state to <see cref="ScarabHUDView"/> (design:
    /// R_VesselActions/SCARAB.md §12).
    ///
    /// Two signals, both event-driven — no per-frame polling:
    /// - <c>ResourceSystem.OnResourceChanged</c> → ball energy (index 0) and switch charges
    ///   (index 1). Subscribed on the vessel's OWN ResourceSystem, never reached for by type
    ///   through the hierarchy — a HUD controller that hunts another vessel's component compiles,
    ///   returns null on every vessel that isn't carrying it, and leaves a dead gauge with no
    ///   error (the Squirrel polled a Sparrow-only executor for its heat gauge for the
    ///   component's entire life that way).
    /// - <c>ScarabJukeController.OnJukeChargeChanged</c> → the binary juke pip, from a serialized
    ///   reference on this vessel's prefab so a missing wire is visible in the inspector.
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

        [Tooltip("This vessel's juke controller (root component). Serialized rather than " +
                 "type-searched so an unwired HUD is visible in the inspector.")]
        [SerializeField] ScarabJukeController jukeController;

        [Header("Resource indices")]
        [Tooltip("Meter that fills toward a ball (the Scarab authors index 0, 'Ball Energy').")]
        [SerializeField] int energyResourceIndex = 0;
        [Tooltip("Meter that holds switch charges (index 1, 'Switch Charges').")]
        [SerializeField] int switchResourceIndex = 1;
        [Tooltip("Charges per full switch meter — must match PlaceSwitchActionSO." +
                 "chargesPerFullMeter, so the pip count and the spend cost agree.")]
        [SerializeField, Min(1)] int switchChargesPerFullMeter = 3;

        ResourceSystem _resources;
        ScarabJukeController _boundJuke;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as ScarabHUDView;

            // Detach FIRST and unconditionally — above the pilot gate below.
            Unbind();

            if (vesselStatus == null) return;
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser) return;

            _resources = vesselStatus.ResourceSystem;
            if (!jukeController)
                jukeController = vesselStatus.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponent<ScarabJukeController>()
                    : GetComponent<ScarabJukeController>();
            _boundJuke = jukeController;

            if (_resources)
            {
                _resources.OnResourceChanged += HandleResourceChanged;
                SeedFromResources();
            }

            if (_boundJuke)
            {
                _boundJuke.OnJukeChargeChanged += HandleJukeChargeChanged;
                view?.SetJukeArmed(_boundJuke.IsJukeArmed);   // seed, don't wait for the first edge
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

        void HandleJukeChargeChanged(bool armed) => view?.SetJukeArmed(armed);

        void Unbind()
        {
            if (_resources) _resources.OnResourceChanged -= HandleResourceChanged;
            if (_boundJuke) _boundJuke.OnJukeChargeChanged -= HandleJukeChargeChanged;
            _resources = null;
            _boundJuke = null;
        }

        void OnDisable() => Unbind();
    }
}
