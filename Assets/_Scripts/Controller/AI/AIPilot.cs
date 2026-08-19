using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using Obvious.Soap;
using CosmicShore.Data;
using Reflex.Attributes;
using System.Linq;

namespace CosmicShore.Gameplay
{
    [Serializable]
    public class AIAbility
    {
        public ShipActionSO Ability;
        public float Duration;
        public float Cooldown;
    }

    public class AIPilot : MonoBehaviour
    {
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        [SerializeField] float skillLevel = 1;

        [SerializeField] float defaultThrottleHigh = .6f;
        [SerializeField] float defaultThrottleLow  = .6f;

        [SerializeField] float defaultAggressivenessHigh = .035f;
        [SerializeField] float defaultAggressivenessLow  = .035f;

        [SerializeField] float throttleIncreaseHigh = .001f;
        [SerializeField] float throttleIncreaseLow  = .001f;

        [SerializeField] float avoidanceHigh = 2.5f;
        [SerializeField] float avoidanceLow = 2.5f;

        [SerializeField] float aggressivenessIncreaseHigh = .001f;
        [SerializeField] float aggressivenessIncreaseLow  = .001f;

        float throttle;
        float aggressiveness;

        public float defaultThrottle => Mathf.Lerp(defaultThrottleLow, defaultThrottleHigh, skillLevel);
        public float defaultAggressiveness => Mathf.Lerp(defaultAggressivenessLow, defaultAggressivenessHigh, skillLevel);
        float throttleIncrease => Mathf.Lerp(throttleIncreaseLow, throttleIncreaseHigh, skillLevel);
        float avoidance => Mathf.Lerp(avoidanceLow, avoidanceHigh, skillLevel);
        float aggressivenessIncrease => Mathf.Lerp(aggressivenessIncreaseLow, aggressivenessIncreaseHigh, skillLevel);


        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        [SerializeField] bool ram;
        [SerializeField] bool drift;

        [Header("Targeting")]
        [Tooltip("When true, AI targets enemy players instead of crystals/items (used for Joust)")]
        [SerializeField] bool seekPlayers;
        [Inject] GameDataSO gameData;
        [Tooltip("Cadence (seconds) for re-selecting which opponent to chase while one is locked.")]
        [SerializeField] float playerSeekUpdateInterval = 0.5f;
        [Tooltip("Faster re-scan cadence (seconds) used while the AI has NO opponent locked, so it re-acquires promptly (e.g. a 1v1 opponent mid-respawn).")]
        [SerializeField] float playerReacquireInterval = 0.1f;

        [Tooltip("Seconds between refreshes of the mass cluster the AI looks at while drifting " +
                 "away from a lined-up crystal. The query is the cell's Burst density grid " +
                 "(Cell.GetExplosionTarget - the same one aggression-1 fauna hunt with), so it is " +
                 "sampled on this cadence and the cached point is flown at in between.")]
        [SerializeField, Min(0.25f)] float massClusterRetargetInterval = 1.5f;

        /// <summary>
        /// Configure AI behavior at runtime (called after spawning for solo-play AI opponents).
        /// </summary>
        public void ConfigureForGameMode(GameDataSO gameDataRef, bool shouldSeekPlayers, float skill)
        {
            gameData = gameDataRef;
            seekPlayers = shouldSeekPlayers;
            skillLevel = Mathf.Clamp01(skill);
        }

        [SerializeField] List<AIAbility> abilities;

        [SerializeField]
        ScriptableEventNoParam OnCellItemsUpdated;

        [SerializeField] private ActionExecutorRegistry actionExecutorRegistry;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };

        IVessel vessel;
        IVesselStatus VesselStatus => vessel.VesselStatus;
        IInputStatus _inputStatus => VesselStatus.InputStatus;

        float _lastPitchTarget;
        float _lastYawTarget;
        float _lastRollTarget;

        RaycastHit _hit;
        float _maxDistance = 50f;
        float _maxDistanceSquared;

