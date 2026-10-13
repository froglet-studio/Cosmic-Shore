using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Stoat's plated hull and its bounding lope (<c>R_VesselActions/STOAT.md</c> §2): the pure
    /// form's parts, element morphs and bounds, and the lope's pose at the user's tuning. The
    /// offline twin of the form half is <c>Tools/Build/stoat_hull_harness</c>.
    /// </summary>
    [TestFixture]
    public class StoatHullTests
    {
        static readonly StoatHullForm.Settings S = StoatHullForm.Defaults;

        [Test]
        public void Form_PartZeroIsTheCorePlate_AndEveryAnimatedPartExists()
        {
            var parts = StoatHullForm.Generate(S);
            Assert.AreEqual(StoatHullForm.PartKind.Core, parts[0].Kind, "part 0 renders on the builder's own (painted) renderer");
            Assert.AreEqual(S.Segments - 1, parts.Count(p => p.Kind == StoatHullForm.PartKind.Segment));
            Assert.AreEqual(1, parts.Count(p => p.Kind == StoatHullForm.PartKind.Head));
            Assert.AreEqual(S.TailSegments, parts.Count(p => p.Kind == StoatHullForm.PartKind.Tail));
            Assert.AreEqual(1, parts.Count(p => p.Kind == StoatHullForm.PartKind.TailTip));
            Assert.AreEqual(4, parts.Count(p => p.Kind == StoatHullForm.PartKind.Leg));
            Assert.AreEqual(parts.Count, parts.Select(p => p.Name).Distinct().Count(), "child parts are found by name");
        }

        [Test]
        public void Form_TheDomainAccentsAreOnSubmeshOne()
        {
            var parts = StoatHullForm.Generate(S);
            Assert.Greater(parts.First(p => p.Kind == StoatHullForm.PartKind.TailTip).Accent.Count, 0, "the tail crystal");
            Assert.Greater(parts.First(p => p.Kind == StoatHullForm.PartKind.Head).Accent.Count, 0, "the eyes");
            Assert.Greater(parts[0].Accent.Count, 0, "the core plate carries a ridge fin (even plate index)");
            Assert.AreEqual(1, StoatHullForm.AccentSubmesh, "ShipHelper paints the domain onto slot 1");
        }

        [Test]
        public void Form_NoseIsForward_AndTheHullIsSquirrelSized()
        {
            var all = StoatHullForm.Generate(S).SelectMany(p => p.Verts).ToList();
            float nose = all.Max(v => v.z), tail = all.Min(v => v.z);
            Assert.Greater(nose, S.BodyLength * 0.5f, "+Z is forward: the head is ahead of the body");
            Assert.That(nose - tail, Is.InRange(6f, 9f), "nose to tail-crystal, world units");
        }

        [Test]
        public void Morphs_EveryElementMovesTheHull_AndTopologyHolds()
        {
            var set = StoatHullForm.BakeMorphSet(S);   // throws if an extreme changed topology
            for (int e = 0; e < StoatHullForm.MorphElements.Length; e++)
            {
                float travel = 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                    foreach (var d in set.Deltas[e][p].VertDeltas) travel = Mathf.Max(travel, d.magnitude);
                Assert.Greater(travel, 0.05f, $"{StoatHullForm.MorphElements[e]} must visibly move the hull");
            }
        }

        [Test]
        public void Morphs_BlendAtOneWeightIsThatExtreme_AndBoundsHoldEveryCorner()
        {
            var set = StoatHullForm.BakeMorphSet(S);
            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            for (int e = 0; e < 4; e++)
            {
                var extreme = StoatHullForm.Generate(StoatHullForm.ApplyElementExtreme(S, StoatHullForm.MorphElements[e]));
                var w = new float[4]; w[e] = 1f;
                for (int p = 0; p < extreme.Count; p++)
                {
                    StoatHullForm.BlendPart(set, p, w, verts, normals);
                    for (int i = 0; i < verts.Count; i++)
                        Assert.Less((verts[i] - extreme[p].Verts[i]).magnitude, 1e-4f);
                }
            }
            for (int corner = 0; corner < 16; corner++)
            {
                var w = new float[4];
                for (int e = 0; e < 4; e++) w[e] = (corner >> e & 1) == 1 ? 1f : 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    StoatHullForm.BlendPart(set, p, w, verts, normals);
                    foreach (var v in verts)
                    {
                        Assert.GreaterOrEqual(v.x, set.BoundsMin[p].x - 1e-4f); Assert.LessOrEqual(v.x, set.BoundsMax[p].x + 1e-4f);
                        Assert.GreaterOrEqual(v.y, set.BoundsMin[p].y - 1e-4f); Assert.LessOrEqual(v.y, set.BoundsMax[p].y + 1e-4f);
                        Assert.GreaterOrEqual(v.z, set.BoundsMin[p].z - 1e-4f); Assert.LessOrEqual(v.z, set.BoundsMax[p].z + 1e-4f);
                    }
                }
            }
        }

        [Test]
        public void Morphs_ElementsSayWhatTheyMean()
        {
            float Length(StoatHullForm.Settings s) { var a = StoatHullForm.Generate(s).SelectMany(p => p.Verts).ToList(); return a.Max(v => v.z) - a.Min(v => v.z); }
            float Width(StoatHullForm.Settings s) { var a = StoatHullForm.Generate(s).SelectMany(p => p.Verts).ToList(); return a.Max(v => v.x) - a.Min(v => v.x); }
            Assert.Greater(Length(StoatHullForm.ApplyElementExtreme(S, Element.Space)), Length(S) * 1.2f, "Space is reach: the body draws out");
            Assert.Greater(Width(StoatHullForm.ApplyElementExtreme(S, Element.Mass)), Width(S) * 1.2f, "Mass is volume: the plates thicken");
        }

        // ------------------------------------------------------------------ the lope

        [Test]
        public void Lope_TheUserTuningIsTheDefault()
        {
            var d = StoatLopeMath.Defaults;
            Assert.AreEqual(0.66f, d.Rate, 1e-6f);
            Assert.AreEqual(0.45f, d.Arch, 1e-6f);
            Assert.AreEqual(0.2f, d.Squash, 1e-6f);
            Assert.AreEqual(0f, d.SpeedLink, 1e-6f, "the bound does not quicken with speed");
            Assert.AreEqual(1.6f, d.Amplitude, 1e-6f, "viewer amplitude 1.00 at the body's ×1.6 scale");
        }

        [Test]
        public void Lope_OneBoundEveryOneOverRateSeconds()
        {
            var d = StoatLopeMath.Defaults;
            float perSecond = StoatLopeMath.PhaseRate(d, 0f);
            Assert.AreEqual(Mathf.PI * 0.66f, perSecond, 1e-5f);
            // sin² repeats every π of phase: 1/rate seconds.
            Assert.AreEqual(1f / 0.66f, Mathf.PI / perSecond, 1e-4f);
            Assert.AreEqual(perSecond, StoatLopeMath.PhaseRate(d, 1f), 1e-6f, "speed link 0: the same rate at cruise");
        }

        [Test]
        public void Lope_TopOfTheBoundIsHighLongAndArched_LandingIsLowBunchedAndReaching()
        {
            var d = StoatLopeMath.Defaults;
            var top = StoatLopeMath.Evaluate(d, Mathf.PI * 0.5f, 0f);
            var land = StoatLopeMath.Evaluate(d, 0f, 0f);
            Assert.AreEqual(d.Amplitude, top.BodyLift, 1e-5f);
            Assert.AreEqual(0f, land.BodyLift, 1e-5f);
            Assert.Greater(top.Spine, 0f, "arched at the top");
            Assert.Less(land.Spine, 0f, "bunched at the landing");
            Assert.Greater(top.Stretch, 1f);
            Assert.Less(land.Stretch, 1f);
            Assert.AreEqual(1f, land.Legs, 1e-5f, "the legs reach at the landing");
            Assert.AreEqual(0f, top.Legs, 1e-5f);
        }

        [Test]
        public void Lope_RisesNoseUpAndFallsNoseDown()
        {
            var d = StoatLopeMath.Defaults;
            Assert.Less(StoatLopeMath.Evaluate(d, Mathf.PI * 0.25f, 0f).PitchDegrees, 0f, "rising: nose up (negative X)");
            Assert.Greater(StoatLopeMath.Evaluate(d, Mathf.PI * 0.75f, 0f).PitchDegrees, 0f, "falling: nose down");
        }
    }
}
