#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Skim Race's crystals are drawn and collected as race rings (<see cref="CrystalRaceRing"/>).
    /// These pin the promises that change made: at intensity 1 the ring is the SIZE of the crystal it
    /// replaced and at intensity 4 it is half that, with 2 and 3 at even steps between; the omni
    /// crystal in its middle is 20% larger than a level-1 elemental crystal; it stands SQUARE across the track, it keeps
    /// the crystal's DOMAIN rule, and the scene actually spawns it.
    /// </summary>
    public class SkimRaceRingCrystalTests
    {
        const string RingPrefabPath = "Assets/_Prefabs/Environment/SkimRaceRingCrystal.prefab";
        const string ReplacedCrystalPath = "Assets/_Prefabs/Environment/BigCrystalVariant.prefab";
        const string ScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity";
        const string ChargeCrystalPath = "Assets/_Prefabs/Environment/CrystalCharge.prefab";

        readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} did not load");
            return prefab;
        }

        // ── Wiring ────────────────────────────────────────────────────────

        [Test]
        public void RingPrefab_IsAnOmniTeamCrystalWithItsRingWired()
        {
            var prefab = LoadPrefab(RingPrefabPath);

            Assert.IsTrue(prefab.TryGetComponent(out Crystal crystal), "no Crystal on the ring prefab");
            Assert.IsTrue(crystal.HasBody,
                "the ring carries a small omni crystal in its middle - the crystal's own models");
            Assert.AreEqual(Element.Omni, crystal.crystalProperties.Element,
                "the crystal in the ring must be an OMNI crystal - it pays what an omni crystal pays");
            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
                Assert.IsFalse(col.enabled,
                    $"'{col.name}' would collect the crystal by touch; the ring is collected by threading only");

            // TeamCrystalImpactor, not the omni one: Skim Race's crystals are strictly per domain.
            Assert.IsTrue(prefab.TryGetComponent(out TeamCrystalImpactor impactor),
                "the ring must keep Skim Race's strict domain rule (TeamCrystalImpactor)");
            Assert.AreSame(crystal, impactor.Crystal, "the impactor must point at its own crystal");

            Assert.IsTrue(prefab.TryGetComponent(out CrystalRaceRing ring), "no CrystalRaceRing on the prefab");
            var so = new SerializedObject(ring);
            Assert.AreSame(crystal, so.FindProperty("crystal").objectReferenceValue);
            Assert.AreSame(impactor, so.FindProperty("impactor").objectReferenceValue);
        }

        [Test]
        public void RingMouth_AtIntensityOne_IsTheSizeOfTheCrystalItReplaced()
        {
            // The crystal's pickup sphere, in world units: what a pilot had to hit.
            var replaced = LoadPrefab(ReplacedCrystalPath);
            Assert.IsTrue(replaced.TryGetComponent(out SphereCollider sphere),
                $"{ReplacedCrystalPath} lost its pickup sphere - re-derive the ring's mouth radius");
            var scale = replaced.transform.lossyScale;
            float crystalRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            var ring = LoadPrefab(RingPrefabPath).GetComponent<CrystalRaceRing>();
            var so = new SerializedObject(ring);
            Assert.AreEqual(crystalRadius, so.FindProperty("intensityOneMouthRadius").floatValue, 1e-3f,
                "the intensity-1 mouth must match the replaced crystal's pickup radius so the target size is unchanged");
            Assert.AreEqual(0.5f, so.FindProperty("intensityFourMouthFraction").floatValue, 1e-5f,
                "the intensity-4 ring is half the size of the intensity-1 ring");
        }

        [Test]
        public void RingMouth_HalvesByIntensityFourInEvenSteps()
        {
            Assert.AreEqual(24f, CrystalRaceRing.MouthRadiusFor(24f, 0.5f, 1), 1e-4f);
            Assert.AreEqual(20f, CrystalRaceRing.MouthRadiusFor(24f, 0.5f, 2), 1e-4f);
            Assert.AreEqual(16f, CrystalRaceRing.MouthRadiusFor(24f, 0.5f, 3), 1e-4f);
            Assert.AreEqual(12f, CrystalRaceRing.MouthRadiusFor(24f, 0.5f, 4), 1e-4f,
                "intensity 4 is 50% of intensity 1");
            Assert.AreEqual(12f, CrystalRaceRing.MouthRadiusFor(24f, 0.5f, 5), 1e-4f,
                "an intensity past the ladder holds intensity 4's size");
        }

        /// <summary>
        /// Half-extent of a crystal's BODY - its first model, the solid that is its silhouette - per
        /// unit of the crystal's ROOT scale. The omni's other models are overlays (the Shepard
        /// triangles' bounds cover their animated sweep, not what sits on screen), so the body is
        /// the comparable number.
        /// </summary>
        static float BodyHalfExtentPerRootScale(Crystal crystal)
        {
            Assert.IsTrue(crystal.CrystalModels is { Count: > 0 } && crystal.CrystalModels[0]?.model,
                $"'{crystal.name}' has no body model to measure");
            var body = crystal.CrystalModels[0].model;
            Mesh mesh = body.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh
                      : body.TryGetComponent(out SkinnedMeshRenderer smr) ? smr.sharedMesh : null;
            Assert.IsNotNull(mesh, $"'{crystal.name}' body '{body.name}' draws no mesh");

            var e = mesh.bounds.extents;
            var s = body.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(e.x * s.x), Mathf.Abs(e.y * s.y), Mathf.Abs(e.z * s.z));
            return extent / Mathf.Abs(crystal.transform.lossyScale.x);
        }

        [Test]
        public void CrystalInTheRing_IsTwentyPercentLargerThanALevelOneElementalCrystal()
        {
            // The ring's reference is an elemental crystal at ROOT SCALE 1 - the size a one-petal
            // crystal was before every elemental crystal became ElementalCrystalSetSO.CrystalWorldScale
            // (the ring's omni kept its size). The Charge model is the reference the other elements'
            // model corrections are equalized to.
            var charge = LoadPrefab(ChargeCrystalPath).GetComponent<Crystal>();
            float levelOneHalfExtent = BodyHalfExtentPerRootScale(charge) * 1f;
            Assert.Greater(levelOneHalfExtent, 0f, "could not measure the Charge crystal's mesh");

            var prefab = LoadPrefab(RingPrefabPath);
            var ring = prefab.GetComponent<CrystalRaceRing>();
            float omniPerScale = BodyHalfExtentPerRootScale(prefab.GetComponent<Crystal>());

            // The reference scale really is level-1 size...
            Assert.AreEqual(levelOneHalfExtent, omniPerScale * ring.LevelOneCrystalScale, levelOneHalfExtent * 0.05f,
                $"at LevelOneCrystalScale the omni crystal draws {omniPerScale * ring.LevelOneCrystalScale * 2f:F2} u " +
                $"across; a level-1 elemental crystal draws {levelOneHalfExtent * 2f:F2} u");
            // ...and the crystal in the ring is 20% larger than that.
            Assert.AreEqual(1.2f, ring.CrystalSizeMultiplier, 1e-4f);
            Assert.AreEqual(levelOneHalfExtent * 1.2f, omniPerScale * ring.CrystalWorldScale, levelOneHalfExtent * 0.06f);
            Assert.AreEqual(ring.CrystalWorldScale, prefab.transform.localScale.x, 1e-4f,
                "the prefab's root scale should preview the size CrystalRaceRing applies at runtime");
        }

        [Test]
        public void SkimRaceScene_SpawnsTheRingCrystal()
        {
            string guid = AssetDatabase.AssetPathToGUID(RingPrefabPath);
            Assert.IsFalse(string.IsNullOrEmpty(guid), "ring prefab has no guid");

            string scene = File.ReadAllText(ScenePath);
            var refs = Regex.Matches(scene, @"crystalPrefab:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-f]{32})");
            Assert.AreEqual(1, refs.Count, "expected exactly one crystal manager in the Skim Race scene");
            Assert.AreEqual(guid, refs[0].Groups[1].Value,
                "the Skim Race crystal manager must spawn SkimRaceRingCrystal");
        }

        // ── Domain ────────────────────────────────────────────────────────

        [Test]
        public void TeamRing_AcceptsOnlyItsOwnDomain()
        {
            var go = new GameObject("RingCrystal");
            _spawned.Add(go);
            var crystal = go.AddComponent<Crystal>();
            var impactor = go.AddComponent<TeamCrystalImpactor>();
            impactor.Crystal = crystal;

            crystal.ownDomain = Domains.Jade;
            Assert.IsTrue(impactor.AcceptsDomain(Domains.Jade));
            Assert.IsFalse(impactor.AcceptsDomain(Domains.Ruby));
            Assert.IsFalse(impactor.AcceptsDomain(Domains.Gold));
        }

        [Test]
        public void OwnedRing_PaintsItsOwnerAndNeutralRingStaysBlue()
        {
            var owned = BuildRing(Vector3.zero, Vector3.forward, 24f, Domains.Ruby);
            Assert.AreEqual(Domains.Ruby, owned.Owner);

            var neutral = BuildRing(Vector3.zero, Vector3.forward, 24f, Domains.Blue);
            Assert.AreEqual(Domains.Blue, neutral.Owner);

            owned.SetOwner(Domains.Gold);
            Assert.AreEqual(Domains.Gold, owned.Owner);
        }

        // ── Square across the track ──────────────────────────────────────

        /// <summary>A closed circle of radius <paramref name="r"/> in the XZ plane, flown counter-clockwise.</summary>
        static SkimRaceCourse CircleCourse(float r, int samples = 256)
        {
            var pts = new List<Vector3>(samples);
            var nrm = new List<Vector3>(samples);
            for (int i = 0; i < samples; i++)
            {
                float a = i * Mathf.PI * 2f / samples;
                pts.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                nrm.Add(Vector3.up);
            }
            return new SkimRaceCourse(pts, nrm);
        }

        [Test]
        public void TrackAxis_IsTheTrackTangentWhereverTheJitterPutsTheRing()
        {
            const float r = 700f;
            var course = CircleCourse(r);

            // The crystal manager jitters each anchor by up to 35 u on a random sphere.
            var jitters = new[] { Vector3.zero, new Vector3(0f, 35f, 0f), new Vector3(30f, -15f, 10f),
                                  new Vector3(-20f, 20f, -25f) };
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI * 2f / 16f + 0.1f;
                Vector3 onTrack = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));

                foreach (var j in jitters)
                {
                    int hint = -1;
                    Vector3 axis = CrystalRaceRing.TrackAxisAt(course, onTrack + j, ref hint);
                    float angle = Vector3.Angle(axis, tangent);
                    Assert.Less(angle, 2f,
                        $"ring at {a * Mathf.Rad2Deg:F0} deg + jitter {j} faces {angle:F2} deg off the track");
                }
            }
        }

        [Test]
        public void PilotFlyingDownTheTrack_ThreadsTheRing_FlyingAcrossItDoesNot()
        {
            const float r = 700f;
            var course = CircleCourse(r);
            float a = 0.8f;
            Vector3 centre = new Vector3(Mathf.Cos(a) * r, 12f, Mathf.Sin(a) * r);
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

            int hint = -1;
            var ring = BuildRing(centre, CrystalRaceRing.TrackAxisAt(course, centre, ref hint), 24f, Domains.Jade);

            // Straight down the track, a little off-centre but inside the mouth: threaded.
            Vector3 offset = outward * 15f;
            Assert.IsTrue(ring.CrossedMouth(centre + offset - tangent * 6f, centre + offset + tangent * 6f),
                "a pilot flying down the track through the mouth must thread the ring");

            // Down the track but outside the mouth: not threaded.
            Vector3 wide = outward * 30f;
            Assert.IsFalse(ring.CrossedMouth(centre + wide - tangent * 6f, centre + wide + tangent * 6f),
                "passing beside the ring must not thread it");

            // Across the track, through the ring's centre: that is flying along its EDGE.
            Assert.IsFalse(ring.CrossedMouth(centre - outward * 6f, centre + outward * 6f),
                "a ring square across the track shows its edge to a pilot crossing the track");
        }

        RaceGateRing BuildRing(Vector3 position, Vector3 axis, float radius, Domains owner)
        {
            var go = new GameObject("RaceRing");
            _spawned.Add(go);
            var ring = go.AddComponent<RaceGateRing>();
            ring.Build(0, new RaceGate(position, axis, radius), null, 0f, owner: owner);
            return ring;
        }
    }
}
#endif
