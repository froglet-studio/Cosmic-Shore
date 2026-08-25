using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The worm colony brain — the kaiju boss (Docs/ECOSYSTEM.md §23). A connected
    /// population of three segment fauna types (<see cref="WormSegmentFauna"/>:
    /// Head / Body / Tail) driven by one coordinator so the whole chain costs one
    /// behavior tick and one movement pass, however long it grows.
    ///
    /// The fight, expressed through the fundamentals:
    ///  • The HEAD is the mouth: it grazes prism mass voraciously (canonical
    ///    herbivore diet rules), and feeding is the ONLY source of new segments —
    ///    length is a record of what the colony has eaten (mass conserved).
    ///  • Danger prisms on head and tail make the ends the threat surface (the
    ///    standard domain-blind danger effect chain — no bespoke damage code).
    ///  • Killing a BODY segment SPLITS the worm in two — both halves regrow their
    ///    missing ends by DIFFERENTIATING the wound segment (a state change of
    ///    existing mass), so mid-body kills multiply the problem.
    ///  • Optimal play emerges: chain end-kills faster than EndRegrowSeconds and you
    ///    always face soft tissue; slower and every kill is armored. Starve it
    ///    (deny mass) and it digests itself tail-first.
    ///  • Souls-like attack grammar: rest pulses → readable telegraph coil → locked
    ///    lunge (dodgeable) → straightened recovery (the punish window), plus a
    ///    tail-whip against loiterers. Contact damage is entirely the danger
    ///    prisms' existing impact pipeline.
    ///
    /// The colony root is lineage-registered (spawner pipeline, live-count, cleanup)
    /// but carries no heart — the colony's crystal contract lives on its capital
    /// segments, which drop their hearts when killed. The root is classified
    /// Predator so nothing in the food web preys on it (nothing eats a kaiju), and
    /// <see cref="Predated"/> is sealed off — the segments are the killable surface.
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

        // Wound clocks (-1 = no wound). Set when an end dies or a split severs the
        // chain; cleared when the wound differentiates into a new capital segment.
        float _headWoundedAt = -1f;
        float _tailWoundedAt = -1f;

        // Feeding bank toward the next body segment (config.FeedsPerSegment).
        int _feedsBanked;
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

        /// <summary>The element wounds differentiate into: the config pick when present, else the tuning default.</summary>
        Element RegrowElement =>
            _pickedHeartElement != Element.None ? _pickedHeartElement : config.RegrownEndElement;

        /// <summary>
        /// Element-as-data for a COMPOSITE creature: the colony root stays heartless
        /// (its crystal contract lives on its capital segments — Docs/ECOSYSTEM.md
        /// §21), so a per-element species config (the Lifeform Matrix toy, an
        /// element-authored SpawnProfile entry) forwards its pick to the head/tail
        /// hearts instead of provisioning a crystal on this empty anchor. Remembered
        /// so wound differentiation regrows hearts of the same element.
        /// </summary>
        protected override void ProvisionHeart(Element element)
        {
            _pickedHeartElement = element;
            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                if (seg && seg.Role != WormSegmentRole.Body)
                    seg.ReprovisionHeart(element);
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
        /// Rest distance between segments[linkIndex-1] and segments[linkIndex]: the body
        /// gap, tapered down the chain, widened behind the HEAD (it needs room —
        /// HeadGapMultiplier) and into the TAIL (TailGapMultiplier). Ratios recovered
        /// from the 2024 chain layout.
        /// </summary>
        float RestSpacing(int linkIndex)
        {
            float s = config.SegmentSpacing * config.KaijuScale
                      * Mathf.Pow(config.TaperPerSegment, linkIndex - 1);
            if (segments[linkIndex - 1].Role == WormSegmentRole.Head) s *= config.HeadGapMultiplier;
            if (segments[linkIndex].Role == WormSegmentRole.Tail) s *= config.TailGapMultiplier;
            return s;
        }

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
                    pos -= dir * RestSpacingForBuild(i, i + 1 == count - 1);
            }
        }

        // BuildChain-time spacing: RestSpacing reads roles from the live list, but during
        // the build the NEXT segment doesn't exist yet — same math, roles known up front.
        float RestSpacingForBuild(int prevIndex, bool nextIsTail)
        {
            float s = config.SegmentSpacing * config.KaijuScale
                      * Mathf.Pow(config.TaperPerSegment, prevIndex);
            if (prevIndex == 0) s *= config.HeadGapMultiplier;
            if (nextIsTail) s *= config.TailGapMultiplier;
            return s;
        }

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
        /// Parent scale is mover-contract (C6 (b), 2026-08-25) — the scale twin of
        /// <see cref="FollowChain"/>. <c>Update</c> already
        /// <c>SyncBodyPrismsToIndex</c> after this; no extra notify.
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
                TickGrowth();
                TickDifferentiation();
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
        /// Boid separation from other colonies: each neighbour pushes this worm's head
        /// away from ITS NEAREST SEGMENT, inverse-square weighted, so two kaiju sharing
        /// a cell swim around each other instead of interpenetrating. Colonies are
        /// lineage-registered, so this is a walk of the cell's small fauna registry —
        /// no physics, no prism queries. O(colonies × segments) at the tick cadence.
        /// </summary>
        void TickSeparation()
        {
            _separation = Vector3.zero;
            if (config.ColonySeparationRadius <= 0f) return;
            var host = cell;
            if (host == null) return;

            Vector3 head = segments[0].transform.position;
            float radiusSqr = config.ColonySeparationRadius * config.ColonySeparationRadius;
            var fauna = host.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                if (fauna[i] is not WormFauna other || ReferenceEquals(other, this)) continue;
                if (!other.TryGetNearestBodyPoint(head, out var point, out float sqr)) continue;
                if (sqr > radiusSqr || sqr <= DegenerateSteeringSqr) continue;
                // Inverse-square falloff: diff/|diff|² == diff.normalized/|diff|.
                _separation += (head - point) / sqr;
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
                _feedsBanked++;
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
                {
                    NotifyFed();
                    _feedsBanked++;
                }
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
        /// Growth is feeding-funded ONLY: every FeedsPerSegment consumed prisms, one
        /// new body segment blooms in behind the head (mass in from consumption —
        /// never a clock). Capped by MaxSegmentsPerWorm (the collider-budget backstop).
        /// </summary>
        void TickGrowth()
        {
            if (_feedsBanked < config.FeedsPerSegment) return;
            if (segments.Count >= config.MaxSegmentsPerWorm) return;
            if (segments[0].Role != WormSegmentRole.Head) return;

            _feedsBanked -= config.FeedsPerSegment;

            Transform head = segments[0].transform;
            Vector3 pos = head.position
                          - head.forward * (config.SegmentSpacing * config.KaijuScale * config.HeadGapMultiplier);

            var seg = Instantiate(bodyPrefab, pos, head.rotation);
            // Blooms from zero (continuity): inserted at scale zero, the per-frame
            // GlideScales pass grows it to its taper target while its prisms run their
            // own growth stamps; the chain's follow springs absorb the insertion — no
            // bespoke make-room animation. Every downstream segment re-proportions
            // through the same glide (their taper index just shifted by one).
            AddSegmentToChain(seg, 1, Vector3.zero);
        }

        /// <summary>
        /// Wound differentiation — the head/tail "regrow". The segment at a wounded
        /// end hardens into the missing capital role once the wound is EndRegrowSeconds
        /// old AND the colony is fed (a starving worm cannot differentiate). This is
        /// a conversion of existing mass — the worm still net-shrank by the kill.
        /// </summary>
        void TickDifferentiation()
        {
            if (IsStarving) return;
            float now = Time.time;

            if (_headWoundedAt >= 0f && now - _headWoundedAt >= config.EndRegrowSeconds)
            {
                var leader = segments[0];
                if (leader.Role == WormSegmentRole.Body)
                {
                    leader.DifferentiateTo(WormSegmentRole.Head, RegrowElement);
                    _headWoundedAt = -1f;
                }
                else if (leader.Role == WormSegmentRole.Head)
                {
                    _headWoundedAt = -1f; // already resolved
                }
                // A lone TAIL leads: no tissue to differentiate — keep the wound
                // armed so the moment a Body exists it hardens. (In practice a
                // headless worm can't feed, so this resolves as starvation.)
            }

            if (_tailWoundedAt >= 0f && now - _tailWoundedAt >= config.EndRegrowSeconds)
            {
                var last = segments[segments.Count - 1];
                if (last.Role == WormSegmentRole.Body)
                {
                    last.DifferentiateTo(WormSegmentRole.Tail, RegrowElement);
                    _tailWoundedAt = -1f;
                }
                else if (last.Role == WormSegmentRole.Tail)
                {
                    _tailWoundedAt = -1f; // already resolved
                }
                // A lone HEAD holds the rear: keep the wound armed — it keeps
                // feeding, and the first Body it grows differentiates into the
                // missing Tail on the next tick (the wound is already past its
                // window). Clearing here would leave a worm that regrows to full
                // length with no tail danger prisms, ever.
            }
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
        /// A segment died (through the sealed Fauna.Die — its heart, if any, already
        /// dropped). End deaths open a wound; an interior death SPLITS the worm: the
        /// rear half becomes a new colony, and both halves start regrowing their
        /// missing ends. The last segment's death is the colony's death.
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
                // doesn't snap, and the wound clock starts.
                _leaderBaseRotation = segments[0].transform.rotation;
                _headWoundedAt = Time.time;
                if (_state == AttackState.Telegraph || _state == AttackState.Lunge)
                    SetState(AttackState.Cruise); // the striking mouth is gone
            }
            else if (index == segments.Count)
            {
                _tailWoundedAt = Time.time;
            }
            else
            {
                // Interior kill: sever. This colony keeps the front half (tail wound);
                // the rear half becomes a new worm with a head wound. Segment totals
                // are conserved — splitting punishes, it never multiplies mass.
                var rear = segments.GetRange(index, segments.Count - index);
                segments.RemoveRange(index, segments.Count - index);
                _tailWoundedAt = Time.time;
                SpawnSplitColony(rear);
            }
        }

        /// <summary>
        /// The severed rear half becomes its own colony. The new brain is a CLONE OF
        /// THIS ONE rather than a fresh prefab instantiation, which is both simpler and
        /// biologically right: the two halves of a split worm are the same animal, so
        /// the child inherits this colony's exact tuning (config, segment prefabs, diet,
        /// starvation clock) with no serialized self-reference to keep wired. The
        /// segments are not children of the root, so the clone brings no body with it —
        /// it adopts the severed half in Initialize.
        /// </summary>
        void SpawnSplitColony(List<WormSegmentFauna> rear)
        {
            if (rear == null || rear.Count == 0) return;

            var colony = Instantiate(this, rear[0].transform.position, rear[0].transform.rotation);
            colony.domain = domain;
            colony.Goal = Goal;
            colony._pendingAdoption = rear;
            colony.Initialize(cell);
            colony._headWoundedAt = Time.time;
            // Same lineage: the split registers in the cell's live count (so the
            // spawner's seed floor sees two worms) and inherits the species config.
            if (cell && SourceConfig)
                colony.AssignLineage(cell, SourceConfig);
            if (cell)
                cell.RegisterSpawnedObject(colony.gameObject);
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
