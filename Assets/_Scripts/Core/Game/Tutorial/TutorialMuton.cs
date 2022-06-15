using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Tutorial 
{
    public class TutorialMuton : MonoBehaviour
    {
        List<Collision> collisions;

        public delegate void OnTutorialMutonCollisionEvent();
        public static event OnTutorialMutonCollisionEvent onMutonCollision;

        // Start is called before the first frame update
        void Start()
        {
            collisions = new List<Collision>();
        }

        /// <summary>
        /// Generate a list of Box Collider Collision so the specific ones can be identified for use
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            collisions.Add(collision);
        }

        private void Update()
        {
            if (collisions.Count > 0)
            {
                Collide(collisions[0].collider);
                collisions.Clear();
            }
        }

        /// <summary>
        /// Handles Tutorial Muton collision logic
        /// </summary>
        /// <param name="other"></param>
        void Collide(Collider other)
        {
            onMutonCollision?.Invoke();
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


