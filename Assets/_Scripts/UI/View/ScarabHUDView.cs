using CosmicShore.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Scarab's HUD readouts (design: R_VesselActions/SCARAB.md §12). Live gauges on top of
    /// the shared four-icon ability row — and the row order is the element map, so the gauges are
    /// named by the ELEMENT they sit under, never by "the third icon":
    /// <b>Shove (charge) · Switch (mass) · Nucleus Seeding (space) · Throttle (time)</b>, with the
    /// Ball Forge on the omni crystal card and the Drift on a core card of its own (2026-10-29).
    ///
    /// 1. <b>Shove</b> (charge) — the card is the punch's volume drawn TO SCALE around the Scarab
    ///    (Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §8): parts under the icon (the kit's
    ///    <c>specs/scarab.py</c> writes them, a rebind wires them): whole prisms outside (blue), the
    ///    PUSH (the volume ahead of the hull, white), the PULL behind it (shown only at Charge 5,
    ///    Backdraft — the mirrored half), each with a shattered twin shown for a beat as the punch
    ///    lands, the plate (white) and the hull (the pilot's domain colour). Ready is white;
    ///    recharging greys the volume and the plate. Still binary on the card, plus the optional
    ///    radial ring that sweeps 0→1 over the cooldown — the length is handed in with the edge,
    ///    so the sweep is ONE tween per use.
    /// 2. <b>Switch</b> (mass) — a white ball threading the pilot's ring (Garrett 2026-10-28,
    ///    Docs/Icons/ABILITY_ICON_SKILL_PLAN.md §8), drawn as parts the kit's <c>specs/scarab.py</c>
    ///    writes: the ring's far and near arcs (domain colour), the wake and the ball (white). Mass
    ///    grows the ring, so the ball shrinks inside it by 1 / switchScale (to scale); with no switch
    ///    banked the card dims under the fleet's cooldown veil.
    /// 3. <b>Nucleus Seeding</b> (space) — a ball of the pilot's domain embedded in the nucleus
    ///    membrane: the ball part is drawn at its Space-10 size and scaled by the live seeded-ball
    ///    size / 4 (<c>ScarabBallForge.SizeScaleFor</c>) about its own centre, which sits on the
    ///    membrane, so it always reads as half-swallowed. (The ball-energy ring that sat here for
    ///    the Ball Forge retired with the slot: the energy economy is off.)
    ///
    /// Because three of these paint ability ICONS, colour here is a GAUGE channel and can carry no
    /// second meaning. That is settled platform-wide now: the ability lockup's card carries the
    /// upgrade (Docs/ABILITY_LOCKUP.md), so this view only has to re-anchor its captured rest
    /// scales in <see cref="SetAbilityUpgraded"/> — otherwise the next gauge write erases the
    /// upgrade's scale bump, which the Squirrel learned first.
    /// </summary>
    public class ScarabHUDView : VesselHUDView
    {
        [Header("Nucleus Seeding (Space row)")]
        [Tooltip("The Nucleus Seeding card's container (the ability icon Image, transparent).")]
        [SerializeField] Image seedIcon;
        [Tooltip("The seeded ball (Scarab_SeedBall), drawn at its Space-10 size, point-anchored at its centre on the " +
                 "membrane and scaled by the live seeded-ball size / seedMaxScale (SetSeedScale). Domain colour.")]
        [SerializeField] Image seedBallIcon;
        [Tooltip("The nucleus membrane (Scarab_SeedMembrane), white, drawn over the ball's sunk half.")]
        [SerializeField] Image seedMembraneIcon;
        [Tooltip("The seeded-ball size the ball part is drawn at: ScarabBallForge's x4 at Space 10.")]
        [SerializeField, Min(1f)] float seedMaxScale = 4f;

        [Header("Switch (Mass row)")]
        [Tooltip("The Switch card's container (the ability icon Image, transparent).")]
        [SerializeField] Image switchIcon;
        [Tooltip("The ring's far arc (Scarab_SwitchFar), in the pilot's domain colour (SetSwitchRingColor).")]
        [SerializeField] Image switchFarIcon;
        [Tooltip("The ball's wake (Scarab_SwitchWake), white.")]
        [SerializeField] Image switchWakeIcon;
        [Tooltip("The ball (Scarab_SwitchBall), white, point-anchored at its own centre: scaled by 1 / switchScale so " +
                 "the ring's growth with Mass reads as the ball shrinking inside it (SetSwitchScale).")]
        [SerializeField] Image switchBallIcon;
        [Tooltip("The ring's near arc (Scarab_SwitchNear), in the pilot's domain colour, drawn over the ball.")]
        [SerializeField] Image switchNearIcon;
        [SerializeField] Color switchBallColor = Color.white;
        [Tooltip("The card's opacity while no switch is banked (the fleet's cooldown veil sweeps over it).")]
        [SerializeField, Range(0f, 1f)] float switchSpentAlpha = 0.3f;

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
        [Tooltip("Charge 5, Backdraft: the PULL - the volume's back half (Scarab_BlastFieldBack), shown and tinted like " +
                 "the front only while the upgrade is active.")]
        [SerializeField] Image blastFieldBackIcon;
        [Tooltip("The pull as the punch lands (Scarab_BlastFieldBackShattered), the back half's shattered twin.")]
        [SerializeField] Image blastShatteredBackIcon;
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

        Color _switchRingColor = Color.white;
        float _switchAlpha = 1f;
        bool _backdraft;
        BlastParts _blastParts;
        bool _seeded;

        public override void Initialize()
        {
            // Idempotent: Initialize re-runs on a vessel swap.
            _seeded = false;
            SetSeedScale(1f);
            SetSwitchCharges(0);
            SetSwitchScale(1f);
            SetBlastReady(true, 0f);
            _seeded = true;
        }

        /// <summary>The Nucleus Seeding card's ball, in the pilot's <paramref name="domain"/> colour.</summary>
        public void SetSeedColor(Color domain)
        {
            if (seedBallIcon) seedBallIcon.color = domain;
        }

        /// <summary>The live seeded-ball size (x1 at rest, x4 at Space 10): the ball part grows on the membrane.</summary>
        public void SetSeedScale(float sizeScale)
        {
            if (seedBallIcon)
                seedBallIcon.rectTransform.localScale = Vector3.one * SeedBallScale(sizeScale, seedMaxScale);
        }

        /// <summary>The ball part's scale for a seeded-ball size: it is drawn at <paramref name="maxScale"/>, so
        /// size / max, clamped to (0, 1].</summary>
        public static float SeedBallScale(float sizeScale, float maxScale)
            => Mathf.Clamp(sizeScale / Mathf.Max(1f, maxScale), 0.05f, 1f);

        /// <summary>Banked switch charges as a discrete count: any banked is the full card, none is dim.</summary>
        public void SetSwitchCharges(int charges)
        {
            float alpha = SwitchCardAlpha(charges, switchSpentAlpha);
            PaintSwitch(switchWakeIcon, switchBallColor, alpha);
            PaintSwitch(switchBallIcon, switchBallColor, alpha);
            _switchAlpha = alpha;
            PaintSwitch(switchFarIcon, _switchRingColor, alpha);
            PaintSwitch(switchNearIcon, _switchRingColor, alpha);
        }

        /// <summary>The Switch card's opacity: full with a switch banked, <paramref name="spentAlpha"/> with none.</summary>
        public static float SwitchCardAlpha(int charges, float spentAlpha) => charges >= 1 ? 1f : spentAlpha;

        /// <summary>The ball's scale for the live ring scale (switchScale, 1 at rest): the ring grows, the ball shrinks.</summary>
        public static float SwitchBallScale(float ringScale) => 1f / Mathf.Max(1f, ringScale);

        /// <summary>The ring's arcs in the pilot's <paramref name="domain"/> colour.</summary>
        public void SetSwitchRingColor(Color domain)
        {
            _switchRingColor = domain;
            PaintSwitch(switchFarIcon, domain, _switchAlpha);
            PaintSwitch(switchNearIcon, domain, _switchAlpha);
        }

        /// <summary>The live Mass ring scale (switchScale): the ball part shrinks inside the ring.</summary>
        public void SetSwitchScale(float ringScale)
        {
            if (switchBallIcon) switchBallIcon.rectTransform.localScale = Vector3.one * SwitchBallScale(ringScale);
        }

        static void PaintSwitch(Image part, Color color, float alpha)
        {
            if (!part) return;
            color.a *= alpha;
            part.color = color;
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
        /// Shove availability — deliberately binary on the card. A spend shows the shattered volume for
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
            _blastParts = parts;
            var fieldColor = parts.Spent ? blastSpentColor : blastPartColor;
            if (blastFieldIcon)
            {
                blastFieldIcon.enabled = parts.Field;
                blastFieldIcon.color = fieldColor;
            }
            if (blastShatteredIcon)
            {
                blastShatteredIcon.enabled = parts.Shattered;
                blastShatteredIcon.color = blastPartColor;
            }
            // The PULL half (Charge 5, Backdraft) mirrors the push, and only exists while upgraded.
            if (blastFieldBackIcon)
            {
                blastFieldBackIcon.enabled = _backdraft && parts.Field;
                blastFieldBackIcon.color = fieldColor;
            }
            if (blastShatteredBackIcon)
            {
                blastShatteredBackIcon.enabled = _backdraft && parts.Shattered;
                blastShatteredBackIcon.color = blastPartColor;
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

            // Charge 5, Backdraft: the shove now pulls too, so the card grows its back half.
            if (element == Element.Charge && _backdraft != upgraded)
            {
                _backdraft = upgraded;
                PaintBlast(_blastParts);
            }

            var icon = element switch
            {
                Element.Charge => blastIcon,
                Element.Mass   => switchIcon,
                Element.Space  => seedIcon,
                _ => null
            };
            if (icon) icon.transform.localScale = AbilityIconRestScale(element);
        }

        void OnDisable()
        {
            // Pooled / swapped HUDs must not resume mid-tween.
            // blastIcon is an ability icon (Charge): resting it at one would wipe the lockup's
            // kerning and the upgrade bump on the first hide, and nothing re-applies either until
            // the next Initialize. Rest it where the row says.
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
