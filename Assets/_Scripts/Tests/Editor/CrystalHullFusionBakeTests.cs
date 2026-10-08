using CosmicShore.Editor;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The edit-time bake of every crystal → hull fusion (CRYSTAL_HULL_FUSION.md §4). A STALE bake
    /// fails: the game would warn and solve at runtime, which is the cost the bake exists to remove,
    /// and nothing else would say so until someone read the console. A MISSING bake is inconclusive
    /// rather than red, because it is the state between landing the code and the first run of the
    /// tool - the runtime fallback still plays it correctly.
    /// </summary>
    public class CrystalHullFusionBakeTests
    {
        [Test]
        public void ShippedBakes_AreCurrent()
        {
            var config = CrystalHullFusionConfigSO.Load();
            Assert.IsNotNull(config, "Resources/CrystalHullFusionConfig is missing");

            int missing = 0;
            foreach (var entry in config.Entries)
            {
                if (entry == null) continue;
                var status = CrystalHullFusionBaker.Evaluate(entry);
                if (status.State == CrystalHullFusionBaker.BakeState.Missing) { missing++; continue; }
                Assert.AreEqual(CrystalHullFusionBaker.BakeState.Current, status.State,
                    $"{entry.vessel} × {entry.element}: {status.Detail} - re-run {CrystalHullFusion.BakeToolMenu}");
            }
            if (missing > 0)
                Assert.Inconclusive($"{missing} fusion(s) not baked yet - run {CrystalHullFusion.BakeToolMenu}.");
        }

        [Test]
        public void ShippedEntries_ResolveToAReadableHullAndCrystal()
        {
            // An entry the tool cannot resolve or read bakes nothing and plays the generic capture
            // forever; the usual cause is a model importer without Read/Write.
            var config = CrystalHullFusionConfigSO.Load();
            Assert.IsNotNull(config);
            foreach (var entry in config.Entries)
            {
                if (entry == null) continue;
                Assert.IsTrue(CrystalHullFusionBaker.TryResolve(entry, out var hull, out var drawn, out _, out _, out string why),
                    $"{entry.vessel} × {entry.element}: {why}");
                Assert.IsNotNull(CrystalHullFusion.CaptureSolveInput(hull, drawn, entry, out why), $"{entry.vessel} × {entry.element}: {why}");
            }
        }

        [Test]
        public void StaticHull_IsTheBodyAndEveryPartUnderIt()
        {
            // The Rhino flies a body plus wings and engines as separate MeshRenderers - no skin.
            var entry = new CrystalHullFusionConfigSO.Entry { vessel = CosmicShore.Data.VesselClassType.Rhino };
            Assert.IsTrue(CrystalHullFusionBaker.TryResolve(entry, out var hull, out _, out _, out _, out string why), why);
            Assert.IsNull(hull.Skinned, "the Rhino has no skinned hull");
            Assert.Greater(hull.PartCount, 1, "the wings and engines are part of the hull");
            Assert.AreEqual(hull.PartCount + 1, hull.Bones.Length, "one pin per part, then the body's space");
            Assert.AreSame(hull.Space, hull.Parts[0].transform, "the body leads and IS the hull's space");
        }

        [Test]
        public void Solve_IsDeterministic_SoAnUnchangedInputBakesAnUnchangedAsset()
        {
            var config = CrystalHullFusionConfigSO.Load();
            Assert.IsNotNull(config);
            Assert.Greater(config.Entries.Count, 0);
            var entry = config.Entries[0];

            Assert.IsTrue(CrystalHullFusionBaker.TryResolve(entry, out var hull, out var drawn, out _, out _, out string why), why);
            var input = CrystalHullFusion.CaptureSolveInput(hull, drawn, entry, out why);
            Assert.IsNotNull(input, why);

            var a = CrystalHullFusionGeometry.Solve(input, out why);
            Assert.IsNotNull(a, why);
            var b = CrystalHullFusionGeometry.Solve(input, out why);

            Assert.AreEqual(a.FaceCount, b.FaceCount);
            CollectionAssert.AreEqual(a.Layout.PointLocal, b.Layout.PointLocal);
            CollectionAssert.AreEqual(a.Layout.PointBone, b.Layout.PointBone);
            CollectionAssert.AreEqual(a.Layout.PatchDirection, b.Layout.PatchDirection);
            CollectionAssert.AreEqual(a.Vertices, b.Vertices);
            int total = a.Layout.Projected + a.Layout.Unprojected;
            Assert.GreaterOrEqual(a.Layout.Projected, total * 0.95f,
                $"{a.Layout.Unprojected} of {total} face points found no skin to land on - measured offline on the " +
                "FBX export it was 0 of 1860, so a large number means the hull or the patch sizing changed");
        }
    }
}
