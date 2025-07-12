using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CosmicShore.Core;
using System;
using CosmicShore.Environment.FlowField;

namespace CosmicShore.Game.AI
{
    [Serializable]
    public class AIAbility
    {
        public ShipAction Ability;
        public float Duration;
        public float Cooldown;
    }

    public class AIPilot : MonoBehaviour
    {
        public float SkillLevel;

        //public float defaultThrottle = .6f;
        [SerializeField] float defaultThrottleHigh = .6f;
        [SerializeField] float defaultThrottleLow  = .6f;

        //public float defaultAggressiveness = .035f;
        [SerializeField] float defaultAggressivenessHigh = .035f;
        [SerializeField] float defaultAggressivenessLow  = .035f;

        //public float throttleIncrease = .001f;
        [SerializeField] float throttleIncreaseHigh = .001f;
        [SerializeField] float throttleIncreaseLow  = .001f;

        //public float avoidance = 2.5f;
        [SerializeField] float avoidanceHigh = 2.5f;
        [SerializeField] float avoidanceLow = 2.5f;

        //public float aggressivenessIncrease = .001f;
        [SerializeField] float aggressivenessIncreaseHigh = .001f;
        [SerializeField] float aggressivenessIncreaseLow  = .001f;

        [HideInInspector] public float throttle;
        [HideInInspector] public float aggressiveness;

        public float XSum;
        public float YSum;
        public float XDiff;
        public float YDiff;

        public float X;
        public float Y;

        public float defaultThrottle => Mathf.Lerp(defaultThrottleLow, defaultThrottleHigh, SkillLevel);
        public float defaultAggressiveness => Mathf.Lerp(defaultAggressivenessLow, defaultAggressivenessHigh, SkillLevel);
        float throttleIncrease => Mathf.Lerp(throttleIncreaseLow, throttleIncreaseHigh, SkillLevel);
        float avoidance => Mathf.Lerp(avoidanceLow, avoidanceHigh, SkillLevel);
        float aggressivenessIncrease => Mathf.Lerp(aggressivenessIncreaseLow, aggressivenessIncreaseHigh, SkillLevel);

        float targetUpdateFrequencySeconds = 2f;

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        [SerializeField] bool LookingAtCrystal;
        [SerializeField] bool ram;
        [SerializeField] bool drift;
        [HideInInspector] public bool SingleStickControls;

        [SerializeField] List<AIAbility> abilities;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };
        public bool AutoPilotEnabled { get; private set; }

        IShip _ship;
        IShipStatus _shipStatus => _ship.ShipStatus;

        float lastPitchTarget;
        float lastYawTarget;
        float lastRollTarget;

        RaycastHit hit;
        float maxDistance = 50f;
        float maxDistanceSquared;

        // [HideInInspector] 
        public Vector3 CrystalPosition;
        // [HideInInspector] 
        public Vector3 TargetPosition;
        Vector3 distance;

        [HideInInspector] public FlowFieldData flowFieldData;

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

        

        public void UpdateCellContent()
        {
            //Debug.Log($"NodeContentUpdated - transform.position: {transform.position}");
            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);

            if (activeCell == null)
                activeCell = CellControlManager.Instance.GetNearestCell(transform.position);

            var cellItems = activeCell.GetItems();
            float MinDistance = Mathf.Infinity;
            CellItem closestItem = null;

            foreach (var item in cellItems.Values)
            {
                // Debuffs are disguised as desireable to the other team
                // So, if it's good, or if it's bad but made by another team, go for it
                if (item.ItemType != ItemType.Buff &&
                    (item.ItemType != ItemType.Debuff || item.OwnTeam == _ship.ShipStatus.Team)) continue;
                var distance = Vector3.SqrMagnitude(item.transform.position - transform.position);
                if (distance < (MinDistance * MinDistance))
                {
                    closestItem = item;
                    MinDistance = distance;
                }
            }

