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
            var settings = ChargeArcSettings.Lab;
            float bundle = settings.FluxBundle * sites.Radius;
            Vector3 At(int k, float u) => path.Strand(k, u, settings.FluxFork, bundle, settings.FluxTurns);
            for (int k = 0; k < 5; k++)
                Assert.That(sites.Corners[0].Min(c => (At(k, 0f) - c).magnitude), Is.LessThan(1e-4f * sites.Radius),
                    "a strand starts on a source corner");
            // through the middle: five distinct filaments round the centre line, 72 degrees apart (one seamless bundle)
            var mid = Enumerable.Range(0, 5).Select(k => At(k, 0.5f)).ToArray();
            foreach (var p in mid) Assert.That((p - path.Centre(0.5f)).magnitude, Is.EqualTo(bundle).Within(0.15f * bundle));
            for (int a = 0; a < 5; a++)
                for (int b = a + 1; b < 5; b++)
                    Assert.Greater((mid[a] - mid[b]).magnitude, 0.9f * 1.1756f * bundle, "two strands touch mid-arc");
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

        // Garrett, 2026-11-12: "the bolt should always be on screen. it spends too much time invisible to the player on
        // the other side of the crystal." The walk, given the viewer, stays on the viewer's side and comes round fast.
        static System.Collections.Generic.List<ChargeSeamWalker.Leg> Walk(ChargeSeamWalker walker, ChargeArcSettings settings,
            float from, float to, System.Func<float, Vector3?> eyeAt)
        {
            var legs = new System.Collections.Generic.SortedDictionary<int, ChargeSeamWalker.Leg>();
            for (float t = from; t < to; t += 0.05f)
            {
                walker.Advance(t, settings, eyeAt(t));
                foreach (var leg in walker.Legs) legs[leg.Serial] = leg;
            }
            return legs.Values.ToList();
        }

        static float Share(ChargeSites sites, ChargeSeamWalker walker, System.Collections.Generic.List<ChargeSeamWalker.Leg> legs, Vector3 eye, bool whole) =>
            legs.Count(l =>
            {
                bool a = walker.Facing(sites.Seams[l.Seam].A, eye) > 0f, b = walker.Facing(sites.Seams[l.Seam].B, eye) > 0f;
                return whole ? a && b : a || b;
            }) / (float)legs.Count;

        [Test]
        public void SeamWalk_GivenTheViewer_StaysOnItsSide_AndKeepsItsRules()
        {
            var mesh = ModelMesh(ChargeFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1f, out var sites, out var problem), problem);
            var steered = ChargeArcSettings.Lab;
            steered.SeamFaceViewer = 1f;
            var eye = new Vector3(0.3f, 0.2f, 1f).normalized * (sites.Radius * 20f);

            for (int seed = 1; seed <= 5; seed++)
            {
                var walker = new ChargeSeamWalker(sites, seed, 0f);
                var legs = Walk(walker, steered, 0f, 120f, _ => eye).Skip(8).ToList();  // from wherever it started, it comes round first
                var free = new ChargeSeamWalker(sites, seed, 0f);
                var freeLegs = Walk(free, steered, 0f, 120f, _ => null).Skip(8).ToList();
                // measured offline over 40 random views (D:/ABLab/boltlab, 2026-11-12): 100 % of seams at least partly in
                // sight and 94 % wholly, against 59 % partly when the walk ignores the viewer
                Assert.Greater(Share(sites, walker, legs, eye, whole: false), 0.99f, $"seed {seed}: every seam the bolt runs is at least partly in sight");
                Assert.Greater(Share(sites, walker, legs, eye, whole: true), 0.85f, $"seed {seed}: most seams are wholly in sight");
                Assert.Less(Share(sites, free, freeLegs, eye, whole: false), 0.75f, $"seed {seed}: control - unsteered, many seams are out of sight");

                int circling = 0;
                for (int i = 1; i < legs.Count; i++)
                {
                    ChargeSeam a = sites.Seams[legs[i - 1].Seam], b = sites.Seams[legs[i].Seam];
                    Assert.AreEqual(legs[i - 1].Forward ? a.B : a.A, legs[i].Forward ? b.A : b.B, "each seam starts where the last ended");
                    Assert.AreNotEqual(legs[i - 1].Seam, legs[i].Seam, "never straight back");
                    if (i < 4) continue;
                    for (int s = 0; s < ChargeSites.SiteCount; s++)
                        if (Enumerable.Range(i - 4, 5).All(j => sites.Seams[legs[j].Seam].Touches(s))) circling++;
                }
                // the no-circling rule yields only when every way out would leave the viewer's sight
                Assert.Less(circling, legs.Count * 0.06f, $"seed {seed}: a fifth seam round one bloom stays rare");
            }
        }

        [Test]
        public void SeamWalk_WhenTheViewerGoesRoundTheBack_TheBoltComesRoundWithinTwoSeconds()
        {
            var mesh = ModelMesh(ChargeFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1f, out var sites, out var problem), problem);
            var steered = ChargeArcSettings.Lab;
            steered.SeamFaceViewer = 1f;
            var front = new Vector3(0.3f, 0.2f, 1f).normalized * (sites.Radius * 20f);
            for (int seed = 1; seed <= 5; seed++)
            {
                var walker = new ChargeSeamWalker(sites, seed, 0f);
                Walk(walker, steered, 0f, 30f, _ => front);
                // the viewer jumps to the far side at t = 30; the walk has planned up to a second ahead with the old view
                var legs = Walk(walker, steered, 30f, 60f, _ => -front);
                var struckAfter = legs.Where(l => l.Struck >= 32f).ToList();
                Assert.Greater(struckAfter.Count, 20);
                Assert.IsTrue(struckAfter.All(l => walker.Facing(sites.Seams[l.Seam].A, -front) > 0f || walker.Facing(sites.Seams[l.Seam].B, -front) > 0f),
                    $"seed {seed}: two seconds after the viewer moved, every seam touches the viewer's side");
            }
        }

        [Test]
        public void SeamWalk_WithNoViewer_IsTheWalkItAlwaysWas()
        {
            var mesh = ModelMesh(ChargeFbxPath);
            Assert.IsTrue(ChargeSites.TryBuild(mesh.vertices, mesh.triangles, 1f, out var sites, out var problem), problem);
            var steered = ChargeArcSettings.Lab;
            steered.SeamFaceViewer = 1f;
            var old = Walk(new ChargeSeamWalker(sites, 7, 0f), ChargeArcSettings.Lab, 0f, 60f, _ => null);
            var noEye = Walk(new ChargeSeamWalker(sites, 7, 0f), steered, 0f, 60f, _ => null);
            var zeroShare = Walk(new ChargeSeamWalker(sites, 7, 0f), ChargeArcSettings.Lab, 0f, 60f, _ => Vector3.forward * 100f);
            CollectionAssert.AreEqual(old.Select(l => l.Seam), noEye.Select(l => l.Seam), "no eye: the same walk");
            CollectionAssert.AreEqual(old.Select(l => l.Seam), zeroShare.Select(l => l.Seam), "share 0: the same walk");
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
