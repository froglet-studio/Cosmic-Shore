using UnityEngine;

namespace Amoebius.Utility.Singleton
{
    /// <remarks>
    /// Creates a Singleton GameObject which is NOT persistent thru scenes
    /// </remarks>
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if(_instance == null)
                {
                    var objs = FindObjectOfType(typeof(T)) as T[];
                    if(objs.Length > 0)
                    {
                        _instance = objs[0];
                    }
                    if(objs.Length > 1)
                    {
                        Debug.Log("Warning : To many " + (typeof(T)).Name + " are in the scene.");
                    }
                    if(_instance == null)
                    {
                        GameObject obj = new GameObject();
                        obj.name = typeof(T).Name;
                        _instance = obj.AddComponent<T>();
                    }
                    
                }
                return _instance;
            }
        }

       
        private void OnDestroy()
        {
           if(_instance == this)
            {
                _instance = null;
            }
        }
    }
}