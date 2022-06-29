using UnityEngine;

namespace StarWriter.Core.Tutorial
{
    public class TutorialJailBlockWall : MonoBehaviour
    {
        public delegate void OnJailBlockCollisionEvent();
        public static event OnJailBlockCollisionEvent onJailBlockCollision;

        /// <summary>
        /// Collision occurs only by passing thru the bars opening
        /// </summary>
        /// <param name="other"></param>
        public void Collide(Collider other)
        {
            onJailBlockCollision?.Invoke();
            //gameObject.SetActive(false);
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