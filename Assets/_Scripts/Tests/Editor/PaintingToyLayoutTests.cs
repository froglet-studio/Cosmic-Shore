#if UNITY_EDITOR
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the monument-anchor packing (<see cref="PaintingToyDefinitionSO.PackMonumentAnchors"/>):
    /// no two monuments may interpenetrate, nothing pokes through the membrane, the on-ramp packs
    /// nearest the stations, and the layout is deterministic.
    /// </summary>
    public class PaintingToyLayoutTests
    {
        const float RingRadius = 984f;
        const float Clearance = 150f;
        static readonly Vector3 Center = Vector3.zero;
        static readonly Vector3 Slot = new(0f, 0f, RingRadius);

        /// <summary>Ground-rebased bounds like a real painting: base y=0, x/z centred.</summary>
        static Bounds Painting(float w, float h, float d)
            => new(new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));

        static Bounds[] Gallery()
        {
            // Shaped like the shipped ladder: 4 small on-ramp entries, then mid-size, then giants
            // (the Matterhorn-class sizes that broke the wall layout).
            return new[]
            {
                Painting(800, 800, 90), Painting(700, 350, 110), Painting(880, 440, 880),
                Painting(1100, 680, 940), Painting(1060, 380, 920), Painting(950, 950, 950),
                Painting(200, 870, 210), Painting(500, 570, 230), Painting(930, 280, 880),
                Painting(430, 750, 430), Painting(1340, 1010, 410), Painting(1400, 1060, 1050),
                Painting(1950, 570, 1950), Painting(1690, 1390, 380), Painting(1980, 1780, 2010),
                Painting(1170, 2100, 340),
            };
        }

        static void Pack(Bounds[] gallery, out Vector3[] positions, out Quaternion[] rotations)
        {
            positions = new Vector3[gallery.Length];
            rotations = new Quaternion[gallery.Length];
            PaintingToyDefinitionSO.PackMonumentAnchors(
                gallery, Center, Slot, RingRadius, Clearance, positions, rotations);
        }

        static Vector3 SphereCenter(Bounds b, Vector3 pos, Quaternion rot) => pos + rot * b.center;
        static float SphereRadius(Bounds b) => b.extents.magnitude + Clearance * 0.5f;

        [Test]
        public void Packing_NeverInterpenetratesAndStaysOutsideTheMembrane()
        {
            var gallery = Gallery();
            Pack(gallery, out var pos, out var rot);

            for (int i = 0; i < gallery.Length; i++)
            {
                Vector3 ci = SphereCenter(gallery[i], pos[i], rot[i]);
                float ri = SphereRadius(gallery[i]);
                Assert.GreaterOrEqual(Vector3.Distance(ci, Center), RingRadius + ri - 1e-3f,
                    $"monument {i} pokes through the membrane");
                for (int j = i + 1; j < gallery.Length; j++)
                {
                    Vector3 cj = SphereCenter(gallery[j], pos[j], rot[j]);
                    float rj = SphereRadius(gallery[j]);
                    Assert.GreaterOrEqual(Vector3.Distance(ci, cj), ri + rj - 1e-3f,
                        $"monuments {i} and {j} interpenetrate");
                }
            }
        }

        [Test]
        public void Packing_KeepsTheOnRampNearestAndGiantsWithinReach()
        {
            var gallery = Gallery();
            Pack(gallery, out var pos, out _);

            // The first painting (the warm-up) sits essentially at the stations…
            Assert.Less(Vector3.Distance(pos[0], Slot), 1400f, "the warm-up painting must be close");
            // …and even the giants stay a short flight away, never wall-corner exiled.
            for (int i = 0; i < gallery.Length; i++)
                Assert.Less(Vector3.Distance(pos[i], Slot), 3200f,
                    $"monument {i} packed too far from the stations");
        }

        [Test]
        public void Packing_IsDeterministic()
        {
            var gallery = Gallery();
            Pack(gallery, out var a, out var ra);
            Pack(gallery, out var b, out var rb);
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreEqual(ra, rb);
        }
    }
}
#endif
