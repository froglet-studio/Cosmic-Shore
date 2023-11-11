using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Game.Animation
{
    public class Pan : MonoBehaviour
    {
        [SerializeField] float speed = -2;
        [SerializeField] Vector3 rotationDirection = Vector3.up;

        bool spinning = false;

        void Update()
        {
            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
            {
                spinning = true;
            }
            if (spinning)
            {
                float speedT = speed * Time.deltaTime;
                transform.Rotate(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT);
            }
        }
    }
}