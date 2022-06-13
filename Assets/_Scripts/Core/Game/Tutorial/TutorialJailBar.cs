using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Tutorial
{
    public class TutorialJailBar : MonoBehaviour
    {
        [SerializeField]
        private GameObject player;
        [SerializeField]
        private GameObject jailBlockWall;

        List<Collision> collisions;
        private readonly Vector3 spawnPointOffset;

        void Start()
        {
            collisions = new List<Collision>();
        }

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
        /// Collision occurs when Player hits the bar
        /// </summary>
        /// <param name="other"></param>
        void Collide(Collider other)
        {
            MoveJailBlockWall();

        }
        /// <summary>
        /// Moves parent object Jail Block Wall if a bar is hit before the test passed collider
        /// </summary>
        void MoveJailBlockWall()
        {

            jailBlockWall.transform.position = player.transform.position +
                                               player.transform.right * spawnPointOffset.x +
                                               player.transform.up * spawnPointOffset.y +
                                               player.transform.forward * spawnPointOffset.z;
        }
    }
}

