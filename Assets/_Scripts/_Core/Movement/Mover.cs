using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Use to move a GameObject in the forward direction
/// </summary>

namespace StarWriter.Movement
{
    public class Mover : MonoBehaviour
    {
        [SerializeField]
        private float speed = 5f;


        // Update is called once per frame
        void Update()
        {
            //Player Gyro
            if (UnityEngine.Input.gyro.userAcceleration.magnitude > 2 && Vector3.Dot(UnityEngine.Input.gyro.userAcceleration,transform.forward)>.5)
            {
                transform.position += speed * UnityEngine.Input.gyro.userAcceleration;
            }
        }
    }
}

