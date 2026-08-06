using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public abstract class CellLifeSpawnerBase : ICellLifeSpawner
    {
        readonly List<Coroutine> _running = new();

        public void Start(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            Stop(host);

            if (!Validate(host, config, runtime, gameData))
                return;

            OnStart(host, config, runtime, gameData);
        }

        public void Stop(Cell host)
        {
            if (!host) return;

            for (int i = 0; i < _running.Count; i++)
            {
                var c = _running[i];
                if (c != null) host.StopCoroutine(c);
            }
            _running.Clear();

            OnStop(host);
        }

        protected abstract void OnStart(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData);
        protected virtual void OnStop(Cell host) { }

        protected bool Validate(Cell host, CellConfigDataSO config, CellRuntimeDataSO runtime, GameDataSO gameData)
        {
            if (!host) { CSDebug.LogError("[CellLifeSpawner] Host is null."); return false; }
            if (!config) { CSDebug.LogError($"[CellLifeSpawner] Config is null for host '{host.name}'."); return false; }
            if (!runtime) { CSDebug.LogError($"[CellLifeSpawner] Runtime is null for host '{host.name}'."); return false; }
            if (!gameData) { CSDebug.LogError($"[CellLifeSpawner] GameData is null for host '{host.name}'."); return false; }
            return true;
        }

        protected Coroutine Track(Cell host, IEnumerator routine)
        {
            if (!host || routine == null) return null;
            var c = host.StartCoroutine(routine);
            if (c != null) _running.Add(c);
            return c;
        }

        // public static so non-subclass consumers (e.g. the freestyle microscene conveyor releasing
        // lifeforms into the cell) reuse the ONE canonical spawn sequence instead of duplicating it.
        // No instance state is touched; unqualified subclass calls still bind to these.
        public static void RegisterSpawned(Cell host, GameObject go)
        {
            if (!host || !go) return;
            host.RegisterSpawnedObject(go);
        }

        // GetExcludedDomain / GetLocalDomainOr were removed with the exclusion roll:
        // the locked no-domain-asymmetry invariant (CLAUDE.md ▸ Ecosystem Design
        // Principles) says all three domains seed flora and fauna spawn in the
        // controlling color - no spawner may bias against any domain.

        public static Domains PickRandomDomain(Domains? excluded)
        {
            // Playable domains only - never Blue, the "no team" sentinel. A Blue
            // lifeform's prisms count as opposing mass for EVERY anti-domain query
            // (anti-Jade, anti-Ruby, AND anti-Gold all include them), so Blue flora
            // act as universal bait that pulls every domain's fauna school to the
            // same place - defeating "different domains go to different locations".
            var candidates = new List<Domains>(3) { Domains.Jade, Domains.Ruby, Domains.Gold };
            if (excluded.HasValue) candidates.Remove(excluded.Value);

            return candidates.Count == 0
                ? Domains.Jade
                : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        protected T PickWeighted<T>(IReadOnlyList<T> items, Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0) return default;

            float total = 0f;
            for (int i = 0; i < items.Count; i++)
                total += Mathf.Max(0f, weightSelector(items[i]));

            if (total <= 0f) return items[0];

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                cumulative += Mathf.Max(0f, weightSelector(item));
                if (roll <= cumulative) return item;
            }
            return items[^1];
        }

        /// <summary>
        /// Correct probability roll. p = 0..1.
        /// </summary>
        protected bool AllowSpawn(float spawnProbability) =>
            UnityEngine.Random.value < Mathf.Clamp01(spawnProbability);

        protected bool TryGetCrystalGoal(CellRuntimeDataSO runtime, out Vector3 goal)
        {
            goal = default;
            if (!runtime) return false;

            var t = runtime.CrystalTransform;
            if (!t) return false;

            goal = t.position;
            return true;
        }

        public static Flora SpawnFlora(Cell host, Flora floraPrefab, Domains? excludedDomain,
            FloraConfigurationSO config = null, Vector3? spawnPosition = null, Vector3? spawnUp = null)
        {
            if (!host || !floraPrefab) return null;

            // IsRecording-guarded label: this runs for EVERY gameplay spawn, so the disarmed
            // path must not pay the interpolated-string allocation.
            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Flora, $"Flora spawn ({floraPrefab.name})")
                : LoadSpanScope.None;
            LoadInsights.Count("Flora spawned during load");

            Vector3 pos = spawnPosition ?? host.transform.position;
            var flora = UnityEngine.Object.Instantiate(floraPrefab, pos, Quaternion.identity);
            flora.domain = PickRandomDomain(excludedDomain);

            // A caller-specified position PINS the planting spot - Plant() implementations
            // honor it instead of dispersing the flora across the cell (the Lifeform Matrix
            // toy roots the spawn where the player triggered it).
            // An authored planting site also carries the ground's normal, so a plant rooted in a
            // garden bed grows away from the bed instead of toward the cell crystal.
            if (spawnPosition.HasValue && spawnUp.HasValue)
                flora.SetPlantPositionOverride(spawnPosition.Value, spawnUp.Value);
            else if (spawnPosition.HasValue)
                flora.SetPlantPositionOverride(spawnPosition.Value);

            // Elemental contract: the config may define the ELEMENT and the variant expression
            // as data (one base prefab, per-element variants from config). Applied BEFORE
            // Initialize - the leaf prism size and crystal lookup are consumed there.
            if (config)
            {
                // One roll decides this plant's variant: which element it carries, the block
                // that expresses that element, and the level it seeds at. With spread off the
                // roll returns the config's authored Element / Variant / InitialLevel, so the
                // legacy per-element-config path is unchanged.
                var pick = config.RollVariant();

                flora.ApplyElement(pick.Element);
                if (pick.Tuning is { Enabled: true })
                    flora.ApplyVariantTuning(pick.Tuning);
                flora.ApplyLevel(pick.Level, config.LeafScalePerLevel, config.CrystalScalePerLevel);
            }

            flora.Initialize(host);

            RegisterSpawned(host, flora.gameObject);
            return flora;
        }

        protected Fauna SpawnFauna(Cell host, Fauna faunaPrefab, Vector3 goal, Domains? excludedDomain)
        {
            if (!host || !faunaPrefab) return null;

            // IsRecording-guarded label — see SpawnFlora.
            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Fauna, $"Fauna spawn ({faunaPrefab.name})")
                : LoadSpanScope.None;
            LoadInsights.Count("Fauna spawned during load");

            var pop = UnityEngine.Object.Instantiate(faunaPrefab, host.transform.position, Quaternion.identity);
            pop.domain = PickRandomDomain(excludedDomain);
            pop.Goal = goal;

            // Subclasses (notably LightFauna) do real work in Initialize: body-prism
            // ChangeTeam + scale animation. Without this call brittlestar/shark bodies
            // render as invisible scale-0 prisms.
            pop.Initialize(host);

            RegisterSpawned(host, pop.gameObject);
            return pop;
        }

        /// <summary>
        /// Variant that assigns an explicit domain (e.g. the cell's controlling color)
        /// instead of rolling randomly. Used by the regulated fauna spawn loop so new
        /// fauna track the live leader rather than producing inconsistent domain mixes.
        /// </summary>
        public static Fauna SpawnFaunaWithDomain(Cell host, Fauna faunaPrefab, Vector3 goal, Domains domain, Vector3? spawnPosition = null)
        {
            if (!host || !faunaPrefab) return null;

            // IsRecording-guarded label — see SpawnFlora.
            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Fauna, $"Fauna spawn ({faunaPrefab.name})")
                : LoadSpanScope.None;
            LoadInsights.Count("Fauna spawned during load");

            // Spawn at the requested position (e.g. on the mass concentration the fauna will
            // forage) when given; otherwise the cell centre (legacy behavior, IntensityWise).
            Vector3 pos = spawnPosition ?? host.transform.position;
            var pop = UnityEngine.Object.Instantiate(faunaPrefab, pos, Quaternion.identity);
            pop.domain = domain;
            pop.Goal = goal;

            pop.Initialize(host);

            RegisterSpawned(host, pop.gameObject);
            return pop;
        }

        // ── Banded placement (shared by BOTH spawners - see the warning) ─────
        //
        // A species penned to a band (FaunaConfigurationSO.BandInner/BandOuterRadius) must be
        // born INSIDE its band and SCATTERED across it. These live on the base, not on one
        // spawner, because which spawner a cell uses is decided by an unrelated field:
        // Cell.StartSpawnerForMode picks IntensityWiseLifeSpawner whenever the cell is on
        // CellTypeChoiceOptions.IntensityWise - which is also the only way to vary a cell by
        // intensity. So a mode that wants per-intensity cells AND penned fauna gets the
        // intensity spawner whether it wanted it or not, and a band implemented in the other
        // spawner is simply dead code. That shipped once: Wildlife Liberation's whole
        // population spawned at the cell centre because the placement lived in
        // RandomLifeSpawner and the cell was running IntensityWiseLifeSpawner.
        //
        // Both spawners now call SpawnFaunaBanded. Do not add a third spawn site that does not.

        /// <summary>True when this species is penned to a band and needs banded placement.</summary>
        protected static bool IsBanded(FaunaConfigurationSO cfg) => cfg && cfg.BandOuterRadius > 0f;

        /// <summary>
        /// A fresh point somewhere in a species' band - uniform in DIRECTION and uniform in
        /// radius across the shell. Each call is independent, which is the point: it is what
        /// spreads a population through its room instead of stacking it on one spot.
        /// </summary>
        protected static Vector3 RandomPointInBand(Cell host, FaunaConfigurationSO cfg)
        {
            float inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius);
            float outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius);
            return host.transform.position
                   + UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(inner, outer);
        }

        /// <summary>
        /// Projects a point into a species' band. Returns it unchanged for an unbanded species
        /// (so the common path is one compare) or for a point already inside the band; a
        /// degenerate input - the cell centre, which is what both the crystal fallback and
        /// <c>GetDensestRegionAnyDomain</c> return before any mass exists - rolls a fresh point
        /// rather than collapsing a whole population onto one radial.
        /// </summary>
        protected static Vector3 ClampToBand(Cell host, FaunaConfigurationSO cfg, Vector3 point)
        {
            if (!host || !IsBanded(cfg)) return point;

            float inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius);
            float outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius);

            Vector3 centre = host.transform.position;
            Vector3 offset = point - centre;
            float d = offset.magnitude;

            if (d < 1f) return RandomPointInBand(host, cfg);
            if (d >= inner && d <= outer) return point;

            // Land anywhere in the shell rather than pinned to whichever wall was nearest -
            // a wave that all lands on one wall reads as a ring, not a population.
            return centre + offset / d * UnityEngine.Random.Range(inner, outer);
        }

        /// <summary>How far a banded creature may be born from its own initial goal.</summary>
        protected const float BandSpawnJitter = 40f;

        /// <summary>
        /// THE fauna spawn call for a species that may be banded. A banded species gets its own
        /// per-creature point in its room for both spawn position and initial goal; an unbanded
        /// one falls through to the caller's <paramref name="fallbackGoal"/> /
        /// <paramref name="fallbackPosition"/> exactly as before, so every existing biome is
        /// untouched.
        /// </summary>
        protected static Fauna SpawnFaunaBanded(Cell host, FaunaConfigurationSO cfg, Domains color,
            Vector3 fallbackGoal, Vector3? fallbackPosition = null)
        {
            if (!host || !cfg || !cfg.FaunaPrefab) return null;

            Vector3 goal = fallbackGoal;
            Vector3? position = fallbackPosition;

            if (IsBanded(cfg))
            {
                goal = RandomPointInBand(host, cfg);
                // A short jitter off its own goal, re-projected, so a creature is not born
                // exactly on the point it is already seeking.
                position = ClampToBand(host, cfg,
                    goal + UnityEngine.Random.insideUnitSphere * BandSpawnJitter);
            }

            var fauna = SpawnFaunaWithDomain(host, cfg.FaunaPrefab, goal, color, position);
            if (fauna) fauna.AssignLineage(host, cfg);
            return fauna;
        }

        protected float GetControllingVolume(GameDataSO gameData) =>
            gameData.GetControllingTeamStatsBasedOnVolumeRemaining().Item2;

        protected IEnumerator RunSpawnLoop(Func<bool> shouldSpawn, Action spawnOnce, Func<float> getWaitSeconds)
        {
            while (true)
            {
                if (shouldSpawn())
                    spawnOnce?.Invoke();

                var wait = Mathf.Max(0f, getWaitSeconds?.Invoke() ?? 0f);
                if (wait <= 0f) yield return null;
                else yield return new WaitForSeconds(wait);
            }
        }

        protected IEnumerator RunThresholdLoop(Func<bool> condition, Action spawnOnce, Func<float> trueWait, Func<float> falseWait)
        {
            while (true)
            {
                if (condition())
                {
                    spawnOnce?.Invoke();
                    var w = Mathf.Max(0f, trueWait?.Invoke() ?? 0f);
                    if (w <= 0f) yield return null;
                    else yield return new WaitForSeconds(w);
                }
                else
                {
                    var w = Mathf.Max(0f, falseWait?.Invoke() ?? 0f);
                    if (w <= 0f) yield return null;
                    else yield return new WaitForSeconds(w);
                }
            }
        }
    }
}