#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for the arcade card's genre petal (<see cref="ModeGenre"/>).
    ///
    /// The rule is EXHAUSTIVE by intent - every scoring metric a mode can be built on says
    /// something about what kind of game it is - but an unclassified metric fails SILENTLY at
    /// runtime (the card simply draws no petal), so the sweep below is what makes adding a
    /// metric without giving it a genre loud instead.
    /// </summary>
    [TestFixture]
    public class ModeGenreTests
    {
        [Test]
        public void EveryScoringMetric_HasAGenre()
        {
            var unclassified = new List<ScoringMetric>();

            foreach (ScoringMetric metric in Enum.GetValues(typeof(ScoringMetric)))
                if (!ModeGenre.TryElementFor(metric, out _))
                    unclassified.Add(metric);

            Assert.IsEmpty(unclassified,
                "ScoringMetric member(s) with no genre: " + string.Join(", ", unclassified) +
                ". A mode scored on one of these draws no genre petal on its arcade card. " +
                "Classify it in ModeGenre - a race is Time, making mass is Mass, destroying " +
                "mass is Space, working other pilots over is Charge.");
        }

        [Test]
        public void EveryGenre_IsOneOfTheFourPlayableElements()
        {
            foreach (ScoringMetric metric in Enum.GetValues(typeof(ScoringMetric)))
            {
                if (!ModeGenre.TryElementFor(metric, out var element)) continue;

                Assert.That(element,
                    Is.EqualTo(Element.Charge)
                      .Or.EqualTo(Element.Mass)
                      .Or.EqualTo(Element.Space)
                      .Or.EqualTo(Element.Time),
                    $"ScoringMetric.{metric} resolves to Element.{element}, which has no petal " +
                    "art in ElementalBarsConfigSO - the card would draw nothing. Only the four " +
                    "playable elements are genres.");
            }
        }

        // The shipped classification, pinned member by member. Not redundant with the sweep
        // above: the sweep proves every metric HAS a genre, these prove it is still the one the
        // cards were designed around - a metric quietly moving from Space to Mass re-labels a
        // shipped card with nothing else to report it.
        [Test]
        [TestCase(ScoringMetric.Crystals,          Element.Time)]   // Scurry - race to collect
        [TestCase(ScoringMetric.OmniCrystals,      Element.Time)]
        [TestCase(ScoringMetric.ElementalCrystals, Element.Time)]
        [TestCase(ScoringMetric.Goals,             Element.Time)]   // Astro League, Scramble, Tollway, Brood Rush
        [TestCase(ScoringMetric.SwitchesThreaded,  Element.Time)]   // Switchback, Headlong, Skein, Regatta, Redline, Breakwater
        [TestCase(ScoringMetric.PrismsRemaining,   Element.Mass)]
        [TestCase(ScoringMetric.PrismsStolen,      Element.Mass)]   // Hijack - nothing is destroyed to score it
        [TestCase(ScoringMetric.PrismsDestroyed,   Element.Space)]  // Rampage, Cleave, Salvo, Wrecking Ball
        [TestCase(ScoringMetric.VolumeDestroyed,   Element.Space)]  // Bloomrush
        [TestCase(ScoringMetric.LifeformsKilled,   Element.Space)]  // Wildlife Liberation
        [TestCase(ScoringMetric.Jousts,            Element.Charge)] // Joust
        [TestCase(ScoringMetric.CombatPoints,      Element.Charge)] // Dog Fight, The Bends, Undertow, Broadside
        public void Metric_ResolvesToItsShippedGenre(ScoringMetric metric, Element expected)
        {
            Assert.IsTrue(ModeGenre.TryElementFor(metric, out var element),
                $"ScoringMetric.{metric} lost its genre.");
            Assert.AreEqual(expected, element,
                $"ScoringMetric.{metric} changed genre - every arcade card scored on it now " +
                "shows a different petal.");
        }

        [Test]
        public void EveryCaseIsCovered_ByTheShippedTable()
        {
            // The TestCase list above must not silently fall behind the enum: if it does, a new
            // metric could be classified in ModeGenre and still never have its genre pinned.
            Assert.AreEqual(12, Enum.GetValues(typeof(ScoringMetric)).Length,
                "ScoringMetric member count changed - add the new metric to ModeGenre and to " +
                "Metric_ResolvesToItsShippedGenre's TestCase list.");
        }
    }
}
#endif
