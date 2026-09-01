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
    ///     Initialize → kind), TIME-budgeted with multithreaded clone batches - the same lay the
    ///     authored cell environments use for their 31-36k-prism worlds - so every prism grows in
    ///     from zero on the GPU clock. Per-prism DOMAIN (incl. neutral Blue) and KIND (plain /
    ///     danger / shielded / supershielded) come themed on the plan; nothing pops.
    ///   • Recycling is TRANSPORT, not removal, in three phases, none of which costs a per-frame
    ///     CPU pass over the scene's prisms:
    ///       1. COLLAPSE - one grow-clock re-stamp per prism toward <see cref="RetiredScale"/>,
    ///          budgeted; the GPU runs the shrink while gameplay state goes final immediately.
    ///       2. TRANSPORT - the stock is hidden (<see cref="Prism.HideForTransport"/>) and the
    ///          container moves in ONE transform write. Unseen by construction: the conveyor only
    ///          recycles a scene already wholly outside the camera frustum.
    ///       3. RE-POSE + BLOOM - the SAME prism instances take fresh plan slots, domains and kinds
    ///          (reversibly, <see cref="PrismKinds.Retheme"/>) and bloom back in from zero,
    ///          budgeted. Mass is conserved by construction. Prisms that fauna ate in the meantime
    ///          (the active-force sink) are re-minted through the sanctioned pool-reuse lifecycle
    ///          (<see cref="Prism.Initialize"/>), which is creation, never resurrection.
    ///     (The predecessor scaled the CONTAINER over ~2.4s and re-synced every child prism's
    ///     spatial entry AND companion render entity every frame to make that visible - ~180,000
    ///     writes per recycle at grand-assembly scale. Docs/PRISM_ANIMATION.md §5 C8.)
    ///   • Lifeforms are NOT toy property: flora/fauna a plan requests are released into the host
    ///     <see cref="Cell"/> as ordinary citizens through the canonical cell spawn path
    ///     (<see cref="CellLifeSpawnerBase.SpawnFlora"/> / <c>SpawnFaunaWithDomain</c>), and are
    ///     never tracked, moved, or despawned by the toy.
    /// </summary>
    public class Microscene : MonoBehaviour
    {
        /// <summary>
        /// The retired size a transported prism collapses to. NOT zero and not a free choice:
        /// <see cref="PrismScaleAnimator.SetTargetScale"/> clamps to its authored min scale
        /// (0.5), which is the floor the clock's grow stamp can shrink toward. At the belt's
        /// placement distances a 0.5-unit prism is sub-pixel, and the container's prisms are
        /// hidden outright before the move — the collapse is the visible half, the vanish is not.
        /// </summary>
        static readonly Vector3 RetiredScale = new(0.5f, 0.5f, 0.5f);

        /// <summary>
        /// Milliseconds per frame a transport may spend re-posing prisms. A grand scene carries
        /// thousands of prisms and each re-pose is a full reset+reinit (state clear, team, spatial
        /// unregister/register, SOAP raises) — doing them in one frame is a multi-hundred-ms stall.
        /// The whole transport is a background operation happening far from the player, so a thin
        /// slice is exactly right.
        /// </summary>
        const float TransportBudgetMsPerFrame = 3f;

        static readonly double MsPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

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
        /// but it has already CLAIMED this slot - the conveyor reads this so it neither double-fills
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
        /// from zero through its own scale animator - nothing pops in.
        /// </summary>
        public async UniTask PopulateAsync(MicroscenePlan plan, System.Random rng, CancellationToken ct)
        {
            Busy = true;
            try
            {
                RecipeName = plan.RecipeName;
                _trail = new Trail();

                // The TIME-budgeted streamed lay, the same primitive the cell environments use for
                // their 31-36k-prism worlds: multithreaded InstantiateAsync clone batches + a
                // shared per-frame millisecond budget, and it participates in the arena-ready gate
                // so the conveyor's first population can be held behind an EnvironmentLoadVeil.
                // (The old count-per-frame LayBatched had no gate integration and no batched
                // clone — at grand-assembly budgets that is the difference between a build and a
                // freeze.)
                await PrismTrailBuilder.LayBudgetedAsync(_prismPrefab, plan.Prisms, transform, _trail,
                    name, LayBudgetMsPerFrame, _prisms, ct);

                SpawnCrystals(plan, rng);
                ReleaseLifeforms(plan, rng);
            }
            finally
            {
                Busy = false;
            }
        }

        /// <summary>Ungated laying slice. The arena gate (a connecting screen, or the freestyle
        /// <c>EnvironmentLoadVeil</c> the conveyor raises for its first population) boosts this
        /// ~10× while it holds, so the whole stock builds behind the veil and the ungated value is
        /// only the fallback pace.</summary>
        const float LayBudgetMsPerFrame = 4f;

        // ── Recycle (collapse → transport → re-pose → bloom) ─────────────────

        /// <summary>
        /// Conveyor-belt transport: collapse the scene's prisms on the GPU clock, carry the stock
        /// to <paramref name="pose"/> while it is unseen, re-pose the SAME prisms into
        /// <paramref name="plan"/> (fresh per-prism domain + kind) and bloom them back in. Total
        /// mass in the belt is unchanged; only arrangement, place, colour, and kind vary.
        /// </summary>
        public async UniTask RecycleAsync(MicroscenePlan plan, Pose pose, System.Random rng,
            float transitionSeconds, CancellationToken ct)
        {
            Busy = true;
            PendingAnchor = pose.position; // claim the destination slot up-front (see PendingAnchor)
            Prism.BeginBulkTransport();
            try
            {
                // ── 1. COLLAPSE (the visible half, on the GPU clock) ─────────────
                // One shrink STAMP per prism, budgeted: each prism's grow clock is re-stamped
                // toward RetiredScale, so the shader runs the collapse with ZERO further CPU
                // writes. This replaces a per-frame container scale + a per-frame
                // NotifyPositionChanged sweep over every prism in the scene — at grand-assembly
                // budgets that sweep was ~180,000 spatial+entity writes per recycle, and it was
                // load-bearing (the container scale is invisible on the instanced path unless
                // every child entity is re-synced every frame). Docs/PRISM_ANIMATION.md §5 C8.
                await StampCollapseAsync(ct);
                await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0.05f, transitionSeconds)),
                    DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, ct);

                // ── 2. TRANSPORT (invisible by construction) ─────────────────────
                // The conveyor only ever recycles a scene that is fully outside the camera
                // frustum, so the vanish is unseen — hide the stock outright rather than
                // animating a second transition nobody can watch, then move the container in ONE
                // transform write.
                HideForTransport();
                transform.SetPositionAndRotation(pose.position, pose.rotation);

                // ── 3. RE-POSE + BLOOM (budgeted) ────────────────────────────────
                await RearrangeIntoAsync(plan, ct);

                // Replacement crystals mint only now, with the container settled at unit scale -
                // Crystal.Start() stamps crystalValue from lossyScale.
                TopUpCrystals(plan, rng);
                ReleaseLifeforms(plan, rng);
            }
            finally
            {
                Prism.EndBulkTransport();
                PendingAnchor = null;
                Busy = false;
            }
        }

        /// <summary>
        /// Re-stamps every prism's grow clock toward <see cref="RetiredScale"/> — the belt's
        /// suction, expressed as the law's one-shot initial-conditions write instead of a
        /// per-frame animation. Gameplay state (transform, volume, spatial index) goes final at
        /// each stamp, so the scene's footprint collapses immediately while the photons catch up.
        /// </summary>
        async UniTask StampCollapseAsync(CancellationToken ct)
        {
            long slice = StartSlice();
            for (int i = 0; i < _prisms.Count; i++)
            {
                var block = _prisms[i];
                if (!block || !block.isActiveAndEnabled) continue;
                block.TargetScale = RetiredScale; // setter = SetTargetScale + BeginGrowthAnimation (the stamp)

                if (!SliceExhausted(slice)) continue;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                slice = StartSlice();
            }
        }

        /// <summary>
        /// Drops the whole stock out of sight for the move. Not a continuity breach: the
        /// conveyor's removal gate guarantees the scene is wholly off-camera before a recycle
        /// starts, and the prisms re-enter through the standard creation bloom at the
        /// destination. Cheap — one visibility + collider write per prism, no re-registration.
        /// </summary>
        void HideForTransport()
        {
            for (int i = 0; i < _prisms.Count; i++)
            {
                var block = _prisms[i];
                if (block) block.HideForTransport();
            }
        }

        async UniTask RearrangeIntoAsync(MicroscenePlan plan, CancellationToken ct)
        {
            RecipeName = plan.RecipeName;

            int count = Mathf.Min(_prisms.Count, plan.Prisms.Count);
            long slice = StartSlice();
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
                // cell's per-domain density grids - UpdatePosition alone never re-files them (the
                // registration-time-binding gap in Docs/SPATIAL_INDEX.md), so a moved-but-not-
                // reregistered prism would leave phantom fauna-sense density at the abandoned site.
                // For fauna-eaten slots this is the pool-reuse mint: ResetState re-arms the scale
                // animator that SetupDestruction disabled, so the replacement mass grows in from zero.
                block.Initialize();

                // AFTER Initialize - on an eaten slot the animator is only re-armed inside ResetState,
                // so an earlier write would be silently dropped.
                block.TargetScale = lay.Point.Scale;

                // Apply the new kind AFTER Initialize (which may have re-applied a stale baked flag);
                // additive on a now-plain prism. Initialize has already cleared IsCreationComplete,
                // so PrismStateManager reads this as a BIRTH transition and the shield snaps
                // silently instead of opening a 0.35s morph across the creation reveal
                // (Docs/PRISM_ANIMATION.md §4.5).
                PrismKinds.Apply(block, lay.Kind);

                if (!SliceExhausted(slice)) continue;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                slice = StartSlice();
            }

            // Surviving crystals ride the belt to fresh plan slots (their value was stamped at full
            // scale when first minted); destroyed OR mid-collection ones drop out so TopUpCrystals
            // mints replacements. A crystal the player just skimmed is flying to the vessel on its
            // own - detach it from the container so the suction doesn't scale it mid-flight, and stop
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
            if (!set) return; // no elemental set in this project state - scenes still work without pickups

            var element = PickupElements[rng.Next(PickupElements.Length)];
            var prefab = set.GetPrefab(element);
            if (!prefab) return;

            var crystal = Instantiate(prefab, transform);
            crystal.transform.localPosition = localPosition;
            // MULTIPLY the prefab's authored scale (the elemental prefabs share one convention:
            // root 1.5, ~2 world units of visible crystal per unit of root scale). The multiplier
            // lands skims at ~1.5-3.5 visible world units. Sized before Start(): crystalValue and
            // the element-level gain both read lossyScale.
            crystal.transform.localScale *= (float)(rng.NextDouble() * 0.7 + 0.5);
            crystal.enabled = true;
            crystal.gameObject.SetActive(true);

            // The standalone elemental prefabs carry no collection components (lifeform prefabs add
            // them as authored overrides) - wire the same pair at runtime so the crystal is
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
        /// The omni jackpot: a body-collected, any-domain pickup (fuel + speed buff) - the richer,
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
            // A touch larger than an elemental skim - the omni is the "big" reward. Sized before
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

        // ── Lifeforms (released to the cell - never toy property) ────────────

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
                    // The species' live cap is the CELL's, and this conveyor is one of the flora
                    // PRODUCERS - a producer that skips the cap gives the species two ceilings
                    // (Docs/ECOSYSTEM.md §32). No-op for every species that authors none.
                    if (cell.IsFloraAtCap(cfg)) continue;
                    // Canonical cell spawn: random playable domain, Initialize(cell), Register. Flora
                    // re-disperses within the membrane in its own Plant(), so cell-centre spawn is fine.
                    CellLifeSpawnerBase.SpawnFlora(cell, cfg.FloraPrefab, null, cfg);
                }
            }

            if (profile.SupportedFaunas is { Count: > 0 })
            {
                for (int i = 0; i < plan.FaunaCount; i++)
                {
                    var cfg = profile.SupportedFaunas[rng.Next(profile.SupportedFaunas.Count)];
                    if (!cfg || !cfg.FaunaPrefab) continue;
                    // Respect the species' per-cell performance cap - the conveyor adds citizens,
                    // never a parallel population. Asked of the CELL so the host biome's own
                    // FaunaPopulationScale applies here too (Cell.ResolveFaunaPopulation).
                    if (cell.IsFaunaAtCap(cfg)) continue;
                    // …and the canonical prey-linked production gate (the ONE shared copy): no
                    // herbivore without enough opposing mass, no predator without enough prey - a
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

        // ── Frame budget ─────────────────────────────────────────────────────
        //
        // The transport passes touch every prism in a grand scene, and each touch is real work
        // (a shrink stamp raises a volume-delta SOAP; a re-pose is a full spatial unregister +
        // reinit + re-register). Both passes therefore run as thin per-frame slices rather than
        // one blocking loop. There is no per-frame cost AFTER a pass completes: the collapse and
        // the bloom are both GPU clock stamps.

        static long StartSlice() => System.Diagnostics.Stopwatch.GetTimestamp();

        static bool SliceExhausted(long since) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - since) * MsPerTick >= TransportBudgetMsPerFrame;
    }
}