            CrystalPosition = closestItem == null ? activeCell.transform.position : closestItem.transform.position;
        }


        public void AssignShip(IShip ship)
        {
            _ship = ship;
        }

        public void Initialize(bool enableAutoPilot)
        {
            AutoPilotEnabled = enableAutoPilot;
            Debug.Log($"AutoPilotStatus {AutoPilotEnabled}");
            if (!AutoPilotEnabled)
                return;
            maxDistanceSquared = maxDistance * maxDistance;
            aggressiveness = defaultAggressiveness;
            throttle = defaultThrottle;

            CornerBehaviors = new Dictionary<Corner, AvoidanceBehavior>() {
                { Corner.TopRight, new AvoidanceBehavior (raycastWidth, raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.BottomRight, new AvoidanceBehavior (raycastWidth, -raycastHeight, CounterClockwise, Vector3.zero ) },
                { Corner.BottomLeft, new AvoidanceBehavior (-raycastWidth, -raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
            };

            var activeNode = CellControlManager.Instance?.GetCellByPosition(transform.position);
            activeNode.RegisterForUpdates(this);

            foreach
                (var ability in abilities)
            {
                StartCoroutine(UseAbilityCoroutine(ability));
            }
            StartCoroutine(SetTargetCoroutine());
        }


        void Update()
        {
            if (AutoPilotEnabled)
            {
                distance = TargetPosition - transform.position;
                Vector3 desiredDirection = distance.normalized;

                LookingAtCrystal = Vector3.Dot(desiredDirection, _shipStatus.Course) >= .9f;
                if (LookingAtCrystal && drift && !_shipStatus.Drifting)
                {
                    _shipStatus.Course = desiredDirection;
                    _ship.PerformShipControllerActions(InputEvents.LeftStickAction);
                    desiredDirection *= -1;
                }
                else if (LookingAtCrystal && _shipStatus.Drifting) desiredDirection *= -1;
                else if (_shipStatus.Drifting) _ship.StopShipControllerActions(InputEvents.LeftStickAction);


                if (distance.magnitude < float.Epsilon) // Avoid division by zero
                    return;

                Vector3 combinedLocalCrossProduct = Vector3.zero;
                float sqrMagnitude = distance.sqrMagnitude;
                Vector3 crossProduct = Vector3.Cross(transform.forward, desiredDirection);
                Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
                combinedLocalCrossProduct += localCrossProduct;

                aggressiveness = 100f;  // Multiplier to mitigate vanishing cross products that cause aimless drift
                float angle = Mathf.Asin(Mathf.Clamp(combinedLocalCrossProduct.sqrMagnitude * aggressiveness / Mathf.Min(sqrMagnitude, maxDistance), -1f, 1f)) * Mathf.Rad2Deg;

                if (SingleStickControls)
                {
                    X = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                    Y = -Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
                }
                else
                {
                    YSum = Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
                    XSum = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
                    YDiff = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);

                    XDiff = (LookingAtCrystal && ram) ? 1 : Mathf.Clamp(throttle, 0, 1);
                }
   
                //aggressiveness += aggressivenessIncrease * Time.deltaTime;
                throttle += throttleIncrease * Time.deltaTime;
            }
        }
        Vector3 ShootLaser(Vector3 position)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out hit, maxDistance))
            {
                Debug.DrawLine(transform.position + position, hit.point, Color.red);
                return hit.point - (transform.position + position);
            }

            Debug.DrawLine(transform.position + position, transform.position + position + transform.forward * maxDistance, Color.green);
            return transform.forward * maxDistance - (transform.position + position);
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


        IEnumerator UseAbilityCoroutine(AIAbility action) 
        {
            yield return new WaitForSeconds(3);
            while (true)
            {
                action.Ability.StartAction();
                yield return new WaitForSeconds(action.Duration);
                action.Ability.StopAction();
                yield return new WaitForSeconds(action.Cooldown);
            }
        }


        IEnumerator SetTargetCoroutine()
        {
            List<ShipTypes> aggressiveShips = new List<ShipTypes> 
            { 
                ShipTypes.Rhino, 
                ShipTypes.Sparrow,
            };

            var rand = new System.Random();

            var activeNode = CellControlManager.Instance.GetCellByPosition(transform.position);  // Assume activeNode can't change.
            if (activeNode == null)
                activeNode = CellControlManager.Instance.GetNearestCell(transform.position);


            while (true)
            {
                if (aggressiveShips.Contains(_ship.ShipStatus.ShipType) && (activeNode.ControllingTeam != Teams.None)) {
                    if ((_ship.ShipStatus.Team == activeNode.ControllingTeam) || (rand.NextDouble() < 0.5))  // Your team is winning.
                    {
                        TargetPosition = CrystalPosition;
                    }
                    else
                    {
                        TargetPosition = activeNode.GetExplosionTarget(activeNode.ControllingTeam);  // Block centroid belonging to the winning team
                    }
                }
                else
                {
                    TargetPosition = CrystalPosition;
                }
                yield return new WaitForSeconds(targetUpdateFrequencySeconds);
            }
        }


        float SigmoidResponse(float input)
        {
            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
            return output;
        }

    }
}