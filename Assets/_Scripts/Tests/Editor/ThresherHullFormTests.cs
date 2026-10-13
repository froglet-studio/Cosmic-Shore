using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher's own hull, swim and wrecking ball (<c>R_VesselActions/THRESHER.md</c> §5b): the pure
    /// shark form's parts, element morphs and bounds, the tail posed as a chain, and the morning-star ball.
    /// The offline twin is <c>Tools/Build/thresher_hull_harness</c>, which runs the same forms plus a
    /// parity-ray winding test over every closed piece.
    /// </summary>
    [TestFixture]
    public class ThresherHullFormTests
    {
        static readonly ThresherHullForm.Settings S = ThresherHullForm.Defaults;

        [Test]
        public void Form_PartZeroIsTheBody_AndEveryAnimatedPartExists()
        {
            var parts = ThresherHullForm.Generate(S);
            Assert.AreEqual(ThresherHullForm.PartKind.Core, parts[0].Kind, "part 0 renders on the builder's own (painted) renderer");
            Assert.AreEqual(2, parts.Count(p => p.Kind == ThresherHullForm.PartKind.Pectoral));
            Assert.AreEqual(1, parts.Count(p => p.Kind == ThresherHullForm.PartKind.TailBase));
            Assert.AreEqual(S.LobeSegments, parts.Count(p => p.Kind == ThresherHullForm.PartKind.Lobe));
            Assert.AreEqual(parts.Count, parts.Select(p => p.Name).Distinct().Count(), "child parts are found by name");
        }

        [Test]
        public void Form_TheDomainAccentsAreOnSubmeshOne()
        {
            var parts = ThresherHullForm.Generate(S);
            Assert.Greater(parts[0].Accent.Count, 0, "the dorsal fin and the eyes");
            Assert.Greater(parts.First(p => p.Kind == ThresherHullForm.PartKind.TailBase).Accent.Count, 0, "the hitch");
            Assert.IsTrue(parts.Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).All(p => p.Accent.Count > 0), "the scythe's blade");
            Assert.AreEqual(1, ThresherHullForm.AccentSubmesh, "ShipHelper paints the domain onto slot 1");
        }

        [Test]
        public void Form_SnoutIsForward_TheScytheTrails_AndTheHullIsSquirrelSized()
        {
            var all = ThresherHullForm.Generate(S).SelectMany(p => p.Verts).ToList();
            float snout = all.Max(v => v.z), tail = all.Min(v => v.z);
            Assert.Greater(snout, 0f, "+Z is forward");
            Assert.Less(tail, -S.BodyLength * 0.5f, "the tail trails the body");
            Assert.That(snout - tail, Is.InRange(6f, 9.5f), "snout to scythe tip, world units");
            Assert.That(all.Max(v => v.x) - all.Min(v => v.x), Is.InRange(2.5f, 4.5f), "across the pectorals");
        }

        [Test]
        public void Form_TheScytheIsContinuous_AndAsLongAsAuthored()
        {
            var lobes = ThresherHullForm.Generate(S).Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).ToList();
            for (int k = 1; k < lobes.Count; k++)
                Assert.Less((lobes[k].Pivot - ThresherHullForm.LobePoint(S, k / (float)S.LobeSegments)).magnitude, 1e-4f,
                    $"segment {k} hinges where segment {k - 1} ends");
            float arc = 0f;
            var prev = ThresherHullForm.LobePoint(S, 0f);
            for (int i = 1; i <= 200; i++) { var p = ThresherHullForm.LobePoint(S, i / 200f); arc += (p - prev).magnitude; prev = p; }
            Assert.AreEqual(S.LobeLength, arc, 0.01f * S.LobeLength);
        }

        [Test]
        public void Form_TheHitchHangsUnderTheTailFork()
        {
            var hitch = ThresherHullForm.HitchCenter(S);
            Assert.Less(hitch.y, ThresherHullForm.ForkPoint(S).y);
            Assert.Less(hitch.z, ThresherHullForm.PeduncleZ(S), "behind the body: the chain leaves the tail");
        }

        [Test]
        public void Morphs_EveryElementMovesTheHull_AndTheBoundsHoldEveryCorner()
        {
            var set = ThresherHullForm.BakeMorphSet(S);   // throws if an extreme changed topology
            for (int e = 0; e < ThresherHullForm.MorphElements.Length; e++)
            {
                float travel = 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                    foreach (var d in set.Deltas[e][p].VertDeltas) travel = Mathf.Max(travel, d.magnitude);
                Assert.Greater(travel, 0.05f, $"{ThresherHullForm.MorphElements[e]} visibly moves the hull");
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int corner = 0; corner < 16; corner++)
            {
                var w = new float[4];
                for (int e = 0; e < 4; e++) w[e] = (corner >> e & 1) == 1 ? 1f : 0f;
                for (int p = 0; p < set.BaseParts.Count; p++)
                {
                    ThresherHullForm.BlendPart(set, p, w, verts, normals);
                    var b = new Bounds();
                    b.SetMinMax(set.BoundsMin[p] - Vector3.one * 1e-4f, set.BoundsMax[p] + Vector3.one * 1e-4f);
                    foreach (var v in verts) Assert.IsTrue(b.Contains(v), $"part {p} corner {corner}: {v} outside its baked bounds");
                }
            }
        }

        [Test]
        public void Swim_TheTailChainNeverOpensAJoint()
        {
            var parts = ThresherHullForm.Generate(S);
            var chain = new List<Vector3> { parts.First(p => p.Kind == ThresherHullForm.PartKind.TailBase).Pivot };
            chain.AddRange(parts.Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).Select(p => p.Pivot));
            var rest = chain.ToArray();
            int n = rest.Length;
            var yaws = new float[n];
            var local = new Quaternion[n];
            var pos = new Vector3[n];
            var rot = new Quaternion[n];
            var swim = ThresherSwimMath.Defaults;
            for (int i = 0; i < 72; i++)
            {
                ThresherSwimMath.JointYaws(swim, i * Mathf.PI / 36f, 1f, 0.6f, -0.8f, 1f, 1f, yaws);
                for (int j = 0; j < n; j++) local[j] = Quaternion.Euler(0f, yaws[j], 0f);
                ThresherSwimMath.PoseChain(rest, local, pos, rot);
                for (int k = 0; k + 1 < n; k++)
                    Assert.Less((pos[k] + rot[k] * (rest[k + 1] - rest[k]) - pos[k + 1]).magnitude, 1e-4f, $"joint {k + 1} at step {i}");
            }
        }

        [Test]
        public void Swim_ATurnSwingsTheTailOutOfIt()
        {
            var swim = ThresherSwimMath.Defaults;
            swim.BaseSwingDegrees = 0f;
            swim.LobeSwingDegrees = 0f;
            var yaws = new float[4];
            ThresherSwimMath.JointYaws(swim, 0f, 1f, 1f, 0f, 0f, 1f, yaws);
            Assert.IsTrue(yaws.All(y => y < 0f), "a right turn swings every joint left");
            Assert.AreEqual(-swim.TurnSwingDegrees, yaws.Sum(), 1e-3f, "the tip reaches the authored swing");
        }

        [Test]
        public void Ball_IsACorePlusTwelveSpikes_AtTheGameplayRadius()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            ThresherBallForm.Build(verts, normals, tris);
            Assert.AreEqual(80 + 12 * (ThresherBallForm.SpikeSides * 2 - 2), tris.Count / 3, "80 core facets + 12 capped pyramids");
            Assert.AreEqual(ThresherBallForm.SpikeTipRadius, verts.Max(v => v.magnitude), 1e-4f);
            for (int t = 0; t < 80 * 3; t += 3)
                Assert.AreEqual(ThresherBallForm.CoreRadius, verts[tris[t]].magnitude, 1e-4f, "core facets lie on the gameplay ball");
            for (int t = 0; t < tris.Count; t += 3)
            {
                var c = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f;
                var n = Vector3.Cross(verts[tris[t + 1]] - verts[tris[t]], verts[tris[t + 2]] - verts[tris[t]]);
                if (t < 80 * 3) Assert.Greater(Vector3.Dot(n, c), 0f, "a core facet faces out");
            }
        }
    }
}