        Vector3 _targetPosition;
        // Live opponent the AI is chasing in player-seek (Joust) mode. Chosen by the
        // UpdatePlayerTarget coroutine; Update() reads its current position every frame.
        Transform _targetVesselTransform;
        Vector3 _distance;
        bool LookingAtCrystal;

        // Cached mass-cluster goal for the drift look-direction (see ResolveDriftLookDirection),
        // refreshed on massClusterRetargetInterval so the Burst grid query is never per-frame.
        Vector3 _massClusterPosition;
        float _nextMassClusterSample;

        // ---- The aim telegraph (see EngageAimTelegraph) ----
        // Which control this vessel puts its IAimTelegraphAction on, resolved once at Initialize
        // from the vessel's own bindings. _hasAimTelegraph is false for most of the fleet, which is
        // the normal case and must stay silent.
        InputEvents _aimTelegraphInput;
        bool _hasAimTelegraph;
        bool _aimTelegraphHeld;

        // Latches the re-seek to the END of a commit, so the loop closes exactly once per cycle.
        // IsDrifting does not fall on the frame the AI releases the control (DriftActionSO reads it
        // back off VesselTransformer.IsDriftActive, and this vessel's ability coroutine runs a drift
        // of its own on a 2s/2s timer), so the "course left the objective" branch can hold for
        // several frames - without this it would re-pick a target every one of them.
        bool _reseekArmed;

        // Optional external steering hook. When set, the provider is sampled every
        // frame and overrides crystal/player seeking entirely. Used by game modes
        // that need bespoke AI objectives (e.g. Astro League ball striking).
        Func<Vector3> _externalTargetProvider;

        /// <summary>
        /// Routes all steering toward positions supplied by <paramref name="provider"/>.
        /// Pass the freshest position each call - it is sampled once per frame.
        /// </summary>
        public void SetExternalTargetProvider(Func<Vector3> provider) => _externalTargetProvider = provider;

        /// <summary>Restores default crystal/player target seeking.</summary>
        public void ClearExternalTargetProvider() => _externalTargetProvider = null;

        /// <summary>
        /// Optional override for the DRIFT LOOK-DIRECTION only - where the AI points its nose
        /// while its course stays locked on the objective. Deliberately a SEPARATE hook from
        /// <see cref="SetExternalTargetProvider"/>, because they answer different questions: the
        /// steering hook decides where the AI GOES (and overrides crystal seeking entirely, which
        /// is fatal in any mode whose objective is a crystal), while this one only decides what it
        /// AIMS AT once it is already going somewhere.
        ///
        /// The Bends is the case that needed it: the Dolphin's blast is fired BY collecting a
        /// crystal, so the AI must keep seeking crystals exactly as the platform already makes it -
        /// but the thing worth pointing the cone at there is an opposing PILOT, not the densest
        /// cluster of hostile mass that <see cref="ResolveDriftLookDirection"/> finds by default.
        ///
        /// Returns a world POSITION. It is sampled on the drift path only, so a null or
        /// unresolvable provider costs nothing and simply falls back to the mass cluster and then
        /// to the legacy flip - the same graceful chain the default already runs.
        /// </summary>
        public void SetDriftLookTargetProvider(Func<Vector3?> provider) => _driftLookTargetProvider = provider;

        /// <summary>Restores the default (mass-cluster) drift look-direction.</summary>
        public void ClearDriftLookTargetProvider() => _driftLookTargetProvider = null;

        Func<Vector3?> _driftLookTargetProvider;

        Dictionary<Corner, AvoidanceBehavior> CornerBehaviors;

        #region Avoidance Stuff
        const float Clockwise = -1;
        const float CounterClockwise = 1;
        struct AvoidanceBehavior
        {
            public float width;
            public float height;
            public float spin;
            public Vector3 direction;

            public AvoidanceBehavior(float width, float height, float spin, Vector3 direction)
            {
                this.width = width;
                this.height = height;
                this.spin = spin;
                this.direction = direction;
            }
        }
        #endregion

        public bool AutoPilotEnabled { get; private set; }

        private void OnEnable()
        {
            OnCellItemsUpdated.OnRaised += UpdateCellContent;
        }

