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
    /// These pin the four promises that change made: the ring is the SIZE of the crystal it replaced,
    /// it stands SQUARE across the track, it keeps the crystal's DOMAIN rule, and the scene actually
    /// spawns it.
    /// </summary>
    public class SkimRaceRingCrystalTests
    {
        const string RingPrefabPath = "Assets/_Prefabs/Environment/SkimRaceRingCrystal.prefab";
        const string ReplacedCrystalPath = "Assets/_Prefabs/Environment/BigCrystalVariant.prefab";
        const string ScenePath = "Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity";

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
        public void RingPrefab_IsABodylessTeamCrystalWithItsRingWired()
        {
            var prefab = LoadPrefab(RingPrefabPath);

            Assert.IsTrue(prefab.TryGetComponent(out Crystal crystal), "no Crystal on the ring prefab");
            Assert.IsFalse(crystal.HasBody,
                "the ring crystal must carry no crystal model - its body is the ring");
            Assert.IsFalse(prefab.GetComponentInChildren<Collider>(true),
                "a collider would collect the crystal by touch; the ring is collected by threading only");
            Assert.IsFalse(prefab.GetComponentInChildren<Renderer>(true),
                "a renderer on the prefab would draw something other than the ring");

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
        public void RingMouth_IsTheSizeOfTheCrystalItReplaced()
        {
            // The crystal's pickup sphere, in world units: what a pilot had to hit.
            var replaced = LoadPrefab(ReplacedCrystalPath);
            Assert.IsTrue(replaced.TryGetComponent(out SphereCollider sphere),
                $"{ReplacedCrystalPath} lost its pickup sphere - re-derive the ring's mouth radius");
            var scale = replaced.transform.lossyScale;
            float crystalRadius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

            var ring = LoadPrefab(RingPrefabPath).GetComponent<CrystalRaceRing>();
            Assert.AreEqual(crystalRadius, ring.MouthRadius, 1e-3f,
                "the ring's mouth must match the replaced crystal's pickup radius so the target size is unchanged");
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
