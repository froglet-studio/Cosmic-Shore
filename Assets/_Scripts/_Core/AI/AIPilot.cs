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
        public float defaultLerp = .035f;

        public float throttleIncrease = .001f;
        public float lerpIncrease = .001f;

        public float throttle;
        public float lerp;

        public float avoidance = 2.5f;

        public float XSum;
        public float YSum;
        public float XDiff;
        public float YDiff;

        int frameCount = 0;

        float LevelAwareAvoidance { get { return avoidance + (DifficultyLevel * .3f); } }
        float LevelAwareDefaultThrottle { get { return defaultThrottle * DifficultyLevel * .3f; } }
        float LevelAwareDefaultLerp { get { return defaultLerp * DifficultyLevel * .3f; } }
        float LevelAwareThrottleIncrease { get { return throttleIncrease * DifficultyLevel * .3f; } }
        float LevelAwareLerpIncrease { get { return lerpIncrease * DifficultyLevel * .3f; } }

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
            if (autoPilotEnabled) { ship.inputController.AutoPilotEnabled = true; }
            lerp = defaultLerp;
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
            ///distance to Crystal 
            distance = CrystalTransform.position - transform.position;

            ///rotate toward Crystal
            Quaternion newRotation = Quaternion.Lerp(transform.localRotation,
                                                         Quaternion.LookRotation(distance, transform.up),
                                                         1);//Mathf.Clamp(lerp/distance.magnitude,.1f,.9f));

            //foreach (Corner corner in Enum.GetValues(typeof(Corner)))
            //{
            //    var behavior = CornerBehaviors[corner];
            //    behavior.direction = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);
            //    newRotation = TurnAway(newRotation, behavior.direction, 
            //                                       -transform.up + (behavior.spin * (transform.right / behavior.direction.magnitude)), 
            //                                       avoidance / behavior.direction.magnitude);
            //}

            var rotationOperator = newRotation * Quaternion.Inverse(transform.rotation);
            XSum = Mathf.Clamp(LinearStep(rotationOperator.eulerAngles.y), -1, 1);
            YSum = Mathf.Clamp(LinearStep(rotationOperator.eulerAngles.x), -1, 1);
            YDiff = Mathf.Clamp(LinearStep(rotationOperator.eulerAngles.z), -1, 1);
                    ///get better
            lerp += lerpIncrease * Time.deltaTime;
            throttle += throttleIncrease * Time.deltaTime;

            ///Move ship velocityDirection
            //Vector3 flowVector = flowFieldData.FlowVector(transform);
            //shipData.Speed = throttle;

            //shipData.Course = transform.forward;

            XDiff = Mathf.Clamp(throttle,0,1);
            //transform.position += (shipData.Speed * shipData.Course) * Time.deltaTime;

            //shipData.blockRotation = transform.rotation;
        }

        float LinearStep(float input) 
        {
            if (input < 180) return -input / 180;
            else return -input / 180 + 2;
        }

        Quaternion TurnAway(Quaternion initial, Vector3 direction, Vector3 down, float lerp)
        {
            return Quaternion.Lerp(initial,
                    Quaternion.Inverse(Quaternion.LookRotation(direction, down)),
                       lerp);
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