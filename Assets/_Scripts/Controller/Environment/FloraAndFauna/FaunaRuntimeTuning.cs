using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Per-run fauna overrides for tuning sweeps (the Ark escort's <c>-arkEscortParams</c> <c>fauna.*</c> keys,
    /// <see cref="ArkEscortParams"/>): values by SPECIES and field, applied to each creature as its cell registers it, and
    /// only in cells that carry this object (<see cref="Cell.RuntimeFaunaTuning"/>, set by the escort on its own worlds).
    /// Never writes an asset: a species' shared data asset (<see cref="LightFaunaDataSO"/>, a worm's
    /// <see cref="WormColonyConfigSO"/>) is copied once per run and the copy carries the overrides.
    ///
    /// <para>A tuning tool, not an in-game fitness: the game never scores or rewards creatures by these numbers; an
    /// offline driver writes them per run (endogenous selection is untouched, ecology §2).</para>
    ///
    /// <para>The species key is the creature prefab's name, lower-cased, with "(Clone)", a leading "Mass" and a
    /// trailing "Fauna" removed (MassSharkFauna -> shark, QuadFish -> quadfish, TadPoleFauna -> tadpole,
    /// WormColony -> wormcolony). <c>all</c> applies to every species; a species' own value wins.</para>
    /// </summary>
    public sealed class FaunaRuntimeTuning
    {
        public const string AllSpecies = "all";

        /// <summary>Fields every species takes (Fauna base and the cell's population resolution).</summary>
        public static readonly string[] CommonFields =
            { "shieldBite", "superShieldBite", "floorScale", "capScale", "speedScale", "biteScale" };

        /// <summary>LightFauna fields (a runtime copy of its <see cref="LightFaunaDataSO"/>).</summary>
        public static readonly string[] LightFields =
        {
            "minSpeed", "maxSpeed", "consumeRadius", "detectionRadius", "feedingClusterRadius", "attackRange",
            "huntInterval", "huntDuration", "territory", "pursuitSpeed",
        };

        /// <summary>Boid fields (written on the instance).</summary>
        public static readonly string[] BoidFields = { "minSpeed", "maxSpeed", "huntSpeed", "feedingClusterRadius" };

        /// <summary>Worm colony fields (a runtime copy of its <see cref="WormColonyConfigSO"/>).</summary>
        public static readonly string[] WormFields =
            { "cruiseSpeed", "aggroRadius", "lungeSpeed", "huntInterval", "huntDuration", "mouthRadius" };

        /// <summary>Every species field any class reads, de-duplicated (the params registry's key list).</summary>
        public static IEnumerable<string> AllFields()
        {
            var seen = new HashSet<string>();
            foreach (var set in new[] { CommonFields, LightFields, BoidFields, WormFields })
                foreach (var f in set)
                    if (seen.Add(f)) yield return f;
        }

        readonly Dictionary<string, Dictionary<string, float>> _species = new(StringComparer.Ordinal);
        readonly Dictionary<(Object source, string species), Object> _copies = new();

        /// <summary>True when any species override is set.</summary>
        public bool HasAny => _species.Count > 0;

        /// <summary>Set <paramref name="field"/> for <paramref name="species"/> (normalized; <c>all</c> for every species).</summary>
        public void Set(string species, string field, float value)
        {
            species = SpeciesKey(species);
            if (!_species.TryGetValue(species, out var fields)) _species[species] = fields = new(StringComparer.Ordinal);
            fields[field] = value;
            _copies.Clear();   // a copy made before this value would miss it
        }

        /// <summary>The species' own value, else the <c>all</c> value.</summary>
        public bool TryGet(string species, string field, out float value)
        {
            value = 0f;
            if (species != null && _species.TryGetValue(species, out var own) && own.TryGetValue(field, out value)) return true;
            return _species.TryGetValue(AllSpecies, out var all) && all.TryGetValue(field, out value);
        }

        /// <summary>True when <paramref name="species"/> (or <c>all</c>) has any override.</summary>
        public bool Touches(string species) =>
            (species != null && _species.ContainsKey(species)) || _species.ContainsKey(AllSpecies);

        /// <summary>Multiplier on a species' seed floor in tuned cells (1 when unset).</summary>
        public float FloorScale(string species) => TryGet(species, "floorScale", out float v) ? Mathf.Max(0f, v) : 1f;

        /// <summary>Multiplier on a species' live cap in tuned cells (1 when unset).</summary>
        public float CapScale(string species) => TryGet(species, "capScale", out float v) ? Mathf.Max(0f, v) : 1f;

        /// <summary>Normalized species key of a prefab or instance name (see the class summary).</summary>
        public static string SpeciesKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string s = raw.Trim();
            int clone = s.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (clone >= 0) s = s.Remove(clone);
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            s = sb.ToString();
            if (s.Length > 4 && s.StartsWith("mass", StringComparison.Ordinal)) s = s.Substring(4);
            if (s.Length > 5 && s.EndsWith("fauna", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 5);
            return s;
        }

        /// <summary>The run's copy of <paramref name="source"/> with this species' LightFauna overrides (the source when
        /// none apply). One copy per source per run.</summary>
        public LightFaunaDataSO ResolveLightData(LightFaunaDataSO source, string species)
        {
            if (!source || !TouchesAny(species, LightFields)) return source;
            if (_copies.TryGetValue((source, species), out var made) && made) return (LightFaunaDataSO)made;
            var copy = Object.Instantiate(source);
            copy.name = source.name + " (tuned)";
            if (TryGet(species, "minSpeed", out float v)) copy.minSpeed = Mathf.Max(0f, v);
            if (TryGet(species, "maxSpeed", out v)) copy.maxSpeed = Mathf.Max(copy.minSpeed, v);
            if (TryGet(species, "consumeRadius", out v)) copy.consumeRadius = Mathf.Max(0f, v);
            if (TryGet(species, "detectionRadius", out v)) copy.detectionRadius = Mathf.Max(0f, v);
            if (TryGet(species, "feedingClusterRadius", out v)) copy.feedingClusterRadius = Mathf.Max(0f, v);
            if (TryGet(species, "attackRange", out v)) copy.attackRange = Mathf.Max(0f, v);
            if (TryGet(species, "huntInterval", out v)) copy.huntIntervalSeconds = Mathf.Max(0f, v);
            if (TryGet(species, "huntDuration", out v)) copy.huntDurationSeconds = Mathf.Max(0f, v);
            if (TryGet(species, "territory", out v)) copy.territoryRadius = Mathf.Max(0f, v);
            if (TryGet(species, "pursuitSpeed", out v)) copy.pursuitSpeedMultiplier = Mathf.Max(1f, v);
            if (TryGet(species, "speedScale", out v))   // a multiplier on top (a voyage's "swift fauna")
            {
                copy.minSpeed *= Mathf.Max(0f, v);
                copy.maxSpeed *= Mathf.Max(0f, v);
            }
            _copies[(source, species)] = copy;
            return copy;
        }

        /// <summary>The run's copy of a worm colony config with this species' overrides (the source when none apply).</summary>
        public WormColonyConfigSO ResolveWormConfig(WormColonyConfigSO source, string species)
        {
            if (!source || !TouchesAny(species, WormFields)) return source;
            if (_copies.TryGetValue((source, species), out var made) && made) return (WormColonyConfigSO)made;
            var copy = Object.Instantiate(source);
            copy.name = source.name + " (tuned)";
            if (TryGet(species, "cruiseSpeed", out float v)) copy.CruiseSpeed = Mathf.Max(0f, v);
            if (TryGet(species, "aggroRadius", out v)) copy.AggroRadius = Mathf.Max(0f, v);
            if (TryGet(species, "lungeSpeed", out v)) copy.LungeSpeed = Mathf.Max(0f, v);
            if (TryGet(species, "huntInterval", out v)) copy.HuntIntervalSeconds = Mathf.Max(0f, v);
            if (TryGet(species, "huntDuration", out v)) copy.HuntDurationSeconds = Mathf.Max(0f, v);
            if (TryGet(species, "mouthRadius", out v)) copy.MouthRadius = Mathf.Max(0f, v);
            if (TryGet(species, "speedScale", out v))
            {
                copy.CruiseSpeed *= Mathf.Max(0f, v);
                copy.LungeSpeed *= Mathf.Max(0f, v);
            }
            _copies[(source, species)] = copy;
            return copy;
        }

        bool TouchesAny(string species, string[] fields)
        {
            if (TryGet(species, "speedScale", out _)) return true;
            foreach (var f in fields)
                if (TryGet(species, f, out _)) return true;
            return false;
        }
    }
}
