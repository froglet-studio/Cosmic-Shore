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
    /// <see cref="Trail"/>) plus crystal pickups, laid out by a <see cref="MicroscenePlan"/>. The
    /// slot owns the continuity-law transitions for its content:
    ///
    ///   • First population lays prisms through the shared canonical primitive
    ///     (<see cref="PrismTrailBuilder"/>: Instantiate → ChangeTeam → pose → TargetScale → Trail →
    ///     Initialize → kind), a few per frame, so every prism grows in from zero via the batched
    ///     PrismScaleAnimator. Per-prism DOMAIN (incl. neutral Blue) and KIND (plain / danger /
    ///     shielded / supershielded) come themed on the plan; nothing pops.
    ///   • Recycling is TRANSPORT, not removal: the container suctions to a point (scale → ~0),
    ///     relocates, re-poses the SAME prism instances into a fresh plan — re-colouring and
    ///     re-theming each prism's kind reversibly (<see cref="PrismKinds.Retheme"/>) — and blooms
    ///     back out. Mass is conserved by construction. Prisms that fauna ate in the meantime (the
    ///     active-force sink) are re-minted through the sanctioned pool-reuse lifecycle
    ///     (<see cref="Prism.Initialize"/>), which is creation, never a resurrection of consumed mass.
    ///   • Lifeforms are NOT toy property: flora/fauna a plan requests are released into the host
    ///     <see cref="Cell"/> as ordinary citizens through the canonical cell spawn path
    ///     (<see cref="CellLifeSpawnerBase.SpawnFlora"/> / <c>SpawnFaunaWithDomain</c>), and are
    ///     never tracked, moved, or despawned by the toy.
    /// </summary>
    public class Microscene : MonoBehaviour
    {
        const float SuctionScale = 0.002f; // never exactly zero — keeps lossyScale well-formed

        static readonly Element[] PickupElements = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        Prism _prismPrefab;
        Crystal _omniCrystalPrefab;
        SkimmerCrystalEffectSO[] _crystalEffects;
        Trail _trail;
        readonly List<Prism> _prisms = new();
        readonly List<Crystal> _crystals = new();

        public string RecipeName { get; private set; } = "";
        public bool Busy { get; private set; }

        /// <summary>World anchor of the scene (its container position).</summary>
        public Vector3 Anchor => transform.position;

        /// <summary>
        /// The destination a recycle is transporting this scene to, set the instant
        /// <see cref="RecycleAsync"/> begins and cleared when it completes. During the ~2×
        /// transition the container is still visually suctioning at its OLD <see cref="Anchor"/>,
        /// but it has already CLAIMED this slot — the conveyor reads this so it neither double-fills
        /// the slot nor measures a stale reach while the bloom is in flight. Null when settled.
        /// </summary>
        public Vector3? PendingAnchor { get; private set; }

        /// <summary>The direction the scene is flown through (+z of the container).</summary>
        public Vector3 Forward => transform.forward;

        public static Microscene Create(Transform parent, string label)
        {
            var go = new GameObject($"Microscene_{label}");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Microscene>();
        }

        public void Configure(Prism prismPrefab, Crystal omniCrystalPrefab, SkimmerCrystalEffectSO[] crystalEffects)
        {
            _prismPrefab = prismPrefab;
            _omniCrystalPrefab = omniCrystalPrefab;
            _crystalEffects = crystalEffects;
        }

        // ── First population (grow-in) ───────────────────────────────────────

        /// <summary>
        /// Lay the scene for the first time at its current pose. Prisms are laid a few per frame
        /// (single-frame prism batches are a known spike) via the shared builder, each growing in
        /// from zero through its own scale animator — nothing pops in.
        /// </summary>
        public async UniTask PopulateAsync(MicroscenePlan plan, System.Random rng, CancellationToken ct)
        {
            Busy = true;
            try
            {
                RecipeName = plan.RecipeName;
                _trail = new Trail();

                // 1/frame on the stripped mobile build (6 otherwise): each Instantiate (GameObject
                // + collider + Prism init + spatial-index register) costs ~0.5-2ms on an old phone,
                // so 6/frame was a visible hitch on every populate. Instantiates only happen for the
                // first poolSize populates; recycles re-pose the same instances.
                int perFrame = PerfStrip.Enabled ? 1 : 6;
                await PrismTrailBuilder.LayBatched(_prismPrefab, plan.Prisms, transform, _trail, name, perFrame, ct, _prisms);

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
        /// re-pose the same prisms into <paramref name="plan"/> (fresh per-prism domain + kind),
        /// then bloom back out. Total mass in the belt is unchanged; only arrangement, place, colour,
        /// and kind vary.
        /// </summary>
        public async UniTask RecycleAsync(MicroscenePlan plan, Pose pose, System.Random rng,
            float transitionSeconds, CancellationToken ct)
        {
            Busy = true;
            PendingAnchor = pose.position; // claim the destination slot up-front (see PendingAnchor)
            try
            {
                await AnimateScaleAsync(1f, SuctionScale, transitionSeconds, ct);

                transform.SetPositionAndRotation(pose.position, pose.rotation);
                await RearrangeIntoAsync(plan, ct);

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
                PendingAnchor = null;
                Busy = false;
            }
        }

        /// <summary>
        /// Amortized re-pose: each prism's full re-initialize (ChangeTeam material state + spatial
        /// index unregister/re-register + density-grid re-file + coroutine start) costs ~50-200µs,
        /// so re-posing ~100 in one frame would be a recurring multi-ms spike on EVERY recycle.
        /// Yielding every few prisms spreads it invisibly — the container sits suctioned at ~zero
        /// scale while this runs.
        /// </summary>
        async UniTask RearrangeIntoAsync(MicroscenePlan plan, CancellationToken ct)
        {
            RecipeName = plan.RecipeName;

            const int perFrame = 5;
            int count = Mathf.Min(_prisms.Count, plan.Prisms.Count);
            for (int i = 0; i < count; i++)
            {
                var block = _prisms[i];
                if (!block) continue;

                var lay = plan.Prisms[i];

                // Wipe any previous kind BACK to plain before re-init, so a shielded/supershielded/
                // danger prism from the last arrangement can't leak its state (or its always-on
                // convex MeshCollider) into a plain slot. Reversible by construction.
                PrismKinds.Clear(block);

                block.ChangeTeam(lay.Domain);
                block.transform.localPosition = lay.Point.Position;
                block.transform.localRotation = lay.Point.Rotation;

                // Zero the scale so EVERY re-posed prism blooms from zero, uniformly. ResetState only
                // zeroes the eaten (disabled-animator) prisms; without this the surviving prisms
                // would keep their old grown scale and MORPH old→new instead of blooming, an
                // inconsistent transition (continuity law: nothing pops). Matches PopulateAsync.
                block.transform.localScale = Vector3.zero;

                // Full re-initialize for EVERY prism (not just eaten ones): ResetState unregisters
                // and CreateBlockCoroutine re-registers at the new position, which re-files the
                // cell's per-domain density grids — UpdatePosition alone never re-files them (the
                // registration-time-binding gap in Docs/SPATIAL_INDEX.md), so a moved-but-not-
                // reregistered prism would leave phantom fauna-sense density at the abandoned site.
                // For fauna-eaten slots this is the pool-reuse mint: ResetState re-arms the scale
                // animator that SetupDestruction disabled, so the replacement mass grows in from zero.
                block.Initialize();

                // AFTER Initialize — on an eaten slot the animator is only re-armed inside ResetState,
                // so an earlier write would be silently dropped.
                block.TargetScale = lay.Point.Scale;

                // Apply the new kind AFTER Initialize (which may have re-applied a stale baked flag);
                // additive on a now-plain prism.
                PrismKinds.Apply(block, lay.Kind);

                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            // Surviving crystals ride the belt to fresh plan slots (their value was stamped at full
            // scale when first minted); destroyed OR mid-collection ones drop out so TopUpCrystals
            // mints replacements. A crystal the player just skimmed is flying to the vessel on its
            // own — detach it from the container so the suction doesn't scale it mid-flight, and stop
            // treating it as a belt resident. (Omni crystals self-destroy on body-collection, so they
            // simply become null below.)
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
                Vector3 local = i < plan.Crystals.Count
                    ? plan.Crystals[i].LocalPosition
                    : plan.Crystals.Count > 0 ? plan.Crystals[^1].LocalPosition : Vector3.zero;
                // Overflow crystals (more survivors than plan slots) fan out vertically instead of
                // stacking co-located on the last slot.
                if (i >= plan.Crystals.Count)
                    local += Vector3.up * 16f * (i - plan.Crystals.Count + 1);
                _crystals[i].transform.localPosition = local;
            }
        }

        void TopUpCrystals(MicroscenePlan plan, System.Random rng)
        {
            for (int slot = _crystals.Count; slot < plan.Crystals.Count; slot++)
                MintCrystal(plan.Crystals[slot], rng);
        }

        // ── Crystals (elemental skims + omni jackpots, manager-less) ─────────

        void SpawnCrystals(MicroscenePlan plan, System.Random rng)
        {
            foreach (var drop in plan.Crystals)
                MintCrystal(drop, rng);
        }

        void MintCrystal(CrystalDrop drop, System.Random rng)
        {
            if (drop.Kind == CrystalKind.Omni)
                MintOmniCrystal(drop.LocalPosition, rng);
            else
                MintElementalCrystal(drop.LocalPosition, rng);
        }

        void MintElementalCrystal(Vector3 localPosition, System.Random rng)
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

            // The standalone elemental prefabs carry no collection components (lifeform prefabs add
            // them as authored overrides) — wire the same pair at runtime so the crystal is
            // skimmable: the impactor collects, the ImpactCollider lets the skimmer side react.
            var impactor = crystal.gameObject.AddComponent<ElementalCrystalImpactor>();
            impactor.Crystal = crystal;
            if (_crystalEffects is { Length: > 0 })
                impactor.SetCollectionEffects(_crystalEffects);
            crystal.gameObject.AddComponent<ImpactCollider>().SetImpactor(impactor);

            EnsureFadeIn(crystal);
            _crystals.Add(crystal);
        }

        /// <summary>
        /// The omni jackpot: a body-collected, any-domain pickup (fuel + speed buff) — the richer,
        /// rarer reward in the mix. Its prefab already carries OmniCrystalImpactor + ImpactCollider,
        /// so no runtime component wiring is needed; the manager-less defensive guards on
        /// Crystal/OmniCrystalImpactor make a local mint collectible without a CrystalManager.
        /// Falls back to an elemental pickup when no omni prefab is wired.
        /// </summary>
        void MintOmniCrystal(Vector3 localPosition, System.Random rng)
        {
            if (!_omniCrystalPrefab)
            {
                MintElementalCrystal(localPosition, rng);
                return;
            }

            var crystal = Instantiate(_omniCrystalPrefab, transform);
            crystal.transform.localPosition = localPosition;
            // A touch larger than an elemental skim — the omni is the "big" reward. Sized before
            // Start() so crystalValue (fuelAmount × lossyScale) reads the intended value.
            crystal.transform.localScale *= (float)(rng.NextDouble() * 0.2 + 0.2);
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            EnsureFadeIn(crystal);
            _crystals.Add(crystal);
        }

        // Continuity-law fade-in: some crystal prefabs carry FadeIn on their model renderers; any
        // that don't would pop into view. Add the standard component wherever it's missing.
        static void EnsureFadeIn(Crystal crystal)
        {
            foreach (var renderer in crystal.GetComponentsInChildren<Renderer>(true))
                if (!renderer.TryGetComponent(out FadeIn _))
                    renderer.gameObject.AddComponent<FadeIn>();
        }

        // ── Lifeforms (released to the cell — never toy property) ────────────

        void ReleaseLifeforms(MicroscenePlan plan, System.Random rng)
        {
            if (plan.FloraCount <= 0 && plan.FaunaCount <= 0) return;

            // Strictly the CONTAINING cell: the belt roams anywhere, and a lifeform released outside
            // a cell's sense radius would be a degraded citizen (no goals, no phase participation).
            // Open-space scenes simply stay prisms + crystals; the living recipes light back up
            // whenever the ride passes through a cell.
            var cell = Cell.FindCellContaining(transform.position);
            var profile = cell ? cell.Config?.SpawnProfile : null;
            if (!cell || profile == null) return;

            if (cell.FloraPlantingEnabled && profile.SupportedFloras is { Count: > 0 })
            {
                for (int i = 0; i < plan.FloraCount; i++)
                {
                    var cfg = profile.SupportedFloras[rng.Next(profile.SupportedFloras.Count)];
                    if (!cfg || !cfg.FloraPrefab) continue;
                    // Canonical cell spawn: random playable domain, Initialize(cell), Register. Flora
                    // re-disperses within the membrane in its own Plant(), so cell-centre spawn is fine.
                    CellLifeSpawnerBase.SpawnFlora(cell, cfg.FloraPrefab, null);
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
                    // …and the canonical prey-linked production gate (the ONE shared copy): no
                    // herbivore without enough opposing mass, no predator without enough prey — a
                    // fauna spawned into famine just withers in ~30s.
                    bool isPredator = cfg.FaunaPrefab.Diet == FaunaDiet.Predator;
                    if (!FaunaReproductionRules.PreyAvailable(isPredator, cell.GetLiveHerbivoreCount(),
                            cell.OpposingVolume(cell.ControllingDomain), profile.FaunaFoodFloor))
                        continue;

                    // Canonical regulated fauna spawn: controlling-colour only (locked invariant),
                    // seeded toward the scene's fresh mass, scattered on the buildup, lineage-bound.
                    var fauna = CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, cfg.FaunaPrefab,
                        transform.position, cell.ControllingDomain, ScatterAround(rng, 40f));
                    if (fauna) fauna.AssignLineage(cell, cfg);
                }
            }
        }

        Vector3 ScatterAround(System.Random rng, float radius)
        {
            var offset = new Vector3(
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1),
                (float)(rng.NextDouble() * 2 - 1)) * radius;
            return transform.position + offset;
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
        /// positions into the spatial index so AOE, occupancy, and fauna senses stay honest. Cheap —
        /// the index only rebuckets on 8m boundary crossings.
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
