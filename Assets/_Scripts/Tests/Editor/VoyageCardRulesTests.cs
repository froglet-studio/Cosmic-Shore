#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The open voyage card's rules (<see cref="VoyageCardRules"/>) and the Voyage window's tab steps
    /// (<see cref="VoyageWindowTabs.Step"/>), the coverflow's poses, the place art's grade and the cover crop, Docs/MENU_REDESIGN_B.md
    /// sections 119 and 151.
    /// </summary>
    [TestFixture]
    public class VoyageCardRulesTests
    {
        [Test]
        public void Wrap_GoesRoundBothWays()
        {
            Assert.AreEqual(1, VoyageCardRules.Wrap(0, 1, 4));
            Assert.AreEqual(0, VoyageCardRules.Wrap(3, 1, 4), "the last voyage steps round to the first");
            Assert.AreEqual(3, VoyageCardRules.Wrap(0, -1, 4), "the first steps back to the last");
            Assert.AreEqual(-1, VoyageCardRules.Wrap(0, 1, 0), "no voyages, no index");
        }

        [Test]
        public void Cycle_StepsThroughTheOptions_AndStartsOverWhenTheValueIsNotOne()
        {
            var elements = new[] { Element.Charge, Element.Mass, Element.Space, Element.Time };
            Assert.AreEqual(Element.Mass, VoyageCardRules.Cycle(elements, Element.Charge));
            Assert.AreEqual(Element.Charge, VoyageCardRules.Cycle(elements, Element.Time), "Time goes round to Charge");
            Assert.AreEqual(Element.Charge, VoyageCardRules.Cycle(elements, Element.None), "an unset element starts on the first");
            var artifacts = new List<string> { null, "a", "b" };
            Assert.AreEqual("a", VoyageCardRules.Cycle(artifacts, null), "no artifact steps to the first one");
            Assert.AreEqual(null, VoyageCardRules.Cycle(artifacts, "b"), "the last artifact steps back to none");
            Assert.AreEqual(default(Element), VoyageCardRules.Cycle(new Element[0], Element.Mass));
        }

        [Test]
        public void Beats_LeaveOutTheEmptyOnes()
        {
            var beats = VoyageCardRules.Beats(
                new VoyageCardRules.Beat("THE NEWS", "", "Celadon Harbour"),
                new VoyageCardRules.Beat("THE PLACE", "Vault shells drift here.", "Lantern Reach"),
                new VoyageCardRules.Beat("WHO BROUGHT IT", null, null),
                new VoyageCardRules.Beat("THE WATER", "Light takes 580 years.", "water on the way"));
            Assert.AreEqual(2, beats.Count, "a voyage without news or a story starts on its place");
            Assert.AreEqual("THE PLACE", beats[0].Label);
            Assert.AreEqual("THE WATER", beats[1].Label);
        }

        [Test]
        public void FlowPose_PickedCardFacesFront_AndEachStepOutTurnsShrinksAndFades()
        {
            var front = VoyageCardRules.FlowPose(0);
            Assert.AreEqual(0f, front.Step);
            Assert.AreEqual(0f, front.Turn, "the picked card faces front");
            Assert.AreEqual(1f, front.Scale, "and stands at full size");
            Assert.AreEqual(1f, front.Alpha);
            var right = VoyageCardRules.FlowPose(1);
            var left = VoyageCardRules.FlowPose(-1);
            Assert.AreEqual(1f, right.Step);
            Assert.AreEqual(-1f, left.Step);
            Assert.AreEqual(-40f, right.Turn, 1e-4f, "a card to the right turns away to the right");
            Assert.AreEqual(40f, left.Turn, 1e-4f, "and one to the left mirrors it");
            Assert.AreEqual(0.82f, right.Scale, 1e-4f);
            Assert.AreEqual(0.7f, right.Alpha, 1e-4f);
            var far = VoyageCardRules.FlowPose(5);
            Assert.AreEqual(0.64f, far.Scale, 1e-4f, "the shrinking stops two steps out");
            Assert.AreEqual(0.35f, far.Alpha, 1e-4f);
        }

        [Test]
        public void PlaceArt_Mute_KeepsAQuarterOfTheColour_AtJustOverHalfTheBrightness()
        {
            var grey = new Color32(100, 100, 100, 255);
            var g = VoyagePlaceArt.Mute(grey, 0.25f, 0.55f);
            Assert.AreEqual(55, g.r, "a grey only darkens");
            Assert.AreEqual(g.r, g.g);
            Assert.AreEqual(g.g, g.b);
            var blue = new Color32(40, 105, 184, 200);
            var m = VoyagePlaceArt.Mute(blue, 0.25f, 0.55f);
            Assert.AreEqual(200, m.a, "alpha is kept");
            Assert.Less(m.b - m.r, (blue.b - blue.r) / 2, "most of the blue is gone: the card no longer reads blue");
            var same = VoyagePlaceArt.Mute(blue, 1f, 1f);
            Assert.AreEqual(blue.r, same.r, "saturation 1 and brightness 1 leave the colour as it was");
            Assert.AreEqual(blue.g, same.g);
            Assert.AreEqual(blue.b, same.b);
        }

        [Test]
        public void Cover_CropsToTheFramesShape_Centred()
        {
            var wide = new Texture2D(256, 160);
            var r = VoyageOpenCard.Cover(wide, 340f / 300f);
            Assert.AreEqual(1f, r.height, 1e-4f, "a wider picture keeps its whole height");
            Assert.AreEqual((340f / 300f) / (256f / 160f), r.width, 1e-4f);
            Assert.AreEqual((1f - r.width) * 0.5f, r.x, 1e-4f, "and is cropped equally on both sides");
            var tall = VoyageOpenCard.Cover(wide, 3f);
            Assert.AreEqual(1f, tall.width, 1e-4f, "a narrower picture keeps its whole width");
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), VoyageOpenCard.Cover(null, 1.5f), "no picture, the whole rectangle");
            UnityEngine.Object.DestroyImmediate(wide);
        }

        [Test]
        public void PlaceArt_Spread_LiftsDarkWaterIntoTheRange_KeepingEachTexelsTint()
        {
            // Two near-black texels of thin open sea, one slightly bluer: the darker lands on the floor, the lighter on
            // the ceiling, and the blue one stays bluer than it is red.
            var px = new[] { new Color32(10, 10, 12, 255), new Color32(14, 16, 30, 255) };
            VoyagePlaceArt.Spread(px, 34, 150);
            float Luma(Color32 c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            Assert.AreEqual(34f, Luma(px[0]), 1.5f, "the darkest texel sits on the floor");
            Assert.AreEqual(150f, Luma(px[1]), 1.5f, "the brightest on the ceiling");
            Assert.Greater(px[1].b, px[1].r, "a texel keeps its tint");
            Assert.AreEqual(255, px[1].a);
            var flat = new[] { new Color32(5, 5, 5, 255), new Color32(5, 5, 5, 255) };
            VoyagePlaceArt.Spread(flat, 34, 150);
            Assert.AreEqual(92f, Luma(flat[0]), 1.5f, "a flat picture is lifted to the middle of the range");
        }

        [Test]
        public void SpriteCover_StaysInsideTheSpritesOwnRect_InAnAtlas()
        {
            // A 200 x 100 sprite in the top-right of a 400 x 400 atlas, shown on a square card.
            var atlas = new Texture2D(400, 400);
            var sprite = Sprite.Create(atlas, new Rect(200f, 300f, 200f, 100f), new Vector2(0.5f, 0.5f));
            var r = QuestCarouselView.SpriteCover(sprite, 1f);
            Assert.AreEqual(100f / 400f, r.height, 1e-4f, "the whole height of the sprite, in atlas uv");
            Assert.AreEqual(100f / 400f, r.width, 1e-4f, "a square of it");
            Assert.AreEqual(0.5f + 50f / 400f, r.x, 1e-4f, "centred across the sprite, not the atlas");
            Assert.AreEqual(300f / 400f, r.y, 1e-4f);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), QuestCarouselView.SpriteCover(null, 1f), "no sprite, the whole rectangle");
            UnityEngine.Object.DestroyImmediate(sprite);
            UnityEngine.Object.DestroyImmediate(atlas);
        }

        [Test]
        public void Tabs_StopAtEitherEnd()
        {
            Assert.AreEqual(1, VoyageWindowTabs.Step(0, +1, 4), "RB from Voyages is Saga");
            Assert.AreEqual(3, VoyageWindowTabs.Step(3, +1, 4), "RB on Episodes stays");
            Assert.AreEqual(0, VoyageWindowTabs.Step(0, -1, 4), "LB on Voyages stays");
            Assert.AreEqual(-1, VoyageWindowTabs.Step(0, 1, 0), "no tabs");
        }
    }
}
#endif
