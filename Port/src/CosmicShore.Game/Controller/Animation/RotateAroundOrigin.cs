// Ported verbatim from Assets/_Scripts/Controller/Animation/RotateAroundOrigin.cs
// (camera arc 2026-07-08). Mechanical substitutions (README):
// UnityEngine → CosmicShore.Engine. FULLY LIVE — pure transform math.

using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class RotateAroundOrigin : MonoBehaviour
    {
        [SerializeField] float speed = 2;
        [SerializeField] Vector3 rotationDirection = Vector3.up;

        void Update()
        {
            float speedT = speed * Time.deltaTime;
            transform.position = Quaternion.Euler(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT) * transform.position;
        }
    }
}
