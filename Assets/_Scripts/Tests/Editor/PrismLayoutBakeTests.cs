using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Unity half of the cell-environment layout bakes (<see cref="PrismLayoutBakeFormat"/>,
    /// <c>Tools/Build/bake_prism_layouts.py</c>, Docs/ECOSYSTEM.md §62.3).
    ///
    /// <para>The bakes are made OFFLINE: the shipped generators run against the card-art harness's
    /// engine shim, and <c>bake_prism_layouts.py --check</c> proves each bake is what that run
    /// produces. Two things can only be proven here, inside Unity. (1) Every baked world's prefab,
    /// as Unity deserializes it, fingerprints to a bake in Resources: the harness read its fields
    /// the way Unity does, so the game finds the bake instead of generating live. (2) The bake is
    /// the layout Unity's own math generates: the shim's Quaternion and Mathf agree with the
    /// engine's to well inside the fit margins. A failure of (2) by a few thousandths is float
    /// noise between the two engines; a failure by whole units means the shim computes something
    /// differently, and the bake (which the clip audit proved flush) is not what this Unity would
    /// have laid.</para>
    /// </summary>
    public class PrismLayoutBakeTests
    {
        const float PositionTolerance = 0.05f;
        const float ScaleTolerance = 1e-3f;      // relative
        const float RotationDotTolerance = 1e-4f;

        static readonly PropertyInfo BakesLayout = typeof(CellEnvironmentSpawnableBase)
            .GetProperty("BakesLayout", BindingFlags.Instance | BindingFlags.NonPublic);

        static List<(string path, CellEnvironmentSpawnableBase env)> BakedWorlds()
        {
            var worlds = new List<(string, CellEnvironmentSpawnableBase)>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || !go.TryGetComponent(out CellEnvironmentSpawnableBase env)) continue;
                if ((bool)BakesLayout.GetValue(env)) worlds.Add((path, env));
            }
            return worlds;
        }

        [Test]
        public void EveryBakedWorldFindsItsBake()
        {
            var worlds = BakedWorlds();
            Assert.That(worlds, Is.Not.Empty, "no baked cell-environment prefab found under Assets/_Prefabs");
            var missing = worlds
                .Where(w => Resources.Load<TextAsset>(PrismLayoutBakeFormat.ResourcePath(w.env.GetType(),
                    PrismLayoutBakeFormat.Fingerprint(w.env))) == null)
                .Select(w => $"{w.path} ({PrismLayoutBakeFormat.FileStem(w.env.GetType(), PrismLayoutBakeFormat.Fingerprint(w.env))})")
                .ToList();
            Assert.That(missing, Is.Empty,
                "these prefabs have no bake for the fields Unity loads (re-bake with python3 Tools/Build/bake_prism_layouts.py; " +
                "if the bake exists under another fingerprint, the harness reads a field differently from Unity):\n  " +
                string.Join("\n  ", missing));
        }

        [Test]
        public void EveryBakeIsTheLayoutUnityGenerates()
        {
            var failures = new List<string>();
            foreach (var (path, env) in BakedWorlds())
            {
                ulong fingerprint = PrismLayoutBakeFormat.Fingerprint(env);
                var asset = Resources.Load<TextAsset>(PrismLayoutBakeFormat.ResourcePath(env.GetType(), fingerprint));
                if (asset == null) continue;   // EveryBakedWorldFindsItsBake reports it
                Assert.IsTrue(PrismLayoutBakeFormat.TryDecode(asset.bytes, fingerprint, out var baked, out var bakedSites),
                    $"{asset.name} does not decode");

                List<PrismLay> live;
                List<FloraPlantingSite> liveSites;
                env.InvalidateCache();
                using (PrismLayoutBake.Bypass())
                {
                    env.GetTrailData();
                    live = env.CachedLays.ToList();
                    liveSites = env.PlantingSites.ToList();
                }
                env.ReleaseGeneratedData();

                if (live.Count != baked.Count || liveSites.Count != bakedSites.Count)
                {
                    failures.Add($"{path}: Unity generates {live.Count} prisms / {liveSites.Count} sites, the bake holds " +
                                 $"{baked.Count} / {bakedSites.Count}");
                    continue;
                }
                float worstPos = 0f, worstScale = 0f, worstDot = 0f;
                int kinds = 0;
                for (int i = 0; i < live.Count; i++)
                {
                    var a = live[i].Point;
                    var b = baked[i].Point;
                    worstPos = Mathf.Max(worstPos, Vector3.Distance(a.Position, b.Position));
                    var d = a.Scale - b.Scale;
                    float rel = Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z))) /
                                Mathf.Max(1f, a.Scale.magnitude);
                    worstScale = Mathf.Max(worstScale, rel);
                    worstDot = Mathf.Max(worstDot, 1f - Mathf.Abs(Quaternion.Dot(a.Rotation, b.Rotation)));
                    if (live[i].Domain != baked[i].Domain || live[i].Kind != baked[i].Kind) kinds++;
                }
                TestContext.WriteLine($"{path}: worst position {worstPos:g3} u, scale {worstScale:g3}, rotation 1-dot {worstDot:g3}");
                if (worstPos > PositionTolerance || worstScale > ScaleTolerance || worstDot > RotationDotTolerance || kinds > 0)
                    failures.Add($"{path}: worst position {worstPos:g3} u, scale {worstScale:g3} (relative), rotation " +
                                 $"1-dot {worstDot:g3}, {kinds} domain/kind mismatches");
            }
            Assert.That(failures, Is.Empty, "a bake differs from what Unity generates:\n  " + string.Join("\n  ", failures));
        }

        [Test]
        public void ABakeRoundTripsExactly()
        {
            var lays = new List<PrismLay>
            {
                new(new SpawnPoint(new Vector3(1.5f, -2f, 3.25f), Quaternion.Euler(10f, 20f, 30f), new Vector3(0.5f, 2f, 7.75f)),
                    Domains.Ruby, PrismKind.Shielded),
                new(new SpawnPoint(new Vector3(-100f, 0f, 1e-3f), Quaternion.identity, Vector3.one), Domains.Gold),
            };
            var sites = new List<FloraPlantingSite> { new(new Vector3(4f, 5f, 6f), Vector3.up, FloraSiteKind.Basket) };
            var data = PrismLayoutBakeFormat.Encode(0x1234UL, 0x5678UL, lays, sites);

            Assert.IsTrue(PrismLayoutBakeFormat.TryDecode(data, 0x1234UL, out var back, out var backSites));
            Assert.AreEqual(lays.Count, back.Count);
            for (int i = 0; i < lays.Count; i++)
            {
                Assert.AreEqual(lays[i].Point.Position, back[i].Point.Position);
                Assert.AreEqual(lays[i].Point.Rotation, back[i].Point.Rotation);
                Assert.AreEqual(lays[i].Point.Scale, back[i].Point.Scale);
                Assert.AreEqual(lays[i].Domain, back[i].Domain);
                Assert.AreEqual(lays[i].Kind, back[i].Kind);
            }
            Assert.AreEqual(FloraSiteKind.Basket, backSites[0].Kind);
            Assert.IsTrue(PrismLayoutBakeFormat.TryReadSourceHash(data, out ulong source));
            Assert.AreEqual(0x5678UL, source);

            Assert.IsFalse(PrismLayoutBakeFormat.TryDecode(data, 0x9999UL, out _, out _), "a bake of other parameters was accepted");
            Assert.IsFalse(PrismLayoutBakeFormat.TryDecode(data.Take(data.Length - 1).ToArray(), 0x1234UL, out _, out _),
                "a truncated bake was accepted");
        }

        [Test]
        public void TheFingerprintFollowsTheGeneratorsFields()
        {
            var go = new GameObject("fingerprint probe");
            try
            {
                var env = go.AddComponent<SpawnableBoneyard>();
                ulong before = PrismLayoutBakeFormat.Fingerprint(env);
                Assert.AreEqual(before, PrismLayoutBakeFormat.Fingerprint(env), "the fingerprint is not stable");

                var so = new SerializedObject(env);
                so.FindProperty("hulkCount").intValue += 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                ulong afterField = PrismLayoutBakeFormat.Fingerprint(env);
                Assert.AreNotEqual(before, afterField, "a generator field did not move the fingerprint");

                so.FindProperty("seed").intValue += 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreNotEqual(afterField, PrismLayoutBakeFormat.Fingerprint(env), "the seed did not move the fingerprint");

                ulong beforeRuntime = PrismLayoutBakeFormat.Fingerprint(env);
                env.intensityLevel = 4;
                env.domain = Domains.Gold;
                Assert.AreEqual(beforeRuntime, PrismLayoutBakeFormat.Fingerprint(env),
                    "runtime-only base fields (intensity, domain) moved the fingerprint");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
