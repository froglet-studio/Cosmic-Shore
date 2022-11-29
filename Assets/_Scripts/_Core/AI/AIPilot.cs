using UnityEngine;
using System.Collections.Generic;
using System;

namespace StarWriter.Core.Input
{
    public class AIPilot : MonoBehaviour
    {
        public float defaultThrottle = .6f;
        public float defaultLerp = .035f;

        public float throttleIncrease = .001f;
        public float lerpIncrease = .001f;

        public float throttle;
        public float lerp;

        public float avoidance = 2.5f;

        [SerializeField] float raycastHeight;
        [SerializeField] float raycastWidth;

        enum Corner 
        {
            TopRight,
            BottomRight,
            BottomLeft,
            TopLeft,
        };

        ShipData shipData;

        RaycastHit hit;
        float maxDistance = 50f;

        [SerializeField] public Transform muton;
        Vector3 distance;

        [SerializeField] FlowFieldData flowFieldData;

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

        // Start is called before the first frame update
        void Start()
        {
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

        // Update is called once per frame
        void Update()
        {
            ////distance to Crystal 
            distance = muton.position - transform.position;
            
            ///rotate toward Crystal
            transform.localRotation = Quaternion.Lerp(transform.localRotation,
                                                         Quaternion.LookRotation(distance, transform.up),
                                                         lerp/distance.magnitude * Time.deltaTime);

            foreach (Corner corner in Enum.GetValues(typeof(Corner)))
            {
                var behavior = CornerBehaviors[corner];
                behavior.direction = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);
                transform.localRotation = TurnAway(behavior.direction, 
                                                   -transform.up + (behavior.spin * (transform.right / behavior.direction.magnitude)), 
                                                   avoidance / behavior.direction.magnitude * Time.deltaTime);
            }

            //get better
            lerp += lerpIncrease * Time.deltaTime;
            throttle += throttleIncrease * Time.deltaTime;

            //Move ship forward
            Vector3 flowVector = flowFieldData.FlowVector(transform);
            transform.position += transform.forward * Time.deltaTime * throttle + flowVector;

            shipData.speed = throttle;
        }

        Quaternion TurnAway(Vector3 direction, Vector3 down, float lerp)
        {
            return Quaternion.Lerp(transform.localRotation,
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