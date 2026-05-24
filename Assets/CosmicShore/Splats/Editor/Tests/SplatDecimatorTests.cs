using NUnit.Framework;
using UnityEngine;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport.Tests
{
    // Pipeline-level tests for the splat decode + decimate path.
    // These exercise the math without any real .ply file so the bake can be validated
    // even before the SuperSplat export is checked in.
    [TestFixture]
    public class SplatDecimatorTests
    {
        [Test]
        public void Decode_RecoversScaleFromLogEncoding()
        {
            var raw = new SplatPlyImporter.RawSplat
            {
                position = Vector3.zero,
                hasScale = true,
                scaleRaw = new Vector3(Mathf.Log(0.5f), Mathf.Log(0.1f), Mathf.Log(0.02f)),
                hasRotation = true,
                rotRaw = new Vector4(1, 0, 0, 0), // identity (w, x, y, z)
                hasColor = true,
                fDc = Vector3.zero, // -> 0.5, 0.5, 0.5
                hasOpacity = true,
                opacityRaw = 0f, // sigmoid(0) = 0.5
            };
            var p = SplatDecimator.Decode(raw, flipHandedness: false);
            Assert.AreEqual(0.5f, p.scale.x, 1e-4f);
            Assert.AreEqual(0.1f, p.scale.y, 1e-4f);
            Assert.AreEqual(0.02f, p.scale.z, 1e-4f);
        }

        [Test]
        public void Decode_RecoversOpacityFromLogit()
        {
            var raw = NewBaseRaw();
            raw.opacityRaw = Mathf.Log(0.8f / 0.2f); // logit(0.8)
            var p = SplatDecimator.Decode(raw, flipHandedness: false);
            Assert.AreEqual(0.8f, p.opacity, 1e-4f);
        }

        [Test]
        public void Decode_RecoversColorFromShDc()
        {
            var raw = NewBaseRaw();
            raw.fDc = new Vector3(
                (0.9f - 0.5f) / SplatDecimator.SH_C0,
                (0.2f - 0.5f) / SplatDecimator.SH_C0,
                (0.7f - 0.5f) / SplatDecimator.SH_C0);
            var p = SplatDecimator.Decode(raw, flipHandedness: false);
            Assert.AreEqual(0.9f, p.color.r, 1e-3f);
            Assert.AreEqual(0.2f, p.color.g, 1e-3f);
            Assert.AreEqual(0.7f, p.color.b, 1e-3f);
        }

        [Test]
        public void Decode_ClampsExtremeColorIntoUnitRange()
        {
            var raw = NewBaseRaw();
            raw.fDc = new Vector3(100f, -100f, 100f);
            var p = SplatDecimator.Decode(raw, flipHandedness: false);
            Assert.AreEqual(1f, p.color.r);
            Assert.AreEqual(0f, p.color.g);
            Assert.AreEqual(1f, p.color.b);
        }

        [Test]
        public void Decode_WFirstQuaternionRoundTrips()
        {
            var expected = Quaternion.AngleAxis(37f, new Vector3(1f, 2f, 3f).normalized);
            var raw = NewBaseRaw();
            raw.rotRaw = new Vector4(expected.w, expected.x, expected.y, expected.z);
            var p = SplatDecimator.Decode(raw, flipHandedness: false);
            Assert.AreEqual(expected.x, p.rotation.x, 1e-4f);
            Assert.AreEqual(expected.y, p.rotation.y, 1e-4f);
            Assert.AreEqual(expected.z, p.rotation.z, 1e-4f);
            Assert.AreEqual(expected.w, p.rotation.w, 1e-4f);
        }

        [Test]
        public void Decimate_NeverExceedsHardCap()
        {
            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(500);
            var settings = SplatDecimateSettings.Default;
            settings.voxelSize = 0.001f; // very fine - most splats survive voxel stage
            settings.maxPrisms = 10; // tiny cap - cap stage decides
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            Assert.LessOrEqual(pts.Length, 10);
            Assert.LessOrEqual(pts.Length, SplatPrismSet.MaxPrisms);
            Assert.AreEqual(500, report.parsed);
        }

        [Test]
        public void Decimate_RespectsTenThousandHardCap()
        {
            var settings = SplatDecimateSettings.Default;
            settings.maxPrisms = 999999; // user tries to override cap
            settings.voxelSize = 0.0001f;
            var (raw, _) = SplatSyntheticTest.MakeSyntheticSplats(50);
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out _);
            Assert.LessOrEqual(pts.Length, SplatPrismSet.MaxPrisms);
        }

        [Test]
        public void Decimate_VoxelCoalescesNearbyPoints()
        {
            // 100 points clustered in a 0.01-radius blob.
            var raw = new SplatPlyImporter.RawSplat[100];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = NewBaseRaw();
                float u = i / (float)raw.Length;
                raw[i].position = new Vector3(u * 0.005f, 0f, 0f);
            }
            var settings = SplatDecimateSettings.Default;
            settings.voxelSize = 0.1f; // far bigger than the blob -> single voxel
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            Assert.AreEqual(1, report.afterVoxel);
            Assert.AreEqual(1, pts.Length);
        }

        [Test]
        public void Decimate_OpacityFilterDropsTransparent()
        {
            var raw = new SplatPlyImporter.RawSplat[10];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = NewBaseRaw();
                raw[i].position = new Vector3(i, 0, 0);
                raw[i].opacityRaw = i < 5 ? -10f : 10f; // first 5 ~0, last 5 ~1
            }
            var settings = SplatDecimateSettings.Default;
            settings.minOpacity = 0.5f;
            settings.voxelSize = 0.001f; // each point in its own voxel
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            Assert.AreEqual(5, report.afterOpacityFilter);
            Assert.AreEqual(5, pts.Length);
        }

        [Test]
        public void Decimate_CropCenteredFractionShrinksBounds()
        {
            // Points spread along X in [-1, 1].
            var raw = new SplatPlyImporter.RawSplat[21];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = NewBaseRaw();
                float t = (i / 20f) * 2f - 1f;
                raw[i].position = new Vector3(t, 0, 0);
            }
            var settings = SplatDecimateSettings.Default;
            settings.cropMode = SplatCropMode.CenteredFraction;
            settings.cropFraction = 0.5f; // keep middle 50% by extent
            settings.voxelSize = 0.001f;
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out var report);
            Assert.Less(report.afterCrop, report.parsed);
            foreach (var p in pts) Assert.LessOrEqual(Mathf.Abs(p.position.x), 0.51f);
        }

        [Test]
        public void Decimate_FlipHandednessNegatesZ()
        {
            var raw = NewBaseRaw();
            raw.position = new Vector3(1f, 2f, 3f);
            var p = SplatDecimator.Decode(raw, flipHandedness: true);
            Assert.AreEqual(1f, p.position.x);
            Assert.AreEqual(2f, p.position.y);
            Assert.AreEqual(-3f, p.position.z);
        }

        [Test]
        public void Decimate_NoPointsWhenInputEmpty()
        {
            var pts = SplatDecimator.DecimateToPoints(new SplatPlyImporter.RawSplat[0], SplatDecimateSettings.Default, out var report);
            Assert.AreEqual(0, pts.Length);
            Assert.AreEqual(0, report.finalCount);
        }

        [Test]
        public void Decimate_BigSpreadProducesAnisotropicScale()
        {
            // The synthetic generator builds anisotropic splats (x > y > z).
            var (raw, truth) = SplatSyntheticTest.MakeSyntheticSplats(50);
            var settings = SplatDecimateSettings.Default;
            settings.voxelSize = 0.001f;
            var pts = SplatDecimator.DecimateToPoints(raw, settings, out _);
            foreach (var p in pts)
            {
                Assert.Greater(p.scale.x, p.scale.y);
                Assert.Greater(p.scale.y, p.scale.z);
            }
            Assert.Greater(truth.Length, 0);
        }

        private static SplatPlyImporter.RawSplat NewBaseRaw()
        {
            return new SplatPlyImporter.RawSplat
            {
                position = Vector3.zero,
                hasScale = true,
                scaleRaw = new Vector3(Mathf.Log(0.1f), Mathf.Log(0.1f), Mathf.Log(0.1f)),
                hasRotation = true,
                rotRaw = new Vector4(1, 0, 0, 0),
                hasColor = true,
                fDc = Vector3.zero,
                hasOpacity = true,
                opacityRaw = 1.386f, // ~0.8
            };
        }
    }
}
