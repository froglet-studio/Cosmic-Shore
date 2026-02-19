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
                float avoidanceWeight = Mathf.Lerp(0.05f, 0.15f, IntensityT);
                desiredDirection = (desiredDirection + avoidanceSteer * avoidanceWeight).normalized;
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

            throttle += throttleIncrease * Time.deltaTime;
        }

        #region Prism Skimming - Lateral Nudge (Intensity 2+)

        void ScanForPrisms()
        {
            _prismScanTimer -= Time.deltaTime;
            if (_prismScanTimer > 0f) return;
            _prismScanTimer = _prismScanInterval;

            float scanRadius = Mathf.Lerp(prismDetectionRadius * 0.4f, prismDetectionRadius, IntensityT);
            int trailBlockMask = 1 << _trailBlockLayer;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, _prismScanResults, trailBlockMask);

            _hasPrismTarget = false;
            if (hitCount == 0) return;

            // Find the best prism that is roughly along our route to the crystal.
            // We want prisms that are ahead of us AND between us and the crystal,
            // so that nudging toward them doesn't pull us off-course.
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
                if (dist < 3f || dist > crystalDist) continue; // Skip if too close or past the crystal

                Vector3 dirToPrism = toPrism / dist;

                // Must be in the forward hemisphere toward the crystal
                float dotCrystal = Vector3.Dot(crystalDir, dirToPrism);
                if (dotCrystal < 0.3f) continue; // Only prisms roughly toward the crystal

                // Must also be ahead of us
                float dotForward = Vector3.Dot(transform.forward, dirToPrism);
                if (dotForward < 0.2f) continue;

                // Score: prefer prisms that are close to the line between us and crystal
                // and that are relatively close (so we can reach them without a big detour)
                float lineProximity = dotCrystal; // Higher = more aligned with crystal direction
                float closeness = 1f - Mathf.Clamp01(dist / scanRadius);

                float score = lineProximity * 2f + closeness;

                if (score > bestScore)
                {
                    bestScore = score;
                    _bestPrismTarget = col.transform.position;
                    _hasPrismTarget = true;
                }
            }
        }

        // Returns a small perpendicular nudge vector to steer the ship's path
        // slightly toward the best prism, without changing the overall heading toward the crystal.
        Vector3 ComputePrismNudge(Vector3 crystalDirection)
        {
            if (!_hasPrismTarget) return Vector3.zero;

            Vector3 toPrism = _bestPrismTarget - transform.position;
            float prismDist = toPrism.magnitude;
            if (prismDist < 1f) return Vector3.zero;

            // Project prism direction onto the plane perpendicular to the crystal direction.
            // This gives us the lateral offset - how much we need to steer sideways
            // to pass near the prism without changing our forward progress toward the crystal.
            Vector3 prismDir = toPrism / prismDist;
            Vector3 lateralOffset = prismDir - Vector3.Dot(prismDir, crystalDirection) * crystalDirection;

            // Scale the nudge:
            // - Stronger at higher intensity (better racers seek prisms more deliberately)
            // - Weaker when close to crystal (don't get distracted at the finish)
            // - Weaker when boost is already high (diminishing returns)
            float distToCrystal = Vector3.Distance(transform.position, _crystalTargetPosition);
            float crystalProximityFade = Mathf.Clamp01(distToCrystal / 40f); // Fades out within 40 units of crystal
            float boostNorm = Mathf.Clamp01((VesselStatus.BoostMultiplier - 1f) / 4f);
            float boostFade = 1f - boostNorm * 0.7f; // Still nudge a bit even at high boost

            // Max nudge magnitude: small at intensity 2 (~0.08), moderate at intensity 4 (~0.25)
            // This is added to a unit-length crystal direction, so 0.25 is roughly a 14 degree deviation
            float maxNudge = Mathf.Lerp(0.08f, 0.25f, IntensityT);
            float nudgeMagnitude = maxNudge * crystalProximityFade * boostFade;

            return lateralOffset.normalized * nudgeMagnitude;
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