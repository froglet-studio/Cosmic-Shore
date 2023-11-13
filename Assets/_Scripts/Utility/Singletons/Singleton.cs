using UnityEngine;

namespace CosmicShore.Utility.Singleton
{
    /// <remarks>
    /// Creates a Singleton GameObject which is NOT persistent thru scenes
    /// </remarks>
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;

            }
            else
                Destroy(gameObject);
        }
    }
}