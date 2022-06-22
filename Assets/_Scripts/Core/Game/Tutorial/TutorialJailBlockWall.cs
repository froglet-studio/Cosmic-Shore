using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Tutorial
{
    public class TutorialJailBlockWall : MonoBehaviour
    {
        // TODO: remove the need for this to know about tutorialManager by using JailBlockCollision event
        [SerializeField]
        TutorialManager tutorialManager;

        public delegate void OnJailBlockCollisionEvent();
        public static event OnJailBlockCollisionEvent onJailBlockCollision;

        List<Collision> collisions;

        // Start is called before the first frame update
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
        /// Collision occurs only by passing thru the bars opening
        /// </summary>
        /// <param name="other"></param>
        void Collide(Collider other)
        {
            onJailBlockCollision?.Invoke();
            gameObject.SetActive(false);
        }

        public void MoveJailBlockWall(Transform playerTransform, Vector3 spawnPointOffset)
        {
            transform.position = playerTransform.position +
                                 playerTransform.right * spawnPointOffset.x +
                                 playerTransform.up * spawnPointOffset.y +
                                 playerTransform.forward * spawnPointOffset.z;
            transform.rotation = Quaternion.LookRotation(playerTransform.position - transform.position, playerTransform.up);
        }
    }
}
                

