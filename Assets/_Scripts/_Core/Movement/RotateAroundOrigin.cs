using UnityEngine;

namespace StarWriter.Movement
{
    public class RotateAroundOrigin : MonoBehaviour
    {
        [SerializeField] float speed = 2;
        [SerializeField] Vector3 rotationDirection = Vector3.up;

        void Update()
        {
            float speedT = speed * Time.deltaTime;
            transform.Rotate(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT);
            //transform.position = Quaternion.Euler(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT) * transform.position;
        }
    }
}