        private void OnDisable()
        {
            OnCellItemsUpdated.OnRaised -= UpdateCellContent;

            // Covers the paths StopAIPilot does not: a vessel despawned or swapped mid-drift, and
            // the scene unloading under a live match.
            ReleaseAimTelegraph();
        }


        void UpdateCellContent()
        {
            // When seeking players (Joust mode), ignore cell item updates
            if (seekPlayers) return;

            // Guard against early calls before vessel is assigned
            if (vessel == null || VesselStatus == null) return;

            // cellData is unset on a vessel spawned outside a cell (tool scenes). It used to be
            // safe to dereference blind because this only ran from Initialize and from a cell's own
            // SOAP raise - neither of which happens without a cell. The drift loop now re-seeks from
            // Update(), so a null here would be an exception EVERY FRAME rather than never.
            if (cellData == null) return;

            var cellItems = cellData.CellItems;
            float MinDistance = Mathf.Infinity;
            CellItem closestItem = null;

            var myDomain = VesselStatus.Domain;

            foreach (var item in cellItems)
            {
                // Debuffs are disguised as desireable to the other team
                // So, if it's good, or if it's bad but made by another team, go for it
                if (item.ItemType != ItemType.Buff &&
                    (item.ItemType != ItemType.Debuff || item.ownDomain == myDomain)) continue;

                // Skip buff items that belong to another player's domain.
                // Only target items with no domain (Blue sentinel) or matching our own domain.
                // When our domain is Blue (e.g. Menu_Main autopilot before pick),
                // skip this check so the vessel still chases crystals freely.
                if (item.ItemType == ItemType.Buff
                    && myDomain != Domains.Blue
                    && item.ownDomain != Domains.Blue
                    && item.ownDomain != myDomain)
                    continue;

                var sqDistance = Vector3.SqrMagnitude(item.transform.position - transform.position);
                if (sqDistance < (MinDistance * MinDistance))
                {
                    closestItem = item;
                    MinDistance = sqDistance;
                }
            }

            if (closestItem != null)
                _targetPosition = closestItem.transform.position;
            else if (cellData.Cell != null)
                _targetPosition = cellData.Cell.transform.position;
        }

        /// <summary>
        /// Where the AI POINTS while it drifts away from a crystal it has already lined up.
        ///
        /// <para>The drift is the interesting half of AI flight: <c>VesselStatus.Course</c> stays
        /// locked on the crystal (so the vessel keeps travelling toward it) while the nose swings
        /// somewhere else, which is how a drifting vessel lays trail, skims, and fires along an
        /// axis that is not its heading. What it points AT is therefore a real decision, and it
        /// used to be <c>-desiredDirection</c> — a flat 180° flip away from the objective, which
        /// aims at nothing in particular and reads as the AI spinning on the spot.</para>
        ///
        /// <para>It now aims at a CLUSTER OF MASS, resolved through the cell's Burst density grid
        /// (<see cref="Cell.GetExplosionTarget"/>) — the exact query aggression-1 fauna use to hunt
        /// prey, so "go where the mass is" is one system on this platform rather than a per-mode
        /// re-derivation. The grid is keyed so <c>GetExplosionTarget(myDomain)</c> returns the
        /// densest region of mass HOSTILE to this pilot, and it already excludes nucleus-interior
        /// and shielded mass — i.e. it can only ever point at mass the AI is allowed to attack.</para>
        ///
        /// <para>Falls back to the legacy flip when there is no cell, no mass to find, or the
        /// cluster happens to lie in the same direction as the crystal (in which case the drift
        /// would not turn the vessel at all).</para>
        /// </summary>
        Vector3 ResolveDriftLookDirection(Vector3 towardTarget)
        {
            // A mode may name what this pilot should point at (The Bends: an opposing pilot).
            // Same "would this drift actually turn the vessel?" test as the mass cluster, so an
            // aim point that already lies along the objective falls through instead of producing
            // a drift that does nothing.
            if (TryGetProvidedLookDirection(towardTarget, out var towardProvided))
                return towardProvided;

            return TryGetMassClusterDirection(towardTarget, out var towardMass)
                ? towardMass
                : -towardTarget;
        }

