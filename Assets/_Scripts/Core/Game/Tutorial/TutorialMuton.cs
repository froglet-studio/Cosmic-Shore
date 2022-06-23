using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Tutorial 
{
    public class TutorialMuton : MonoBehaviour
    {
        float lastCollisionTime;
        float collisionCoolOff = 2f;

        public delegate void OnTutorialMutonCollisionEvent();
        public static event OnTutorialMutonCollisionEvent onMutonCollision;

        /// <summary>
        /// Generate a list of Box Collider Collision so the specific ones can be identified for use
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time > lastCollisionTime + collisionCoolOff)
            {
                lastCollisionTime = Time.time;
                onMutonCollision?.Invoke();
            }
        }

        /// <summary>
        /// Moves the Muton to an offset in front of the Tutorial Player
        /// </summary>
        public void MoveMuton(Transform playerTransform, Vector3 spawnPointOffset)
        {
            transform.position = playerTransform.position +
                                 playerTransform.right * spawnPointOffset.x +
                                 playerTransform.up * spawnPointOffset.y +
                                 playerTransform.forward * spawnPointOffset.z;
        }
    }
}