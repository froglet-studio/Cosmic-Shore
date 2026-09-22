#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// One-shot count of every <see cref="Renderer"/> on an active GameObject — the population
    /// the camera's culling pass walks every frame.
    ///
    /// Why it exists: a Profiler capture of the Lattice boot world put
    /// <c>CullScriptable → EndRenderQueueExtraction</c> at 5.6 ms and the main thread 14 ms
    /// IDLE waiting on render jobs, while draw submission cost ~0.2 ms in total. Both of those
    /// scale with the number of renderers in the scene, not with draw calls or entities — and
    /// nothing on screen said how many there were. Prisms are NOT in that number: on the
    /// instanced path their GameObject renderer is disabled and Entities Graphics culls them
    /// separately, so they land in <see cref="Disabled"/>. What IS in it is everything hung off
    /// the prisms as a GameObject (flora spindles, cytoplasm shards, crystals).
    ///
    /// On demand only (console <c>renderers</c>, and once at the end of a saved diagnostic):
    /// <c>FindObjectsByType</c> over tens of thousands of objects is a spike in its own right,
    /// so sampling it periodically would corrupt the very frame times the HUD is reporting.
    /// </summary>
    [Serializable]
    public class RendererCensus
    {
        /// <summary>Renderers on active GameObjects that are enabled — i.e. the culling population.</summary>
        public int enabled;
        /// <summary>Renderers on active GameObjects that are disabled (instanced prisms land here).</summary>
        public int disabled;
        /// <summary><c>Renderer.isVisible</c> — drawn by ANY camera last frame, Scene view included.</summary>
        public int visible;

        public int mesh, skinned, particle, trail, line, other;

        /// <summary>Largest groups of ENABLED renderers by shared-material name, "name=count", descending.</summary>
        public string[] topMaterials;

        public float takenAt;

        public const int TopMaterialCount = 8;

        public static RendererCensus Take()
        {
            var all = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var c = new RendererCensus { takenAt = Time.unscaledTime };
            var byMaterial = new Dictionary<Material, int>(64);
            int noMaterial = 0;

            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (!r.enabled) { c.disabled++; continue; }
                c.enabled++;
                if (r.isVisible) c.visible++;

                switch (r)
                {
                    case SkinnedMeshRenderer _: c.skinned++; break;
                    case MeshRenderer _: c.mesh++; break;
                    case ParticleSystemRenderer _: c.particle++; break;
                    case TrailRenderer _: c.trail++; break;
                    case LineRenderer _: c.line++; break;
                    default: c.other++; break;
                }

                // Keyed by reference, named only for the winners: 50k .name reads would be
                // 50k string allocations for a table that prints eight rows.
                var m = r.sharedMaterial;
                if (m == null) { noMaterial++; continue; }
                byMaterial.TryGetValue(m, out int n);
                byMaterial[m] = n + 1;
            }

            var named = new Dictionary<string, int>(byMaterial.Count + 1);
            foreach (var kv in byMaterial)
            {
                string key = kv.Key.name;
                named.TryGetValue(key, out int n);
                named[key] = n + kv.Value;
            }
            if (noMaterial > 0) named["<no material>"] = noMaterial;

            c.topMaterials = Top(named, TopMaterialCount);
            return c;
        }

        /// <summary>Pure: the <paramref name="count"/> largest entries as "name=n", largest first, ties by name.</summary>
        public static string[] Top(IReadOnlyDictionary<string, int> groups, int count)
        {
            var list = new List<KeyValuePair<string, int>>(groups);
            list.Sort((a, b) => b.Value != a.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            int n = Math.Min(Math.Max(count, 0), list.Count);
            var result = new string[n];
            for (int i = 0; i < n; i++) result[i] = list[i].Key + "=" + list[i].Value;
            return result;
        }

        /// <summary>The one-line HUD value.</summary>
        public string Summary() =>
            $"{enabled:N0} on · {disabled:N0} off · {visible:N0} visible";

        /// <summary>The console answer: counts, type split, and the material groups that dominate.</summary>
        public string Describe() =>
            $"{Summary()} | mesh {mesh:N0} · skinned {skinned:N0} · particle {particle:N0} · " +
            $"trail {trail:N0} · line {line:N0} · other {other:N0} | top: " +
            (topMaterials is { Length: > 0 } ? string.Join(", ", topMaterials) : "none");
    }
}
#endif
