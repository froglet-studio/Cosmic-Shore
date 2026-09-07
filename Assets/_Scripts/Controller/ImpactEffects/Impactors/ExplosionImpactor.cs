using System.Collections.Generic;
using CosmicShore.Gameplay;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What one blast claimed, reported once as it retires. A struct rather than a widening
    /// parameter list so a future quantity (crystals converted, flora felled) is an added field
    /// instead of a signature change that silently reorders two ints at every call site.
    ///
    /// Presentation only. <see cref="ExplosionImpactor.OnBlastResolved"/> is a HUD channel.
    /// </summary>
    public readonly struct BlastTally
    {
        /// <summary>Distinct prisms the blast destroyed.</summary>
        public readonly int Prisms;
        /// <summary>Distinct VESSELS the blast landed on — pilots it debuffed.</summary>
        public readonly int Vessels;

        public BlastTally(int prisms, int vessels)
        {
            Prisms = prisms;
            Vessels = vessels;
        }
    }

    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        [SerializeField] private ExplosionImpactorDataContainerSO explosionImpactorDataContainer;

        [SerializeField] bool affectSelf;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating;
        [SerializeField] bool shielding;

        AOEExplosion explosion;

        public override Domains OwnDomain => explosion.Domain;

        /// <summary>
        /// The vessel that fired this blast, or null for an anonymous one (a detonation with no
        /// attributable shooter - see <see cref="AOEExplosion.AnonymousExplosion"/>, which is
        /// also why prism damage from those blasts is credited to "GuyFawkes" rather than a
        /// player). Exposed so a scoring effect can attribute a blast back to its pilot without
        /// reaching for the private explosion reference.
        /// </summary>
        public IVessel SourceVessel =>
            explosion == null || explosion.AnonymousExplosion ? null : explosion.Vessel;

        /// <summary>
        /// Per-instance friendly fire. False spares the blast's own domain (allied prisms are
        /// shielded rather than damaged, allied vessels are skipped entirely); true lets the blast
        /// hit everyone. Set by <see cref="AOEExplosion.InitializeStruct.AffectSelfOverride"/> so an
        /// elemental upgrade can hand a pilot a blast that no longer eats their own team's mass.
        /// Safe on the instance: every explosion is a fresh Instantiate of its prefab.
        /// </summary>
        public void SetAffectSelf(bool value) => affectSelf = value;

        /// <summary>Per-instance override of the authored `devastating` flag — a devastating
        /// blast destroys SHIELDED prisms outright instead of only shedding their shields. Mirrors
        /// <see cref="SetAffectSelf"/>; used by the Scarab's CHARGE-5 "Cavitation Shear".</summary>
        public void SetDevastating(bool value) => devastating = value;

        // Batch AOE processing - bypasses Physics for prisms entirely
        private bool _useBatchProcessing;
        private static int _trailBlockLayer = -1;
        private HashSet<int> _batchHitTracker;

        // Damage deferred by the per-frame budget. Entries here are already claimed
        // in _batchHitTracker, so the spatial query will never re-emit them - this
        // queue is their only resolution path. See MAX_NEW_HITS_PER_FRAME.
        private Queue<PendingExplosionHit> _batchPending;

        // Blast -> crystal dispatch state (see SweepCrystals). Static because only one blast
        // sweeps at a time on the main thread. _crystalLayerMask uses 0 for "not resolved yet"
        // and -1 for "resolved, and this project has no Crystals layer".
        //
        // The buffer is bounded, deliberately: a blast engulfing more than 16 crystals at once
        // reaches the rest on a later frame as it grows (_crystalsHit stops the ones already
        // handled from repeating), and no shipped cell places 16 crystals in one blast radius.
        private static readonly Collider[] s_crystalHits = new Collider[16];
        private int _crystalLayerMask;
        private HashSet<int> _crystalsHit;

        // Living lifeform HEARTS this blast has already acted on, the twin of _crystalsHit and
        // for the same reason: a blast grows over many frames and its sweep re-finds a creature
        // that is still standing inside it. Its own buffer, sized for a forest rather than for
        // the handful of omni crystals a cell places — a 95-unit warhead in a seeded cell can
        // legitimately contain dozens of hearts.
        // Growable, for the reason Projectile's fuze records: OverlapSphereNonAlloc fills in
        // unspecified order and silently drops the remainder, and every discriminating test here
        // (embedded, alive, fauna-not-flora, domain) runs AFTER the fill. Layer 9 is dominated by
        // hearts this sweep rejects - one always-on collider per FLORA plant, and a lattice
        // colony or a Hesperides garden stands hundreds inside a 95-unit warhead - so a fixed
        // buffer means the creatures the rocket visibly engulfed are arbitrarily not passed to
        // the effect. A full buffer is a POSSIBLY-TRUNCATED result, so grow and re-ask.
        private static Collider[] s_heartHits = new Collider[128];
        private const int HeartSweepBufferCap = 1024;
        private HashSet<int> _heartsHit;

        // Distinct VESSELS this blast has landed on, keyed by instance ID. Same once-per-blast
        // ledger shape as _crystalsHit and for the same reason: a blast grows over many frames and
        // its trigger re-reports a pilot who is still standing inside it, so a raw counter would
        // climb every frame a target loiters in the cone. Only vessels that passed the domain /
        // friendly-fire gate are recorded, so the count is "pilots this blast actually debuffed",
        // not "pilots it overlapped".
        private HashSet<int> _vesselsHit;

        public bool IsBatchProcessing => _useBatchProcessing;

        /// <summary>True while budget-deferred damage is still waiting to resolve.</summary>
        public bool HasPendingBatchWork => _batchPending != null && _batchPending.Count > 0;

        /// <summary>
        /// When true, BeginBatchProcessing() is a no-op - forces Physics OnTriggerEnter
        /// for all collisions. Used by AOEBenchmarkOverlay for A/B comparison.
        /// </summary>
        public static bool ForceLegacyPhysics { get; set; }

        // A/B switch owned by the benchmark overlay; must not survive into a normal session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForceLegacy() => ForceLegacyPhysics = false;

        // --- ProfilerMarkers ---
        private static readonly ProfilerMarker s_onTriggerEnter = new("AOE.OnTriggerEnter");
        private static readonly ProfilerMarker s_onTriggerSkipped = new("AOE.OnTriggerEnter.Skipped");
        private static readonly ProfilerMarker s_processBatch = new("AOE.ProcessBatchFrame");

        void Awake()
        {
            explosion ??= GetComponent<AOEExplosion>();
            if (_trailBlockLayer < 0)
                _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");
        }

        /// <summary>
        /// Begins batch AOE processing for this explosion's lifetime.
        /// Call once when the explosion starts. While active, prism collisions
        /// are skipped in OnTriggerEnter and handled by ProcessBatchFrame instead.
        /// </summary>
        public void BeginBatchProcessing()
        {
            if (ForceLegacyPhysics) return;

            // A blast that does not touch mass never starts the prism pass at all. ONE gate here
            // rather than one per explosion SHAPE: the spherical, conic and cylindrical blasts
            // each own their ExplodeAsync but all three begin batch processing through this
            // method, so a flag honoured here cannot be silently ignored by two of the three.
            // The prism layer is additionally excluded on such a blast's own trigger collider
            // (authored on the prefab), so the Physics fallback cannot reach a prism either.
            if (explosion != null && !explosion.AffectsPrisms) return;

            var registry = PrismSpatialIndex.Instance;
            if (registry == null || !registry.IsAvailable)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("[ExplosionImpactor] PrismSpatialIndex unavailable - falling back to Physics triggers");
#endif
                return;
            }
            _useBatchProcessing = true;
            // Reuse cached HashSet to avoid GC allocation per explosion
            if (_batchHitTracker == null)
                _batchHitTracker = new HashSet<int>(256);
            else
                _batchHitTracker.Clear();

            if (_batchPending == null)
                _batchPending = new Queue<PendingExplosionHit>(256);
            else
                _batchPending.Clear();

            _crystalsHit?.Clear();
            _heartsHit?.Clear();
            _vesselsHit?.Clear();

            if (explosion != null && explosion.Vessel != null)
                OnBlastBegan?.Invoke(explosion.Vessel);
        }

        /// <summary>
        /// Processes one frame of batch AOE damage via the PrismSpatialIndex.
        /// Called from AOEExplosion.ExplodeAsync each frame instead of relying on Physics.
        /// center/radius describe this frame's blast sphere (stationary centre,
        /// growing radius - so each frame's volume strictly contains the last);
        /// blastOrigin is the emission point all impact vectors radiate from.
        /// The conic explosion uses <see cref="ProcessBatchConeFrame"/> instead.
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchFrame(Vector3 center, float radius, Vector3 blastOrigin, in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                // Runs whether or not the batch path is available: crystals are not prisms and
                // are not in the spatial index, so nothing below would ever reach them. That is
                // also what makes a NON-PRISM blast (AffectsPrisms off, so batch never began)
                // still able to act on crystals and creatures.
                SweepCrystals(center, radius);
                SweepLifeformHearts(center, radius);

                if (!_useBatchProcessing) return true;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) return true;

                return registry.ProcessExplosionFrame(
                    center, radius, blastOrigin, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion,
                    explosion.Vessel,
                    _batchHitTracker,
                    _batchPending);
            }
        }

        /// <summary>
        /// Processes one frame of batch AOE damage for the CONIC explosion: an exact
        /// test against the swept blast over the axial slab [sliceMin, sliceMax]
        /// it newly covers this frame. Successive slabs tile the sweep exactly,
        /// so coverage does not depend on frame rate and never reaches past the
        /// visible tip.
        ///
        /// The cross-section is a CAPSULE (a stadium): a disc of radius
        /// tanCoreHalfAngle*s dragged along <paramref name="gapeAxis"/> for
        /// +/- tanGapePerUnit*s. Both tangents are invariant as the self-similar
        /// blast grows; tanGapePerUnit = 0 is the plain circular cone.
        /// Returns true if the explosion should continue, false if it should be
        /// destroyed (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchConeFrame(
            Vector3 apex, Vector3 axis, Vector3 gapeAxis, float sliceMin, float sliceMax,
            float tanCoreHalfAngle, float tanGapePerUnit, in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                // Bounding sphere of the cone slab this frame covers. A crystal conversion is a
                // discrete, once-per-blast event, so the sphere's slight over-reach at the cone's
                // flanks is not worth an exact test.
                float coneReach = Mathf.Max(sliceMax, 0f);
                float coneSweepRadius = coneReach * (0.5f + Mathf.Max(tanCoreHalfAngle, tanGapePerUnit));
                Vector3 coneSweepCentre = apex + axis * (coneReach * 0.5f);
                SweepCrystals(coneSweepCentre, coneSweepRadius);
                SweepLifeformHearts(coneSweepCentre, coneSweepRadius);

                if (!_useBatchProcessing) return true;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) return true;

                return registry.ProcessExplosionConeFrame(
                    apex, axis, gapeAxis, sliceMin, sliceMax,
                    tanCoreHalfAngle, tanGapePerUnit, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion,
                    explosion.Vessel,
                    _batchHitTracker,
                    _batchPending);
            }
        }

        /// <summary>
        /// Processes one frame of batch AOE damage for the CYLINDRICAL explosion: an exact test
        /// against the swept plate over the axial slab [sliceMin, sliceMax] it newly covers this
        /// frame. Successive slabs tile the sweep exactly, so coverage does not depend on frame
        /// rate and never reaches past the visible end cap.
        ///
        /// The cross-section is a DISC of constant <paramref name="radius"/>, and every prism the
        /// plate claims is shoved along <paramref name="axis"/> — the blast's own velocity —
        /// rather than radially from an origin.
        /// Returns true if the explosion should continue, false if it should be destroyed
        /// (e.g. hit a super-shielded enemy prism).
        /// </summary>
        public bool ProcessBatchCylinderFrame(
            Vector3 origin, Vector3 axis, float sliceMin, float sliceMax, float radius,
            in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                // Bounding sphere of the swept-so-far cylinder (centre at its axial midpoint,
                // radius the half-diagonal) as the BROADPHASE, plus the exact cylinder as the
                // narrowphase. The cone gets away with a bare bounding sphere; a squat cylinder
                // does not. At the Scarab's 45-wide, 54-long plate the sphere reaches ~43 units
                // BEHIND the pilot on the first frame, and a crystal conversion is not a soft
                // outcome — it SPENDS the crystal and forges a ball — so the blast would have
                // built a ball out of mass it visibly missed while its prism half, running the
                // exact slab, agreed it had touched nothing there.
                float depth = Mathf.Max(sliceMax, 0f);
                float half = depth * 0.5f;
                float cylinderSweepRadius = Mathf.Sqrt(half * half + radius * radius);
                Vector3 cylinderSweepCentre = origin + axis * half;
                var cylinderNarrowphase = new SweptCylinder(origin, axis, depth, radius);
                SweepCrystals(cylinderSweepCentre, cylinderSweepRadius, cylinderNarrowphase);
                SweepLifeformHearts(cylinderSweepCentre, cylinderSweepRadius, cylinderNarrowphase);

                if (!_useBatchProcessing) return true;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) return true;

                return registry.ProcessExplosionCylinderFrame(
                    origin, axis, sliceMin, sliceMax, radius, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion,
                    explosion.Vessel,
                    _batchHitTracker,
                    _batchPending);
            }
        }

        /// <summary>
        /// Drains one frame's worth of budget-deferred damage without running a new
        /// spatial query. Called after the explosion's visual finishes so a blast
        /// dense enough to exceed the per-frame budget still damages everything it
        /// enclosed. Returns true while work remains.
        /// </summary>
        public bool DrainPendingBatchFrame(in ExplosionImpulse impulse)
        {
            using (s_processBatch.Auto())
            {
                if (!HasPendingBatchWork) return false;
                var registry = PrismSpatialIndex.Instance;
                if (registry == null) { _batchPending.Clear(); return false; }

                return registry.DrainPendingExplosionDamage(
                    _batchPending, impulse,
                    explosion.Domain,
                    affectSelf, destructive, devastating, shielding,
                    explosion.AnonymousExplosion, explosion.Vessel);
            }
        }

        /// <summary>
        /// How many distinct prisms this blast has claimed so far. The batch tracker already keys
        /// every prism the blast reached, so this is free — it is the blast's own footprint, not a
        /// second count kept alongside it.
        /// </summary>
        public int BatchHitCount => _batchHitTracker?.Count ?? 0;

        /// <summary>How many distinct vessels this blast has landed on. See <see cref="_vesselsHit"/>.</summary>
        public int VesselHitCount => _vesselsHit?.Count ?? 0;

        /// <summary>
        /// Raised once per blast as it BEGINS, with the vessel that fired it. Exists so a listener
        /// can zero window counters it keeps for effects the blast causes but does not itself
        /// dispatch — the Dolphin's HUD counts fauna kills this way, because a creature dies when
        /// its last body prism is destroyed and that death is raised by the ECOLOGY
        /// (<c>CellRuntimeDataSO.OnFaunaKilled</c>), several steps downstream of the prism damage.
        /// Presentation only, exactly like <see cref="OnBlastResolved"/>.
        /// </summary>
        public static event System.Action<IVessel> OnBlastBegan;

        /// <summary>
        /// Raised once per blast as it retires, with the vessel that fired it and what it claimed.
        /// Presentation only (a HUD tally) — listeners must not change outcomes. Static because
        /// explosions are spawned and destroyed per shot, so there is nothing durable for a HUD to
        /// subscribe to; listeners filter by the vessel they own.
        /// </summary>
        public static event System.Action<IVessel, BlastTally> OnBlastResolved;

        /// <summary>
        /// Ends batch processing and cleans up tracking data.
        /// </summary>
        public void EndBatchProcessing()
        {
            if (_useBatchProcessing && explosion != null && explosion.Vessel != null)
                OnBlastResolved?.Invoke(explosion.Vessel,
                    new BlastTally(BatchHitCount, VesselHitCount));

            _useBatchProcessing = false;
            // Keep HashSet/Queue allocated for reuse - cleared on next BeginBatchProcessing.
            // Any hits still pending here are abandoned deliberately: EndBatchProcessing
            // runs on cancellation (turn end) and on the destroy paths, where further
            // damage must not land.
            _batchPending?.Clear();
        }

        protected override void OnTriggerEnter(Collider other)
        {
            using (s_onTriggerEnter.Auto())
            {
                // Skip prisms entirely - they're handled by batch AOE processing
                if (_useBatchProcessing && other.gameObject.layer == _trailBlockLayer)
                {
                    s_onTriggerSkipped.Begin();
                    s_onTriggerSkipped.End();
                    return;
                }

                // The Astro League ball is shoved by a blast the same way prism mass is - see
                // AstroLeagueBall.ApplyBlastServer. It is recognised HERE rather than through an
                // ImpactCollider + impactor pair because the response is one fixed physics
                // impulse with no authored variation (the same reason ExecuteCommonPrismCommands
                // is hard-coded alongside the effect list), and because the growing trigger fires
                // exactly once per blast per ball, which is the semantics we want for free. This
                // lookup only runs on the non-prism trigger path, which for an explosion means
                // vessels, crystals and mines - a handful of contacts per blast.
                if (other.TryGetComponent(out AstroLeagueBall ball))
                {
                    AstroLeagueBall.RequestBlast(ball, SourceVessel, transform.position,
                                                 explosion.CalculateImpactVector(ball.transform.position),
                                                 explosion.Domain);
                    return;
                }

                base.OnTriggerEnter(other);
            }
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {    
            var impactVector = explosion.CalculateImpactVector(impactee.Transform.position);
            
            switch (impactee)
            {
                case VesselImpactor vesselImpactee:
                    if (vesselImpactee.Vessel.VesselStatus.Domain == explosion.Domain && !affectSelf)
                        break;

                    // Recorded BEFORE the effect container is consulted: a pilot who passed the
                    // friendly-fire gate has been caught by this blast whether or not the firing
                    // vessel happens to author any vessel effects, and the tally is a report of the
                    // blast's reach, not of one container's wiring.
                    _vesselsHit ??= new HashSet<int>(4);
                    _vesselsHit.Add(vesselImpactee.Vessel.Transform.GetInstanceID());

                    if (!explosionImpactorDataContainer) return;
                    var vesselExplosionEffects = explosionImpactorDataContainer.vesselExplosionEffects;
                    if(!DoesEffectExist(vesselExplosionEffects)) return;
                    foreach (var effect in vesselExplosionEffects)
                    {
                        effect.Execute(vesselImpactee, this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    // A blast that does not touch mass declines here TOO, not only in
                    // BeginBatchProcessing. The flag has to hold on BOTH paths or it holds on
                    // neither in practice: the batch early-return leaves _useBatchProcessing
                    // false, which is exactly the state in which OnTriggerEnter does NOT skip the
                    // prism layer - so a prism trigger would fall straight through to
                    // ExecuteCommonPrismCommands, whose first branch on a non-destructive blast
                    // ARMOURS the prism (ActivateShield) instead of ignoring it. The prefab's
                    // authored layer exclusion is a second line, not the only one; an explosion
                    // authored tomorrow without it would otherwise shield half an arena.
                    if (explosion != null && !explosion.AffectsPrisms) return;
                    ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                    if (!explosionImpactorDataContainer) return;
                    var explosionPrismEffects = explosionImpactorDataContainer.explosionPrismEffects;
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    foreach (var effect in explosionPrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;

                // NOTE: there is deliberately NO `case OmniCrystalImpactor` here. Crystals sit on
                // layer 9 and explosions on layer 10, and that pair is DISABLED in the project's
                // collision matrix — a trigger case for crystals would compile, read correctly,
                // and never once fire. Blast↔crystal is dispatched explicitly by SweepCrystals
                // instead. Do not "tidy" it back into this switch.
            }
        }

        /// <summary>
        /// This blast's impulse — the (speed × inertia) magnitude and ceiling it hands the mass it
        /// destroys. Exposed so a crystal effect can size its output off the same number the prism
        /// debris rides, instead of authoring a second one that drifts from it.
        /// </summary>
        public ExplosionImpulse BlastImpulse => explosion != null ? explosion.Impulse : default;

        /// <summary>
        /// Blast → CRYSTAL, dispatched by an explicit overlap rather than through the trigger.
        /// Layer 9 (Crystals) × layer 10 (Explosions) is off in the collision matrix, so no
        /// trigger pair is ever generated for this contact; the alternative to querying here was
        /// turning that pair on project-wide, which would mint trigger pairs between EVERY blast
        /// and EVERY crystal in every mode to serve one weapon.
        ///
        /// The query is skipped entirely unless this blast AUTHORS crystal effects — which today
        /// is only the Scarab's cavitation punch, so the other twelve AOE prefabs pay one array
        /// null-check per frame and nothing else. `_crystalsHit` is the once-per-blast ledger, the
        /// same shape as the prism batch tracker: a blast grows over many frames and would
        /// otherwise re-convert the same crystal on each of them.
        /// </summary>
        /// <summary>
        /// Exact swept-CYLINDER narrowphase for the crystal sweep, when the caller's blast is one.
        /// A sphere cannot bound a squat cylinder tightly — for the Scarab's 45-wide, 54-long plate
        /// the bounding sphere reaches ~43 units BEHIND the pilot on the first frame — so without
        /// this the two halves of one blast disagree about what it contained: prisms come off the
        /// exact slab while crystals came off the broadphase, and a crystal plainly astern got
        /// consumed and forged into a ball. <see cref="Radius"/> 0 means "no narrowphase", which is
        /// what the spherical and conic blasts pass.
        /// </summary>
        private readonly struct SweptCylinder
        {
            public readonly Vector3 Origin;
            public readonly Vector3 Axis;
            public readonly float Depth;
            public readonly float Radius;

            public SweptCylinder(Vector3 origin, Vector3 axis, float depth, float radius)
            {
                Origin = origin; Axis = axis; Depth = depth; Radius = radius;
            }

            public bool IsValid => Radius > 0f;

            /// <summary>The same predicate <c>AOECylinderSweepQueryJob.Execute</c> runs on prisms,
            /// over the whole swept-so-far volume rather than one frame's slab (the caller dedupes,
            /// so a crystal the blast has ever contained resolves exactly once).</summary>
            public bool Contains(Vector3 point)
            {
                Vector3 rel = point - Origin;
                float s = Vector3.Dot(rel, Axis);
                if (s < 0f || s > Depth) return false;
                return Vector3.ProjectOnPlane(rel, Axis).sqrMagnitude <= Radius * Radius;
            }
        }

        void SweepCrystals(Vector3 centre, float radius) =>
            SweepCrystals(centre, radius, default);

        void SweepCrystals(Vector3 centre, float radius, in SweptCylinder narrowphase)
        {
            if (!explosionImpactorDataContainer || radius <= 0f) return;
            var effects = explosionImpactorDataContainer.explosionCrystalEffects;
            if (!DoesEffectExist(effects)) return;

            int mask = ResolveCrystalLayerMask();
            if (mask <= 0) return;

            _crystalsHit ??= new HashSet<int>(8);

            int found = Physics.OverlapSphereNonAlloc(centre, radius, s_crystalHits,
                                                      mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < found; i++)
            {
                var col = s_crystalHits[i];
                if (col == null) continue;
                if (!col.TryGetComponent(out OmniCrystalImpactor crystal)) continue;
                // The sphere is only the broadphase when the caller supplied a real shape.
                if (narrowphase.IsValid && !narrowphase.Contains(col.transform.position)) continue;
                if (!crystal.CanBlastConsume(explosion.Domain)) continue;
                if (!_crystalsHit.Add(crystal.GetInstanceID())) continue;

                for (int e = 0; e < effects.Length; e++)
                {
                    if (IsEffectSlotEmpty(effects[e], explosionImpactorDataContainer,
                            nameof(ExplosionImpactorDataContainerSO.explosionCrystalEffects), e))
                        continue;
                    effects[e].Execute(this, crystal);
                }

                // Spend the crystal exactly as a collect does: it blooms out and respawns rather
                // than vanishing (continuity of existence), and cannot be forged twice.
                crystal.ConsumeByBlast(explosion.Domain, transform.position);
            }
        }
        
        /// <summary>
        /// The Crystals layer as a mask, resolved once. 0 means "not looked up yet" and -1 means
        /// "resolved, and this project has no Crystals layer" — the same sentinel pair both
        /// sweeps read, so they cannot disagree about where crystals live.
        /// </summary>
        int ResolveCrystalLayerMask()
        {
            if (_crystalLayerMask != 0) return _crystalLayerMask;
            int layer = LayerMask.NameToLayer("Crystals");
            _crystalLayerMask = layer >= 0 ? 1 << layer : -1;
            return _crystalLayerMask;
        }

        void SweepLifeformHearts(Vector3 centre, float radius) =>
            SweepLifeformHearts(centre, radius, default);

        /// <summary>
        /// Blast → a LIVING lifeform's HEART, dispatched by an explicit overlap for exactly the
        /// reason <see cref="SweepCrystals"/> is: layer 9 (Crystals) × layer 10 (Explosions) is
        /// off in the collision matrix, so no trigger pair is ever generated and a case in
        /// <see cref="AcceptImpactee"/> would compile, read correctly and never fire.
        ///
        /// Skipped entirely unless this blast AUTHORS lifeform-crystal effects — today only the
        /// Sparrow's missile warhead — so every other AOE prefab pays one array null-check per
        /// frame. <c>_heartsHit</c> is the once-per-blast ledger: a growing blast re-finds a
        /// creature that has not finished dying yet, and <c>Jousted</c> is idempotent but the
        /// effect list should not run twice for one creature.
        ///
        /// A heart stops matching on its own once the creature dies —
        /// <c>Crystal.ActivateCrystal</c> clears <c>EmbeddedIn</c> and it becomes an ordinary
        /// free pickup — so the sweep never re-kills what it already killed.
        /// </summary>
        void SweepLifeformHearts(Vector3 centre, float radius, in SweptCylinder narrowphase)
        {
            if (!explosionImpactorDataContainer || radius <= 0f) return;
            var effects = explosionImpactorDataContainer.explosionLifeformCrystalEffects;
            if (!DoesEffectExist(effects)) return;

            int mask = ResolveCrystalLayerMask();
            if (mask <= 0) return;

            _heartsHit ??= new HashSet<int>(16);

            int found;
            while (true)
            {
                found = Physics.OverlapSphereNonAlloc(centre, radius, s_heartHits,
                                                      mask, QueryTriggerInteraction.Collide);
                if (found < s_heartHits.Length || s_heartHits.Length >= HeartSweepBufferCap) break;
                s_heartHits = new Collider[s_heartHits.Length * 2];
            }
            for (int i = 0; i < found; i++)
            {
                var col = s_heartHits[i];
                if (col == null) continue;
                if (!col.TryGetComponent(out Crystal crystal)) continue;
                if (!crystal.IsEmbedded) continue;
                // EMBEDDED IS NOT ALIVE. A creature with a progressive wither re-homes its heart
                // onto the cell at the TOP of its death and deliberately leaves it embedded for
                // the whole animation (Docs/ECOSYSTEM.md §26), so a corpse's heart is swept for
                // seconds after it died. Jousting one re-runs the sealed death: a second kill
                // credit for one creature, and - because the joust stamps the style first - the
                // heart pops free while the wither is still eating inward. Fauna.Predated now
                // declines a corpse outright, so this is the belt to that brace; it also keeps
                // the sweep from spending effect dispatches on the dead.
                if (crystal.EmbeddedIn is { IsDying: true }) continue;
                // The sphere is only the broadphase when the caller supplied a real shape.
                if (narrowphase.IsValid && !narrowphase.Contains(col.transform.position)) continue;
                if (!_heartsHit.Add(crystal.GetInstanceID())) continue;

                for (int e = 0; e < effects.Length; e++)
                {
                    if (IsEffectSlotEmpty(effects[e], explosionImpactorDataContainer,
                            nameof(ExplosionImpactorDataContainerSO.explosionLifeformCrystalEffects), e))
                        continue;
                    effects[e].Execute(this, crystal);
                }
            }
        }

        /// <summary>
        /// The Physics-trigger fallback's per-prism resolution. Mirrors
        /// <c>PrismSpatialIndex.ResolveExplosionHit</c>, INCLUDING the debris ceiling:
        /// the batch path and this path must hand a prism the same impulse, or a blast
        /// throws mass at one speed with the spatial index up and another without it.
        /// </summary>
        void ExecuteCommonPrismCommands(Prism prism, Vector3 impactVector)
        {
            // Super-shielded prisms are fully invulnerable. The explosion is
            // physically blocked by the shield: destroy the explosion VFX so
            // it doesn't visibly expand through the prism, and skip all
            // damage / steal / shield-decay state changes. Mirrors the
            // PrismSpatialIndex Burst path — including its deflection stamp,
            // routed through the same shared gate, so the shield rocks on
            // both paths and this one cannot drift silently out of parity.
            // Ways to break super-shields will be added later as targeted
            // opt-in mechanics.
            if (prism.AbsorbSuperShieldHit(impactVector.magnitude))
            {
                Destroy(gameObject);
                return;
            }

            if ((prism.Domain == explosion.Domain && !affectSelf) || !destructive)
            {
                // The blast is ACCEPTED, not ignored: the prism armours up instead of the
                // explosion visibly passing through it. It carries this blow's magnitude
                // and ceiling so the pop that ends the shield sheds at half of it - see
                // PrismStateManager.ExecuteTimerDeactivation. Read off the impulse rather
                // than as impactVector.magnitude: CalculateImpactVector is Impulse.Along(a
                // unit direction), so the two are equal by construction and this spends no
                // root. It is also the SAME expression the Burst twin uses
                // (PrismSpatialIndex.ResolveExplosionHit) - these two must not drift, or a
                // blast pops shields differently with the spatial index up.
                float impactSpeed = explosion.Impulse.Speed * explosion.Impulse.Inertia;
                float limit = explosion.Impulse.DebrisSpeedLimit;
                if (shielding && prism.Domain == explosion.Domain)
                    prism.ActivateShieldFromImpact(impactSpeed, limit);
                else 
                    prism.ActivateShield(2f, impactSpeed, limit);
                return;
            }
            
            float debrisSpeedLimit = explosion.Impulse.DebrisSpeedLimit;

            if (explosion.AnonymousExplosion) // Vessel Status will be null here
                prism.Damage(impactVector, Domains.Blue, "🔥GuyFawkes🔥", devastating,
                             debrisSpeedLimit: debrisSpeedLimit);
            else
            {
                var shipStatus = explosion.Vessel.VesselStatus;
                prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name, devastating,
                             debrisSpeedLimit: debrisSpeedLimit);
            }
        }
    }
}