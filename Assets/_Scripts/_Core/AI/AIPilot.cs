using UnityEngine;
using System.Collections.Generic;
using System;

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

        public bool autoPilotEnabled;

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

        void Start()
        {
            ship = GetComponent<Ship>();
            if (autoPilotEnabled) { ship.InputController.AutoPilotEnabled = true; }
            aggressiveness = defaultAggressiveness;
            throttle = defaultThrottle;
            shipData = GetComponent<ShipStatus>();

            CornerBehaviors = new Dictionary<Corner, AvoidanceBehavior>() {
                { Corner.TopRight, new AvoidanceBehavior (raycastWidth, raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.BottomRight, new AvoidanceBehavior (raycastWidth, -raycastHeight, CounterClockwise, Vector3.zero ) },
                { Corner.BottomLeft, new AvoidanceBehavior (-raycastWidth, -raycastHeight, Clockwise, Vector3.zero ) },
                { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
            };
        }

        void Update()
        {
            Vector3 distance = CrystalTransform.position - transform.position;
            Vector3 desiredDirection = distance.normalized;

            if (distance.magnitude < float.Epsilon) // Avoid division by zero
                return;

            Vector3 combinedLocalCrossProduct = Vector3.zero;
            

            foreach (Corner corner in Enum.GetValues(typeof(Corner)))
            {
                var behavior = CornerBehaviors[corner];
                Vector3 laserHitDirection = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);
                float clampedLerp = Mathf.Clamp(avoidance / laserHitDirection.magnitude, 0, 0.9f);
                Vector3 adjustedDirection = TurnAway(desiredDirection, laserHitDirection, (-transform.up + (behavior.spin * transform.right)) , clampedLerp); // TODO: avoidance is wip
                Vector3 crossProduct = Vector3.Cross(transform.forward, adjustedDirection);
                Vector3 localCrossProduct = transform.InverseTransformDirection(crossProduct);
                combinedLocalCrossProduct += localCrossProduct;
            }
            
            float angle = Mathf.Asin(Mathf.Clamp(combinedLocalCrossProduct.magnitude * aggressiveness/ Mathf.Min(distance.magnitude, maxDistance*10), -1f, 1f)) * Mathf.Rad2Deg;

            YSum = Mathf.Clamp(angle * combinedLocalCrossProduct.x, -1, 1);
            XSum = Mathf.Clamp(angle * combinedLocalCrossProduct.y, -1, 1);
            YDiff = Mathf.Clamp(angle * combinedLocalCrossProduct.z, -1, 1);
            ///get better
            aggressiveness += aggressivenessIncrease * Time.deltaTime;
            throttle += throttleIncrease * Time.deltaTime;

  
            XDiff = Mathf.Clamp(throttle,0,1);
           
        }

        Vector3 TurnAway(Vector3 originalDirection, Vector3 obstacleDirection, Vector3 rotationDirection, float avoidanceFactor)
        {
            if (obstacleDirection == Vector3.zero) // No obstacle detected
                return originalDirection;

            float dotProduct = Vector3.Dot(originalDirection, obstacleDirection.normalized);
            if (dotProduct > 0) // Obstacle is in the direction we want to move
                return originalDirection + (rotationDirection * avoidanceFactor);

            return originalDirection;
        }

        float SigmoidResponse(float input)
        {
            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
            return output;
        }


        Vector3 ShootLaser(Vector3 position)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out hit, maxDistance))
            {
                Debug.DrawLine(transform.position + position, hit.point, Color.red);
                return hit.point - transform.position;
            }
            else
            {
                Debug.DrawLine(transform.position + position, (transform.position + position) + transform.forward * maxDistance, Color.green);
                return transform.forward - transform.position;
            }
        }
    }
}