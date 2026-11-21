using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher's own hull, tail and wrecking ball (<c>R_VesselActions/THRESHER.md</c> §5b): the pure
    /// shark form's parts, element morphs and bounds, the tail hanging along its cord as a chain of springs,
    /// and the morning-star ball.
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
        public void Tail_TheChainNeverOpensAJoint()
        {
            var parts = ThresherHullForm.Generate(S);
            var chain = new List<Vector3> { parts.First(p => p.Kind == ThresherHullForm.PartKind.TailBase).Pivot };
            chain.AddRange(parts.Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).Select(p => p.Pivot));
            var rest = chain.ToArray();
            int n = rest.Length;
            var rot = new Quaternion[n];
            var pos = new Vector3[n];
            for (int i = 0; i < 72; i++)
            {
                for (int j = 0; j < n; j++)
                    rot[j] = ThresherSwimMath.JointRotation(new ThresherSwimMath.Joint { Yaw = 90f * Mathf.Sin(i * 0.3f + j), Pitch = 40f * Mathf.Cos(i * 0.2f - j) });
                ThresherSwimMath.PoseChain(rest, rot, pos);
                for (int k = 0; k + 1 < n; k++)
                    Assert.Less((pos[k] + rot[k] * (rest[k + 1] - rest[k]) - pos[k + 1]).magnitude, 1e-4f, $"joint {k + 1} at step {i}");
            }
        }

        // Garrett, 2026-10-19: "remove the tail wiggle when flying". Nothing in the tail is a function of time on
        // its own: with the cord straight behind and the hull flying straight, it never moves off its rest.
        [Test]
        public void Tail_FlyingStraight_NeverWiggles()
        {
            var tail = ThresherSwimMath.Defaults;
            var joints = new ThresherSwimMath.Joint[4];
            var yawT = new float[4];
            var pitchT = new float[4];
            ThresherSwimMath.CordBend(tail, Vector3.back * 6f, Vector3.back * 12f, 6f, out float bendYaw, out float bendPitch);
            ThresherSwimMath.JointTargets(bendYaw, bendPitch, yawT, pitchT);
            for (int f = 0; f < 600; f++)
            {
                ThresherSwimMath.Step(tail, joints, yawT, pitchT, 0f, 0f, 0f, 1f / 60f);
                foreach (var j in joints)
                    Assert.AreEqual(0f, Mathf.Abs(j.Yaw) + Mathf.Abs(j.Pitch), 1e-5f, $"frame {f}");
            }
        }

        // "...as though it is the last links of the cord": a cord leaving to the pilot's right lays the tail along
        // it - every joint swings right (negative yaw in Unity's convention), the tip by the whole bend - and the
        // same pose is reached at a phone's 30 fps and a PC's 144.
        [Test]
        public void Tail_SettlesAlongTheCord_AtAnyFrameRate()
        {
            var tail = ThresherSwimMath.Defaults;
            Vector3 toCord = new Vector3(8f, 0f, -8f);   // back and to the right, 45 degrees off the rest line
            float follow = ThresherSwimMath.CordBend(tail, Vector3.back * 6f, toCord, 6f, out float bendYaw, out float bendPitch);
            Assert.AreEqual(1f, follow, 1e-4f, "a cord behind the hull is followed fully");
            Assert.AreEqual(-45f, bendYaw, 1e-3f, "the bend is the cord's angle off the rest line, to the right");
            Assert.AreEqual(0f, bendPitch, 1e-3f);

            float Settle(float fps)
            {
                var joints = new ThresherSwimMath.Joint[4];
                var yawT = new float[4];
                var pitchT = new float[4];
                ThresherSwimMath.JointTargets(bendYaw, bendPitch, yawT, pitchT);
                for (int f = 0; f < Mathf.RoundToInt(4f * fps); f++)
                    ThresherSwimMath.Step(tail, joints, yawT, pitchT, 0f, 0f, 0f, 1f / fps);
                Assert.IsTrue(joints.All(j => j.Yaw < 0f), "every joint swings toward the cord");
                return joints[3].Yaw;
            }
            float phone = Settle(30f), pc = Settle(144f);
            Assert.AreEqual(bendYaw, phone, 0.5f, "the tip lies along the cord (30 fps)");
            Assert.AreEqual(pc, phone, 0.5f, "30 fps and 144 fps settle to the same pose");
        }

        // A cord swinging round AHEAD of the hull is where the side of the bend would flip; the tail stops
        // following it there instead of snapping from one side to the other.
        [Test]
        public void Tail_ACordAhead_IsNotFollowed()
        {
            var tail = ThresherSwimMath.Defaults;
            float follow = ThresherSwimMath.CordBend(tail, Vector3.back * 6f, new Vector3(0.01f, 0f, 12f), 6f, out float y, out float p);
            Assert.AreEqual(0f, follow, 1e-6f);
            Assert.AreEqual(0f, y, 1e-6f);
            Assert.AreEqual(0f, p, 1e-6f);
            ThresherSwimMath.CordBend(tail, Vector3.back * 6f, new Vector3(-0.01f, 0f, 12f), 6f, out float y2, out _);
            Assert.AreEqual(y, y2, 1e-6f, "either side of dead ahead poses the same tail");
        }

        // The sway: the hull turning right leaves the tail pointing where it was (it lags, to the right of the
        // hull's back) and its spring brings it round, overshooting a little, then it settles straight.
        [Test]
        public void Tail_AHullTurnSwaysTheTail_ThenItSettles()
        {
            var tail = ThresherSwimMath.Defaults;
            var joints = new ThresherSwimMath.Joint[4];
            var zero = new float[4];
            const float dt = 1f / 60f;
            for (int f = 0; f < 30; f++)   // half a second at the hull's 120 deg/s
                ThresherSwimMath.Step(tail, joints, zero, zero, 120f * dt, 0f, 0f, dt);
            Assert.Less(joints[3].Yaw, -5f, "a right turn leaves the tip lagging to the hull's right");
            Assert.Less(joints[3].Yaw, joints[0].Yaw, "the tip lags more than the root");
            float overshoot = 0f;
            for (int f = 0; f < 300; f++)
            {
                ThresherSwimMath.Step(tail, joints, zero, zero, 0f, 0f, 0f, dt);
                overshoot = Mathf.Max(overshoot, joints[3].Yaw);
            }
            Assert.Greater(overshoot, 0.5f, "it sways past straight once the turn stops (damping under 1)");
            Assert.AreEqual(0f, joints[3].Yaw, 0.05f, "and settles straight");
        }

        // A hitch, a pause or a respawn hands the springs one huge frame; they stay bounded.
        [Test]
        public void Tail_AHugeFrame_StaysBounded()
        {
            var tail = ThresherSwimMath.Defaults;
            var joints = new ThresherSwimMath.Joint[4];
            var target = new[] { -30f, -60f, -90f, -110f };
            var zero = new float[4];
            ThresherSwimMath.Step(tail, joints, target, zero, 180f, -90f, 5000f, 3f);
            foreach (var j in joints)
            {
                Assert.LessOrEqual(Mathf.Abs(j.Yaw), tail.MaxYawDegrees + ThresherSwimMath.OvershootDegrees + 1e-3f);
                Assert.LessOrEqual(Mathf.Abs(j.Pitch), tail.MaxPitchDegrees + ThresherSwimMath.OvershootDegrees + 1e-3f);
                Assert.IsFalse(float.IsNaN(j.Yaw) || float.IsNaN(j.Pitch));
            }
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
