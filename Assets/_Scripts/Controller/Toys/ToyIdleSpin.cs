using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Gentle idle rotation for toy bodies (crystal-spike cones catch the light, jacks tumble).
    /// Motion without scale change - the calm "alive" read the rest of the game's pickups have,
    /// instead of a pulsing throb.
    /// </summary>
    public class ToyIdleSpin : MonoBehaviour
    {
        [SerializeField, Tooltip("Local-space axis to rotate around.")]
        Vector3 localAxis = Vector3.forward;

        [SerializeField, Tooltip("Rotation speed, degrees per second.")]
        float degreesPerSecond = 45f;

        public void Configure(Vector3 axis, float speed)
        {
            localAxis = axis;
            degreesPerSecond = speed;
        }

        void Update()
        {
            transform.localRotation *= Quaternion.AngleAxis(degreesPerSecond * Time.deltaTime, localAxis);
        }
    }
}
