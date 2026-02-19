using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using CosmicShore.Soap;
using Obvious.Soap;

namespace CosmicShore.Game.AI
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

        float targetUpdateFrequencySeconds = 2f;

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        [SerializeField] bool ram;
        [SerializeField] bool drift;

        [SerializeField] List<AIAbility> abilities;

        [SerializeField]
        ScriptableEventNoParam OnCellItemsUpdated;

        [SerializeField] private ActionExecutorRegistry actionExecutorRegistry;

        [Header("Intensity Scaling")]
        [SerializeField] IntVariable selectedIntensity;

        [Header("Prism Skimming (defaults, overridden by genome at intensity 4)")]
        [SerializeField] float prismDetectionRadius = 120f;
        [SerializeField] float skimStandoffDistance = 12f;
        [SerializeField] float collisionAvoidanceDistance = 30f;

        [Header("Evolution")]
        [SerializeField] PilotEvolution evolution;

        int Intensity => selectedIntensity != null ? Mathf.Clamp(selectedIntensity.Value, 1, 4) : 1;

        // Intensity-derived parameters (0 = no effect at intensity 1, 1 = full effect at intensity 4)
        float IntensityT => (Intensity - 1) / 3f;

        // Active genome parameters (loaded from evolution or defaults)
        PilotGenome _activeGenome;
        PilotFitnessTracker _fitnessTracker;

        // Genome-aware accessors: lerp from inspector defaults toward genome values by IntensityT.
        // At intensity 2 (IntensityT=0.33) mostly defaults; at intensity 4 (IntensityT=1) fully genome.
        float G_PrismDetectionRadius => _activeGenome != null ? Mathf.Lerp(prismDetectionRadius, _activeGenome.prismDetectionRadius, IntensityT) : prismDetectionRadius;
        float G_SkimStandoffDistance => _activeGenome != null ? Mathf.Lerp(skimStandoffDistance, _activeGenome.skimStandoffDistance, IntensityT) : skimStandoffDistance;
        float G_MinPrismScanDistance => _activeGenome != null ? Mathf.Lerp(20f, _activeGenome.minPrismScanDistance, IntensityT) : 20f;
        float G_MaxNudgeStrength => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.maxNudgeStrength, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
        float G_DotCrystalThreshold => _activeGenome != null ? Mathf.Lerp(0.5f, _activeGenome.dotCrystalThreshold, IntensityT) : 0.5f;
        float G_DotForwardThreshold => _activeGenome != null ? Mathf.Lerp(0.3f, _activeGenome.dotForwardThreshold, IntensityT) : 0.3f;
        float G_CollisionAvoidanceDistance => _activeGenome != null ? Mathf.Lerp(collisionAvoidanceDistance, _activeGenome.collisionAvoidanceDistance, IntensityT) : collisionAvoidanceDistance;
        float G_AvoidanceWeight => _activeGenome != null ? Mathf.Lerp(0.15f, _activeGenome.avoidanceWeight, IntensityT) : Mathf.Lerp(0.05f, 0.15f, IntensityT);
        float G_CrystalFadeDistance => _activeGenome != null ? Mathf.Lerp(50f, _activeGenome.crystalFadeDistance, IntensityT) : 50f;
        float G_BoostFadeStrength => _activeGenome != null ? Mathf.Lerp(0.6f, _activeGenome.boostFadeStrength, IntensityT) : 0.6f;
        float G_ThrottleRampRate => _activeGenome != null ? Mathf.Lerp(throttleIncrease, _activeGenome.throttleRampRate, IntensityT) : throttleIncrease;

        IVessel vessel;
        IVesselStatus VesselStatus => vessel.VesselStatus;
        IInputStatus _inputStatus => VesselStatus.InputStatus;

        float _lastPitchTarget;
        float _lastYawTarget;
        float _lastRollTarget;

        RaycastHit _hit;
        float _maxDistance = 50f;
        float _maxDistanceSquared;

        Vector3 _crystalTargetPosition;
        Vector3 _targetPosition;
        Vector3 _distance;
        bool LookingAtCrystal;

        // Prism seeking state
        int _trailBlockLayer;
        Vector3 _bestPrismTarget;
        bool _hasPrismTarget;
        float _prismScanTimer;
        float _prismScanInterval = 0.25f;
        static readonly Collider[] _prismScanResults = new Collider[64];

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
            var activeCell = cellData.Cell;
            var cellItems = cellData.CellItems;
            float MinDistance = Mathf.Infinity;
            CellItem closestItem = null;

            foreach (var item in cellItems)
            {
                // Debuffs are disguised as desireable to the other team
                // So, if it's good, or if it's bad but made by another team, go for it
                if (item.ItemType != ItemType.Buff &&
                    (item.ItemType != ItemType.Debuff || item.ownDomain == VesselStatus.Domain)) continue;
                var sqDistance = Vector3.SqrMagnitude(item.transform.position - transform.position);
                if (sqDistance < (MinDistance * MinDistance))
                {
                    closestItem = item;
                    MinDistance = sqDistance;
                }
            }

            _crystalTargetPosition = !closestItem ? activeCell.transform.position : closestItem.transform.position;
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

            _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");

            LoadGenome();
        }

        void LoadGenome()
        {
            if (evolution != null && Intensity >= 2)
            {
                _activeGenome = evolution.GetNextGenome();
                throttle = _activeGenome.throttleBase;

                _fitnessTracker = GetComponent<PilotFitnessTracker>();
            }
            else
            {
                _activeGenome = null;
            }
        }

        public void StartAIPilot()
        {
            AutoPilotEnabled = true;

            if (_fitnessTracker != null)
                _fitnessTracker.StartTracking(VesselStatus);

            foreach (var ability in abilities)
            {
                StartCoroutine(UseAbilityCoroutine(ability));
            }
        }

        public void StopAIPilot()
        {
            AutoPilotEnabled = false;

            if (_fitnessTracker != null)
                _fitnessTracker.StopTracking();

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
                return;

            // At intensity 1, behave exactly as the original crystal-only AI.
            // At higher intensities, the crystal remains the primary target but
            // the AI applies small lateral nudges to graze prisms along the way.
            if (Intensity <= 1)
            {
                UpdateIntensity1();
                return;
            }

            // Always head toward the crystal
            _targetPosition = _crystalTargetPosition;
            _distance = _targetPosition - transform.position;

            if (_distance.magnitude < float.Epsilon)
                return;

            Vector3 desiredDirection = _distance.normalized;

            // Apply a small lateral nudge to pass near prisms that are along our route
            ScanForPrisms();
            Vector3 prismNudge = ComputePrismNudge(desiredDirection);
            if (prismNudge.sqrMagnitude > 0.001f)
                desiredDirection = (desiredDirection + prismNudge).normalized;

            // Subtle collision avoidance - small corrections, never overrides crystal heading
            Vector3 avoidanceSteer = ComputeCollisionAvoidance();
            if (avoidanceSteer.sqrMagnitude > 0.001f)
            {
                desiredDirection = (desiredDirection + avoidanceSteer * G_AvoidanceWeight).normalized;
            }

            LookingAtCrystal = Vector3.Dot(desiredDirection, VesselStatus.Course) >= .9f;
            if (LookingAtCrystal && drift && !VesselStatus.IsDrifting)
            {
                VesselStatus.Course = desiredDirection;
                vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                desiredDirection *= -1;
            }
            else if (LookingAtCrystal && VesselStatus.IsDrifting) desiredDirection *= -1;
            else if (VesselStatus.IsDrifting) vessel.StopShipControllerActions(InputEvents.LeftStickAction);

            ApplySteering(desiredDirection, _distance.sqrMagnitude);
        }

        // Original intensity 1 behavior - unchanged from baseline
        void UpdateIntensity1()
        {
            _targetPosition = _crystalTargetPosition;
            _distance = _targetPosition - transform.position;
            Vector3 desiredDirection = _distance.normalized;

            LookingAtCrystal = Vector3.Dot(desiredDirection, VesselStatus.Course) >= .9f;
            if (LookingAtCrystal && drift && !VesselStatus.IsDrifting)
            {
                VesselStatus.Course = desiredDirection;
                vessel.PerformShipControllerActions(InputEvents.LeftStickAction);
                desiredDirection *= -1;
            }
            else if (LookingAtCrystal && VesselStatus.IsDrifting) desiredDirection *= -1;
            else if (VesselStatus.IsDrifting) vessel.StopShipControllerActions(InputEvents.LeftStickAction);


            if (_distance.magnitude < float.Epsilon)
                return;

            ApplySteering(desiredDirection, _distance.sqrMagnitude);
        }

        void ApplySteering(Vector3 desiredDirection, float sqrMagnitude)
        {
            Vector3 crossProduct = Vector3.Cross(transform.forward, desiredDirection);
            Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);

            aggressiveness = 100f;
            float angle = Mathf.Asin(Mathf.Clamp(localCrossProduct.sqrMagnitude * aggressiveness / Mathf.Min(sqrMagnitude, _maxDistance), -1f, 1f)) * Mathf.Rad2Deg;

            if (VesselStatus.IsSingleStickControls)
            {
                float x = Mathf.Clamp(angle * localCrossProduct.y, -1, 1);
                float y = -Mathf.Clamp(angle * localCrossProduct.x, -1, 1);
                _inputStatus.EasedLeftJoystickPosition = new Vector2(x, y);
            }
            else
            {
                _inputStatus.XSum = Mathf.Clamp(angle * localCrossProduct.y, -1, 1);
                _inputStatus.YSum = Mathf.Clamp(angle * localCrossProduct.x, -1, 1);
                _inputStatus.YDiff = Mathf.Clamp(angle * localCrossProduct.y, -1, 1);
                _inputStatus.XDiff = (LookingAtCrystal && ram) ? 1 : Mathf.Clamp(throttle, 0, 1);
            }

            throttle += G_ThrottleRampRate * Time.deltaTime;
        }

        #region Prism Skimming - Flyby Nudge (Intensity 2+)

        void ScanForPrisms()
        {
            _prismScanTimer -= Time.deltaTime;
            if (_prismScanTimer > 0f) return;
            _prismScanTimer = _prismScanInterval;

            float scanRadius = G_PrismDetectionRadius;
            int trailBlockMask = 1 << _trailBlockLayer;
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position, scanRadius, _prismScanResults,
                trailBlockMask, QueryTriggerInteraction.Collide);

            _hasPrismTarget = false;
            if (hitCount == 0) return;

            // Find the best prism that is along our route to the crystal.
            // Only consider prisms that are well ahead (20+ units) so we plan
            // a flyby path rather than yanking toward a nearby prism.
            Vector3 myPos = transform.position;
            Vector3 toCrystal = _crystalTargetPosition - myPos;
            float crystalDist = toCrystal.magnitude;
            Vector3 crystalDir = crystalDist > 0.01f ? toCrystal / crystalDist : transform.forward;

            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _prismScanResults[i];
                if (col == null) continue;

                Vector3 toPrism = col.transform.position - myPos;
                float dist = toPrism.magnitude;

                // Ignore prisms that are too close (can't adjust in time),
                // behind us, or past the crystal
                if (dist < G_MinPrismScanDistance || dist > crystalDist) continue;

                Vector3 dirToPrism = toPrism / dist;

                // Must be ahead and roughly toward the crystal
                float dotCrystal = Vector3.Dot(crystalDir, dirToPrism);
                if (dotCrystal < G_DotCrystalThreshold) continue;

                float dotForward = Vector3.Dot(transform.forward, dirToPrism);
                if (dotForward < G_DotForwardThreshold) continue;

                // Score: prisms closest to the line toward crystal score highest
                float score = dotCrystal * 2f + (1f - dist / scanRadius);

                if (score > bestScore)
                {
                    bestScore = score;
                    _bestPrismTarget = col.transform.position;
                    _hasPrismTarget = true;
                }
            }
        }

        // Computes a small perpendicular nudge so the ship's path passes within
        // skim range of the best prism, rather than aiming AT the prism center.
        // If already within skim range, returns zero - no correction needed.
        Vector3 ComputePrismNudge(Vector3 crystalDirection)
        {
            if (!_hasPrismTarget) return Vector3.zero;

            Vector3 toPrism = _bestPrismTarget - transform.position;

            // How far off-line is the prism from our crystal-bound path?
            float alongPath = Vector3.Dot(toPrism, crystalDirection);
            Vector3 perpVector = toPrism - alongPath * crystalDirection;
            float perpDist = perpVector.magnitude;

            // If already within standoff range, the skimmer will graze it naturally - no nudge
            float standoff = G_SkimStandoffDistance;
            if (perpDist < standoff) return Vector3.zero;

            // Nudge direction: perpendicular toward the prism's side of our path
            Vector3 nudgeDir = perpVector / perpDist;

            // How much do we need to close? Only close to standoff range, not to center
            float gapToClose = perpDist - standoff;
            float gapNorm = Mathf.Clamp01(gapToClose / (G_PrismDetectionRadius * 0.5f));

            // Scale by crystal proximity and boost
            float distToCrystal = (_crystalTargetPosition - transform.position).magnitude;
            float crystalFade = Mathf.Clamp01(distToCrystal / G_CrystalFadeDistance);
            float boostNorm = Mathf.Clamp01((VesselStatus.BoostMultiplier - 1f) / 4f);
            float boostFade = 1f - boostNorm * G_BoostFadeStrength;

            float nudgeMag = G_MaxNudgeStrength * gapNorm * crystalFade * boostFade;

            return nudgeDir * nudgeMag;
        }

        #endregion

        #region Collision Avoidance (Intensity 2+)

        Vector3 ComputeCollisionAvoidance()
        {
            if (Intensity < 2) return Vector3.zero;

            float checkDist = G_CollisionAvoidanceDistance;
            int trailBlockMask = 1 << _trailBlockLayer;
            Vector3 avoidance = Vector3.zero;
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;

            // Central ray - detect head-on collisions
            if (Physics.Raycast(origin, fwd, out var hitCenter, checkDist, trailBlockMask, QueryTriggerInteraction.Collide))
            {
                float urgency = 1f - (hitCenter.distance / checkDist);
                avoidance += hitCenter.normal * urgency;
            }

            // At intensity 3+, peripheral rays for better awareness
            if (Intensity >= 3)
            {
                float spreadAngle = 20f;
                Vector3 right = transform.right;
                Vector3 up = transform.up;

                Vector3[] offsets = {
                    Quaternion.AngleAxis(spreadAngle, up) * fwd,
                    Quaternion.AngleAxis(-spreadAngle, up) * fwd,
                    Quaternion.AngleAxis(spreadAngle, right) * fwd,
                    Quaternion.AngleAxis(-spreadAngle, right) * fwd,
                };

                float sideRange = checkDist * 0.7f;
                foreach (var dir in offsets)
                {
                    if (Physics.Raycast(origin, dir, out var hitSide, sideRange, trailBlockMask, QueryTriggerInteraction.Collide))
                    {
                        float urgency = 1f - (hitSide.distance / sideRange);
                        avoidance += hitSide.normal * urgency * 0.5f;
                    }
                }
            }

            // At intensity 4, diagonal rays for tight spaces
            if (Intensity >= 4)
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                float diagAngle = 35f;

                Vector3[] diags = {
                    Quaternion.AngleAxis(diagAngle, up) * Quaternion.AngleAxis(diagAngle, right) * fwd,
                    Quaternion.AngleAxis(-diagAngle, up) * Quaternion.AngleAxis(diagAngle, right) * fwd,
                    Quaternion.AngleAxis(diagAngle, up) * Quaternion.AngleAxis(-diagAngle, right) * fwd,
                    Quaternion.AngleAxis(-diagAngle, up) * Quaternion.AngleAxis(-diagAngle, right) * fwd,
                };

                float diagRange = checkDist * 0.5f;
                foreach (var dir in diags)
                {
                    if (Physics.Raycast(origin, dir, out var hitDiag, diagRange, trailBlockMask, QueryTriggerInteraction.Collide))
                    {
                        float urgency = 1f - (hitDiag.distance / diagRange);
                        avoidance += hitDiag.normal * urgency * 0.3f;
                    }
                }
            }

            return avoidance;
        }

        #endregion

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
    }
}