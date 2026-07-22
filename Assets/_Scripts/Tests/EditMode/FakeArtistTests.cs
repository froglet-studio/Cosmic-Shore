#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pure-logic tests for the Fake Artist minigame's two deterministic cores:
    /// <see cref="FakeArtistArtworkBuilder"/> (the server generates gameplay data from it
    /// and every client regenerates the identical artwork at reveal - byte-equal results
    /// are load-bearing) and <see cref="FakeArtistScorer"/> (the vote tally that encodes
    /// the game's point rules).
    /// </summary>
    public class FakeArtistArtworkBuilderTests
    {
        [TestCase(3, 3)]
        [TestCase(5, 3)]
        [TestCase(12, 3)]
        [TestCase(12, 1)]
        public void BuildStrokes_ProducesExactStrokeCount(int playerCount, int strokesPerPlayer)
        {
            int target = playerCount * strokesPerPlayer;
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Saturn, 600f, 12345, target);
            Assert.AreEqual(target, strokes.Count);
        }

        [Test]
        public void BuildStrokes_EveryStrokeIsFlyable()
        {
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Phoenix, 600f, 777, 36);
            foreach (var stroke in strokes)
            {
                Assert.GreaterOrEqual(stroke.points.Count, 2, $"{stroke.name} has too few points");
                Assert.Greater(FakeArtistArtworkBuilder.ArcLength(stroke.points), 0f, $"{stroke.name} has zero length");
            }
        }

        [Test]
        public void BuildStrokes_IsDeterministic()
        {
            var a = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.TorusKnot, 600f, 42, 27);
            var b = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.TorusKnot, 600f, 42, 27);

            Assert.AreEqual(a.Count, b.Count);
            for (int s = 0; s < a.Count; s++)
            {
                Assert.AreEqual(a[s].points.Count, b[s].points.Count, $"stroke {s} point count");
                for (int i = 0; i < a[s].points.Count; i++)
                    Assert.That((a[s].points[i] - b[s].points[i]).sqrMagnitude, Is.LessThan(1e-8f),
                        $"stroke {s} point {i} differs");
            }
        }

        [Test]
        public void BuildStrokes_DifferentSeedsDiffer()
        {
            var a = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Star, 600f, 1, 9);
            var b = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Star, 600f, 2, 9);

            bool anyDifferent = false;
            for (int s = 0; s < a.Count && !anyDifferent; s++)
            {
                if (a[s].points.Count != b[s].points.Count) { anyDifferent = true; break; }
                for (int i = 0; i < a[s].points.Count; i++)
                {
                    if ((a[s].points[i] - b[s].points[i]).sqrMagnitude > 1e-4f) { anyDifferent = true; break; }
                }
            }
            Assert.IsTrue(anyDifferent, "two seeds produced identical artwork - parametric variation is dead");
        }

        [Test]
        public void Deal_GivesEveryPlayerTheFullBundle()
        {
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Lotus, 600f, 5, 12 * 3);
            var bundles = FakeArtistArtworkBuilder.Deal(strokes, 12, 3);

            Assert.AreEqual(12, bundles.Length);
            var seen = new HashSet<PaintingStroke>();
            foreach (var bundle in bundles)
            {
                Assert.AreEqual(3, bundle.Count);
                foreach (var stroke in bundle)
                    Assert.IsTrue(seen.Add(stroke), "stroke dealt twice");
            }
            Assert.AreEqual(36, seen.Count);
        }

        [Test]
        public void BuildDots_AlwaysIncludesEndpoints()
        {
            var strokes = FakeArtistArtworkBuilder.BuildStrokes(PaintingPreset.Rose, 600f, 9, 18);
            foreach (var stroke in strokes)
            {
                var dots = FakeArtistArtworkBuilder.BuildDots(stroke.points, 24f);
                Assert.GreaterOrEqual(dots.Count, 2, $"{stroke.name}: fewer than 2 dots");
                Assert.That((dots[0] - stroke.points[0]).sqrMagnitude, Is.LessThan(1e-6f));
                Assert.That((dots[^1] - stroke.points[^1]).sqrMagnitude, Is.LessThan(1e-6f));
            }
        }

        [Test]
        public void BuildSubjectChoices_ContainsCorrectAndDistinctDecoys()
        {
            var choices = FakeArtistArtworkBuilder.BuildSubjectChoices(PaintingPreset.Peacock, 31337, 4);
            Assert.AreEqual(4, choices.Count);
            Assert.Contains(PaintingPreset.Peacock, choices);
            Assert.AreEqual(choices.Count, choices.Distinct().Count(), "duplicate subject options");
        }

        [Test]
        public void BuildSubjectChoices_CorrectAnswerPositionVaries()
        {
            // Across many seeds the correct option must not always land at index 0
            // (its position would leak the answer).
            var positions = new HashSet<int>();
            for (int seed = 0; seed < 30; seed++)
            {
                var choices = FakeArtistArtworkBuilder.BuildSubjectChoices(PaintingPreset.Star, seed, 4);
                positions.Add(choices.IndexOf(PaintingPreset.Star));
            }
            Assert.Greater(positions.Count, 1, "correct subject always shuffles to the same slot");
        }

        [Test]
        public void AnchorForRound_FirstCanvasIsOnTheSpawnCluster()
        {
            // Round 0 sits at the origin (right where players spawn) so nobody flies far
            // to reach their first strokes.
            FakeArtistArtworkBuilder.AnchorForRound(0, 600f, out var a, out _);
            Assert.That(a.magnitude, Is.LessThan(1f), "round 0 canvas should be on the spawn cluster");
        }

        [Test]
        public void AnchorForRound_SuccessiveRoundsSpreadOut()
        {
            // Later canvases spiral outward so the gallery accumulates without overlap,
            // but stay within easy flying distance (not the old far ring).
            FakeArtistArtworkBuilder.AnchorForRound(1, 600f, out var b, out _);
            FakeArtistArtworkBuilder.AnchorForRound(4, 600f, out var e, out _);
            Assert.Greater(b.magnitude, 1f, "round 1 canvas is offset from the origin");
            Assert.Greater(e.magnitude, b.magnitude, "later rounds spiral further out");
        }

        [Test]
        public void DirectionChanges_StraightLineIsZero_ZigzagIsPositive()
        {
            var straight = new List<Vector3>();
            for (int i = 0; i < 10; i++) straight.Add(new Vector3(i * 10f, 0f, 0f));
            Assert.AreEqual(0, FakeArtistArtworkBuilder.DirectionChanges(straight));

            var zigzag = new List<Vector3>
            {
                new(0, 0, 0), new(10, 0, 10), new(20, 0, 0), new(30, 0, 10), new(40, 0, 0), new(50, 0, 10),
            };
            Assert.Greater(FakeArtistArtworkBuilder.DirectionChanges(zigzag), 0,
                "a zigzag reverses turn direction repeatedly");
        }
    }

    public class FakeArtistScorerTests
    {
        static readonly FakeArtistScorer.Config SpecConfig = new(
            correctSubjectPoints: 1, correctImposterPoints: 1, guessedPenalty: -1, imposterReward: 4);

        [Test]
        public void ImposterAlwaysEarnsReward()
        {
            var deltas = FakeArtistScorer.ScoreRound(4, new[] { 2 }, correctSubjectChoice: 0,
                new Dictionary<int, FakeArtistScorer.Answers>(), SpecConfig);
            Assert.AreEqual(4, deltas[2]);
        }

        [Test]
        public void CorrectSubjectAndAccusationEachScoreOnePoint()
        {
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                // voter 0: right subject, right accusation
                [0] = new(subjectChoice: 1, accusedIndex: 3),
                // voter 1: wrong subject, wrong accusation
                [1] = new(subjectChoice: 0, accusedIndex: 2),
            };
            var deltas = FakeArtistScorer.ScoreRound(4, new[] { 3 }, correctSubjectChoice: 1, answers, SpecConfig);

            Assert.AreEqual(2, deltas[0], "+1 subject, +1 accusation");
            Assert.AreEqual(0, deltas[1], "both answers wrong, nobody accused voter 1");
            Assert.AreEqual(-1, deltas[2], "wrongly accused player takes the penalty");
            Assert.AreEqual(4 - 1, deltas[3], "caught imposter: +4 reward -1 accused penalty");
        }

        [Test]
        public void GuessedPenaltyAppliesOncePerRoundRegardlessOfAccuserCount()
        {
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [0] = new(-1, 3),
                [1] = new(-1, 3),
                [2] = new(-1, 3),
            };
            var deltas = FakeArtistScorer.ScoreRound(5, new[] { 4 }, correctSubjectChoice: 0, answers, SpecConfig);
            Assert.AreEqual(-1, deltas[3], "penalty is flat, not per accusing voter");
        }

        [Test]
        public void UnansweredQuestionsScoreNothing()
        {
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [0] = new(-1, -1),
            };
            var deltas = FakeArtistScorer.ScoreRound(3, new[] { 2 }, correctSubjectChoice: 0, answers, SpecConfig);
            Assert.AreEqual(0, deltas[0]);
            Assert.AreEqual(0, deltas[1]);
        }

        [Test]
        public void ImposterVoteIsIgnoredEvenIfPresent()
        {
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [2] = new(0, 0), // the imposter tries to vote
            };
            var deltas = FakeArtistScorer.ScoreRound(3, new[] { 2 }, correctSubjectChoice: 0, answers, SpecConfig);
            Assert.AreEqual(0, deltas[0], "imposter's accusation must not land a penalty");
            Assert.AreEqual(4, deltas[2], "imposter earns only the reward");
        }

        [Test]
        public void SelfAccusationIsIgnored()
        {
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [0] = new(-1, 0), // voter accuses themselves (client UI forbids it; defend anyway)
            };
            var deltas = FakeArtistScorer.ScoreRound(3, new[] { 2 }, correctSubjectChoice: 0, answers, SpecConfig);
            Assert.AreEqual(0, deltas[0]);
        }

        [Test]
        public void NegativeTotalsArePossible()
        {
            // A player who scores nothing and gets accused ends the round negative -
            // the spec allows negative running totals.
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [0] = new(-1, 1),
            };
            var deltas = FakeArtistScorer.ScoreRound(3, new[] { 2 }, correctSubjectChoice: 0, answers, SpecConfig);
            Assert.AreEqual(-1, deltas[1]);
        }

        [Test]
        public void TwoImposters_BothRewarded_AccusingEitherScores()
        {
            // Players 4 and 5 are both fake artists (large-group round).
            var answers = new Dictionary<int, FakeArtistScorer.Answers>
            {
                [0] = new(subjectChoice: -1, accusedIndex: 4), // correct accusation of imposter 4
                [1] = new(subjectChoice: -1, accusedIndex: 5), // correct accusation of imposter 5
                [2] = new(subjectChoice: -1, accusedIndex: 0), // wrong accusation of player 0
            };
            var deltas = FakeArtistScorer.ScoreRound(6, new[] { 4, 5 }, correctSubjectChoice: 0, answers, SpecConfig);

            Assert.AreEqual(0, deltas[0], "+1 correct accusation, but accused by voter 2 => net 0");
            Assert.AreEqual(1, deltas[1], "+1 correct accusation, not accused");
            Assert.AreEqual(0, deltas[2], "wrong accusation, not accused");
            Assert.AreEqual(4 - 1, deltas[4], "imposter 4: +reward, accused => -1");
            Assert.AreEqual(4 - 1, deltas[5], "imposter 5: +reward, accused => -1");
        }
    }
}
#endif
