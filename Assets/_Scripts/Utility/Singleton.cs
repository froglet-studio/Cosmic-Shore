using Unity.Netcode;
using UnityEngine;

/*
        Generic classes for the use of singleton
        there are 3 types:
        - MonoBehaviour -> for the use of singleton to normal MonoBehaviours
        - NetworkBehaviour -> for the use of singleton that uses NetworkBehaviours
        - Persistent -> when we need to make sure that the object is not destroyed during the session.
*/

namespace CosmicShore.Utilities
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;

                print("Instantiate Singleton : " + Instance);
            }
            else
            {
                print("Destroy Singleton: " + gameObject.name);

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

                print("Instantiate SingletonPersistent : " + Instance);
            }
            else
            {
                print("Destroy SingletonPersistent: " + gameObject.name);

                Destroy(gameObject);
            }
        }
    }

    public class SingletonNetwork<T> : NetworkBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;

                print("Instantiate SingletonNetwork: " + Instance);
            }
            else
            {
                print("Destroy SingletonNetwork: " + Instance);

                Destroy(gameObject);
            }
        }
    }

    public class SingletonNetworkPersistent<T> : NetworkBehaviour where T : Component
    {
        public static T Instance { get; private set; }

        public virtual void Awake()
        {
            if (Instance == null)
            {
                Instance = this as T;
                DontDestroyOnLoad(this);

                print("Instantiate SingletonNetworkPersistent: " + Instance);
            }
            else
            {
                print("Destroy SingletonNetworkPersistent: " + Instance);

                Destroy(gameObject);
            }
        }
    }
}