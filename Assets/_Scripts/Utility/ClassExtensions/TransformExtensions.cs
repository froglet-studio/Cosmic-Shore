using UnityEngine;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Set game object position, rotation and local scale.
        /// </summary>
        /// <param name="transform">Game object transform</param>
        /// <param name="position">Game object position</param>
        /// <param name="rotation">Game object rotation</param>
        /// <param name="scale">Game object local scale</param>
        public static void SetFullProperties(this Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
        }
        
        /// <summary>
        /// A helper method to convert local position, relativeto a transform, into a to global position.
        /// </summary>
        /// <param name="transform">The Transform to call this method</param>
        /// <param name="localPosition">Vector3 local position</param>
        /// <returns>Vector3 global position</returns>
        public static Vector3 ToGlobal(this Transform transform, Vector3 localPosition)
        {
            return localPosition.x * transform.right + localPosition.y * transform.up + localPosition.z * transform.forward + transform.position;
        }
    }
}
