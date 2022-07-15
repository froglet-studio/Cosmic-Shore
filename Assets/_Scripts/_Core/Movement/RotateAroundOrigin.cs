using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Movement
{
    public class RotateAroundOrigin : MonoBehaviour
    {
        [SerializeField]
        private float speed = 2;
        [SerializeField]
        private Vector3 rotationDirection = Vector3.up;


        // Update is called once per frame
        void Update()
        {
            float speedT = speed * Time.deltaTime;
            transform.Rotate(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT);
            transform.position = Quaternion.Euler(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT) * transform.position;
        }
    }
}