        bool TryGetProvidedLookDirection(Vector3 towardTarget, out Vector3 direction)
        {
            direction = default;
            if (_driftLookTargetProvider == null) return false;

            var provided = _driftLookTargetProvider();
            if (!provided.HasValue) return false;

            var offset = provided.Value - transform.position;
            if (offset.sqrMagnitude < 1f) return false;

            direction = offset.normalized;
            return Vector3.Dot(direction, towardTarget) < 0.9f;
        }

        bool TryGetMassClusterDirection(Vector3 towardTarget, out Vector3 direction)
        {
            direction = default;

            var cell = cellData != null ? cellData.Cell : null;
            if (cell == null) return false;

            // Burst density query on a cadence; the cached point is flown at in between.
            if (Time.time >= _nextMassClusterSample)
            {
                _nextMassClusterSample = Time.time + massClusterRetargetInterval;
                _massClusterPosition = cell.GetExplosionTarget(VesselStatus.Domain);
            }

            var offset = _massClusterPosition - transform.position;
            if (offset.sqrMagnitude < 1f) return false;

            direction = offset.normalized;

            // Same bearing as the objective ⇒ the drift would be a no-op; take the flip instead.
            return Vector3.Dot(direction, towardTarget) < 0.9f;
        }

        // Joust targeting. The coroutine only RE-SELECTS which opponent to chase; Update()
        // reads that opponent's LIVE position every frame (via _targetVesselTransform), so the
        // AI never chases a stale 0.5s-old point. While locked on, it re-selects on the slower
        // playerSeekUpdateInterval; with NO opponent it re-scans on the faster
        // playerReacquireInterval and SelectClosestOpponent falls back to the cell centre,
        // instead of holding a stale/zero target and flying off along the forward axis.
        IEnumerator UpdatePlayerTarget()
        {
            while (AutoPilotEnabled)
            {
                SelectClosestOpponent();
                yield return new WaitForSeconds(
                    _targetVesselTransform != null ? playerSeekUpdateInterval : playerReacquireInterval);
            }
        }

        void SelectClosestOpponent()
        {
            if (gameData == null || vessel == null) return;

            var myDomain = VesselStatus.Domain;
            float closestSqDist = Mathf.Infinity;
            Transform best = null;

            foreach (var player in gameData.Players)
            {
                // Skip self and teammates (same-domain), and any player without a live vessel.
                if (player.Domain == myDomain) continue;
                if (player.Vessel == null) continue;

                var candidate = player.Vessel.Transform;
                if (candidate == null) continue; // destroyed vessel transform (Unity null)

                var sqDist = Vector3.SqrMagnitude(candidate.position - transform.position);
                if (sqDist < closestSqDist)
                {
                    closestSqDist = sqDist;
                    best = candidate;
                }
            }

            _targetVesselTransform = best;

            if (best != null)
                _targetPosition = best.position;
            else if (cellData != null && cellData.Cell != null)
                // No opponent found - loiter toward the cell centre rather than holding a
                // stale/zero target. Mirrors the crystal-seek fallback in UpdateCellContent().
                _targetPosition = cellData.Cell.transform.position;
        }

