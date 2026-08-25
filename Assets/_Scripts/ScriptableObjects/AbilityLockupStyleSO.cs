using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The one source of truth for the ABILITY LOCKUP - the fleet-wide card that fuses an ability
    /// icon with the element indicator that upgrades it, so a single glance answers four questions:
    /// how much of the element you hold, which ability it upgrades, whether that upgrade is live,
    /// and how to fire it.
    ///
    /// <para>Shape is the "TOTEM": one silhouette, two stacked cells - the element flower in the
    /// upper cell, the ability icon in the lower one, a hairline divider between them, the control
    /// chip below. The flower is deliberately SMALLER than the ability icon: the icon names the
    /// ability and is what a pilot hunts for, the flower qualifies it.</para>
    ///
    /// <para>House motif is soft-hard-soft: BLOOM (soft) around a FLAT, radius-0, corner-slivered
    /// plate (hard) carrying a smooth-curve glyph (soft). Nothing glows at rest - the bloom and the
    /// bright rim are reserved for the upgraded state, per STYLE_FOUNDATION's "glow is state".</para>
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
        [Tooltip("Width of the lockup plate. The shipped ability cells are 150 wide, so this sits " +
                 "inside them with margin and leaves the row's authored pitch untouched.")]
        [Min(1f)] public float plateWidth = 104f;

        [Tooltip("Height of the LOWER cell - the one centred on the vessel's existing ability icon. " +
                 "The icon is never moved; the lockup is built around wherever it already sits.")]
        [Min(1f)] public float abilityCellHeight = 104f;

        [Tooltip("Height of the UPPER cell, added ABOVE the ability cell. This is what makes the " +
                 "card a totem, and it is why no authored rect has to change to adopt the style.")]
        [Min(1f)] public float petalCellHeight = 62f;

        [Tooltip("Size of the element flower inside the upper cell. Keep it BELOW the ability icon's " +
                 "drawn size: the ability is the headline, the element qualifies it.")]
        [Min(1f)] public float petalFlowerSize = 44f;

        [Tooltip("Scale applied to the vessel's ability icon inside the card - the lockup's KERNING. " +
                 "A shipped icon is 80 square in a 104 cell whose corner sliver already eats 12, so at " +
                 "1.0 its corners run into the sliver and the card reads packed. 0.75 draws it at 60, " +
                 "leaving an even 22 of negative space on every side. It multiplies the upgrade bump " +
                 "rather than replacing it, so an upgraded icon rests at scale x 1.15 as before.")]
        [Range(0.4f, 1f)] public float iconContentScale = 0.75f;

        [Tooltip("Horizontal inset of the divider from the plate edge.")]
        [Min(0f)] public float dividerInset = 8f;

        [Tooltip("Divider thickness. 1 = the hairline the style guide specifies.")]
        [Min(0f)] public float dividerThickness = 1f;

        [Tooltip("How far the bloom extends past the plate on every side.")]
        [Min(0f)] public float bloomPadding = 26f;

        [Header("Sprites (white + alpha, tinted at runtime)")]
        [Tooltip("9-sliced plate body with the corner sliver on two opposite corners.")]
        public Sprite plateSprite;
        [Tooltip("9-sliced outline matching the plate silhouette. Carries BOTH the resting hairline " +
                 "and the upgraded rim - the two differ by colour and alpha, not by asset.")]
        public Sprite rimSprite;
        [Tooltip("9-sliced soft box glow. Reserved for the upgraded state.")]
        public Sprite bloomSprite;

        [Header("Colours - resting")]
        [Tooltip("Plate fill. Near-black and translucent so the arena reads through the row.")]
        public Color plateColor = new(0.024f, 0.031f, 0.063f, 0.86f);
        [Tooltip("Resting outline - the STYLE_FOUNDATION inactive-light hairline.")]
        public Color hairlineColor = new(0.361f, 0.373f, 0.439f, 0.9f);
        [Tooltip("Divider between the element cell and the ability cell.")]
        public Color dividerColor = new(0.145f, 0.157f, 0.216f, 1f);

        [Header("Colours - upgraded")]
        [Tooltip("Upgraded rim. The level-5 white the element flowers already speak - all-petals-" +
                 "white IS level 5, so the lit frame and the full flower say the same thing.")]
        public Color upgradedRimColor = new(0.96f, 0.96f, 1f, 1f);
        [Tooltip("Upgraded bloom. Alpha carries it - in engine, gameplay bloom clamps at max-channel " +
                 "0.5, so glow is bought with lit AREA, never with intensity.")]
        public Color bloomColor = new(0.96f, 0.96f, 1f, 0.24f);
        [Tooltip("Plate fill while upgraded. Kept close to the resting fill: the RIM changes, not the body.")]
        public Color upgradedPlateColor = new(0.024f, 0.031f, 0.063f, 0.9f);

        [Header("Motion (states travel, nothing pops)")]
        [Tooltip("Seconds the rim/bloom take to cross between resting and upgraded.")]
        [Min(0.01f)] public float upgradeTransitionDuration = 0.2f;
        [Tooltip("One-shot scale punch on the card when an upgrade unlocks. 1 = no punch.")]
        [Min(1f)] public float unlockPunchScale = 1.05f;
        [Min(0.01f)] public float unlockPunchDuration = 0.5f;

        /// <summary>Total height of the lockup card - both cells stacked.</summary>
        public float PlateHeight => abilityCellHeight + petalCellHeight;

        /// <summary>
        /// Vertical offset from the ability icon's centre to the CARD's centre. The lower cell is
        /// centred on the icon, so the card's centre rides half the upper cell above it.
        /// </summary>
        public float CardCenterOffsetY => petalCellHeight * 0.5f;

        /// <summary>Local Y of the divider, measured from the card's centre.</summary>
        public float DividerLocalY => (abilityCellHeight - petalCellHeight) * 0.5f;

        /// <summary>Local Y of the element flower's centre, measured from the card's centre.</summary>
        public float FlowerLocalY => abilityCellHeight * 0.5f;
    }
}
