using System;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="CellGenome"/> and <see cref="CellGenomeFlow"/> (Docs/GENOME_CELL_GENOME.md): the packed form
    /// round-trips and fits in 64 bytes, FromSeed is deterministic and stays inside its safe ranges, and the flow
    /// field is finite and bounded everywhere.
    /// </summary>
    public class CellGenomeTests
    {
        [Test]
        public void PackedSize_IsAtMost64Bytes()
        {
            Assert.That(CellGenome.Size, Is.LessThanOrEqualTo(64));
            Assert.That(CellGenome.FromSeed(1).Pack().Length, Is.EqualTo(CellGenome.Size));
        }

        [Test]
        public void PackUnpack_RoundTrips([Values(0, 1, 42, -7, int.MaxValue, int.MinValue)] int seed)
        {
            var g = CellGenome.FromSeed(seed);
            var bytes = g.Pack();
            Assert.That(CellGenome.TryUnpack(bytes, out var back), Is.True);
            Assert.That(back, Is.EqualTo(g));
            CollectionAssert.AreEqual(bytes, back.Pack());
            Assert.That(CellGenome.TryFromBase64(g.ToBase64(), out var fromText), Is.True);
            Assert.That(fromText, Is.EqualTo(g));
        }

        [Test]
        public void Unpack_RejectsCorruptOrWrongLength()
        {
            var bytes = CellGenome.FromSeed(3).Pack();
            bytes[10] ^= 0x55;
            Assert.That(CellGenome.TryUnpack(bytes, out _), Is.False, "checksum");
            Assert.That(CellGenome.TryUnpack(new byte[CellGenome.Size - 1], out _), Is.False, "length");
            Assert.That(CellGenome.TryFromBase64("not base64!", out _), Is.False);
        }

        [Test]
        public void HandAuthoredGenome_QuantisesIdempotently()
        {
            var g = new CellGenome
            {
                WobbleSeed = 123456, WobbleFrequency = 0.6f, WobbleAmplitude = 10f, WobblePulseSpeed = 0.4f,
                MembraneBrightHsv = new Vector3(0.03f, -0.1f, 0.05f), FlowEnabled = true, FlowBaseStretch = 0.3f,
                Flow0 = new CellGenome.FlowTerm { Kind = CellGenome.FlowKind.Abc, AxisYaw = 45f, AxisPitch = 10f, Frequency = 1f, Strength = 0.5f },
                AmbientPitch = -2f, AmbientVolume = 0.5f,
            };
            var q = g.Quantised();
            Assert.That(q.WobbleFrequency, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(q.WobbleAmplitude, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(q.WobblePulseSpeed, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(q.Quantised(), Is.EqualTo(q));
        }

        [Test]
        public void FromSeed_IsDeterministicAndVaried()
        {
            Assert.That(CellGenome.FromSeed(99), Is.EqualTo(CellGenome.FromSeed(99)));
            CollectionAssert.AreEqual(CellGenome.FromSeed(99).Pack(), CellGenome.FromSeed(99).Pack());
            Assert.That(CellGenome.FromSeed(99), Is.Not.EqualTo(CellGenome.FromSeed(100)));
            Assert.That(CellGenome.StableHash("Cell_A"), Is.EqualTo(CellGenome.StableHash("Cell_A")));
            Assert.That(CellGenome.StableHash("Cell_A"), Is.Not.EqualTo(CellGenome.StableHash("Cell_B")));
        }

        [Test]
        public void FromSeed_StaysInSafeRanges()
        {
            const float q = 0.01f; // quantisation slack
            for (int seed = 0; seed < 500; seed++)
            {
                var g = CellGenome.FromSeed(CellGenome.StableHash("cell" + seed));
                Assert.That(g.WobbleFrequency, Is.InRange(0.45f - q, 0.8f + q));
                Assert.That(g.WobbleAmplitude, Is.InRange(7f - q, 14f + q));
                Assert.That(g.WobblePulseSpeed, Is.InRange(0.28f - q, 0.55f + q));
                foreach (var hsv in new[] { g.MembraneBrightHsv, g.MembraneDullHsv, g.CytoplasmHsv })
                {
                    Assert.That(Math.Abs(hsv.x), Is.LessThanOrEqualTo(0.05f + q));
                    Assert.That(Math.Abs(hsv.y), Is.LessThanOrEqualTo(0.08f + q));
                    Assert.That(Math.Abs(hsv.z), Is.LessThanOrEqualTo(0.08f + q));
                }
                Assert.That(g.FlowBaseStretch, Is.InRange(0.15f - q, 0.4f + q));
                int active = 0;
                for (int i = 0; i < CellGenome.FlowTermCount; i++)
                {
                    var t = g.GetFlowTerm(i);
                    if (t.Kind == CellGenome.FlowKind.Off) continue;
                    active++;
                    Assert.That(Math.Abs(t.Strength), Is.InRange(0.3f - q, 0.9f + q));
                    Assert.That(t.Frequency, Is.InRange(0.5f - 0.02f, 2f + 0.02f));
                }
                Assert.That(active, Is.InRange(2, 3));
            }
        }

        [Test]
        public void Flow_IsFiniteAndBounded()
        {
            var rng = new System.Random(7);
            for (int seed = 0; seed < 64; seed++)
            {
                var g = CellGenome.FromSeed(seed);
                var centre = new Vector3(100f, -50f, 20f);
                var flow = new CellGenomeFlow(g, centre, 500f);
                Assert.That(flow.MaxMagnitude, Is.GreaterThan(0f));
                for (int i = 0; i < 200; i++)
                {
                    // inside, on and far outside the membrane, and the exact centre
                    float scale = i == 0 ? 0f : (float)rng.NextDouble() * 2000f;
                    var dir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                    var p = centre + dir.normalized * scale;
                    var v = flow.Evaluate(p);
                    Assert.That(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z), Is.False);
                    Assert.That(v.magnitude, Is.LessThanOrEqualTo(flow.MaxMagnitude + 1e-4f));
                    if (flow.TryFlow(p, out var d, out float stretch))
                    {
                        Assert.That(d.sqrMagnitude, Is.GreaterThan(0f));
                        Assert.That(stretch, Is.InRange(0f, 1f));
                    }
                }
            }
        }

        [Test]
        public void ShiftHsv_ZeroOffsetKeepsColourAndHdr()
        {
            var hdr = new Color(2f, 1f, 0.5f, 0.7f);
            var c = CellGenome.ShiftHsv(hdr, Vector3.zero);
            Assert.That(c.r, Is.EqualTo(hdr.r).Within(1e-3f));
            Assert.That(c.g, Is.EqualTo(hdr.g).Within(1e-3f));
            Assert.That(c.b, Is.EqualTo(hdr.b).Within(1e-3f));
            Assert.That(c.a, Is.EqualTo(hdr.a));
        }
    }
}