        public void Initialize(IVessel v)
        {
            vessel = v;

            foreach (var ability in abilities)
            {
                var asset = ability.Ability;
                if (asset == null) continue;

                var inst = Instantiate(asset);
                inst.name = $"{asset.name} [AI:{vessel.VesselStatus.PlayerName}]";
                inst.Initialize(VesselStatus);
                ability.Ability = inst;
            }

            _maxDistanceSquared = _maxDistance * _maxDistance;
            aggressiveness = defaultAggressiveness;
            throttle = defaultThrottle;

            CornerBehaviors = new Dictionary<Corner, AvoidanceBehavior>() {
                { Corner.TopRight, new AvoidanceBehavior (raycastWidth, raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.BottomRight, new AvoidanceBehavior (raycastWidth, -raycastHeight, CounterClockwise, Vector3.zero ) },
                { Corner.BottomLeft, new AvoidanceBehavior (-raycastWidth, -raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
            };

            // Pick up any crystals that were spawned before this AI was initialized
            UpdateCellContent();

            // Which control does this hull put its aim telegraph on? Asked ONCE, of the vessel's own
            // bindings, so the AI never names a vessel or a trigger - see IAimTelegraphAction. Most
            // of the fleet has none and this is simply false forever after.
            var handler = VesselStatus?.ActionHandler;
            _hasAimTelegraph = handler != null &&
                               handler.TryGetInputForAction<IAimTelegraphAction>(out _aimTelegraphInput);
        }

        public void StartAIPilot()
        {
            AutoPilotEnabled = true;

            foreach (var ability in abilities)
            {
                StartCoroutine(UseAbilityCoroutine(ability));
            }

            if (seekPlayers)
            {
                StartCoroutine(UpdatePlayerTarget());
            }
        }

        public void StopAIPilot()
        {
            AutoPilotEnabled = false;

            // Before the coroutines, because Update() stops running the moment AutoPilotEnabled is
            // false and this is the last chance to put the telegraph down.
            ReleaseAimTelegraph();

            foreach (var ability in abilities)
            {
                StopCoroutine(UseAbilityCoroutine(ability));
            }
        }

        void Update()
        {
            if (!AutoPilotEnabled)
                return;

            if (VesselStatus.IsStationary)
            {
                // A stopped vessel is not committed to anything, and the telegraph is the one AI
                // input that would otherwise stay lit through the pause.
                ReleaseAimTelegraph();
                return;
            }

            // Player-seek (Joust): track the chosen opponent's LIVE position every frame so the
            // AI steers at where the target IS, not where it was at the last coroutine tick. When
            // _targetVesselTransform is null (no opponent / destroyed) we keep the fallback target
            // the coroutine set (cell centre).
            if (seekPlayers && _targetVesselTransform != null)
                _targetPosition = _targetVesselTransform.position;

            // External steering hook (e.g. Astro League ball striking) overrides all other
            // targeting when set - checked last so it always wins.
            if (_externalTargetProvider != null)
                _targetPosition = _externalTargetProvider();

            _distance = _targetPosition - transform.position;
            Vector3 desiredDirection = _distance.normalized;

            LookingAtCrystal = Vector3.Dot(desiredDirection, VesselStatus.Course) >= .9f;
            if (LookingAtCrystal && drift && !VesselStatus.IsDrifting)
            {
                // COMMIT. The course locks onto the objective and the nose is freed to swing
                // elsewhere; from here the vessel is travelling at the crystal no matter where it
                // points, which is exactly the window in which announcing the aim is honest.
                VesselStatus.Course = desiredDirection;
                vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                EngageAimTelegraph();
                desiredDirection = ResolveDriftLookDirection(desiredDirection);
            }
            else if (LookingAtCrystal && VesselStatus.IsDrifting)
            {
                // Drifting AND on course for the objective - the same committed state as the branch
                // above, reached the other way. It matters that this engages too: this vessel's AI
                // ability coroutine runs its own drift on a 2s/2s timer, so roughly half the time
                // the AI lines up a crystal the drift is ALREADY on and the commit branch never
                // fires. The rule is the state, not the transition: while the course is locked on
                // the objective and the vessel is drifting, the aim is announced.
                EngageAimTelegraph();
                desiredDirection = ResolveDriftLookDirection(desiredDirection);
            }
            else if (VesselStatus.IsDrifting)
            {
                // The course has swung off the objective - the AI overshot it, someone else took
                // it, or it moved. Straighten up, stop announcing, and go find another one. That
                // last step is what CLOSES the loop: without it the AI keeps the target it can no
                // longer reach until the cell happens to raise OnCellItemsUpdated, which is a
                // crystal event and not a "this pilot needs a new goal" event.
                vessel.StopShipControllerActions(InputEvents.LeftStickAction);
                ReleaseAimTelegraph();

                if (_reseekArmed)
                {
                    _reseekArmed = false;
                    UpdateCellContent();
                }
            }
            else
            {
                // Neither committed nor unwinding a commit. Nothing above can hold the telegraph in
                // this state, but the drift flag is written from three places (this loop, the drift
                // action, and the transformer's ease-out) and only one of them is here - so the
                // invariant is enforced rather than assumed: the telegraph is lit if and only if the
                // course is locked on the objective and the vessel is drifting along it.
                ReleaseAimTelegraph();
            }


            if (_distance.sqrMagnitude < float.Epsilon) // On top of the target - avoid div-by-zero (guards the sqrMagnitude divisor below)
            {
                // Don't latch the previous frame's turn input (which would keep the vessel
                // veering with nothing to steer toward). Fly a clean straight pass-through.
                if (VesselStatus.IsSingleStickControls)
                    _inputStatus.EasedLeftJoystickPosition = Vector2.zero;
                else
                {
                    _inputStatus.XSum = 0;
                    _inputStatus.YSum = 0;
                    _inputStatus.YDiff = 0;
                    _inputStatus.XDiff = Mathf.Clamp(throttle, 0, 1);
                }
                return;
            }

            Vector3 combinedLocalCrossProduct = Vector3.zero;
            float sqrMagnitude = _distance.sqrMagnitude;
            Vector3 crossProduct = Vector3.Cross(transform.forward, desiredDirection);
            Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
            combinedLocalCrossProduct += localCrossProduct;

            aggressiveness = 100f;  // Multiplier to mitigate vanishing cross products that cause aimless drift
            float angle = Mathf.Asin(Mathf.Clamp(combinedLocalCrossProduct.sqrMagnitude * aggressiveness / Mathf.Min(sqrMagnitude, _maxDistance), -1f, 1f)) * Mathf.Rad2Deg;

            if (VesselStatus.IsSingleStickControls)
            {
                float x = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                float y = -Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
                _inputStatus.EasedLeftJoystickPosition = new Vector2(x, y);
            }
            else
            {
                _inputStatus.XSum = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                _inputStatus.YSum = Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
                _inputStatus.YDiff = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                _inputStatus.XDiff = (LookingAtCrystal && ram) ? 1 : Mathf.Clamp(throttle, 0, 1);
            }

            //aggressiveness += aggressivenessIncrease * Time.deltaTime;
            throttle += throttleIncrease * Time.deltaTime;
        }
        
        /// <summary>
        /// Hold this vessel's <see cref="IAimTelegraphAction"/> — the Dolphin's Echo Sight today —
        /// for as long as the AI is committed to a drift onto its objective.
        ///
        /// <para><b>Why the AI holds it at all.</b> A telegraph does nothing for the pilot pressing
        /// it; it exists so everyone ELSE can read the aim. A human Dolphin holds it to see its own
        /// cone, and the side effect is that rivals learn what it is about to take. An AI has no
        /// use for the first half and every reason to pay the second: without this an AI's blast
        /// arrives with no warning while a human's is always announced, which is the wrong
        /// asymmetry — the harder opponent should be the more readable one.</para>
        ///
        /// <para><b>Held for exactly the drift.</b> The drift IS the commitment: the course is
        /// locked on the crystal, so the cone that eventually comes out is already decided in
        /// direction. Announcing before the commit would be a lie (the AI is still choosing) and
        /// announcing after it would be pointless (the blast has happened). The Dolphin's whole
        /// weapon is "bank energy, drift onto a crystal, release a cone" and this lights up for the
        /// middle beat of it.</para>
        ///
        /// <para><b>Replicated on purpose.</b> An AI pilot runs on the server only, and a telegraph
        /// nobody else sees is not a telegraph — see
        /// <see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/> for why this
        /// is the one class of AI input that has to travel while the drift itself does not.</para>
        ///
        /// Idempotent: the latch means an <see cref="Update"/> that re-enters the commit branch
        /// (a drift that has not registered yet) cannot stack presses.
        /// </summary>
        void EngageAimTelegraph()
        {
            if (!_hasAimTelegraph || _aimTelegraphHeld) return;

            var handler = VesselStatus?.ActionHandler;
            if (handler == null) return;

            handler.PerformShipControllerActionsReplicated(_aimTelegraphInput);
            _aimTelegraphHeld = true;
            _reseekArmed = true;
        }

        /// <summary>
        /// Stop announcing. Called when the drift ends, and again from
        /// <see cref="StopAIPilot"/> and <see cref="OnDisable"/> — a telegraph is the one AI input
        /// that leaves something on screen, so every path out of autopilot has to put it down or
        /// the mark outlives the behaviour that raised it.
        /// </summary>
        void ReleaseAimTelegraph()
        {
            if (!_aimTelegraphHeld) return;
            _aimTelegraphHeld = false;

            var handler = VesselStatus?.ActionHandler;
            if (handler == null) return;

            handler.StopShipControllerActionsReplicated(_aimTelegraphInput);
        }

        IEnumerator UseAbilityCoroutine(AIAbility action) 
        {
            yield return new WaitForSeconds(3);
            while (AutoPilotEnabled)
            {
                action.Ability.StartAction(actionExecutorRegistry, VesselStatus);
                yield return new WaitForSeconds(action.Duration);
                action.Ability.StopAction(actionExecutorRegistry, VesselStatus);
                yield return new WaitForSeconds(action.Cooldown);
            }
        }
        
        #region Unused Methods

        Vector3 ShootLaser(Vector3 position)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out _hit, _maxDistance))
            {
                Debug.DrawLine(transform.position + position, _hit.point, Color.red);
                return _hit.point - (transform.position + position);
            }

            Debug.DrawLine(transform.position + position, transform.position + position + transform.forward * _maxDistance, Color.green);
            return transform.forward * _maxDistance - (transform.position + position);
        }
        
