using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Scarab's HUD readouts (design: R_VesselActions/SCARAB.md §12). Three live gauges on
    /// top of the shared four-icon ability row — and the row order is the element map, so the
    /// gauges are named by the ELEMENT they sit under, never by "the third icon":
    /// <b>Cavitation Blast (charge) · Switch (mass) · Ball Forge (space) · Throttle (time)</b>.
    ///
    /// 1. <b>Cavitation blast</b> (charge) — the card is the punch's volume drawn TO SCALE around the
    ///    Scarab (Garrett 2026-10-28, Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §8): parts under the icon
    ///    (the kit's <c>specs/scarab.py</c> writes them, a rebind wires them): whole prisms outside
    ///    (blue), the volume (white), the volume with the claimed prisms cut out as forward-flung
    ///    slivers (white, shown for a beat as the punch lands), the plate (white) and the hull (the
    ///    pilot's domain colour). Ready is white; recharging greys the volume and the plate. Still
    ///    binary on the card, plus the optional radial ring that sweeps 0→1 over the live cooldown
    ///    — the cooldown length is handed in with the edge, so the sweep is ONE tween per use.
    /// 2. <b>Switch charges</b> (mass) — a discrete count (0..3) shown by tinting the Switch icon
    ///    through an authored colour ramp, so "can I place one?" is answerable at a glance.
    /// 3. <b>Ball energy</b> (space) — a radial fill on a ring sibling of the row, mirrored as a
    ///    ready tint on the Ball Forge icon. Crossing FULL is the beat that matters: it changes
    ///    what touching a crystal DOES, so the threshold is a hard colour flip plus a one-shot
    ///    punch rather than a bar that quietly tops out. Without that a player cannot tell a
    ///    working forge from a broken one — which is exactly how the first playtest went.
    ///
    /// Because three of these paint ability ICONS, colour here is a GAUGE channel and can carry no
    /// second meaning. That is settled platform-wide now: the ability lockup's card carries the
    /// upgrade (Docs/ABILITY_LOCKUP.md), so this view only has to re-anchor its captured rest
    /// scales in <see cref="SetAbilityUpgraded"/> — otherwise the next gauge write erases the
    /// upgrade's scale bump, which the Squirrel learned first.
    /// </summary>
    public class ScarabHUDView : VesselHUDView
    {
        [Header("Ball energy (Space row — the Ball Forge)")]
        [Tooltip("Radial-fill Image driven 0..1 by the ball-energy meter.")]
        [SerializeField] Image energyRing;
        [Tooltip("The Ball Forge ability icon, tinted filling / READY alongside the ring.")]
        [SerializeField] Image ballIcon;
        [Tooltip("Ring colour while the meter is still filling.")]
        [SerializeField] Color energyFillingColor = new(0.35f, 0.55f, 0.75f, 0.85f);
        [Tooltip("Ring colour once the meter is FULL — the next crystal forges a ball.")]
        [SerializeField] Color energyReadyColor = new(1f, 0.75f, 0.2f, 1f);
        [SerializeField, Min(0.01f)] float energyTweenDuration = 0.2f;
        [Tooltip("One-shot punch when the meter crosses into READY, so the state change is felt " +
                 "and not just displayed.")]
        [SerializeField, Min(0f)] float energyReadyPunchScale = 0.35f;

        [Header("Switch charges (Mass row)")]
        [Tooltip("The Switch ability icon, tinted by how many charges are banked.")]
        [SerializeField] Image switchIcon;
        [Tooltip("Tint per charge count. Element 0 = empty, last element = full. The array's " +
                 "length defines the displayed maximum.")]
        [SerializeField] Color[] switchChargeColors =
        {
            new(0.35f, 0.4f, 0.45f, 0.5f),
            new(0.6f, 0.75f, 0.85f, 0.85f),
            new(0.8f, 0.9f, 1f, 0.95f),
            new(1f, 1f, 1f, 1f),
        };

        [Header("Cavitation blast (Charge row)")]
        [Tooltip("The Cavitation Blast card's container (the ability icon Image, transparent): punched as one on a spend.")]
        [SerializeField] Image blastIcon;
        [Tooltip("Whole prisms outside the volume (Scarab_BlastPrisms), always shown in blastPrismColor.")]
        [SerializeField] Image blastPrismsIcon;
        [Tooltip("The punch's volume, the hull knocked out (Scarab_BlastField): white ready, blastSpentColor recharging.")]
        [SerializeField] Image blastFieldIcon;
        [Tooltip("The volume with the claimed prisms cut out as forward-flung slivers (Scarab_BlastFieldShattered), " +
                 "shown for blastShatterSeconds as the punch lands, in place of the volume.")]
        [SerializeField] Image blastShatteredIcon;
        [Tooltip("The plate, the volume's front edge (Scarab_BlastPlate): white ready, blastSpentColor recharging.")]
        [SerializeField] Image blastPlateIcon;
        [Tooltip("The Scarab at true scale (Scarab_BlastHull), in the pilot's domain colour (SetBlastHullColor).")]
        [SerializeField] Image blastHullIcon;
        [Tooltip("OPTIONAL radial-fill Image that sweeps 0 -> 1 over the cooldown. Leave unwired " +
                 "for the card-only readout.")]
        [SerializeField] Image blastCooldownRing;
        [SerializeField] Color blastPartColor = Color.white;
        [Tooltip("The volume and the plate while the blast recharges: grey means not ready, fleet-wide.")]
        [SerializeField] Color blastSpentColor = new(1f, 1f, 1f, 0.3f);
        [Tooltip("The whole prisms outside the volume: the HUD's blue (ElementalBarsConfigSO.BlueColor).")]
        [SerializeField] Color blastPrismColor = new(0.2196f, 0.5098f, 1f, 1f);
        [Tooltip("How long the shattered volume shows as the punch lands, before the card greys for the recharge.")]
        [SerializeField, Min(0f)] float blastShatterSeconds = 0.35f;
        [SerializeField, Min(0.01f)] float blastTweenDuration = 0.15f;
        [SerializeField, Min(0f)] float blastSpendPunchScale = 0.3f;

        bool _energyReady;
        bool _seeded;

        public override void Initialize()
        {
            // Idempotent: Initialize re-runs on a vessel swap.
            _seeded = false;
            _energyReady = false;
            if (energyRing)
            {
                energyRing.fillAmount = 0f;
                energyRing.color = energyFillingColor;
            }
            SetSwitchCharges(0);
            SetBlastReady(true, 0f);
            _seeded = true;
        }

        /// <summary>Ball energy, normalized 0..1. Crossing <paramref name="isFull"/> is the beat
        /// that matters — that is when the next crystal makes a ball instead of topping up.</summary>
        public void SetBallEnergy(float normalized, bool isFull)
        {
            float target = Mathf.Clamp01(normalized);
            if (energyRing)
            {
                energyRing.DOKill();
                energyRing.DOFillAmount(target, energyTweenDuration).SetLink(energyRing.gameObject);
            }

            var readyColor = isFull ? energyReadyColor : energyFillingColor;

            if (isFull != _energyReady)
            {
                _energyReady = isFull;
                if (energyRing)
                    energyRing.DOColor(readyColor, energyTweenDuration).SetLink(energyRing.gameObject);
                if (ballIcon)
                {
                    ballIcon.DOKill();
                    ballIcon.DOColor(readyColor, energyTweenDuration).SetLink(ballIcon.gameObject);
                }

                if (isFull && _seeded && energyReadyPunchScale > 0f)
                {
                    var punchTarget = energyRing ? energyRing.transform : (ballIcon ? ballIcon.transform : null);
                    if (punchTarget)
                        punchTarget.DOPunchScale(Vector3.one * energyReadyPunchScale,
                                                 energyTweenDuration * 2f, 1, 0.5f)
                                   .SetLink(punchTarget.gameObject);
                }
            }
            else
            {
                if (energyRing) energyRing.color = readyColor;
                if (ballIcon) ballIcon.color = readyColor;
            }
        }

        /// <summary>Banked switch charges as a discrete count.</summary>
        public void SetSwitchCharges(int charges)
        {
            if (!switchIcon || switchChargeColors == null || switchChargeColors.Length == 0) return;
            int index = Mathf.Clamp(charges, 0, switchChargeColors.Length - 1);
            switchIcon.DOKill();
            switchIcon.DOColor(switchChargeColors[index], blastTweenDuration).SetLink(switchIcon.gameObject);
        }

        /// <summary>Which of the Cavitation Blast card's parts show, and how: the pure rule the view paints.</summary>
        public struct BlastParts
        {
            public bool Field, Shattered, Spent;
        }

        /// <summary>
        /// Ready: the volume, white. Landing (the beat after a spend): the shattered volume in its place, still white.
        /// Recharging: the volume and the plate grey.
        /// </summary>
        public static BlastParts BlastPartsFor(bool ready, bool landing)
            => new() { Field = ready || !landing, Shattered = !ready && landing, Spent = !ready && !landing };

        /// <summary>The Charge card's hull, in the pilot's <paramref name="domain"/> colour.</summary>
        public void SetBlastHullColor(Color domain)
        {
            if (blastHullIcon) blastHullIcon.color = domain;
        }

        /// <summary>
        /// Cavitation blast availability — deliberately binary on the card. A spend shows the shattered volume for
        /// <see cref="blastShatterSeconds"/>, then greys the volume and the plate for the recharge. When the optional
        /// ring is wired, <paramref name="cooldownSeconds"/> drives ONE sweep tween for the whole recharge, so the
        /// analog readout costs nothing per frame.
        /// </summary>
        public void SetBlastReady(bool ready, float cooldownSeconds)
        {
            if (blastPrismsIcon) blastPrismsIcon.color = blastPrismColor;
            bool landing = !ready && _seeded && blastShatterSeconds > 0f;
            PaintBlast(BlastPartsFor(ready, landing));
            if (blastShatteredIcon)
            {
                blastShatteredIcon.DOKill();
                if (landing)
                    DOVirtual.DelayedCall(blastShatterSeconds, () => PaintBlast(BlastPartsFor(false, false)))
                             .SetTarget(blastShatteredIcon).SetLink(blastShatteredIcon.gameObject);
            }

            if (blastIcon && !ready && _seeded && blastSpendPunchScale > 0f)
            {
                blastIcon.transform.DOKill();
                blastIcon.transform.localScale = AbilityIconRestScale(Element.Charge);
                blastIcon.transform
                    .DOPunchScale(Vector3.one * blastSpendPunchScale, blastTweenDuration * 2f, 1, 0.5f)
                    .SetLink(blastIcon.gameObject);
            }

            if (!blastCooldownRing) return;
            blastCooldownRing.DOKill();
            if (ready || cooldownSeconds <= 0.01f)
            {
                blastCooldownRing.fillAmount = 1f;
            }
            else
            {
                blastCooldownRing.fillAmount = 0f;
                blastCooldownRing.DOFillAmount(1f, cooldownSeconds)
                                 .SetEase(Ease.Linear)
                                 .SetLink(blastCooldownRing.gameObject);
            }
        }

        void PaintBlast(BlastParts parts)
        {
            if (blastFieldIcon)
            {
                blastFieldIcon.enabled = parts.Field;
                blastFieldIcon.color = parts.Spent ? blastSpentColor : blastPartColor;
            }
            if (blastShatteredIcon)
            {
                blastShatteredIcon.enabled = parts.Shattered;
                blastShatteredIcon.color = blastPartColor;
            }
            if (blastPlateIcon) blastPlateIcon.color = parts.Spent ? blastSpentColor : blastPartColor;
        }

        /// <summary>
        /// Re-anchor the captured rest scales after the base class applies its upgrade bump.
        /// Required because this view punches icon scales for its own gauge feedback: without it
        /// the next punch settles back to the PRE-upgrade scale and silently erases the bump
        /// (`SquirrelVesselHUDView` is the reference for this exact hazard).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);

            var icon = element switch
            {
                Element.Charge => blastIcon,
                Element.Mass   => switchIcon,
                Element.Space  => ballIcon,
                _ => null
            };
            if (icon) icon.transform.localScale = AbilityIconRestScale(element);
        }

        void OnDisable()
        {
            // Pooled / swapped HUDs must not resume mid-tween.
            // energyRing is this view's OWN gauge image, not a bound ability icon, so it rests at
            // one. ballIcon / blastIcon ARE ability icons (Space / Charge): resting them at one
            // would wipe the lockup's kerning and the upgrade bump on the first hide, and nothing
            // re-applies either until the next Initialize. Rest them where the row says.
            if (energyRing) { energyRing.DOKill(); energyRing.transform.localScale = Vector3.one; }
            if (ballIcon) { ballIcon.DOKill(); ballIcon.transform.localScale = AbilityIconRestScale(Element.Space); }
            if (switchIcon) switchIcon.DOKill();
            if (blastIcon) { blastIcon.transform.DOKill(); blastIcon.transform.localScale = AbilityIconRestScale(Element.Charge); }
            if (blastShatteredIcon)
            {
                // A hide mid-landing would leave the shattered volume up with nothing left to swap it back.
                bool wasLanding = blastShatteredIcon.enabled;
                blastShatteredIcon.DOKill();
                if (wasLanding) PaintBlast(BlastPartsFor(false, false));
            }
            if (blastCooldownRing) blastCooldownRing.DOKill();
        }
    }
}
