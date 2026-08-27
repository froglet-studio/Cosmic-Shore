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

        /// <param name="spawnRotation">
        /// Orientation to instantiate at. Only a species whose growth rule is ORIENTED cares -
        /// an <see cref="AssembledFlora"/> seeds its first prism at the flora's own rotation, so
        /// a daughter continuing a parent's lattice must be born already aligned to it. Null
        /// keeps the legacy identity rotation.
        /// </param>
        /// <param name="inherit">
        /// A parent's variant pick, passed when this plant is an OFFSPRING: the lineage keeps
        /// its element and the level it seeded at instead of re-rolling a fresh identity every
        /// birth. Null rolls from the config, which with spread off is just the authored values.
        /// </param>
        /// <param name="preInitialize">
        /// Per-family setup applied after the variant/level and BEFORE <c>Initialize</c> - the
        /// only window in which a plant's growth rule can be seeded.
        /// </param>
        /// <param name="domainOverride">
        /// Plant in THIS domain instead of rolling one. Used by reproduction, where a child is its
        /// parent's colour (the fauna rule, §6.1) - and it must be applied HERE rather than with a
        /// <c>SetTeam</c> after the fact, because <c>Initialize</c> files the plant's first prisms
        /// into the cell's per-domain grids on the way through. The uniform Jade/Ruby/Gold roll is
        /// still the SPAWNER's job, so the no-domain-asymmetry invariant is untouched.
        /// </param>
        public static Flora SpawnFlora(Cell host, Flora floraPrefab, Domains? excludedDomain,
            FloraConfigurationSO config = null, Vector3? spawnPosition = null, Vector3? spawnUp = null,
            Quaternion? spawnRotation = null,
            LifeformVariantPick<FloraVariantTuning>? inherit = null,
            Action<Flora> preInitialize = null,
            Domains? domainOverride = null)
        {
            if (!host || !floraPrefab) return null;

            // IsRecording-guarded label: this runs for EVERY gameplay spawn, so the disarmed
            // path must not pay the interpolated-string allocation.
            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Flora, $"Flora spawn ({floraPrefab.name})")
                : LoadSpanScope.None;
            LoadInsights.Count("Flora spawned during load");

            Vector3 pos = spawnPosition ?? host.transform.position;
            var flora = UnityEngine.Object.Instantiate(floraPrefab, pos, spawnRotation ?? Quaternion.identity);
            flora.domain = domainOverride ?? PickRandomDomain(excludedDomain);

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
                // One roll decides this plant's variant: which element it carries and the block
                // that expresses that element. With spread off the roll returns the config's
                // authored Element / Variant, so the legacy per-element-config path is
                // unchanged. An OFFSPRING passes its parent's pick, which RollVariant returns
                // verbatim - a lineage breeds true. There is no level: a plant is its species
                // and its element, and the element states everything - leaf, tempo, budget and
                // the size of its heart (Docs/ECOSYSTEM.md §40).
                var pick = config.RollVariant(inherit);

                flora.ApplyElement(pick.Element);
                if (pick.Tuning is { Enabled: true })
                    flora.ApplyVariantTuning(pick.Tuning);

                // Cell-level overrides LAST, so they survive SpreadElements: the roll above may
                // have replaced this config's whole Variant with a palette sibling's (the
                // element's identity), but WHERE this cell plants the species and HOW BIG one
                // plant may get are the cell's decisions, not the element's. The owning
                // SpawnProfile's density scalar folds into the same block - one application
                // path, so a scaled budget can never diverge from an overridden one.
                var spawnProfile = host.Config ? host.Config.SpawnProfile : null;
                float plantBudgetScale = spawnProfile ? spawnProfile.FloraPlantBudgetScale : 1f;

                if (config.TryBuildCellOverrideTuning(plantBudgetScale, out var cellOverrides))
                    flora.ApplyVariantTuning(cellOverrides);

                // Lineage BEFORE Initialize, so the plant is already counted in the cell's
                // per-species population by the time its first growth tick can try to seed an
                // offspring - otherwise a species could momentarily overshoot its own cap.
                // Carries the pick it actually got, so its own offspring inherit that identity
                // rather than the (possibly null) one passed in here.
                flora.AssignLineage(host, config, pick);
            }

            preInitialize?.Invoke(flora);

            flora.Initialize(host);

            RegisterSpawned(host, flora.gameObject);
            return flora;
        }

        /// <summary>
        /// How many plants a periodic tick should seed - the flora seeder is a SEEDER, not the
        /// population driver (Docs/ECOSYSTEM.md §32). A species that authors a seed floor is
        /// topped back up to it and no further: bootstrap, plus recovery after the food web
        /// grazes the species out, so extinction is never permanent. Above the floor the forest
        /// is grown by the plants' own reproduction (<c>Flora.TryReproduce</c>) and bounded by
        /// grazing.
        ///
        /// <para>A species that authors NO floor keeps the legacy behaviour exactly - one plant
        /// per plant period, bounded only by the cell's Frenzy gate - so the population model is
        /// opt-in per species rather than a silent re-tune of every biome.</para>
        ///
        /// <para>Seed floor AND cap come from the CELL, so a profile's <c>FloraPopulationScale</c>
        /// moves both together; scaling the floor alone would be clamped away by the authored cap
        /// and read as doing nothing (see <c>Cell.ResolveFloraPopulation</c>).</para>
        ///
        /// <para>It lives on the BASE, not on one spawner, for the same reason banded fauna
        /// placement does - see the warning below. <c>CellTypeChoiceOptions.IntensityWise</c>
        /// silently swaps which spawner a cell runs, so a population rule implemented in only one
        /// of them is dead code in exactly the modes that asked for it.</para>
        /// </summary>
        public static int FloraSeedDeficit(Cell host, FloraConfigurationSO floraCfg)
        {
            if (!host || !floraCfg) return 0;
            if (!FloraReproductionRules.HasPopulationModel(floraCfg.PopulationSize)) return 1;

            return FloraReproductionRules.SeedSpawnCount(
                host.GetLiveFloraCount(floraCfg),
                host.ResolveFloraPopulation(floraCfg.PopulationSize),
                host.ResolveFloraCap(floraCfg));
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
        /// A radius drawn VOLUME-uniformly across the shell [<paramref name="inner"/>,
        /// <paramref name="outer"/>] - the cube root of a uniform draw between the cubed walls,
        /// so equal VOLUMES receive equal numbers of creatures.
        ///
        /// A uniform-in-radius draw instead gives every radial shell the same headcount, and a
        /// shell's space grows as r^2, so it crowds the inner wall and leaves the outside empty.
        /// That was invisible while every authored band was a thin annulus (660..990 moves its
        /// mean radius by ~1.6%, which is why no shipped biome changes here) and becomes the
        /// whole story the moment a band spans a WHOLE arena: a uniform 0..1180 draw puts half
        /// the population inside r=590, one eighth of the volume - the exact "everything is
        /// concentrated in the centre" the roam band exists to end. Same lesson the flora
        /// planting band already records (Docs/ECOSYSTEM.md 27): a species disperses in a
        /// volume-uniform BAND, never on a shell.
        /// </summary>
        protected static float RandomBandRadius(float inner, float outer)
        {
            float i3 = inner * inner * inner;
            float o3 = outer * outer * outer;
            return Mathf.Pow(UnityEngine.Random.Range(i3, o3), 1f / 3f);
        }

        /// <summary>
        /// A fresh point somewhere in a species' band - uniform in DIRECTION and volume-uniform
        /// in radius across the shell (see <see cref="RandomBandRadius"/>). Each call is
        /// independent, which is the point: it is what spreads a population through its band
        /// instead of stacking it on one spot.
        /// </summary>
        protected static Vector3 RandomPointInBand(Cell host, FaunaConfigurationSO cfg)
        {
            float inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius);
            float outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius);
            return host.transform.position
                   + UnityEngine.Random.onUnitSphere * RandomBandRadius(inner, outer);
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
            return centre + offset / d * RandomBandRadius(inner, outer);
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

            // The CELL's own pen (outer containment + inner exclusion) applies to the birth
            // POSITION too. Fauna.Goal clamps itself in its setter, but a creature born inside a
            // closed pen would already be standing where it is not allowed to be - which reads as
            // the pen leaking. Applied here, at the one spawn call both spawners share, for the
            // same reason banded placement lives here (see the warning above). No-op for every
            // biome that authors no pen.
            if (host.FaunaExclusionRadius > 0f || host.FaunaContainmentRadius > 0f)
            {
                // No explicit position means "the cell centre", which is the one point a closed
                // inner pen definitely forbids - so resolve it here rather than letting the
                // default put the whole brood in the middle of the excluded zone.
                Vector3 birth = position ?? (host.transform.position
                    + UnityEngine.Random.onUnitSphere * Mathf.Max(1f, host.FaunaExclusionRadius));
                position = host.ClampToFaunaContainment(birth, birth);
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