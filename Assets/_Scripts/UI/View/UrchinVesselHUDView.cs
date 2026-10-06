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
                chargeRing.color = WithAlpha(chargeBuildingColor, 0f);
            }

            if (ammoFill)
            {
                ammoFill.fillAmount = 0f;
                ammoFill.color = ammoNormalColor;
            }

            _riding = false;
            _ridingBlend = 0f;
            if (ridingIndicator)
            {
                ridingIndicator.fillAmount = 0f;
                ridingIndicator.color = ridingOffColor;
            }
        }

        /// <param name="normalized">Ammo as a 0..1 fraction of capacity.</param>
        public void SetAmmo(float normalized)
        {
            if (!ammoFill) return;
            normalized = Mathf.Clamp01(normalized);
            ammoFill.fillAmount = normalized;
            ammoFill.color = Color.Lerp(ammoNormalColor, ammoFullColor, normalized);
        }

        public void SetRiding(bool riding) => _riding = riding;

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
            var c = Color.Lerp(chargeBuildingColor, chargeFullColor, _chargeProgress);
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
            ridingIndicator.color = Color.Lerp(ridingOffColor, ridingOnColor, _ridingBlend);
        }
    }
}
