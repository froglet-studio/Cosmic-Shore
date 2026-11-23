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
    /// (<see cref="VoyageWindowTabs.Step"/>), Docs/MENU_REDESIGN_B.md section 119.
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
        public void Frame_CentresOnTheCourse_AtTheCardsShape_InsideTheWater()
        {
            var lo = new Vector2(0f, 0f);
            var hi = new Vector2(1000f, 600f);
            var course = new List<Vector2> { new(400f, 200f), new(500f, 300f) };
            var (flo, fhi) = VoyageCardRules.Frame(lo, hi, course, 0.8f);
            var size = fhi - flo;
            Assert.AreEqual(0.8f, size.x / size.y, 1e-3f, "the frame has the card's shape");
            Assert.AreEqual(450f, (flo.x + fhi.x) * 0.5f, 1e-2f, "centred on the course across");
            Assert.AreEqual(250f, (flo.y + fhi.y) * 0.5f, 1e-2f, "centred on the course down");
            Assert.That(flo.x >= lo.x && flo.y >= lo.y && fhi.x <= hi.x && fhi.y <= hi.y, "inside the painted water");

            // A course at the water's edge: the frame slides back inside rather than showing unpainted space.
            var edge = new List<Vector2> { new(980f, 580f), new(995f, 590f) };
            var (elo, ehi) = VoyageCardRules.Frame(lo, hi, edge, 0.8f);
            Assert.That(ehi.x <= hi.x + 1e-3f && ehi.y <= hi.y + 1e-3f, "kept inside the water at its edge");
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
