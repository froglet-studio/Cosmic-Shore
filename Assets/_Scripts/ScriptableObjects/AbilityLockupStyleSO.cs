using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The one source of truth for the ABILITY LOCKUP - the fleet-wide card that fuses an ability
    /// icon with the element indicator that upgrades it, so a single glance answers four questions:
    /// how much of the element you hold, which ability it upgrades, whether that upgrade is live,
    /// and how to fire it.
    ///
    /// <para>Shape is the "TOTEM": TWO BORDERLESS TRAPEZOIDS meeting at their wide edges across a
    /// small gap - the element flower in the upper one, the ability icon in the lower one, the
    /// control chip below. The flower is deliberately SMALLER than the ability icon: the icon names
    /// the ability and is what a pilot hunts for, the flower qualifies it.</para>
    ///
    /// <para><b>The gap IS the divider and the silhouette IS the frame.</b> A hairline between two
    /// halves of one plate, and an outline around it, were both drawing a boundary the shape can
    /// state on its own - so the divider and the rim are retired and the plates are borderless. The
    /// slant does the work an outline used to: two trapezoids meeting wide-edge to wide-edge read
    /// as one object with a waist, where two stacked rectangles read as a list.</para>
    ///
    /// <para>House motif is soft-hard-soft: BLOOM (soft) around FLAT, radius-0 plates (hard)
    /// carrying a smooth-curve glyph (soft). Nothing glows at rest - the bloom is reserved for the
    /// upgraded state, per STYLE_FOUNDATION's "glow is state".</para>
    ///
    /// <para>Every sprite here is white + alpha and tinted at runtime (the T7 sprite-kit rule), so
    /// one asset set serves every vessel and every domain. Colour is information, never decoration:
    /// the lockup never wears a team colour, because "whose is this" is not a question the pilot's
    /// own HUD row has to answer.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityLockupStyle",
                     menuName = "ScriptableObjects/UI/Ability Lockup Style")]
    public class AbilityLockupStyleSO : ScriptableObject
    {
        [Header("Geometry (reference px @1920x1080)")]
        [Tooltip("Width of the lockup plate at its WIDE edge - the seam where the two trapezoids " +
                 "face each other. The shipped ability cells are 150 wide, so this sits inside them " +
                 "with margin and leaves the row's authored pitch untouched.")]
        [Min(1f)] public float plateWidth = 104f;

        [Tooltip("How far each side pulls in at a trapezoid's NARROW edge, in px. The element plate " +
                 "narrows upward and the ability plate narrows downward, so the pair mirrors about " +
                 "the gap. 0 makes both plates rectangles and the totem reads as a list.")]
        [Min(0f)] public float trapezoidInset = 9f;

        [Tooltip("Gap between the two trapezoids. This REPLACES the hairline divider: a real gap " +
                 "separates the cells without drawing a line, which is what lets the plates be " +
                 "borderless.")]
        [Min(0f)] public float cellGap = 6f;

        [Tooltip("Height of the LOWER plate - the one centred on the vessel's existing ability icon. " +
                 "The icon is never moved; the lockup is built around wherever it already sits.")]
        [Min(1f)] public float abilityCellHeight = 88f;

        [Tooltip("Height of the UPPER plate, added ABOVE the ability plate. Keep it EQUAL to the " +
                 "ability plate: mirrored heights make the pair symmetric about the gap, and a tall " +
                 "narrow stack of two unequal plates reads as a coffin rather than as a totem. The " +
                 "hierarchy lives in the MARKS (flower smaller than icon), not in the plates.")]
        [Min(1f)] public float petalCellHeight = 88f;

        [Tooltip("Size of the element flower inside the upper plate. Authored EQUAL to iconBoxSize: " +
                 "the two plates are mirror images, so an equal mark in each lands on the same " +
                 "negative-space solution and the pair kerns as one object. (An earlier pass drew " +
                 "the flower smaller to keep the ability the 'headline'; with mirrored plates that " +
                 "just made the top one look under-filled - the two marks are different KINDS of " +
                 "thing, always in the same columns, so size was never what told them apart.)")]
        [Min(1f)] public float petalFlowerSize = 60f;

        [Tooltip("The size EVERY vessel's ability icon is drawn at, whatever size its prefab authored. " +
                 "The lockup derives each icon's scale from this (iconBoxSize / its authored size), so " +
                 "apparent size is uniform across the fleet and nobody re-authors an icon to match. " +
                 "60 in a 104 cell whose corner sliver eats 12 leaves an even 22 of air on every side - " +
                 "the lockup's KERNING. It multiplies the upgrade bump rather than replacing it.")]
        [Min(1f)] public float iconBoxSize = 60f;

        [Header("Row (the lockup owns the whole row, on every vessel)")]
        [Tooltip("Centre-to-centre distance between cards. One number for the fleet - a vessel cannot " +
                 "space its own row.")]
        [Min(1f)] public float cardPitch = 137.7f;

        [Tooltip("Distance from the screen's RIGHT edge to the right edge of the last card.")]
        public float rowMarginRight = 65.1f;

        [Tooltip("Distance from the screen's BOTTOM edge to the bottom of the ability cell.")]
        public float rowMarginBottom = 53f;

        [Tooltip("Height of the control chip below the card. The chip is placed by the lockup, so a " +
                 "vessel can no longer author its own offset for the (LT)/(RT) label.")]
        [Min(1f)] public float chipHeight = 24f;

        [Tooltip("Gap between the bottom of the card and the control chip.")]
        [Min(0f)] public float chipGap = 8f;

        [Tooltip("How far the bloom extends past the plate on every side.")]
        [Min(0f)] public float bloomPadding = 26f;

        [Header("Slant edge (sloped sides only)")]
        [Tooltip("Thickness of the band drawn INSIDE each plate along its two sloped sides. It is " +
                 "not a border - it never closes - so the plate's core stays transparent while the " +
                 "two edges that carry the shape's identity get an accent. 0 = off.")]
        [Min(0f)] public float slantEdgeThickness = 3f;

        [Tooltip("How far the band wraps around each corner onto the horizontal edges, in px. The " +
                 "whole grade to nothing happens along that wrap, so the sloped side stays solid " +
                 "end to end and the CORNER is lit rather than being where the accent died.")]
        [Min(0f)] public float slantEdgeWrap = 14f;

        [Tooltip("Width of the zero-alpha feather baked onto both sides of the band - the " +
                 "antialiasing. A generated diagonal gets none from the canvas, so without this it " +
                 "is stair-stepped. ~1px at the reference resolution.")]
        [Min(0f)] public float slantEdgeAntialias = 1f;

        [Tooltip("Resting colour of the slant edge.")]
        public Color slantEdgeColor = new(0.51f, 0.53f, 0.61f, 0.85f);

        [Tooltip("Slant edge while upgraded - the level-5 white the flowers already speak.")]
        public Color upgradedSlantEdgeColor = new(0.96f, 0.96f, 1f, 1f);

        [Tooltip("Height of the gauge that fills the ability cell behind the icon, as a fraction of " +
                 "that cell. 1 = the gauge rises the full height of the icon's cell.")]
        [Range(0.2f, 1f)] public float gaugeCellFraction = 1f;

        [Header("Sprites (white + alpha, tinted at runtime)")]
        [Tooltip("9-sliced soft box glow. Reserved for the upgraded state. The ONLY sprite the " +
                 "lockup still needs - the plates are generated geometry (TrapezoidGraphic), because " +
                 "a trapezoid has no 9-slice and a sprited one would freeze the slant into the art.")]
        public Sprite bloomSprite;

        [Header("Colours - resting")]
        [Tooltip("Plate fill. Near-black and translucent so the arena reads through the row. " +
                 "Borderless: there is no resting outline, by design.")]
        public Color plateColor = new(0.024f, 0.031f, 0.063f, 0.86f);

        [Header("Colours - upgraded")]
        [Tooltip("Upgraded bloom. Alpha carries it - in engine, gameplay bloom clamps at max-channel " +
                 "0.5, so glow is bought with lit AREA, never with intensity. With the rim retired " +
                 "this and the plate lift are the WHOLE upgrade signal, so the pair has to carry it.")]
        public Color bloomColor = new(0.96f, 0.96f, 1f, 0.3f);
        [Tooltip("Plate fill while upgraded. It lifts further from the resting fill than it used to " +
                 "- the rim used to carry the state change and there is no rim now.")]
        public Color upgradedPlateColor = new(0.11f, 0.12f, 0.17f, 0.92f);

        [Header("Gauge - fills the ability cell linearly, behind the icon")]
        [Tooltip("The gauge's unfilled track. Sits behind the icon and reads as part of the plate.")]
        public Color gaugeTrackColor = new(0.086f, 0.094f, 0.129f, 0.9f);
        [Tooltip("The filled part. Rises bottom-to-top through the icon's cell, so the icon reads as " +
                 "filling up - one gauge shape for every meter on every vessel.")]
        public Color gaugeFillColor = new(0.22f, 0.51f, 1f, 0.55f);

        [Header("Locked slot - an ability that is not designed yet")]
        [Tooltip("Plate fill for a slot whose ability does not exist. Honest rather than empty: the " +
                 "element flower above it is still live, because the element IS real.")]
        public Color lockedPlateColor = new(0.024f, 0.031f, 0.063f, 0.55f);
        [Tooltip("The locked slot's placeholder mark - a short bar where the icon would be.")]
        public Color lockedMarkColor = new(0.361f, 0.373f, 0.439f, 0.55f);
        [Tooltip("Thickness of that mark.")]
        [Min(0.5f)] public float lockedMarkThickness = 2f;

        [Header("Cooldown - the fleet's ONE recharge readout")]
        [Tooltip("The veil swept over the ability plate while an ability recharges. Deliberately a " +
                 "RADIAL sweep over the icon, where the gauge is a LINEAR fill behind it: two " +
                 "motions that cannot be confused for each other, which is the whole reason a mode " +
                 "can show both on one card.")]
        public Color cooldownVeilColor = new(0.024f, 0.031f, 0.063f, 0.72f);

        [Tooltip("One-shot flash the moment an ability comes back. This is the beat the player is " +
                 "actually waiting for, so it is the loudest thing the card ever does.")]
        public Color cooldownReadyFlashColor = new(0.96f, 0.96f, 1f, 0.5f);
        [Min(0.01f)] public float cooldownReadyFlashDuration = 0.32f;

        [Header("Press feedback")]
        [Tooltip("Flash the CARD takes on an ability press. Replaces the per-vessel circular glow, " +
                 "which was authored for the old round button and reads as a foreign shape now.")]
        public Color pressFlashColor = new(0.96f, 0.96f, 1f, 0.22f);
        [Min(0.01f)] public float pressFlashDuration = 0.18f;

        [Header("Motion (states travel, nothing pops)")]
        [Tooltip("Seconds the rim/bloom take to cross between resting and upgraded.")]
        [Min(0.01f)] public float upgradeTransitionDuration = 0.2f;
        [Tooltip("One-shot scale punch on the card when an upgrade unlocks. 1 = no punch.")]
        [Min(1f)] public float unlockPunchScale = 1.05f;
        [Min(0.01f)] public float unlockPunchDuration = 0.5f;

        /// <summary>Total height of the lockup - both trapezoids plus the gap between them.</summary>
        public float PlateHeight => abilityCellHeight + cellGap + petalCellHeight;

        /// <summary>
        /// Vertical offset from the ability icon's centre to the CARD's centre. The lower trapezoid
        /// is centred on the icon, so the card's centre rides half of (upper cell + gap) above it.
        /// This is what keeps "no authored rect moves" true: the icon never learns about the card.
        /// </summary>
        public float CardCenterOffsetY => (petalCellHeight + cellGap) * 0.5f;

        /// <summary>Local Y of the ABILITY trapezoid's centre, from the card's centre.</summary>
        public float AbilityPlateLocalY => -CardCenterOffsetY;

        /// <summary>
        /// Local Y of the ELEMENT trapezoid's centre - and of the flower inside it - from the card's
        /// centre. One number for both, because the flower IS centred in its own plate.
        /// </summary>
        public float FlowerLocalY => (abilityCellHeight + cellGap) * 0.5f;

        /// <summary>
        /// The narrow edge as a fraction of the wide edge. Both plates read their edges from this
        /// ONE number, mirrored - the element plate narrows upward, the ability plate downward - so
        /// the slant can never disagree between the two halves of one totem.
        /// </summary>
        public float NarrowEdgeFraction =>
            plateWidth > 0.01f ? Mathf.Clamp01((plateWidth - trapezoidInset * 2f) / plateWidth) : 1f;

        /// <summary>
        /// The scale that draws an icon authored at <paramref name="authoredSize"/> at the fleet's
        /// one drawn size. This is what makes "every vessel's icons are the same size" true without
        /// anyone editing a prefab: the Dolphin authors 80 on three slots and 96 on its fourth, the
        /// Squirrel scales its whole button 0.7, and all of them still draw at iconBoxSize.
        /// </summary>
        /// <summary>
        /// How far the two plates are from being mirror images. 0 = perfectly balanced. The auditor
        /// and the tests both key off this, because "the totem drifted back into a coffin" is a
        /// thing a single field edit can do silently.
        /// </summary>
        public float PlateImbalance
        {
            get
            {
                float max = Mathf.Max(abilityCellHeight, petalCellHeight);
                return max > 0.01f ? Mathf.Abs(abilityCellHeight - petalCellHeight) / max : 0f;
            }
        }

        public float IconScaleFor(float authoredSize)
            => authoredSize > 0.01f ? iconBoxSize / authoredSize : 1f;
    }
}
