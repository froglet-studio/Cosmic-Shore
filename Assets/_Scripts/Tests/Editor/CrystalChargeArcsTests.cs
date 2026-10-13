#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Charge light (<see cref="CrystalChargeArcs"/>, Omni Motion Lab round 5): the real models read as their sites
    /// and seam net, the seam walk and the flux chain keep their rules, and the generator's wiring
    /// (<c>Tools/Build/author_omni_crystal_triangles.py</c>) puts the flux arcs on the omni and the seam walk on the
    /// Charge crystal. The harness <c>Tools/Build/charge_arcs_harness</c> runs the same core over ten minutes of walking.
    /// </summary>
    public class CrystalChargeArcsTests
    {
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string ChargePrefabPath = "Assets/_Prefabs/Environment/CrystalCharge.prefab";
        const string OmniFbxPath = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx";
        const string ChargeFbxPath = "Assets/_Models/ChargeCrystalExport1_7-11-25.fbx";

        static Mesh ModelMesh(string path)
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
            Assert.IsNotNull(mesh, $"{path}: no mesh");
            return mesh;
        }

        [Test]
        public void ChargeCrystal_ReadsAsTwelveBlooms_AndTheDodecahedralSeamNet()
        {
            var mesh = ModelMesh(ChargeFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1f, out var sites, out var problem), problem);
            Assert.AreEqual(ChargeSiteKind.Blooms, sites.Kind);
            Assert.IsTrue(sites.FacesOfSite.All(f => f.Length == 5), "every bloom is five petals");
            Assert.AreEqual(20, sites.Junctions.Length);
            Assert.AreEqual(30, sites.Seams.Length);
            Assert.IsTrue(sites.SeamsOfJunction.All(e => e.Length == 3), "three seams meet at every junction");
            Assert.IsTrue(sites.Faces.All(f => f.Back.Length == 3), "a petal's outline is its three short sides");
        }

        [Test]
        public void Omni_ReadsAsTwelvePentagons_WithArcEndsOnTheirCorners()
        {
            var mesh = ModelMesh(OmniFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1.1f, out var sites, out var problem), problem);
            Assert.AreEqual(ChargeSiteKind.Pentagons, sites.Kind);
            Assert.IsTrue(sites.Corners.All(c => c.Length == 5));
            var path = sites.PathOf(0, sites.Neighbours[0][0]);
            for (int k = 0; k < 5; k++)
                Assert.That(sites.Corners[0].Min(c => (path.Strand(k, 0f, 0.3f) - c).magnitude), Is.LessThan(1e-4f * sites.Radius),
                    "a strand starts on a source corner");
        }

        [Test]
        public void SeamWalk_IsContinuous_NeverBack_NeverCircles()
        {
            var mesh = ModelMesh(ChargeFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1f, out var sites, out var problem), problem);
            var settings = ChargeArcSettings.Lab;
            var walker = new ChargeSeamWalker(sites, 3, 0f);
            var legs = new System.Collections.Generic.SortedDictionary<int, ChargeSeamWalker.Leg>();
            for (float t = 0f; t < 120f; t += 0.1f)
            {
                walker.Advance(t, settings);
                foreach (var leg in walker.Legs) legs[leg.Serial] = leg;
            }
            var list = legs.Values.ToList();
            Assert.Greater(list.Count, 100);
            for (int i = 1; i < list.Count; i++)
            {
                ChargeSeam a = sites.Seams[list[i - 1].Seam], b = sites.Seams[list[i].Seam];
                Assert.AreEqual(list[i - 1].Forward ? a.B : a.A, list[i].Forward ? b.A : b.B, "each seam starts where the last ended");
                Assert.AreNotEqual(list[i - 1].Seam, list[i].Seam, "never straight back");
                if (i < 4) continue;
                for (int s = 0; s < ChargeSites.SiteCount; s++)
                    Assert.IsFalse(Enumerable.Range(i - 4, 5).All(j => sites.Seams[list[j].Seam].Touches(s)), "a fifth seam round one bloom");
            }
        }

        [Test]
        public void FluxChain_LeapsOnFromTheLastSink()
        {
            var mesh = ModelMesh(OmniFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1.1f, out var sites, out var problem), problem);
            var settings = ChargeArcSettings.Lab;
            var chain = new ChargeFluxChain(sites, 11);
            ChargeFluxChain.Discharge last = default;
            for (long k = 0; k < 300; k++)
            {
                Assert.IsTrue(chain.TryGet(k, settings, out var d));
                float angle = Vector3.Angle(sites.Axes[d.Source], sites.Axes[d.Sink]);
                Assert.That(angle, Is.InRange(settings.FluxMinDegrees, settings.FluxMaxDegrees));
                if (k > 0)
                {
                    Assert.AreEqual(last.Sink, d.Source, "each discharge leaps on from the last sink");
                    Assert.AreNotEqual(last.Source, d.Sink, "never straight back");
                }
                last = d;
            }
        }

        [Test]
        public void Prefabs_CarryTheirChargeLight()
        {
            var omni = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath);
            var omniArcs = omni.GetComponentInChildren<CrystalChargeArcs>(true);
            Assert.IsNotNull(omniArcs, "the omni has no CrystalChargeArcs");
            var so = new SerializedObject(omniArcs);
            Assert.AreEqual((int)CrystalChargeArcs.Effect.Flux, so.FindProperty("effect").intValue);
            Assert.IsNotNull(so.FindProperty("profile").objectReferenceValue);
            Assert.AreSame(ModelMesh(OmniFbxPath), so.FindProperty("sourceMesh").objectReferenceValue);
            Assert.IsTrue(omniArcs.TryGetComponent<CrystalAccentTint>(out _), "the omni's lit pentagons must wear its domain colour");
            Assert.IsFalse(omni.GetComponentsInChildren<CrystalEdgeArcs>(true).Any(), "the retired edge crawl is still on the omni");

            var charge = AssetDatabase.LoadAssetAtPath<GameObject>(ChargePrefabPath);
            var chargeArcs = charge.GetComponentInChildren<CrystalChargeArcs>(true);
            Assert.IsNotNull(chargeArcs, "the Charge crystal has no CrystalChargeArcs");
            so = new SerializedObject(chargeArcs);
            Assert.AreEqual((int)CrystalChargeArcs.Effect.SeamWalk, so.FindProperty("effect").intValue);
            Assert.AreSame(ModelMesh(ChargeFbxPath), so.FindProperty("sourceMesh").objectReferenceValue);
            var shell = charge.GetComponent<Crystal>().CrystalModels[0].model.GetComponent<Renderer>();
            Assert.AreSame(shell, so.FindProperty("silenceCrawlOn").objectReferenceValue, "the seam walk replaces the shell's crawl");
        }
    }
}
#endif
