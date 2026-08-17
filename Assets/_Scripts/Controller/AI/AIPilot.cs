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
        }


        void UpdateCellContent()
        {
            // When seeking players (Joust mode), ignore cell item updates
            if (seekPlayers) return;

            // Guard against early calls before vessel is assigned
            if (vessel == null || VesselStatus == null) return;

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
            return TryGetMassClusterDirection(towardTarget, out var towardMass)
                ? towardMass
                : -towardTarget;
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

            // Update() only runs while the pilot is active (StartAIPilot/StopAIPilot
            // toggle it) - human-piloted vessels pay no per-frame AI cost.
            enabled = AutoPilotEnabled;
        }

        public void StartAIPilot()
        {
            // Idempotent: menu activation calls this twice for the same vessel
            // (MenuServerPlayerVesselInitializer.ActivateAutopilot + the client-side
            // ActivateLocalPlayerAutopilot) and a second pass would stack duplicate
            // ability/seek coroutines.
            if (AutoPilotEnabled)
                return;

            AutoPilotEnabled = true;
            enabled = true;

            // The OnCellItemsUpdated subscription was inactive while the component was
            // disabled - re-seed the target so the pilot doesn't fly a stale heading
            // until the next raise.
            UpdateCellContent();

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
            // Already stopped - every human turn start clears the pilot defensively,
            // so skip the redundant native StopAllCoroutines/enabled writes.
            if (!AutoPilotEnabled && !enabled)
                return;

            AutoPilotEnabled = false;

            // StopCoroutine(UseAbilityCoroutine(ability)) stopped a freshly created
            // enumerator, never the running coroutine, so abilities could fire one more
            // Start/Stop cycle after handing the vessel to a human. Kill them all.
            StopAllCoroutines();

            // No Update() dispatch while a human pilots this vessel.
            enabled = false;
        }

        void Update()
        {
            if (!AutoPilotEnabled)
                return;

            if (VesselStatus.IsStationary)
                return;

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
                VesselStatus.Course = desiredDirection;
                vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                desiredDirection = ResolveDriftLookDirection(desiredDirection);
            }
            else if (LookingAtCrystal && VesselStatus.IsDrifting)
                desiredDirection = ResolveDriftLookDirection(desiredDirection);
            else if (VesselStatus.IsDrifting) vessel.StopShipControllerActions(InputEvents.LeftStickAction);


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