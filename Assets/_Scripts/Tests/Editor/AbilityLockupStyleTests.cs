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
        const float MinMarginPx = 8f;

        static AbilityLockupStyleSO Load()
        {
            var style = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            Assert.IsNotNull(style, "Resources/AbilityLockupStyle is missing - every vessel would " +
                                    "fall back to un-styled ability icons.");
            return style;
        }

        [Test]
        public void Style_ShipsTheOneSpriteItStillNeeds()
        {
            var s = Load();
            // The plates are generated geometry now, so the bloom is the last authored asset - and
            // with the rim retired it is half the upgrade signal, not a flourish on top of one.
            Assert.IsNotNull(s.bloomSprite, "no bloom sprite - the upgrade loses its glow, and the " +
                                            "plates are borderless, so only the plate lift would remain");
        }

        [Test]
        public void Totem_IsTwoTrapezoidsSeparatedByARealGap()
        {
            var s = Load();

            // The gap IS the divider and the slant IS the frame. Either at zero and the totem falls
            // back to the two stacked rectangles this shape was chosen over.
            Assert.Greater(s.cellGap, 0f,
                "cellGap 0 fuses the two plates into one shape - the gap is what replaced the hairline");
            Assert.Greater(s.trapezoidInset, 0f,
                "trapezoidInset 0 makes both plates rectangles; borderless rectangles read as a list, " +
                "not as one waisted object");

            Assert.Less(s.trapezoidInset * 2f, s.plateWidth * 0.5f,
                "the slant eats more than half the plate - the totem becomes a pair of wedges");
            Assert.Less(s.NarrowEdgeFraction, 1f, "the narrow edge is not narrower than the wide edge");
            Assert.Greater(s.NarrowEdgeFraction, 0.5f, "the taper is extreme enough to read as a funnel");
        }

        [Test]
        public void Plates_AreBalancedEnoughToReadAsSymmetric()
        {
            var s = Load();
            // Two stacked plates of very different heights read as a coffin, not a totem. The
            // hierarchy that matters lives in the MARKS - flower smaller than icon - not in the
            // plates, so nothing is lost by mirroring them and a great deal of shape is gained.
            Assert.LessOrEqual(s.PlateImbalance, 0.25f,
                $"the plates are {s.PlateImbalance:P0} out of balance " +
                $"({s.abilityCellHeight} vs {s.petalCellHeight}) - the totem stops reading as symmetric");
        }

        [Test]
        public void SlantEdge_RidesTheSlantItAccents()
        {
            var s = Load();
            if (s.slantEdgeThickness <= 0f) return;   // off is a legitimate authored state

            Assert.Greater(s.slantEdgeColor.a, 0f,
                "the slant edge has thickness but no opacity - geometry that draws nothing");
            Assert.LessOrEqual(s.slantEdgeThickness + s.slantEdgeAntialias, s.trapezoidInset,
                "the band reaches further inward than the slant it rides, so it reads as a chamfer");
            Assert.Greater(s.slantEdgeAntialias, 0f,
                "no antialias feather - a generated diagonal gets none from the canvas and will " +
                "stair-step; the feather IS the antialiasing");
            Assert.Greater(s.upgradedSlantEdgeColor.a, 0f, "the upgraded slant edge is transparent");
        }

        [Test]
        public void Cooldown_ReadsAsRechargingAndIsLoudWhenItReturns()
        {
            var s = Load();
            Assert.Greater(s.cooldownVeilColor.a, 0f,
                "the cooldown veil is transparent - a recharging ability would look ready");
            Assert.Greater(s.cooldownReadyFlashColor.a, s.pressFlashColor.a,
                "the ready flash is no louder than an ordinary press, but coming back off cooldown " +
                "is the beat the player is actually waiting for");
            Assert.Greater(s.cooldownReadyFlashDuration, 0f, "the ready flash snaps off");
        }

        [Test]
        public void Icon_ClearsTheAbilityPlatesNarrowEdge()
        {
            var s = Load();
            // The ability plate tapers DOWNWARD, so the tightest place the icon must clear is its
            // base - measuring against the rect would pass a size that overhangs the visible shape.
            float narrowEdge = s.plateWidth * s.NarrowEdgeFraction;
            Assert.Greater((narrowEdge - s.iconBoxSize) * 0.5f, 0f,
                $"an icon drawn at {s.iconBoxSize} overhangs the ability plate's {narrowEdge} narrow edge");
        }

        [Test]
        public void Kerning_LeavesNegativeSpaceAroundTheDrawnIcon()
        {
            var s = Load();
            float cell   = Mathf.Min(s.plateWidth * s.NarrowEdgeFraction, s.abilityCellHeight);
            float margin = (cell - s.iconBoxSize) * 0.5f;

            Assert.GreaterOrEqual(margin, MinMarginPx,
                $"an icon drawn at {s.iconBoxSize} leaves {margin} of air against the ability plate's " +
                $"tightest dimension ({cell}) - kerned, not packed.");
        }

        [Test]
        public void Marks_AreBalancedAgainstTheirMirroredPlates()
        {
            var s = Load();

            // The plates are mirror images, so equal marks land on the SAME negative-space solution
            // and the pair kerns as one object. Equality is the intent; the flower may be smaller
            // but must never exceed the icon, which would invert what the row is built around.
            Assert.LessOrEqual(s.petalFlowerSize, s.iconBoxSize,
                "the element flower is drawn larger than the ability icon");
            Assert.GreaterOrEqual(s.petalFlowerSize / s.iconBoxSize, 0.75f,
                $"the flower ({s.petalFlowerSize}) is much smaller than the icon ({s.iconBoxSize}); " +
                "in mirrored plates that reads as the upper plate being under-filled, not as hierarchy");
        }

        [Test]
        public void IconScale_NormalisesEveryAuthoredSizeToOneDrawnSize()
        {
            var s = Load();
            // The three sizes the fleet actually authors today, plus a nonsense one.
            foreach (var authored in new[] { 80f, 96f, 148f })
                Assert.AreEqual(s.iconBoxSize, authored * s.IconScaleFor(authored), 0.01f,
                    $"an icon authored at {authored} must still draw at {s.iconBoxSize} - that is what " +
                    "makes icon size uniform across the fleet without anyone editing a prefab.");

            Assert.AreEqual(1f, s.IconScaleFor(0f), 0.001f,
                "an unreadable authored size must fall back to scale 1, never divide by zero");
        }

        [Test]
        public void Row_IsSpacedWiderThanTheCardsAreWide()
        {
            var s = Load();
            Assert.Greater(s.cardPitch, s.plateWidth,
                "cards would overlap: the row pitch is narrower than a card");
        }

        [Test]
        public void Flower_FitsItsOwnCell()
        {
            var s = Load();
            Assert.LessOrEqual(s.petalFlowerSize, s.petalCellHeight,
                "the flower is taller than the cell it sits in");
        }

        [Test]
        public void Geometry_StacksTheTwoPlatesAcrossExactlyTheAuthoredGap()
        {
            var s = Load();
            Assert.AreEqual(s.abilityCellHeight + s.cellGap + s.petalCellHeight, s.PlateHeight, 0.001f,
                "the totem is not exactly its two plates plus the gap");

            // Facing edges: the top of the ability plate and the bottom of the element plate must be
            // exactly cellGap apart, or the seam the shape is built around is not where it says.
            float abilityTop   = s.AbilityPlateLocalY + s.abilityCellHeight * 0.5f;
            float elementBottom = s.FlowerLocalY - s.petalCellHeight * 0.5f;
            Assert.AreEqual(s.cellGap, elementBottom - abilityTop, 0.001f,
                "the plates do not face each other across the authored gap");

            // The lower plate is centred on the icon, so the card's centre rides half of
            // (upper plate + gap) above it - this is what lets a vessel adopt the style with no
            // authored rect moving.
            Assert.AreEqual(0f, s.AbilityPlateLocalY + s.CardCenterOffsetY, 0.001f,
                "the ability plate would not be centred on the icon it is built around");
        }

        [Test]
        public void Gauge_FillsTheAbilityCellAndReadsAgainstItsTrack()
        {
            var s = Load();

            Assert.Greater(s.gaugeCellFraction, 0f, "the gauge would have no height to fill");
            Assert.LessOrEqual(s.gaugeCellFraction, 1f,
                "the gauge would overflow the ability cell and run under the element flower");

            // A fill the same colour as its track is a gauge you cannot read. Compare in luminance
            // rather than per-channel: the two are authored as a dim track and a bright fill.
            float track = Luminance(s.gaugeTrackColor) * s.gaugeTrackColor.a;
            float fill  = Luminance(s.gaugeFillColor)  * s.gaugeFillColor.a;
            Assert.Greater(fill - track, 0.1f,
                "the gauge fill does not read against its own track - a meter nobody can see is the " +
                "state the ring gauges were replaced to fix");
        }

        [Test]
        public void LockedSlot_ReadsAsQuieterThanALiveOne()
        {
            var s = Load();

            // An undesigned slot must be legible as a slot and unmistakable for a live ability -
            // otherwise the Rhino's three open slots read as three abilities the player cannot find.
            Assert.Less(Luminance(s.lockedPlateColor) * s.lockedPlateColor.a,
                        Luminance(s.plateColor) * s.plateColor.a + 0.001f,
                "a locked card is brighter than a live one - the row would advertise abilities that " +
                "do not exist yet");
            Assert.Greater(s.lockedMarkColor.a, 0f,
                "the locked slot draws nothing at all, so the row silently loses a column");
        }

        [Test]
        public void ControlChip_SitsBelowTheCardWithClearance()
        {
            var s = Load();
            Assert.Greater(s.chipHeight, 0f, "the control chip has no height, so no hint can land on it");
            Assert.GreaterOrEqual(s.chipGap, 0f, "a negative gap puts the chip inside the card");

            // The chip must clear the card AND still be inside the row's bottom margin, or every
            // control label lands off the bottom of the screen (which is how this failed before).
            float reach = s.chipGap + s.chipHeight;
            Assert.Less(reach, s.rowMarginBottom,
                $"the control chip reaches {reach}px below the card but the row only sits " +
                $"{s.rowMarginBottom}px off the bottom of the screen - the labels would be clipped");
        }

        [Test]
        public void PressFlash_IsVisibleAndDecays()
        {
            var s = Load();
            Assert.Greater(s.pressFlashColor.a, 0f,
                "the press flash is transparent - a fired ability would show nothing, which is what " +
                "the retired circular glow used to do");
            Assert.Greater(s.pressFlashDuration, 0f,
                "a zero decay snaps the flash off; nothing pops out of existence");
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        [Test]
        public void Upgrade_IsVisibleAndTravels()
        {
            var s = Load();
            // Borderless: the bloom and the plate lift are the WHOLE upgrade signal now, so both
            // have to be real. A bloom alone on an unchanged plate was the state the rim used to
            // rescue.
            Assert.Greater(s.bloomColor.a, 0f, "the upgraded bloom is fully transparent");
            Assert.Greater(Luminance(s.upgradedPlateColor) * s.upgradedPlateColor.a
                         - Luminance(s.plateColor) * s.plateColor.a, 0.01f,
                "the upgraded plate does not lift off the resting plate - with no rim, that leaves " +
                "the bloom carrying the upgrade by itself");
            Assert.Greater(s.upgradeTransitionDuration, 0f,
                "states must travel, never pop - a zero duration snaps the card between states");
            Assert.GreaterOrEqual(s.unlockPunchScale, 1f, "the unlock punch must not shrink the card");
        }
    }
}
#endif
