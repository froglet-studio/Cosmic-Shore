using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds the Scarab's live state to <see cref="ScarabHUDView"/> (design:
    /// R_VesselActions/SCARAB.md §12).
    ///
    /// Two signals, both event-driven — no per-frame polling in this class:
    /// - <c>ResourceSystem.OnResourceChanged</c> → switch charges (index 1, the MASS row) and that
    ///   row's COOLDOWN VEIL. <c>OnElementLevelChange</c> → the Switch ball's scale (Mass) and the
    ///   seeded ball's size on the SPACE row's Nucleus Seeding card (Space). The ball-energy meter
    ///   (index 0) has no readout since the Space slot became Nucleus Seeding (2026-10-29).
    ///   Subscribed on the vessel's OWN
    ///   ResourceSystem, never reached for by type through the hierarchy — a HUD controller that
    ///   hunts another vessel's component compiles, returns null on every vessel that isn't
    ///   carrying it, and leaves a dead gauge with no error (the Squirrel polled a Sparrow-only
    ///   executor for its heat gauge for the component's entire life that way).
    /// - <c>ScarabCavitationBlast.OnBlastReadyChanged</c> → the CHARGE row's (the Shove's) ready/recharging
    ///   readout, from a serialized reference on this vessel's prefab so a missing wire is
    ///   visible in the inspector. The edge carries the live cooldown length, so the optional
    ///   sweep ring is one tween per use. Each edge (and the seed) also repaints the card's hull in the
    ///   pilot's domain colour, so the Charge card needs no per-frame poll either.
    ///
    /// The right-stick DASH has no readout because it has no cooldown — it is always available
    /// (SCARAB.md §3.4). Only the blast that rides it is paced, and that is what the Charge row
    /// shows.
    ///
    /// <b>The MASS row carries two readouts and they answer different questions.</b> The pip count
    /// says how many rings the pilot HOLDS; the fleet's cooldown veil
    /// (<c>VesselHUDView.SetAbilityCooldown</c>) says whether the button does anything if pressed
    /// RIGHT NOW — so it is clear the moment a single charge is banked and only sweeps while the
    /// bank is empty. That is the fleet's meaning of a veil and it is deliberately not "progress
    /// toward the next charge": a tank of three cannot say both on one dial (the split
    /// <c>Docs/CLAUDE.md</c> records for the Sparrow's rocket bay), and a veil drawn over a
    /// pressable button is the one reading a player cannot recover from. Nothing else drives the
    /// Scarab's switch cooldown, because the meter IS the cooldown: <c>PlaceSwitchActionExecutor</c>
    /// trickles it back at <c>rechargeSecondsPerCharge</c>, so the veil is a pure function of the
    /// resource event this controller was already subscribed to — no polling, no second clock.
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
        [Tooltip("Meter that holds switch charges (index 1, 'Switch Charges').")]
        [SerializeField] int switchResourceIndex = 1;
        [Tooltip("Charges per full switch meter — must match PlaceSwitchActionSO." +
                 "chargesPerFullMeter, so the pip count and the spend cost agree. At the " +
                 "shipped 1 the meter IS the single charge: the icon is lit or it is not, and " +
                 "the depleting veil below carries the whole answer to \"can I plant one\".")]
        [SerializeField, Min(1)] int switchChargesPerFullMeter = 1;

        [Tooltip("The Scarab's place-switch action (PlaceSwitchAction.asset): its switchScale is the live Mass " +
                 "ring scale the Switch card's ball shrinks by. Unwired, the ball stays at its Mass 1 size.")]
        [SerializeField] PlaceSwitchActionSO placeSwitch;

        [Inject] GameDataSO _gameData;

        ResourceSystem _resources;
        ScarabCavitationBlast _boundBlast;
        IVesselStatus _status;

        // The switch meter now RECHARGES continuously (PlaceSwitchActionExecutor), so
        // OnResourceChanged fires every frame it is filling. SetSwitchCharges starts a colour
        // tween, and a tween restarted every frame never advances - the icon would sit frozen on
        // the colour it had when the recharge began. The readout is a discrete count, so only a
        // change in that count is news. -1 means "nothing shown yet", which no real count can be.
        int _lastSwitchCharges = -1;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!view) view = View as ScarabHUDView;

            // Detach FIRST and unconditionally — above the pilot gate below.
            Unbind();

            if (vesselStatus == null) return;
            if (vesselStatus.IsInitializedAsAI || !vesselStatus.IsLocalUser) return;

            _status = vesselStatus;
            _resources = vesselStatus.ResourceSystem;
            if (!cavitationBlast)
                cavitationBlast = vesselStatus.ShipTransform
                    ? vesselStatus.ShipTransform.GetComponent<ScarabCavitationBlast>()
                    : GetComponent<ScarabCavitationBlast>();
            _boundBlast = cavitationBlast;

            if (_resources)
            {
                view?.SetSwitchRingColor(ResolveDomainSignalColor());
                view?.SetSeedColor(ResolveDomainSignalColor());
                _resources.OnResourceChanged += HandleResourceChanged;
                _resources.OnElementLevelChange += HandleElementLevelChange;
                SeedFromResources();
                PushSwitchScale();
                PushSeedScale();
            }

            if (_boundBlast)
            {
                _boundBlast.OnBlastReadyChanged += HandleBlastReadyChanged;
                view?.SetBlastHullColor(ResolveDomainSignalColor());
                view?.SetBlastReady(_boundBlast.IsBlastReady, 0f);   // seed, don't wait for an edge
            }
        }

        void SeedFromResources()
        {
            if (_resources == null) return;
            var list = _resources.Resources;
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

            if (index == switchResourceIndex)
            {
                // Floor, not round: the count must never claim a charge the spend gate would
                // refuse (PlaceSwitchActionExecutor tests against the cost with an epsilon).
                float perCharge = max / Mathf.Max(1, switchChargesPerFullMeter);
                int charges = Mathf.FloorToInt(current / perCharge + 0.0001f);

                // The veil, BEFORE the count early-out: the recharge is a smooth trickle, so this
                // is the one signal here that has to move on every event rather than only when the
                // discrete count changes. Clear whenever a charge is banked (the button works);
                // otherwise how much of the first one is still owed. SetAbilityCooldown is written
                // for exactly this call pattern - it early-outs while parked at ready.
                view.SetAbilityCooldown(Element.Mass,
                    charges >= 1 ? 0f : 1f - Mathf.Clamp01(current / perCharge));

                if (charges == _lastSwitchCharges) return;
                _lastSwitchCharges = charges;
                view.SetSwitchRingColor(ResolveDomainSignalColor());
                view.SetSwitchCharges(charges);
            }
        }

        void HandleElementLevelChange(Element element, int level)
        {
            if (element == Element.Mass) PushSwitchScale();
            else if (element == Element.Space) PushSeedScale();
        }

        /// <summary>The Nucleus Seeding card's ball grows with Space - the size the seeder mints at
        /// (ScarabBallForge.SizeScaleFor, read live at the level change).</summary>
        void PushSeedScale()
        {
            if (!view || _status == null) return;
            view.SetSeedScale(ScarabBallForge.SizeScaleFor(_status));
        }

        /// <summary>The Switch card's ball shrinks as Mass grows the ring (switchScale, read live at the
        /// level change - the same value the executor sizes the ring with).</summary>
        void PushSwitchScale()
        {
            if (!view || !placeSwitch || _status == null) return;
            view.SetSwitchScale(placeSwitch.SwitchScale.EvaluateLive(_status));
        }

        void HandleBlastReadyChanged(bool ready, float cooldownSeconds)
        {
            if (!view) return;
            view.SetBlastHullColor(ResolveDomainSignalColor());
            view.SetBlastReady(ready, cooldownSeconds);
        }

        /// <summary>The pilot's domain at full brightness (<c>SO_ColorSet.GetDomainSignalColor</c>), as the
        /// Rhino's and the Dolphin's hull cards read it.</summary>
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _status == null) return Color.white;
            return colorSet.GetDomainSignalColor(_status.Domain);
        }

        void Unbind()
        {
            // A re-init must re-push the count, so forget what was last shown.
            _lastSwitchCharges = -1;
            if (_resources)
            {
                _resources.OnResourceChanged -= HandleResourceChanged;
                _resources.OnElementLevelChange -= HandleElementLevelChange;
            }
            if (_boundBlast) _boundBlast.OnBlastReadyChanged -= HandleBlastReadyChanged;
            _resources = null;
            _boundBlast = null;
            _status = null;
        }

        void OnDisable() => Unbind();
    }
}
