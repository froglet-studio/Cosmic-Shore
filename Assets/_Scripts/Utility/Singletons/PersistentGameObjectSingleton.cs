using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PersistentGameObjectSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    #region Public Accessors

    /// <summary>
    /// Static instance of PersistentGameObjectSingleton which allows it to be accessed by any other script.
    /// </summary>
    public static PersistentGameObjectSingleton<T> Instance { get; private set; }

    #endregion

    

    /// <summary>
    /// Things to do as soon as the scene starts up
    /// </summary>
    void Awake()
    {
        //Logging.Log("{0} - Awoken. Initializing Singleton pattern. Instance Id : {1}", this.GetType().Name, gameObject.GetInstanceID());

        if (Instance == null)
        {
            //Logging.Log("{0} - Setting first instance. Instance Id : {1}", this.GetType().Name, gameObject.GetInstanceID());

            //if not, set instance to this
            Instance = this;

            //Sets this to not be destroyed when reloading scene
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            //Logging.LogWarning("{0} - Destroying secondary instance. Instance Id : {1}", this.GetType().Name, gameObject.GetInstanceID());

            //Then destroy this. This enforces our singleton pattern, meaning there can only ever be one instance of a GlobalManager.
            DestroyImmediate(gameObject);

            return;
        }
    }
}