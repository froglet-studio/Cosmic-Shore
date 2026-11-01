using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for animal-like lifeforms and their managers.
    /// Uses virtual methods instead of abstract to satisfy LSP - subclasses only
    /// override what they need, and managers don't have to throw NotImplementedException.
    /// </summary>
    public abstract class Fauna : MonoBehaviour, ILifeFormEntity
    {
        [Header("Data References")]
        [Inject] GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Team & Goals")]
        [FormerlySerializedAs("Team")]
        public Domains domain;
        [SerializeField] float goalUpdateInterval = 5f;
        [Tooltip("Goal-update cadence multipliers indexed by CellAggressionLevel " +
                 "(Level0/Level1/Level2). Lower = faster relocation under stress.")]
        [SerializeField] float[] goalUpdateIntervalByAggression = { 1f, 0.55f, 0.25f };
        [Tooltip("Each fauna picks a stable random offset on a sphere of this radius " +
                 "and adds it to its resolved goal. Prevents the whole pack from " +
                 "converging onto a single point (e.g. the crystal at origin), which " +
                 "otherwise creates a depletion zone where fauna repeatedly consume " +
                 "the same prism configuration.")]
        [SerializeField] float goalOrbitRadius = 60f;

        Vector3 _goal;

        /// <summary>
        /// Where this creature is currently heading. A PROPERTY rather than a field so the
        /// cell's fauna containment (<see cref="Cell.ClampToFaunaContainment"/>) applies at the
        /// single point every writer must pass through - and there are many: this class's
        /// <see cref="ResolveGoal"/>, Boid's override, LightFauna's half-dozen direct writes on
        /// its own behavior tick, the spawner's initial goal, and the inheritance in
        /// <see cref="TryReproduce"/>. Clamping in each of them would be a rule you could forget
        /// to apply in the next grazer; clamping here is a rule that cannot be bypassed.
        /// No-op (returns the value unchanged) for every cell that authors no containment,
        /// which is all of them except a mode that pens its brood.
        /// </summary>
        public Vector3 Goal
        {
            get => _goal;
            set
            {
                // Two pens compose here, outermost first: the CELL's pen (one radius, every
                // creature) and then this SPECIES' own band (an annulus, authored per config -
                // see BandInner/BandOuter). Both are no-ops when unauthored, which is every
                // biome that is not a mode's pen, so the common path is still two compares.
                var clamped = cell ? cell.ClampToFaunaContainment(value, transform.position) : value;
                _goal = ClampToBand(clamped);
            }
        }

        // ── Per-species band (the layered pen) ────────────────────────────────
        //
        // A mode that stacks CONCENTRIC pens needs more than the cell's single containment
        // radius: Wildlife Liberation stocks three nested cages with three different tiers of
        // creature, and a lone radius can only express "inside R". The band generalizes it to
        // an ANNULUS (inner..outer) authored per species on FaunaConfigurationSO, so the
        // outer swarm rides the outer shell, the mid tier the mid shell, and the kaiju the
        // core - each unable to reach the others' feeding ground.
        //
        // Same contract as the cell pen and for the same reason (Docs/ECOSYSTEM.md §22): it is
        // a spatial DIET + STEERING rule, never a wall. Nothing is teleported, no collider is
        // added, no creature is culled for leaving. A creature can still drift across a
        // boundary on its own momentum - it simply has no reason to and nothing to eat there.
        float _bandInner;
        float _bandOuter;

        /// <summary>True when this creature is penned to an annulus of its own (see the band notes).</summary>
        protected bool HasBand => _bandOuter > 0f;

        /// <summary>
        /// The centre both pens measure from. The host cell when there is one (which is every
        /// spawner-born creature); this creature's own spawn point otherwise, so a manager-spawned
        /// or drone fauna with no cell simply has no band rather than one centred on the origin.
        /// </summary>
        Vector3 BandCentre => cell ? cell.transform.position : transform.position;

        /// <summary>True when <paramref name="position"/> lies in this creature's band (always true with no band).</summary>
        public bool IsInsideBand(Vector3 position)
        {
            if (!HasBand) return true;
            float d = (position - BandCentre).magnitude;
            return d >= _bandInner && d <= _bandOuter;
        }

        /// <summary>
        /// Pulls a goal back into this creature's band. Returns the point unchanged when there is
        /// no band or the goal is already inside, so the common path costs one compare. A goal
        /// beyond the outer wall is pulled in to it; a goal inside the inner wall is pushed out to
        /// it (a goal exactly at the centre picks the creature's own outward radial, so the push
        /// is well-defined rather than arbitrary).
        /// </summary>
        public Vector3 ClampToBand(Vector3 goal)
        {
            if (!HasBand) return goal;

            Vector3 centre = BandCentre;
            Vector3 offset = goal - centre;
            float d = offset.magnitude;

            if (d >= _bandInner && d <= _bandOuter) return goal;

            // A goal AT THE CENTRE is the ecology's "nothing sensed" answer
            // (Cell.GetDensestRegionAnyDomain falls back to the cell anchor on an empty grid),
            // not a destination. Clamping it radially would send every creature in the room to
            // exactly _bandInner - the whole population collapsing onto the inner wall as a
            // shell, which is the same clumping the spawner's per-creature spread exists to
            // avoid. Project the creature's OWN position instead, so an unfed creature mills
            // about where it already is inside its room.
            if (d <= 0.0001f)
            {
                Vector3 own = transform.position - centre;
                float ownD = own.magnitude;
                if (ownD <= 0.0001f) return centre + Vector3.up * _bandInner;
                return centre + own / ownD * Mathf.Clamp(ownD, _bandInner, _bandOuter);
            }

            return centre + offset / d * Mathf.Clamp(d, _bandInner, _bandOuter);
        }

        /// <summary>
        /// The herbivore diet rule as THIS creature sees it: the cell's spatialized rule
        /// (<see cref="Cell.IsPreyForHerbivore"/>) AND the creature's own band. Every grazer
        /// routes its edibility test through here rather than calling the cell directly, for the
        /// same reason <see cref="IsShieldedMass"/> exists - "a creature must never be led to
        /// mass it cannot reach or eat" is ONE rule, and a per-subclass copy is a rule you can
        /// forget to apply in the next grazer.
        /// </summary>
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain) => IsPreyForMe(position, preyDomain, domain);

        /// <summary>The same rule for an eater that is not this creature's own domain - a MultiDomain swarm's
        /// member eats as ITS domain (Docs/SWARM_FAUNA.md §16.4). One predicate, two callers.</summary>
        protected bool IsPreyForMe(Vector3 position, Domains preyDomain, Domains eaterDomain) =>
            cell != null && IsInsideBand(position) &&
            cell.IsPreyForHerbivore(position, eaterDomain, preyDomain);

        // ── The leash ─────────────────────────────────────────────────────────
        //
        // A vessel that tames and commands fauna (the Angler, R_VesselActions/ANGLER.md) holds an IFaunaLeash. While
        // leashed, a creature steers where its holder says and eats only the prism its holder orders, re-checked by its
        // OWN edibility rule and eaten by its OWN mouthful. Behaviour only: nothing is spawned, killed or recoloured, the
        // starvation clock keeps running, and the cell's pens still clamp every goal (Goal's setter). One holder at a
        // time; only the peer that SIMULATES the creature can leash it (a puppet's pose is replicated). Its one visual is
        // the holder's MARK (IFaunaLeash.TryGetLeashMark): the spindles lean toward the holder's domain hue, replicated
        // to puppets by FaunaNetworkSync; its domain, body prisms and diet are untouched.

        /// <summary>The holder steering this creature, or null when it is wild.</summary>
        public IFaunaLeash Leash { get; private set; }

        /// <summary>True while a holder steers this creature.</summary>
        public bool IsLeashed => Leash != null;

        /// <summary>Which creatures a leash can hold: those that eat prisms. A predator eats creatures, never prisms, so
        /// there is nothing to order it to eat.</summary>
        protected virtual bool CanBeLeashed => diet == FaunaDiet.Herbivore;

        /// <summary>Whether a leash can take this creature (or, read on a prefab, this species): <see cref="CanBeLeashed"/>.</summary>
        public bool Leashable => CanBeLeashed;

        /// <summary>Put this creature on <paramref name="holder"/>'s leash. False when it cannot be leashed, is dying,
        /// is a puppet on this peer, or another holder already has it - unless <paramref name="steal"/> (the Angler's
        /// Mass level-5 upgrade, <c>R_VesselActions/ANGLER.md</c> §1d): then the other holder is told it lost the creature
        /// (<see cref="IFaunaLeash.Released"/>) and this one takes it.</summary>
        public bool TryLeash(IFaunaLeash holder, bool steal = false)
        {
            if (holder == null || !CanBeLeashed || !IsSimAuthority || _diedThisLife) return false;
            if (Leash != null && !ReferenceEquals(Leash, holder))
            {
                if (!steal) return false;
                var previous = Leash;
                Leash = holder;
                previous.Released(this);
                ShowLeashMark();
                return true;
            }
            Leash = holder;
            ShowLeashMark();
            return true;
        }

        /// <summary>Let go (a no-op unless <paramref name="holder"/> holds it). The holder's own release, so it is not told.</summary>
        public void Unleash(IFaunaLeash holder)
        {
            if (!ReferenceEquals(Leash, holder)) return;
            Leash = null;
            if (!_diedThisLife) ShowLeashMark();
        }

        /// <summary>
        /// Wear the holder's mark, or none when wild, and publish it to the puppets. On the simulating peer. A dying
        /// creature keeps the mark it died in, so its wither does not change colour (<see cref="DropLeash"/> leaves it).
        /// </summary>
        void ShowLeashMark()
        {
            Domains markDomain = default;
            float lean = 0f, glow = 0f;
            if (Leash == null || !Leash.TryGetLeashMark(out markDomain, out lean, out glow)) lean = glow = 0f;
            ApplyLeashMark(markDomain, lean, glow);
            var sync = NetSync;
            if (sync) sync.PublishLeashMark(markDomain, lean, glow);
        }

        /// <summary>
        /// Re-read the holder's mark: its holder calls this when the mark changes while it holds the creature (the Jacks'
        /// herd lights up while herding and while called, <see cref="IFaunaLeash.TryGetLeashMark"/>). A no-op on a puppet,
        /// on a wild creature and on a dying one (it keeps the mark it died in).
        /// </summary>
        public void RefreshLeashMark()
        {
            if (Leash == null || !IsSimAuthority || _diedThisLife) return;
            ShowLeashMark();
        }

        static readonly List<Spindle> s_markSpindles = new();

        /// <summary>
        /// The leash's mark (Garrett, 2026-10-21: "distinguish them by giving their spindles a slight hue shift in your
        /// domain"): every spindle's colours lean <paramref name="lean"/> of the way toward <paramref name="markDomain"/>'s
        /// prism hue; 0 takes the mark off. <paramref name="glow"/> lights them up (colours x (1 + glow), HDR); 0 = unlit.
        /// Presentation only. Called by the leash on the simulating peer and by <see cref="FaunaNetworkSync"/> on a puppet.
        /// </summary>
        public void ApplyLeashMark(Domains markDomain, float lean, float glow)
        {
            var colors = PrismLit.ColorSet;
            Color hue = default;
            bool on = lean > 0f && colors && colors.TryGetPrismKindColors(markDomain, PrismKind.Plain, out hue, out _);
            GetComponentsInChildren(true, s_markSpindles);
            for (int i = 0; i < s_markSpindles.Count; i++)
                if (s_markSpindles[i]) s_markSpindles[i].SetHueLean(hue, on ? lean : 0f, glow);
            s_markSpindles.Clear();
        }

        /// <summary>The creature's own release: it is dying or being destroyed. The holder is told so it drops it.</summary>
        void DropLeash()
        {
            var holder = Leash;
            Leash = null;
            holder?.Released(this);
        }

        /// <summary>The speed a leash plans with: this creature's top swim speed as it swims now (u/s).</summary>
        public virtual float LeashTopSpeed => 0f;

        /// <summary>This creature's minimum swim speed (u/s).</summary>
        public virtual float LeashMinSpeed => 0f;

        /// <summary>How far this creature's mouth reaches (it eats from this far; u).</summary>
        public virtual float LeashMouthReach => 0f;

        /// <summary>Whether this creature's own diet would eat <paramref name="prism"/> now (domain, nucleus claim, pens,
        /// shields), asked by a holder BEFORE it gives the order, so it never sends a creature to a meal it will refuse
        /// on arrival. The same predicate the creature applies to an ordered meal; true for a species with no rule of its
        /// own.</summary>
        public virtual bool LeashCanEat(Prism prism) => prism && !prism.destroyed;

        /// <summary>Set the creature's velocity outright (a holder's throw: the creature leaves the ball at throw speed
        /// instead of easing up to it, and lands at its own swim speed). A no-op on a creature that does not integrate
        /// its own velocity, and on a puppet.</summary>
        public virtual void LeashLaunch(Vector3 velocity) { }

        /// <summary>A creature that DASHES toward a far goal (a forager boid's arrival-capped hunt) reports the dash
        /// multiplier and its behaviour tick, which cap how fast it can follow; false for a steady swimmer.</summary>
        public virtual bool TryGetLeashDash(out float dashMultiplier, out float tickSeconds)
        {
            dashMultiplier = 1f;
            tickSeconds = 1f;
            return false;
        }

        // Stable per-instance offset so each fauna orbits its resolved goal at a
        // different point. Seeded once at Start so the spread is deterministic per
        // spawn but varied across the pack.
        Vector3 _goalOrbitOffset;

        /// <summary>
        /// This creature's stable offset from whatever point it is currently seeking -
        /// the anti-convergence term. Subclasses that recompute <see cref="Goal"/> on
        /// their own cadence MUST add it wherever <see cref="ResolveGoal"/> does, or the
        /// pack collapses onto one point: every creature seeks the identical centroid,
        /// arrives, and its goal direction degenerates to zero. See
        /// <see cref="LightFauna"/>'s behavior tick.
        /// </summary>
        protected Vector3 GoalOrbitOffset => _goalOrbitOffset;

        [Header("Diet (predator / prey)")]
        [Tooltip("What this fauna eats - the predator/herbivore selector. Herbivore: " +
                 "opposing-domain prism MASS (flora canopy + vessel trails); the default, " +
                 "original behavior. Predator: herbivore FAUNA (ignores prism mass for " +
                 "feeding). Both starve via the clock below, so a Predator layered on " +
                 "Herbivores yields a two-tier Lotka-Volterra food web. Docs/ECOSYSTEM.md §7/§10.")]
        [SerializeField] protected FaunaDiet diet = FaunaDiet.Herbivore;

        /// <summary>What this fauna eats - the predator/herbivore selector. See <see cref="FaunaDiet"/>.</summary>
        public FaunaDiet Diet => diet;

        [Tooltip("Seconds after spawn during which this fauna CANNOT be eaten by a predator. " +
                 "All fauna spawn co-located at the cell centre, so without this a predator " +
                 "eats every herbivore the instant it spawns ('only sharks'). The grace window " +
                 "lets the swarm disperse first. See Docs/ECOSYSTEM.md §7.")]
        [SerializeField] protected float predationImmunitySeconds = 6f;

        // Set in Awake (runs during Instantiate, before any predator's behavior tick) so a
        // freshly-spawned creature is immune from frame zero, not only after its Start runs.
        float _spawnTime = -1f;

        /// <summary>
        /// Moves this creature's birth time back by <paramref name="ageSeconds"/>. For a creature whose GameObject is
        /// younger than the creature itself - a swarm member's proxy, materialised for a member that has lived in the
        /// simulation for minutes (Docs/SWARM_FAUNA.md §16.3) - so the post-spawn predation grace is measured from the
        /// member's birth, not from the proxy's. A newborn member keeps its full grace.
        /// </summary>
        protected void BackdateSpawn(float ageSeconds)
        {
            if (ageSeconds > 0f) _spawnTime = Time.time - ageSeconds;
        }

        /// <summary>
        /// A predator has this creature as its CURRENT target this frame. Nothing by default; a creature whose
        /// GameObject would otherwise be retired (a swarm member's proxy) keeps itself alive while it is hunted.
        /// </summary>
        public virtual void NotifyHunted() { }

        /// <summary>The authored post-spawn predation grace, in seconds (what <see cref="IsPredationImmune"/> measures).</summary>
        public float PredationImmunitySeconds => predationImmunitySeconds;

        /// <summary>True during the post-spawn grace window when this fauna can't be predated.</summary>
        public bool IsPredationImmune =>
            predationImmunitySeconds > 0f && _spawnTime >= 0f && (Time.time - _spawnTime) < predationImmunitySeconds;

        [Header("Population control (prey-linked)")]
        [Tooltip("Seconds of upkeep ONE nominal meal buys (the authored starvation clock, migrated - round 11f, " +
                 "Docs/ECOLOGY_LOD.md §2). The creature now has a conserved STOMACH: metabolism drains it " +
                 "(Capacity / starvationSeconds per second, paid to the cell's soil), every feed fills it with the " +
                 "volume actually eaten, and it starves when it is empty. A meal of at least one nominal prism (16) " +
                 "refills it, so it starves exactly starvationSeconds after its last such meal - as the clock did; " +
                 "smaller meals buy proportionally less. 0 = never starve. Concrete creature fauna (e.g. LightFauna) " +
                 "call NotifyFed(volume) on consume and despawn when IsStarving; manager-type Fauna ignore it. " +
                 "See Docs/ECOSYSTEM.md §6.")]
        [SerializeField] protected float starvationSeconds = 30f;

        [Tooltip("What the stomach holds, in prism volume. <= 0 (the default every existing asset carries) = one " +
                 "nominal prism (16), which is what makes the migrated starvationSeconds behave as the clock did. " +
                 "Raise it to let a creature bank several meals; its upkeep stays Capacity / starvationSeconds.")]
        [SerializeField] protected float stomachCapacity = -1f;

        // The conserved stomach (lazy: a level at a timestamp + a constant drain - nothing runs per frame).
        FaunaStomach _stomach;
        bool _stomachLive;
        // What a predator took when it ate this creature (stomach + body), read by the eater right after Predated.
        float _surrenderedMeal;

        /// <summary>True once metabolism has burned this creature's stomach to zero (never with starvationSeconds 0), unless
        /// its leash holder SUSTAINS it (<see cref="IFaunaLeash.Sustains"/>: the Angler's keeper and a herd's floor, the fenced exceptions).</summary>
        protected bool IsStarving => _stomachLive && _stomach.IsEmpty(Time.time) && !(Leash != null && Leash.Sustains(this));

        /// <summary>The stomach's current contents in prism volume (0 before the creature comes alive).</summary>
        public float StomachVolume => _stomachLive ? _stomach.Level(Time.time) : 0f;

        /// <summary>
        /// The meal a predator took from this creature in its last successful <see cref="Predated"/>: its stomach plus
        /// its body (<see cref="BodyMealVolume"/>). The eater passes it to <see cref="NotifyFed(float)"/> - predation
        /// moves the prey's mass into the predator, exactly (Docs/ECOLOGY_LOD.md §2).
        /// </summary>
        public float SurrenderedMeal => _surrenderedMeal;

        /// <summary>The body volume a devouring predator gains. One nominal prism unless a species says otherwise.</summary>
        protected virtual float BodyMealVolume => CellPhaseThresholds.NominalPrismVolume;

        void StartStomach()
        {
            float cap = stomachCapacity > 0f ? stomachCapacity : CellPhaseThresholds.NominalPrismVolume;
            _stomach = FaunaStomach.FromStarvationClock(starvationSeconds, cap);
            _stomach.Fill(Time.time);   // born fed: the clock started at birth
            _stomachLive = true;
        }

        void PayToSoil(float volume)
        {
            if (volume > 0f && cell) cell.DepositSoilNutrient(volume);
        }

        /// <summary>
        /// A feed of one nominal meal - for feeds that are not a measured volume (an own-domain pilot's Nourish).
        /// Feeding is also the reproduction trigger: prey converts to population (Docs/ECOSYSTEM.md §6), so every
        /// feed advances the birth counter and may birth offspring when the species' lineage config allows it.
        /// </summary>
        protected void NotifyFed() => NotifyFed(CellPhaseThresholds.NominalPrismVolume);

        /// <summary>
        /// This creature ate <paramref name="volume"/> of prism mass (a consumed prism's volume, or a prey's
        /// <see cref="SurrenderedMeal"/>): it fills the stomach, and the upkeep burned since the last feed plus any
        /// overflow goes to the cell's soil - conserved, never a clock reset.
        /// </summary>
        protected void NotifyFed(float volume)
        {
            if (!_stomachLive) StartStomach();
            if (UsesConservedStomach)
                PayToSoil(_stomach.Feed(volume, Time.time));
            else
                _stomach.Fill(Time.time);   // the shipped rule: any feed is a full refill, as the clock reset did
            TryReproduce();
        }

        /// <summary>
        /// True when this creature's cell opts into the conserved stomach (<see cref="CellConfigDataSO.ConservedFaunaStomach"/>,
        /// the Swarm cell only). Everywhere else a feed refills the stomach completely - the shipped starvation-clock rule -
        /// so the volume-paid metabolism stays contained to the cell that was tuned for it.
        /// </summary>
        bool UsesConservedStomach
        {
            get
            {
                var c = cell;
                var cfg = c ? c.Config : null;
                return cfg && cfg.ConservedFaunaStomach;
            }
        }

        // -------------------------------------------------------------------
        //  Reproduction - the population driver (retires the fixed-period
        //  spawner as the source of population; the spawner is now only a
        //  seeder). All tuning lives on the species' FaunaConfigurationSO; a
        //  fauna with no lineage (manager-spawned, drones) never reproduces.
        // -------------------------------------------------------------------

        Cell hostCell;
        FaunaConfigurationSO sourceConfig;

        /// <summary>The cell whose population this creature belongs to (null before
        /// <see cref="Initialize"/> / <see cref="AssignLineage"/>). Exposed for the
        /// replication layer, which resolves the species config as an index into this
        /// cell's own spawn profile.</summary>
        public Cell HostCell => hostCell;

        /// <summary>This individual's variant tuning block, or null if it rolled none.
        /// <see cref="FaunaNetworkSync"/> reads it to work out WHICH palette sibling supplied
        /// it, which is the half of the identity that element alone does not name.</summary>
        public FaunaVariantTuning VariantTuningForReplication => _variantPick?.Tuning;

        // This individual's rolled variant (element + the block expressing it). Passed to
        // offspring so a lineage breeds true instead of re-rolling per birth.
        LifeformVariantPick<FaunaVariantTuning>? _variantPick;

        // The prefab's own root scale, read before any variant rewrites it - so a pooled body that comes back as a
        // variant which keeps the prefab gets the prefab's size and proportions back, not the last variant's.
        Vector3 _authoredRootScale = -Vector3.one;

        /// <summary>The root scale the prefab authored (read before the first variant touched it).</summary>
        protected Vector3 AuthoredRootScale => _authoredRootScale.x < 0f ? transform.localScale : _authoredRootScale;

        /// <summary>This creature's element spread and reach (<see cref="FaunaElementalForm"/>): 1 unless it is Space.
        /// Multiplied where a spacing or a reach is READ.</summary>
        protected float ElementSpread { get; private set; } = 1f;
        protected float ElementReach { get; private set; } = 1f;

        /// <summary>The element this individual rolled (None before <see cref="AssignLineage"/>).</summary>
        protected Element RolledElement { get; private set; } = Element.None;

        // What an eroded Space spindle changed (the spindles' prefab meshes - a SkinnedMeshRenderer's or a static
        // MeshFilter's - and the body prisms' prefab seats), so a pooled body that comes back as another element gets the
        // prefab's body back. Null until a body is first eroded.
        Dictionary<Component, Mesh> _authoredSpindleMeshes;
        Dictionary<HealthPrism, Vector3> _authoredPrismSeats;

        /// <summary>How thick this body's thickest section is against its root scale: 1, or the Space spindle's eroded
        /// girth (<see cref="FaunaElementalForm.ThickestGirth"/>). A body-relative dial taken by GIRTH multiplies it in.</summary>
        protected float BodyGirthFactor { get; private set; } = 1f;
        bool lineageRegistered;
        int _feedsSinceBirth;
        float _lastBirthTime = float.NegativeInfinity;

        // Offspring appear within this radius of the parent - far enough not to
        // stack, close enough to join the parent's swarm/feeding ground.
        const float OffspringSpawnJitter = 25f;

        /// <summary>The species config this fauna was spawned from (null for manager-spawned/drone fauna).</summary>
        public FaunaConfigurationSO SourceConfig => sourceConfig;

        /// <summary>
        /// This individual's rolled variant - element, tuning, and the level it hatched at -
        /// or null before <see cref="AssignLineage"/> has run. Exposed so a subclass that
        /// produces a NEW individual outside the reproduction path can pass it on: the worm
        /// colony's split hands it to the severed half, because the two halves of a split
        /// worm are the same animal and must not re-roll their identity.
        /// </summary>
        protected LifeformVariantPick<FaunaVariantTuning>? VariantPick => _variantPick;

        /// <summary>
        /// Binds this fauna to its species lineage: the cell whose population it
        /// belongs to and the FaunaConfigurationSO that defines the species.
        /// Registers it in the cell's per-species live count (unregistered in
        /// OnDestroy). Called by the spawner after Initialize, and by a parent
        /// for its offspring - heredity is what lets reproduction recurse.
        /// </summary>
        public void AssignLineage(Cell host, FaunaConfigurationSO config) =>
            AssignLineage(host, config, null);

        /// <summary>
        /// Lineage bind with an optional INHERITED variant pick: a parent passes its own pick to
        /// its offspring so a lineage keeps its element (and the level it hatched at) instead of
        /// re-rolling a fresh identity every birth. Null rolls a new pick from the config - which,
        /// with spread off, is just the authored Element / Variant.
        /// </summary>
        public void AssignLineage(Cell host, FaunaConfigurationSO config,
            LifeformVariantPick<FaunaVariantTuning>? inherit)
        {
            hostCell = host;
            sourceConfig = config;
            if (host && config && !lineageRegistered)
            {
                host.RegisterLiveFauna(this);
                lineageRegistered = true;
            }

            // Elemental contract: the species config may define the ELEMENT as data (one base
            // prefab, FOUR data-defined variants) - re-provision the heart to that element if
            // the prefab-authored crystal disagrees, then apply the variant's expression
            // (behavior / body / audio / heart deltas that used to force a prefab variant per
            // element). It spawns AT size; nothing pops mid-life.
            if (config)
            {
                // The species' own pen (see the band notes above). Authored on the config so a
                // mode pens a TIER rather than a cell, and so an offspring inherits its parent's
                // band for free - it binds the same config.
                _bandInner = Mathf.Max(0f, config.BandInnerRadius);
                _bandOuter = Mathf.Max(0f, config.BandOuterRadius);
                if (_bandOuter > 0f && _bandInner > _bandOuter)
                    (_bandInner, _bandOuter) = (_bandOuter, _bandInner);

                // The spawner sets Goal before Initialize (cell still null, so the setter's
                // clamp was a no-op) - re-run it now that the band exists, or the creature's
                // first few seconds are spent swimming to a point outside its own pen.
                if (HasBand) Goal = _goal;

                var pick = config.RollVariant(inherit);
                _variantPick = pick;
                RolledElement = pick.Element;
                ElementSpread = FaunaElementalForm.Spread(pick.Element);
                ElementReach = FaunaElementalForm.Reach(pick.Element);

                if (pick.Element != Element.None)
                    ProvisionHeart(pick.Element);
                if (pick.Tuning is { Enabled: true })
                    ApplyVariantTuning(pick.Tuning);

                // RE-SIZE THE HEART LAST, and unconditionally. This is load-bearing and it is
                // easy to delete by accident, because from here it looks redundant with
                // Crystal.SetEmbeddedIn.
                //
                // A heart is written in WORLD scale, which LifeFormCrystal divides out the
                // parent chain to reach - so it is only correct while the parent chain it was
                // divided against is the final one. ProvisionHeart sizes the heart against the
                // body's CURRENT root scale, and ApplyVariantTuning then REWRITES that root
                // scale from the variant's BaseBodyScale. Without this line every creature that
                // authors a body scale wears a heart of `authored x BaseBodyScale` - 0.4 and 0.7
                // on the shipped tadpoles, i.e. a silent 2.5x and 1.43x cut to the one elemental
                // crystal size, with nothing reporting it.
                //
                // It also covers a SECOND inversion one level up: the Boid / LightFauna spawn
                // path runs Initialize (which provisions and sizes the heart) BEFORE
                // CellLifeSpawnerBase.SpawnFaunaBanded calls AssignLineage. Landing the size
                // here rather than inside ApplyVariantTuning covers both orderings, and covers
                // the variant that authors NO heart size of its own - which is the case that
                // makes a conditional re-size wrong.
                //
                // Until 2026-08 this job was done by Fauna.SetLevel, as an incidental
                // side-effect of seeding the spawn level (Docs/ECOSYSTEM.md §40.3).
                ApplyHeartSize();
            }
        }

        /// <summary>
        /// Element-as-data landing point: gives this creature its heart of the config's
        /// picked element. Default = the standard embedded-crystal provisioning every
        /// simple fauna uses. A COLONY overrides to route the element to its MEMBERS —
        /// the worm colony forwards it to every segment, each of which is its own fauna
        /// carrying its own heart (the colony root is the population anchor and is
        /// deliberately heartless, Docs/ECOSYSTEM.md §23.3).
        /// </summary>
        protected virtual void ProvisionHeart(Element element)
        {
            crystal = LifeFormCrystal.EnsureElementalCrystal(this, element);
            if (crystal) crystal.SetEmbeddedIn(this);
        }

        void TryReproduce()
        {
            // Birth is a decision - only the ONE simulation makes it, and the child then
            // replicates to every peer through the spawn seam below.
            if (!IsSimAuthority) return;

            var cfg = sourceConfig;
            var host = hostCell;
            if (!cfg || !host || cfg.FeedsPerOffspring <= 0 || !cfg.FaunaPrefab) return;

            _feedsSinceBirth++;
            // The cap is the CELL's, not the config's: a biome that scales its population
            // (SpawnProfileSO.FaunaPopulationScale) has to scale what reproduction may fill to,
            // or the seeder and the food web would be working to two different ceilings.
            // A herd's holder scales the feeds a birth takes (IFaunaHerd: the Jacks' Mass element).
            int feedsPerOffspring = Leash is IFaunaHerd herd
                ? Mathf.Max(1, Mathf.RoundToInt(cfg.FeedsPerOffspring * Mathf.Max(0f, herd.FeedsPerOffspringScale(this))))
                : cfg.FeedsPerOffspring;
            if (!FaunaReproductionRules.ShouldBirth(
                    _feedsSinceBirth, feedsPerOffspring,
                    Time.time - _lastBirthTime, cfg.ReproductionCooldownSeconds,
                    host.GetLiveFaunaCount(cfg), host.ResolveFaunaCap(cfg)))
                return;

            _lastBirthTime = Time.time;
            _feedsSinceBirth = 0;

            int offspring = Mathf.Max(1, cfg.OffspringPerBirth);
            for (int i = 0; i < offspring; i++)
            {
                // Re-check the cap per birth so a multi-offspring birth can't
                // overshoot the performance backstop.
                if (host.IsFaunaAtCap(cfg))
                    break;
                var child = SpawnOffspring(host, cfg);
                // A herded creature's offspring is born into the herd (IFaunaHerd.Born).
                if (child && Leash is IFaunaHerd herdHolder) herdHolder.Born(this, child);
            }
        }

        Fauna SpawnOffspring(Cell host, FaunaConfigurationSO cfg)
        {
            Vector3 pos = transform.position + Random.insideUnitSphere * OffspringSpawnJitter;
            var child = Instantiate(cfg.FaunaPrefab, pos, Quaternion.identity);
            child.domain = domain;
            child.Goal = Goal;
            // Same lifecycle as a spawner birth: Initialize grows the body prisms
            // and starts behavior; AssignLineage registers the species count and
            // passes heredity so the child can reproduce in turn. Predation
            // immunity (stamped in Awake) gives it time to disperse.
            child.Initialize(host);
            // Heredity: the child inherits this parent's variant pick - its element and the
            // level it hatched at - rather than rolling a new identity. In-world level-ups
            // (the Shepherd joust) are NOT inherited: acquired growth is not heritable.
            child.AssignLineage(host, cfg, _variantPick);
            host.RegisterSpawnedObject(child.gameObject);
            // AFTER AssignLineage: the lineage bind is what settles this child's element, and
            // the spawn payload carries that identity to every peer.
            FaunaNetworkSync.ServerSpawn(child);
            return child;
        }

        protected virtual void OnDestroy()
        {
            DropLeash();
            if (lineageRegistered && hostCell)
                hostCell.UnregisterLiveFauna(this);
            lineageRegistered = false;

            // Leave the cell's spawned-object list too. Flora does this in LifeForm.Die, but Fauna
            // is not a LifeForm and nothing else removed it, so every creature that died (or was
            // destroyed by a teardown, cell swap or split) left a dead entry behind and kept
            // Cell's LifeFormsInCell stat inflated. Safe for every path: the removal is a no-op
            // when the object was never registered or is already gone, and a cell that is itself
            // being destroyed is skipped by the null check.
            if (hostCell)
                hostCell.UnregisterSpawnedObject(gameObject);

            // Last line of defence for a DEFERRED heart (see DefersHeartRelease): an interrupted
            // wither - a cell drain, a manager pulling the husk, a turn ending - never reaches
            // the release inside the wither. This is a genuine recovery rather than a hopeful
            // one only because StashHeart already re-homed the crystal onto the cell at the top
            // of Die, so it is not a child of this husk and releasing it here still works.
            // Skipped during scene unload, where the cascade must not run at all (the same rule
            // Spindle.OnDisable follows) and where nothing survives to collect anyway.
            if (_diedThisLife && !_heartReleased && gameObject.scene.isLoaded)
                ReleaseHeart();
        }

        // --- ILifeFormEntity ---
        public Domains Domain => domain;
        public GameObject GetGameObject() => gameObject;

        /// <inheritdoc/>
        /// <remarks>The exact flag <see cref="Nourish"/> and <see cref="ApplyReplicatedDeath"/>
        /// already gate on. It is set at the TOP of <see cref="Die"/>, so it is true for the
        /// whole of a deferred wither - the window in which this creature's heart is still
        /// embedded but its owner is a corpse.</remarks>
        public bool IsDying => _diedThisLife;

        // --- Elemental contract: one base prefab, FOUR data-defined variants ---
        // There is no level (Docs/ECOSYSTEM.md §40). A creature is its species and its element;
        // the element states its body scale, prism shape, survival numbers, flocking, audio and
        // the size of its heart, exactly once, in its own variant tuning block.

        /// <summary>The element this creature carries - single source: its crystal (the heart).</summary>
        public Element Element => crystal ? crystal.crystalProperties.Element : Element.None;

        /// <summary>Current travel speed (world units/s). Mobile subclasses (Boid, LightFauna)
        /// override with their live velocity; manager/rooted fauna read as stationary.</summary>
        public virtual float CurrentSpeed => 0f;

        /// <summary>
        /// A faster vessel jousted this creature's heart - routes through Predated
        /// (idempotent, immunity-respecting) so it withers and drops its crystal.
        ///
        /// The style is stamped BEFORE the death runs because the death READS it: a joust
        /// frees the heart at once (the jouster reached in and took it) and the body then
        /// unravels from the hole outward, the mirror of a starvation wither. A joust that
        /// does not land - spawn immunity, or a creature already dying - leaves the style
        /// alone, so a later starvation still withers the ordinary way.
        /// </summary>
        public bool Jousted(string killerName)
        {
            var previousStyle = _deathStyle;
            _deathStyle = LifeformDeathStyle.Jousted;
            if (Predated(killerName)) return true;
            _deathStyle = previousStyle;
            return false;
        }

        /// <summary>
        /// Sizes this creature's heart to the one elemental crystal size
        /// (<see cref="CosmicShore.ScriptableObjects.ElementalCrystalSetSO.CrystalWorldScale"/>).
        /// Called from the spawn path BEFORE the creature is established, so it spawns AT size
        /// and nothing pops; called again whenever the body's scale is rewritten.
        /// </summary>
        public void ApplyHeartSize()
        {
            if (!crystal) crystal = GetComponentInChildren<Crystal>(true);
            LifeFormCrystal.ApplyHeartSize(crystal);
        }

        /// <summary>
        /// NOURISH: an own-domain pilot fed this creature (the Squirrel's Space-5 'Shepherd'
        /// joust). A creature's nourishment is a FEED — the starvation clock resets and the
        /// birth counter advances — so shepherding pays out as more of the thing you protected
        /// rather than as a bigger individual (Docs/ECOSYSTEM.md §40.4).
        ///
        /// <para>It routes through the ordinary <see cref="NotifyFed"/> path, so every gate an
        /// eaten prism passes applies: the reproduction quota, the per-individual cooldown, the
        /// cell's per-species cap and the Frenzy production freeze. A dying creature declines.</para>
        /// </summary>
        public virtual bool Nourish()
        {
            if (_diedThisLife) return false;
            NotifyFed();
            return true;
        }

        /// <summary>
        /// Applies the config's per-variant expression - the deltas that used to force a prefab
        /// variant per element (see FaunaVariantTuning). The base handles what every fauna has:
        /// body scale, spindle material, starvation, forager-agnostic survival, and the size of
        /// its heart; Boid layers the flocking numbers on top. Runs at AssignLineage, before the
        /// creature is visible-established (spawn-time - continuity is not violated).
        /// </summary>
        public virtual void ApplyVariantTuning(FaunaVariantTuning tuning)
        {
            if (tuning == null) return;

            if (_authoredRootScale.x < 0f) _authoredRootScale = transform.localScale;
            var scaleBefore = transform.localScale;
            transform.localScale = tuning.BaseBodyScale > 0f ? Vector3.one * tuning.BaseBodyScale : _authoredRootScale;
            // A resized body REBINDS its rig: Animation Rigging records every constraint's offset (the spine's
            // DampedTransforms, the jaws' aims) in WORLD units when the rig is built at spawn, so a body grown after it
            // would be pulled back to the prefab's bone spacing - the skin bunched up round the prisms the bones still
            // carry at their own scale (the Mass shark's clipping, 2026-10-27).
            if (transform.localScale != scaleBefore) RebuildRig();

            if (tuning.StarvationSeconds >= 0f)
            {
                starvationSeconds = tuning.StarvationSeconds;
                if (_stomachLive) StartStomach();   // re-derive the upkeep (spawn-time: nothing has been eaten yet)
            }

            // Every heart is the one elemental crystal size; the tuning's HeartWorldScale is no
            // longer read (Docs/ECOSYSTEM.md §40.2). The body scale was just rewritten above,
            // and AssignLineage re-applies this after the whole lineage lands.
            ApplyHeartSize();

            // Per-element body PRISM shape: retarget every body HealthPrism's TargetScale (the
            // bloom target - the clock growth stamp blooms toward it, so a post-Initialize
            // retarget still grows continuously, never pops).
            if (tuning.BodyPrismScale != Vector3.zero)
            {
                foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
                {
                    if (!hp) continue;
                    hp.AdmitTargetScale(tuning.BodyPrismScale);   // a stated size, not one grown into a bound
                    hp.TargetScale = tuning.BodyPrismScale;
                }
            }

            // ...and a Space spindle is eroded, its prisms re-seated in it and given its element's leaf law (or a pooled
            // body gets the prefab's skin, seats and prisms back).
            if (tuning.ErodeSpindle) ErodeSpindle(RolledElement);
            else if (_authoredPrismSeats != null)
            {
                RestoreSpindle();
                if (tuning.BodyPrismScale == Vector3.zero)
                    foreach (var hp in _authoredPrismSeats.Keys)
                        if (hp) SeatAuthoredSize(hp);
            }

            // Per-element body look: swap the spindle renderers' shared material (never
            // renderer.material - that clones). Crystal models keep their own materials.
            if (tuning.BodyMaterial)
            {
                foreach (var sp in GetComponentsInChildren<Spindle>(true))
                {
                    if (sp && sp.TryGetComponent<Renderer>(out var rend))
                        rend.sharedMaterial = tuning.BodyMaterial;
                }
            }

            // Per-element audio loop: retarget the FMOD emitter before its ObjectStart play
            // (AssignLineage runs in the spawn call, ahead of the emitter's Start). An empty
            // reference with OverrideAudio on silences the loop (the Space tadpole is silent).
            var emitter = tuning.OverrideAudio
                ? GetComponentInChildren<FMODUnity.StudioEventEmitter>(true)
                : null;
            if (emitter)
            {
                emitter.EventReference = tuning.AudioLoopEvent;
                if (tuning.AudioMinDistance >= 0f || tuning.AudioMaxDistance >= 0f)
                {
                    emitter.OverrideAttenuation = true;
                    if (tuning.AudioMinDistance >= 0f) emitter.OverrideMinDistance = tuning.AudioMinDistance;
                    if (tuning.AudioMaxDistance >= 0f) emitter.OverrideMaxDistance = tuning.AudioMaxDistance;
                }
            }
        }

        // Prefer the explicit host cell (set by Initialize/AssignLineage). The
        // cellData runtime SO is a SHARED asset holding only the LAST cell that
        // initialized it, so in multi-cell scenes the SO path can point a fauna at
        // the wrong cell; it remains the fallback for scene-placed managers that
        // are never Initialize(cell)-called. Unity-null guards on both so callers
        // just get null and skip the goal/avoidance branches that need a cell.
        protected Cell cell => hostCell ? hostCell : (cellData ? cellData.Cell : null);

        /// <summary>
        /// Squared length below which a steering vector counts as degenerate. A steering
        /// sum that cancels to ~zero normalizes to <see cref="Vector3.zero"/>, which zeroes
        /// the creature's velocity - and a motionless creature recomputes the identical
        /// zero from the identical position on every later tick, so the stall is PERMANENT
        /// (it hangs in place until it starves). Steering code must test against this and
        /// keep its last heading rather than publish a zero direction.
        /// </summary>
        protected const float DegenerateSteeringSqr = 1e-6f;

        /// <summary>
        /// Shielded mass is not food for ANY herbivore - the one canonical rule every
        /// species' edibility predicate routes through, so no new grazer can re-acquire
        /// the bug. A SUPER-shielded prism is fully invulnerable and a SHIELDED one only
        /// sheds its shield (see <see cref="Prism.Consume"/>), so a grazer that adopts one
        /// as its feed target approaches it, faces it, "eats" it, and finds it still there
        /// on the next mouthful-chaining query - grazing forever without ever removing
        /// mass. That is what parked brittlestars on Skim Race's super-shielded track
        /// prisms. Excluding shields from the predicate makes the creature skip straight
        /// to the next normal prism on the same behavior tick.
        ///
        /// <para>Public since round 11c: the threat flora (ThreatGrove) eat through the same rule, so a plant that
        /// eats is held to it exactly as a creature is.</para>
        /// </summary>
        public static bool IsShieldedMass(Prism prism)
        {
            var properties = prism ? prism.prismProperties : null;
            return properties != null && (properties.IsShielded || properties.IsSuperShielded);
        }

        /// <summary>
        /// The shield half of THIS creature's diet: true when <paramref name="prism"/> carries a shield it may not eat
        /// through. Every grazer's edibility predicate routes through here (not <see cref="IsShieldedMass"/>), so the two
        /// exceptions apply everywhere at once:
        /// <list type="bullet">
        /// <item>its cell is in a <b>Frenzy</b> (<see cref="Cell.ShieldedMassIsFood"/>; the designer, 2026-10-21: "in general
        /// shielded prisms should be food during a frenzy when the cell has too many prism");</item>
        /// <item>its herd's holder lets it eat shields (<see cref="IFaunaHerd.EatsShields"/>: the Jacks at Space 5).</item>
        /// </list>
        /// Super-shielded mass is never food. A shielded prism that is food is eaten whole: the grazer consumes it with
        /// <c>devastate</c> (<see cref="Prism.Consume"/> otherwise only sheds the shield).
        /// </summary>
        protected bool IsInedibleShield(Prism prism)
        {
            if (!IsShieldedMass(prism)) return false;
            if (prism.prismProperties.IsSuperShielded) return true;
            var c = cell;
            if (c && c.ShieldedMassIsFood) return false;
            return !(Leash is IFaunaHerd herd && herd.EatsShields(this));
        }

        /// <summary>
        /// Shared scratch buffer for Physics.OverlapSphereNonAlloc in fauna
        /// behavior ticks. All fauna tick on the main thread and consume the
        /// buffer within a single call, so one static buffer serves every
        /// creature - eliminating the per-tick Collider[] allocation that made
        /// large swarms GC-churn. NonAlloc silently truncates beyond capacity,
        /// which for steering/grazing just means considering a subset of an
        /// extremely dense neighborhood that tick.
        /// </summary>
        protected static readonly Collider[] OverlapScratch = new Collider[256];

        /// <summary>
        /// Shared scratch list for PrismSpatialIndex.QuerySphere in fauna behavior
        /// ticks - the prism half of the neighborhood scan (the physics half above
        /// now only carries non-prism populations like vessels). Same single-buffer
        /// rationale as <see cref="OverlapScratch"/>; reproduction's deferred first
        /// behavior tick (see Boid.CalculateBehaviorCoroutine) keeps a parent from
        /// having its snapshot clobbered mid-iteration.
        /// </summary>
        protected static readonly List<Prism> PrismScratch = new(256);

        /// <summary>
        /// Separate scratch list for the small per-MOUTHFUL cluster query intentional
        /// feeding runs when a herbivore actually consumes (LightFauna.ConsumeMouthful /
        /// Boid feeding). Kept distinct from <see cref="PrismScratch"/> because mouthful
        /// consumption fires from per-frame Update code, which must never clobber a
        /// behavior tick's in-flight neighborhood snapshot.
        /// </summary>
        protected static readonly List<Prism> FeedScratch = new(64);

        /// <summary>
        /// Overlap mask for the physics half of fauna scans: everything EXCEPT prism
        /// layers (TrailBlocks + Mound). Prisms - including other fauna's body
        /// HealthPrisms - are served by PrismSpatialIndex.QuerySphere instead, so the
        /// physics query stops paying broadphase + GetComponent costs for thousands
        /// of prism colliders (and stops truncating ships out of the 256-slot scratch
        /// in dense fields). Lazy so LayerMask resolves after engine init.
        /// </summary>
        static int s_nonPrismOverlapMask;
        protected static int NonPrismOverlapMask =>
            s_nonPrismOverlapMask != 0
                ? s_nonPrismOverlapMask
                : s_nonPrismOverlapMask = ~LayerMask.GetMask("TrailBlocks", "Mound");

        /// <summary>The same mask for a cell-wide vessel sense outside a creature (CellEcologyLod, round 11f).</summary>
        public static int VesselSenseMask => NonPrismOverlapMask;

        // --- Body prisms (the movers contract with PrismSpatialIndex) -------
        // Fauna bodies are HealthPrisms - registered prism mass that MOVES every
        // frame. The index stores positions, so the mover must keep them honest
        // (Docs/SPATIAL_INDEX.md): otherwise batch AOE hits the creature at its
        // spawn point and index-served fauna senses look for it where it used
        // to be.

        HealthPrism[] _bodyPrisms;

        /// <summary>The body's rig, re-bound at its current scale: the Animator back to its default pose, then every
        /// RigBuilder rebuilt so its constraints record their offsets at this size.</summary>
        void RebuildRig()
        {
            foreach (var animator in GetComponentsInChildren<Animator>(true))
                if (animator && animator.isActiveAndEnabled) animator.Rebind();
            foreach (var rig in GetComponentsInChildren<UnityEngine.Animations.Rigging.RigBuilder>(true))
                if (rig && rig.isActiveAndEnabled) rig.Build();
        }

        /// <summary>
        /// A body prism takes the size its prefab AUTHORED. <see cref="Prism.Initialize"/> re-reads the target through the
        /// scale animator's [0.5, 10] window, which suits a trail prism grown into a bound and silently trims a stated
        /// size: the shark's dorsal (19 long) came out 10, its wings 12 → 10, its teeth 15 → 10, the Brittlestar's 0.3
        /// spines 0.5 - on every element, so no prism filled its socket (the Mass shark showed it). Call after
        /// Initialize.
        /// </summary>
        protected static void SeatAuthoredSize(HealthPrism hp)
        {
            var authored = hp.AuthoredScale;
            if (authored.x <= 0f || authored.y <= 0f || authored.z <= 0f) return;
            hp.AdmitTargetScale(authored);
            hp.TargetScale = authored;
        }

        // One eroded copy of each prefab spindle mesh, shared by every Space instance (never mutated once built).
        sealed class ErodedSpindle
        {
            public SpindleErosion Map;
            public Mesh Mesh;
            public Vector3 Shift;          // mesh space: how far the spindle's root moved with the body it hangs from
            public Vector3[] Vertices;     // the prefab mesh's, for finding which spindle a prism sits in
        }
        static readonly Dictionary<Mesh, ErodedSpindle> s_erodedSpindles = new();
        static readonly HashSet<Mesh> s_unreadableSpindles = new();

        /// <summary>
        /// THE SPACE SPINDLE (Docs/ECOSYSTEM.md 64.9, Garrett 2026-10-28: "the space spindle needs to be universally
        /// scaled to bigger than mass, but we must erode and thin its thickest sections so the whole spindle gets more
        /// spindly"). The root is already scaled whole; this erodes every spindle of the body by
        /// <see cref="SpindleErosion"/> - the BODY (the skin with the most vertices) along its spine through the heart,
        /// every other skin of the body (the wings - never the heart, <see cref="IsSpindlePart"/>) along its own spine and then moved with the body where it is rooted, so it stays
        /// attached. Each body prism moves with the skin it sits in, and takes THE LATTICE FLORA'S PROPORTIONAL RULES:
        /// "an element authors the SHAPE of its plate, and the size is FITTED to the largest that clears" - the leaf law's
        /// shape (<see cref="FloraElementalForm.ShapeLeaf"/>, Space's needle), spanning its eroded socket's length less
        /// 3% (<see cref="FaunaElementalForm.FitClearance"/>), never thicker than the socket. Teeth keep their size.
        /// Runs at spawn, in the bind pose.
        /// </summary>
        void ErodeSpindle(Element element)
        {
            RestoreSpindle();   // a pooled body is measured from the prefab's seats, never its last life's

            // the body is the skin with the most vertices; where several share one mesh (a body built of braids, the
            // /fauna-creation kit's), the one that spans furthest in the world - the spine
            Component body = null;
            float bodySpan = 0f;
            var skins = SpindleMeshes();
            _authoredSpindleMeshes ??= new Dictionary<Component, Mesh>();
            foreach (var skin in skins)
            {
                if (!_authoredSpindleMeshes.ContainsKey(skin)) _authoredSpindleMeshes[skin] = MeshOf(skin);
                var m = _authoredSpindleMeshes[skin];
                float span = m ? RestMeshToWorld(skin, m).MultiplyVector(m.bounds.size).magnitude : 0f;
                int count = m ? m.vertexCount : 0, bodyCount = body ? _authoredSpindleMeshes[body].vertexCount : -1;
                if (!body || count > bodyCount || (count == bodyCount && span > bodySpan)) { body = skin; bodySpan = span; }
            }
            if (!body) return;

            var bodyMesh = _authoredSpindleMeshes[body];
            var bodyToWorld = RestMeshToWorld(body, bodyMesh);
            var worldToBody = bodyToWorld.inverse;
            var heart = crystal ? crystal.transform.position : transform.position;
            var bodySpindle = Eroded(bodyMesh, () => SpindleErosion.Measure(bodyMesh.vertices, worldToBody.MultiplyPoint3x4(heart)), null);
            if (bodySpindle == null) return;

            var hosts = new List<(Component skin, ErodedSpindle spindle, Matrix4x4 toWorld)>();
            foreach (var skin in skins)
            {
                if (!skin || !_authoredSpindleMeshes.TryGetValue(skin, out var mesh)) continue;
                var toWorld = RestMeshToWorld(skin, mesh);
                var spindle = skin == body ? bodySpindle : Eroded(mesh, () =>
                {
                    var v = mesh.vertices;
                    return SpindleErosion.Measure(v, SpindleErosion.Centroid(v));
                }, map =>
                {
                    // rooted where it comes nearest the body's spine; it moves as the body's skin there moves
                    var meshToBody = worldToBody * toWorld;
                    var v = mesh.vertices;
                    Vector3 root = default; float best = float.MaxValue;
                    for (int i = 0; i < v.Length; i++)
                    {
                        var p = meshToBody.MultiplyPoint3x4(v[i]);
                        float r = bodySpindle.Map.OffSpine(p);
                        if (r < best) { best = r; root = p; }
                    }
                    return meshToBody.inverse.MultiplyVector(bodySpindle.Map.Map(root) - root);
                });
                if (spindle == null) continue;
                SetMesh(skin, spindle.Mesh);
                hosts.Add((skin, spindle, toWorld));
            }
            BodyGirthFactor = bodySpindle.Map.Thickest;

            _authoredPrismSeats ??= new Dictionary<HealthPrism, Vector3>();
            foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
            {
                if (!hp || !hp.transform.parent || !IsSpindlePart(hp)) continue;
                if (!_authoredPrismSeats.ContainsKey(hp)) _authoredPrismSeats[hp] = hp.transform.localPosition;

                // the spindle it sits in: the one with a vertex nearest its centre
                var centre = hp.transform.position;
                int host = -1; float nearest = float.MaxValue;
                for (int h = 0; h < hosts.Count; h++)
                {
                    // measured in the WORLD: a braid stretched along its length only (the shipped Branch, the tadpole's,
                    // a /fauna-creation body) has no single unit, and one axis's would misjudge the nearest host
                    var toWorld = hosts[h].toWorld;
                    var verts = hosts[h].spindle.Vertices;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        float d = (toWorld.MultiplyPoint3x4(verts[i]) - centre).sqrMagnitude;
                        if (d < nearest) { nearest = d; host = h; }
                    }
                }
                if (host < 0) continue;
                var (_, seat, meshToWorld) = hosts[host];
                var worldToMesh = meshToWorld.inverse;
                var local = worldToMesh.MultiplyPoint3x4(centre);
                hp.transform.position = meshToWorld.MultiplyPoint3x4(seat.Map.Map(local) + seat.Shift);

                if (IsAuthoredDanger(hp)) continue;   // a tooth keeps its size, at its new seat (never a Charge body's armed prism)

                var authored = hp.AuthoredScale;
                var parent = hp.transform.parent.localToWorldMatrix;
                Vector3 size = default, perLocal = default, socket = default;
                for (int i = 0; i < 3; i++)
                {
                    var axis = parent.MultiplyVector(hp.transform.localRotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward));
                    perLocal[i] = axis.magnitude;                                        // world units per local unit
                    size[i] = authored[i] * perLocal[i];                                 // the prefab's prism, in world
                    var inMesh = worldToMesh.MultiplyVector(axis).normalized;
                    socket[i] = size[i] * seat.Map.MapDirection(local, inMesh).magnitude; // its seat, eroded
                }
                if (perLocal.x <= 1e-6f || perLocal.y <= 1e-6f || perLocal.z <= 1e-6f) continue;
                var shape = FloraElementalForm.ShapeLeaf(size, element);
                if (shape.x <= 0f || shape.y <= 0f || shape.z <= 0f) continue;
                // it spans its socket's LENGTH (less the lattice's 3%), the law sets how THIN it is against that length,
                // and it is never thicker than the socket
                int lng = shape.x >= shape.y && shape.x >= shape.z ? 0 : shape.y >= shape.z ? 1 : 2;
                float length = socket[lng] * FaunaElementalForm.FitClearance;
                Vector3 target = default;
                for (int i = 0; i < 3; i++)
                    target[i] = (i == lng ? length
                        : Mathf.Min(length * shape[i] / shape[lng], socket[i] * FaunaElementalForm.FitClearance)) / perLocal[i];
                hp.AdmitTargetScale(target);   // a needle may be longer than the animator's default window
                hp.TargetScale = target;
            }
        }

        /// <summary>
        /// True for a part of the creature's OWN body: not under its heart. Erosion is a change of shape, and anything
        /// below the body in the hierarchy is carried along by it - the Space heart is a skinned mesh of its own
        /// (<c>spacecrystalanim</c>) under the root, so the first pass eroded it with the wings, squashing the one round
        /// crystal (and every crystal a dead Space shark dropped kept the squashed mesh). The heart has its own size law
        /// (<see cref="LifeFormCrystal.ApplyHeartSize"/>); the body never reshapes it.
        /// </summary>
        static bool IsSpindlePart(Component part) => !part.GetComponentInParent<Crystal>(true);

        /// <summary>The cached eroded copy of <paramref name="source"/>, built on first use (null when the mesh is not
        /// readable - its import needs Read/Write - which is said once).</summary>
        static ErodedSpindle Eroded(Mesh source, System.Func<SpindleErosion> measure, System.Func<SpindleErosion, Vector3> shift)
        {
            if (s_erodedSpindles.TryGetValue(source, out var cached)) return cached;
            if (!source.isReadable)
            {
                if (s_unreadableSpindles.Add(source))
                    CSDebug.LogWarning($"[Fauna] {source.name} cannot be eroded into a Space spindle: enable Read/Write on its model import.");
                return null;
            }
            var map = measure();
            if (map == null) return null;
            var spindle = new ErodedSpindle { Map = map, Vertices = source.vertices };
            spindle.Shift = shift?.Invoke(map) ?? Vector3.zero;
            spindle.Mesh = map.Erode(source, spindle.Shift);
            if (!spindle.Mesh) return null;
            s_erodedSpindles[source] = spindle;
            return spindle;
        }

        /// <summary>A spindle mesh's mesh-to-world matrix in the bind pose: a skin's through its first bone (the bone's
        /// world matrix times its bind pose is the same for every bone at rest), anything else through its own transform.</summary>
        static Matrix4x4 RestMeshToWorld(Component part, Mesh mesh)
        {
            if (part is SkinnedMeshRenderer skin)
            {
                var bones = skin.bones;
                if (bones is { Length: > 0 } && bones[0] && mesh.bindposeCount > 0)
                    return bones[0].localToWorldMatrix * mesh.bindposes[0];
            }
            return part.transform.localToWorldMatrix;
        }

        /// <summary>
        /// The meshes the body is drawn with: every skin of the body, and every static mesh a <see cref="Spindle"/> draws
        /// (the QuadFish, Clawfish and tadpole bodies are MeshFilters, not skins). Never the heart's (<see cref="IsSpindlePart"/>)
        /// and never a prism's (its box and its shields are the prism's own geometry).
        /// </summary>
        List<Component> SpindleMeshes()
        {
            var parts = new List<Component>();
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin && skin.sharedMesh && IsSpindlePart(skin)) parts.Add(skin);
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
                if (filter && filter.sharedMesh && IsSpindlePart(filter) && !filter.GetComponentInParent<Prism>(true)
                    && filter.GetComponentInParent<Spindle>(true))
                    parts.Add(filter);
            return parts;
        }

        static Mesh MeshOf(Component part) => part is SkinnedMeshRenderer skin ? skin.sharedMesh : ((MeshFilter)part).sharedMesh;

        static void SetMesh(Component part, Mesh mesh)
        {
            if (part is SkinnedMeshRenderer skin) skin.sharedMesh = mesh;
            else ((MeshFilter)part).sharedMesh = mesh;
        }

        /// <summary>Puts back what <see cref="ErodeSpindle"/> changed: the prefab skins and the prisms' prefab seats.</summary>
        void RestoreSpindle()
        {
            BodyGirthFactor = 1f;
            if (_authoredSpindleMeshes != null)
                foreach (var kv in _authoredSpindleMeshes)
                    if (kv.Key) SetMesh(kv.Key, kv.Value);
            if (_authoredPrismSeats != null)
                foreach (var kv in _authoredPrismSeats)
                    if (kv.Key) kv.Key.transform.localPosition = kv.Value;
        }

        /// <summary>
        /// Caches this fauna's body HealthPrisms for the per-frame movement
        /// notification, and stamps each with its owner so fauna senses resolve
        /// "whose body is this prism" with a field read (HealthPrism.OwnerFauna)
        /// instead of a GetComponentInParent walk per neighbor per behavior tick.
        /// Call from Initialize, after the body hierarchy exists.
        /// Returns the cached array so subclasses can reuse it for body setup.
        /// </summary>
        protected HealthPrism[] CacheBodyPrisms()
        {
            _bodyPrisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < _bodyPrisms.Length; i++)
            {
                if (_bodyPrisms[i])
                    _bodyPrisms[i].OwnerFauna = this;
            }
            return _bodyPrisms;
        }

        /// <summary>The cached body prisms (see <see cref="CacheBodyPrisms"/>). May contain destroyed entries.</summary>
        protected HealthPrism[] BodyPrisms => _bodyPrisms;

        // The body prisms its PREFAB made dangerous (a shark's teeth, a worm's fangs), captured the first time the body is
        // armoured and before anything arms it - so a Charge body's elemental danger never reads as teeth. And the prisms
        // the ELEMENT changed, so a pooled body that comes back as another element sheds exactly that and nothing its
        // prefab authored (the worm head's shielded plate cage keeps its shield).
        HashSet<HealthPrism> _authoredDanger;
        readonly HashSet<HealthPrism> _elementArmed = new();
        Coroutine _bodyShieldRegen;
        WaitForSeconds _bodyShieldWait;

        /// <summary>True for a body prism its prefab authored dangerous (teeth, fangs, a stinger) - never one the
        /// element armed (<see cref="ArmourBody"/>). Mouths and jaws read this, not <c>IsDangerous</c>: on a Charge
        /// creature every body prism is dangerous, and a mouth at the centroid of the whole body is no mouth.</summary>
        protected bool IsAuthoredDanger(HealthPrism hp) =>
            hp && (_authoredDanger != null ? _authoredDanger.Contains(hp) : hp.prismProperties is { IsDangerous: true });

        /// <summary>
        /// THE ELEMENTAL ARMOUR on a creature's body (<see cref="ElementalArmour"/>, Docs/ECOSYSTEM.md §65; Garrett,
        /// 2026-10-28: "yes keep consistency" for fauna) - the same law a plant's leaves obey:
        /// <list type="bullet">
        /// <item><b>MASS is armoured.</b> Every plain body prism is shielded now and re-armoured one every
        /// <see cref="ElementalArmour.ShieldPeriod"/> seconds while the creature lives, so a Mass creature takes two
        /// passes. Its authored danger prisms stay danger (a shield clears danger - the two are exclusive by locked
        /// design).</item>
        /// <item><b>CHARGE is dangerous.</b> Every body prism is a danger prism, not only the teeth.</item>
        /// <item>Every other element wears its prefab's own body.</item>
        /// </list>
        /// Call after the body prisms are initialised and the heart carrying the element is in place. A previous life's
        /// elemental armour is taken off first, so a pooled body never keeps it; what the prefab authored is never touched.
        /// </summary>
        protected void ArmourBody()
        {
            var body = _bodyPrisms;
            if (body == null) return;
            if (_authoredDanger == null)
            {
                _authoredDanger = new HashSet<HealthPrism>();
                for (int i = 0; i < body.Length; i++)
                    if (body[i] && body[i].prismProperties is { IsDangerous: true }) _authoredDanger.Add(body[i]);
            }

            foreach (var hp in _elementArmed)
            {
                if (!hp || hp.destroyed || hp.prismProperties == null || _authoredDanger.Contains(hp)) continue;
                hp.prismProperties.IsDangerous = false;
                hp.DeactivateShields();
            }
            _elementArmed.Clear();
            if (_bodyShieldRegen != null)
            {
                StopCoroutine(_bodyShieldRegen);
                _bodyShieldRegen = null;
            }

            Element element = Element != Element.None ? Element : RolledElement;
            bool dangerous = ElementalArmour.ArmsEveryPrism(element);
            bool armoured = ElementalArmour.Shields(element);
            if (!dangerous && !armoured) return;
            for (int i = 0; i < body.Length; i++)
            {
                var hp = body[i];
                if (!hp || hp.destroyed || hp.prismProperties is not { IsDangerous: false } props) continue;
                if (dangerous)
                {
                    props.IsDangerous = true;
                    hp.MakeDangerous();
                    _elementArmed.Add(hp);
                }
                else if (!props.IsShielded && !props.IsSuperShielded)
                {
                    hp.ActivateShield();
                    _elementArmed.Add(hp);
                }
            }
            if (armoured) _bodyShieldRegen = StartCoroutine(RearmourBody());
        }

        /// <summary>Mass's re-armour: one plain body prism that has shed its shield takes it back every
        /// <see cref="ElementalArmour.ShieldPeriod"/> seconds - a plant's regen (<c>LifeForm.ShieldRegenCoroutine</c>)
        /// on a creature's body, which that coroutine never sees (a body prism is not the cell's grazeable mass).</summary>
        IEnumerator RearmourBody()
        {
            _bodyShieldWait ??= new WaitForSeconds(ElementalArmour.ShieldPeriod);
            int next = 0;
            while (true)
            {
                yield return _bodyShieldWait;
                var body = _bodyPrisms;
                if (body == null || body.Length == 0) continue;
                for (int tried = 0; tried < body.Length; tried++)
                {
                    var hp = body[next];
                    next = (next + 1) % body.Length;
                    if (!hp || hp.destroyed || hp.prismProperties is not { IsDangerous: false, IsShielded: false, IsSuperShielded: false })
                        continue;
                    hp.ActivateShield();
                    break;
                }
            }
        }

        /// <summary>True while any body prism is alive — the creature's health read.</summary>
        public bool HasLiveBodyPrisms
        {
            get
            {
                var prisms = _bodyPrisms;
                if (prisms == null) return false;
                for (int i = 0; i < prisms.Length; i++)
                {
                    var hp = prisms[i];
                    if (hp && !hp.destroyed) return true;
                }
                return false;
            }
        }

        // One-shot guard: a burst (a missile's AOE, a shotgun frame) can strip the last
        // several body prisms inside one frame, and every one of them calls back here.
        bool _diedFromBodyLoss;

        /// <summary>
        /// A prism of this creature's BODY was destroyed by an active force (vessel,
        /// projectile, AOE). Raised by <see cref="HealthPrism.Explode"/> through the
        /// stamped owner. <paramref name="killerName"/> is the attribution the destruction
        /// pipeline carried.
        ///
        /// THE PLAYER KILL PATH (platform-wide since Wildlife Liberation, 2026-08): when the
        /// LAST body prism is gone the creature dies through the sealed <see cref="Die"/> —
        /// it withers / is suctioned out (continuity of existence) and drops its elemental
        /// crystal (mass conserved), exactly like a starvation or predation death. Before
        /// this only <c>WormSegmentFauna</c> implemented it, so shooting any other creature
        /// stripped its body and left an immortal husk swimming; a whole class of vessel
        /// (the Sparrow, whose verbs ARE guns and missiles) could not kill wildlife at all.
        ///
        /// This is an ACTIVE force removing mass, so it is squarely inside the conserved-mass
        /// law — there is no timer, no lifespan and no cull here; a creature nobody shoots
        /// still only ever dies to starvation or predation. Subclasses may override to add
        /// topology bookkeeping (the worm colony re-links its chain first), but a creature
        /// that survives losing its whole body would violate the rule, so overrides must end
        /// in a death.
        /// </summary>
        public virtual void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            // Networked creature: the loss has to reach the ONE simulation, because a
            // projectile is a pooled LOCAL object - a client's bullet does not exist on the
            // server at all, and a creature that died only on the shooter's screen would swim
            // on for everyone else. The shooter's own prism is already gone (the hit reads
            // instantly); this tells the server, which resolves the same loss and, if it was
            // the last one, replicates the death back.
            var sync = NetSync;
            if (sync && sync.IsSpawned)
            {
                int index = IndexOfBodyPrism(prism);
                if (index >= 0)
                {
                    if (IsSimAuthority) sync.NotifyBodyPrismDestroyed(index, killerName);
                    else
                    {
                        sync.ReportBodyPrismDestroyed_ServerRpc(index, killerName ?? string.Empty);
                        return; // death is the server's to declare
                    }
                }
                else if (!IsSimAuthority) return;
            }

            if (_diedFromBodyLoss || HasLiveBodyPrisms) return;
            _diedFromBodyLoss = true;
            Die(killerName);
        }

        /// <summary>
        /// Pushes the body prisms' current positions into the spatial index. Call
        /// every frame after moving the creature. Cheap: the index only rebuckets
        /// when a body crosses an 8m occupancy-bucket boundary; unregistered
        /// bodies (inside Prism.waitTime) no-op.
        /// </summary>
        // Per-frame body sync for every moving creature: one index update, one shell refresh and
        // one render-matrix write per body prism. Named so a Profiler capture can separate it
        // from the rest of a creature's Update (PERFORMANCE_OPTIMIZATION.md §1, S5).
        static readonly Unity.Profiling.ProfilerMarker s_BodySyncMarker = new("Fauna.BodySync");

        protected void NotifyBodyPrismsMoved()
        {
            var prisms = _bodyPrisms;
            if (prisms == null) return;
            using (s_BodySyncMarker.Auto())
            {
                for (int i = 0; i < prisms.Length; i++)
                {
                    var hp = prisms[i];
                    if (hp) hp.NotifyPositionChanged();
                }
            }
        }

        protected virtual void Awake()
        {
            // Stamp spawn time as early as possible (Instantiate runs Awake synchronously,
            // before the spawner calls Initialize or any predator's first behavior tick), so
            // predation immunity is active from the moment the creature exists.
            _spawnTime = Time.time;

            // Emitter-driven loops (brittlestar / shark / tadpole) must honour the SFX slider:
            // StudioEventEmitter never sets instance volume itself, so bind it here.
            if (!TryGetComponent<CosmicShore.Gameplay.Audio.EmitterSfxVolumeBinder>(out _)
                && GetComponentInChildren<FMODUnity.StudioEventEmitter>(true))
                gameObject.AddComponent<CosmicShore.Gameplay.Audio.EmitterSfxVolumeBinder>();
        }

        protected virtual void Start()
        {
            if (domain == Domains.Blue)
                CSDebug.LogWarning($"{name}: Population domain is Blue (sentinel). Assign a real domain before spawning, or set it on the prefab.");

            _goalOrbitOffset = Random.onUnitSphere * Mathf.Max(0f, goalOrbitRadius);
            StartStomach(); // the stomach starts full when the creature comes alive (the clock started here)

            // A replicated puppet resolves no goals: where it swims is the server's decision,
            // arriving through its NetworkTransform. (The client-side CPU this saves is the
            // decision half of every tick - a net client saving, not a cost.)
            if (IsSimAuthority)
                StartCoroutine(UpdateGoalCoroutine());
        }

        /// <summary>
        /// Initialize this fauna with its parent cell. Overrides must call base -
        /// the base remembers the spawning cell explicitly (the shared cellData SO
        /// only tracks the LAST cell that initialized it, which is wrong in
        /// multi-cell scenes). Otherwise intentionally minimal so managers and
        /// stubs don't need to throw NotImplementedException.
        /// </summary>
        public virtual void Initialize(Cell cell)
        {
            hostCell = cell;
        }

        // ===================================================================
        //  Network replication - authority, puppets, replicated decisions
        //  (Docs/ECOSYSTEM_NETWORK_SYNC.md; the transport itself lives in
        //   FaunaNetworkSync, the ONE file here that imports Unity.Netcode)
        // ===================================================================

        FaunaNetworkSync _netSync;

        /// <summary>
        /// The replication component, if this species has been rolled out (see the per-species
        /// rollout). Null on every unnetworked prefab, which is what keeps the whole feature
        /// opt-in per species.
        /// </summary>
        protected FaunaNetworkSync NetSync => _netSync ? _netSync : (_netSync = GetComponent<FaunaNetworkSync>());

        /// <summary>
        /// True when THIS peer decides what this creature does - goals, feeding, starvation,
        /// predation, reproduction, death. False only on a party CLIENT's replicated puppet.
        ///
        /// Every DECISION is gated on this; nothing else is. A puppet still moves (its
        /// NetworkTransform moves it), still keeps the movers contract, and still GRAZES its own
        /// peer's prisms - because fauna consumption is the only legal down-force on mass and a
        /// client whose creatures ate nothing would accumulate prisms without a sink.
        /// </summary>
        public bool IsSimAuthority { get; private set; } = true;

        /// <summary>
        /// Turn this creature into a replicated puppet. Called by <see cref="FaunaNetworkSync"/>
        /// BEFORE the client's visual init, so the sim halves never start in the first place
        /// rather than being started and then stopped.
        /// </summary>
        public void EnterPuppetMode()
        {
            IsSimAuthority = false;
            OnEnteredPuppetMode();
        }

        /// <summary>Subclass hook for anything that must stop when this creature becomes a
        /// puppet (a manager registration, a steering coroutine started outside Start).</summary>
        protected virtual void OnEnteredPuppetMode() { }

        /// <summary>
        /// Rebuild this puppet as the SAME individual the server spawned, from the replicated
        /// identity. This is not cosmetic: a lifeform is its species and its element and nothing
        /// else (Docs/ECOSYSTEM.md §40), and the element states the body scale, the variant
        /// tuning and the HEART SIZE - and a heart's world scale IS the collect reward and the
        /// collect reward. A client that re-rolled its own element would pay a different
        /// price for the same kill.
        ///
        /// Routed through the ordinary <see cref="AssignLineage"/> inherit path, so the puppet
        /// takes exactly the code a server-side offspring takes; nothing is re-rolled.
        /// </summary>
        public void ApplyReplicatedIdentity(Cell host, FaunaConfigurationSO config,
                                            FaunaConfigurationSO paletteSibling, Element element)
        {
            if (config)
            {
                var tuning = paletteSibling ? paletteSibling.Variant : config.Variant;
                AssignLineage(host, config, new LifeformVariantPick<FaunaVariantTuning>(element, tuning));
                return;
            }

            // Unlisted species (spawned outside the host cell's profile - a toy release, the
            // freestyle conveyor). Still honour the element so the heart matches; there is no
            // config to bind a lineage to.
            if (element != Element.None) ProvisionHeart(element);
        }

        /// <summary>
        /// The server decided this creature died. Run the SAME sealed death locally so each peer
        /// drops its own crystal and withers its own body: mass is conserved on every peer and
        /// nothing pops out of existence (the continuity law). The style travels with the
        /// decision because the style IS the animation - a jousted creature unravels outward
        /// from the heart, a starved one inward from its extremities, a devoured one suctions
        /// into a mouth (Docs/ECOSYSTEM.md §26).
        /// </summary>
        public void ApplyReplicatedDeath(LifeformDeathStyle style)
        {
            if (_diedThisLife) return;
            _deathStyle = style;
            // Attribution is a DECISION and belongs to the server: ReportKill is authority-gated,
            // so this local death scores for nobody and cannot double-credit the shooter.
            Die();
        }

        /// <summary>
        /// Apply the loss of the body prism at <paramref name="prismIndex"/> that another peer
        /// resolved. The index is a position in <see cref="BodyPrisms"/> -
        /// <c>GetComponentsInChildren</c> order over an identical prefab hierarchy, so it names
        /// the same prism on every peer with nothing extra to keep in sync. Idempotent: a prism
        /// already gone is a no-op, which is what makes the shooter's own echo harmless.
        /// </summary>
        public void ApplyReplicatedBodyPrismLoss(int prismIndex, string killerName)
        {
            var prisms = _bodyPrisms;
            if (prisms == null || prismIndex < 0 || prismIndex >= prisms.Length) return;
            var hp = prisms[prismIndex];
            if (!hp || hp.destroyed) return;

            // Devastate so the prism actually dies rather than shedding a shield: the peer that
            // fired already resolved that this hit was lethal to the prism, and re-running the
            // shield ladder here would leave the two peers' bodies different.
            hp.Damage(Vector3.zero, domain, killerName, devastate: true);
        }

        /// <summary>Index of a body prism for replication, or -1 if it is not one of ours.</summary>
        protected int IndexOfBodyPrism(HealthPrism prism)
        {
            var prisms = _bodyPrisms;
            if (prisms == null || !prism) return -1;
            for (int i = 0; i < prisms.Length; i++)
                if (ReferenceEquals(prisms[i], prism)) return i;
            return -1;
        }

        /// <summary>
        /// Removal routing for a spent husk. Returns true when the network layer owns the
        /// removal (the server despawns after a grace; a client does nothing and lets the
        /// server's despawn destroy the object - a client destroying a replicated NetworkObject
        /// is an NGO error). Returns false for an unnetworked creature, so every existing
        /// removal path runs exactly as before.
        ///
        /// This hooks the HUSK-REMOVAL points, deliberately not <see cref="Die"/>: Die is sealed
        /// and must keep running on every peer (it is what conserves the mass), while the object
        /// may only be destroyed once, by its owner.
        /// </summary>
        protected bool DespawnOrDestroy()
        {
            var sync = NetSync;
            return sync && sync.HandleHuskRemoval();
        }

        /// <summary>
        /// The elemental crystal this fauna conserves its mass into on death. Set by
        /// concrete creature subclasses in Initialize via
        /// <see cref="LifeFormCrystal.EnsureElementalCrystal"/>; null only for MANAGER
        /// fauna and for a colony ROOT, which is a population anchor rather than an
        /// organism — every actual member of a colony carries its own heart
        /// (Docs/ECOSYSTEM.md §23.3).
        /// </summary>
        protected Crystal crystal;

        /// <summary>The heart while this creature LIVES with it embedded (null once released). What a
        /// blast's lifeform-crystal effects act on when they reach a creature through its body rather
        /// than through a collider overlap (<see cref="VirtualFauna"/>, Docs/SWARM_FAUNA.md §19).</summary>
        public Crystal LivingHeart => crystal && crystal.IsEmbedded ? crystal : null;

        // How this creature came apart - see LifeformDeathStyle. Written by the force that
        // killed it (Jousted / the devour overload of Predated); starvation and every other
        // death leave the default.
        LifeformDeathStyle _deathStyle = LifeformDeathStyle.Withered;

        /// <summary>How this creature is coming apart - read by subclass death animations.</summary>
        protected LifeformDeathStyle DeathStyle => _deathStyle;

        bool _heartReleased;
        bool _diedThisLife;   // Die ran - arms the OnDestroy heart backstop below

        /// <summary>
        /// True when this subclass runs a progressive wither that calls
        /// <see cref="ReleaseHeart"/> itself at the moment the wither reaches the core -
        /// the "outside-in until the crystal becomes collectable" death. Everything else
        /// drops the heart inside <see cref="Die"/> exactly as before, so the crystal
        /// invariant cannot be lost by forgetting to opt in. A subclass that DOES opt in
        /// must release, and <see cref="OnDestroy"/> screams if it didn't.
        /// </summary>
        protected virtual bool DefersHeartRelease => false;

        /// <summary>
        /// Death chokepoint - SEALED so no fauna can die without conserving its mass.
        /// Every death path (starvation, <see cref="Predated"/>, a jouster taking the
        /// heart, losing the last body prism) routes here; it frees the elemental crystal
        /// (the locked "every lifeform drops one elemental crystal on death, mass is
        /// conserved" invariant - the creature does not just vanish) and then runs
        /// subclass removal via <see cref="OnDeath"/>.
        ///
        /// WHEN the heart is freed is part of HOW the creature died (Docs/ECOSYSTEM.md §26):
        ///   • Jousted  - a vessel reached in and TOOK it, so it is free immediately and
        ///                the joust chain awards it to that pilot.
        ///   • Consumed - a predator is tearing the body apart around it; free immediately,
        ///                as it always has been.
        ///   • Withered - the body is spent from the outside in and the heart is the LAST
        ///                thing standing, so a subclass with a progressive wither
        ///                (<see cref="DefersHeartRelease"/>) releases it when the wither
        ///                reaches the core. Everything else still releases here.
        /// </summary>
        protected void Die(string killerName = "")
        {
            _diedThisLife = true;
            DropLeash();
            // the stomach closes its books: what is left (0 after a predator took it) and the upkeep burned since the
            // last settle go to the soil - the stomach never holds mass past its creature (round 11f)
            if (_stomachLive)
            {
                float left = _stomach.SurrenderAll(Time.time, out float burned);
                PayToSoil(left + burned);
                _stomachLive = false;
            }

            // Replicate the DECISION before running it, so every peer starts its own wither at
            // roughly the same moment rather than RTT after this one has finished. No-op when
            // this creature is not networked, and on a client (whose Die is itself the mirror).
            var sync = NetSync;
            if (sync) sync.NotifyDied(_deathStyle);

            if (_deathStyle != LifeformDeathStyle.Withered || !DefersHeartRelease)
                ReleaseHeart();
            else
                StashHeart();

            ReportKill(killerName);
            OnDeath(killerName);
        }

        /// <summary>
        /// Re-homes the heart onto the cell but leaves it EMBEDDED - still uncollectable, still
        /// wearing the neutral heart tint - for a wither that will release it when it reaches the
        /// core. Doing this at the START of the death is what makes the deferral SAFE rather than
        /// merely late: a crystal still parented to the husk dies with it, so an interrupted
        /// wither (a cell drain, a manager removing the husk, a scene unload) would silently lose
        /// the crystal. With it re-homed, <see cref="ReleaseHeart"/> works from anywhere,
        /// including the <see cref="OnDestroy"/> backstop.
        /// </summary>
        void StashHeart()
        {
            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
                crystal.DetachHeartToCell();
        }

        /// <summary>
        /// Frees this creature's heart into the world as the collectible elemental crystal -
        /// the sealed half of "every lifeform drops one elemental crystal on death".
        /// ActivateCrystal reparents it to the cell, so it survives this object's
        /// destruction as a powerup. Idempotent: the wither, the husk removal and the
        /// backstop can all call it.
        /// </summary>
        protected void ReleaseHeart()
        {
            if (_heartReleased) return;
            _heartReleased = true;

            if (crystal && crystal.gameObject && crystal.gameObject.activeInHierarchy)
                crystal.ActivateCrystal();
        }

        /// <summary>
        /// Leaves this creature's body prisms in the world as its SKELETON - the frame stays
        /// where it died instead of being destroyed with the husk (Docs/ECOSYSTEM.md §26).
        /// Called by the wither styles (starvation, joust); a devoured creature does NOT
        /// call it, because there the mass genuinely transfers to the predator.
        ///
        /// Run this BEFORE withering any spindle: a body prism is parented to a spindle, so
        /// evaporating the spindle first would destroy the very mass the skeleton conserves.
        /// </summary>
        protected void LeaveSkeleton()
        {
            var host = cell;
            Transform skeletonParent = host ? host.transform : null;

            var prisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < prisms.Length; i++)
            {
                var hp = prisms[i];
                if (hp && !hp.destroyed) hp.LeaveAsSkeleton(skeletonParent, recycleWhenDestroyed: true);
            }
        }

        /// <summary>
        /// Publishes an ATTRIBUTED death on the cell's SOAP channel - the fauna half of the
        /// stat <see cref="LifeForm.OnLifeFormDeath"/> already reports for flora, routed through
        /// a ScriptableEvent rather than a static event per the SOAP policy. StatsManager
        /// (server only) turns it into <see cref="IRoundStats.LifeformsKilled"/>.
        ///
        /// Only PLAYER-attributed deaths are published. The ecology's own deaths carry engine
        /// attribution ("starvation", a predator's name, a colony wither reason) and must not
        /// score for anyone - a mode whose objective is killing wildlife cannot have the wildlife
        /// killing itself onto the scoreboard. StatsManager resolves the name against the player
        /// roster anyway, so this is belt-and-braces on a hot-ish path.
        /// </summary>
        void ReportKill(string killerName)
        {
            // Attribution belongs to the ONE simulation. Without this gate a networked kill
            // would be counted twice for the shooter: once by the server crediting directly,
            // and again by the client's replicated death re-entering StatsManager's
            // client branch and firing Player.ReportFaunaKill_ServerRpc.
            if (!IsSimAuthority) return;
            if (string.IsNullOrEmpty(killerName)) return;
            if (killerName == StarvationKiller) return;

            var runtimeData = hostCell ? hostCell.RuntimeData : cellData;
            if (runtimeData) runtimeData.OnFaunaKilled.Raise(killerName);
        }

        /// <summary>Attribution string every starvation death uses - never a scoring kill.</summary>
        public const string StarvationKiller = "starvation";

        /// <summary>
        /// Subclass death behavior (manager removal / destroy / worm-splitting). Override
        /// THIS, not <see cref="Die"/> - the crystal drop is sealed into Die so the mass-
        /// conservation invariant cannot be bypassed by a subclass. Default is empty so
        /// managers and stubs don't need to throw NotImplementedException.
        /// </summary>
        protected virtual void OnDeath(string killerName = "") { }

        // Idempotency for predation: two predators can reach the same herbivore on the
        // same frame (each iterating its own OverlapSphere snapshot). Without this guard
        // the second Predated() re-enters Die() and double-removes / double-destroys.
        bool _consumedAsPrey;

        /// <summary>False once a predator has eaten this fauna - predators skip already-eaten prey.</summary>
        public bool IsAlivePrey => !_consumedAsPrey;

        /// <summary>
        /// Where a devouring predator wants this prey's body prisms suctioned to (the
        /// predator's mouth). Set by the devour overload of <see cref="Predated"/> before
        /// Die runs, so subclass OnDeath can choose the break-apart-and-suction exit over
        /// the default wither. Null for non-predation deaths (starvation).
        /// </summary>
        protected Transform DevourTarget { get; private set; }

        /// <summary>
        /// A predator has caught this fauna. Routes through the normal <see cref="Die"/> path
        /// (manager removal / destroy), is idempotent, and respects the post-spawn predation
        /// immunity window. Returns true only if the prey was actually eaten this call - the
        /// predator should reset its starvation clock (NotifyFed) only on a true result.
        /// </summary>
        public bool Predated(string predatorName = "predator") => Predated(predatorName, null);

        /// <summary>
        /// Devour variant: <paramref name="devourTarget"/> is the predator's mouth — the
        /// suction sink the prey's body prisms implode toward (continuity rule: the prey
        /// breaks apart and is pulled into the mouth, never popping out of existence). The
        /// sealed <see cref="Die"/> still drops the elemental crystal first (mass conserved).
        /// </summary>
        public virtual bool Predated(string predatorName, Transform devourTarget)
        {
            // Who eats whom is the server's call; a puppet predator never reaches here either
            // (its hunting tick is gated), so this is the belt to that braces.
            if (!IsSimAuthority) return false;

            // A SILENCED pilot harms no creature (VesselSilence): a joust, a ripple or a black hole
            // that names a silenced pilot as the killer lets the creature live. Creature predators
            // are never silenced - only pilots are - so the food web is untouched.
            if (VesselSilence.IsSilenced(predatorName)) return false;

            // ALREADY DEAD - a corpse is not prey. Die() is sealed but NOT idempotent: it
            // re-raises ReportKill (a second LifeformsKilled credit for one creature) and,
            // because Jousted has by then stamped the style, it takes the ReleaseHeart branch
            // and frees the heart while the wither is still eating inward - the exact ordering
            // Docs/ECOSYSTEM.md §26 forbids ("the heart is the LAST thing standing").
            //
            // _consumedAsPrey was never the right gate for this: only Predated sets it, so a
            // creature that starved or lost its last body prism arrives here with it false. The
            // corpse window is REAL and seconds long - a deferred wither leaves the heart
            // embedded for the whole animation - and it is exactly where a big AOE sweep looks.
            // LifeForm.Jousted has always carried the equivalent guard (dying || isCleaningUp);
            // Fauna is a SIBLING of LifeForm rather than a subclass, so it never inherited it,
            // and Fauna.Jousted's own doc already promises the behaviour this restores
            // ("a creature already dying ... leaves the style alone").
            if (_diedThisLife) return false;

            if (_consumedAsPrey || IsPredationImmune) return false;
            _consumedAsPrey = true;
            DevourTarget = devourTarget;
            // A mouth to suction into IS the consumed style: the body transfers to the
            // eater, so there is no skeleton and no wither ordering to pick.
            if (devourTarget) _deathStyle = LifeformDeathStyle.Consumed;
            // the eater takes the stomach and the body (conserved; the upkeep burned until now goes to the soil)
            float burned = 0f;
            float meal = _stomachLive ? _stomach.SurrenderAll(Time.time, out burned) : 0f;
            PayToSoil(burned);
            _surrenderedMeal = meal + BodyMealVolume;
            Die(predatorName);
            return true;
        }

        public void SetTeam(Domains domain)
        {
            if (!AcceptsTeamRecolour) return;
            this.domain = domain;
            OnTeamChanged();
        }

        /// <summary>
        /// Whether a live <see cref="SetTeam"/> recolours this creature's whole body, so it can be STOLEN
        /// (<see cref="StealInto"/>). False unless the species re-reads its colour in <see cref="OnTeamChanged"/>.
        /// </summary>
        public virtual bool CanBeStolen => false;

        /// <summary>
        /// STEAL this creature into <paramref name="thief"/>'s domain (Garrett, 2026-10-24: "if an opposing team's fauna is
        /// netted it steals it"; the third sign-off on the one-colour law, <c>Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md</c>):
        /// its domain, its body prisms and its diet's side become the thief's, and its offspring are born the thief's
        /// (birth copies the parent's domain). Nothing is spawned or killed. On the peer that simulates it; a puppet takes
        /// it from <see cref="FaunaNetworkSync"/>. False when it cannot be stolen, is dying, or is already the thief's.
        /// </summary>
        public bool StealInto(Domains thief)
        {
            if (!CanBeStolen || !AcceptsTeamRecolour || !IsSimAuthority || _diedThisLife || domain == thief) return false;
            ApplyStolenDomain(thief);
            var sync = NetSync;
            if (sync) sync.PublishStolenDomain(thief);
            return true;
        }

        /// <summary>The stolen domain, applied (the simulating peer's <see cref="StealInto"/>, and a puppet's replica).</summary>
        internal void ApplyStolenDomain(Domains thief)
        {
            if (!CanBeStolen || !AcceptsTeamRecolour || domain == thief) return;
            SetTeam(thief);
        }

        /// <summary>
        /// SHIELD this creature's body (Garrett, 2026-10-24: a creature of the player's domain "goes through the net shielding
        /// ... its prisms"): every live plain body prism takes a shield; danger prisms stay danger (danger and shield are
        /// exclusive by locked design) and a shielded one is left as it is. On the simulating peer; the puppets shield theirs
        /// through <see cref="FaunaNetworkSync"/>. False when nothing was shielded.
        /// </summary>
        public bool ShieldBody()
        {
            if (!IsSimAuthority || _diedThisLife) return false;
            if (!ApplyShieldBody()) return false;
            var sync = NetSync;
            if (sync) sync.PublishShieldBody();
            return true;
        }

        internal bool ApplyShieldBody()
        {
            var body = _bodyPrisms;
            if (body == null) return false;
            bool any = false;
            for (int i = 0; i < body.Length; i++)
            {
                var hp = body[i];
                if (!hp || hp.destroyed || hp.prismProperties is not { IsDangerous: false, IsShielded: false, IsSuperShielded: false })
                    continue;
                hp.ActivateShield();
                any = true;
            }
            return any;
        }

        /// <summary>False for a creature whose colours are its own history rather than its cell's controller
        /// (a MultiDomain swarm, Docs/SWARM_FAUNA.md §16.4): <see cref="Cell.SetModeControlOverride"/>'s
        /// one-colour re-colour does not apply to it.</summary>
        protected virtual bool AcceptsTeamRecolour => true;

        /// <summary>After <see cref="SetTeam"/> changed <see cref="domain"/> - a subclass that draws or caches
        /// its colour re-reads it here.</summary>
        protected virtual void OnTeamChanged() { }

        IEnumerator UpdateGoalCoroutine()
        {
            while (true)
            {
                yield return CosmicShore.Utility.WaitCache.Seconds(GetAggressionScaledGoalInterval());
                if (cell == null) continue;
                Goal = ResolveGoal();
            }
        }

        /// <summary>
        /// Targeting strategy by aggression level (per user spec):
        ///   Level0 - head toward the cell's crystal
        ///   Level1 - head toward the nearest opposing-color centroid
        ///   Level2 - head toward the nearest centroid of ANY color
        ///
        /// A per-instance orbit offset is added at Levels 0 and 1 so the pack spreads
        /// around the target. Level 2 skips the offset - at berserk aggression we
        /// want tight convergence onto the densest cleanup target.
        /// </summary>
        protected virtual Vector3 ResolveGoal()
        {
            if (cell == null) return Goal;

            switch (cell.AggressionLevel)
            {
                case CellAggressionLevel.Level2:
                    return cell.GetDensestRegionAnyDomain();

                case CellAggressionLevel.Level1:
                    return cell.GetExplosionTarget(domain) + _goalOrbitOffset;

                case CellAggressionLevel.Level0:
                default:
                    // Voracious exterior: with a nucleus control zone, sensed mass
                    // outside the nucleus is prey at EVERY phase - hunt its densest
                    // region even at Calm instead of idling at the crystal. (The
                    // targeting grids only ever hold exterior mass in such cells.)
                    if (cell.HasNucleusControlZone && cell.HasSensedExteriorMass)
                        return cell.GetDensestRegionAnyDomain() + _goalOrbitOffset;

                    Vector3 anchor = cellData && cellData.CrystalTransform
                        ? cellData.CrystalTransform.position
                        : cell.transform.position;
                    return anchor + _goalOrbitOffset;
            }
        }

        float GetAggressionScaledGoalInterval()
        {
            float baseInterval = Mathf.Max(0.05f, goalUpdateInterval);
            if (cell == null || goalUpdateIntervalByAggression == null || goalUpdateIntervalByAggression.Length == 0)
                return baseInterval;

            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, goalUpdateIntervalByAggression.Length - 1);
            float mult = Mathf.Max(0.05f, goalUpdateIntervalByAggression[idx]);
            return baseInterval * mult;
        }
    }
}
