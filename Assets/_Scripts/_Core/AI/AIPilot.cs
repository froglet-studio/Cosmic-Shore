using UnityEngine;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

namespace StarWriter.Core.Input
{
    public class AIPilot : MonoBehaviour
    {
        public int DifficultyLevel;

        public float defaultThrottle = .6f;
        public float defaultAggressiveness = .035f;

        public float throttleIncrease = .001f;
        public float aggressivenessIncrease = .001f;

        public float throttle;
        public float aggressiveness = .1f;

        public float avoidance = 2.5f;

        public float XSum;
        public float YSum;
        public float XDiff;
        public float YDiff;

        int frameCount = 0;

        float LevelAwareAvoidance { get { return avoidance + (DifficultyLevel * .3f); } }
        float LevelAwareDefaultThrottle { get { return defaultThrottle * DifficultyLevel * .3f; } }
        float LevelAwareDefaultLerp { get { return defaultAggressiveness * DifficultyLevel * .3f; } }
        float LevelAwareThrottleIncrease { get { return throttleIncrease * DifficultyLevel * .3f; } }
        float LevelAwareLerpIncrease { get { return aggressivenessIncrease * DifficultyLevel * .3f; } }

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        [SerializeField] bool autoPilotEnabled;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };

        ShipData shipData;
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
            shipData = GetComponent<ShipData>();

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
            float clampedLerp = Mathf.Clamp(avoidance / distance.magnitude, 0, 0.9f);

            foreach (Corner corner in Enum.GetValues(typeof(Corner)))
            {
                var behavior = CornerBehaviors[corner];
                Vector3 laserHitDirection = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);

                Vector3 adjustedDirection = TurnAway(desiredDirection, laserHitDirection, (-transform.up + (behavior.spin * transform.right)) / laserHitDirection.magnitude, clampedLerp); // TODO: avoidance is wip
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
                return originalDirection + rotationDirection * avoidanceFactor;

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