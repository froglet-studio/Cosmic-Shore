using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// How Riposte's AI reads its arena (<see cref="RiposteLanes"/>, RIPOSTE.md section 3): it picks the nearest rail
    /// nobody has found cut, flies it from its near end to its far end, runs out only to a lane mouth ahead of its
    /// nose, and winds up down the lane until the rival is close.
    /// </summary>
    public class RiposteLanesTests
    {
        static readonly RiposteLanes.Line X = new(new Vector3(100f, 0f, 0f), new Vector3(400f, 0f, 0f));
        static readonly RiposteLanes.Line Y = new(new Vector3(0f, 100f, 0f), new Vector3(0f, 400f, 0f));
        static readonly List<RiposteLanes.Line> Lines = new() { X, Y };

        [Test]
        public void Nearest_IsTheClosestLine_MeasuredToTheSegment()
        {
            Assert.AreEqual(0, RiposteLanes.Nearest(new Vector3(250f, 30f, 0f), Lines));
            Assert.AreEqual(1, RiposteLanes.Nearest(new Vector3(30f, 250f, 0f), Lines));
            Assert.AreEqual(0, RiposteLanes.Nearest(new Vector3(900f, 0f, 0f), Lines), "past its end still counts");
        }

        [Test]
        public void Nearest_SkipsARailFoundCut_AndAnswersNoneWhenAllAre()
        {
            var self = new Vector3(250f, 30f, 0f);
            Assert.AreEqual(1, RiposteLanes.Nearest(self, Lines, new HashSet<int> { 0 }));
            Assert.AreEqual(-1, RiposteLanes.Nearest(self, Lines, new HashSet<int> { 0, 1 }));
            Assert.AreEqual(-1, RiposteLanes.Nearest(self, new List<RiposteLanes.Line>()), "an arena with no rails");
        }

        [Test]
        public void ARailIsFlown_FromItsNearEnd_ToItsFarEnd()
        {
            var inner = new Vector3(120f, 50f, 0f);
            Assert.AreEqual(X.A, RiposteLanes.Entry(inner, X));
            Assert.AreEqual(X.B, RiposteLanes.Exit(inner, X));
            var outer = new Vector3(390f, -50f, 0f);
            Assert.AreEqual(X.B, RiposteLanes.Entry(outer, X));
            Assert.AreEqual(X.A, RiposteLanes.Exit(outer, X), "the whole rail in one straight pass, either way");
        }

        [Test]
        public void LinedUp_IsWithinTheRadiusOfTheLine()
        {
            Assert.IsTrue(RiposteLanes.LinedUp(new Vector3(250f, 30f, 0f), X, 40f));
            Assert.IsFalse(RiposteLanes.LinedUp(new Vector3(250f, 60f, 0f), X, 40f));
            Assert.IsFalse(RiposteLanes.LinedUp(new Vector3(500f, 0f, 0f), X, 40f), "past the end is off the rail");
        }

        [Test]
        public void ARunOut_GoesToALaneMouth_OnlyWhenOneIsAheadOfTheNose()
        {
            var self = new Vector3(50f, 50f, 0f);
            Assert.AreEqual(0, RiposteLanes.MouthAhead(self, Vector3.right, Lines, 60f, 100f));
            Assert.AreEqual(1, RiposteLanes.MouthAhead(self, Vector3.up, Lines, 60f, 100f));
            Assert.AreEqual(-1, RiposteLanes.MouthAhead(self, Vector3.back, Lines, 60f, 100f),
                            "neither mouth is ahead: run straight out, never turn hard for a lane");
        }

        [Test]
        public void ARunOut_IgnoresAMouthItIsAlreadyAt()
        {
            var atMouth = X.B + new Vector3(-20f, 0f, 0f);
            Assert.AreEqual(-1, RiposteLanes.MouthAhead(atMouth, Vector3.right, new List<RiposteLanes.Line> { X }, 60f, 100f));
        }

        [Test]
        public void TheWindUp_HoldsTheLane_UntilTheRivalIsCloseOrTheCentreIsReached()
        {
            const float centre = 100f, breakDistance = 320f;
            Assert.IsTrue(RiposteLanes.RunIn(350f, centre, 600f, breakDistance), "far rival, still in the lane");
            Assert.IsFalse(RiposteLanes.RunIn(350f, centre, 300f, breakDistance), "the rival is close: turn onto it");
            Assert.IsFalse(RiposteLanes.RunIn(90f, centre, 600f, breakDistance), "the lane is flown: it ends at the clearing");
        }
    }
}
