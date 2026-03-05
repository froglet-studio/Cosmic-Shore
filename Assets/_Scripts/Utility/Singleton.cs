using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

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
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
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
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
        }
    }

    public abstract class SingletonScriptableObject<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance = null;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    T[] results = Resources.FindObjectsOfTypeAll<T>();
                    if (results.Length == 0)
                    {
                        CSDebug.LogError("SingletonScriptableObject -> Instance -> results length is 0 for type" + typeof(T).ToString() + ".");
                        return null;

                    }
                    if (results.Length > 1)
                    {
                        CSDebug.LogError("SingletonScriptableObject -> Instance -> results length is greater than for type" + typeof(T).ToString() + ".");
                        return null;

                    }
                    _instance = results[0];
                }
                return _instance;
            }
        }
    }
}