        float CalculateRollAdjustment(Dictionary<Corner, Vector3> obstacleDirections)
        {
            float rollAdjustment = 0f;

            // Example logic: If top right and bottom left corners detect obstacles, induce a roll.
            if (obstacleDirections[Corner.TopRight].magnitude > 0 && obstacleDirections[Corner.BottomLeft].magnitude > 0)
                rollAdjustment -= 1; // Roll left
            if (obstacleDirections[Corner.TopLeft].magnitude > 0 && obstacleDirections[Corner.BottomRight].magnitude > 0)
                rollAdjustment += 1; // Roll right

            return rollAdjustment;
        }

        float SigmoidResponse(float input)
        {
            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
            return output;
        }
        
        
        /*
         
         // TODO - This method is moved inside AIPilot. Some logics and conditions might have
         // been temporarily removed, but check this method, for adding those logics inside the
         // new method of AIPilot
         IEnumerator SetTargetCoroutine()
        {
            // TODO - these lists if needed, should be specified separate.
            List<ShipClassType> aggressiveShips = new List<ShipClassType>
            {
                ShipClassType.Rhino,
                ShipClassType.Sparrow,
            };

            var rand = new System.Random();

            // Assume activeNode can't change.
            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);
            if (activeCell == null)
                activeCell = CellControlManager.Instance.GetNearestCell(transform.position);

            while (true)
            {
                if (activeCell != null &&
                    // TODO - Commented out as aggressive
                    // aggressiveShips.Contains(vessel.VesselStatus.ShipType) &&
                    activeCell.ControllingTeam != Teams.None)
                {
                    if ((VesselStatus.Team == activeCell.ControllingTeam) || (rand.NextDouble() < 0.5))  // Your team is winning.
                    {
                        _targetPosition = _crystalPosition;
                    }
                    else
                    {
                        _targetPosition = activeCell.GetExplosionTarget(activeCell.ControllingTeam);  // Block centroid belonging to the winning team
                    }
                }
                else
                {
                    _targetPosition = _crystalPosition;
                }
                yield return new WaitForSeconds(targetUpdateFrequencySeconds);
            }
        }*/


        #endregion
    }
}