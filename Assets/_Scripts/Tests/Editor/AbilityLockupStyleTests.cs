#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The ability lockup's geometry is one shared asset that every vessel's HUD reads
    /// (Docs/ABILITY_LOCKUP.md). That is what makes the style consistent, and it is also the whole
    /// exposure: a single field edited to a plausible-looking number silently changes every vessel
    /// at once, and the failure — an icon overflowing its plate, or an element flower that has
    /// grown larger than the ability it qualifies — only ever surfaces by someone flying a vessel
    /// and noticing.
    ///
    /// These assert the RELATIONSHIPS the design rests on rather than the values themselves, so
    /// retuning stays free and only a change that breaks the composition fails.
    /// </summary>
    public class AbilityLockupStyleTests
    {
        /// <summary>The size every shipped vessel authors its ability icons at.</summary>
        const float FleetIconSize = 80f;
        const float MinMarginPx = 8f;

        static AbilityLockupStyleSO Load()
        {
            var style = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            Assert.IsNotNull(style, "Resources/AbilityLockupStyle is missing - every vessel would " +
                                    "fall back to un-styled ability icons.");
            return style;
        }

        [Test]
        public void Style_ShipsEverySprite()
        {
            var s = Load();
            Assert.IsNotNull(s.plateSprite, "no plate sprite - the card has no body");
            Assert.IsNotNull(s.rimSprite,   "no rim sprite - no resting hairline and no upgrade rim");
            Assert.IsNotNull(s.bloomSprite, "no bloom sprite - the upgrade loses its glow");
        }

        [Test]
        public void Kerning_LeavesNegativeSpaceAroundTheFleetIcon()
        {
            var s = Load();
            float drawn  = FleetIconSize * s.iconContentScale;
            float cell   = Mathf.Min(s.plateWidth, s.abilityCellHeight);
            float margin = (cell - drawn) * 0.5f;

            Assert.GreaterOrEqual(margin, MinMarginPx,
                $"an {FleetIconSize} icon draws at {drawn} inside a {cell} cell, leaving {margin} of " +
                "air. The card's corner sliver alone eats 12, so the icon would run into it.");
        }

        [Test]
        public void Flower_StaysSmallerThanTheDrawnIcon()
        {
            var s = Load();
            float drawn = FleetIconSize * s.iconContentScale;
            Assert.Less(s.petalFlowerSize, drawn,
                "the element flower must stay under the ability icon's DRAWN size - the ability is " +
                "the headline and the element qualifies it. Shrinking the icon without shrinking " +
                "the flower is how that hierarchy inverts unnoticed.");
        }

        [Test]
        public void Flower_FitsItsOwnCell()
        {
            var s = Load();
            Assert.LessOrEqual(s.petalFlowerSize, s.petalCellHeight,
                "the flower is taller than the cell it sits in");
        }

        [Test]
        public void Geometry_StacksTheTwoCellsWithoutOverlapOrGap()
        {
            var s = Load();
            Assert.AreEqual(s.abilityCellHeight + s.petalCellHeight, s.PlateHeight, 0.001f,
                "the card is not exactly its two cells");

            // The divider sits on the seam, and the flower is centred in the cell above it.
            Assert.AreEqual(s.DividerLocalY + s.petalCellHeight * 0.5f, s.FlowerLocalY, 0.001f,
                "the flower is not centred in the element cell the divider opens");

            // The lower cell is centred on the icon, so the card's centre rides half the upper cell
            // above it - this is what lets a vessel adopt the style with no authored rect moving.
            Assert.AreEqual(s.petalCellHeight * 0.5f, s.CardCenterOffsetY, 0.001f,
                "the ability cell would not be centred on the icon it is built around");
        }

        [Test]
        public void Upgrade_IsVisibleAndTravels()
        {
            var s = Load();
            Assert.Greater(s.bloomColor.a, 0f, "the upgraded bloom is fully transparent");
            Assert.Greater(s.upgradedRimColor.a, 0f, "the upgraded rim is fully transparent");
            Assert.Greater(s.upgradeTransitionDuration, 0f,
                "states must travel, never pop - a zero duration snaps the card between states");
            Assert.GreaterOrEqual(s.unlockPunchScale, 1f, "the unlock punch must not shrink the card");
        }
    }
}
#endif
