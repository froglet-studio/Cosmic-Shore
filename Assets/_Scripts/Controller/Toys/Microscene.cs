using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One slot on the freestyle microscene conveyor: a container of toy-owned prisms (one
    /// <see cref="Trail"/>) plus elemental-crystal pickups, laid out by a
    /// <see cref="MicroscenePlan"/>. The slot owns the continuity-law transitions for its content:
    ///
    ///   • First population lays prisms through the canonical environment sequence
    ///     (Instantiate → ChangeTeam → pose → TargetScale → Trail → Initialize), a few per frame,
    ///     so every prism grows in from zero via the batched PrismScaleAnimator.
    ///   • Recycling is TRANSPORT, not removal: the container suctions to a point
    ///     (scale → ~0 over a visible transition), relocates, re-poses the SAME prism instances
    ///     into a fresh plan, and blooms back out — mass is conserved by construction. Prisms
    ///     that fauna ate in the meantime (the active-force sink) are re-minted through the
    ///     sanctioned pool-reuse lifecycle (<see cref="Prism.Initialize"/>), which is creation,
    ///     never a resurrection of consumed mass.
    ///   • Lifeforms are NOT toy property: flora/fauna a plan requests are released into the host
    ///     <see cref="Cell"/> as ordinary citizens (canonical spawn sequences, controlling-color
    ///     fauna, lineage population caps) and are never tracked, moved, or despawned by the toy.
    /// </summary>
    public class Microscene : MonoBehaviour
    {
        const float SuctionScale = 0.002f; // never exactly zero — keeps lossyScale well-formed

        static readonly Element[] PickupElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        Prism _prismPrefab;
        SkimmerCrystalEffectSO[] _crystalEffects;
        Trail _trail;
        readonly List<Prism> _prisms = new();
        readonly List<Crystal> _crystals = new();

        public string RecipeName { get; private set; } = "";
        public bool Busy { get; private set; }

        /// <summary>World anchor of the scene (its container position).</summary>
        public Vector3 Anchor => transform.position;

        /// <summary>The direction the scene is flown through (+z of the container).</summary>
        public Vector3 Forward => transform.forward;

        public static Microscene Create(Transform parent, string label)
        {
            var go = new GameObject($"Microscene_{label}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Microscene>();
        }

        public void Configure(Prism prismPrefab, SkimmerCrystalEffectSO[] crystalEffects)
        {
            _prismPrefab = prismPrefab;
            _crystalEffects = crystalEffects;
        }

        // ── First population (grow-in) ───────────────────────────────────────

        /// <summary>
        /// Lay the scene for the first time at its current pose. Prisms are instantiated a few
        /// per frame (single-frame prism batches are a known spike) and each grows in from zero
        /// through its own scale animator — nothing pops in.
        /// </summary>
        public async UniTask PopulateAsync(MicroscenePlan plan, Domains domain, System.Random rng, CancellationToken ct)
        {
            Busy = true;
            try
            {
                RecipeName = plan.RecipeName;
                _trail = new Trail();

                if (_prismPrefab)
                {
                    // 1/frame on the stripped mobile build: each Instantiate (GameObject + collider
                    // + Prism init + spatial-index register) costs ~0.5-2ms on an old phone, so 6/frame
                    // was a visible 3-12ms hitch during every populate. At 1/frame a 40-prism scene
                    // takes ~40 frames (~0.3s @120Hz) to lay — invisible: scenes spawn ≥170m ahead and
                    // each prism's own grow-in covers it. Instantiates only happen for the first
                    // poolSize populates; recycles re-pose the same instances.
                    int perFrame = CosmicShore.Utility.PerfStrip.Enabled ? 1 : 6;
                    for (int i = 0; i < plan.PrismPoints.Count; i++)
                    {
                        var point = plan.PrismPoints[i];
                        var block = Instantiate(_prismPrefab, transform);
                        block.ChangeTeam(domain);
                        block.ownerID = $"{name}::{i}";
                        block.transform.localPosition = point.Position;
                        block.transform.localRotation = point.Rotation;
                        block.TargetScale = point.Scale;
                        block.Trail = _trail;
                        block.Initialize();
                        _trail.Add(block);
                        _prisms.Add(block);

                        if ((i + 1) % perFrame == 0)
                            await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }

                SpawnCrystals(plan, rng);
                ReleaseLifeforms(plan, rng);
            }
            finally
            {
                Busy = false;
            }
        }

        // ── Recycle (suction → relocate → re-pose → bloom) ───────────────────

        /// <summary>
        /// Conveyor-belt transport: suction the whole scene toward its anchor (a sanctioned
        /// continuity transition), move it to <paramref name="pose"/> while effectively a point,
        /// re-pose the same prisms into <paramref name="plan"/> with a fresh domain, then bloom
        /// back out. Total mass in the belt is unchanged; only arrangement, place, and colour vary.
        /// </summary>
        public async UniTask RecycleAsync(MicroscenePlan plan, Pose pose, Domains domain, System.Random rng,
            float transitionSeconds, CancellationToken ct)
        {
            Busy = true;
            try
            {
                await AnimateScaleAsync(1f, SuctionScale, transitionSeconds, ct);

                transform.SetPositionAndRotation(pose.position, pose.rotation);
                await RearrangeIntoAsync(plan, domain, ct);

                await AnimateScaleAsync(SuctionScale, 1f, transitionSeconds, ct);
                transform.localScale = Vector3.one;
                NotifyPrismPositions();

                // Replacement crystals mint only now, at full container scale — Crystal.Start()
                // stamps crystalValue from lossyScale, so minting while suctioned would leave
                // permanently worthless pickups.
                TopUpCrystals(plan, rng);
                ReleaseLifeforms(plan, rng);
            }
            finally
            {
                Busy = false;
            }
        }

        /// <summary>
        /// Amortized re-pose: each prism's full re-initialize (ChangeTeam material state + spatial
        /// index unregister/re-register + density-grid re-file + coroutine start) costs ~50-200µs,
        /// so re-posing all ~40 in one frame was a recurring 2-8ms spike on EVERY recycle. Yielding
        /// every few prisms spreads it over ~8 frames — completely invisible, because the container
        /// sits suctioned at ~zero scale while this runs.
        /// </summary>
        async UniTask RearrangeIntoAsync(MicroscenePlan plan, Domains domain, CancellationToken ct)
        {
            RecipeName = plan.RecipeName;

            const int perFrame = 5;
            int count = Mathf.Min(_prisms.Count, plan.PrismPoints.Count);
            for (int i = 0; i < count; i++)
            {
                var block = _prisms[i];
                if (!block) continue;

                var point = plan.PrismPoints[i];
                block.ChangeTeam(domain);
                block.transform.localPosition = point.Position;
                block.transform.localRotation = point.Rotation;

                // Zero the scale so EVERY re-posed prism blooms from zero, uniformly. ResetState
                // only zeroes the eaten (disabled-animator) prisms; without this the surviving
                // prisms would keep their old grown scale and MORPH old→new instead of blooming,
                // an inconsistent transition (continuity law: nothing pops). Matches the
                // first-population path in PopulateAsync.
                block.transform.localScale = Vector3.zero;

                // Full re-initialize for EVERY prism (not just eaten ones): ResetState
                // unregisters and CreateBlockCoroutine re-registers at the new position, which
                // re-files the cell's per-domain density grids — UpdatePosition alone never
                // re-files them (the documented registration-time-binding gap in
                // Docs/SPATIAL_INDEX.md), so a moved-but-not-reregistered prism would leave
                // phantom fauna-sense density at the abandoned scene site. For fauna-eaten slots
                // this is the pool-reuse mint: ResetState re-arms the scale animator that
                // SetupDestruction disabled, so the replacement mass grows in from zero.
                block.Initialize();

                // AFTER Initialize — on an eaten slot the animator is only re-armed inside
                // ResetState, so an earlier write would be silently dropped.
                block.TargetScale = point.Scale;

                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // Surviving crystals ride the belt to fresh plan slots (their value was stamped at
            // full scale when first minted); destroyed OR mid-collection ones drop out so
            // TopUpCrystals mints replacements. A crystal the player just skimmed is flying to
            // the vessel on its own — detach it from the container so the suction doesn't scale
            // it mid-flight, and stop treating it as a belt resident.
            for (int i = _crystals.Count - 1; i >= 0; i--)
            {
                var crystal = _crystals[i];
                if (!crystal)
                {
                    _crystals.RemoveAt(i);
                }
                else if (crystal.TryGetComponent(out ElementalCrystalImpactor impactor) && impactor.HasBeenCollected)
                {
                    crystal.transform.SetParent(null, worldPositionStays: true);
                    _crystals.RemoveAt(i);
                }
            }

            for (int i = 0; i < _crystals.Count; i++)
            {
                Vector3 local = i < plan.CrystalPoints.Count
                    ? plan.CrystalPoints[i]
                    : plan.CrystalPoints.Count > 0 ? plan.CrystalPoints[^1] : Vector3.zero;
                // Overflow crystals (more survivors than plan slots) fan out vertically instead
                // of stacking co-located on the last slot.
                if (i >= plan.CrystalPoints.Count)
                    local += Vector3.up * 16f * (i - plan.CrystalPoints.Count + 1);
                _crystals[i].transform.localPosition = local;
            }
        }

        void TopUpCrystals(MicroscenePlan plan, System.Random rng)
        {
            for (int slot = _crystals.Count; slot < plan.CrystalPoints.Count; slot++)
                MintCrystal(plan.CrystalPoints[slot], rng);
        }

        // ── Crystals (elemental pickups, manager-less) ───────────────────────

        void SpawnCrystals(MicroscenePlan plan, System.Random rng)
        {
            foreach (var local in plan.CrystalPoints)
                MintCrystal(local, rng);
        }

        void MintCrystal(Vector3 localPosition, System.Random rng)
        {
            var set = ElementalCrystalSetSO.Load();
            if (!set) return; // no elemental set in this project state — scenes still work without pickups

            var element = PickupElements[rng.Next(PickupElements.Length)];
            var prefab = set.GetPrefab(element);
            if (!prefab) return;

            var crystal = Instantiate(prefab, transform);
            crystal.transform.localPosition = localPosition;
            // MULTIPLY the prefab's authored scale (the elemental prefabs ship at root scale 10 —
            // assigning would shrink the pickup and its trigger 10×). Sized before Start():
            // crystalValue and the element-level gain both read lossyScale.
            crystal.transform.localScale *= (float)(rng.NextDouble() * 0.2 + 0.1);
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            // The standalone elemental prefabs carry no collection components (lifeform prefabs
            // add them as authored overrides) — wire the same pair at runtime so the crystal is
            // skimmable: the impactor collects, the ImpactCollider lets the skimmer side react.
            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            if (_crystalEffects is { Length: > 0 })
                impactor.SetCollectionEffects(_crystalEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            // Continuity-law fade-in: three of the four elemental prefabs carry FadeIn on their
            // model renderers; CrystalCharge ships without one and would pop into view. Add the
            // standard component wherever it's missing so every pickup fades in.
            foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                if (!renderer.TryGetComponent(out FadeIn _))
                    renderer.gameObject.AddComponent<FadeIn>();

            _crystals.Add(crystal);
        }

        // ── Lifeforms (released to the cell — never toy property) ────────────

        void ReleaseLifeforms(MicroscenePlan plan, System.Random rng)
        {
            if (plan.FloraCount <= 0 && plan.FaunaCount <= 0) return;

            // Strictly the CONTAINING cell: the belt roams anywhere, and a lifeform released
            // outside a cell's sense radius would be a degraded citizen (no goals, no phase
            // participation). Open-space scenes simply stay prisms + crystals; the living
            // recipes light back up whenever the ride passes through a cell.
            var cell = Cell.FindCellContaining(transform.position);
            var profile = cell ? cell.Config?.SpawnProfile : null;
            if (!cell || profile == null) return;

            if (cell.FloraPlantingEnabled && profile.SupportedFloras is { Count: > 0 })
            {
                for (int i = 0; i < plan.FloraCount; i++)
                {
                    var cfg = profile.SupportedFloras[rng.Next(profile.SupportedFloras.Count)];
                    if (!cfg || !cfg.FloraPrefab) continue;

                    var flora = Instantiate(cfg.FloraPrefab, ScatterAround(rng, 30f), Quaternion.identity);
                    flora.domain = PickPlayableDomain(rng);
                    flora.Initialize(cell);
                    cell.RegisterSpawnedObject(flora.gameObject);
                }
            }

            if (profile.SupportedFaunas is { Count: > 0 })
            {
                for (int i = 0; i < plan.FaunaCount; i++)
                {
                    var cfg = profile.SupportedFaunas[rng.Next(profile.SupportedFaunas.Count)];
                    if (!cfg || !cfg.FaunaPrefab) continue;
                    // Respect the species' per-cell performance cap — the conveyor adds citizens,
                    // never a parallel population.
                    if (cfg.MaxLivePopulation > 0 && cell.GetLiveFaunaCount(cfg) >= cfg.MaxLivePopulation) continue;
                    // …and the canonical prey-linked production gate (mirrors RandomLifeSpawner):
                    // no herbivore without enough opposing mass, no predator without enough prey —
                    // a fauna spawned into famine just withers in ~30s.
                    if (!PreyAvailable(cell, profile, cfg.FaunaPrefab.Diet)) continue;

                    var fauna = Instantiate(cfg.FaunaPrefab, ScatterAround(rng, 40f), Quaternion.identity);
                    fauna.domain = cell.ControllingDomain; // locked invariant: controlling colour only
                    fauna.Goal = transform.position;       // seed it toward the scene's fresh mass
                    fauna.Initialize(cell);
                    fauna.AssignLineage(cell, cfg);
                    cell.RegisterSpawnedObject(fauna.gameObject);
                }
            }
        }

        static bool PreyAvailable(Cell cell, SpawnProfileSO profile, FaunaDiet diet)
        {
            if (profile.FaunaFoodFloor <= 0) return true; // 0 = always produce (profile-authored)
            return diet == FaunaDiet.Predator
                ? cell.GetLiveHerbivoreCount() >= profile.FaunaFoodFloor
                : cell.OpposingVolume(cell.ControllingDomain) >=
                  profile.FaunaFoodFloor * CellPhaseThresholds.NominalPrismVolume;
        }

        Vector3 ScatterAround(System.Random rng, float radius)
        {
            var offset = new Vector3(
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1)) * radius;
            return transform.position + offset;
        }

        static Domains PickPlayableDomain(System.Random rng)
        {
            // Flora seed in a random PLAYABLE domain (never Blue, the no-team sentinel) —
            // mirrors CellLifeSpawnerBase.PickRandomDomain.
            return rng.Next(3) switch { 0 => Domains.Jade, 1 => Domains.Ruby, _ => Domains.Gold };
        }

        // ── Animation ────────────────────────────────────────────────────────

        async UniTask AnimateScaleAsync(float from, float to, float seconds, CancellationToken ct)
        {
            float elapsed = 0f;
            int frame = 0;
            seconds = Mathf.Max(0.05f, seconds);
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                float eased = t * t * (3f - 2f * t); // smoothstep, matching Toy.BloomIn
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, eased);

                // Stripped mobile build: intermediate notifies every 3rd frame is plenty — the
                // spatial index only rebuckets on 8m boundary crossings, and RecycleAsync issues
                // an exact NotifyPrismPositions after the animation settles.
                if (!CosmicShore.Utility.PerfStrip.Enabled || (frame++ % 3) == 0)
                    NotifyPrismPositions();

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            transform.localScale = Vector3.one * to;
        }

        /// <summary>
        /// Movers contract (Docs/SPATIAL_INDEX.md): whenever the belt moves prisms, push their
        /// positions into the spatial index so AOE, occupancy, and fauna senses stay honest.
        /// Cheap — the index only rebuckets on 8m boundary crossings.
        /// </summary>
        void NotifyPrismPositions()
        {
            for (int i = 0; i < _prisms.Count; i++)
            {
                var block = _prisms[i];
                if (block) block.NotifyPositionChanged();
            }
        }
    }
}
