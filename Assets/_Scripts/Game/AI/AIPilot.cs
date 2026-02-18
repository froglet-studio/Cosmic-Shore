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

        [Header("Prism Seeking")]
        [SerializeField] float prismDetectionRadius = 120f;
        [SerializeField] float collisionAvoidanceDistance = 30f;

        int Intensity => selectedIntensity != null ? Mathf.Clamp(selectedIntensity.Value, 1, 4) : 1;

        // Intensity-derived parameters (0 = no effect at intensity 1, 1 = full effect at intensity 4)
        float IntensityT => (Intensity - 1) / 3f;

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
        }

        public void StartAIPilot()
        {
            AutoPilotEnabled = true;

            foreach (var ability in abilities)
            {
                StartCoroutine(UseAbilityCoroutine(ability));
            }
        }

        public void StopAIPilot()
        {
            AutoPilotEnabled = false;

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
            // At higher intensities, blend prism-seeking and collision avoidance.
            if (Intensity <= 1)
            {
                UpdateIntensity1();
                return;
            }

            ScanForPrisms();
            _targetPosition = ComputeBlendedTarget();
            Vector3 avoidanceSteer = ComputeCollisionAvoidance();

            _distance = _targetPosition - transform.position;
            Vector3 desiredDirection = _distance.normalized;

            // Blend in avoidance steering based on intensity
            float avoidanceWeight = IntensityT * 0.6f;
            if (avoidanceSteer.sqrMagnitude > 0.001f)
                desiredDirection = (desiredDirection * (1f - avoidanceWeight) + avoidanceSteer.normalized * avoidanceWeight).normalized;

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

            throttle += throttleIncrease * Time.deltaTime;
        }

        #region Prism Seeking (Intensity 2+)

        void ScanForPrisms()
        {
            _prismScanTimer -= Time.deltaTime;
            if (_prismScanTimer > 0f) return;

            // Scan more frequently at higher intensity
            _prismScanTimer = Mathf.Lerp(_prismScanInterval * 2f, _prismScanInterval * 0.5f, IntensityT);

            float scanRadius = Mathf.Lerp(prismDetectionRadius * 0.4f, prismDetectionRadius, IntensityT);
            int trailBlockMask = 1 << _trailBlockLayer;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _prismScanResults, trailBlockMask);

            if (hitCount == 0)
            {
                _hasPrismTarget = false;
                return;
            }

            // Find the best prism to skim: one that is roughly ahead of us and at a skimmable offset
            // A good skim target is one we can fly alongside, not directly into.
            Vector3 forward = transform.forward;
            Vector3 myPos = transform.position;
            float bestScore = float.NegativeInfinity;
            Vector3 bestTarget = myPos;
            bool foundTarget = false;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _prismScanResults[i];
                if (col == null) continue;

                Vector3 toPrism = col.transform.position - myPos;
                float dist = toPrism.magnitude;
                if (dist < 2f) continue; // Too close, skip

                Vector3 dirToPrism = toPrism / dist;
                float dotForward = Vector3.Dot(forward, dirToPrism);

                // Only consider prisms that are roughly ahead (within ~120 degree cone at intensity 2,
                // within ~150 degree cone at intensity 4)
                float minDot = Mathf.Lerp(-0.1f, -0.5f, IntensityT);
                if (dotForward < minDot) continue;

                // Score: prefer prisms that are ahead and at moderate distance
                // Being directly ahead is fine - the skimmer has a width, so flying near is enough
                // Prefer closer prisms that are ahead
                float forwardScore = dotForward * 2f;
                float distScore = 1f - Mathf.Clamp01(dist / scanRadius);

                // At higher intensity, prefer prisms that form a line we can follow
                // (i.e., prisms whose forward roughly aligns with ours - same trail direction)
                float alignScore = 0f;
                if (Intensity >= 3)
                {
                    float trailAlign = Mathf.Abs(Vector3.Dot(forward, col.transform.forward));
                    alignScore = trailAlign * 0.5f;
                }

                float score = forwardScore + distScore + alignScore;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = col.transform.position;
                    foundTarget = true;
                }
            }

            _hasPrismTarget = foundTarget;
            _bestPrismTarget = bestTarget;
        }

        Vector3 ComputeBlendedTarget()
        {
            // Factors that increase crystal bias:
            // 1. Distance to crystal is small (we're close, go grab it)
            // 2. Boost is near max (we're already fast, don't need more prisms)
            //
            // Factors that increase prism bias:
            // 1. Distance to crystal is large (long way to go, skim for speed)
            // 2. Boost is low (need speed boost from prisms)
            // 3. Higher intensity (smarter AI)

            if (!_hasPrismTarget)
                return _crystalTargetPosition;

            float distToCrystal = Vector3.Distance(transform.position, _crystalTargetPosition);

            // Normalize boost: 0 = base, 1 = max (base=1, max=5 typically)
            float boostNorm = Mathf.Clamp01((VesselStatus.BoostMultiplier - 1f) / 4f);

            // Crystal proximity bias: increases sharply when close to crystal
            // At ~50 units, starts pulling toward crystal; at ~15 units, fully crystal-focused
            float crystalCloseRange = Mathf.Lerp(30f, 15f, IntensityT);
            float crystalFarRange = Mathf.Lerp(80f, 50f, IntensityT);
            float crystalProximityBias = 1f - Mathf.Clamp01((distToCrystal - crystalCloseRange) / (crystalFarRange - crystalCloseRange));

            // Boost saturation bias: when boost is near max, prefer crystal
            float boostBias = boostNorm * boostNorm; // Quadratic - ramps up fast near max

            // Combined crystal weight
            float crystalWeight = Mathf.Max(crystalProximityBias, boostBias);

            // At higher intensity, lean more toward prisms when far from crystal and low on boost
            float basePrismWeight = IntensityT * 0.9f;
            float prismWeight = basePrismWeight * (1f - crystalWeight);

            // Final blend
            float totalWeight = crystalWeight + prismWeight;
            if (totalWeight < 0.001f) return _crystalTargetPosition;

            float crystalFraction = crystalWeight / totalWeight;
            return Vector3.Lerp(_bestPrismTarget, _crystalTargetPosition, crystalFraction);
        }

        #endregion

        #region Collision Avoidance (Intensity 2+)

        Vector3 ComputeCollisionAvoidance()
        {
            if (Intensity < 2) return Vector3.zero;

            float checkDist = Mathf.Lerp(collisionAvoidanceDistance * 0.5f, collisionAvoidanceDistance, IntensityT);
            int trailBlockMask = 1 << _trailBlockLayer;
            Vector3 avoidance = Vector3.zero;
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;

            // Cast a central ray forward to detect head-on collisions
            if (Physics.Raycast(origin, fwd, out var hitCenter, checkDist, trailBlockMask))
            {
                float urgency = 1f - (hitCenter.distance / checkDist);
                avoidance += hitCenter.normal * urgency;
            }

            // At intensity 3+, add peripheral rays for better awareness
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

                foreach (var dir in offsets)
                {
                    if (Physics.Raycast(origin, dir, out var hitSide, checkDist * 0.7f, trailBlockMask))
                    {
                        float urgency = 1f - (hitSide.distance / (checkDist * 0.7f));
                        avoidance += hitSide.normal * urgency * 0.5f;
                    }
                }
            }

            // At intensity 4, also check diagonals for tight spaces
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

                foreach (var dir in diags)
                {
                    if (Physics.Raycast(origin, dir, out var hitDiag, checkDist * 0.5f, trailBlockMask))
                    {
                        float urgency = 1f - (hitDiag.distance / (checkDist * 0.5f));
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