using UnityEngine;
using TMPro;
using Cinemachine;

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

         enum corners 
        {
            topRight,
            bottomRight,
            bottomLeft,
            topLeft,
        };

        Vector3 topRight;
        Vector3 bottomRight;
        Vector3 bottomLeft;
        Vector3 topLeft;

        ShipData shipData;

        RaycastHit hit;
        float maxDistance = 50f;

        //RaycastHit hit;

        [SerializeField] public Transform muton;
        Vector3 distance;

        [SerializeField] FlowFieldData flowFieldData;

        // Start is called before the first frame update
        void Start()
        {
            lerp = defaultLerp;
            throttle = defaultThrottle;
            shipData = GetComponent<ShipData>();

            topRight = bottomRight = bottomLeft = topLeft = transform.forward;
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

            //rotate away from blocks
            ShootLaser(raycastWidth * transform.right + raycastHeight * transform.up, (int)corners.topRight);
            transform.localRotation = TurnAway(topRight, -transform.up - (transform.right / topRight.magnitude));

            ShootLaser(raycastWidth * transform.right - raycastHeight * transform.up, (int)corners.bottomRight);
            transform.localRotation = TurnAway(bottomRight, -transform.up + (transform.right / topRight.magnitude));

            ShootLaser(-raycastWidth * transform.right - raycastHeight * transform.up, (int)corners.bottomLeft);
            transform.localRotation = TurnAway(bottomLeft, -transform.up - (transform.right / topRight.magnitude));

            ShootLaser(-raycastWidth * transform.right + raycastHeight * transform.up, (int)corners.topLeft);
            transform.localRotation = TurnAway(topLeft, -transform.up + (transform.right / topRight.magnitude));

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


        void ShootLaser(Vector3 position, int corner)
        {
            if (Physics.Raycast(transform.position + position, transform.forward, out hit, maxDistance))
            {
                switch (corner)
                {
                    case 0:
                        topRight = hit.point - transform.position;
                        break;
                    case 1:
                        bottomRight = hit.point - transform.position;
                        break;
                    case 2:
                        bottomLeft = hit.point - transform.position;
                        break;
                    case 3:
                        topLeft = hit.point - transform.position;
                        break;
                }
                Debug.DrawLine(transform.position + position, hit.point, Color.red);
            }
            else
            {
                switch (corner)
                {
                    case 0:
                        topRight = transform.forward - transform.position;
                        break;
                    case 1:
                        bottomRight = transform.forward - transform.position;
                        break;
                    case 2:
                        bottomLeft = transform.forward - transform.position;
                        break;
                    case 3:
                        topLeft = transform.forward - transform.position;
                        break;
                }
                Debug.DrawLine(transform.position + position, (transform.position + position) + transform.forward * maxDistance, Color.green);
            }
        }
    }
}
