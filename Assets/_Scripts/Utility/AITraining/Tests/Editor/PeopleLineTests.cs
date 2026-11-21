#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.AITraining.Tests
{
    /// <summary>
    /// <see cref="PeopleLine"/> and the PeopleLine fitness kind: the term that makes the trainer prefer the AI that
    /// flies where people fly (Shombith, 2026-11-01). Pins the nearest-segment distance, the flat-list loading the
    /// seed file uses, and that with no line the term is exactly 0 so every profile may carry it.
    /// </summary>
    public class PeopleLineTests
    {
        [TearDown]
        public void TearDown() => PeopleLine.Clear();

        [Test]
        public void NoLine_IsNoTerm()
        {
            PeopleLine.Clear();
            Assert.IsFalse(PeopleLine.HasLines);
            Assert.AreEqual(0f, PeopleLine.Distance(new Vector3(100, 100, 100)));
            var f = FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.PeopleLine, "PeopleLine");
            Assert.IsNotNull(f, "the factory makes the PeopleLine kind");
            var ctx = new DecisionContext { Position = new Vector3(50, 0, 0), EpisodeTime = 0f };
            f.OnEpisodeStart(ctx);
            for (int i = 0; i < 10; i++) { ctx.EpisodeTime = i * 0.5f; f.OnFrame(ctx); }
            Assert.AreEqual(0f, f.Evaluate(ctx, null), "no line loaded: the term is 0, not a penalty");
        }

        [Test]
        public void Distance_IsToTheNearestSegment_OfAnyLine()
        {
            // two lines from one flat list: the x axis 0..100, and y = 50 from x 0..100
            var pts = new List<Vector3> { new(0, 0, 0), new(100, 0, 0), new(0, 50, 0), new(100, 50, 0) };
            PeopleLine.Set(pts, new List<int> { 0, 2 });
            Assert.AreEqual(2, PeopleLine.LineCount);
            Assert.AreEqual(10f, PeopleLine.Distance(new Vector3(40, 10, 0)), 1e-4f, "nearest is the x axis");
            Assert.AreEqual(5f, PeopleLine.Distance(new Vector3(40, 45, 0)), 1e-4f, "nearest is the y = 50 line");
            Assert.AreEqual(Mathf.Sqrt(20f * 20f + 0f), PeopleLine.Distance(new Vector3(120, 0, 0)), 1e-4f, "past the end: to the endpoint");
        }

        [Test]
        public void Fitness_IsMinusTheMeanDistance_SampledByEpisodeTime()
        {
            PeopleLine.Set(new List<Vector3> { new(0, 0, 0), new(1000, 0, 0) }, new List<int> { 0 });
            var f = FitnessComponentFactory.Create(FitnessProfileSO.ComponentKind.PeopleLine, "PeopleLine");
            var ctx = new DecisionContext { Position = new Vector3(0, 10, 0), EpisodeTime = 0f };
            f.OnEpisodeStart(ctx);
            f.OnFrame(ctx);                                   // t = 0: 10 off
            ctx.EpisodeTime = 0.1f; ctx.Position = new Vector3(10, 30, 0); f.OnFrame(ctx);   // inside the 0.25 s window: not sampled
            ctx.EpisodeTime = 0.3f; ctx.Position = new Vector3(20, 30, 0); f.OnFrame(ctx);   // 30 off
            Assert.AreEqual(-20f, f.Evaluate(ctx, null), 1e-4f, "the mean of the two samples, negated");
        }

        [Test]
        public void Set_SkipsALineOfOnePoint_AndTakesAnImplicitFirstStart()
        {
            PeopleLine.Set(new List<Vector3> { new(0, 0, 0), new(5, 0, 0), new(9, 9, 9) }, new List<int> { 2 });
            Assert.AreEqual(1, PeopleLine.LineCount, "the first line starts at 0 without being listed; the one-point line is dropped");
        }
    }
}
#endif
