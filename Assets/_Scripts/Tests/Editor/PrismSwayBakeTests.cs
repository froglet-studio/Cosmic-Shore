using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The living-mass sway's LOCKUP claim, tested on the C# half
    /// (Docs/ECOSYSTEM.md §47). Tools/Shaders/verify_prism_sway.py proves the shipped HLSL
    /// evaluates the limb's own shear field; these prove the four numbers it is handed
    /// actually describe this prism's attachment to this limb.
    ///
    /// The identity under test, stated once: a prism vertex displaced by the baked span
    /// must land, IN LIMB SPACE, exactly where the limb's shear puts the point at that
    /// vertex's own height. Everything else here is a special case of it.
    /// </summary>
    public class PrismSwayBakeTests
    {
        const float Amplitude = 0.12f;

        static (Transform limb, Transform prism) Rig(Vector3 prismLocalPos, Quaternion prismLocalRot,
            Vector3 prismLocalScale, Vector3 limbPos, Quaternion limbRot, Vector3 limbScale)
        {
            var limb = new GameObject("limb").transform;
            limb.SetPositionAndRotation(limbPos, limbRot);
            limb.localScale = limbScale;

            var prism = new GameObject("prism").transform;
            prism.SetParent(limb, false);
            prism.localPosition = prismLocalPos;
            prism.localRotation = prismLocalRot;
            prism.localScale = prismLocalScale;
            return (limb, prism);
        }

        static void Cleanup(Transform limb)
        {
            if (limb) Object.DestroyImmediate(limb.gameObject);
        }

        /// The shear SpindleSway applies, in limb object space, at height z.
        static Vector3 LimbShear(float z, float t, float phase)
        {
            const float secondaryRatio = 0.73f;   // SPINDLE_SWAY_SECONDARY_RATIO
            const float secondaryWeight = 0.45f;  // SPINDLE_SWAY_SECONDARY_WEIGHT
            float span = z * Amplitude;
            return new Vector3(
                span * Mathf.Sin(t),
                span * Mathf.Sin(t * secondaryRatio + phase + Mathf.PI / 2f) * secondaryWeight,
                0f);
        }

        /// Reproduces what the shader does with the stamp, on the CPU, in prism object space.
        static Vector3 PrismOffset(float3 spanX, float3 spanY, float3 axis, float z0,
            Vector3 vertex, float t, float phase)
        {
            const float secondaryRatio = 0.73f;
            const float secondaryWeight = 0.45f;
            float zl = z0 + (axis.x * vertex.x + axis.y * vertex.y + axis.z * vertex.z);
            Vector3 sx = new Vector3(spanX.x, spanX.y, spanX.z) * zl;
            Vector3 sy = new Vector3(spanY.x, spanY.y, spanY.z) * zl;
            return sx * Mathf.Sin(t)
                 + sy * (Mathf.Sin(t * secondaryRatio + phase + Mathf.PI / 2f) * secondaryWeight);
        }

        static void AssertRidesTheLimb(Transform limb, Transform prism, string what)
        {
            PrismSway.BakeAttachment(limb, prism, Amplitude,
                out var spanX, out var spanY, out var axis, out float z0);

            const float phase = 2.1f;
            foreach (float t in new[] { 0f, 0.37f, 1.7f, 4.4f })
            foreach (var vertex in new[] { Vector3.zero, new Vector3(0.4f, -0.3f, 0.9f),
                                           new Vector3(-1f, 0.5f, -0.7f) })
            {
                Vector3 offset = PrismOffset(spanX, spanY, axis, z0, vertex, t, phase);

                // Where the vertex sits on the limb, before and after.
                Vector3 restOnLimb = limb.InverseTransformPoint(prism.TransformPoint(vertex));
                Vector3 movedOnLimb = limb.InverseTransformPoint(prism.TransformPoint(vertex + offset));

                Vector3 expected = LimbShear(restOnLimb.z, t, phase);
                Vector3 actual = movedOnLimb - restOnLimb;

                Assert.That((actual - expected).magnitude, Is.LessThan(1e-4f),
                    $"{what}: vertex {vertex} at t={t} moved {actual} in limb space, " +
                    $"but the limb's own surface at height {restOnLimb.z} moved {expected}");
            }
        }

        [Test]
        public void PrismAtIdentity_RidesTheLimbExactly()
        {
            var (limb, prism) = Rig(new Vector3(0f, 0f, 3f), Quaternion.identity, Vector3.one,
                Vector3.zero, Quaternion.identity, Vector3.one);
            try { AssertRidesTheLimb(limb, prism, "identity"); }
            finally { Cleanup(limb); }
        }

        [Test]
        public void RotatedNonUniformlyScaledPrism_RidesTheLimbExactly()
        {
            // The Clawfish's fluke ribs: pitched by the blade's own local slope and carrying
            // a 2.6 x 0.4 x 3.0 leaf. This is the case a normalized axis gets wrong.
            var (limb, prism) = Rig(new Vector3(1.83f, 0f, -16f), Quaternion.Euler(17.7f, 0f, 0f),
                new Vector3(2.6f, 0.4f, 3f),
                new Vector3(11f, -4f, 7f), Quaternion.Euler(20f, 40f, 15f), Vector3.one);
            try { AssertRidesTheLimb(limb, prism, "rotated + non-uniform scale"); }
            finally { Cleanup(limb); }
        }

        [Test]
        public void ScaledLimb_RidesTheLimbExactly()
        {
            // A limb carrying its own scale — the lattice branches do. The span has to come
            // back through the limb's scale as well as the prism's.
            var (limb, prism) = Rig(new Vector3(0.2f, -0.1f, 1.4f), Quaternion.Euler(0f, 35f, 0f),
                new Vector3(0.8f, 1.6f, 0.5f),
                new Vector3(-30f, 12f, 4f), Quaternion.Euler(5f, 200f, 80f),
                new Vector3(3f, 3f, 6.2f));
            try { AssertRidesTheLimb(limb, prism, "scaled limb"); }
            finally { Cleanup(limb); }
        }

        [Test]
        public void PrismAtTheLimbRoot_DoesNotTranslate()
        {
            // The shear is exactly zero at z = 0, so a prism seated AT the root must not
            // move its own centre — which is why every lattice species, whose prisms sit at
            // their spindle's origin, is left essentially still by this feature.
            var (limb, prism) = Rig(Vector3.zero, Quaternion.Euler(0f, 0f, 30f), Vector3.one,
                Vector3.zero, Quaternion.identity, Vector3.one);
            try
            {
                PrismSway.BakeAttachment(limb, prism, Amplitude,
                    out var spanX, out var spanY, out var axis, out float z0);
                Assert.That(z0, Is.EqualTo(0f).Within(1e-6f), "a prism at the root has height 0");
                Vector3 centre = PrismOffset(spanX, spanY, axis, z0, Vector3.zero, 1.7f, 2.1f);
                Assert.That(centre.magnitude, Is.LessThan(1e-6f),
                    "a prism seated at the limb root must not translate");
            }
            finally { Cleanup(limb); }
        }

        [Test]
        public void ZeroAmplitude_BakesAnExactNoOp()
        {
            // The blast-radius control: every prism that is not part of a living limb keeps
            // this default, and it has to be bit-exact or the whole arena starts shimmering.
            var (limb, prism) = Rig(new Vector3(1f, 2f, 9f), Quaternion.Euler(10f, 20f, 30f),
                new Vector3(2f, 0.5f, 3f), Vector3.one, Quaternion.Euler(1f, 2f, 3f), Vector3.one);
            try
            {
                PrismSway.BakeAttachment(limb, prism, 0f,
                    out var spanX, out var spanY, out var axis, out float z0);
                Assert.That(math.lengthsq(spanX), Is.EqualTo(0f), "spanX must be exactly zero");
                Assert.That(math.lengthsq(spanY), Is.EqualTo(0f), "spanY must be exactly zero");
                Vector3 offset = PrismOffset(spanX, spanY, axis, z0,
                    new Vector3(0.4f, -0.3f, 0.9f), 1.7f, 2.1f);
                Assert.That(offset, Is.EqualTo(Vector3.zero), "a zero amplitude must not move anything");
            }
            finally { Cleanup(limb); }
        }
    }
}
