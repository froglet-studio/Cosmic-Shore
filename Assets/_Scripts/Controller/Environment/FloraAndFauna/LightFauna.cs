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

        // NOTE: bleeding-edge's frame-paced grazing queue (maxConsumesPerFrame /
        // _pendingMeals / EatPrism / DrainPendingMeals) is intentionally NOT carried
        // here. The intentional-feeding model below supersedes it for both diets: the
        // herbivore eats bounded mouthfuls (maxClusterBites) spaced by the facing hold,
        // and the predator devours prey at the mouth — both already frame-bounded, so
        // the burst-pacer had nothing left to pace. (Boid's drone path keeps the queue
        // for its combat Damage cascade.)

        private Vector3 currentVelocity;

        /// <summary>Live travel speed - the joust's "must be moving faster" comparison reads this.</summary>
        public override float CurrentSpeed => currentVelocity.magnitude;
        private Vector3 desiredDirection;
        private Quaternion desiredRotation;

        // True once death has begun the wither animation. Freezes movement/behavior so the
        // husk withers in place instead of drifting, and makes the death path idempotent.
        bool _withering;

        // --- Intentional feeding (herbivore) -------------------------------
        // The behavior tick picks the nearest edible prism; per-frame code then
        // approaches to consumeRadius, turns to FACE it before the suction starts,
        // and holds facing until the suction shader has pulled the mouthful in.
        Prism _feedTarget;          // meal chosen by the behavior tick
        Vector3 _feedFocusPoint;    // where the creature keeps looking while consuming
        float _feedHoldUntil = -1f; // facing hold until the suction completes
        float _consumeRadius;       // cached each tick (aggression-scaled) for mouthful chaining
        float _consumeRadiusSqr;    // squared companion for the per-frame gate

        // --- Hunting (predator) ---------------------------------------------
        Fauna _targetPrey;          // prey being pursued (refreshed each behavior tick)
        Transform _mouth;           // suction sink at the danger-prism centroid
        Vector3 _territoryAnchor;   // this predator's den — fixed at spawn (tiger-shark territoriality)
        bool _hasTerritory;
        float _huntCycleAnchor;     // hunt-pulse clock zero — set at Initialize so the cycle starts RESTING

        /// <summary>
        /// True while this predator is alive and inside its hunt window — the
        /// aggression state, exposed for presentation (the jaw rig opens the mouth
        /// while hunting). False for herbivores, withering bodies, and rest stretches.
        /// </summary>
        public bool IsActivelyHunting =>
            diet == FaunaDiet.Predator && !_withering && data && IsHuntWindow;

        // ── Subclass hooks ───────────────────────────────────────────────────
        // A species with a behaviour the base does not have (the swordfish's vessel strike)
        // layers it on top of the ordinary predator instead of forking the class: it reads
        // the same data, rides the same behavior tick, and may take the BODY for a frame.
        // Nothing here changes what a plain LightFauna does.

        /// <summary>The species data - read-only to subclasses; tuning stays on the asset.</summary>
        protected LightFaunaDataSO Data => data;

        /// <summary>True once death has begun the wither; movement and behaviour are frozen.</summary>
        protected bool IsWithering => _withering;

        /// <summary>The integrated velocity. A subclass that owns steering writes it directly.</summary>
        protected Vector3 CurrentVelocity
        {
            get => currentVelocity;
            set => currentVelocity = value;
        }

        /// <summary>The facing the per-frame rotation lerp chases.</summary>
        protected Quaternion DesiredRotation
        {
            get => desiredRotation;
            set => desiredRotation = value;
        }

        /// <summary>
        /// True while a subclass owns the body: the behavior tick then skips its own velocity
        /// and facing write (starvation, goal resolution and prey selection still run).
        /// </summary>
        protected virtual bool SubclassOwnsSteering => false;

        /// <summary>Called on every behavior tick of the simulating peer, after the starvation check.</summary>
        protected virtual void OnBehaviorTick() { }

        /// <summary>
        /// Per-frame movement hook on the simulating peer. Return true when the subclass wrote
        /// <see cref="CurrentVelocity"/> / <see cref="DesiredRotation"/> this frame; the base
        /// then stands aside from its own per-frame steering (feeding approach, prey homing)
        /// but keeps a predator's mouth working.
        /// </summary>
        protected virtual bool TickSubclassMovement() => false;

        /// <summary>
        /// Hunt pulses: true while this predator's periodic hunt window is open.
        /// Pure clock math (one Mathf.Repeat), no state, no coroutine. The anchor is
        /// set at Initialize so the cycle opens with the REST stretch — a freshly
        /// spawned predator cruises before its first attack (layered on the prey's
        /// own spawn immunity). interval 0 = always hunting (legacy).
        /// </summary>
        protected bool IsHuntWindow
        {
            get
            {
                float interval = data.huntIntervalSeconds;
                if (interval <= 0f) return true;
                float duration = Mathf.Min(data.huntDurationSeconds, interval);
                return Mathf.Repeat(Time.time - _huntCycleAnchor, interval) < duration;
            }
        }

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

            if (diet == FaunaDiet.Predator)
            {
                InitializeMouth();

                // Start the hunt-pulse cycle in its REST stretch: with the anchor set
                // one hunt-duration in the past, the clock sits just past the window,
                // so the first hunt opens (interval - duration) seconds from now.
                _huntCycleAnchor = Time.time - Mathf.Min(data.huntDurationSeconds, data.huntIntervalSeconds);

                // Tiger-shark territoriality: roll the den once, at spawn. Random
                // direction from the cell centre spreads concurrent predators apart
                // (the species caps at 2-3 per cell) with zero coordination cost.
                // The den stays in the HEMISPHERE the predator spawned in (each pole
                // of the predator spawn ring owns a hemisphere): mirror the random
                // direction if it points into the opposite half. A centre spawn
                // (legacy, no ring) has no hemisphere — the direction stands as-is.
                if (data.territoryRadius > 0f)
                {
                    Vector3 centre = cell ? cell.transform.position : transform.position;
                    Vector3 denDirection = Random.onUnitSphere;
                    Vector3 spawnDirection = transform.position - centre;
                    if (Vector3.Dot(denDirection, spawnDirection) < 0f)
                        denDirection = -denDirection;
                    _territoryAnchor = centre + denDirection * data.territoryAnchorDistance;
                    _hasTerritory = true;
                }
            }

            float minSpeed = Mathf.Max(0f, data.minSpeed);
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed);

            currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
            StartCoroutine(UpdateBehaviorCoroutine());
        }

        /// <summary>
        /// Death = wither (or devour), never a pop. Starvation and a joust both WITHER — the
        /// soft tissue evaporates spindle by spindle and the body prisms are left behind as a
        /// skeleton (mass conserved) — differing only in the direction the wither travels; a
        /// predation death with a devour target instead BREAKS the body apart and suctions it
        /// into the predator's mouth, because there the mass transfers to the eater. Every
        /// exit FADES out of existence rather than vanishing (the platform-wide continuity
        /// rule). Only the husk is removed, after the body is gone.
        /// </summary>
        protected override void OnDeath(string killerName = "")
        {
            if (_withering) return;
            _withering = true;
            currentVelocity = Vector3.zero;

            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                StartCoroutine(DevourTarget ? DevouredCoroutine(DevourTarget, killerName) : WitherCoroutine());
            else
                RemoveHusk(); // can't animate while inactive (scene teardown) - remove directly
        }

        /// <summary>
        /// This creature's heart waits for its wither: an outside-in death spends the body
        /// from the extremities and the heart is the LAST thing standing, so it only becomes
        /// collectable once the wither reaches it. <see cref="Fauna.Die"/> still releases it
        /// outright for every other style, and <see cref="RemoveHusk"/> is the terminal that
        /// guarantees it either way.
        /// </summary>
        protected override bool DefersHeartRelease => true;

        void RemoveHusk()
        {
            // Terminal for every LightFauna death path — the guaranteed release point for a
            // deferred heart (an interrupted wither must not swallow the crystal). Idempotent.
            ReleaseHeart();

            // A REPLICATED creature is removed by its owner: the server despawns after a grace
            // that covers clients whose wither started ~RTT later, and a client does nothing at
            // all (destroying a replicated NetworkObject locally is an NGO error). Unnetworked
            // fauna fall straight through to the legacy removal below.
            if (DespawnOrDestroy())
            {
                if (LightFaunaManager) LightFaunaManager.Deregister(this);
                return;
            }

            if (LightFaunaManager)
                LightFaunaManager.RemoveFauna(this);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// Withers the soft tissue one spindle ring at a time and leaves the body prisms
        /// standing as a skeleton (Docs/ECOSYSTEM.md §26). The DIRECTION is the death itself,
        /// and the two are exact mirrors around the heart:
        ///
        ///   • starvation (and any ordinary death) travels FARTHEST-FROM-THE-HEART FIRST — a
        ///     shark's fins / a brittlestar's arms evaporate before the core body, emergent
        ///     from geometry with no per-prefab special-casing — and the heart, the last thing
        ///     left, becomes collectable by ANY vessel when the wither finally reaches it.
        ///   • a joust travels NEAREST-THE-HEART FIRST: the jouster already took the heart, so
        ///     the body comes apart around the hole it left and unravels outward.
        ///
        /// Both reuse the same <see cref="Spindle.ForceWither"/> evaporation flora use, and
        /// both honor the continuity rule — the tissue fades, the frame stays.
        /// </summary>
        IEnumerator WitherCoroutine()
        {
            float interval = data && data.witherRingInterval > 0f ? data.witherRingInterval : 0.25f;
            bool fromHeartOutward = DeathStyle == LifeformDeathStyle.Jousted;

            // Where the wither is measured from. Captured NOW: a jousted heart has already
            // been freed and is flying to the pilot who took it, so reading it later would
            // sort the rings against a moving point.
            Vector3 heart = crystal ? crystal.transform.position : transform.position;

            // Isolate before anything else — withering one spindle must not cascade into its
            // children or destroy them with its GameObject, and handing its prisms to the
            // skeleton must not evaporate it out of turn. See Spindle.IsolateForOrderedWither.
            var spindles = GetComponentsInChildren<Spindle>(true).Where(s => s).ToList();
            for (int i = 0; i < spindles.Count; i++)
                spindles[i].IsolateForOrderedWither(transform);

            // The frame stays in the world. Must precede the wither: a body prism is parented
            // to a spindle, so evaporating spindles first would destroy the skeleton's mass.
            LeaveSkeleton();

            spindles = fromHeartOutward
                ? spindles.OrderBy(s => (s.transform.position - heart).sqrMagnitude).ToList()
                : spindles.OrderByDescending(s => (s.transform.position - heart).sqrMagnitude).ToList();

            // Stamp every spindle in this one pass. The distance sort above is the
            // ecology-LOCKED order; the offset is when that spindle's fade STARTS.
            // Do not WaitForSeconds between ForceWither calls — that was the per-frame
            // cascade C11 retires.
            for (int i = 0; i < spindles.Count; i++)
                if (spindles[i]) spindles[i].ForceWither(i * interval);

            // Heart stays uncollectable until the wither has reached the core — same
            // instant as the old path (count * interval after the first stamp, including
            // the wait that used to follow the LAST ForceWither).
            float coreDelay = spindles.Count * interval;
            if (coreDelay > 0f) yield return new WaitForSeconds(coreDelay);
            ReleaseHeart();

            // Let the last ring finish evaporating before the husk goes — destroying the root
            // takes any still-fading spindle with it, which is a pop. Spindles destroy
            // themselves when their fade completes; the deadline means a stalled fade can
            // never leak an immortal husk (same guard the worm colony uses).
            float deadline = Time.time + 5f;
            while (Time.time < deadline && GetComponentInChildren<Spindle>(true))
                yield return null;

            RemoveHusk();
        }

        /// <summary>
        /// Predation exit: the body BREAKS APART — every live body prism detaches and
        /// suctions (implodes) into the predator's mouth, nearest-to-mouth first — while
        /// the residual structure (spindles) evaporates via their CheckForLife as the
        /// prisms leave. Continuity rule: pulled into the mouth, never popped. The sealed
        /// <see cref="Fauna.Die"/> has already dropped the elemental crystal (mass conserved).
        /// </summary>
        IEnumerator DevouredCoroutine(Transform mouth, string predatorName)
        {
            if (string.IsNullOrEmpty(predatorName)) predatorName = "predator";
            Vector3 mouthPos = mouth ? mouth.position : transform.position;

            var prisms = GetComponentsInChildren<HealthPrism>(true)
                .Where(p => p && !p.destroyed)
                .OrderBy(p => (p.transform.position - mouthPos).sqrMagnitude)
                .ToList();

            // A few bites per frame: fast enough to read as a strike, staggered enough
            // to smooth the implosion-VFX burst for large bodies.
            const int BitesPerFrame = 4;
            for (int i = 0; i < prisms.Count; i++)
            {
                var p = prisms[i];
                if (p && !p.destroyed)
                    p.Consume(mouth ? mouth : transform, domain, predatorName, true, true);
                if ((i + 1) % BitesPerFrame == 0) yield return null;
            }

            // Give the spindle evaporation kicked off by the prism removals a beat to
            // play before the husk goes (mirrors WitherCoroutine's trailing interval).
            yield return new WaitForSeconds(0.35f);
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
        /// Live, non-immune herbivore from the host cell's fauna registry. Territorial
        /// (tiger-shark) mode: only prey inside the territory counts, and the pick is the
        /// one closest to the DEN — the predator works its own patch instead of racing
        /// other predators to the same school. Non-territorial: nearest to self (legacy).
        /// O(live fauna) per behavior tick either way — the registry is small (bounded by
        /// the per-species MaxLivePopulation caps) and this is one sqrMagnitude per entry.
        /// </summary>
        Fauna FindNearestPreyFauna()
        {
            var host = cell;
            if (host == null) return null;

            Vector3 origin = _hasTerritory ? _territoryAnchor : transform.position;
            float maxSqr = _hasTerritory
                ? data.territoryRadius * data.territoryRadius
                : float.PositiveInfinity;

            var fauna = host.LiveFauna;
            Fauna best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (!f || f == this) continue;
                if (f.Diet != FaunaDiet.Herbivore || !f.IsAlivePrey || f.IsPredationImmune) continue;

                float d = (f.transform.position - origin).sqrMagnitude;
                if (d < bestSqr && d <= maxSqr) { bestSqr = d; best = f; }
            }

            return best;
        }

        /// <summary>
        /// Creates the mouth — a lightweight transform at the danger-prism centroid.
        /// The mouth is deliberately NOT a danger prism's own transform: the suction
        /// sink must keep tracking the swimming predator even if players destroy the
        /// mouth prisms mid-devour (danger prisms stay fully vulnerable to normal
        /// prism destruction).
        /// </summary>
        void InitializeMouth()
        {
            var body = BodyPrisms;
            int dangerCount = 0;
            Vector3 centroid = Vector3.zero;

            if (body != null)
            {
                for (int i = 0; i < body.Length; i++)
                {
                    var hp = body[i];
                    if (!hp || hp.prismProperties == null || !hp.prismProperties.IsDangerous) continue;
                    centroid += hp.transform.position;
                    dangerCount++;
                }
            }

            var mouthGO = new GameObject("Mouth");
            mouthGO.transform.SetParent(transform, false);
            if (dangerCount > 0)
                mouthGO.transform.position = centroid / dangerCount;
            _mouth = mouthGO.transform;
        }

        void UpdateBehavior()
        {
            if (!data || _withering)
                return;

            // A replicated puppet takes the GRAZING half of this tick and none of the
            // deciding half: no starvation, no steering, no goal, no predation. Grazing is
            // kept deliberately — fauna consumption is the only legal down-force on mass
            // (mass is conserved, nothing decays), so a client whose creatures ate nothing
            // would accumulate prisms with no sink at all, and the perf win fauna exist for
            // would land on the host alone.
            if (!IsSimAuthority)
            {
                UpdatePuppetGraze();
                return;
            }

            // Prey-linked population control: a fauna that hasn't fed in starvationSeconds
            // despawns, so the live population self-bounds to available prey (Docs/ECOSYSTEM.md §6).
            if (IsStarving)
            {
                Die(StarvationKiller);
                return;
            }

            OnBehaviorTick();

            Vector3 separation = Vector3.zero;

            // Phase-driven goal. Each phase swaps the goal source rather than killing/spawning
            // systems, so the same fauna instance can transition through aggression levels
            // as the cell's phase changes around it.
            //   Calm:     aggression 0 - head toward crystal
            //   Restless: aggression 1 - head toward nearest opposing-color centroid
            //   Frenzy:   aggression 2 - head toward nearest centroid (any domain)
            // GoalOrbitOffset at Calm/Restless, none at Frenzy — the same split
            // Fauna.ResolveGoal uses. This tick RECOMPUTES Goal on its own cadence, so
            // omitting the offset here silently clobbered the one the goal coroutine
            // applied: every creature then sought the identical centroid (or the
            // crystal), arrived, and its goal direction degenerated to zero — see the
            // degenerate-steering guard below.
            var phase = cell ? cell.Phase : CellPhase.Calm;
            Goal = phase switch
            {
                CellPhase.Restless => cell.GetExplosionTarget(domain) + GoalOrbitOffset,
                CellPhase.Frenzy => cell.GetDensestRegionAnyDomain(),
                _ => ((cellData && cellData.CrystalTransform)
                       ? cellData.CrystalTransform.position
                       : (cell ? cell.transform.position : transform.position)) + GoalOrbitOffset,
            };

            // Voracious exterior: with a nucleus control zone, mass outside the
            // nucleus is prey at EVERY phase - even a Calm herbivore hunts the
            // densest sensed exterior region instead of idling at the crystal
            // (the grids only hold exterior mass in such cells; aggression still
            // scales cadence/radius/speed).
            if (phase == CellPhase.Calm && cell != null &&
                cell.HasNucleusControlZone && cell.HasSensedExteriorMass)
                Goal = cell.GetDensestRegionAnyDomain() + GoalOrbitOffset;

            // Centre focus (per-deployment, FaunaConfigurationSO.CenterFocusBias): pull
            // the herbivore's roaming goal toward the cell centre so it lingers on the
            // central canopy (the gyroids around the nucleus). Edibility is untouched —
            // a nucleus claim stays protected. One lerp per tick; 0 = off.
            if (diet == FaunaDiet.Herbivore && cell != null &&
                SourceConfig && SourceConfig.CenterFocusBias > 0f)
                Goal = Vector3.Lerp(Goal, cell.transform.position, SourceConfig.CenterFocusBias);

            // Predators hunt PREY, not mass: seek the nearest live herbivore the cell
            // senses (Cell.LiveFauna - the fauna analogue of the prism density grid).
            // Replaces the v1 approximation where predators converged on prism-density
            // centroids and only met herbivores incidentally. Skips predation-immune
            // newborns so a shark doesn't camp a fresh birth; with no herbivores alive
            // the phase-based goal above stands (roam plausibly, then starve). The
            // target is HELD as a reference: UpdateHunting homes on its live position
            // every frame between ticks, and the mouth check devours it in range.
            if (diet == FaunaDiet.Predator)
            {
                // Hunt pulses: outside the window the predator carries no target — it
                // cruises its territory without pursuing or feeding, guaranteeing the
                // herbivores grazing time between attacks.
                _targetPrey = IsHuntWindow ? FindNearestPreyFauna() : null;
                if (_targetPrey) Goal = _targetPrey.transform.position;
                // Empty patch (or resting): patrol the den instead of roaming the shared
                // density goal — a territorial predator stays out of other predators'
                // territories, so a distant herbivore group faces at most the one shark
                // whose patch it's in.
                else if (_hasTerritory) Goal = _territoryAnchor;
            }

            if (!IsFinite(Goal) || Goal.sqrMagnitude < 0.001f)
            {
                // Offset here too: this fallback fires when the resolved goal lands on the
                // world ORIGIN, which in an origin-centred cell is exactly where the whole
                // pack would otherwise pile up.
                Goal = (cellData && cellData.CrystalTransform
                    ? cellData.CrystalTransform.position
                    : cell.transform.position) + GoalOrbitOffset;
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
            _consumeRadius = consumeRadius;       // per-frame feeding gate + mouthful
            _consumeRadiusSqr = consumeRadiusSqr; // chaining read the tick's values

            // Intentional feeding: the tick SELECTS the nearest edible prism; the actual
            // approach → face → suction sequence runs per-frame in UpdateFeeding.
            Prism feedCandidate = null;
            float bestFeedSqr = float.PositiveInfinity;

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
                        // Prey bodies never repel a hunting predator — a shark doesn't
                        // avoid its dinner. Environment prisms (flora / trails / other
                        // predators) keep their separation push below, so the predator
                        // still maneuvers around obstacles to reach its prey. The actual
                        // kill is mouth-driven: TryDevourPreyAtMouth (per-frame) devours
                        // prey within a danger-prism length of the mouth.
                        neighborCount++;
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

                    // Herbivore: one edibility check per prism decides BOTH roles —
                    // FOOD ATTRACTS AND NEVER REPELS (edible prisms are feed candidates,
                    // exempt from separation), while non-edible mass (own canopy, the
                    // nucleus claim, fauna bodies) keeps pushing us away. Flora are
                    // HealthPrisms, so before this split a brittlestar's own food
                    // repelled it from separationRadius (70) out while feeding needed
                    // consumeRadius (40) — approach geometry decided whether it ever ate,
                    // which read as "swims past a lot of mass before feeding".
                    // (The diet rule is spatialized through Cell.IsPreyForHerbivore —
                    // see IsEdibleForHerbivore. Intentional feeding: don't vacuum;
                    // remember the NEAREST edible prism, UpdateFeeding approaches it,
                    // turns to face it, and only then starts the suction.)
                    if (diet == FaunaDiet.Herbivore && IsEdibleForHerbivore(prism))
                    {
                        if (sqr < bestFeedSqr)
                        {
                            bestFeedSqr = sqr;
                            feedCandidate = prism;
                        }
                        continue;
                    }

                    bool sameDomain = otherHealthBlock.LifeForm && otherHealthBlock.LifeForm.domain == domain;

                    if (sqr < separationRadiusSqr && !(dropFriendlyAvoidance && sameDomain))
                        separation += diff / sqr;

                    continue;
                }

                // Handle blocks (trail prisms) — same spatialized diet rule as above
                // (plain prisms never contributed separation here, so only the
                // feed-candidate role applies).
                if (diet == FaunaDiet.Herbivore && sqr < bestFeedSqr && IsEdibleForHerbivore(prism))
                {
                    bestFeedSqr = sqr;
                    feedCandidate = prism;
                }
            }

            // Herbivore intent: adopt the tick's nearest edible prism as the feed target
            // (unless mid-suction-hold) and beeline for it — the visible approach is what
            // makes the grazing read as deliberate.
            if (diet == FaunaDiet.Herbivore)
            {
                if (!IsFeedingHold)
                    _feedTarget = feedCandidate;
                if (_feedTarget && !_feedTarget.destroyed)
                    goalDirection = (_feedTarget.transform.position - transform.position).normalized;
            }

            averageSpeed = neighborCount > 0
                ? (averageSpeed > 0 ? averageSpeed / neighborCount : currentVelocity.magnitude)
                : currentVelocity.magnitude;

            float separationWeight = Mathf.Max(0f, data.separationWeight);
            float goalWeight = Mathf.Max(0f, data.goalWeight);

            // While feeding owns the body (hovering at the meal, turning to face it,
            // holding through the suction), don't overwrite its velocity/rotation with
            // the steering result — steering resumes when the mouthful is gone.
            if (IsFeedingEngaged || SubclassOwnsSteering)
                return;

            // Degenerate steering is a PERMANENT stall, so it must never reach the
            // velocity. `Vector3.normalized` returns ZERO for a ~zero vector, which zeroes
            // currentVelocity; the creature then stops moving, so the next tick recomputes
            // the identical zero from the identical position and it is frozen for life
            // (it just hangs there until it starves). Two ways in: the creature reaches
            // its goal exactly (goalDirection → zero), or separation cancels the goal pull.
            // This is what parked brittlestars against Skim Race's track — the crystal they
            // seek at Calm sits on the track, and once shielded prisms stopped being valid
            // feed targets there was no _feedTarget left to override goalDirection. Keep
            // the last heading instead: swimming on always re-acquires a goal.
            Vector3 steering = (separation * separationWeight) + (goalDirection * goalWeight);
            if (steering.sqrMagnitude > DegenerateSteeringSqr)
                desiredDirection = steering.normalized;
            else if (desiredDirection.sqrMagnitude <= DegenerateSteeringSqr)
                desiredDirection = transform.forward;

            float speedMult = GetAggressionSpeedMultiplier();
            // Active pursuit: a predator with live prey in its sights closes noticeably
            // faster than it cruises.
            if (diet == FaunaDiet.Predator && _targetPrey)
                speedMult *= Mathf.Max(1f, data.pursuitSpeedMultiplier);
            float minSpeed = Mathf.Max(0f, data.minSpeed) * speedMult;
            float maxSpeed = Mathf.Max(minSpeed, data.maxSpeed * speedMult);

            currentVelocity = desiredDirection * Mathf.Clamp(averageSpeed, minSpeed, maxSpeed);

            if (currentVelocity != Vector3.zero && SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                desiredRotation = rotation;
            else
                desiredRotation = transform.rotation;
        }

        // --- Intentional feeding (herbivore, per-frame) ----------------------

        /// <summary>True while holding position/facing until the current mouthful's suction completes.</summary>
        bool IsFeedingHold => Time.time < _feedHoldUntil;

        /// <summary>
        /// True while feeding owns movement: either mid-suction-hold, or hovering inside
        /// the minimum feeding distance of a live target while turning to face it.
        /// </summary>
        bool IsFeedingEngaged =>
            IsFeedingHold ||
            (_feedTarget && !_feedTarget.destroyed &&
             (_feedTarget.transform.position - transform.position).sqrMagnitude <= _consumeRadiusSqr);

        /// <summary>
        /// Same edibility rule the old inline consume used: HealthPrisms must belong to a
        /// LifeForm (fauna bodies carry none — herbivores never eat creatures), and the
        /// diet is spatialized through Cell.IsPreyForHerbivore (nucleus interior = the
        /// territorial claim, never consumed; exterior = voracious any-domain; no nucleus
        /// = legacy opposing-domain rule). Shielded and super-shielded mass is never food
        /// (Fauna.IsShieldedMass) — targeting one is a feed-hold the creature can never
        /// finish, which is what stuck brittlestars on Skim Race's track prisms.
        /// </summary>
        bool IsEdibleForHerbivore(Prism prism)
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
        /// Per-frame feeding sequence: approach (steering) → arrive inside consumeRadius
        /// (the minimum feeding distance — the creature never needs to be right on the
        /// prisms) → brake to a hover and TURN toward the meal → once facing it, start
        /// the suction (ConsumeMouthful) → hold facing until the suction shader has
        /// pulled the prisms entirely in.
        /// </summary>
        void UpdateFeeding()
        {
            if (IsFeedingHold)
            {
                FaceFeedFocus();
                BrakeToHover();
                return;
            }

            // A suction hold just finished: CHAIN straight to the next edible prism
            // still inside feeding range (one small index query per completed
            // mouthful), so a creature parked at a buildup keeps eating mouthful
            // after mouthful instead of drifting until the next behavior tick
            // re-targets — it feeds more than it swims. Resumes roaming only when
            // the local patch is actually clear.
            if (_feedHoldUntil > 0f)
            {
                _feedHoldUntil = -1f;
                if (!_feedTarget || _feedTarget.destroyed)
                    _feedTarget = FindNearestEdibleInFeedingRange();
            }

            var target = _feedTarget;
            if (!target || target.destroyed)
            {
                _feedTarget = null;
                return;
            }

            Vector3 toTarget = target.transform.position - transform.position;
            if (toTarget.sqrMagnitude > _consumeRadiusSqr)
                return; // still approaching — the behavior tick's steering owns movement

            _feedFocusPoint = target.transform.position;
            FaceFeedFocus();
            BrakeToHover();
            if (Vector3.Angle(transform.forward, toTarget) <= data.feedingFacingAngle)
                ConsumeMouthful(target);
        }

        /// <summary>
        /// The client puppet's whole behavior: eat what is already within reach on THIS peer.
        /// It reuses the authority path's own mouthful — same diet predicates, same cluster
        /// query, same suction — so there is no second copy of the diet rules to drift.
        /// A PREDATOR puppet does nothing: which creature gets eaten is a decision, and the
        /// server's <see cref="Fauna.Predated"/> already replicates the result.
        /// </summary>
        void UpdatePuppetGraze()
        {
            if (diet != FaunaDiet.Herbivore) return;

            // UpdateBehavior normally publishes these; the puppet skips it, so set the same
            // aggression-scaled radius here rather than feeding against a stale zero.
            _consumeRadius = Mathf.Max(0f, data.consumeRadius) * GetAggressionConsumeRadiusMultiplier();
            _consumeRadiusSqr = _consumeRadius * _consumeRadius;

            if (_feedHoldUntil > Time.time) return;

            var target = FindNearestEdibleInFeedingRange();
            if (target) ConsumeMouthful(target);
        }

        /// <summary>
        /// One deliberate bite: suction the faced target plus edible prisms clustered
        /// around it toward this creature (the suction sink tracks us), then hold facing
        /// for consumeHoldSeconds so the creature watches its meal all the way in. One
        /// small index query per BITE — not per behavior tick.
        /// </summary>
        void ConsumeMouthful(Prism target)
        {
            _feedFocusPoint = target.transform.position;
            _feedHoldUntil = Time.time + data.consumeHoldSeconds;
            _feedTarget = null;

            int bites = 0;
            if (IsEdibleForHerbivore(target))
            {
                target.Consume(transform, domain, PLAYER_NAME, true, true);
                NotifyFed();
                bites++;
            }

            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int found = spatialIndex != null && spatialIndex.IsAvailable && data.feedingClusterRadius > 0f
                ? spatialIndex.QuerySphere(_feedFocusPoint, data.feedingClusterRadius, FeedScratch)
                : 0;
            for (int i = 0; i < found && bites < data.maxClusterBites; i++)
            {
                var prism = FeedScratch[i];
                if (prism == target || !IsEdibleForHerbivore(prism)) continue;
                prism.Consume(transform, domain, PLAYER_NAME, true, true);
                NotifyFed();
                bites++;
            }

            // Someone else got the whole mouthful first — nothing to watch, resume roaming.
            if (bites == 0)
                _feedHoldUntil = 0f;
        }

        /// <summary>
        /// Nearest edible prism within the (aggression-scaled) consume radius — the
        /// mouthful-chaining query. Bounded and infrequent: one QuerySphere per
        /// COMPLETED mouthful (every consumeHoldSeconds at most), never per frame.
        /// </summary>
        Prism FindNearestEdibleInFeedingRange()
        {
            if (_consumeRadius <= 0f) return null;
            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            if (spatialIndex == null || !spatialIndex.IsAvailable) return null;

            int found = spatialIndex.QuerySphere(transform.position, _consumeRadius, FeedScratch);
            Prism best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < found; i++)
            {
                var prism = FeedScratch[i];
                if (!IsEdibleForHerbivore(prism)) continue;
                float d = (prism.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = prism; }
            }
            return best;
        }

        void FaceFeedFocus()
        {
            if (SafeLookRotation.TryGet(_feedFocusPoint - transform.position, out var rotation, this))
                desiredRotation = rotation;
        }

        void BrakeToHover()
        {
            currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero,
                Mathf.Clamp01(Time.deltaTime * data.feedingBrakeSharpness));
        }

        // --- Hunting (predator, per-frame) -----------------------------------

        /// <summary>
        /// Per-frame pursuit: home the velocity onto the LIVE prey position between
        /// behavior ticks so the chase tracks a fleeing target (the tick's full steering
        /// — separation from environment prisms, i.e. obstacle avoidance — still applies
        /// each tick), then devour any prey within a danger-prism length of the mouth.
        /// </summary>
        void UpdateHunting()
        {
            // Hunt pulses: the window can close mid-chase — break off immediately.
            // While resting the predator neither homes nor devours, so even prey
            // swimming straight into its jaws survives until the next window.
            if (!IsHuntWindow)
            {
                _targetPrey = null;
                return;
            }

            if (_targetPrey && !_targetPrey.IsAlivePrey)
                _targetPrey = null;

            var prey = _targetPrey;
            if (prey)
            {
                Goal = prey.transform.position;
                Vector3 toPrey = Goal - transform.position;
                float speed = currentVelocity.magnitude;
                if (speed > 0.01f && toPrey.sqrMagnitude > 0.01f)
                {
                    currentVelocity = Vector3.Slerp(currentVelocity / speed, toPrey.normalized,
                        Mathf.Clamp01(Time.deltaTime * data.pursuitAgility)) * speed;
                    if (SafeLookRotation.TryGet(currentVelocity, out var rotation, this))
                        desiredRotation = rotation;
                }
            }

            TryDevourPreyAtMouth();
        }

        /// <summary>
        /// The attack: any live, non-immune herbivore whose root comes within
        /// data.attackRange of the mouth (danger-prism centroid) is devoured — it breaks
        /// apart and its prisms suction into the mouth (Predated with a devour target).
        /// Pure math against the cell's small fauna registry, every frame: no physics,
        /// no contact — so the kill is deterministic ("flawless") and the danger prisms
        /// are never disturbed by the prey being eaten, while staying fully vulnerable
        /// to vessels and projectiles. Handles a whole school swimming into the mouth.
        /// </summary>
        void TryDevourPreyAtMouth()
        {
            float attackRangeSqr = data.attackRange * data.attackRange;
            if (!_mouth || attackRangeSqr <= 0f) return;
            var host = cell;
            if (host == null) return;

            Vector3 mouthPos = _mouth.position;
            var fauna = host.LiveFauna;
            for (int i = 0; i < fauna.Count; i++)
            {
                var f = fauna[i];
                if (!f || f == this || f.Diet != FaunaDiet.Herbivore) continue;
                if (!f.IsAlivePrey || f.IsPredationImmune) continue;
                if ((f.transform.position - mouthPos).sqrMagnitude > attackRangeSqr) continue;

                // Predated() respects the prey's post-spawn immunity window and returns
                // false if the prey couldn't be eaten — only feed on a real kill.
                if (f.Predated(PLAYER_NAME, _mouth))
                    NotifyFed();
            }
        }

        void Update()
        {
            // A puppet's pose belongs to its server-authoritative NetworkTransform: integrating
            // a local velocity on top would fight the replicated stream. The MOVERS CONTRACT
            // still applies on every peer though — the body prisms are registered, moving mass,
            // so their index entries must follow the replicated transform or this peer's AOE
            // and fauna senses would target the spawn point forever.
            if (!IsSimAuthority)
            {
                NotifyBodyPrismsMoved();
                return;
            }

            transform.position += currentVelocity * Time.deltaTime;
            // Movers contract: the body prisms are registered mass - keep their
            // stored index positions tracking the swimming creature.
            NotifyBodyPrismsMoved();

            if (!_withering && data)
            {
                // A subclass may take the body for a frame (the swordfish's vessel strike): it
                // has written velocity and facing itself, so the base steering stands aside —
                // but a predator's mouth keeps working, so a lunge that runs through prey eats it.
                bool driven = TickSubclassMovement();
                if (diet == FaunaDiet.Herbivore)
                {
                    if (!driven) UpdateFeeding();
                }
                else if (diet == FaunaDiet.Predator)
                {
                    if (driven) TryDevourPreyAtMouth();
                    else UpdateHunting();
                }
            }

            float lerpSpeed = data ? Mathf.Max(0f, data.rotationLerpSpeed) : 5f;
            var t = Mathf.Clamp(Time.deltaTime * lerpSpeed, 0f, 0.99f);

            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, t);
        }

        static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
