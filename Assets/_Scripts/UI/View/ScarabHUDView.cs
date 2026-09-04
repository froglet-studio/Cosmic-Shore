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
    /// 1. <b>Cavitation blast</b> (charge) — binary ready / recharging on the Charge icon, plus an
    ///    optional radial ring that sweeps 0→1 over the live cooldown. The cooldown length is
    ///    handed in with the edge, so the sweep is ONE tween per use, never a per-frame poll.
    ///    Binary stays visibly binary on the icon itself (a partial icon fill would read as a
    ///    meter and reopen the question it exists to close).
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
        [Tooltip("The Cavitation Blast ability icon, tinted ready / recharging.")]
        [SerializeField] Image blastIcon;
        [Tooltip("OPTIONAL radial-fill Image that sweeps 0 -> 1 over the cooldown. Leave unwired " +
                 "for the tint-only readout.")]
        [SerializeField] Image blastCooldownRing;
        [SerializeField] Color blastReadyColor = new(1f, 0.55f, 0.35f, 1f);
        [SerializeField] Color blastSpentColor = new(0.35f, 0.4f, 0.45f, 0.5f);
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

        /// <summary>
        /// Cavitation blast availability — deliberately binary on the icon. When the optional ring
        /// is wired, <paramref name="cooldownSeconds"/> drives ONE sweep tween for the whole
        /// recharge, so the analog readout costs nothing per frame.
        /// </summary>
        public void SetBlastReady(bool ready, float cooldownSeconds)
        {
            if (blastIcon)
            {
                blastIcon.DOKill();
                blastIcon.DOColor(ready ? blastReadyColor : blastSpentColor, blastTweenDuration)
                         .SetLink(blastIcon.gameObject);

                if (!ready && _seeded && blastSpendPunchScale > 0f)
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
            if (blastIcon) { blastIcon.DOKill(); blastIcon.transform.localScale = AbilityIconRestScale(Element.Charge); }
            if (blastCooldownRing) blastCooldownRing.DOKill();
        }
    }
}
