using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Spearline's seals (SPEARLINE.md): the ladder, the disc in the mouth, and the hole rule against a model of what a
    /// Swordfish cuts on the way through (<see cref="SpearlineSeals.ModelHullRadius"/>,
    /// <see cref="SpearlineSeals.ModelStrokeHalfWidth"/>): the hull alone never opens a seal; at I1 one stroke to a side
    /// does; at I2-I3 a stroke to each side; at I4 both sides and the bill stood up.
    /// </summary>
    public class SpearlineSealsTests
    {
        static readonly int[] Rungs = { 1, 2, 3, 4 };

        static RaceGate Gate(float radius) => new(new Vector3(100f, -40f, 700f), new Vector3(0.3f, 0.2f, 1f).normalized, radius);

        static float Share(int intensity, System.Func<float, float, bool> cut)
        {
            var gate = Gate(HeadlongCircuitSettings.ForIntensity(intensity).RingRadius);
            var at = new List<Vector3>();
            SpearlineSeals.Lay(gate, at);
            SpearlineSeals.Basis(gate.Axis, out var u, out var v);
            Vector3 crossing = gate.Position;
            return SpearlineSeals.OpenShare(gate, crossing, at, i =>
            {
                Vector3 d = at[i] - crossing;
                return !cut(Vector3.Dot(d, u), Vector3.Dot(d, v));
            });
        }

        static float Reach => SpearlineSeals.ModelStrokeHalfWidth + 0.5f * SpearlineSeals.PrismSize;
        static bool Hull(float x, float y) =>
            x * x + y * y <= Sq(SpearlineSeals.ModelHullRadius + 0.5f * SpearlineSeals.PrismSize);
        static bool HalfStroke(float x, float y) => Mathf.Abs(y) <= Reach && x >= 0f;
        static bool FullStroke(float x, float y) => Mathf.Abs(y) <= Reach;
        static bool Stood(float x, float y) => Mathf.Abs(x) <= Reach;
        static float Sq(float a) => a * a;

        [Test]
        public void Ladder_IsOrderedTowardHarder()
        {
            for (int i = 2; i <= 4; i++)
            {
                var a = SpearlineSeals.ForIntensity(i - 1);
                var b = SpearlineSeals.ForIntensity(i);
                Assert.GreaterOrEqual(b.OpenFraction, a.OpenFraction, $"I{i} opens easier than I{i - 1}");
                Assert.LessOrEqual(b.RegrowSeconds, a.RegrowSeconds, $"I{i} regrows slower than I{i - 1}");
                Assert.GreaterOrEqual(SealedOf8(b), SealedOf8(a), $"I{i} seals fewer rings than I{i - 1}");
                Assert.GreaterOrEqual(ShieldedOf8(b), ShieldedOf8(a), $"I{i} shields fewer seals than I{i - 1}");
            }
        }

        static int SealedOf8(SpearlineSealSettings s) { int n = 0; for (int r = 0; r < 8; r++) if (SpearlineSeals.IsSealed(r, s)) n++; return n; }
        static int ShieldedOf8(SpearlineSealSettings s) { int n = 0; for (int r = 0; r < 8; r++) if (SpearlineSeals.IsShielded(r, s)) n++; return n; }

        [Test]
        public void RingZero_IsNeverSealed_AndShieldsOnlyOnSeals()
        {
            foreach (int i in Rungs)
            {
                var s = SpearlineSeals.ForIntensity(i);
                Assert.IsFalse(SpearlineSeals.IsSealed(0, s), $"I{i}: the start ring is sealed");
                for (int r = 0; r < 24; r++)
                    if (SpearlineSeals.IsShielded(r, s)) Assert.IsTrue(SpearlineSeals.IsSealed(r, s));
                Assert.AreEqual(i >= 4 ? PrismKind.Shielded : PrismKind.Plain, SpearlineSeals.KindFor(1, s));
            }
        }

        [Test]
        public void Seal_FillsTheMouth_InThePlane_ClearOfTheFrame()
        {
            foreach (int i in Rungs)
            {
                var gate = Gate(HeadlongCircuitSettings.ForIntensity(i).RingRadius);
                var at = new List<Vector3>();
                SpearlineSeals.Lay(gate, at);
                Assert.Greater(at.Count, 40, $"I{i}: a seal of {at.Count} prisms is not a pane");
                foreach (var p in at)
                {
                    Vector3 d = p - gate.Position;
                    Assert.Less(Mathf.Abs(Vector3.Dot(d, gate.Axis)), 1e-3f, "a seal prism is off the ring's plane");
                    Assert.LessOrEqual(d.magnitude + 0.5f * SpearlineSeals.PrismSize, gate.Radius - SpearlineSeals.RimInset + 1e-3f,
                        $"I{i}: a seal prism reaches the ring's frame");
                }
                // The centre is covered: the hole at the centre judges a full pane.
                Assert.AreEqual(0f, SpearlineSeals.OpenShare(gate, gate.Position, at, _ => true), 1e-6f);
            }
        }

        [Test]
        public void HullAlone_NeverOpensASeal()
        {
            foreach (int i in Rungs)
                Assert.Less(Share(i, Hull), SpearlineSeals.ForIntensity(i).OpenFraction, $"I{i}: ramming opens the seal");
        }

        [Test]
        public void TheStrokesEachRungAsksFor_OpenIt_AndOneFewerDoesNot()
        {
            System.Func<float, float, bool> half = (x, y) => Hull(x, y) || HalfStroke(x, y);
            System.Func<float, float, bool> full = (x, y) => Hull(x, y) || FullStroke(x, y);
            System.Func<float, float, bool> cross = (x, y) => Hull(x, y) || FullStroke(x, y) || Stood(x, y);

            Assert.GreaterOrEqual(Share(1, half), SpearlineSeals.ForIntensity(1).OpenFraction, "I1: one stroke does not open it");
            for (int i = 2; i <= 3; i++)
            {
                Assert.Less(Share(i, half), SpearlineSeals.ForIntensity(i).OpenFraction, $"I{i}: one stroke opens it");
                Assert.GreaterOrEqual(Share(i, full), SpearlineSeals.ForIntensity(i).OpenFraction, $"I{i}: a stroke each side does not");
            }
            Assert.Less(Share(4, full), SpearlineSeals.ForIntensity(4).OpenFraction, "I4: two strokes open it");
            Assert.GreaterOrEqual(Share(4, cross), SpearlineSeals.ForIntensity(4).OpenFraction, "I4: the cross does not open it");
        }

        [Test]
        public void PlaneCrossing_FindsThePoint_AndOnlyACrossing()
        {
            var gate = Gate(60f);
            Assert.IsTrue(SpearlineSeals.PlaneCrossing(gate, gate.Position - gate.Axis * 5f, gate.Position + gate.Axis * 15f, out var p));
            Assert.Less((p - gate.Position).magnitude, 1e-3f);
            Assert.IsFalse(SpearlineSeals.PlaneCrossing(gate, gate.Position + gate.Axis, gate.Position + gate.Axis * 9f, out _));
        }
    }
}
