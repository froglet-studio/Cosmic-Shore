using System;
using System.Collections.Generic;
using CosmicShore.Data;
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
        [SerializeField, Min(1f)] float plateWidth = 104f;
        public float PlateWidth => plateWidth;

        [Tooltip("How far each side pulls in at a trapezoid's NARROW edge, in px. The element plate " +
                 "narrows upward and the ability plate narrows downward, so the pair mirrors about " +
                 "the gap. 0 makes both plates rectangles and the totem reads as a list.")]
        [SerializeField, Min(0f)] float trapezoidInset = 9f;
        public float TrapezoidInset => trapezoidInset;

        [Tooltip("Gap between the two trapezoids. This REPLACES the hairline divider: a real gap " +
                 "separates the cells without drawing a line, which is what lets the plates be " +
                 "borderless.")]
        [SerializeField, Min(0f)] float cellGap = 6f;
        public float CellGap => cellGap;

        [Tooltip("Height of the LOWER plate - the one centred on the vessel's existing ability icon. " +
                 "The icon is never moved; the lockup is built around wherever it already sits.")]
        [SerializeField, Min(1f)] float abilityCellHeight = 88f;
        public float AbilityCellHeight => abilityCellHeight;

        [Tooltip("Height of the UPPER plate, added ABOVE the ability plate. Keep it EQUAL to the " +
                 "ability plate: mirrored heights make the pair symmetric about the gap, and a tall " +
                 "narrow stack of two unequal plates reads as a coffin rather than as a totem. The " +
                 "hierarchy lives in the MARKS (flower smaller than icon), not in the plates.")]
        [SerializeField, Min(1f)] float petalCellHeight = 88f;
        public float PetalCellHeight => petalCellHeight;

        [Tooltip("Size of the element flower inside the upper plate. Authored EQUAL to iconBoxSize: " +
                 "the two plates are mirror images, so an equal mark in each lands on the same " +
                 "negative-space solution and the pair kerns as one object. (An earlier pass drew " +
                 "the flower smaller to keep the ability the 'headline'; with mirrored plates that " +
                 "just made the top one look under-filled - the two marks are different KINDS of " +
                 "thing, always in the same columns, so size was never what told them apart.)")]
        [SerializeField, Min(1f)] float petalFlowerSize = 60f;
        public float PetalFlowerSize => petalFlowerSize;

        [Tooltip("The size EVERY vessel's ability icon is drawn at, whatever size its prefab authored. " +
                 "The lockup derives each icon's scale from this (iconBoxSize / its authored size), so " +
                 "apparent size is uniform across the fleet and nobody re-authors an icon to match. " +
                 "60 in a 104 cell whose corner sliver eats 12 leaves an even 22 of air on every side - " +
                 "the lockup's KERNING. It multiplies the upgrade bump rather than replacing it.")]
        [SerializeField, Min(1f)] float iconBoxSize = 60f;
        public float IconBoxSize => iconBoxSize;

        [Header("Row (the lockup owns the whole row, on every vessel)")]
        [Tooltip("Centre-to-centre distance between cards. One number for the fleet - a vessel " +
                 "cannot space its own row, so the spacing is consistent by construction. Authored " +
                 "as plateWidth + 4x cellGap, so the space BETWEEN totems is four times the space " +
                 "WITHIN one: the same relationship as inter-word against inter-letter spacing, and " +
                 "what makes four cards read as four objects rather than one strip. It was 2x " +
                 "(pitch 116) until the rest-scale fix let the four authored cards draw at their " +
                 "real 104 width and closed the row up.")]
        [SerializeField, Min(1f)] float cardPitch = 128f;
        public float CardPitch => cardPitch;

        [Tooltip("Distance from the screen's RIGHT edge to the right edge of the last card.")]
        [SerializeField] float rowMarginRight = 40f;
        public float RowMarginRight => rowMarginRight;

        [Tooltip("Distance from the screen's BOTTOM edge to the bottom of the ability plate. The " +
                 "CONTROL CHIP hangs below that, so this must clear chipGap + chipHeight or every " +
                 "label falls off the screen - the row's real bottom margin is the remainder.")]
        [SerializeField] float rowMarginBottom = 44f;
        public float RowMarginBottom => rowMarginBottom;

        [Tooltip("Height of the control chip below the card. The chip is placed by the lockup, so a " +
                 "vessel can no longer author its own offset for the (LT)/(RT) label.")]
        [SerializeField, Min(1f)] float chipHeight = 24f;
        public float ChipHeight => chipHeight;

        [Tooltip("Gap between the bottom of the card and the control chip. Authored EQUAL to " +
                 "cellGap: the chip is a third element in the same stack, so it should sit off the " +
                 "ability plate by the same distance the element plate does.")]
        [SerializeField, Min(0f)] float chipGap = 6f;
        public float ChipGap => chipGap;

        [Tooltip("How far the bloom extends past the plate on every side.")]
        [SerializeField, Min(0f)] float bloomPadding = 26f;
        public float BloomPadding => bloomPadding;

        [Header("Control chip - the style-B keycap (Docs/MENU_REDESIGN_B.md section 22)")]
        [Tooltip("Draw each control chip as a KEYCAP - a slivered socket carrying the control's name " +
                 "(LT, RT, A, B; LSHIFT on a keyboard) - the same keycap every menu hint wears. Off " +
                 "falls back to the glyph set's pad artwork. A control with no name authored " +
                 "(ControlGlyphSetSO padLabel empty) still shows its artwork either way.")]
        [SerializeField] bool keycapChips = true;
        public bool KeycapChips => keycapChips;

        [Tooltip("The keycap's fill: kit_sliver10_fill, 9-sliced, tinted by keycapFillToken.")]
        [SerializeField] Sprite keycapFillSprite;
        public Sprite KeycapFillSprite => keycapFillSprite;

        [Tooltip("The keycap's hairline: kit_sliver10_line1, 9-sliced, tinted by keycapBorderToken.")]
        [SerializeField] Sprite keycapBorderSprite;
        public Sprite KeycapBorderSprite => keycapBorderSprite;

        [Tooltip("The keycap face. Chakra Petch SemiBold, the kit's Label face. Empty = the TMP " +
                 "default font.")]
        [SerializeField] TMPro.TMP_FontAsset keycapFont;
        public TMPro.TMP_FontAsset KeycapFont => keycapFont;

        [Tooltip("Point size of the control's name on the keycap.")]
        [SerializeField, Min(1f)] float keycapTextSize = 13f;
        public float KeycapTextSize => keycapTextSize;

        [Tooltip("Space either side of the name, in px. The keycap hugs its text, so LT and LSHIFT " +
                 "are different widths and both stay centred under their card.")]
        [SerializeField, Min(0f)] float keycapPadding = 8f;
        public float KeycapPadding => keycapPadding;

        [Tooltip("Keycap fill at rest (style B: Neutral Very Dark).")]
        [SerializeField] CosmicShore.UI.UIColorToken keycapFillToken = CosmicShore.UI.UIColorToken.SurfaceVeryDark;
        public CosmicShore.UI.UIColorToken KeycapFillToken => keycapFillToken;
        [Tooltip("Keycap hairline at rest (style B: Neutral Light).")]
        [SerializeField] CosmicShore.UI.UIColorToken keycapBorderToken = CosmicShore.UI.UIColorToken.SurfaceLight;
        public CosmicShore.UI.UIColorToken KeycapBorderToken => keycapBorderToken;
        [Tooltip("Keycap text at rest.")]
        [SerializeField] CosmicShore.UI.UIColorToken keycapTextToken = CosmicShore.UI.UIColorToken.TextLight;
        public CosmicShore.UI.UIColorToken KeycapTextToken => keycapTextToken;
        [Tooltip("Keycap fill while the control is HELD - the key reads as pressed in.")]
        [SerializeField] CosmicShore.UI.UIColorToken keycapHeldFillToken = CosmicShore.UI.UIColorToken.TextLight;
        public CosmicShore.UI.UIColorToken KeycapHeldFillToken => keycapHeldFillToken;
        [Tooltip("Keycap text while held, dark on the lit fill.")]
        [SerializeField] CosmicShore.UI.UIColorToken keycapHeldTextToken = CosmicShore.UI.UIColorToken.SurfaceBlack;
        public CosmicShore.UI.UIColorToken KeycapHeldTextToken => keycapHeldTextToken;

        [Header("Slant edge (sloped sides only)")]
        [Tooltip("Thickness of the band drawn INSIDE each plate along its two sloped sides. It is " +
                 "not a border - it never closes - so the plate's core stays transparent while the " +
                 "two edges that carry the shape's identity get an accent. 0 = off.")]
        [SerializeField, Min(0f)] float slantEdgeThickness = 3f;
        public float SlantEdgeThickness => slantEdgeThickness;

        [Tooltip("How far the band wraps around each corner onto the horizontal edges, in px. The " +
                 "whole grade to nothing happens along that wrap, so the sloped side stays solid " +
                 "end to end and the CORNER is lit rather than being where the accent died.")]
        [SerializeField, Min(0f)] float slantEdgeWrap = 14f;
        public float SlantEdgeWrap => slantEdgeWrap;

        [Tooltip("Width of the zero-alpha feather baked onto both sides of the band - the " +
                 "antialiasing. A generated diagonal gets none from the canvas, so without this it " +
                 "is stair-stepped. ~1px at the reference resolution.")]
        [SerializeField, Min(0f)] float slantEdgeAntialias = 1f;
        public float SlantEdgeAntialias => slantEdgeAntialias;

        [Tooltip("Resting colour of the slant edge.")]
        [SerializeField] Color slantEdgeColor = new(0.51f, 0.53f, 0.61f, 0.85f);
        public Color SlantEdgeColor => slantEdgeColor;

        [Tooltip("Slant edge while upgraded - the level-5 white the flowers already speak.")]
        [SerializeField] Color upgradedSlantEdgeColor = new(0.96f, 0.96f, 1f, 1f);
        public Color UpgradedSlantEdgeColor => upgradedSlantEdgeColor;

        [Tooltip("Height of the gauge that fills the ability cell behind the icon, as a fraction of " +
                 "that cell. 1 = the gauge rises the full height of the icon's cell.")]
        [SerializeField, Range(0.2f, 1f)] float gaugeCellFraction = 1f;
        public float GaugeCellFraction => gaugeCellFraction;

        [Header("Sprites (white + alpha, tinted at runtime)")]
        [Tooltip("9-sliced soft box glow. Reserved for the upgraded state. The ONLY sprite the " +
                 "lockup still needs - the plates are generated geometry (TrapezoidGraphic), because " +
                 "a trapezoid has no 9-slice and a sprited one would freeze the slant into the art.")]
        [SerializeField] Sprite bloomSprite;
        public Sprite BloomSprite => bloomSprite;

        [Tooltip("The mark a NON-ELEMENTAL card wears in its UPPER plate, where an elemental card " +
                 "carries its element flower. One row per core ability that has one; an ability " +
                 "with no row here has no upper cell at all, which is the Squirrel drift's shape. " +
                 "It lives on the fleet-wide style rather than on a vessel because the mark means " +
                 "the same thing on every hull - the OMNI CRYSTAL emblem says 'this is what you " +
                 "get for flying through a crystal' whoever you are flying - so authoring it " +
                 "per-vessel would be eight chances to disagree about one fact.")]
        [SerializeField] List<CoreAbilityEmblem> coreAbilityEmblems = new();
        public List<CoreAbilityEmblem> CoreAbilityEmblems => coreAbilityEmblems;

        /// <summary>The mark <paramref name="ability"/>'s card wears above its plate, or null.</summary>
        public Sprite EmblemFor(CoreAbility ability)
        {
            for (int i = 0; i < coreAbilityEmblems.Count; i++)
                if (coreAbilityEmblems[i].ability == ability) return coreAbilityEmblems[i].sprite;
            return null;
        }

        [Header("Colours - resting")]
        [Tooltip("Plate fill. Near-black and translucent so the arena reads through the row. " +
                 "Borderless: there is no resting outline, by design.")]
        [SerializeField] Color plateColor = new(0.024f, 0.031f, 0.063f, 0.86f);
        public Color PlateColor => plateColor;

        [Header("Element colour - the rev 3 lava lamp (retired 2026-10-24: elements carry no colour)")]
        [Tooltip("Each card wears its ELEMENT's colour: soft blobs drifting slowly behind the icon like a " +
                 "lava lamp, the slant edge in the same colour, and a matching keycap edge. Colour here " +
                 "is information (which element scales this ability), never a team colour. The flower " +
                 "petals keep their level ladder - they are the HUD's vocabulary and are never tinted. " +
                 "Off = the borderless near-black plates as before. OFF since 2026-10-24 (Garrett: elements are " +
                 "shown by shape, animation and effects, never a fixed colour; Docs/UI_COLOR_RULES.md).")]
        [SerializeField] bool elementColour;
        public bool ElementColour => elementColour;
        [SerializeField] Color chargeColour = new(1f, 0.827f, 0.302f, 1f);
        public Color ChargeColour => chargeColour;
        [SerializeField] Color massColour = new(0.498f, 0.659f, 1f, 1f);
        public Color MassColour => massColour;
        [SerializeField] Color spaceColour = new(0.690f, 0.486f, 1f, 1f);
        public Color SpaceColour => spaceColour;
        [SerializeField] Color timeColour = new(0.373f, 0.941f, 0.784f, 1f);
        public Color TimeColour => timeColour;
        [Tooltip("The drift card (no element) - the control blue.")]
        [SerializeField] Color driftColour = new(0f, 0.831f, 1f, 1f);
        public Color DriftColour => driftColour;
        [Tooltip("Blob alpha at its brightest. Low on purpose: the icon is the headline, the colour qualifies it.")]
        [SerializeField, Range(0f, 1f)] float lavaAlpha = 0.45f;
        public float LavaAlpha => lavaAlpha;
        [Tooltip("Blob diameter as a fraction of the plate width.")]
        [SerializeField, Range(0.2f, 1.5f)] float lavaBlobScale = 0.75f;
        public float LavaBlobScale => lavaBlobScale;
        [Tooltip("How far a blob wanders from the plate's centre, in px.")]
        [SerializeField, Min(0f)] float lavaDrift = 18f;
        public float LavaDrift => lavaDrift;
        [Tooltip("Seconds per wander cycle, min to max - each card picks its own so the row never pulses in step.")]
        [SerializeField] Vector2 lavaPeriod = new(9f, 14f);
        public Vector2 LavaPeriod => lavaPeriod;
        [Tooltip("Move the blobs. Off keeps the colour but holds it still (no per-frame layout work).")]
        [SerializeField] bool lavaMotion;
        public bool LavaMotion => lavaMotion;
        [Tooltip("Slant-edge alpha in the element colour.")]
        [SerializeField, Range(0f, 1f)] float elementEdgeAlpha = 0.9f;
        public float ElementEdgeAlpha => elementEdgeAlpha;

        /// <summary>The colour an element's card wears (rev 3), or the resting edge grey for none.</summary>
        public Color ColourFor(Element element) => element switch
        {
            Element.Charge => chargeColour,
            Element.Mass => massColour,
            Element.Space => spaceColour,
            Element.Time => timeColour,
            _ => slantEdgeColor,
        };

        [Header("Colours - upgraded")]
        [Tooltip("Upgraded bloom. Alpha carries it - in engine, gameplay bloom clamps at max-channel " +
                 "0.5, so glow is bought with lit AREA, never with intensity. With the rim retired " +
                 "this and the plate lift are the WHOLE upgrade signal, so the pair has to carry it.")]
        [SerializeField] Color bloomColor = new(0.96f, 0.96f, 1f, 0.3f);
        public Color BloomColor => bloomColor;
        [Tooltip("Plate fill while upgraded. It lifts further from the resting fill than it used to " +
                 "- the rim used to carry the state change and there is no rim now.")]
        [SerializeField] Color upgradedPlateColor = new(0.11f, 0.12f, 0.17f, 0.92f);
        public Color UpgradedPlateColor => upgradedPlateColor;

        [Header("Gauge - fills the ability cell linearly, behind the icon")]
        [Tooltip("The gauge's unfilled track. Sits behind the icon and reads as part of the plate.")]
        [SerializeField] Color gaugeTrackColor = new(0.086f, 0.094f, 0.129f, 0.9f);
        public Color GaugeTrackColor => gaugeTrackColor;
        [Tooltip("The filled part. Rises bottom-to-top through the icon's cell, so the icon reads as " +
                 "filling up - one gauge shape for every meter on every vessel.")]
        [SerializeField] Color gaugeFillColor = new(0.22f, 0.51f, 1f, 0.55f);
        public Color GaugeFillColor => gaugeFillColor;

        [Header("Locked slot - an ability that is not designed yet")]
        [Tooltip("Plate fill for a slot whose ability does not exist. Honest rather than empty: the " +
                 "element flower above it is still live, because the element IS real.")]
        [SerializeField] Color lockedPlateColor = new(0.024f, 0.031f, 0.063f, 0.55f);
        public Color LockedPlateColor => lockedPlateColor;
        [Tooltip("The locked slot's placeholder mark - a short bar where the icon would be.")]
        [SerializeField] Color lockedMarkColor = new(0.361f, 0.373f, 0.439f, 0.55f);
        public Color LockedMarkColor => lockedMarkColor;
        [Tooltip("Thickness of that mark.")]
        [SerializeField, Min(0.5f)] float lockedMarkThickness = 2f;
        public float LockedMarkThickness => lockedMarkThickness;

        [Header("Cooldown - the fleet's ONE recharge readout")]
        [Tooltip("The veil swept over the ability plate while an ability recharges. Deliberately a " +
                 "RADIAL sweep over the icon, where the gauge is a LINEAR fill behind it: two " +
                 "motions that cannot be confused for each other, which is the whole reason a mode " +
                 "can show both on one card.")]
        [SerializeField] Color cooldownVeilColor = new(0.024f, 0.031f, 0.063f, 0.72f);
        public Color CooldownVeilColor => cooldownVeilColor;

        [Tooltip("A flat wash over the WHOLE card, icon included, for as long as the ability recharges, under the " +
                 "sweep. The sweep alone is a dark wedge on a dark plate and read as nothing until a press was " +
                 "refused (Garrett, 2026-10-29: \"cooldowns in general are signaled too faintly unless you try to use " +
                 "the ability\"); the wash says UNAVAILABLE at a glance and the sweep says for how long. Alpha 0 = off.")]
        [SerializeField] Color cooldownWashColor = new(0.024f, 0.031f, 0.063f, 0.55f);
        public Color CooldownWashColor => cooldownWashColor;

        [Tooltip("One-shot flash the moment an ability comes back. This is the beat the player is " +
                 "actually waiting for, so it is the loudest thing the card ever does.")]
        [SerializeField] Color cooldownReadyFlashColor = new(0.96f, 0.96f, 1f, 0.75f);
        public Color CooldownReadyFlashColor => cooldownReadyFlashColor;
        [SerializeField, Min(0.01f)] float cooldownReadyFlashDuration = 0.32f;
        public float CooldownReadyFlashDuration => cooldownReadyFlashDuration;

        [Header("Press feedback")]
        [Tooltip("Flash the CARD takes on an ability press. Replaces the per-vessel circular glow, " +
                 "which was authored for the old round button and reads as a foreign shape now.")]
        [SerializeField] Color pressFlashColor = new(0.96f, 0.96f, 1f, 0.22f);
        public Color PressFlashColor => pressFlashColor;
        [SerializeField, Min(0.01f)] float pressFlashDuration = 0.18f;
        public float PressFlashDuration => pressFlashDuration;

        [Header("Motion (states travel, nothing pops)")]
        [Tooltip("Seconds the rim/bloom take to cross between resting and upgraded.")]
        [SerializeField, Min(0.01f)] float upgradeTransitionDuration = 0.2f;
        public float UpgradeTransitionDuration => upgradeTransitionDuration;
        [Tooltip("One-shot scale punch on the card when an upgrade unlocks. 1 = no punch.")]
        [SerializeField, Min(1f)] float unlockPunchScale = 1.05f;
        public float UnlockPunchScale => unlockPunchScale;
        [SerializeField, Min(0.01f)] float unlockPunchDuration = 0.5f;
        public float UnlockPunchDuration => unlockPunchDuration;

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
        /// <summary>
        /// How far the slant band reaches inward from a plate's outline. Anything drawn ON a plate
        /// that is not meant to cover the band - the gauge and its clip - insets by this.
        /// </summary>
        public float PlateEdgeReach => slantEdgeThickness + slantEdgeAntialias;

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

        /// <summary>
        /// One core ability's upper-plate mark. A tiny type rather than two parallel lists,
        /// because a list of abilities and a list of sprites can fall out of step and a list of
        /// pairs cannot.
        /// </summary>
        [Serializable]
        public struct CoreAbilityEmblem
        {
            [Tooltip("Which non-elemental card wears this mark.")]
            public CoreAbility ability;

            [Tooltip("White + alpha, like every other lockup sprite. Drawn UNTINTED - the emblem " +
                     "says what the card IS, and a domain tint would say who owns it, which for " +
                     "an omni crystal is nobody until somebody takes it.")]
            public Sprite sprite;
        }
    }
}
