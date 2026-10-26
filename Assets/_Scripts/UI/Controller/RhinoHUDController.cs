using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD: pushes the sword's state onto the fleet's four ability cards.
    ///
    /// <para><b>Control chips.</b> Charge, Mass and Space wear the two-trigger glyph (map Input
    /// BothSticksAction: hold both to energize, swing to cut; the afterimages follow the energize).
    /// Time wears the sticks-apart glyph, set here because a gesture has no map input.</para>
    ///
    /// <para><b>CHARGE card</b> - the sword's ENERGIZE, which the card is bound to (the both-triggers
    /// stance), and how many petals a sword hit takes (Charge scales it; its level-5, Sword Steal,
    /// lands them on the Rhino instead of ejecting them; 2026-10-26). The card is lit while the blade is
    /// energized, the recharge veil sweeps over it through the energize cooldown, and an authored
    /// gauge (if the row has one) shows the energize: filling as the stance is held, full while
    /// lit, draining over the let-go tail (<see cref="ShieldSkimmerScaleDriver.EnergizeGauge01"/>).
    /// It used to show stored energy; the Rhino has none since 2026-10-26.</para>
    ///
    /// <para><b>MASS card</b> - lit while the sword is laying its afterimages (the window after an
    /// energize, and the whole of an omni-crystal surge).</para>
    ///
    /// <para><b>TIME card</b> - the ramp. Lit while the ramp is BOOSTING (Garrett, 2026-10-17: "the
    /// time icon should respond to boosting"); its gauge fills with speed as a fraction of the
    /// ramp's top speed, so the wind-up the Time element scales is visible as it happens, and the gauge
    /// DIMS as the line bends while the ramp is engaged (<c>RampBoostActionExecutor.Straightness01</c>,
    /// down to <see cref="bentLineGaugeAlpha"/>): how much of the boost the current line is costing the
    /// pilot, which they could otherwise only infer from speed they are not gaining. When the
    /// Time level-5 ward, <b>Momentum</b>, takes hold (upgraded and above 70% of top speed, danger
    /// prisms cannot drain it) the card flashes once - the moment the pilot becomes warded.</para>
    ///
    /// <para>Polled rather than event-driven, deliberately: every value is a read of state the sword
    /// driver already owns, so there is no subscription to leak or double across a re-initialise -
    /// the asymmetric subscribe/unsubscribe pair this replaced did both, depending on whether the
    /// re-init flipped the pilot between local and AI. Each push is change-detected, so a steady
    /// card costs a compare per frame.</para>
    /// </summary>
    public sealed class RhinoHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private RhinoVesselHUDView view;

        [Header("Rhino – Scene Refs")]
        [Tooltip("The sword's brain (energize phase, surge). On the Rhino root's ScaleSkimmerObject.")]
        [FormerlySerializedAs("growSkimmerExecutor")]
        [SerializeField] private ShieldSkimmerScaleDriver swordDriver;

        [Tooltip("The Mass ability (afterimages), beside the sword driver. Found under the vessel when empty.")]
        [SerializeField] private RhinoSwordAfterimage afterimage;

        [Tooltip("The Time level-5 ward (VesselElementalImmunity on the root, above 70% of top speed). " +
                 "Found on the vessel when empty.")]
        [SerializeField] private VesselElementalImmunity speedWard;

        [Header("Time card - the ramp's line")]
        [Tooltip("The ramp (RampBoostActionExecutor on the vessel), whose straightness dims the Time gauge. " +
                 "Found on the vessel when empty.")]
        [SerializeField] private RampBoostActionExecutor ramp;
        [Tooltip("The Time gauge's opacity, as a fraction of its authored colour, on a fully BENT line while " +
                 "the ramp is engaged (1 = never dims). A straight line, or no ramp engaged, draws it as authored.")]
        [SerializeField, Range(0f, 1f)] private float bentLineGaugeAlpha = 0.3f;

        IVesselStatus _vesselStatus;
        ITopSpeedSource _topSpeed;

        bool _chargeLit;
        float _chargeVeil = -1f;
        float _chargeGauge = -1f;
        bool _massLit;
        bool _timeLit;
        bool _warded;
        float _timeGauge = -1f;
        float _timeGaugeAlpha = -1f;
        Color _timeGaugeColor;
        bool _timeGaugeColorCaptured;

        bool IsHudAllowed => _vesselStatus is { IsInitializedAsAI: false, IsLocalUser: true };

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _vesselStatus = vesselStatus;

            if (!view)
                view = View as RhinoVesselHUDView;

            // Explicit Unity-null tests, not `??` (a missing component can be a fake-null object).
            var root = vesselStatus?.ShipTransform;
            if (!afterimage && root) afterimage = root.GetComponentInChildren<RhinoSwordAfterimage>(true);
            if (!speedWard && root) speedWard = root.GetComponentInChildren<VesselElementalImmunity>(true);
            _topSpeed = root ? root.GetComponentInChildren<ITopSpeedSource>(true) : null;
            if (!ramp && root) ramp = root.GetComponentInChildren<RampBoostActionExecutor>(true);

            // A re-initialise (menu vessel swap, pilot swap) starts the cards from a known state so
            // the change detection below pushes everything once.
            _chargeLit = false;
            _chargeVeil = -1f;
            _chargeGauge = -1f;
            _massLit = false;
            _timeLit = false;
            _warded = false;
            _timeGauge = -1f;
            _timeGaugeAlpha = -1f;
            if (!view) return;

            // Time is a gesture, not a button (map Input 0 is the passive sentinel, which draws no
            // chip): full throttle on the dual sticks is both sticks pushed APART.
            view.SetAbilityControlGlyph(Element.Time,
                InputDeviceIconSetSwitcher.HintBinding.PadSticksOutward,
                InputDeviceIconSetSwitcher.HintBinding.KeySticksOutward);

            view.SetAbilityPressed(Element.Charge, false);
            view.SetAbilityPressed(Element.Mass, false);
            view.SetAbilityPressed(Element.Time, false);
        }

        void Update()
        {
            if (!view || !IsHudAllowed) return;

            bool massLit = afterimage && afterimage.IsLaying;
            if (massLit != _massLit)
            {
                _massLit = massLit;
                view.SetAbilityPressed(Element.Mass, massLit);
            }

            bool timeLit = _vesselStatus.IsBoosting;
            if (timeLit != _timeLit)
            {
                _timeLit = timeLit;
                view.SetAbilityPressed(Element.Time, timeLit);
            }

            bool warded = speedWard && speedWard.IsHolding;
            if (warded && !_warded) view.PlayAbilityFlash(Element.Time);
            _warded = warded;

            float top = _topSpeed != null ? _topSpeed.TopSpeed : 0f;
            float speed01 = top > 0f ? Mathf.Clamp01(_vesselStatus.Speed / top) : 0f;
            if (!Mathf.Approximately(speed01, _timeGauge) && view.TryGetAbilityGauge(Element.Time, out var timeGauge))
            {
                _timeGauge = speed01;
                timeGauge.fillAmount = speed01;
            }
            PushLineToTimeGauge();

            if (!swordDriver) return;

            // Re-asserted every frame while lit, not change-detected: the card is bound to the
            // both-triggers input, so the base press path darkens it the moment the stance is let
            // go - while the energized tail still has seconds to run.
            bool lit = swordDriver.IsEnergized;
            if (lit || lit != _chargeLit)
            {
                _chargeLit = lit;
                view.SetAbilityPressed(Element.Charge, lit);
            }

            float veil = swordDriver.EnergizeCooldownRemaining01;
            if (!Mathf.Approximately(veil, _chargeVeil))
            {
                _chargeVeil = veil;
                view.SetAbilityCooldown(Element.Charge, veil);
            }

            float energize = swordDriver.EnergizeGauge01;
            if (!Mathf.Approximately(energize, _chargeGauge) && view.TryGetAbilityGauge(Element.Charge, out var gauge))
            {
                _chargeGauge = energize;
                gauge.fillAmount = energize;
            }
        }

        // The ramp's line on the Time gauge: its authored colour on a straight line (or with no ramp
        // engaged), dimming toward bentLineGaugeAlpha as the line bends. The authored colour is captured
        // once, so the dim never compounds.
        void PushLineToTimeGauge()
        {
            if (!view.TryGetAbilityGauge(Element.Time, out var gauge)) return;
            if (!_timeGaugeColorCaptured)
            {
                _timeGaugeColor = gauge.color;
                _timeGaugeColorCaptured = true;
            }

            float straight = ramp && ramp.IsEngaged ? Mathf.Clamp01(ramp.Straightness01) : 1f;
            float alpha = Mathf.Lerp(bentLineGaugeAlpha, 1f, straight);
            if (Mathf.Abs(alpha - _timeGaugeAlpha) < 0.01f) return;
            _timeGaugeAlpha = alpha;
            var c = _timeGaugeColor;
            c.a *= alpha;
            gauge.color = c;
        }
    }
}
