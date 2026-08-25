using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The worm colony — a POPULATION, not a creature with body parts (Docs/ECOSYSTEM.md
    /// §23). Its members are individual fauna (<see cref="WormSegmentFauna"/>:
    /// Head / Body / Tail), each carrying its own elemental heart; this root is the
    /// population's anchor and brain, driving the whole chain from one behavior tick and
    /// one movement pass however long it grows.
    ///
    /// The fight, expressed through the fundamentals:
    ///  • The HEAD is the mouth: it grazes prism mass voraciously (canonical
    ///    herbivore diet rules), and feeding is the ONLY source of new segments —
    ///    length is a record of what the colony has eaten (mass conserved).
    ///  • Danger prisms on head and tail make the ends the threat surface (the
    ///    standard domain-blind danger effect chain — no bespoke damage code).
    ///  • Killing a BODY segment SPLITS the population in two — the head and the
    ///    segments still attached to it stay THIS colony; the tail and everything
    ///    attached to it become a NEW colony that strongly separates from every other
    ///    worm population (see <see cref="TickSeparation"/>). Both halves regrow their
    ///    missing ends by DIFFERENTIATING the wound segment (a state change of
    ///    existing mass), so mid-body kills multiply the problem.
    ///  • Optimal play emerges: chain end-kills faster than the cell's fauna production
    ///    cycle and you always face soft tissue; slower and every kill is armored.
    ///    Starve it (deny mass) and it digests itself tail-first.
    ///  • Souls-like attack grammar: rest pulses → readable telegraph coil → locked
    ///    lunge (dodgeable) → straightened recovery (the punish window), plus a
    ///    tail-whip against loiterers. Contact damage is entirely the danger
    ///    prisms' existing impact pipeline.
    ///
    /// The colony root is lineage-registered (spawner pipeline, live-count, cleanup)
    /// but carries no heart of its own: it is the population anchor, and the crystal
    /// invariant lands on the MEMBERS — every segment drops its heart when killed. The
    /// root is classified Predator so nothing in the food web preys on it (nothing eats
    /// a kaiju), and <see cref="Predated"/> is sealed off — the segments are the
    /// killable surface.
    /// </summary>
    public class WormFauna : Fauna
    {
        const string PLAYER_NAME = "WormColony";
        const string MouthName = "Mouth";

        [Header("Worm Colony")]
        [SerializeField] WormColonyConfigSO config;
        [Tooltip("The three colony member types. Head and Tail author danger prisms + " +
                 "an elemental heart; Body is the splittable connective tissue.")]
        [SerializeField] WormSegmentFauna headPrefab;
        [SerializeField] WormSegmentFauna bodyPrefab;
        [SerializeField] WormSegmentFauna tailPrefab;

        // The chain, head-first. The list IS the topology — segment order, wounds,
        // and splits all operate on it (simpler than per-segment prev/next links,
        // at the cost of an IndexOf per death — rare events on a short list).
        readonly List<WormSegmentFauna> segments = new();

        // Adoption channel for split-off colonies: set before Initialize so the
        // new brain wires the severed half instead of building a fresh chain.
        List<WormSegmentFauna> _pendingAdoption;

        // The colony's production clock: it rides the CELL's fauna production cycle, so a
        // worm grows on the same heartbeat the biome spawns wildlife on (§23.9). Stamped
        // at Initialize so a fresh colony — and each half of a split — waits a full cycle
        // before its first production.
        float _lastProductionTime;
        float _lastStarvationShed;

        // --- Attack state machine (souls-like grammar) ---
        //  Cruise → (pilot sensed in a hunt window) Pursue → (inside StrikeRange)
        //  Telegraph → Lunge → Recover → Cruise.
        enum AttackState { Cruise = 0, Pursue = 1, Telegraph = 2, Lunge = 3, Recover = 4 }
        AttackState _state = AttackState.Cruise;
        float _stateSince;
        Transform _threat;        // acquired on the behavior tick, hunt windows only
        Vector3 _lungePoint;      // locked at telegraph end — dodge by moving after
        float _huntCycleAnchor;
        Transform _mouth;         // suction sink for devoured creatures (head fang centroid)
        float _whipUntil = -1f;
        float _lastWhip = float.NegativeInfinity;

        // Leader steering state: the sway oscillation is applied on top of this
        // base rotation every frame (never accumulated into it, so it can't drift).
        Quaternion _leaderBaseRotation = Quaternion.identity;
        float _currentSpeed;

        // Boid separation from OTHER colonies, recomputed per behavior tick and held
        // between ticks (the goal pull is likewise tick-scoped).
        Vector3 _separation;

        /// <summary>Live head speed — jousting a segment heart means outracing the kaiju.</summary>
        public override float CurrentSpeed => _currentSpeed;

        /// <summary>Current chain length (the boss's visible health-and-history bar).</summary>
        public int SegmentCount => segments.Count;

        /// <summary>
        /// Squared distance from <paramref name="from"/> to the nearest point of this
        /// colony's BODY, and that point. A worm is long, so head-to-head distance is
        /// the wrong read for separation — neighbours must repel from the part of each
        /// other that is actually close.
        /// </summary>
        public bool TryGetNearestBodyPoint(Vector3 from, out Vector3 point, out float sqrDistance)
        {
            point = default;
            sqrDistance = float.PositiveInfinity;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (!seg) continue;
                float d = (seg.transform.position - from).sqrMagnitude;
                if (d < sqrDistance)
                {
                    sqrDistance = d;
                    point = seg.transform.position;
                }
            }
            return sqrDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// Closest approach between this colony's body and <paramref name="other"/>'s — the
        /// two points, one on each worm, that are nearest each other. Both worms are long, so
        /// neither head-to-head nor head-to-their-nearest-segment describes how close the two
        /// populations actually are: a worm being trailed along its flank has to feel it from
        /// the segment that is being crowded, not from its nose. O(mine × theirs) at the
        /// behavior-tick cadence over a handful of colonies — no physics, no prism queries.
        /// </summary>
        bool TryGetNearestApproach(WormFauna other, out Vector3 mine, out Vector3 theirs,
            out float sqrDistance)
        {
            mine = default;
            theirs = default;
            sqrDistance = float.PositiveInfinity;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (!seg) continue;
                Vector3 from = seg.transform.position;
                if (!other.TryGetNearestBodyPoint(from, out var point, out float sqr)) continue;
                if (sqr >= sqrDistance) continue;
                sqrDistance = sqr;
                mine = from;
                theirs = point;
            }
            return sqrDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// An apex FORAGER hunts mass, so the colony overrides the base fauna goal
        /// (which idles at the cell crystal while Calm): head for the densest sensed
        /// region at every phase — the same "voracious" read LightFauna gets in nucleus
        /// cells, and the reason a worm dropped outside the membrane comes home instead
        /// of drifting in empty space. The per-instance orbit offset is kept so two
        /// colonies never converge on the identical point (the base class's
        /// anti-convergence rule — load-bearing now that worms also repel each other).
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (cell == null) return Goal;
            return cell.GetDensestRegionAnyDomain() + GoalOrbitOffset;
        }

        /// <summary>
        /// Nothing in the food web preys on the kaiju: the root is not devourable or
        /// joustable (its segments are the killable surface — their hearts joust, their
        /// prisms shatter). Returning false also keeps a hunting predator honest: a
        /// failed Predated never feeds it.
        /// </summary>
        public override bool Predated(string predatorName, Transform devourTarget) => false;

        // The element a per-element species config picked for this colony (None = the
        // config didn't say; segments keep their authored hearts and wounds use the
        // WormColonyConfigSO.RegrownEndElement fallback).
        Element _pickedHeartElement = Element.None;

        /// <summary>
        /// Element-as-data for a POPULATION: the colony root stays heartless (it is the
        /// anchor; the crystal invariant lands on its members — Docs/ECOSYSTEM.md §23.3),
        /// so a per-element species config (the Lifeform Matrix toy, an element-authored
        /// SpawnProfile entry) forwards its pick to EVERY segment's heart instead of
        /// provisioning a crystal on this empty anchor. Remembered so every member grown on
        /// a later production cycle carries the same element — a colony breeds true.
        /// </summary>
        protected override void ProvisionHeart(Element element)
        {
            _pickedHeartElement = element;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg) seg.ReprovisionHeart(element);
            }
        }

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell); // record the explicit host cell

            if (!config || !headPrefab || !bodyPrefab || !tailPrefab)
            {
                CSDebug.LogError($"{nameof(WormFauna)} on {name} is missing its config or segment prefabs.");
                return;
            }

            if (_pendingAdoption != null)
                AdoptChain(_pendingAdoption);
            else
                BuildChain();
            _pendingAdoption = null;

            if (segments.Count > 0)
                _leaderBaseRotation = segments[0].transform.rotation;

            // The jaws: a light transform tracking the head's fang centroid each frame.
            // Deliberately NOT a danger prism's own transform — the sink must keep
            // tracking the swimming worm even after players shoot the fangs off.
            // A split-born colony is a clone of its parent root, so it may already
            // carry one — reuse it rather than stacking a second.
            _mouth = transform.Find(MouthName);
            if (!_mouth)
            {
                var mouthGO = new GameObject(MouthName);
                mouthGO.transform.SetParent(transform, false);
                _mouth = mouthGO.transform;
            }

            // Attack pulses start in the REST stretch (the shark's pattern): the
            // first hunt window opens (interval - duration) seconds from now, so a
            // fresh kaiju cruises and grazes before its first assault.
            _huntCycleAnchor = Time.time - Mathf.Min(config.HuntDurationSeconds, config.HuntIntervalSeconds);
            _lastStarvationShed = Time.time;
            _lastProductionTime = Time.time;
        }

        protected override void Start()
        {
            base.Start(); // goal coroutine (the colony DOES steer by Fauna.ResolveGoal)
            StartCoroutine(BehaviorTickCoroutine());
        }

        /// <summary>
        /// Kaiju proportions, recovered from the 2024 worm authoring: segment i tapers
        /// by TaperPerSegment^i (the head is the biggest thing on the worm).
        /// </summary>
        float SegmentTargetScale(int index) =>
            config.KaijuScale * Mathf.Pow(config.TaperPerSegment, index);

        /// <summary>
        /// Rest distance across the link between two adjacent members: the body gap,
        /// tapered down the chain, widened behind the HEAD (it needs room —
        /// HeadGapMultiplier) and into the TAIL (TailGapMultiplier). Ratios recovered from
        /// the 2024 chain layout. Roles are passed rather than read off the list so the
        /// build pass, the follow pass and every growth site share ONE formula — a member
        /// being placed does not exist in the list yet.
        /// </summary>
        float LinkSpacing(int linkIndex, WormSegmentRole prevRole, WormSegmentRole nextRole)
        {
            float s = config.SegmentSpacing * config.KaijuScale
                      * Mathf.Pow(config.TaperPerSegment, Mathf.Max(0, linkIndex - 1));
            if (prevRole == WormSegmentRole.Head) s *= config.HeadGapMultiplier;
            if (nextRole == WormSegmentRole.Tail) s *= config.TailGapMultiplier;
            return s;
        }

        /// <summary>Rest distance between segments[linkIndex-1] and segments[linkIndex].</summary>
        float RestSpacing(int linkIndex) =>
            LinkSpacing(linkIndex, segments[linkIndex - 1].Role, segments[linkIndex].Role);

        /// <summary>Fresh spawn: head + bodies + tail laid out along -forward, kaiju-scaled and tapered.</summary>
        void BuildChain()
        {
            Vector3 dir = transform.forward;
            Quaternion facing = SafeLookRotation.TryGet(dir, out var rot, this) ? rot : Quaternion.identity;
            int count = Mathf.Max(3, config.SpawnSegmentCount);

            Vector3 pos = transform.position;
            for (int i = 0; i < count; i++)
            {
                var prefab = i == 0 ? headPrefab : i == count - 1 ? tailPrefab : bodyPrefab;
                AddSegmentToChain(Instantiate(prefab, pos, facing), segments.Count,
                    Vector3.one * SegmentTargetScale(i));
                if (i + 1 < count)
                    pos -= dir * LinkSpacing(i + 1, RoleForBuildIndex(i, count),
                                             RoleForBuildIndex(i + 1, count));
            }
        }

        static WormSegmentRole RoleForBuildIndex(int index, int count) =>
            index == 0 ? WormSegmentRole.Head
            : index == count - 1 ? WormSegmentRole.Tail
            : WormSegmentRole.Body;

        /// <summary>Split adoption: the severed rear half becomes this colony's chain.</summary>
        void AdoptChain(List<WormSegmentFauna> adopted)
        {
            for (int i = 0; i < adopted.Count; i++)
            {
                var seg = adopted[i];
                if (!seg) continue;
                seg.Colony = this;
                segments.Add(seg);
            }
        }

        void AddSegmentToChain(WormSegmentFauna seg, int index, Vector3 scale)
        {
            seg.transform.localScale = scale;
            seg.domain = domain;
            seg.Colony = this;
            seg.Initialize(cell);
            // A colony breeds true: a member grown after the species config picked an
            // element carries that element's heart, not the body prefab's authored one.
            // No-ops when they already agree (EnsureElementalCrystal keeps a match).
            if (_pickedHeartElement != Element.None)
                seg.ReprovisionHeart(_pickedHeartElement);
            if (cell) cell.RegisterSpawnedObject(seg.gameObject);
            segments.Insert(index, seg);
        }

        // -------------------------------------------------------------------
        //  Movement — one pass drives the whole chain (follow-the-leader with
        //  a head-driven slither wave; the body propagates it naturally).
        // -------------------------------------------------------------------

        void Update()
        {
            // Defensive sweep: segments can be destroyed outside the death path
            // (scene teardown, turn-end cleanup) — a null in the chain is fatal to
            // the follow pass, and an emptied colony must die, not idle.
            if (segments.RemoveAll(s => !s) > 0 && segments.Count == 0)
            {
                Die();
                return;
            }
            if (segments.Count == 0 || !config) return;

            float dt = Time.deltaTime;
            UpdateAttackState();
            MoveLeader(dt);
            FollowChain(dt);
            GlideScales(dt);

            // Root anchors at the head so registry/cell distance reads track the
            // creature; the jaws track the head's fang centroid so a devoured
            // creature suctions into the actual mouth as the worm swims.
            transform.position = segments[0].transform.position;
            if (_mouth) _mouth.position = segments[0].MouthPoint;
            for (int i = 0; i < segments.Count; i++)
                segments[i].SyncBodyPrismsToIndex();
        }

        float AggressionMultiplier(float[] table)
        {
            if (cell == null || table == null || table.Length == 0) return 1f;
            int idx = Mathf.Clamp((int)cell.AggressionLevel, 0, table.Length - 1);
            return Mathf.Max(0.05f, table[idx]);
        }

        bool IsHuntWindow
        {
            get
            {
                float interval = config.HuntIntervalSeconds;
                if (interval <= 0f) return true;
                float duration = Mathf.Min(config.HuntDurationSeconds, interval);
                return Mathf.Repeat(Time.time - _huntCycleAnchor, interval) < duration;
            }
        }

        void SetState(AttackState state)
        {
            _state = state;
            _stateSince = Time.time;
        }

        void UpdateAttackState()
        {
            float elapsed = Time.time - _stateSince;
            switch (_state)
            {
                case AttackState.Cruise:
                    if (_threat && IsHuntWindow)
                        SetState(AttackState.Pursue);
                    break;

                case AttackState.Pursue:
                    // The chase: nose-on and faster until the pilot is inside striking
                    // distance, then the wind-up. Lose them (or the window closes) and
                    // the kaiju goes back to grazing.
                    if (!_threat || !IsHuntWindow)
                        SetState(AttackState.Cruise);
                    else if ((segments[0].transform.position - _threat.position).sqrMagnitude
                             <= config.StrikeRange * config.StrikeRange)
                        SetState(AttackState.Telegraph);
                    break;

                case AttackState.Telegraph:
                    if (!_threat)
                    {
                        SetState(AttackState.Cruise); // target fled mid-wind-up
                    }
                    else if (elapsed >= config.TelegraphSeconds)
                    {
                        // The strike point locks HERE — everything after is dodgeable.
                        _lungePoint = _threat.position;
                        SetState(AttackState.Lunge);
                    }
                    break;

                case AttackState.Lunge:
                    if (elapsed >= config.LungeMaxSeconds ||
                        (segments[0].transform.position - _lungePoint).sqrMagnitude
                            <= config.LungeArriveRadius * config.LungeArriveRadius)
                        SetState(AttackState.Recover);
                    break;

                case AttackState.Recover:
                    if (elapsed >= config.RecoverSeconds)
                    {
                        _threat = null; // re-acquired by the next behavior tick
                        SetState(AttackState.Cruise);
                    }
                    break;
            }
        }

        void MoveLeader(float dt)
        {
            Transform leader = segments[0].transform;

            float speedMult = AggressionMultiplier(config.SpeedByAggression);
            float targetSpeed;
            Vector3 desiredDirection;
            float swayMultiplier = 1f;
            float turnRate = config.TurnDegreesPerSecond;
            // Separation applies while the worm is free to steer. A committed strike
            // (Telegraph/Lunge) ignores it — the wind-up must stay readable and the
            // locked lunge must stay dodgeable-by-moving, not deflected by a neighbour.
            bool separates = false;

            switch (_state)
            {
                case AttackState.Pursue:
                    // Nose-on chase — tracks a juking pilot between behavior ticks.
                    desiredDirection = _threat
                        ? (_threat.position - leader.position).normalized
                        : leader.forward;
                    targetSpeed = config.CruiseSpeed * config.PursuitSpeedMultiplier * speedMult;
                    turnRate *= config.PursuitTurnMultiplier;
                    separates = true;
                    break;

                case AttackState.Telegraph:
                    // Rear back and coil: pull away from the threat with an upward
                    // arch, near-stopped, slither exaggerated — the readable wind-up.
                    Vector3 away = _threat
                        ? (leader.position - _threat.position).normalized
                        : leader.forward;
                    desiredDirection = (away + Vector3.up * 0.8f).normalized;
                    targetSpeed = config.CruiseSpeed * 0.15f * speedMult;
                    swayMultiplier = config.TelegraphAmplitudeMultiplier;
                    turnRate *= 2f;
                    break;

                case AttackState.Lunge:
                    desiredDirection = (_lungePoint - leader.position).normalized;
                    targetSpeed = config.LungeSpeed;
                    swayMultiplier = 0.2f; // arrow-straight strike
                    turnRate *= 3f;
                    break;

                case AttackState.Recover:
                    // Spent: drift straight and slow — the co-op punish window.
                    desiredDirection = leader.forward;
                    targetSpeed = config.CruiseSpeed * config.RecoverSpeedFraction * speedMult;
                    swayMultiplier = 0.3f;
                    separates = true;
                    break;

                default:
                    desiredDirection = (Goal - leader.position).normalized;
                    targetSpeed = config.CruiseSpeed * speedMult;
                    separates = true;
                    break;
            }

            // Boid steering: goal pull (weight 1) + neighbour repulsion. The degenerate
            // guard below keeps a cancelling sum from zeroing the heading (a zeroed
            // heading is a PERMANENT stall — see Fauna.DegenerateSteeringSqr).
            if (separates && _separation != Vector3.zero)
                desiredDirection = (desiredDirection + _separation * config.ColonySeparationWeight).normalized;

            if (desiredDirection.sqrMagnitude > DegenerateSteeringSqr &&
                SafeLookRotation.TryGet(desiredDirection, out var targetRotation, this))
                _leaderBaseRotation = Quaternion.RotateTowards(_leaderBaseRotation, targetRotation, turnRate * dt);

            // The slither wave: a yaw oscillation layered ON TOP of the steered base
            // rotation each frame (never accumulated into it). The body inherits the
            // wave through follow-the-leader — no per-segment animation needed.
            float sway = Mathf.Sin(Time.time * config.UndulationFrequency)
                         * config.UndulationYawDegrees * swayMultiplier;
            leader.rotation = _leaderBaseRotation * Quaternion.Euler(0f, sway, 0f);

            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, 1f - Mathf.Exp(-3f * dt));
            leader.position += leader.forward * (_currentSpeed * dt);
        }

        /// <summary>
        /// Every segment glides to its taper target (continuity): a freshly grown
        /// segment blooms from zero, and the whole chain re-proportions smoothly when
        /// topology changes (growth, splits, end deaths). Early-outs once settled.
        /// </summary>
        void GlideScales(float dt)
        {
            float glideT = 1f - Mathf.Exp(-(3f / Mathf.Max(0.05f, config.SegmentBloomSeconds)) * dt);
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i].transform;
                Vector3 target = Vector3.one * SegmentTargetScale(i);
                if ((seg.localScale - target).sqrMagnitude <= 1e-4f) continue;
                seg.localScale = Vector3.Lerp(seg.localScale, target, glideT);
            }
        }

        void FollowChain(float dt)
        {
            float followT = 1f - Mathf.Exp(-config.FollowSharpness * dt);
            float rotateT = 1f - Mathf.Exp(-config.RotationSharpness * dt);
            bool whipping = Time.time < _whipUntil;
            int whipFrom = segments.Count - Mathf.Min(config.WhipSegmentCount, segments.Count - 1);

            for (int i = 1; i < segments.Count; i++)
            {
                Transform prev = segments[i - 1].transform;
                Transform seg = segments[i].transform;

                Vector3 followPoint = prev.position - prev.forward * RestSpacing(i);

                // Tail whip: the rear segments' follow points swing laterally so the
                // danger tail sweeps its neighborhood. Contact damage is the danger
                // prisms' own effect chain — this is pure motion.
                if (whipping && i >= whipFrom)
                {
                    float phase = Time.time * config.WhipFrequency + i * config.UndulationPhaseStep;
                    followPoint += prev.right * (Mathf.Sin(phase) * config.WhipLateralAmplitude * config.KaijuScale);
                }

                seg.position = Vector3.Lerp(seg.position, followPoint, followT);

                Vector3 toPrev = prev.position - seg.position;
                if (toPrev.sqrMagnitude > DegenerateSteeringSqr &&
                    SafeLookRotation.TryGet(toPrev, out var look, this))
                    seg.rotation = Quaternion.Slerp(seg.rotation, look, rotateT);
            }
        }

        // -------------------------------------------------------------------
        //  Behavior tick — senses, feeding, growth, wounds, starvation, whip.
        // -------------------------------------------------------------------

        IEnumerator BehaviorTickCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0.1f, config ? config.BehaviorTickSeconds : 1.5f)
                    * AggressionMultiplier(config ? config.CadenceByAggression : null));

                if (!config || segments.Count == 0) continue;

                TickStarvation();
                if (segments.Count == 0) yield break; // starvation took the last segment

                TickSeparation();
                TickThreatScan();
                TickFeeding();
                TickPredation();
                TickProduction();
                TickTailWhip();
            }
        }

        /// <summary>
        /// Population bounded by consumption: while starving, the colony digests
        /// itself — the tail-most segment withers every shed interval. Deny the
        /// kaiju food and it shrinks; keep denying and it dies.
        /// </summary>
        void TickStarvation()
        {
            if (!IsStarving) return;
            if (Time.time - _lastStarvationShed < config.StarvationShedIntervalSeconds) return;

            _lastStarvationShed = Time.time;
            segments[segments.Count - 1].WitherAway(StarvationKiller);
        }

        /// <summary>
        /// Boid separation between worm POPULATIONS — the rule that makes a split read as
        /// two animals rather than one animal drawn twice. Each neighbouring colony pushes
        /// this worm along the axis of CLOSEST APPROACH between the two bodies, so two kaiju
        /// sharing a cell swim around each other instead of interpenetrating, and a
        /// freshly-severed rear half peels away from the front half it was part of a moment
        /// ago. Colonies are lineage-registered, so this is a walk of the cell's small fauna
        /// registry — no physics, no prism queries.
        ///
        /// The term is a NORMALIZED direction scaled by a falloff in [0,1] — 1 where the two
        /// bodies touch, 0 at <c>ColonySeparationRadius</c>. The raw inverse-square form this
        /// replaced (<c>diff/|diff|²</c>) has magnitude <c>1/|diff|</c>, which at real worm
        /// distances (30–160u) is 0.006–0.03 against a UNIT goal direction: the weight could
        /// not deflect the steering by more than a few degrees no matter how it was tuned, so
        /// worms only nominally repelled. With the falloff, <c>ColonySeparationWeight</c> is a
        /// true ratio against the goal pull — above 1 the repulsion wins at close range, which
        /// is what "strong separation" means here.
        /// </summary>
        void TickSeparation()
        {
            _separation = Vector3.zero;
            // Null-guarded because this now runs from the SPLIT path too, not only from the
            // behavior coroutine (which already gates on config). Initialize reports a missing
            // config loudly; a death must not also cascade an NRE on top of it.
            if (!config) return;
            float radius = config.ColonySeparationRadius;
            if (radius <= 0f) return;
            var host = cell;
            if (host == null) return;

            float radiusSqr = radius * radius;
            var fauna = host.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                if (fauna[i] is not WormFauna other || ReferenceEquals(other, this)) continue;
                if (!TryGetNearestApproach(other, out var mine, out var theirs, out float sqr)) continue;
                if (sqr > radiusSqr || sqr <= DegenerateSteeringSqr) continue;

                float falloff = 1f - Mathf.Sqrt(sqr) / radius;
                _separation += (mine - theirs).normalized * (falloff * falloff);
            }
        }

        void TickThreatScan()
        {
            if (!IsHuntWindow)
            {
                _threat = null;
                return;
            }
            if (_state == AttackState.Lunge || _state == AttackState.Recover)
                return; // committed — the locked strike plays out

            Vector3 headPos = segments[0].transform.position;
            _threat = FindNearestVessel(headPos, config.AggroRadius);
        }

        /// <summary>
        /// Vessel scan via the shared physics scratch masked to non-prism layers —
        /// the sanctioned vessel-sensing path (prisms never go through physics).
        /// </summary>
        Transform FindNearestVessel(Vector3 origin, float radius)
        {
            if (radius <= 0f) return null;
            int hits = Physics.OverlapSphereNonAlloc(origin, radius, OverlapScratch, NonPrismOverlapMask);
            Transform best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < hits; i++)
            {
                var collider = OverlapScratch[i];
                if (!collider || !collider.TryGetComponent(out IVesselStatus _)) continue;
                float sqr = (collider.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = collider.transform;
                }
            }
            return best;
        }

        /// <summary>
        /// The head is the colony's mouth: suction edible prisms around it (bounded
        /// bites per tick), feed the starvation clock, and bank feeds toward growth.
        /// A wounded colony whose leader is not yet a Head cannot feed — regrow the
        /// head or starve, which is exactly the co-op pressure the fight wants.
        /// </summary>
        void TickFeeding()
        {
            if (segments[0].Role != WormSegmentRole.Head) return;

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            if (spatialIndex == null || !spatialIndex.IsAvailable) return;

            Transform head = segments[0].transform;
            int found = spatialIndex.QuerySphere(head.position, config.MouthRadius, PrismScratch);
            int bites = 0;
            for (int i = 0; i < found && bites < config.MaxBitesPerTick; i++)
            {
                var prism = PrismScratch[i];
                if (!IsEdiblePrism(prism)) continue;
                prism.Consume(head, domain, PLAYER_NAME, true, true);
                NotifyFed();
                bites++;
            }
        }

        /// <summary>
        /// The PREDATOR half of the apex omnivore: any live creature whose root comes
        /// within FaunaBiteRange of the jaws is devoured — it breaks apart and suctions
        /// into the mouth (Predated with a devour target), exactly the shark's kill.
        /// Every catch feeds the colony, so hunting also grows it.
        ///
        /// Unlike the shark this is NOT limited to herbivores: an apex kaiju eats
        /// sharks too. It skips its own segments (they aren't in the registry anyway),
        /// other worm colonies (kaiju don't cannibalise), and predation-immune
        /// newborns. Pure math against the cell's small fauna registry — no physics.
        /// </summary>
        void TickPredation()
        {
            if (config.FaunaBiteRange <= 0f || !_mouth) return;
            var host = cell;
            if (host == null) return;

            float rangeSqr = config.FaunaBiteRange * config.FaunaBiteRange;
            Vector3 mouthPos = _mouth.position;
            var fauna = host.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (!f || f == this || f is WormFauna or WormSegmentFauna) continue;
                if (!f.IsAlivePrey || f.IsPredationImmune) continue;
                if ((f.transform.position - mouthPos).sqrMagnitude > rangeSqr) continue;

                // Predated respects the prey's immunity and returns false if it
                // couldn't be eaten — only feed on a real kill.
                if (f.Predated(PLAYER_NAME, _mouth))
                    NotifyFed();
            }
        }

        /// <summary>
        /// Canonical herbivore edibility (the same rule LightFauna grazes by): the
        /// diet is spatialized through Cell.IsPreyForHerbivore (nucleus interior is
        /// the untouchable claim; exterior is voracious any-domain), shielded and
        /// super-shielded mass is never food (<see cref="Fauna.IsShieldedMass"/>),
        /// and creatures are never food — a HealthPrism with no LifeForm is somebody's
        /// body (including this colony's own segments) and is skipped.
        /// </summary>
        bool IsEdiblePrism(Prism prism)
        {
            if (!prism || prism.destroyed) return false;
            if (IsShieldedMass(prism)) return false;
            if (prism is HealthPrism hp)
            {
                if (!hp.LifeForm) return false;
                return cell != null
                    ? IsPreyForMe(prism.transform.position, hp.LifeForm.domain)
                    : hp.LifeForm.domain != domain;
            }
            return cell != null
                ? IsPreyForMe(prism.transform.position, prism.Domain)
                : prism.Domain != domain;
        }

        /// <summary>
        /// The colony's PRODUCTION cycle — a population grows on its biome's own heartbeat.
        /// Once per <see cref="ProductionPeriod"/> (the host cell's fauna production cycle,
        /// <see cref="Cell.CurrentFaunaSpawnPeriod"/>) the colony grows exactly ONE member,
        /// in priority order:
        ///
        ///   1. a HEAD, if it has none — a decapitated colony cannot feed, so a mouth comes
        ///      before length, and this is the only way a beheaded worm ever recovers;
        ///   2. a TAIL, if it has none — the stinger that makes its rear dangerous again;
        ///   3. otherwise a BODY segment behind the head — the colony simply gets longer.
        ///
        /// Same shape as the lattice flora colonies, which birth one plant per fauna-wave
        /// period (Docs/ECOSYSTEM.md §32.7): the cell's fauna cadence is the platform's
        /// "population production cycle", so a worm's growth rate is a property of the BIOME
        /// it lives in rather than of the species. Reading the PERIOD rather than listening
        /// for a wave event is deliberate — <c>OnFaunaWaveSpawned</c> is raised only by
        /// <c>RandomLifeSpawner</c>, so an event subscription would be dead code in every
        /// IntensityWise cell (the spawner-swap trap), and the period is served by the cell
        /// itself either way.
        ///
        /// Invariants: this is PRODUCTION gating, which the conserved-mass law permits —
        /// nothing here removes mass, there is no lifespan and no decay. Body growth is
        /// gated on the colony being fed, so length still only accrues while the kaiju is
        /// eating; head/tail regrowth is NOT, because a headless colony cannot feed and
        /// gating its mouth on feeding is a deadlock. <see cref="WormColonyConfigSO.MaxSegmentsPerWorm"/>
        /// still caps every path — it is the collider-budget backstop — and it needs no
        /// exemption for the ends: reaching the cap requires a complete colony, so losing an
        /// end always drops it below the cap first, and a missing end can never be blocked.
        /// </summary>
        void TickProduction()
        {
            if (segments.Count == 0) return;
            if (Time.time - _lastProductionTime < ProductionPeriod) return;

            // The cycle TURNS whether or not it produces. Stamping here rather than after a
            // successful growth is what makes an end kill cost a real cycle: otherwise a
            // colony sitting at the segment cap banks unbounded elapsed time and regrows a
            // shot-off head on the next behavior tick (~1.5 s) instead of the next wave.
            _lastProductionTime = Time.time;

            if (segments.Count >= config.MaxSegmentsPerWorm) return;

            if (segments[0].Role != WormSegmentRole.Head)
                GrowHead();
            else if (segments[segments.Count - 1].Role != WormSegmentRole.Tail)
                GrowTail();
            else if (!IsStarving)
                GrowBody();
            // else starving: the colony is digesting itself, not lengthening.
        }

        /// <summary>
        /// The host cell's fauna production period. Falls back to the config's own number
        /// only for a cell that authors no SpawnProfile (a bare tool scene) — every real
        /// biome answers this itself.
        /// </summary>
        float ProductionPeriod
        {
            get
            {
                float period = cell ? cell.CurrentFaunaSpawnPeriod : 0f;
                return period > 0f ? period : Mathf.Max(0.5f, config.FallbackProductionPeriodSeconds);
            }
        }

        /// <summary>
        /// Grows a real HEAD at the front of the chain — the whole armored prefab, not a
        /// hardened stump: a regrown mouth has its plate cage, its fangs (which ARE the
        /// jaws the feeding and devouring paths read) and its own heart. It blooms from
        /// zero and takes over the steering, inheriting the outgoing leader's heading so
        /// the body does not snap.
        /// </summary>
        void GrowHead()
        {
            Transform leader = segments[0].transform;
            Vector3 pos = leader.position
                          + leader.forward * LinkSpacing(1, WormSegmentRole.Head, segments[0].Role);
            var seg = Instantiate(headPrefab, pos, leader.rotation);
            AddSegmentToChain(seg, 0, Vector3.zero);
            _leaderBaseRotation = seg.transform.rotation;
        }

        /// <summary>Grows a real TAIL off the rear of the chain — stinger, tip tier and heart.</summary>
        void GrowTail()
        {
            int index = segments.Count;
            Transform last = segments[index - 1].transform;
            Vector3 pos = last.position
                          - last.forward * LinkSpacing(index, segments[index - 1].Role, WormSegmentRole.Tail);
            AddSegmentToChain(Instantiate(tailPrefab, pos, last.rotation), index, Vector3.zero);
        }

        /// <summary>
        /// Grows a BODY segment in behind the head. It is inserted at scale zero and blooms
        /// to its taper target through the per-frame GlideScales pass while its prisms run
        /// their own growth stamps; the chain's follow springs absorb the insertion, so
        /// there is no bespoke make-room animation and every downstream member
        /// re-proportions through the same glide (its taper index just shifted by one).
        /// </summary>
        void GrowBody()
        {
            Transform head = segments[0].transform;
            Vector3 pos = head.position
                          - head.forward * LinkSpacing(1, WormSegmentRole.Head, WormSegmentRole.Body);
            AddSegmentToChain(Instantiate(bodyPrefab, pos, head.rotation), 1, Vector3.zero);
        }

        void TickTailWhip()
        {
            if (!IsHuntWindow) return;
            if (Time.time - _lastWhip < config.TailWhipCooldownSeconds) return;
            if (segments.Count < 2) return;

            Vector3 tailPos = segments[segments.Count - 1].transform.position;
            if (!FindNearestVessel(tailPos, config.TailWhipRadius)) return;

            _lastWhip = Time.time;
            _whipUntil = Time.time + config.TailWhipSeconds;
        }

        // -------------------------------------------------------------------
        //  Topology — segment death, wounds, splits, colony death.
        // -------------------------------------------------------------------

        /// <summary>
        /// A member died (through the sealed Fauna.Die — its heart already dropped; every
        /// segment carries one). End deaths open a wound; an interior death SPLITS the
        /// POPULATION: the head and every segment still attached to it stay this colony,
        /// while the tail and everything attached to IT become a new colony that strongly
        /// separates from this one. Both halves start regrowing their missing ends. The
        /// last member's death is the colony's death.
        /// </summary>
        public void HandleSegmentDeath(WormSegmentFauna segment, string killerName = "")
        {
            int index = segments.IndexOf(segment);
            if (index < 0) return;
            segments.RemoveAt(index);

            if (segments.Count == 0)
            {
                Die(killerName);
                return;
            }

            if (index == 0)
            {
                // Decapitated: the new leader inherits the steering base so the body
                // doesn't snap. A replacement head is grown on the cell's next fauna
                // production cycle (TickProduction) — until then the colony cannot feed.
                _leaderBaseRotation = segments[0].transform.rotation;
                if (_state == AttackState.Telegraph || _state == AttackState.Lunge)
                    SetState(AttackState.Cruise); // the striking mouth is gone
            }
            else if (index < segments.Count)
            {
                // Interior kill: sever. This colony keeps the HEAD side (head + every
                // segment still attached to it); the TAIL side (everything from the dead
                // member's successor to the tail, inclusive) becomes a new population.
                // Segment totals are conserved — splitting punishes, it never multiplies
                // mass. Each half regrows the end it is now missing on its own next
                // production cycle.
                var rear = segments.GetRange(index, segments.Count - index);
                segments.RemoveRange(index, segments.Count - index);
                SpawnSplitColony(rear);
            }
            // else: the TAIL died — the rear stays soft until the next production cycle
            // grows a replacement stinger.
        }

        /// <summary>
        /// The severed tail side becomes its own POPULATION. The new brain is a CLONE OF
        /// THIS ONE rather than a fresh prefab instantiation, which is both simpler and
        /// biologically right: the two halves of a split worm are the same animal, so
        /// the child inherits this colony's exact tuning (config, segment prefabs, diet,
        /// starvation clock) with no serialized self-reference to keep wired. The
        /// segments are not children of the root, so the clone brings no body with it —
        /// it adopts the severed half in Initialize.
        ///
        /// It is a genuinely SEPARATE population from the moment it exists: it rolls its
        /// own goal orbit offset (Fauna.Start) so the two halves stop seeking the identical
        /// point, and both sides evaluate separation immediately rather than waiting up to a
        /// behavior tick — the two bodies are interpenetrating at the instant of the cut,
        /// which is exactly where the falloff in <see cref="TickSeparation"/> is strongest,
        /// so they shoulder apart on the next frame instead of swimming home in convoy.
        /// </summary>
        void SpawnSplitColony(List<WormSegmentFauna> rear)
        {
            if (rear == null || rear.Count == 0) return;

            var colony = Instantiate(this, rear[0].transform.position, rear[0].transform.rotation);
            colony.domain = domain;
            colony.Goal = Goal;
            colony._pendingAdoption = rear;
            colony.Initialize(cell);
            // Same lineage AND the same identity: the split registers in the cell's live
            // count (so the spawner's seed floor sees two worms), inherits the species
            // config, and is handed this colony's own variant pick rather than re-rolling
            // one — the halves of a split worm are the same animal and must keep the same
            // element (heredity, exactly as an offspring inherits its parent's pick).
            if (cell && SourceConfig)
                colony.AssignLineage(cell, SourceConfig, VariantPick);
            if (cell)
                cell.RegisterSpawnedObject(colony.gameObject);

            // Both populations feel each other NOW (see the summary above).
            colony.TickSeparation();
            TickSeparation();
        }

        /// <summary>
        /// Colony death: the chain is already gone (every segment died through its
        /// own sealed path, dropping its heart where it had one). Only the empty
        /// brain remains — remove it. The root deliberately has no crystal: the
        /// colony's drop contract lives on its capital segments.
        /// </summary>
        protected override void OnDeath(string killerName = "")
        {
            StopAllCoroutines();
            Destroy(gameObject);
        }
    }
}
