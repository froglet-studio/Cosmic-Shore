using CosmicShore.Engine;
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

        // PORT Deviation (engine serializer: the original auto-creates serialized List fields on
        // deserialize; the port engine has no deserializer, so initialize inline — same default
        // as an unconfigured inspector list):
        [SerializeField] List<BoidCollisionEffects> collisionEffects = new();

        [Header("Forager")]
        [Tooltip("ON = this boid is a food-web FORAGER (tadpole): it feeds when it grazes " +
                 "opposing mass (Explode effect) and starves (despawns) after starvationSeconds " +
                 "without feeding, so the swarm self-limits to available trail/flora prey. " +
                 "OFF (default) = drone/mound boid (BoidController) — never feeds or starves.")]
        [SerializeField] bool forager = false;

        BoxCollider blockCollider;

        List<Collider> separatedBoids = new List<Collider>();
        HealthPrism embeddedHealthPrism;

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
            // scale 0 and only grow once Prism.Initialize fires the scale animator — without
            // this the tadpole has no visible/active health prism. (LightFauna does the same
            // for the brittlestar/shark body.)
            var bodyPrisms = GetComponentsInChildren<HealthPrism>(true);
            for (int i = 0; i < bodyPrisms.Length; i++)
            {
                var hp = bodyPrisms[i];
                if (!hp) continue;
                hp.ChangeTeam(domain);
                hp.Initialize("tadpole");
            }

            currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
            float initialDelay = normalizedIndex * behaviorUpdateRate;
            StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
        }

        /// <summary>
        /// Foragers (tadpoles) actively HUNT the biggest mass concentration the cell senses
        /// — the densest region of environment prisms across the whole density grid —
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
                yield return new WaitForSeconds(behaviorUpdateRate);
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

            float averageSpeed = 0.0f;
            separatedBoids.Clear();

            // Shared non-alloc scratch (Fauna.OverlapScratch) — at swarm scale the old
            // per-tick Collider[] allocation was pure GC churn.
            int colliderCount = Physics.OverlapSphereNonAlloc(transform.position, cohesionRadius, OverlapScratch);

            for (int i = 0; i < colliderCount; i++)
            {
                Collider collider = OverlapScratch[i];
                if (!collider) continue;

                // Ignore our own collider (if present)
                if (blockCollider && collider.gameObject == blockCollider.gameObject) continue;

                Boid otherBoid = collider.GetComponentInParent<Boid>();
                Prism otherPrism = collider.GetComponent<Prism>();

                Vector3 diff = transform.position - collider.transform.position;
                float distance = diff.magnitude;
                if (distance == 0) continue;

                if (otherBoid)
                {
                    cohesion += -diff.normalized / distance;
                    alignment += collider.transform.forward;

                    if (distance < separationRadius)
                    {
                        separatedBoids.Add(collider);
                        separation += diff.normalized / distance;
                        averageSpeed += currentVelocity.magnitude;
                    }
                }
                else if (otherPrism)
                {
                    blockAttraction += -diff.normalized / distance;

                    // Drones eat OPPOSING-domain mass (combat). Foragers (tadpoles) are cleanup
                    // grazers: they eat prisms of ANY domain — so the dominant trail gets grazed
                    // too, not just the minority — but they must NOT eat:
                    //   - shielded prisms (protected structure like the Skim Race track), or
                    //   - other fauna's BODY prisms (brittlestar/shark bodies are HealthPrisms but
                    //     not Boids, so they reach this branch; herbivores eating fauna is the
                    //     predator's job, not a forager's). GetComponentInParent<Fauna> catches
                    //     any fauna body; this prism's own boid was already excluded above.
                    var pp = otherPrism.prismProperties;
                    bool shielded = pp != null && (pp.IsShielded || pp.IsSuperShielded);
                    bool isFaunaBody = collider.GetComponentInParent<Fauna>() != null;
                    bool edible = forager
                        ? (!shielded && !isFaunaBody)
                        : embeddedHealthPrism && otherPrism.Domain != embeddedHealthPrism.Domain;

                    if (distance < trailBlockInteractionRadius && embeddedHealthPrism && edible)
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
                                            // Foragers CONSUME (implode toward the tadpole → the
                                            // suction shader), matching how LightFauna grazes.
                                            // devastate:false so a shielded prism that somehow
                                            // reaches here only loses its shield, never gets eaten.
                                            otherPrism.Consume(transform, embeddedHealthPrism.Domain,
                                                embeddedHealthPrism.PlayerName + " tadpole", false);
                                            NotifyFed();
                                        }
                                        else
                                        {
                                            otherPrism.Damage(currentVelocity * embeddedHealthPrism.Volume, embeddedHealthPrism.Domain,
                                                embeddedHealthPrism.PlayerName + " boid", true);
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
            }

            int totalBoids = colliderCount - 1;

            if (totalBoids > 0)
            {
                cohesion /= totalBoids;
                cohesion = (cohesion - transform.position).normalized;
            }

            averageSpeed = separatedBoids.Count > 0 ? averageSpeed / separatedBoids.Count : currentVelocity.magnitude;

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
                float distToGoal = (target - transform.position).magnitude;
                speedMult = distToGoal > trailBlockInteractionRadius ? Mathf.Max(1f, huntSpeedMultiplier) : 1f;
            }
            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed * speedMult, maxSpeed * speedMult);

            desiredRotation = SafeLookRotation.TryGet(currentVelocity, out var desiredRot, this) ? desiredRot : transform.rotation;
        }

        protected override void Die(string killerName = "")
        {
            isKilled = true;
            StopAllCoroutines();
            Destroy(gameObject);
        }

        IEnumerator AddToMoundCoroutine()
        {
            isAttached = false;
            isTraveling = true;

            if (Mound) target = Mound.position;

            float scanRadius = 30f;

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
            transform.position += currentVelocity * Time.deltaTime;
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, Time.deltaTime);
        }
    }
}
