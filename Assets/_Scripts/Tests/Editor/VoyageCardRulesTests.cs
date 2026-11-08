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
