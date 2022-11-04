using UnityEngine;
using TMPro;
using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace StarWriter.Core.Input
{
    public class AiShipController : MonoBehaviour
    {
        public float defaultThrottle = .6f;
        public float defaultLerp = .035f;

        public float throttleIncrease = .001f;
        public float lerpIncrease = .001f;

        public float throttle;
        public float lerp;

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
            ////distance to muton 
            distance = muton.position - transform.position;
            
            ///rotate toward muton
            transform.localRotation = Quaternion.Lerp(transform.localRotation,
                                                         Quaternion.LookRotation(distance, transform.up),
                                                         lerp/distance.magnitude);

            foreach (Corner corner in Enum.GetValues(typeof(Corner)))
            {
                var behavior = CornerBehaviors[corner];
                behavior.direction = ShootLaser(behavior.width * transform.right + behavior.height * transform.up);
                transform.localRotation = TurnAway(behavior.direction, -transform.up + (behavior.spin * (transform.right / behavior.direction.magnitude)));
            }

            //get better
            lerp += lerpIncrease;
            throttle += throttleIncrease;

            //Move ship forward
            Vector3 flowVector = flowFieldData.FlowVector(transform);
            transform.position += transform.forward * Time.deltaTime * throttle + flowVector;

            shipData.speed = throttle;
        }

        Quaternion TurnAway(Vector3 direction, Vector3 down)
        {
            return Quaternion.Lerp(transform.localRotation,
                    Quaternion.Inverse(Quaternion.LookRotation(direction, down)),
                       .4f/direction.magnitude);
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