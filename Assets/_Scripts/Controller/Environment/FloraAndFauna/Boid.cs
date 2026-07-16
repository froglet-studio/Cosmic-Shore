using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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

        [Header("Grazing Pacing")]
        [Tooltip("Upper bound on prisms this boid consumes/damages per FRAME. The behavior tick " +
                 "still finds every edible prism in range, but the death cascade each consume " +
                 "triggers (implosion VFX, cell volume updates, flora reactions) drains at this " +
                 "rate over the following frames instead of landing in one. Pacing only - every " +
                 "queued prism is eaten well inside one behavior tick, so grazing throughput " +
                 "(the food web's population regulator) is unchanged; a dense cluster visibly " +
                 "melts instead of popping in a single frame. 0 or less = unpaced legacy burst.")]
        [SerializeField] int maxConsumesPerFrame = 8;

        // Edible prisms found by the behavior tick, drained at maxConsumesPerFrame.
        // Entries are re-validated at drain time (destroyed / shielded / fauna-body
        // can all change inside the pacing window).
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

            currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));

            if (IsSimAuthority)
            {
                float initialDelay = normalizedIndex * behaviorUpdateRate;
                StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
            }
            else if (forager && collisionEffects != null && collisionEffects.Contains(BoidCollisionEffects.Explode))
            {
                // Client puppet: the server owns motion + decisions; a replicated FORAGER
                // still grazes this peer's local prisms (trails/flora are client-local
                // objects - without this, nothing consumes them on clients and the
                // prism-count reduction foragers exist for lands on the host only).
                // Attach (mound drone) behavior is never puppet-run.
                StartCoroutine(PuppetGrazeCoroutine());
            }
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
                return cell.GetDensestRegionAnyDomain();
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
                    Die("starvation");
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
                desiredDirection = (target - transform.position).normalized;
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

            // Everything this scan inspects is a registered prism - neighbor boids are
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
                    // grazers: they eat prisms of ANY domain - so the dominant trail gets grazed
                    // too, not just the minority - but they must NOT eat:
                    //   - shielded prisms (protected structure like the Skim Race track), or
                    //   - other fauna's BODY prisms (brittlestar/shark bodies are HealthPrisms but
                    //     not Boids, so they reach this branch; herbivores eating fauna is the
                    //     predator's job, not a forager's). The resolved OwnerFauna catches any
                    //     fauna body; this prism's own boid was already excluded above.
                    var pp = otherPrism.prismProperties;
                    bool shielded = pp != null && (pp.IsShielded || pp.IsSuperShielded);
                    bool isFaunaBody = ownerFauna != null;
                    // Foragers additionally respect the nucleus control zone: mass
                    // inside the nucleus is the territorial claim (never eaten);
                    // everything outside stays any-domain edible (Cell.IsPreyForHerbivore).
                    bool edible = forager
                        ? (!shielded && !isFaunaBody &&
                           (cell == null || !cell.IsInsideNucleus(otherPrism.transform.position)))
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
                                        if (maxConsumesPerFrame > 0)
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

            averageSpeed = separatedBoidCount > 0 ? averageSpeed / separatedBoidCount : currentVelocity.magnitude;

            desiredDirection = ((separation * separationWeight)
                               + (alignment * alignmentWeight)
                               + (cohesion * cohesionWeight)
                               + (goalDirection * goalWeight)
                               + blockAttraction).normalized;

            // Foragers DASH (huntSpeedMultiplier, e.g. 10x) toward a mass concentration so
            // the swarm covers the arena quickly, then ease back to base speed once within
            // consume range so they graze it reliably instead of overshooting. 1x when the
            // cell is empty (idling at the crystal) or when not a forager.
            float speedMult = 1f;
            if (forager && cell != null && cell.LiveBlockCount > 0)
            {
                float distToGoalSqr = (target - transform.position).sqrMagnitude;
                speedMult = distToGoalSqr > trailBlockInteractionRadiusSqr ? Mathf.Max(1f, huntSpeedMultiplier) : 1f;
            }
            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed * speedMult, maxSpeed * speedMult);

            desiredRotation = SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this) ? desiredRot : transform.rotation;
        }

        /// <summary>
        /// Client-puppet slice of the behavior tick: the forager consume sweep ONLY.
        /// Same cadence (with the same anti-phase-lock jitter), but the query radius is
        /// the grazing radius rather than the full cohesion radius, and there is no
        /// flocking/goal/starvation math - a puppet costs strictly less than today's
        /// client-local sim. Enqueued prisms are re-validated by <see cref="EatPrism"/>
        /// (shielded / fauna-body / nucleus rules) exactly like the sim path, and drain
        /// through the same maxConsumesPerFrame pacing.
        /// </summary>
        IEnumerator PuppetGrazeCoroutine()
        {
            // Same first-tick deferral contract as CalculateBehaviorCoroutine: never
            // scan synchronously inside StartCoroutine (shared scratch safety).
            yield return null;

            while (true)
            {
                UpdatePuppetGraze();
                yield return new WaitForSeconds(behaviorUpdateRate * Random.Range(0.9f, 1.1f));
            }
        }

        void UpdatePuppetGraze()
        {
            if (isKilled || !embeddedHealthPrism)
                return;

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int prismCount = spatialIndex != null && spatialIndex.IsAvailable
                ? spatialIndex.QuerySphere(transform.position, trailBlockInteractionRadius, PrismScratch)
                : 0;

            for (int i = 0; i < prismCount; i++)
            {
                var prism = PrismScratch[i];
                if (!prism || prism.destroyed) continue;
                if (blockCollider && prism.gameObject == blockCollider.gameObject) continue;

                if (maxConsumesPerFrame > 0)
                    _pendingMeals.Enqueue(prism);
                else
                    EatPrism(prism); // unpaced legacy burst
            }

            DrainPendingMeals();
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
                var pp = prism.prismProperties;
                bool shielded = pp != null && (pp.IsShielded || pp.IsSuperShielded);
                bool isFaunaBody = prism is HealthPrism bodyPrism && bodyPrism.ResolveOwnerFauna() != null;
                if (shielded || isFaunaBody) return;
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
                prism.Damage(currentVelocity * embeddedHealthPrism.Volume, embeddedHealthPrism.Domain,
                    _damagerName, true, true);
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
            // Continuity rule - nothing pops out of existence. The sealed Fauna.Die already
            // dropped this boid's elemental crystal (mass conserved); shrink the body out
            // (suction-like) instead of instantly destroying it, then remove the husk.
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(FadeOutAndRemove());
            else if (!TryNetworkDespawn())
                Destroy(gameObject);
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
            // Networked boids: the server despawns the shrunk husk (after a grace so a
            // client's later-starting fade isn't clipped); client husks wait for it.
            if (!TryNetworkDespawn())
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

        void Update()
        {
            // Puppets: the server-authoritative NetworkTransform owns position and
            // rotation - local integration would just fight the interpolation.
            if (IsSimAuthority)
            {
                transform.position += currentVelocity * Time.deltaTime;
                transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime);
            }

            // Movers contract on EVERY peer: the body prism is registered mass - keep
            // its stored index position tracking the (locally-moved or replicated)
            // swimming boid, or client-side AOE/senses target the spawn point.
            NotifyBodyPrismsMoved();

            if (!isKilled && _pendingMeals.Count > 0)
                DrainPendingMeals();
        }
    }
}
