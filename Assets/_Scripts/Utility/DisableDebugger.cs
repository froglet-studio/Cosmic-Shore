using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Can't figure out what's disabling a gameobject? Throw this script on the object and monitor the console.
    /// You will get a stacktrace showing what disabled the object.
    /// If there is no stacktrace (except OnDisable), it probably means the gameobject was disabled by an animation
    /// </summary>
    public class DisableDebugger : MonoBehaviour
    {
        void OnDisable()
        {
            throw new System.Exception($"DisableDebugger - {gameObject.name} has been disabled!");
        }
    }
}
