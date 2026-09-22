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

    /// <summary>
    /// The A/B half of the census: switch OFF every enabled renderer whose shared material name
    /// starts with a prefix, then switch exactly those back on. A census says who is in the
    /// culling population; only removing them says what they COST.
    ///
    /// The first reading of the Lattice boot world put ~40,000 of 45,197 enabled renderers on
    /// the eight <c>SpindleMaterial_Phase*</c> variants, with 4,599 visible — so the hypothesis
    /// "culling walks every renderer and most of them are spindles nobody can see" is testable
    /// in one keystroke: <c>renderers hide Spindle</c>, read CPU, <c>renderers show</c>.
    ///
    /// Diagnostic only, and deliberately blunt: it writes <c>Renderer.enabled</c>, the property
    /// the spindle lifecycle also writes. A spindle that withers while hidden is destroyed as
    /// normal; one GROWN while hidden is visible (reported as drift on <c>show</c>). It restores
    /// only what it switched off, so it can never light a renderer something else disabled.
    /// </summary>
    public static class RendererHideSwitch
    {
        static readonly List<Renderer> s_hidden = new();
        static string s_prefix;

        public static int HiddenCount => s_hidden.Count;

        /// <summary>Pure: does a material name belong to the hidden group? Ordinal, case-insensitive.</summary>
        public static bool Matches(string materialName, string prefix) =>
            !string.IsNullOrEmpty(prefix) && materialName != null &&
            materialName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        public static string Hide(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return "usage: renderers hide <material-name-prefix>   e.g. renderers hide Spindle";
            if (s_hidden.Count > 0) return $"already hiding {s_hidden.Count:N0} '{s_prefix}*' renderers — 'renderers show' first";

            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            // Name each material once, not once per renderer: 40k .name reads are 40k strings.
            var verdict = new Dictionary<Material, bool>(64);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (!r.enabled) continue;
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (!verdict.TryGetValue(m, out bool hit)) verdict[m] = hit = Matches(m.name, prefix);
                if (!hit) continue;
                r.enabled = false;
                s_hidden.Add(r);
            }
            s_prefix = prefix;
            return $"hid {s_hidden.Count:N0} renderers on '{prefix}*' ({all.Length:N0} scanned). " +
                   "Wait ~5 s, read CPU (busy) and Frame Time, then 'renderers show'.";
        }

        public static string Show()
        {
            if (s_hidden.Count == 0) return "nothing hidden";
            int restored = 0;
            for (int i = 0; i < s_hidden.Count; i++)
            {
                var r = s_hidden[i];
                if (r == null) continue; // destroyed while hidden (a withered spindle) — nothing to restore
                r.enabled = true;
                restored++;
            }
            int gone = s_hidden.Count - restored;
            string prefix = s_prefix;
            s_hidden.Clear();
            s_prefix = null;
            return $"restored {restored:N0} '{prefix}*' renderers" + (gone > 0 ? $" ({gone:N0} were destroyed while hidden)" : "");
        }

        /// <summary>Put everything back — a hidden world must never survive the HUD that hid it.</summary>
        public static void ShowIfHidden() { if (s_hidden.Count > 0) Show(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { s_hidden.Clear(); s_prefix = null; }
    }
}
#endif
