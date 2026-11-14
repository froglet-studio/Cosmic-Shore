using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD: pushes the plough's state onto the fleet's four ability cards (the 2026-11-12 rework; the sword and
    /// its katana, afterimage and Blade Reach cards went to the Swordfish, <c>SwordfishHUDController</c>, and their code
    /// left this class on 2026-11-13).
    ///
    /// <para><b>Control chips.</b> Time wears the sticks-apart glyph, set here because a gesture has no map input.</para>
    ///
    /// <para><b>CHARGE card - Gore</b>: the plate fills with the ram speed toward the gore's live minimum (full when a ram
    /// counts), the card lights while it does or a Stampede runs, and flashes when a Stampede starts (<see cref="PushGore"/>).
    /// <b>MASS</b> (Heft) and <b>SPACE</b> (Blade) are single pictures with their level-5 swaps; the base view handles them.</para>
    ///
    /// <para><b>TIME card</b> - the ramp. Lit while the ramp is BOOSTING (Garrett, 2026-10-17: "the
    /// time icon should respond to boosting"); its gauge fills with speed as a fraction of the
    /// ramp's top speed, so the wind-up the Time element scales is visible as it happens, and the gauge
    /// DIMS as the line bends while the ramp is engaged (<c>RampBoostActionExecutor.Straightness01</c>,
    /// down to <see cref="bentLineGaugeAlpha"/>): how much of the boost the current line is costing the
    /// pilot, which they could otherwise only infer from speed they are not gaining. When the
    /// Time level-5 ward, <b>Momentum</b>, takes hold (upgraded and above 70% of top speed, danger
    /// prisms cannot drain it) the card flashes once - the moment the pilot becomes warded - and its picture
    /// changes: the Rhino's ward comes up and the danger prism ahead glances away
    /// (<see cref="RhinoVesselHUDView.SetMomentum"/>; Garrett 2026-10-28). The ward's threshold is one mark on the
    /// gauge, the Sparrow's edge ticks (<see cref="RhinoVesselHUDView.SetMomentumMark"/>).</para>
    ///
    /// <para>Polled rather than event-driven, deliberately: every value is a read of state the hull's
    /// components already own, so there is no subscription to leak or double across a re-initialise -
    /// the asymmetric subscribe/unsubscribe pair this replaced did both, depending on whether the
    /// re-init flipped the pilot between local and AI. Each push is change-detected, so a steady
    /// card costs a compare per frame.</para>
    /// </summary>
    public sealed class RhinoHUDController : VesselHUDController
    {
        [Header("View")]
        [SerializeField] private RhinoVesselHUDView view;

        [Header("Rhino – Scene Refs")]
        [Tooltip("The Time level-5 ward (VesselElementalImmunity on the root, above 70% of top speed). " +
                 "Found on the vessel when empty.")]
        [SerializeField] private VesselElementalImmunity speedWard;

        [Header("Charge card - the gore")]
        [Tooltip("The Rhino's Gore speed (RhinoGoreSpeed, the asset RhinoGoreBySkimmerEffect gates on): " +
                 "the Charge plate is the ram speed against the gore's live minimum - full when a ram counts - and " +
                 "the card lights while it does or a Stampede runs.")]
        [SerializeField] private JoustSpeedConfigSO goreSpeed;

        [Header("Time card - the ramp's line")]
        [Tooltip("The ramp (RampBoostActionExecutor on the vessel), whose straightness dims the Time gauge. " +
                 "Found on the vessel when empty.")]
        [SerializeField] private RampBoostActionExecutor ramp;
        [Tooltip("The Time gauge's opacity, as a fraction of its authored colour, on a fully BENT line while " +
                 "the ramp is engaged (1 = never dims). A straight line, or no ramp engaged, draws it as authored.")]
        [SerializeField, Range(0f, 1f)] private float bentLineGaugeAlpha = 0.3f;

        // The palette, for the Time card's hull (the pilot's domain colour). Injected, as the Sparrow's.
        [Inject] GameDataSO _gameData;

        IVesselStatus _vesselStatus;
        ITopSpeedSource _topSpeed;

        bool _chargeLit;
        bool _stampede;
        float _chargeGauge = -1f;
        bool _timeLit;
        bool _warded;
        Color _timeHullColor;
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
            if (!speedWard && root) speedWard = root.GetComponentInChildren<VesselElementalImmunity>(true);
            _topSpeed = root ? root.GetComponentInChildren<ITopSpeedSource>(true) : null;
            if (!ramp && root) ramp = root.GetComponentInChildren<RampBoostActionExecutor>(true);

            // A re-initialise (menu vessel swap, pilot swap) starts the cards from a known state so
            // the change detection below pushes everything once.
            _chargeLit = false;
            _stampede = false;
            _chargeGauge = -1f;
            _timeLit = false;
            _warded = false;
            _timeHullColor = default;
            _timeGauge = -1f;
            _timeGaugeAlpha = -1f;
            if (!view) return;

            // Time is a gesture, not a button (map Input 0 is the passive sentinel, which draws no
            // chip): full throttle on the dual sticks is both sticks pushed APART.
            view.SetAbilityControlGlyph(Element.Time,
                InputDeviceIconSetSwitcher.HintBinding.PadSticksOutward,
                InputDeviceIconSetSwitcher.HintBinding.KeySticksOutward);

            view.SetAbilityPressed(Element.Charge, false);
            view.SetAbilityPressed(Element.Time, false);
            view.SetMomentum(false, ResolveDomainSignalColor());
        }

        void Update()
        {
            if (!view || !IsHudAllowed) return;

            var domainColor = ResolveDomainSignalColor();

            bool timeLit = _vesselStatus.IsBoosting;
            if (timeLit != _timeLit)
            {
                _timeLit = timeLit;
                view.SetAbilityPressed(Element.Time, timeLit);
            }

            bool warded = speedWard && speedWard.IsHolding;
            if (warded && !_warded) view.PlayAbilityFlash(Element.Time);
            if (warded != _warded || domainColor != _timeHullColor)
            {
                _timeHullColor = domainColor;
                view.SetMomentum(warded, domainColor);
            }
            _warded = warded;

            float top = _topSpeed != null ? _topSpeed.TopSpeed : 0f;
            float speed01 = top > 0f ? Mathf.Clamp01(_vesselStatus.Speed / top) : 0f;
            if (!Mathf.Approximately(speed01, _timeGauge) && view.TryGetAbilityGauge(Element.Time, out var timeGauge))
            {
                _timeGauge = speed01;
                timeGauge.fillAmount = speed01;
                view.SetMomentumMark(speedWard ? speedWard.TopSpeedFraction : 0f, speed01);
            }
            PushLineToTimeGauge();

            PushGore();
        }

        // The Rhino's Charge card: the plate fills with the ram speed toward the gore's minimum (the same number the gore is
        // admitted at: the replicated Charge level, or the Charge 10 one in a Stampede), the card lights while a ram would
        // count, and flashes when a Stampede starts.
        void PushGore()
        {
            if (!goreSpeed) return;

            bool stampede = RhinoStampede.IsActive(_vesselStatus);
            if (stampede && !_stampede) view.PlayAbilityFlash(Element.Charge);
            _stampede = stampede;

            float minimum = stampede ? goreSpeed.MinimumSpeedAtLevel(1f) : goreSpeed.MinimumSpeedFor(_vesselStatus);
            float ready01 = minimum > 0f ? Mathf.Clamp01(_vesselStatus.Speed / minimum) : 1f;
            if (!Mathf.Approximately(ready01, _chargeGauge) && view.TryGetAbilityGauge(Element.Charge, out var gauge))
            {
                _chargeGauge = ready01;
                gauge.fillAmount = ready01;
            }

            bool lit = stampede || ready01 >= 1f;
            if (lit == _chargeLit) return;
            _chargeLit = lit;
            view.SetAbilityPressed(Element.Charge, lit);
        }

        // The pilot's domain colour (the domain can change mid-match); white until the palette resolves.
        Color ResolveDomainSignalColor()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (colorSet == null || _vesselStatus == null) return Color.white;
            return colorSet.GetDomainSignalColor(_vesselStatus.Domain);
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
