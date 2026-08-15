using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public enum BoidCollisionEffects
    {
        Attach = 0,
        Explode = 1,
    }

    public class Boid : Fauna
    {
        [Header("Detection Settings")]
        [SerializeField] float cohesionRadius = 10.0f;
        [SerializeField] float behaviorUpdateRate = 1.5f;
        [SerializeField] float separationRadius = 5f;
        [SerializeField] float trailBlockInteractionRadius = 10f;

        [Header("Behavior Weights")]
        [SerializeField] float separationWeight = 1.5f;
        [SerializeField] float alignmentWeight = 1.0f;
        [SerializeField] float cohesionWeight = 1.0f;
        [SerializeField] float goalWeight = 1.0f;

        [Header("Speed Settings")]
        [SerializeField] float minSpeed = 2.0f;
        [SerializeField] float maxSpeed = 5.0f;
        [Tooltip("Speed multiplier applied to a FORAGER while it is hunting mass (the cell has " +
                 "registered prisms to clear). Lets the swarm dash between concentrations and " +
                 "clear them quickly. Drops back to 1x when there's no mass (idling at the crystal).")]
        [SerializeField] float huntSpeedMultiplier = 10f;

        [Header("Goal Settings")]
        public Transform DefaultGoal;
        public Vector3 target = Vector3.zero;

        public float normalizedIndex;

        [Header("Mound Settings")]
        public Transform Mound;

        [SerializeField]
        Prism healthPrism;

        Vector3 currentVelocity;
        Vector3 desiredDirection;
        Quaternion desiredRotation;

        /// <summary>Live travel speed - the joust's "must be moving faster" comparison reads this.</summary>
        public override float CurrentSpeed => currentVelocity.magnitude;

        /// <summary>
        /// Layer the Boid flocking/feeding numbers of the config's variant expression on top of
        /// the base (scale / material / starvation / audio). Sentinel -1 keeps the prefab value;
        /// Forager is a tri-state. This is what lets ONE tadpole prefab express the authored
        /// Mass/Space/Time behavior differences as data (see FaunaVariantTuning).
        /// </summary>
        public override void ApplyVariantTuning(FaunaVariantTuning tuning)
        {
            base.ApplyVariantTuning(tuning);
            if (tuning == null) return;

            if (tuning.CohesionRadius >= 0f) cohesionRadius = tuning.CohesionRadius;
            if (tuning.BehaviorUpdateRate >= 0f) behaviorUpdateRate = tuning.BehaviorUpdateRate;
            if (tuning.TrailBlockInteractionRadius >= 0f) trailBlockInteractionRadius = tuning.TrailBlockInteractionRadius;
            if (tuning.GoalWeight >= 0f) goalWeight = tuning.GoalWeight;
            if (tuning.MinSpeed >= 0f) minSpeed = tuning.MinSpeed;
            if (tuning.MaxSpeed >= 0f) maxSpeed = Mathf.Max(tuning.MaxSpeed, minSpeed);
            if (tuning.Forager != FaunaVariantTuning.TriState.KeepPrefab)
                forager = tuning.Forager == FaunaVariantTuning.TriState.On;
        }

        public bool isKilled = false;
        bool isTraveling = false;
        bool isAttached = false;

        [SerializeField] List<BoidCollisionEffects> collisionEffects;

        [Header("Forager")]
        [Tooltip("ON = this boid is a food-web FORAGER (tadpole): it feeds when it grazes " +
                 "opposing mass (Explode effect) and starves (despawns) after starvationSeconds " +
                 "without feeding, so the swarm self-limits to available trail/flora prey. " +
                 "OFF (default) = drone/mound boid (BoidController) - never feeds or starves.")]
        [SerializeField] bool forager = false;

        [Header("Intentional Feeding (forager)")]
        [Tooltip("A forager must be facing its meal within this many degrees before the " +
                 "suction (Consume) starts — it turns toward the prism it is about to eat " +
                 "instead of grazing everything in radius. trailBlockInteractionRadius is " +
                 "the minimum approach distance (it never needs to touch the prisms).")]
        [SerializeField] float feedingFacingAngle = 30f;
        [Tooltip("Seconds the boid stays facing the spot it is consuming after the suction " +
                 "starts — match the suction shader's travel time (PrismImplosion, 2s) so " +
                 "it watches the prisms all the way in.")]
        [SerializeField] float consumeHoldSeconds = 2f;
        [Tooltip("One mouthful = the faced prism plus edible prisms within this radius of " +
                 "it — keeps swarm cleanup throughput while reading as deliberate bites.")]
        [SerializeField] float feedingClusterRadius = 10f;
        [Tooltip("Cap on prisms consumed per mouthful — bounds the implosion-VFX burst.")]
        [SerializeField] int maxClusterBites = 6;
        [Tooltip("Rotation-speed multiplier while feeding, so the slow boid turn can " +
                 "actually reach the facing angle before the swarm drifts it away.")]
        [SerializeField] float feedingTurnBoost = 4f;
        [Tooltip("How sharply the boid brakes to a hover while feeding (per-second " +
                 "exponential damping of velocity).")]
        [SerializeField] float feedingBrakeSharpness = 4f;

        // Intentional-feeding state: the behavior tick picks the nearest edible prism;
        // per-frame code turns to FACE it before the suction starts and holds facing
        // until the suction shader has pulled the mouthful in.
        Prism _feedTarget;
        Vector3 _feedFocusPoint;
        float _feedHoldUntil = -1f;

        [Header("Grazing Pacing (drone combat path)")]
        [Tooltip("Upper bound on prisms a DRONE boid (non-forager) damages per FRAME. The " +
                 "behavior tick still finds every edible prism in range, but the death cascade " +
                 "each hit triggers (implosion VFX, cell volume updates, flora reactions) drains " +
                 "at this rate over the following frames instead of landing in one. Pacing only - " +
                 "every queued prism is hit well inside one behavior tick. Foragers do NOT use " +
                 "this path: they feed through the intentional mouthful sequence above, which is " +
                 "already frame-bounded by maxClusterBites + the facing hold. 0 = unpaced legacy " +
                 "burst.")]
        [SerializeField] int maxConsumesPerFrame = 8;

        // Drone Damage cascade, drained at maxConsumesPerFrame. Entries are re-validated
        // at drain time (destroyed / shielded / fauna-body can all change inside the
        // pacing window). Foragers never enqueue here.
        readonly Queue<Prism> _pendingMeals = new();

        BoxCollider blockCollider;

        HealthPrism embeddedHealthPrism;

        // Attribution strings for Consume/Damage, cached once - the inline
        // concat was per-eaten-prism garbage during grazing bursts.
        string _consumerName;
        string _damagerName;

        public BoidManager BoidManager { get; set; }
        public BoidController BoidController { get; set; }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell); // record the explicit host cell (multi-cell correctness)

            embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
            if (!embeddedHealthPrism)
            {
                CSDebug.LogError($"{nameof(Boid)} on {name} has no embedded HealthPrism in children. Scaling cannot work.");
                return;
            }

            blockCollider = embeddedHealthPrism.GetComponent<BoxCollider>();
            if (!blockCollider)
                CSDebug.LogWarning($"{nameof(Boid)} on {name}: embedded HealthPrism has no BoxCollider.");

            // Initialize the body health prism(s) so they actually render and become a real
            // (consumable) health prism. Like LightFauna's body prisms, they start at local
            // scale 0 and only grow once Prism.Initialize fires the scale animator - without
            // this the tadpole has no visible/active health prism. (LightFauna does the same
            // for the brittlestar/shark body.) The cache also powers NotifyBodyPrismsMoved -
            // the per-frame spatial-index position sync for a moving body.
            var bodyPrisms = CacheBodyPrisms();
            for (int i = 0; i < bodyPrisms.Length; i++)
            {
                var hp = bodyPrisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("tadpole");
            }

            _consumerName = embeddedHealthPrism.PlayerName + " tadpole";
            _damagerName = embeddedHealthPrism.PlayerName + " boid";

            // Locked invariant: every lifeform carries one elemental crystal it drops as
            // a powerup on death (mass conserved). EnsureElementalCrystal uses the prefab's
            // authored crystal if present (validator-enforced fast path) or provisions one;
            // the sealed Fauna.Die drops it on any death path (predation / forager starvation).
            crystal = LifeFormCrystal.EnsureElementalCrystal(this);
            // The crystal is this creature's HEART while it lives: joustable by vessels
            // (Squirrel Space-5 withers via Predated) but never skim-collectable until
            // death drops it. Cleared by ActivateCrystal in the sealed Die path.
            if (crystal) crystal.SetEmbeddedIn(this);

            currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
            float initialDelay = normalizedIndex * behaviorUpdateRate;
            StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
        }

        /// <summary>
        /// Foragers (tadpoles) actively HUNT the biggest mass concentration the cell senses
        /// - the densest region of environment prisms across the whole density grid -
        /// regardless of aggression level, so they roam to the trail/flora buildup and clean
        /// it instead of sitting at the crystal/spawn. Emergent (reads the density grid),
        /// NOT track-following. `GetDensestRegionAnyDomain` falls back to the cell anchor
        /// (crystal) when the grid is empty, so an idle swarm gathers at the centre.
        /// Non-foragers (drones) keep the aggression-tiered base goal.
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (forager && cell != null)
            {
                Vector3 goal = cell.GetDensestRegionAnyDomain();
                // Centre focus (per-deployment, FaunaConfigurationSO.CenterFocusBias):
                // pull the forager's roaming goal toward the cell centre so the swarm
                // lingers on the central canopy. One lerp per goal update; 0 = off
                // (keep 0 for far-ranging deployments like the Skim Race cleanup swarm).
                float bias = SourceConfig ? SourceConfig.CenterFocusBias : 0f;
                return bias > 0f ? Vector3.Lerp(goal, cell.transform.position, bias) : goal;
            }
            return base.ResolveGoal();
        }

        IEnumerator CalculateBehaviorCoroutine(float initialDelay)
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            else
                // Never run the first behavior tick synchronously inside StartCoroutine:
                // reproduction spawns offspring from a parent that is mid-iteration over
                // the shared Fauna.OverlapScratch, and an immediate OverlapSphereNonAlloc
                // here would clobber the parent's snapshot.
                yield return null;

            while (true)
            {
                // Forager swarms (tadpoles) self-limit to available prey: a boid that hasn't
                // grazed in starvationSeconds despawns, so the swarm thins out when there's no
                // opposing mass left to eat (and its per-boid CPU cost drops with it). Gated
                // on `forager` so drone/mound boids (BoidController) never starve.
                if (forager && IsStarving)
                {
                    Die(StarvationKiller);
                    yield break;
                }

                if (!isAttached)
                {
                    target = Goal;      // Check it later
                }

                CalculateBehavior();
                // Jitter the cadence ±10% so boids spawned in the same burst drift
                // out of phase instead of ticking (and paying their consume
                // cascades) in the same frame forever - a phase-locked swarm lands
                // several full behavior ticks on one frame at a regular beat.
                yield return new WaitForSeconds(behaviorUpdateRate * Random.Range(0.9f, 1.1f));
            }
        }

        void CalculateBehavior()
        {
            if (isAttached)
            {
                Vector3 toTarget = target - transform.position;
                if (toTarget.sqrMagnitude > DegenerateSteeringSqr)
                    desiredDirection = toTarget.normalized;
                else if (desiredDirection.sqrMagnitude <= DegenerateSteeringSqr)
                    desiredDirection = transform.forward;
                currentVelocity = desiredDirection * Mathf.Clamp(currentVelocity.magnitude, minSpeed, maxSpeed);

                if (SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                    desiredRotation = rotation;
                else
                    desiredRotation = transform.rotation;

                return;
            }

            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            Vector3 goalDirection = target - transform.position;
            Vector3 blockAttraction = Vector3.zero;

            // Squared-distance space: every per-neighbor use of `distance` below is either a
            // radius threshold or the inverse-square weight diff.normalized/distance - and
            // diff.normalized/distance == (diff/|diff|)/|diff| == diff/diff.sqrMagnitude - so
            // no per-neighbor sqrt is needed. Radii are squared once per behavior tick here.
            float separationRadiusSqr = separationRadius * separationRadius;
            float trailBlockInteractionRadiusSqr = trailBlockInteractionRadius * trailBlockInteractionRadius;

            float averageSpeed = 0.0f;
            int separatedBoidCount = 0;

            // Intentional feeding: the tick SELECTS the nearest edible prism; the actual
            // face-then-suction sequence runs per-frame in UpdateFeeding.
            Prism feedCandidate = null;
            float bestFeedSqr = float.PositiveInfinity;

            // Everything this scan inspects is a registered prism — neighbor boids are
            // sensed through their body HealthPrisms, attraction/grazing targets ARE
            // prisms - so the whole neighborhood comes from the spatial index
            // (Fauna.PrismScratch snapshot): no physics broadphase, no per-collider
            // GetComponent, no 256-slot truncation in dense fields. Entries can be
            // consumed by our own side effects mid-loop, so each is re-checked.
            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int prismCount = spatialIndex != null && spatialIndex.IsAvailable
                ? spatialIndex.QuerySphere(transform.position, cohesionRadius, PrismScratch)
                : 0;

            for (int i = 0; i < prismCount; i++)
            {
                Prism otherPrism = PrismScratch[i];
                if (!otherPrism || otherPrism.destroyed) continue;

                // Ignore our own body prism (if present)
                if (blockCollider && otherPrism.gameObject == blockCollider.gameObject) continue;

                // Only HealthPrisms can be fauna bodies. Resolve the owner ONCE per
                // neighbor via the stamped HealthPrism.OwnerFauna (a field read; the
                // walk-and-backfill fallback covers species that never stamp) - the
                // old per-neighbor GetComponentInParent<Boid> + <Fauna> walks were
                // the bulk of this method's self-time in a swarm.
                Fauna ownerFauna = otherPrism is HealthPrism bodyPrism ? bodyPrism.ResolveOwnerFauna() : null;
                Boid otherBoid = ownerFauna as Boid;

                Vector3 diff = transform.position - otherPrism.transform.position;
                float sqr = diff.sqrMagnitude;
                if (sqr == 0f) continue;

                if (otherBoid)
                {
                    cohesion += -diff / sqr;
                    alignment += otherPrism.transform.forward;

                    if (sqr < separationRadiusSqr)
                    {
                        separatedBoidCount++;
                        separation += diff / sqr;
                        averageSpeed += currentVelocity.magnitude;
                    }
                }
                else
                {
                    blockAttraction += -diff / sqr;

                    // Drones eat OPPOSING-domain mass (combat). Foragers (tadpoles) are cleanup
                    // grazers: they eat prisms of ANY domain — so the dominant trail gets grazed
                    // too, not just the minority — but they must NOT eat shielded prisms
                    // (protected structure like the Skim Race track), other fauna's BODY prisms
                    // (herbivores eating fauna is the predator's job — this also keeps a
                    // predator's danger prisms untouchable by its prey), or nucleus-interior
                    // mass (the territorial claim). One rule, shared with the per-frame
                    // mouthful path and the drone EatPrism recheck: IsEdibleForForager.
                    bool edible = forager
                        ? IsEdibleForForager(otherPrism)
                        : embeddedHealthPrism && otherPrism.Domain != embeddedHealthPrism.Domain;

                    if (sqr < trailBlockInteractionRadiusSqr && embeddedHealthPrism && edible)
                    {
                        foreach (var effect in collisionEffects)
                        {
                            switch (effect)
                            {
                                case BoidCollisionEffects.Attach:
                                    if (!isTraveling)
                                    {
                                        if (!otherPrism.IsSmallest)
                                        {
                                            isAttached = true;
                                            target = otherPrism.transform.position;
                                            otherPrism.Grow(-1);
                                            embeddedHealthPrism.Grow(1);
                                            if (embeddedHealthPrism.IsLargest) StartCoroutine(AddToMoundCoroutine());
                                        }
                                        else if (DefaultGoal) target = DefaultGoal.position;
                                    }
                                    break;

                                case BoidCollisionEffects.Explode:
                                    if (embeddedHealthPrism)
                                    {
                                        if (forager)
                                        {
                                            // Foragers no longer graze inline: remember the
                                            // NEAREST edible prism; UpdateFeeding (per-frame)
                                            // turns to face it BEFORE the suction starts and
                                            // holds facing until it's pulled all the way in.
                                            if (sqr < bestFeedSqr)
                                            {
                                                bestFeedSqr = sqr;
                                                feedCandidate = otherPrism;
                                            }
                                        }
                                        // Drone combat: frame-paced Damage cascade (foragers
                                        // never reach here — they feed through mouthfuls).
                                        else if (maxConsumesPerFrame > 0)
                                            _pendingMeals.Enqueue(otherPrism);
                                        else
                                            EatPrism(otherPrism); // unpaced legacy burst
                                    }
                                    break;
                            }
                        }
                    }
                }
            }

            // Eat the first slice in the tick frame itself so single-prey grazing is
            // frame-identical to the old inline path; Update() drains the rest.
            DrainPendingMeals();

            int totalBoids = prismCount - 1;

            if (totalBoids > 0)
            {
                cohesion /= totalBoids;
                cohesion = (cohesion - transform.position).normalized;
            }

            // Forager intent: adopt the tick's nearest edible prism as the feed target
            // (unless mid-suction-hold). While feeding owns the body (hovering + facing
            // the meal), don't overwrite its velocity/rotation with the flock steering.
            if (forager)
            {
                if (!IsFeedingHold)
                    _feedTarget = feedCandidate;
                if (IsFeedingEngaged)
                    return;
            }

            averageSpeed = separatedBoidCount > 0 ? averageSpeed / separatedBoidCount : currentVelocity.magnitude;

            // Same permanent-stall guard as LightFauna: a steering sum that cancels to
            // ~zero normalizes to Vector3.zero, which zeroes currentVelocity - and a
            // motionless boid recomputes the identical zero from the identical position
            // every tick, so it never recovers. Hold the last heading instead.
            Vector3 steering = (separation * separationWeight)
                               + (alignment * alignmentWeight)
                               + (cohesion * cohesionWeight)
                               + (goalDirection * goalWeight)
                               + blockAttraction;
            if (steering.sqrMagnitude > DegenerateSteeringSqr)
                desiredDirection = steering.normalized;
            else if (desiredDirection.sqrMagnitude <= DegenerateSteeringSqr)
                desiredDirection = transform.forward;

            // Foragers DASH (huntSpeedMultiplier, e.g. 10x) toward a mass concentration so
            // the swarm covers the arena quickly. The dash is ARRIVAL-CAPPED: never faster
            // than "reach the goal around the next behavior tick". The old binary 10x dash
            // covered ~200+ units per 1.5s tick — far past the interaction radius — so a
            // boid overshot the goal between ticks, reversed at 10x, overshot again: a
            // rapid back-and-forth oscillation across a distant goal point that never
            // settled into feeding range. The cap decelerates the approach smoothly (one
            // sqrt per tick). 1x when within range, when the cell is empty, or not a forager.
            float speedMult = 1f;
            if (forager && cell != null && cell.LiveBlockCount > 0)
            {
                float distToGoal = Vector3.Distance(target, transform.position);
                if (distToGoal > trailBlockInteractionRadius)
                {
                    float arrivalMult = distToGoal / Mathf.Max(0.05f, maxSpeed * behaviorUpdateRate);
                    speedMult = Mathf.Clamp(arrivalMult, 1f, Mathf.Max(1f, huntSpeedMultiplier));
                }
            }
            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed * speedMult, maxSpeed * speedMult);

            desiredRotation = SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this) ? desiredRot : transform.rotation;
        }

        /// <summary>
        /// Consumes up to maxConsumesPerFrame queued meals. Called once from the
        /// behavior tick (first slice lands in the tick frame) and then from
        /// Update() until the queue empties - always well inside one behavior
        /// cycle, so pacing never reduces what the boid actually eats.
        /// </summary>
        void DrainPendingMeals()
        {
            int budget = maxConsumesPerFrame;
            while (budget-- > 0 && _pendingMeals.Count > 0)
                EatPrism(_pendingMeals.Dequeue());
        }

        /// <summary>
        /// The consume/damage half of the old inline Explode effect, with the
        /// scan's edibility predicate re-checked - inside the pacing window a
        /// flockmate may have eaten the prism (destroyed), a shield may have
        /// engaged, or its owner may have changed.
        /// </summary>
        void EatPrism(Prism prism)
        {
            if (!prism || prism.destroyed || !embeddedHealthPrism) return;

            if (forager)
            {
                bool isFaunaBody = prism is HealthPrism bodyPrism && bodyPrism.ResolveOwnerFauna() != null;
                if (IsShieldedMass(prism) || isFaunaBody) return;
                // Nucleus-interior mass is the territorial claim, never forager food -
                // same check as the scan, re-applied in case the nucleus radius
                // refreshed inside the pacing window.
                if (cell != null && cell.IsInsideNucleus(prism.transform.position)) return;

                // Foragers CONSUME (implode toward the tadpole → the suction
                // shader), matching how LightFauna grazes. devastate:false so a
                // shielded prism that somehow reaches here only loses its shield,
                // never gets eaten.
                prism.Consume(transform, embeddedHealthPrism.Domain, _consumerName, false, true);
                NotifyFed();
            }
            else
            {
                if (prism.Domain == embeddedHealthPrism.Domain) return;
                // The debris leaves at the creature's own speed. This used to be scaled by
                // embeddedHealthPrism.Volume to cancel Prism.Explode's divide, but that divide
                // is a no-op (SetupDestruction pins prismProperties.volume to 1 before it runs)
                // AND the factor was the volume of the BOID's health prism, not the victim's -
                // so it survived as a straight, unrelated multiplier on the debris speed.
                prism.Damage(currentVelocity, embeddedHealthPrism.Domain, _damagerName, true, true);
            }
        }

        protected override void OnDeath(string killerName = "")
        {
            if (isKilled) return;
            isKilled = true;
            // Uneaten queued prey stays in the world (mass conserved) - a dead
            // boid just stops being its eater.
            _pendingMeals.Clear();
            StopAllCoroutines();
            // Continuity rule — nothing pops out of existence. The sealed Fauna.Die already
            // dropped this boid's elemental crystal (mass conserved). A predation death with
            // a devour target breaks the body prisms off and suctions them into the
            // predator's mouth; every other death leaves the body prism standing as this
            // creature's skeleton (Docs/ECOSYSTEM.md §26) and only the husk fades out — so
            // the detach must happen BEFORE the fade, or the shrink would take the frame
            // with it.
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
            {
                if (DevourTarget)
                {
                    StartCoroutine(DevouredCoroutine(DevourTarget, killerName));
                }
                else
                {
                    currentVelocity = Vector3.zero; // wither in place, beside the frame it leaves
                    LeaveSkeleton();
                    StartCoroutine(FadeOutAndRemove());
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Predation exit: the body prism(s) break off and suction (implode) into the
        /// predator's mouth — the same suction shader, sinking to the mouth transform —
        /// then any residual structure fades out. Continuity rule: pulled into the mouth,
        /// never popped.
        /// </summary>
        IEnumerator DevouredCoroutine(Transform mouth, string predatorName)
        {
            if (string.IsNullOrEmpty(predatorName)) predatorName = "predator";
            currentVelocity = Vector3.zero; // break apart where it was caught

            var prisms = BodyPrisms;
            if (prisms != null)
            {
                for (int i = 0; i < prisms.Length; i++)
                {
                    var p = prisms[i];
                    if (p && !p.destroyed)
                        p.Consume(mouth ? mouth : transform, domain, predatorName, true, true);
                }
            }

            // The prisms ARE the visible body; fade any residual structure out.
            yield return FadeOutAndRemove();
        }

        IEnumerator FadeOutAndRemove()
        {
            Vector3 from = transform.localScale;
            float t = 0f;
            const float dur = 0.4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(from, Vector3.zero, t / dur);
                yield return null;
            }
            Destroy(gameObject);
        }

        IEnumerator AddToMoundCoroutine()
        {
            isAttached = false;
            isTraveling = true;

            if (Mound) target = Mound.position;

            float scanRadius = 30f;

            // Intentionally a physics query: the mound blocks this hunts for are built
            // by NewBlock WITHOUT Prism.Initialize, so they never register with the
            // spatial index - their colliders on the dedicated Mound layer are the
            // only way to find them (tiny population, narrow layer; physics is fine).
            Collider[] colliders = new Collider[0];
            while (colliders.Length == 0)
            {
                int layerIndex = LayerMask.NameToLayer("Mound");
                int layerMask = 1 << layerIndex;
                colliders = Physics.OverlapSphere(transform.position, scanRadius, layerMask);

                GyroidAssembler nakedEdge = null;
                foreach (var collider in colliders)
                {
                    nakedEdge = collider.GetComponent<GyroidAssembler>();
                    if (nakedEdge && !nakedEdge.IsFullyBonded() && nakedEdge.preferedBlocks.Count == 0 && (nakedEdge.IsBonded() || nakedEdge.isSeed))
                    {
                        (var newBlock1, var gyroidBlock1) = NewBlock();
                        nakedEdge.preferedBlocks.Enqueue(gyroidBlock1);
                        gyroidBlock1.Prism = newBlock1;

                        nakedEdge.Depth = 1;
                        nakedEdge.StartBonding();
                        break;
                    }
                }

                if (!nakedEdge) colliders = new Collider[0];
                yield return null;
            }

            isTraveling = false;

            if (!embeddedHealthPrism) yield break;
            embeddedHealthPrism.IsLargest = false;
            embeddedHealthPrism.DeactivateShields();
            embeddedHealthPrism.Grow(-3);
        }

        private (Prism, GyroidAssembler) NewBlock()
        {
            var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
            newBlock.ChangeTeam(domain);
            newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
            newBlock.prismProperties = new() { prism = newBlock };
            var gyroidBlock = newBlock.gameObject.AddComponent<GyroidAssembler>();
            return (newBlock, gyroidBlock);
        }

        // --- Intentional feeding (forager, per-frame) -------------------------

        /// <summary>True while holding position/facing until the current mouthful's suction completes.</summary>
        bool IsFeedingHold => Time.time < _feedHoldUntil;

        /// <summary>
        /// True while feeding owns movement: mid-suction-hold, or hovering inside the
        /// interaction radius of a live target while turning to face it.
        /// </summary>
        bool IsFeedingEngaged =>
            IsFeedingHold ||
            (_feedTarget && !_feedTarget.destroyed &&
             (_feedTarget.transform.position - transform.position).sqrMagnitude
                 <= trailBlockInteractionRadius * trailBlockInteractionRadius);

        /// <summary>
        /// Same forager edibility rule the inline graze used: unshielded prisms of ANY
        /// domain that are not our own body, not another fauna's body, and not inside
        /// the nucleus (the territorial claim is never eaten).
        /// </summary>
        bool IsEdibleForForager(Prism prism)
        {
            if (!prism || prism.destroyed) return false;
            if (blockCollider && prism.gameObject == blockCollider.gameObject) return false;
            if (IsShieldedMass(prism)) return false;
            if (prism is HealthPrism && prism.GetComponentInParent<Fauna>() != null) return false;
            if (cell == null) return true;
            // Band + cell pen: mass outside this creature's own annulus is unreachable, so it
            // is not food (Fauna.IsInsideBand). A forager's diet is otherwise any-domain, so
            // it does not go through IsPreyForMe's domain leg.
            if (!IsInsideBand(prism.transform.position)) return false;
            if (!cell.IsInsideFaunaContainment(prism.transform.position)) return false;
            return !cell.IsInsideNucleus(prism.transform.position);
        }

        /// <summary>
        /// Per-frame feeding: once the tick-selected target is inside the interaction
        /// radius (the minimum feeding distance), brake to a hover and TURN toward it;
        /// only when actually facing it does the suction start, and the boid holds
        /// facing until the suction shader has pulled the mouthful in. Returns true
        /// while feeding owns the body, so Update can boost the turn rate.
        /// </summary>
        bool UpdateFeeding()
        {
            if (IsFeedingHold)
            {
                FaceFeedFocus();
                BrakeToHover();
                return true;
            }

            var feedTarget = _feedTarget;
            if (!feedTarget || feedTarget.destroyed)
            {
                _feedTarget = null;
                return false;
            }

            Vector3 toTarget = feedTarget.transform.position - transform.position;
            if (toTarget.sqrMagnitude > trailBlockInteractionRadius * trailBlockInteractionRadius)
                return false; // still approaching — flock steering owns movement

            _feedFocusPoint = feedTarget.transform.position;
            FaceFeedFocus();
            BrakeToHover();
            if (Vector3.Angle(transform.forward, toTarget) <= feedingFacingAngle)
                ConsumeMouthful(feedTarget);
            return true;
        }

        /// <summary>
        /// One deliberate bite: suction the faced prism plus edible prisms clustered
        /// around it toward this boid (devastate:false — a shielded prism that somehow
        /// reaches here only loses its shield, never gets eaten), then hold facing for
        /// consumeHoldSeconds. One small index query per BITE, not per behavior tick.
        /// </summary>
        void ConsumeMouthful(Prism feedTarget)
        {
            _feedFocusPoint = feedTarget.transform.position;
            _feedHoldUntil = Time.time + consumeHoldSeconds;
            _feedTarget = null;

            Domains eaterDomain = embeddedHealthPrism ? embeddedHealthPrism.Domain : domain;
            string eaterName = _consumerName; // cached at Initialize (no per-mouthful concat garbage)

            int bites = 0;
            if (IsEdibleForForager(feedTarget))
            {
                feedTarget.Consume(transform, eaterDomain, eaterName, false, true);
                NotifyFed();
                bites++;
            }

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int found = spatialIndex != null && spatialIndex.IsAvailable && feedingClusterRadius > 0f
                ? spatialIndex.QuerySphere(_feedFocusPoint, feedingClusterRadius, FeedScratch)
                : 0;
            for (int i = 0; i < found && bites < maxClusterBites; i++)
            {
                var prism = FeedScratch[i];
                if (prism == feedTarget || !IsEdibleForForager(prism)) continue;
                prism.Consume(transform, eaterDomain, eaterName, false, true);
                NotifyFed();
                bites++;
            }

            // Someone else got the whole mouthful first — nothing to watch, resume roaming.
            if (bites == 0)
                _feedHoldUntil = 0f;
        }

        void FaceFeedFocus()
        {
            if (SafeLookRotation.TryGet(_feedFocusPoint - transform.position, out var rotation, this))
                desiredRotation = rotation;
        }

        void BrakeToHover()
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero,
                Mathf.Clamp01(Time.deltaTime * feedingBrakeSharpness));
        }

        void Update()
        {
            transform.position += currentVelocity * Time.deltaTime;
            // Movers contract: the body prism is registered mass - keep its stored
            // index position tracking the swimming boid.
            NotifyBodyPrismsMoved();

            // Forager: per-frame intentional-feeding driver (approach → face → mouthful →
            // hold), with a turn boost while it owns the body so the slow boid turn can
            // reach the facing angle. Drones skip it (they graze via the paced queue below).
            float turnRate = 1f;
            if (forager && !isKilled && UpdateFeeding())
                turnRate = Mathf.Max(1f, feedingTurnBoost);

            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime * turnRate);

            // Drone combat: drain the frame-paced Damage cascade (foragers never enqueue).
            if (!isKilled && _pendingMeals.Count > 0)
                DrainPendingMeals();
        }
    }
}
