using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Named overrides for an Ark escort run, for tuning sweeps: the escort sim reads a flat JSON object of
    /// <c>"key": number</c> pairs from <c>-arkEscortParams path.json</c> (<see cref="ArkEscortSim"/>) and the run applies
    /// them at set-sail to its RUNTIME COPY of the escort (<see cref="ArkEscortRun"/> instantiates the asset first), never
    /// to an asset. Every applied, refused or unknown key is echoed in the run's JSON line
    /// (<see cref="ArkEscortReport.Params"/>), and the registered key patterns in <see cref="ArkEscortReport.ParamKeys"/>.
    ///
    /// <para>A REGISTRY of setters by key pattern, so other systems add their own by name later
    /// (<see cref="Register"/>): a pattern is dot-separated segments, one of which may be a <c>{placeholder}</c> that
    /// captures that segment of the key (<c>tier.{d}.population</c> matches <c>tier.4.population</c> and
    /// <c>tier.all.population</c>). Keys starting <c>ai.</c> go to <see cref="ExternalSetter"/> (the AI-pilot gene
    /// registry on its own branch hooks in there), so this compiles without it.</para>
    /// </summary>
    public static class ArkEscortParams
    {
        /// <summary>What a setter may write: the run's runtime copy of the escort, and the difficulty it sails.</summary>
        public sealed class Target
        {
            public ArkEscortSO Escort;
            public int Difficulty;

            /// <summary>Variety overrides: the voyage's seed (null = this run picks one), the modifiers to force (a bitmask
            /// over the escort's modifier list; -1 = roll them).</summary>
            public int? VoyageSeed;
            public int ModifierMask = -1;

            /// <summary>Per-species fauna overrides (<c>fauna.&lt;species&gt;.&lt;field&gt;</c>), handed to each escort world.</summary>
            public FaunaRuntimeTuning Fauna = new();

            /// <summary>Per-world fauna scales (<c>fauna.&lt;1-9|all&gt;.{populationScale, floorScale, capScale}</c>), by
            /// world ("all" or the 1-based leg number) and field.</summary>
            public readonly Dictionary<(string world, string field), float> World = new();

            /// <summary>Leg <paramref name="world1"/>'s (1-based) value for <paramref name="field"/>, else the "all" value,
            /// else <paramref name="fallback"/>.</summary>
            public float WorldValue(int world1, string field, float fallback)
            {
                if (World.TryGetValue((world1.ToString(CultureInfo.InvariantCulture), field), out float v)) return v;
                return World.TryGetValue(("all", field), out v) ? v : fallback;
            }
        }

        /// <summary>Apply <paramref name="value"/>; <paramref name="capture"/> is the placeholder's segment (or null).
        /// Return false to refuse (the key is echoed as not applied).</summary>
        public delegate bool Setter(Target target, string capture, float value);

        /// <summary>Hook for keys starting <c>ai.</c> (e.g. <c>ai.sparrow.engageRadius</c>): the AI-pilot gene registry
        /// sets it (<c>EscortGeneRegistry.TrySet</c>). Unset, those keys are echoed as not applied.</summary>
        public static Func<string, float, bool> ExternalSetter = EscortGeneRegistry.TrySet;

        /// <summary>The overrides the next escort applies at set-sail (the sim loads them once; they apply to every run).</summary>
        public static Dictionary<string, float> Pending;

        sealed class Entry
        {
            public string Pattern;
            public string[] Segments;
            public int Capture = -1;
            public Setter Set;
        }

        static readonly List<Entry> Entries = new();

        /// <summary>Every registered key pattern, plus <c>ai.*</c>.</summary>
        public static List<string> KeyPatterns()
        {
            var keys = new List<string>(Entries.Count + 1);
            foreach (var e in Entries) keys.Add(e.Pattern);
            foreach (var gene in EscortGeneRegistry.All) keys.Add(gene.Key);
            return keys;
        }

        /// <summary>Register (or replace) the setter for <paramref name="pattern"/>.</summary>
        public static void Register(string pattern, Setter set)
        {
            if (string.IsNullOrEmpty(pattern) || set == null) return;
            Entries.RemoveAll(e => e.Pattern == pattern);
            var segments = pattern.Split('.');
            int capture = -1;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i].StartsWith("{") && segments[i].EndsWith("}")) { capture = i; break; }
            Entries.Add(new Entry { Pattern = pattern, Segments = segments, Capture = capture, Set = set });
        }

        static ArkEscortParams()
        {
            // ── The Ark's pace (ArkEscortSO.ArkPacing; the tier's ArkSpeed scales crawl and entry on top) ──
            RegisterPacing("ark.cruiseSpeed", (ref ArkSpeedProfile p, float v) => p.Cruise = v);
            RegisterPacing("ark.entrySpeed", (ref ArkSpeedProfile p, float v) => p.Entry = v);
            RegisterPacing("ark.crawlSpeed", (ref ArkSpeedProfile p, float v) => p.Crawl = v);
            RegisterPacing("ark.membraneBand", (ref ArkSpeedProfile p, float v) => p.MembraneBand = v);
            RegisterPacing("ark.hullHalfLength", (ref ArkSpeedProfile p, float v) => p.HullHalfLength = v);
            RegisterPacing("ark.speedCeiling", (ref ArkSpeedProfile p, float v) => p.Ceiling = v);
            Register("ark.cellSpacing", (t, _, v) => { t.Escort.CellSpacing = v; return true; });
            Register("ark.platesAreFood", (t, _, v) => { t.Escort.PlatesAreFood = v > 0.5f; return true; });
            Register("escort.maxPopulation", (t, _, v) => { t.Escort.MaxPopulation = v; return true; });
            // ── Variety ──
            Register("voyage.seed", (t, _, v) => { t.VoyageSeed = Mathf.RoundToInt(v); return true; });
            Register("voyage.modifiers", (t, _, v) =>
            {
                if (v < -1f || v > int.MaxValue) return false;
                t.ModifierMask = Mathf.RoundToInt(v);
                return true;
            });
            Register("voyage.worlds", (t, _, v) =>
            {
                if (v < 0f || v > 20f) return false;
                t.Escort.WorldsPerVoyage = Mathf.RoundToInt(v);
                return true;
            });
            Register("escort.pacifyMinVolume", (t, _, v) =>
            {
                if (v < 0f || v > 1e8f) return false;
                t.Escort.PacifyMinVolume = v;
                return true;
            });
            Register("escort.pacifyNucleusSeconds", (t, _, v) =>
            {
                if (v < 0f || v > 600f) return false;
                t.Escort.PacifyNucleusSeconds = v;
                return true;
            });
            Register("escort.pacifyShare", (t, _, v) =>
            {
                if (v < 0f || v > 1f) return false;
                t.Escort.PacifyShare = v;
                return true;
            });

            // ── Tiers: tier.<1-9|all>.<field> ──
            RegisterTier("population", (tier, v) => tier.Population = Mathf.Max(0.05f, v));
            RegisterTier("regrowSeconds", (tier, v) => tier.RegrowSeconds = Mathf.Max(0.05f, v));
            RegisterTier("hostileShare", (tier, v) => tier.HostileShare = Mathf.Clamp01(v));
            RegisterTier("arkSpeed", (tier, v) => tier.ArkSpeed = Mathf.Max(0.25f, v));
            RegisterTier("breachHull", (tier, v) => tier.BreachHull = Mathf.Clamp(v, 0f, 0.95f));

            // ── Fauna: fauna.<species>.<field> (FaunaRuntimeTuning), and fauna.<world 1-9|all>.<scale> ──
            foreach (var field in FaunaRuntimeTuning.AllFields())
            {
                if (field == "floorScale" || field == "capScale") continue;
                string f = field;
                float max = FieldMax(f);
                Register("fauna.{species}." + f, (t, species, v) =>
                {
                    if (t.Fauna == null || IsWorld(species) && species != "all" || v < 0f || v > max) return false;
                    t.Fauna.Set(species, f, v);
                    return true;
                });
            }
            foreach (var scale in new[] { "populationScale", "floorScale", "capScale" })
            {
                string f = scale;
                Register("fauna.{world}." + f, (t, who, v) =>
                {
                    if (v < 0f || v > 4f) return false;
                    if (IsWorld(who))
                    {
                        t.World[(who.ToLowerInvariant(), f)] = v;
                        return true;
                    }
                    if (f == "populationScale" || t.Fauna == null) return false;   // species take floor and cap only
                    t.Fauna.Set(who, f, v);
                    return true;
                });
            }
        }

        /// <summary>A world selector: a 1-based leg number or "all".</summary>
        static bool IsWorld(string who) =>
            string.Equals(who, "all", StringComparison.OrdinalIgnoreCase)
            || int.TryParse(who, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1 && n <= ArkEscortSO.MaxDifficulty;

        /// <summary>Sane upper bound for a fauna field (a value past it is refused, not clamped).</summary>
        static float FieldMax(string field) => field switch
        {
            "shieldBite" or "superShieldBite" or "speedScale" or "biteScale" => 5f,
            "minSpeed" or "maxSpeed" or "cruiseSpeed" or "lungeSpeed" => 250f,
            "huntSpeed" or "pursuitSpeed" => 20f,
            "huntInterval" or "huntDuration" => 600f,
            _ => 3000f,   // radii and ranges
        };

        delegate void PacingField(ref ArkSpeedProfile profile, float value);

        static void RegisterPacing(string key, PacingField write) =>
            Register(key, (t, _, v) =>
            {
                if (v < 0f) return false;
                var p = t.Escort.ArkPacing;
                write(ref p, v);
                t.Escort.ArkPacing = p;
                return true;
            });

        static void RegisterTier(string field, Action<ArkEscortSO.Tier, float> write) =>
            Register("tier.{d}." + field, (t, d, v) =>
            {
                var tiers = t.Escort.Tiers;
                if (tiers == null || tiers.Count == 0) return false;
                if (d == "all" || d == "*")
                {
                    foreach (var tier in tiers) write(tier, v);
                    return true;
                }
                if (!int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return false;
                if (n < ArkEscortSO.MinDifficulty || n > ArkEscortSO.MaxDifficulty || n > tiers.Count) return false;
                write(tiers[n - 1], v);
                return true;
            });

        /// <summary>Apply every override in <paramref name="values"/> to <paramref name="target"/>; one echo per key.</summary>
        public static List<ArkEscortReport.Param> Apply(Target target, IReadOnlyDictionary<string, float> values)
        {
            var echo = new List<ArkEscortReport.Param>();
            if (values == null) return echo;
            foreach (var kv in values)
            {
                var param = new ArkEscortReport.Param { Key = kv.Key, Value = kv.Value };
                try
                {
                    if (kv.Key.StartsWith("ai.", StringComparison.Ordinal))
                    {
                        param.Applied = ExternalSetter != null && ExternalSetter(kv.Key, kv.Value);
                        if (!param.Applied) param.Note = ExternalSetter == null ? "no ai setter" : "refused";
                    }
                    else if (TryFind(kv.Key, out var entry, out string capture))
                    {
                        // fauna.* keys write the run's fauna tuning; every other key writes the escort copy.
                        bool reachable = target != null
                                         && (kv.Key.StartsWith("fauna.", StringComparison.Ordinal) || target.Escort != null);
                        param.Applied = reachable && entry.Set(target, capture, kv.Value);
                        if (!param.Applied) param.Note = "refused";
                    }
                    else param.Note = "unknown key";
                }
                catch (Exception e)
                {
                    param.Applied = false;
                    param.Note = "threw: " + e.Message;
                }
                if (!param.Applied) CSDebug.LogWarning($"[ArkEscortParams] '{kv.Key}' = {kv.Value} not applied ({param.Note}).");
                echo.Add(param);
            }
            return echo;
        }

        static bool TryFind(string key, out Entry found, out string capture)
        {
            var segments = key.Split('.');
            foreach (var e in Entries)
            {
                if (e.Segments.Length != segments.Length) continue;
                bool match = true;
                for (int i = 0; i < segments.Length && match; i++)
                    match = i == e.Capture || string.Equals(e.Segments[i], segments[i], StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                found = e;
                capture = e.Capture >= 0 ? segments[e.Capture] : null;
                return true;
            }
            found = null;
            capture = null;
            return false;
        }

        static readonly Regex Pair = new(
            "\"(?<k>[^\"]+)\"\\s*:\\s*(?<v>-?[0-9]+(?:\\.[0-9]+)?(?:[eE][-+]?[0-9]+)?|true|false)",
            RegexOptions.CultureInvariant);

        /// <summary>The <c>"key": number|true|false</c> pairs of a flat JSON object (other values are skipped).</summary>
        public static Dictionary<string, float> ParseFlatJson(string json)
        {
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json)) return values;
            foreach (Match m in Pair.Matches(json))
            {
                string v = m.Groups["v"].Value;
                float f = v == "true" ? 1f : v == "false" ? 0f
                    : float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
                values[m.Groups["k"].Value] = f;
            }
            return values;
        }
    }
}
