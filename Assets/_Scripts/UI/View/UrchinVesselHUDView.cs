using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Urchin's HUD. Beyond the fleet-standard four-icon ability row (which the base class
    /// owns, in charge -> mass -> space -> time order), it shows the two things the Urchin's
    /// loop actually turns on:
    ///
    /// * **Ammo** - the volley spends it and the ride refills it, so it is the meter that ties
    ///   the two halves of the vessel together.
    /// * **Riding** - whether the vessel is latched onto a trail. Deliberately BINARY: the
    ///   Urchin either is on the ribbon or it is not, and a partial fill here would read as a
    ///   meter and invite the question of what "half attached" means.
    /// * **Charge** - the Chain Spikes hold-to-charge burst, as a ring around the Charge icon.
    ///   The Charge card's lockup gauge is already the ammo meter (one gauge per card), so the
    ///   charge rides its own ring, and it is drawn only once a hold has become a charge: a tap
    ///   shows nothing, because the press already fired it.
    ///
    /// Both are driven from the Urchin's OWN components by the controller. Nothing here polls
    /// another vessel's executor by type - that compiles, returns null on every other hull, and
    /// leaves a gauge silently dead (the Squirrel's heat ring lived that way for its whole life).
    /// </summary>
    public class UrchinVesselHUDView : VesselHUDView
    {
        [Header("Ammo")]
        [Tooltip("Radial or bar fill for the spike ammo the volley spends and the ride refills.")]
        [SerializeField] Image ammoFill;
        [SerializeField] Color ammoNormalColor = Color.white;
        [SerializeField] Color ammoFullColor = Color.cyan;

        [Header("Riding")]
        [Tooltip("Lit while attached to a trail. Binary by design - see the class summary.")]
        [SerializeField] Image ridingIndicator;
        [SerializeField] Color ridingOffColor = new(1f, 1f, 1f, 0.25f);
        [SerializeField] Color ridingOnColor = Color.green;

        [Tooltip("Seconds the riding indicator takes to cross between its two states, so the " +
                 "change is visible rather than a snap. Continuity of existence applies to UI too.")]
        [SerializeField, Min(0f)] float ridingBlendSeconds = 0.15f;

        [Header("Charge (hold-to-charge burst)")]
        [Tooltip("Radial ring around the Charge card's icon - a CHILD of the icon, so the lockup " +
                 "keeps it (RetireLegacyChrome retires a host's other children) and it inherits the " +
                 "icon's rest scale and upgrade bump. Fills with the held charge, i.e. the fraction " +
                 "the release burst's spike count is lerped by. Never the icon itself: the icon " +
                 "carries the four-icon row's upgrade tint and badge.")]
        [SerializeField] Image chargeRing;
        [Tooltip("Ring colour as the charge builds.")]
        [SerializeField] Color chargeBuildingColor = new(0.22f, 0.51f, 1f, 0.55f);
        [Tooltip("Ring colour at a full charge - the maximum burst.")]
        [SerializeField] Color chargeFullColor = new(0.96f, 0.96f, 1f, 0.5f);
        [Tooltip("Seconds the ring takes to appear once a hold becomes a charge, and to dissolve " +
                 "after the release - so neither is a snap.")]
        [SerializeField, Min(0f)] float chargeFadeSeconds = 0.12f;


        [Header("Ability parts (icon kit: Tools/Build/icon_kit/specs/urchin.py)")]
        [Tooltip("Charge card: the Urchin nose-on (domain).")]
        [SerializeField] Image chainUrchinIcon;
        [Tooltip("Charge card: the thrown spike (white).")]
        [SerializeField] Image chainSpikeIcon;
        [Tooltip("Charge card: the rival prism the spike hits (danger).")]
        [SerializeField] Image chainPrismIcon;
        [Tooltip("Charge card: the next generation sprayed from the hit prism (white).")]
        [SerializeField] Image chainSprayIcon;
        [Tooltip("Charge card, Overcharge (Charge 5): the full-length spray, replacing the short one.")]
        [SerializeField] Image chainSprayOverIcon;
        [Tooltip("Charge card, Overcharge (Charge 5): the generation deeper.")]
        [SerializeField] Image chainDeeperIcon;
        [Tooltip("Mass card: the Urchin riding the trail (domain).")]
        [SerializeField] Image riderUrchinIcon;
        [Tooltip("Mass card: the Urchin lifted off the trail (domain).")]
        [SerializeField] Image riderLiftedIcon;
        [Tooltip("Mass card: the trail under and behind it, made its own (domain).")]
        [SerializeField] Image riderMineIcon;
        [Tooltip("Mass card: the rival's trail ahead (danger).")]
        [SerializeField] Image riderTheirsIcon;
        [Tooltip("Mass card, Reinforced Wake (Mass 5): the rim on what it made (white).")]
        [SerializeField] Image riderShieldIcon;
        [Tooltip("Space card: the Urchin from behind (domain).")]
        [SerializeField] Image trackUrchinIcon;
        [Tooltip("Space card: the projected rail (white; faint while recharging).")]
        [SerializeField] Image trackRailIcon;
        [Tooltip("Space card, Long Haul (Space 5): the rail's further run (white).")]
        [SerializeField] Image trackLongHaulIcon;
        [Tooltip("Time card: the Urchin riding its trail (domain).")]
        [SerializeField] Image slipRidingIcon;
        [Tooltip("Time card: the ghost it leaves on the trail while it slips (domain, faint).")]
        [SerializeField] Image slipGhostIcon;
        [Tooltip("Time card: the Urchin lifted clear while it slips (domain).")]
        [SerializeField] Image slipLiftedIcon;
        [Tooltip("Time card: the trail it rides (domain; danger once Slipstream is live).")]
        [SerializeField] Image slipTrailIcon;
        [Tooltip("Time card, Slipstream (Time 5): the speed lines kept while slipping (white).")]
        [SerializeField] Image slipStreamIcon;
        [Tooltip("Rival colour on the parts (the fleet danger orange).")]
        [SerializeField] Color dangerColor = new(1f, 0.3294f, 0.1020f, 1f);
        [Tooltip("Alpha of the trail parts while off the trail.")]
        [SerializeField, Range(0f, 1f)] float offTrailAlpha = 0.4f;
        [Tooltip("Alpha of the rail while the Track Projector recharges.")]
        [SerializeField, Range(0f, 1f)] float railRechargingAlpha = 0.4f;
        [Tooltip("Alpha of the ghost left on the trail while slipping.")]
        [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.35f;

        Color _domain = Color.white;
        bool _slipping;
        bool _trackReady = true;
        bool _partsRiding;

        float _ridingBlend;
        bool _riding;

        bool _chargeArmed;
        float _chargeProgress;
        float _chargeVisible;

        public override void Initialize()
        {
            _chargeArmed = false;
            _chargeProgress = 0f;
            _chargeVisible = 0f;
            if (chargeRing)
            {
                chargeRing.fillAmount = 0f;
                chargeRing.color = WithAlpha(ColorTrace.Retrace(ColorTrace.Ui, chargeBuildingColor), 0f);
            }

            if (ammoFill)
            {
                ammoFill.fillAmount = 0f;
                ammoFill.color = ColorTrace.Retrace(ColorTrace.Ui, ammoNormalColor);
            }

            _riding = false;
            _ridingBlend = 0f;
            if (ridingIndicator)
            {
                ridingIndicator.fillAmount = 0f;
                ridingIndicator.color = ColorTrace.Retrace(ColorTrace.Ui, ridingOffColor);
            }

            _slipping = false;
            _trackReady = true;
            _partsRiding = false;
            RefreshParts();
        }

        /// <summary>The pilot's domain colour (alpha 0 = keep the last); every Urchin on the cards wears it.</summary>
        public void SetDomainColor(Color domain)
        {
            if (domain.a <= 0f || domain == _domain) return;
            _domain = domain;
            RefreshParts();
        }

        /// <summary>Slip's phase (the executor's ghosted flag): the Time card lifts the Urchin and leaves its ghost.</summary>
        public void SetSlipping(bool slipping)
        {
            if (slipping == _slipping) return;
            _slipping = slipping;
            RefreshParts();
        }

        /// <summary>The Track Projector is ready: its rail is drawn solid, faint while it recharges.</summary>
        public void SetTrackReady(bool ready)
        {
            if (ready == _trackReady) return;
            _trackReady = ready;
            RefreshParts();
        }

        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            RefreshParts();
        }

        void OnEnable() => ColorTrace.Changed += Repaint;
        void OnDisable() => ColorTrace.Changed -= Repaint;

        // A colour set worn while the HUD is up: the parts and the ammo fill take it now (the charge ring and the
        // riding indicator repaint every frame).
        void Repaint()
        {
            RefreshParts();
            if (ammoFill) SetAmmo(ammoFill.fillAmount);
        }

        void RefreshParts()
        {
            bool charge5 = IsAbilityUpgraded(Element.Charge);
            var danger = ColorTrace.Retrace(ColorTrace.Ui, dangerColor);
            Paint(chainUrchinIcon, _domain, 1f);
            Paint(chainSpikeIcon, Color.white, 1f);
            Paint(chainPrismIcon, danger, 1f);
            Paint(chainSprayIcon, Color.white, charge5 ? 0f : 1f);
            Paint(chainSprayOverIcon, Color.white, charge5 ? 1f : 0f);
            Paint(chainDeeperIcon, Color.white, charge5 ? 1f : 0f);

            bool riding = _partsRiding;
            Paint(riderUrchinIcon, _domain, riding ? 1f : 0f);
            Paint(riderLiftedIcon, _domain, riding ? 0f : 1f);
            Paint(riderMineIcon, _domain, riding ? 1f : offTrailAlpha);
            Paint(riderTheirsIcon, danger, riding ? 1f : offTrailAlpha);
            Paint(riderShieldIcon, Color.white, riding && IsAbilityUpgraded(Element.Mass) ? 1f : 0f);

            Paint(trackUrchinIcon, _domain, 1f);
            Paint(trackRailIcon, Color.white, _trackReady ? 1f : railRechargingAlpha);
            Paint(trackLongHaulIcon, Color.white, IsAbilityUpgraded(Element.Space) ? (_trackReady ? 1f : railRechargingAlpha) : 0f);

            bool time5 = IsAbilityUpgraded(Element.Time);
            Paint(slipRidingIcon, _domain, _slipping ? 0f : 1f);
            Paint(slipGhostIcon, _domain, _slipping ? ghostAlpha : 0f);
            Paint(slipLiftedIcon, _domain, _slipping ? 1f : 0f);
            Paint(slipTrailIcon, time5 ? danger : _domain, 1f);
            Paint(slipStreamIcon, Color.white, time5 && _slipping ? 1f : 0f);
        }

        static void Paint(Image part, Color color, float alpha)
        {
            if (!part) return;
            var c = new Color(color.r, color.g, color.b, color.a * alpha);
            if (part.color != c) part.color = c;
        }

        /// <param name="normalized">Ammo as a 0..1 fraction of capacity.</param>
        public void SetAmmo(float normalized)
        {
            if (!ammoFill) return;
            normalized = Mathf.Clamp01(normalized);
            ammoFill.fillAmount = normalized;
            ammoFill.color = Color.Lerp(ColorTrace.Retrace(ColorTrace.Ui, ammoNormalColor),
                                        ColorTrace.Retrace(ColorTrace.Ui, ammoFullColor), normalized);
        }

        public void SetRiding(bool riding)
        {
            _riding = riding;
            // The Mass card's parts switch with the gauge: the Urchin on the trail, or lifted off it.
            if (riding == _partsRiding) return;
            _partsRiding = riding;
            RefreshParts();
        }

        /// <summary>
        /// The held Chain Spikes charge. <paramref name="armed"/> is false for a tap (and below the
        /// authored minimum hold), so a tap - which the press has already paid for - draws no
        /// ring at all; the ring only appears once releasing would throw a burst.
        /// </summary>
        /// <param name="progress01">0..1 of the burst's spike-count range.</param>
        public void SetSpikeCharge(bool armed, float progress01)
        {
            _chargeArmed = armed;
            // Held at its last value while the ring dissolves, so a release reads as the charge
            // being thrown rather than the meter emptying.
            if (armed) _chargeProgress = Mathf.Clamp01(progress01);
        }

        void Update()
        {
            UpdateChargeRing();
            UpdateRiding();
        }

        void UpdateChargeRing()
        {
            if (!chargeRing) return;

            float target = _chargeArmed ? 1f : 0f;
            _chargeVisible = chargeFadeSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_chargeVisible, target, Time.deltaTime / chargeFadeSeconds);

            chargeRing.fillAmount = _chargeProgress;
            var c = Color.Lerp(ColorTrace.Retrace(ColorTrace.Ui, chargeBuildingColor),
                               ColorTrace.Retrace(ColorTrace.Ui, chargeFullColor), _chargeProgress);
            chargeRing.color = WithAlpha(c, c.a * _chargeVisible);
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        void UpdateRiding()
        {
            if (!ridingIndicator) return;

            float target = _riding ? 1f : 0f;
            _ridingBlend = ridingBlendSeconds <= 0f
                ? target
                : Mathf.MoveTowards(_ridingBlend, target, Time.deltaTime / ridingBlendSeconds);

            // The lockup adopts this Image as the Mass card's gauge (a vertical fill), so the
            // binary state is drawn as EMPTY or FULL with only the blend's transition between -
            // never parked part-way, which would read as a meter.
            ridingIndicator.fillAmount = _ridingBlend;
            ridingIndicator.color = Color.Lerp(ColorTrace.Retrace(ColorTrace.Ui, ridingOffColor),
                                               ColorTrace.Retrace(ColorTrace.Ui, ridingOnColor), _ridingBlend);
        }
    }
}
