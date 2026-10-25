#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The maths a wormhole's illusion rests on (<see cref="WormholeGeometry"/>). Every rule here has
    /// a visible failure if it drifts: a transit that lurches, a pilot bounced straight back out of
    /// the mouth they arrived in, a far ship missing from the window, or a panorama whose six faces
    /// do not meet.
    ///
    /// <para>The panorama tests are the contract between three places that each hold one half of it:
    /// the face camera's pose (<see cref="WormholeGeometry.FaceRotation"/>), the C# face lookup
    /// (<see cref="WormholeGeometry.FaceOf"/> / <see cref="WormholeGeometry.FaceUV"/>), and
    /// <c>Wormhole.shader</c>'s <c>SamplePanorama</c>, which is the same table written out. A camera
    /// posed by FaceRotation must see a direction exactly where FaceUV says it is.</para>
    /// </summary>
    public class WormholeGeometryTests
    {
        static readonly Vector3 A = new(0f, 0f, 620f);
        static readonly Vector3 B = new(-700f, 450f, -480f);
        const float R = 70f;

        [Test]
        public void Through_IsAPureTranslation()
        {
            var p = A + new Vector3(12f, -30f, 55f);
            var q = WormholeGeometry.Through(p, A, B);
            Assert.That(Vector3.Distance(q - B, p - A), Is.LessThan(1e-3f),
                "a point must sit at the same offset from the far mouth as from the near one");
        }

        [Test]
        public void SegmentEntersBall_FromOutsideIntoTheBall()
        {
            var from = A + Vector3.forward * (R + 5f);
            var to = A + Vector3.forward * (R - 5f);
            Assert.IsTrue(WormholeGeometry.SegmentEntersBall(from, to, A, R));
        }

        [Test]
        public void SegmentEntersBall_NeverForAStepThatStartsInside()
        {
            // A pilot just carried into the far mouth must fly out before it can take them back.
            var from = A + Vector3.forward * (R - 20f);
            var to = A + Vector3.forward * (R - 10f);
            Assert.IsFalse(WormholeGeometry.SegmentEntersBall(from, to, A, R));
            Assert.IsFalse(WormholeGeometry.SegmentEntersBall(to, A + Vector3.forward * (R + 10f), A, R),
                "flying OUT of a mouth is not entering it");
        }

        [Test]
        public void SegmentEntersBall_AStepThatPassesClean_Through_Counts()
        {
            // A fast frame can carry a ship across the whole ball; it still went through.
            var from = A + Vector3.left * (R + 10f);
            var to = A + Vector3.right * (R + 10f);
            Assert.IsTrue(WormholeGeometry.SegmentEntersBall(from, to, A, R));
        }

        [Test]
        public void SegmentEntersBall_AMissIsAMiss()
        {
            var from = A + new Vector3(-200f, R + 1f, 0f);
            var to = A + new Vector3(200f, R + 1f, 0f);
            Assert.IsFalse(WormholeGeometry.SegmentEntersBall(from, to, A, R));
        }

        [Test]
        public void NearestSurfacePoint_LiesOnTheSphere()
        {
            var p = WormholeGeometry.NearestSurfacePoint(A + new Vector3(3f, 4f, 0f) * 40f, A, R);
            Assert.That(Mathf.Abs(Vector3.Distance(p, A) - R), Is.LessThan(1e-3f));
            Assert.That(Vector3.Dot((p - A).normalized, new Vector3(0.6f, 0.8f, 0f)), Is.GreaterThan(0.9999f));
        }

        [Test]
        public void NearCapPlane_KeepsTheWholeFarBall()
        {
            // The window must show anything inside the far ball - that is where a ship is the
            // instant after a transit - so every point of the ball is on the kept side.
            var eye = B + new Vector3(150f, -40f, 300f);
            Assert.IsTrue(WormholeGeometry.TryNearCapPlane(eye, B, R, out var n, out var point));
            Assert.That(Mathf.Abs(Vector3.Distance(point, B) - R), Is.LessThan(1e-3f), "tangent to the ball");
            Assert.That(Vector3.Dot(n, point - eye), Is.GreaterThan(0f), "normal points away from the eye");

            var rng = new System.Random(7);
            for (int i = 0; i < 500; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f,
                                      (float)rng.NextDouble() - 0.5f).normalized;
                var x = B + dir * (R * (float)rng.NextDouble());
                Assert.That(Vector3.Dot(n, x - point), Is.GreaterThanOrEqualTo(-1e-3f));
            }
        }

        [Test]
        public void NearCapPlane_NoneForAnEyeInsideTheBall()
        {
            Assert.IsFalse(WormholeGeometry.TryNearCapPlane(B + Vector3.up * (R * 0.5f), B, R, out _, out _));
        }

        [Test]
        public void Crop_MapsTheFootprintOntoTheWholeTarget()
        {
            var p = Matrix4x4.Perspective(60f, 16f / 9f, 0.3f, 5000f);
            var fp = Rect.MinMaxRect(0.2f, 0.3f, 0.5f, 0.9f);
            var c = WormholeGeometry.Crop(p, fp);

            // A view-space point that lands at the footprint's min corner (viewport 0.2, 0.3)
            // under the original projection must land at NDC (-1, -1) under the cropped one.
            var ndcMin = new Vector2(fp.xMin * 2f - 1f, fp.yMin * 2f - 1f);
            float z = -40f;
            var vx = ndcMin.x * -z / p.m00;
            var vy = ndcMin.y * -z / p.m11;
            var clip = c * new Vector4(vx, vy, z, 1f);
            Assert.That(clip.x / clip.w, Is.EqualTo(-1f).Within(1e-3f));
            Assert.That(clip.y / clip.w, Is.EqualTo(-1f).Within(1e-3f));

            Assert.That((WormholeGeometry.Crop(p, new Rect(0f, 0f, 1f, 1f)).GetRow(0) - p.GetRow(0)).magnitude,
                        Is.LessThan(1e-5f), "the whole viewport is the identity crop");
        }

        [Test]
        public void Panorama_EachFaceCameraLooksDownItsOwnFace()
        {
            for (int face = 0; face < WormholeGeometry.FaceCount; face++)
            {
                var forward = WormholeGeometry.FaceRotation(face) * Vector3.forward;
                Assert.AreEqual(face, WormholeGeometry.FaceOf(forward), $"face {face}");
                var uv = WormholeGeometry.FaceUV(face, forward);
                Assert.That((uv - new Vector2(0.5f, 0.5f)).magnitude, Is.LessThan(1e-4f), $"face {face} centre");
            }
        }

        [Test]
        public void Panorama_FaceUV_IsWhereTheFaceCameraSeesTheDirection()
        {
            // The camera renders face f with rotation FaceRotation(f), +Z forward, x right, y up,
            // a 90° square frustum: a direction lands at (x/z, y/z) * 0.5 + 0.5 of its image.
            // FaceUV (and the shader) must agree for every direction, or the faces will not meet.
            var rng = new System.Random(11);
            for (int i = 0; i < 2000; i++)
            {
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f,
                                      (float)rng.NextDouble() * 2f - 1f);
                if (dir.sqrMagnitude < 1e-4f) continue;
                dir.Normalize();

                int face = WormholeGeometry.FaceOf(dir);
                var local = Quaternion.Inverse(WormholeGeometry.FaceRotation(face)) * dir;
                Assert.That(local.z, Is.GreaterThan(0f), "the chosen face is in front of its camera");
                var seen = new Vector2(local.x / local.z * 0.5f + 0.5f, local.y / local.z * 0.5f + 0.5f);
                var uv = WormholeGeometry.FaceUV(face, dir);

                Assert.That((seen - uv).magnitude, Is.LessThan(1e-4f), $"dir {dir} face {face}");
                Assert.That(uv.x, Is.InRange(-1e-4f, 1f + 1e-4f));
                Assert.That(uv.y, Is.InRange(-1e-4f, 1f + 1e-4f));
            }
        }

        [Test]
        public void Parallax_AtAFarProxyIsTheViewDirection()
        {
            var rel = new Vector3(0f, 30f, -R);
            var view = new Vector3(0.2f, -0.1f, 1f).normalized;
            var dir = WormholeGeometry.ParallaxDirection(rel, view, 1e6f).normalized;
            Assert.That(Vector3.Dot(dir, view), Is.GreaterThan(0.99999f));
        }

        [Test]
        public void Parallax_EndsOnTheProxySphere()
        {
            var rel = new Vector3(10f, 30f, -60f);
            var view = new Vector3(0.3f, 0.1f, 1f).normalized;
            const float proxy = 600f;
            var end = WormholeGeometry.ParallaxDirection(rel, view, proxy);
            Assert.That(end.magnitude, Is.EqualTo(proxy).Within(0.01f));
            Assert.That(Vector3.Dot(end - rel, view), Is.GreaterThan(0f), "the ray runs forward, never back");
        }

        [Test]
        public void Clearance_NeverBelowTheFloor_AndGrowsWithTheNearClip()
        {
            Assert.That(WormholeGeometry.Clearance(0.01f), Is.EqualTo(WormholeGeometry.MinClearance));
            Assert.That(WormholeGeometry.Clearance(1f), Is.GreaterThan(WormholeGeometry.Clearance(0.3f)));
            Assert.That(WormholeGeometry.Clearance(1f), Is.GreaterThan(1f), "beyond the near clip itself");
        }
    }
}
#endif
