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
        public void MassCrystal_FliesItsDepthPrisms_WithTheThicknessInTheMesh()
        {
            // The Mass crystal is one renderer of 60 depth prisms (round 8). The fusion moves that mesh's
            // vertices onto the hull and draws them on the crystal's own OmniDepthPrismShader material,
            // undriven - where the vertex stage is the identity only while the material leaves the
            // thickness to the mesh. A thickness on the material stretches every moved face away from a
            // plate centre it has left (x5.25 at the Mass's 6.25).
            var set = ElementalCrystalSetSO.Load();
            Assert.IsNotNull(set);
            var mass = set.GetPrefab(CosmicShore.Data.Element.Mass);
            Assert.IsNotNull(mass);
            Assert.IsTrue(CrystalHullFusion.TryResolveCrystal(mass, out var drawn, out var source, out _, out _, out var renderer));
            Assert.AreSame(drawn, source, "the Mass prisms draw their own mesh");
            Assert.AreEqual("MassCrystalPrisms", source.name, $"the Mass crystal flies '{source.name}'");
            var material = renderer.sharedMaterial;
            Assert.IsTrue(material.HasProperty("_DepthDriven"), $"'{material.name}' is not on OmniDepthPrismShader");
            Assert.AreEqual(1f, material.GetFloat("_Thickness"),
                $"'{material.name}' carries _Thickness {material.GetFloat("_Thickness")}: the thickness belongs in the mesh - re-run Tools/Build/author_mass_crystal_look.py");
        }

        [Test]
        public void Undrive_TurnsOffTheCrystalsDepthDriver()
        {
            var set = ElementalCrystalSetSO.Load();
            var mass = set.GetPrefab(CosmicShore.Data.Element.Mass);
            Assert.IsTrue(CrystalHullFusion.TryResolveCrystal(mass, out _, out _, out _, out _, out var renderer));
            var block = new MaterialPropertyBlock();
            block.SetFloat("_DepthDriven", 1f);   // what CrystalDepthPrisms leaves on a visible crystal
            CrystalHullFusion.Undrive(block, renderer.sharedMaterial);
            Assert.AreEqual(0f, block.GetFloat("_DepthDriven"));

            // A crystal on any other shader is left alone.
            var other = new MaterialPropertyBlock();
            var plain = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                CrystalHullFusion.Undrive(other, plain);
                Assert.IsFalse(other.HasFloat("_DepthDriven"));
            }
            finally { Object.DestroyImmediate(plain); }
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
