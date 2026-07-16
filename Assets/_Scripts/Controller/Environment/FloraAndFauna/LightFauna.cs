using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lightweight boid-like creature with separation, cohesion, and goal-seeking behaviors.
    /// Consumes enemy health prisms within range.
    /// </summary>
    public class LightFauna : Fauna
    {
        const string PLAYER_NAME = "light FaunaPrefab";

        [Header("Data")]
        [SerializeField] private LightFaunaDataSO data;

        [Tooltip("Upper bound on prisms this fauna consumes per FRAME. The behavior tick still " +
                 "finds every edible prism in range, but the death cascade each consume triggers " +
                 "(pool release, VFX activation, spindle teardown, cell volume updates) drains at " +
                 "this rate over the following frames instead of landing in one. Pacing only — " +
                 "the meal plan is re-derived from the live scan every tick, so nothing is ever " +
                 "lost or duplicated and grazing throughput (the food web's population regulator) " +
                 "is unchanged; a dense cluster visibly melts instead of popping in a single " +
                 "frame. 0 or less = unpaced legacy burst.")]
        [SerializeField] int maxConsumesPerFrame = 8;

        // Edible prisms found by the behavior tick, drained at maxConsumesPerFrame.
        // REBUILT each tick (cleared before the scan): LightFauna can tick every
        // few frames at Frenzy cadence — unlike Boid's ~1.5s — so carrying the
        // queue across ticks would re-enqueue every still-live prism as a
        // duplicate, and a dead-dupe backlog would burn drain budget while fresh
        // meals starve behind it. Entries are also re-validated at drain time
        // (destroyed / domain-stolen / owner-died can all change inside the
        // pacing window). Same drain pattern as Boid._pendingMeals — see
        // Docs/ECOSYSTEM.md (consume pacing).
        readonly Queue<Prism> _pendingMeals = new();

        private Vector3 currentVelocity;
        private Vector3 desiredDirection;
        private Quaternion desiredRotation;

        // True once death has begun the wither animation. Freezes movement/behavior so the
        // husk withers in place instead of drifting, and makes the death path idempotent.
        bool _withering;

        [HideInInspector] public float Phase;

        public LightFaunaManager LightFaunaManager { get; set; }

        /// <summary>
        /// True when the host cell's phase is <see cref="CellPhase.Frenzy"/>: aggression-2
        /// fauna ignore danger-prism damage. Read by impactor pipelines that would
        /// otherwise debuff/damage the fauna on dangerous-prism contact. Centralizing
        /// the rule here keeps the impact code path from re-deriving phase semantics.
        /// </summary>
        public bool IsDangerImmune => cell && cell.Phase >= CellPhase.Frenzy;

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell); // record the explicit host cell (multi-cell correctness)

            if (!data)
            {
                CSDebug.LogError($"{nameof(LightFauna)} on {name} is missing {nameof(LightFaunaDataSO)}.");
                return;
            }

            // The fauna body is composed of nested HealthPrism prefab instances
            // (e.g. MassBrittlestarFauna embeds DynamicHealthBlock children). They
            // start at local scale 0 and only grow to their authored target scale
            // when Prism.Initialize fires the scale animator. LifeForm does this
            // automatically via BindEmbeddedParts, but LightFauna does NOT extend
            // LifeForm - without this step the brittlestar / shark renders as a
            // cluster of invisible prisms. Recolor to the fauna's domain first,
            // then kick off the growth animation. LifeForm is intentionally not
            // assigned so these body prisms don't register with Cell as
            // consumable targets. The cache also powers NotifyBodyPrismsMoved -
            // the per-frame spatial-index position sync for a moving body.
            var bodyPrisms = CacheBodyPrisms();
            for (int i = 0; i < bodyPrisms.Length; i++)
            {
                var hp = bodyPrisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
            }

            // Locked invariant: every lifeform carries one elemental crystal it drops as
            // a powerup on death (mass conserved). EnsureElementalCrystal uses the prefab's
            // authored crystal if present (validator-enforced fast path) or provisions one;
            // the sealed Fauna.Die drops it on any death path.
            crystal = LifeFormCrystal.EnsureElementalCrystal(this);
            // The crystal is this creature's HEART while it lives: joustable by vessels
            // (Squirrel Space-5 withers via Predated) but never skim-collectable until
            // death drops it. Cleared by ActivateCrystal in the sealed Die path.
            if (crystal) crystal.SetEmbeddedIn(this);

            float minSpeed = Mathf.Max(0f, data.minSpeed);
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

            currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
            StartCoroutine(UpdateBehaviorCoroutine());
        }

        /// <summary>
        /// Death = wither, never a pop. The sealed <see cref="Fauna.Die"/> has already dropped
        /// this creature's elemental crystal (mass conserved); here the body withers from its
        /// extremities inward so it FADES out of existence rather than vanishing - the
        /// platform-wide continuity rule. Only the husk is removed, after the body is gone.
        /// </summary>
        protected override void OnDeath(string killerName = "")
        {
            if (_withering) return;
            _withering = true;
            // Uneaten queued meals stay in the world (mass conserved) — a dead
            // fauna just stops being their eater.
            _pendingMeals.Clear();
            currentVelocity = Vector3.zero;

            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(WitherCoroutine());
            else
                RemoveHusk(); // can't animate while inactive (scene teardown) - remove directly
        }

        void RemoveHusk()
        {
            if (LightFaunaManager)
                LightFaunaManager.RemoveFauna(this);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// Collapses the body one spindle ring at a time, FARTHEST-from-centre first, so the
        /// creature visibly withers inward (a shark's fins / a brittlestar's arms evaporate
        /// before the core body - emergent from geometry, no per-prefab special-casing). Reuses
        /// the same <see cref="Spindle.ForceWither"/> evaporation flora use on death; a body
        /// with no spindle structure falls back to suctioning its prisms inward. Honors the
        /// continuity rule (nothing disappears instantly), then removes the spent husk.
        /// </summary>
        IEnumerator WitherCoroutine()
        {
            float interval = data && data.witherRingInterval > 0f ? data.witherRingInterval : 0.25f;

            var spindles = GetComponentsInChildren<Spindle>(true)
                .Where(s => s)
                .OrderByDescending(s => (s.transform.position - transform.position).sqrMagnitude)
                .ToList();

            if (spindles.Count > 0)
            {
                // Outer rings first: by the time an inner ring's turn comes its children are
                // already gone, so ForceWither just collapses that ring.
                for (int i = 0; i < spindles.Count; i++)
                {
                    if (spindles[i]) spindles[i].ForceWither();
                    if (interval > 0f) yield return new WaitForSeconds(interval);
                    else yield return null;
                }
            }
            else
            {
                // No spindle structure (e.g. a single-prism body) - suction the body prisms
                // inward toward the centre so the body still leaves continuously, not instantly.
                var prisms = GetComponentsInChildren<HealthPrism>(true)
                    .Where(p => p)
                    .OrderByDescending(p => (p.transform.position - transform.position).sqrMagnitude)
                    .ToList();
                for (int i = 0; i < prisms.Count; i++)
                {
                    if (prisms[i]) prisms[i].Consume(transform, domain, PLAYER_NAME, true, true);
                    if (interval > 0f) yield return new WaitForSeconds(interval);
                    else yield return null;
                }
            }

            RemoveHusk();
        }

        IEnumerator UpdateBehaviorCoroutine()
        {
            while (true)
            {
                if (!data)
                    yield break;

                float cadence = Mathf.Max(0.05f, data.behaviorUpdateRate + Phase) * GetAggressionCadenceMultiplier();
                yield return new WaitForSeconds(cadence);
                UpdateBehavior();
            }
        }

        // Cleanup urgency multipliers indexed by CellAggressionLevel (3 levels).
        // Level0 = baseline (world feels alive), Level1 = tighter, Level2 = berserk.
        static readonly float[] CadenceByAggression       = { 1f,   0.55f, 0.25f };
        static readonly float[] ConsumeRadiusByAggression = { 1f,   1.4f,  1.8f  };
        static readonly float[] SpeedByAggression         = { 1f,   1.25f, 1.6f  };

        float GetAggressionCadenceMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, CadenceByAggression.Length - 1);
            return CadenceByAggression[idx];
        }

        float GetAggressionConsumeRadiusMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, ConsumeRadiusByAggression.Length - 1);
            return ConsumeRadiusByAggression[idx];
        }

        float GetAggressionSpeedMultiplier()
        {
            if (cell == null) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, SpeedByAggression.Length - 1);
            return SpeedByAggression[idx];
        }

        bool IsBerserk => cell != null && cell.AggressionLevel == CellAggressionLevel.Level2;

        /// <summary>
        /// Nearest live, non-immune herbivore in the host cell's fauna registry.
        /// O(live fauna) per behavior tick - the registry is small (bounded by the
        /// per-species MaxLivePopulation caps).
        /// </summary>
        bool TryFindNearestPreyFauna(out Vector3 position)
        {
            position = default;
            var host = cell;
            if (host == null) return false;

            var fauna = host.LiveFauna;
            Fauna best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (!f || f == this) continue;
                if (f.Diet != FaunaDiet.Herbivore || !f.IsAlivePrey || f.IsPredationImmune) continue;

                float d = (f.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = f; }
            }

            if (!best) return false;
            position = best.transform.position;
            return true;
        }

        void UpdateBehavior()
        {
            if (!data || _withering)
                return;

            // Prey-linked population control: a fauna that hasn't fed in starvationSeconds
            // despawns, so the live population self-bounds to available prey (Docs/ECOSYSTEM.md §6).
            if (IsStarving)
            {
                Die("starvation");
                return;
            }

            Vector3 separation = Vector3.zero;

            // Phase-driven goal. Each phase swaps the goal source rather than killing/spawning
            // systems, so the same fauna instance can transition through aggression levels
            // as the cell's phase changes around it.
            //   Calm:     aggression 0 - head toward crystal
            //   Restless: aggression 1 - head toward nearest opposing-color centroid
            //   Frenzy:   aggression 2 - head toward nearest centroid (any domain)
            var phase = cell ? cell.Phase : CellPhase.Calm;
            Goal = phase switch
            {
                CellPhase.Restless => cell.GetExplosionTarget(domain),
                CellPhase.Frenzy => cell.GetDensestRegionAnyDomain(),
                _ => (cellData && cellData.CrystalTransform)
                       ? cellData.CrystalTransform.position
                       : (cell ? cell.transform.position : transform.position),
            };

            // Voracious exterior: with a nucleus control zone, mass outside the
            // nucleus is prey at EVERY phase - even a Calm herbivore hunts the
            // densest sensed exterior region instead of idling at the crystal
            // (the grids only hold exterior mass in such cells; aggression still
            // scales cadence/radius/speed).
            if (phase == CellPhase.Calm && cell != null &&
                cell.HasNucleusControlZone && cell.HasSensedExteriorMass)
                Goal = cell.GetDensestRegionAnyDomain();

            // Predators hunt PREY, not mass: seek the nearest live herbivore the cell
            // senses (Cell.LiveFauna - the fauna analogue of the prism density grid).
            // Replaces the v1 approximation where predators converged on prism-density
            // centroids and only met herbivores incidentally. Skips predation-immune
            // newborns so a shark doesn't camp a fresh birth; with no herbivores alive
            // the phase-based goal above stands (roam plausibly, then starve).
            if (diet == FaunaDiet.Predator && TryFindNearestPreyFauna(out var preyPos))
                Goal = preyPos;

            if (!IsFinite(Goal) || Goal.sqrMagnitude < 0.001f)
            {
                Goal = cellData && cellData.CrystalTransform ? cellData.CrystalTransform.position : cell.transform.position;
            }

            Vector3 goalDirection = (Goal - transform.position).normalized;

            int neighborCount = 0;
            float averageSpeed = 0f;

            float detectionRadius = Mathf.Max(0f, data.detectionRadius);
            float separationRadius = Mathf.Max(0f, data.separationRadius);
            float consumeRadius = Mathf.Max(0f, data.consumeRadius) * GetAggressionConsumeRadiusMultiplier();

            // Squared-distance space for the neighbor loops below: every `distance` use is a
            // radius threshold or the inverse-square weight diff.normalized/distance
            // (== diff/diff.sqrMagnitude), so no per-neighbor sqrt is needed. Both radii are
            // loop-invariant here, squared once.
            float separationRadiusSqr = separationRadius * separationRadius;
            float consumeRadiusSqr = consumeRadius * consumeRadius;

            // Aggression 2 drops friendly avoidance (same-domain ships, fauna, and
            // health prisms stop contributing to separation). Cross-domain entities
            // still push us away so we don't clip through enemy mass.
            bool dropFriendlyAvoidance = phase >= CellPhase.Frenzy;

            // --- Non-prism populations (vessels) via physics --------------------
            // Prism layers are masked out: that whole population - trail/flora
            // prisms AND other fauna's body HealthPrisms - comes from the spatial
            // index below, so the broadphase no longer wades through thousands of
            // prism colliders (which also used to truncate ships out of the
            // 256-slot scratch in dense fields).
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, OverlapScratch, NonPrismOverlapMask);

            for (int ci = 0; ci < hitCount; ci++)
            {
                var collider = OverlapScratch[ci];
                if (!collider || collider.gameObject == gameObject) continue;

                if (!collider.TryGetComponent(out IVesselStatus vessel)) continue;

                Vector3 diff = transform.position - collider.transform.position;
                float sqr = diff.sqrMagnitude;
                if (sqr <= 0f) continue;

                neighborCount++;
                // Level 2: skip separation from same-domain ships.
                if (!(dropFriendlyAvoidance && vessel.Domain == domain))
                    separation -= diff / sqr;
            }

            // --- Prism populations via the spatial index -------------------------
            // Snapshot of live prisms in range (Fauna.PrismScratch). Entries can be
            // consumed/predated by our own side effects mid-loop, so each is
            // re-checked - the same contract collider snapshots had.
            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int prismCount = spatialIndex != null && spatialIndex.IsAvailable
                ? spatialIndex.QuerySphere(transform.position, detectionRadius, PrismScratch)
                : 0;

            // Re-derive the meal plan from this tick's live scan. Clearing loses
            // nothing — anything still edible and in range is re-found below —
            // and carrying entries across ticks would duplicate them (see the
            // _pendingMeals comment).
            _pendingMeals.Clear();

            for (int pi = 0; pi < prismCount; pi++)
            {
                var prism = PrismScratch[pi];
                if (!prism || prism.destroyed) continue;

                Vector3 diff = transform.position - prism.transform.position;
                float sqr = diff.sqrMagnitude;
                if (sqr <= 0f) continue;

                // Predator diet: hunt herbivore fauna. Another creature's body shows up
                // here as its child HealthPrisms, so walk up to the owning Fauna (only
                // HealthPrisms can be fauna bodies - plain prisms skip the walk). We match
                // the Fauna BASE (not LightFauna) so a predator eats ANY herbivore species
                // - LightFauna (brittlestar) and Boid (tadpole) alike. The creature is the
                // nearest Fauna ancestor of its body prisms, so managers (also Fauna,
                // but with no body in the scene) are never returned. A predator's own body
                // resolves to `this` and is skipped; other predators (Diet != Herbivore)
                // are neighbors, not prey, so predators don't cannibalize. Predation
                // ignores domain - it is a diet relationship, not a team fight - so
                // predators always have prey even in a single-domain cell.
                if (diet == FaunaDiet.Predator && prism is HealthPrism preyBody)
                {
                    // Stamped owner (field read) instead of a GetComponentInParent walk
                    // per neighbor per tick; the walk-and-backfill fallback preserves
                    // the nearest-Fauna-ancestor semantics for unstamped species.
                    var prey = preyBody.ResolveOwnerFauna();
                    if (prey && prey != this && prey.Diet == FaunaDiet.Herbivore)
                    {
                        neighborCount++;
                        if (sqr < separationRadiusSqr)
                            separation += diff / sqr;

                        // Predated() respects the prey's post-spawn immunity window and
                        // returns false if the prey couldn't be eaten - only feed on a real kill.
                        if (prey.IsAlivePrey && sqr < consumeRadiusSqr && prey.Predated(PLAYER_NAME))
                            NotifyFed();
                        continue;
                    }
                    // Not prey (flora / another predator's body): predators don't eat
                    // prism mass, so fall through for separation only - the consume
                    // calls below are gated to Herbivore.
                }

                // Handle other fauna/health prisms
                if (prism is HealthPrism otherHealthBlock)
                {
                    neighborCount++;

                    bool sameDomain = otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain == domain;

                    if (sqr < separationRadiusSqr && !(dropFriendlyAvoidance && sameDomain))
                        separation += diff / sqr;

                    // Herbivores eat plant/trail mass; predators never eat prisms.
                    // The diet rule is spatialized through Cell.IsPreyForHerbivore:
                    // in a cell with a nucleus control zone, exterior mass is
                    // voraciously edible regardless of domain while nucleus-interior
                    // mass (the territorial claim) is never consumed; without a
                    // nucleus the legacy opposing-domain rule applies. (Fauna bodies
                    // never reach this branch - their body prisms carry no LifeForm.)
                    if (diet == FaunaDiet.Herbivore && sqr < consumeRadiusSqr && otherHealthBlock.LifeForm &&
                        (cell != null
                            ? cell.IsPreyForHerbivore(otherHealthBlock.transform.position, domain, otherHealthBlock.LifeForm.domain)
                            : otherHealthBlock.LifeForm.domain != domain))
                    {
                        if (maxConsumesPerFrame > 0)
                            _pendingMeals.Enqueue(otherHealthBlock);
                        else
                            EatPrism(otherHealthBlock); // unpaced legacy burst
                    }

                    continue;
                }

                // Handle blocks (trail prisms) - same spatialized diet rule as above.
                if (diet == FaunaDiet.Herbivore && sqr < consumeRadiusSqr &&
                    (cell != null
                        ? cell.IsPreyForHerbivore(prism.transform.position, domain, prism.Domain)
                        : prism.Domain != domain))
                {
                    if (maxConsumesPerFrame > 0)
                        _pendingMeals.Enqueue(prism);
                    else
                        EatPrism(prism); // unpaced legacy burst
                }
            }

            // Eat the first slice in the tick frame itself so sparse grazing is
            // frame-identical to the old inline path; Update() drains the rest.
            DrainPendingMeals();

            averageSpeed = neighborCount > 0
                ? (averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude)
                : currentVelocity.magnitude;

            float separationWeight = Mathf.Max(0f, data.separationWeight);
            float goalWeight = Mathf.Max(0f, data.goalWeight);

            desiredDirection = ((separation * separationWeight) + (goalDirection * goalWeight)).normalized;

            float speedMult = GetAggressionSpeedMultiplier();
            float minSpeed = Mathf.Max(0f, data.minSpeed) * speedMult;
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed * speedMult);

            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);

            if (currentVelocity != Vector3.zero && SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                desiredRotation = rotation;
            else
                desiredRotation = transform.rotation;
        }

        /// <summary>
        /// Consumes up to maxConsumesPerFrame queued meals. Called once from the
        /// behavior tick (first slice lands in the tick frame) and then from
        /// Update() until the queue empties or the next tick rebuilds it. Only
        /// ACTUAL consumes spend budget — entries invalidated inside the pacing
        /// window (eaten by a flockmate, domain stolen) are skipped for free, so
        /// stale entries can never throttle real grazing throughput.
        /// </summary>
        void DrainPendingMeals()
        {
            int budget = maxConsumesPerFrame;
            while (budget > 0 && _pendingMeals.Count > 0)
            {
                if (EatPrism(_pendingMeals.Dequeue()))
                    budget--;
            }
        }

        /// <summary>
        /// The consume half of the old inline scan, with the scan's edibility
        /// predicate re-checked — inside the pacing window a flockmate may have
        /// eaten the prism (destroyed), its domain may have been stolen to ours,
        /// or its owning lifeform may have died. Returns true only when a
        /// consume was actually issued.
        /// </summary>
        bool EatPrism(Prism prism)
        {
            if (_withering || !prism || prism.destroyed) return false;

            if (prism is HealthPrism healthBlock)
            {
                // Same spatialized predicate as the scan (Cell.IsPreyForHerbivore):
                // with a nucleus control zone, exterior mass is edible regardless of
                // domain; otherwise only opposing-domain lifeform mass is edible.
                if (!healthBlock.LifeForm) return false;
                if (!(cell != null
                        ? cell.IsPreyForHerbivore(healthBlock.transform.position, domain, healthBlock.LifeForm.domain)
                        : healthBlock.LifeForm.domain != domain)) return false;
                healthBlock.Consume(transform, domain, PLAYER_NAME, true, true);
                NotifyFed();
                return true;
            }

            if (!(cell != null
                    ? cell.IsPreyForHerbivore(prism.transform.position, domain, prism.Domain)
                    : prism.Domain != domain)) return false;
            prism.Consume(transform, domain, PLAYER_NAME, true, true);
            NotifyFed();
            return true;
        }

        void Update()
        {
            if (!_withering && _pendingMeals.Count > 0)
                DrainPendingMeals();

            transform.position += currentVelocity * Time.deltaTime;
            // Movers contract: the body prisms are registered mass - keep their
            // stored index positions tracking the swimming creature.
            NotifyBodyPrismsMoved();

            float lerpSpeed = data ? Mathf.Max(0f, data.rotationLerpSpeed) : 5f;
            var t = Mathf.Clamp(Time.deltaTime * lerpSpeed, 0f, 0.99f);

            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, t);
        }

        static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
