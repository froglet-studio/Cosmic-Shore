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
    }
}
