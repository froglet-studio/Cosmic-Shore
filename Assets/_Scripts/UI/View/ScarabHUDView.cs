using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Scarab's HUD readouts (design: R_VesselActions/SCARAB.md §12). Three live gauges on
    /// top of the shared four-icon ability row (Ball · Switch · Juke · Throttle, charge → mass →
    /// space → time):
    ///
    /// 1. <b>Ball energy</b> — a radial fill on a ring sibling of the ability row. This is the
    ///    most important readout on the vessel: crossing FULL changes what touching a crystal
    ///    DOES (top up → forge a ball), so the threshold is signalled as a hard colour flip plus
    ///    a one-shot punch rather than a bar that quietly tops out. Without it a player cannot
    ///    tell a working forge from a broken one — which is exactly how the first playtest went.
    /// 2. <b>Switch charges</b> — a discrete count (0..3) shown by tinting the Switch icon
    ///    through an authored colour ramp, so "can I place one?" is answerable at a glance.
    /// 3. <b>Juke</b> — binary armed / recharging on the Juke icon. Binary stays VISIBLY binary
    ///    (a partial fill would read as a meter and reopen the question it exists to close).
    ///
    /// Because three of these paint ability ICONS, the view sets <c>tintIconOnUpgrade = false</c>
    /// on the prefab and overrides <see cref="SetAbilityUpgraded"/> to re-anchor its captured rest
    /// scales — otherwise the base class's upgrade tint fights the gauge colour, and the upgrade
    /// scale bump is erased by the next gauge write (the Squirrel learned both). The element-petal
    /// BADGE still carries the upgrade signal, so nothing is lost.
    /// </summary>
    public class ScarabHUDView : VesselHUDView
    {
        [Header("Ball energy (Charge row)")]
        [Tooltip("Radial-fill Image driven 0..1 by the ball-energy meter.")]
        [SerializeField] Image energyRing;
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

        [Header("Juke (Space row)")]
        [Tooltip("The Juke ability icon, tinted armed / recharging.")]
        [SerializeField] Image jukeIcon;
        [SerializeField] Color jukeArmedColor = new(0.55f, 0.9f, 1f, 1f);
        [SerializeField] Color jukeSpentColor = new(0.35f, 0.4f, 0.45f, 0.5f);
        [SerializeField, Min(0.01f)] float jukeTweenDuration = 0.15f;
        [SerializeField, Min(0f)] float jukeSpendPunchScale = 0.3f;

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
            SetJukeArmed(false);
            _seeded = true;
        }

        /// <summary>Ball energy, normalized 0..1. Crossing <paramref name="isFull"/> is the beat
        /// that matters — that is when the next crystal makes a ball instead of topping up.</summary>
        public void SetBallEnergy(float normalized, bool isFull)
        {
            if (!energyRing) return;

            float target = Mathf.Clamp01(normalized);
            energyRing.DOKill();
            energyRing.DOFillAmount(target, energyTweenDuration).SetLink(energyRing.gameObject);

            if (isFull != _energyReady)
            {
                _energyReady = isFull;
                energyRing.DOColor(isFull ? energyReadyColor : energyFillingColor, energyTweenDuration)
                          .SetLink(energyRing.gameObject);

                if (isFull && _seeded && energyReadyPunchScale > 0f)
                    energyRing.transform
                        .DOPunchScale(Vector3.one * energyReadyPunchScale, energyTweenDuration * 2f, 1, 0.5f)
                        .SetLink(energyRing.gameObject);
            }
            else
            {
                energyRing.color = isFull ? energyReadyColor : energyFillingColor;
            }
        }

        /// <summary>Banked switch charges as a discrete count.</summary>
        public void SetSwitchCharges(int charges)
        {
            if (!switchIcon || switchChargeColors == null || switchChargeColors.Length == 0) return;
            int index = Mathf.Clamp(charges, 0, switchChargeColors.Length - 1);
            switchIcon.DOKill();
            switchIcon.DOColor(switchChargeColors[index], jukeTweenDuration).SetLink(switchIcon.gameObject);
        }

        /// <summary>Juke availability — deliberately binary.</summary>
        public void SetJukeArmed(bool armed)
        {
            if (!jukeIcon) return;
            jukeIcon.DOKill();
            jukeIcon.DOColor(armed ? jukeArmedColor : jukeSpentColor, jukeTweenDuration)
                    .SetLink(jukeIcon.gameObject);

            if (!armed && _seeded && jukeSpendPunchScale > 0f)
                jukeIcon.transform
                    .DOPunchScale(Vector3.one * jukeSpendPunchScale, jukeTweenDuration * 2f, 1, 0.5f)
                    .SetLink(jukeIcon.gameObject);
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
                Element.Mass  => switchIcon,
                Element.Space => jukeIcon,
                _ => null
            };
            if (icon) icon.transform.localScale = AbilityIconRestScale(element);
        }

        void OnDisable()
        {
            // Pooled / swapped HUDs must not resume mid-tween.
            if (energyRing) { energyRing.DOKill(); energyRing.transform.localScale = Vector3.one; }
            if (switchIcon) switchIcon.DOKill();
            if (jukeIcon)   { jukeIcon.DOKill(); jukeIcon.transform.localScale = Vector3.one; }
        }
    }
}
