using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.UIElements;

namespace StarWriter.Core.IO
{
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

        [HideInInspector] public float defaultThrottle { get { return Mathf.Lerp(defaultThrottleLow, defaultThrottleHigh, SkillLevel); } }
        [HideInInspector] public float defaultAggressiveness { get { return Mathf.Lerp(defaultAggressivenessLow, defaultAggressivenessHigh, SkillLevel); } }
        float throttleIncrease { get { return Mathf.Lerp(throttleIncreaseLow, throttleIncreaseHigh, SkillLevel); } }
        float avoidance { get { return Mathf.Lerp(avoidanceLow, avoidanceHigh, SkillLevel); } }
        float aggressivenessIncrease { get { return Mathf.Lerp(aggressivenessIncreaseLow, aggressivenessIncreaseHigh, SkillLevel); } }

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        public bool AutoPilotEnabled;
        [SerializeField] bool LookingAtCrystal = false;
        [SerializeField] bool ram = false;
        [SerializeField] bool drift = false;

        [SerializeField] bool useAbility = false;
        [SerializeField] float abilityCooldown;
        [SerializeField] ShipActionAbstractBase ability;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };

        ShipStatus shipData;
        Ship ship;

        float lastPitchTarget;
        float lastYawTarget;
        float lastRollTarget;

        RaycastHit hit;
        float maxDistance = 50f;
        float maxDistanceSquared;

        // TODO: rename to 'TargetTransform'
        public Transform CrystalTransform;
        Vector3 distance;

        public FlowFieldData flowFieldData;

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

        

        public void NodeContentUpdated()
        {
            Debug.Log($"NodeContentUpdated - transform.position: {transform.position}");
            var activeNode = NodeControlManager.Instance.GetNodeByPosition(transform.position);

            // TODO: null checks
            //CrystalTransform = activeNode.GetClosestItem(transform.position).transform;

            var nodeItems = activeNode.GetItems();
            float MinDistance = Mathf.Infinity;
            NodeItem closestItem = null;

            foreach (var item in nodeItems.Values)
            { 
                // Debuffs are disguised as desireable to the other team
                // So, if it's good, or if it's bad but made by another team, go for it
                if (item.ItemType == ItemType.Buff || (item.ItemType == ItemType.Debuff && item.Team != ship.Team))
                {
                    var distance = Vector3.Distance(item.transform.position, transform.position);
                    if (distance < MinDistance)
                    {
                        closestItem = item;
                        MinDistance = distance;
                    }
                }
            }

            CrystalTransform = closestItem == null ? activeNode.transform : closestItem.transform;
        }

        void Start()
        {
            maxDistanceSquared = maxDistance * maxDistance;
            ship = GetComponent<Ship>();
            if (AutoPilotEnabled)
            {
                ship.InputController.AutoPilotEnabled = true;
                ship.ShipStatus.AutoPilotEnabled = true;
            }
            aggressiveness = defaultAggressiveness;
            throttle = defaultThrottle;
            shipData = GetComponent<ShipStatus>();

            CornerBehaviors = new Dictionary<Corner, AvoidanceBehavior>() {
                { Corner.TopRight, new AvoidanceBehavior (raycastWidth, raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.BottomRight, new AvoidanceBehavior (raycastWidth, -raycastHeight, CounterClockwise, Vector3.zero ) },
                { Corner.BottomLeft, new AvoidanceBehavior (-raycastWidth, -raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
            };

            var activeNode = NodeControlManager.Instance.GetNodeByPosition(transform.position);
            activeNode.RegisterForUpdates(this);

        }

        void Update()
        {
            if (AutoPilotEnabled)
            {
                if (useAbility) StartCoroutine(UseAbilityCoroutine(ability));
                Vector3 distance = CrystalTransform.position - transform.position;

                Vector3 desiredDirection = distance.normalized;
                LookingAtCrystal = Vector3.Dot(desiredDirection, shipData.Course) >= .93;
                if (LookingAtCrystal && drift)
                {
                    shipData.Drifting = true;
                    desiredDirection *= -1;
                }
                else shipData.Drifting = false;

                if (distance.magnitude < float.Epsilon) // Avoid division by zero
                    return;

                Vector3 combinedAdjustment = Vector3.zero;
                Dictionary<Corner, Vector3> obstacleDirections = new Dictionary<Corner, Vector3>();


                foreach (Corner corner in Enum.GetValues(typeof(Corner)))
                {
                    var behavior = CornerBehaviors[corner];
                    Vector3 laserHitDirection = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);
                    float scaledAvoidance = Mathf.Clamp(avoidance / laserHitDirection.sqrMagnitude, -1, 1);
                    Vector3 adjustment = CalculatePitchAndYawAdjustments(desiredDirection, laserHitDirection, scaledAvoidance);
                    combinedAdjustment += adjustment;

                    obstacleDirections[corner] = laserHitDirection;
                }

                combinedAdjustment.x = -Mathf.Clamp(combinedAdjustment.x*4  / Mathf.Min(distance.magnitude, maxDistance), -1, 1); // Pitch
                combinedAdjustment.y = Mathf.Clamp(combinedAdjustment.y*4  / Mathf.Min(distance.magnitude, maxDistance), -1, 1); // Yaw

                // Roll adjustment
                float rollAdjustment = CalculateRollAdjustment(obstacleDirections);
                //combinedAdjustment.z = Mathf.Clamp(rollAdjustment, -1, 1); // Roll

                XSum = combinedAdjustment.x;
                YSum = combinedAdjustment.y;
                YDiff = Mathf.Clamp(CalculateRollAdjustment(obstacleDirections), -1, 1);
                XDiff = (LookingAtCrystal && ram) ? 1 : Mathf.Clamp(throttle, 0, 1);

                ///get better
                aggressiveness += aggressivenessIncrease * Time.deltaTime;
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
            else
            {
                Debug.DrawLine(transform.position + position, transform.position + position + transform.forward * maxDistance, Color.green);
                return transform.forward * maxDistance - (transform.position + position);
            }
        }

        Vector3 CalculatePitchAndYawAdjustments(Vector3 originalDirection, Vector3 obstacleDirection, float avoidanceFactor)
        {
            Vector3 adjustment = Vector3.zero;

            // Primary objective adjustments
            adjustment.y = Vector3.Dot(originalDirection, transform.right);   // Yaw
            adjustment.x = Vector3.Dot(originalDirection, transform.up);    // Pitch

            //if (obstacleDirection != Vector3.zero) // If obstacle detected
            //{
            //    float dotProduct = Vector3.Dot(originalDirection, obstacleDirection.normalized);
            //    if (dotProduct > 0.2) // Obstacle is in the desired direction or slightly off-course
            //    {
            //        // Calculate obstacle-based yaw and pitch adjustments
            //        adjustment.y += Vector3.Dot(obstacleDirection, transform.right) * avoidanceFactor;   // Yaw
            //        adjustment.x += Vector3.Dot(obstacleDirection, transform.up) * avoidanceFactor;     // Pitch
            //    }
            //}

            return adjustment;
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


        IEnumerator UseAbilityCoroutine(ShipActionAbstractBase action) 
        {
            while (useAbility)
            {
                yield return new WaitForSeconds(abilityCooldown); // wait first to give the resource system time to load
                action.StartAction();
            }
            action.StopAction();
        }


        float SigmoidResponse(float input)
        {
            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
            return output;
        }

    }
}