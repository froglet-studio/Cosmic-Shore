#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for the arcade card's genre petal (<see cref="ModeGenre"/>).
    ///
    /// The rule is EXHAUSTIVE by intent - every mode says something about what kind of game it
    /// is - but a mode with no genre fails SILENTLY at runtime (the card simply draws no petal),
    /// so the sweeps below are what make adding a metric, or a card, without giving it a genre
    /// loud instead.
    /// </summary>
    [TestFixture]
    public class ModeGenreTests
    {
        const string ArcadeRoster = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset";
        const string ArenaRoster = "Assets/_SO_Assets/Games/GameLists/ArenaGames.asset";

        // The one card with no genre, and correctly so: Maelstrom draws OTHER modes, so it has
        // no kind of its own to advertise.
        static readonly HashSet<GameModes> Genreless = new() { GameModes.Maelstrom };

        [Test]
        public void EveryScoringMetric_HasAGenre()
        {
            var unclassified = new List<ScoringMetric>();

            foreach (ScoringMetric metric in Enum.GetValues(typeof(ScoringMetric)))
                if (!ModeGenre.TryElementForMetric(metric, out _))
                    unclassified.Add(metric);

            Assert.IsEmpty(unclassified,
                "ScoringMetric member(s) with no genre: " + string.Join(", ", unclassified) +
                ". A mode scored on one of these, and with no row of its own in ModeGenre, draws " +
                "no genre petal on its arcade card. Classify it - a race is Time, making or " +
                "taking mass is Mass, destroying mass is Space, working other pilots over is " +
                "Charge.");
        }

        [Test]
        public void EveryGenre_IsOneOfTheFourPlayableElements()
        {
            foreach (ScoringMetric metric in Enum.GetValues(typeof(ScoringMetric)))
            {
                if (!ModeGenre.TryElementForMetric(metric, out var element)) continue;
                AssertPlayable(element, $"ScoringMetric.{metric}");
            }

            foreach (GameModes mode in Enum.GetValues(typeof(GameModes)))
            {
                // Ask with no metric, so this only exercises the mode table's own rows.
                if (!ModeGenre.TryElementsFor(mode, null, out var first, out var second)) continue;
                AssertPlayable(first, $"GameModes.{mode} (primary)");
                if (second != Element.None) AssertPlayable(second, $"GameModes.{mode} (secondary)");
            }
        }

        static void AssertPlayable(Element element, string what)
        {
            Assert.That(element,
                Is.EqualTo(Element.Charge)
                  .Or.EqualTo(Element.Mass)
                  .Or.EqualTo(Element.Space)
                  .Or.EqualTo(Element.Time),
                $"{what} resolves to Element.{element}, which has no petal art in " +
                "ElementalBarsConfigSO - the card would draw nothing. Only the four playable " +
                "elements are genres.");
        }

        // The shipped FALLBACK, pinned member by member. Not redundant with the sweep above: the
        // sweep proves every metric HAS a genre, these prove it is still the one the cards were
        // designed around - a metric quietly moving from Space to Mass re-labels every shipped
        // card that has no row of its own, with nothing else to report it.
        [Test]
        [TestCase(ScoringMetric.Crystals,          Element.Time)]   // Skim Race
        [TestCase(ScoringMetric.OmniCrystals,      Element.Time)]
        [TestCase(ScoringMetric.ElementalCrystals, Element.Time)]
        [TestCase(ScoringMetric.Goals,             Element.Time)]   // Astro League, Scarab Scramble
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
            Assert.IsTrue(ModeGenre.TryElementForMetric(metric, out var element),
                $"ScoringMetric.{metric} lost its genre.");
            Assert.AreEqual(expected, element,
                $"ScoringMetric.{metric} changed genre - every arcade card scored on it, and " +
                "with no row of its own, now shows a different petal.");
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

        // The MODE table's own rows - the three cards whose genre their metric gets wrong, and
        // which are therefore the entire reason ModeGenre is keyed on the mode at all. Each is
        // asked with the metric its shipped preview definition carries, so the test fails if a
        // row is deleted and the card silently falls back.
        [Test]
        [TestCase(GameModes.Scurry,    ScoringMetric.Crystals, Element.Mass,  Element.None)]
        [TestCase(GameModes.Tollway,   ScoringMetric.Goals,    Element.Mass,  Element.None)]
        [TestCase(GameModes.BroodRush, ScoringMetric.Goals,    Element.Mass,  Element.Space)]
        public void Mode_OverridesItsMetricsGenre(GameModes mode, ScoringMetric metric,
                                                  Element expectedPrimary, Element expectedSecondary)
        {
            Assert.IsTrue(ModeGenre.TryElementsFor(mode, metric, out var first, out var second),
                $"GameModes.{mode} lost its genre.");
            Assert.AreEqual(expectedPrimary, first,
                $"GameModes.{mode}'s primary genre changed - its card now shows a different petal.");
            Assert.AreEqual(expectedSecondary, second,
                $"GameModes.{mode}'s secondary genre changed.");

            // And the point of the row: the metric alone would have said something else.
            ModeGenre.TryElementForMetric(metric, out var fromMetric);
            Assert.AreNotEqual(fromMetric, first,
                $"GameModes.{mode} now agrees with its metric, so its row in ModeGenre is dead " +
                "weight - delete the row, or the row is no longer saying what it was added to say.");
        }

        [Test]
        public void ASecondGenre_IsNeverTheFirstOneTwice()
        {
            foreach (GameModes mode in Enum.GetValues(typeof(GameModes)))
            {
                if (!ModeGenre.TryElementsFor(mode, null, out var first, out var second)) continue;
                if (second == Element.None) continue;

                Assert.AreNotEqual(first, second,
                    $"GameModes.{mode} resolves to the same element twice - its card would draw " +
                    "one petal under another, which reads as a rendering fault rather than as a " +
                    "genre.");
            }
        }

        [Test]
        public void NoMode_HasAGenreWithNoMetricAndNoRow()
        {
            // A null metric means "this mode has no preview definition to read". Nothing may
            // claim a genre out of thin air there - the fallback has to have been consulted.
            foreach (GameModes mode in Enum.GetValues(typeof(GameModes)))
            {
                if (!ModeGenre.TryElementsFor(mode, null, out _, out _)) continue;

                Assert.That(mode,
                    Is.EqualTo(GameModes.Scurry)
                      .Or.EqualTo(GameModes.Tollway)
                      .Or.EqualTo(GameModes.BroodRush),
                    $"GameModes.{mode} answers with no metric, so it has a row in ModeGenre that " +
                    "is not in this list. Add it here and to Mode_OverridesItsMetricsGenre, so a " +
                    "shipped card's genre is pinned rather than merely present.");
            }
        }

        // The end-to-end guard, and the one that catches the failure nobody would notice: a card
        // added to a roster with no preview definition, or a definition whose metric has no
        // genre, simply draws no petal. This asks the SHIPPED rosters and the SHIPPED preview
        // library exactly what GameCard.ResolveGenrePetals asks them.
        [Test]
        public void EveryShippedCard_ResolvesAGenre()
        {
            var library = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            Assert.IsNotNull(library,
                $"Resources/{ModePreviewLibrarySO.ResourcePath} is missing - every card's genre " +
                "fallback reads it.");

            var genreless = new List<string>();

            foreach (var rosterPath in new[] { ArcadeRoster, ArenaRoster })
            {
                var roster = AssetDatabase.LoadAssetAtPath<SO_GameList>(rosterPath);
                Assert.IsNotNull(roster, $"missing roster asset: {rosterPath}");

                foreach (var game in roster.Games)
                {
                    if (!game) continue;
                    if (Genreless.Contains(game.Mode)) continue;

                    var definition = library.Resolve(game.Mode);
                    ScoringMetric? metric = definition ? definition.ObjectiveMetric : (ScoringMetric?)null;

                    if (!ModeGenre.TryElementsFor(game.Mode, metric, out _, out _))
                        genreless.Add($"{game.Mode}" +
                                      (definition ? $" (metric {definition.ObjectiveMetric})"
                                                  : " (no preview definition)"));
                }
            }

            Assert.IsEmpty(genreless,
                "Shipped card(s) that draw NO genre petal: " + string.Join(", ", genreless) +
                ". Either give the mode a row in ModeGenre, or give it a ModePreviewDefinitionSO " +
                "whose ObjectiveMetric has one.");
        }
    }
}
#endif
