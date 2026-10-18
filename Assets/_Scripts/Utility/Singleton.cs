using CosmicShore.Core;
using UnityEngine;
using CosmicShore.Utility;

/*
        Generic classes for the use of singleton
        there are 2 types:
        - MonoBehaviour -> for the use of singleton to normal MonoBehaviours
        - Persistent -> when we need to make sure that the object is not destroyed during the session.
*/

namespace CosmicShore.Utility
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;
            }
            else if (!ApplicationLifecycleManager.IsQuitting)
            {
                Destroy(gameObject);
            }
        }
    }

    public class SingletonPersistent<T> : MonoBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;
                DontDestroyOnLoad(this);
            }
            else if (!ApplicationLifecycleManager.IsQuitting)
            {
                Destroy(gameObject);
            }
        }
    }
}
