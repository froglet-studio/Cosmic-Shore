#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// What a HELD gun costs per round, pinned for the two vessels that fire the
    /// <c>SparrowProjectile</c> by the hundred: the Sparrow's full-auto (two muzzles at 90
    /// volleys/s) and the Brittle Star's turrets (up to five guns at 90/s, ~2000 rounds in the
    /// air). Each test guards one per-ROUND cost that scaled with the trigger and lagged the game
    /// while it was held:
    /// <list type="bullet">
    /// <item>a swept round is not a collider-LOD focus (the LOD manager pays per prism PER focus);</item>
    /// <item>the per-round launch one-shot is burst-throttled like the prism-break sounds;</item>
    /// <item>the round's rigidbody is kinematic (it is moved by transform, never simulated).</item>
    /// </list>
    /// Plus the readability fact the Sparrow report raised: the round is DRAWN at least as wide
    /// as the sphere it hits with.
    ///
    /// <para>Under an <c>Editor/</c> folder per CLAUDE.md.</para>
    /// </summary>
    public class ProjectileFireCostTests
    {
        const string SparrowRoundPrefab = "Assets/_Prefabs/Projectile/SparrowProjectile.prefab";
        const string SparrowRoundMaterial = "Assets/_Graphics/Materials/SparrowProjectileMaterial.mat";

        static List<Transform> Foci()
        {
            var field = typeof(PrismColliderLodManager).GetField("s_foci",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "PrismColliderLodManager.s_foci was renamed - update this test");
            return (List<Transform>)field.GetValue(null);
        }

        static void Call(Object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, null);

        static void SetSwept(Projectile projectile, bool swept) =>
            typeof(Projectile).GetField("sweptPrismDetection", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(projectile, swept);

        [Test]
        public void SweptRoundIsNotAColliderLodFocus_AndATriggerRoundStillIs()
        {
            var go = new GameObject("round");
            try
            {
                var projectile = go.AddComponent<Projectile>();

                SetSwept(projectile, true);
                Call(projectile, "OnEnable");
                Assert.IsFalse(Foci().Contains(go.transform),
                    "a swept round reads prisms from PrismSpatialIndex and must not cost the " +
                    "collider-LOD manager a focus (it loops every focus per prism)");
                Call(projectile, "OnDisable");

                // Negative control: a round that meets prisms through PhysX still wakes the
                // colliders on its path, and still lets go of them on return.
                SetSwept(projectile, false);
                Call(projectile, "OnEnable");
                Assert.IsTrue(Foci().Contains(go.transform),
                    "a trigger round must keep the prism colliders on its path awake");
                Call(projectile, "OnDisable");
                Assert.IsFalse(Foci().Contains(go.transform), "a returned round must release its focus");
            }
            finally
            {
                Foci().Remove(go.transform);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ProjectileLaunchSoundIsBurstThrottled()
        {
            var go = new GameObject("audio");
            try
            {
                var audio = go.AddComponent<AudioSystem>();
                var field = typeof(AudioSystem).GetField("throttledCategories",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(field, "AudioSystem.throttledCategories was renamed - update this test");
                var categories = (GameplaySFXCategory[])field.GetValue(audio);
                CollectionAssert.Contains(categories, GameplaySFXCategory.ProjectileLaunch,
                    "Projectile.LaunchProjectile plays one per ROUND - a held full-auto starts " +
                    "hundreds of FMOD instances a second without the throttle");
                // The throttle's existing members stay throttled.
                CollectionAssert.Contains(categories, GameplaySFXCategory.BlockDestroy);
            }
            finally { Object.DestroyImmediate(go); }

            // The default only ships while no AudioSystem serializes its own list over it.
            var overrides = Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".prefab") || p.EndsWith(".unity"))
                .Where(p => File.ReadAllText(p).Contains("throttledCategories"))
                .ToList();
            foreach (var path in overrides)
                StringAssert.Contains("ProjectileLaunch", File.ReadAllText(path),
                    $"{path} serializes throttledCategories - make sure ProjectileLaunch (5) is in it");
        }

        [Test]
        public void SparrowRoundIsKinematic_AndDrawnAtItsHitWidth()
        {
            string prefab = File.ReadAllText(SparrowRoundPrefab);
            StringAssert.Contains("m_IsKinematic: 1", prefab,
                "the round is moved by transform every frame; a dynamic body is simulated and " +
                "written back for nothing");

            // The hit sphere: radius x the largest axis of the root scale (PhysX's rule, and
            // Projectile.MeasureHitRadius's).
            var scale = Regex.Match(prefab, @"m_LocalScale: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+)\}");
            float sx = F(scale.Groups[1].Value), sz = F(scale.Groups[3].Value);
            float hitRadius = F(Regex.Match(prefab, @"m_Radius: ([\d.]+)").Groups[1].Value) * Mathf.Max(sx, sz);

            // The drawn radius: the unit sphere mesh (0.5) at the root's cross-section, pushed out
            // by the material's _Spread in WORLD units (SpreadFresnelShader divides it by scale).
            string mat = File.ReadAllText(SparrowRoundMaterial);
            var spread = Regex.Match(mat, @"_Spread: \{r: ([\d.]+)");
            Assert.IsTrue(spread.Success, "SparrowProjectileMaterial no longer overrides _Spread " +
                          "(its parent's is 0.01: a needle thinner than what it hits)");
            float drawnRadius = 0.5f * sx + F(spread.Groups[1].Value);

            Assert.GreaterOrEqual(drawnRadius, 0.95f * hitRadius,
                $"the round is drawn {2 * drawnRadius:0.00} u wide but hits {2 * hitRadius:0.00} u wide - " +
                "seen end-on from the chase camera it disappears");
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
#endif
