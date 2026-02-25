using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Systems.Bootstrap
{
    /// <summary>
    /// Lightweight service locator for static access to registered services.
    /// Complements Reflex DI for contexts where constructor/field injection isn't available
    /// (e.g., ScriptableObjects, static methods, non-MonoBehaviour classes).
    /// </summary>
    public static class ServiceLocator
    {
        static readonly Dictionary<Type, object> _services = new();
        static readonly Dictionary<Type, object> _sceneServices = new();

        /// <summary>
        /// Register a service that persists across scene loads.
        /// </summary>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
                Debug.LogWarning($"[ServiceLocator] Overwriting global registration for {type.Name}");

            _services[type] = service;
        }

        /// <summary>
        /// Register a scene-scoped service that is cleared on scene transitions.
        /// </summary>
        public static void RegisterSceneService<T>(T service) where T : class
        {
            var type = typeof(T);
            _sceneServices[type] = service;
        }

        /// <summary>
        /// Retrieve a registered service. Checks scene-scoped first, then global.
        /// </summary>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);

            if (_sceneServices.TryGetValue(type, out var sceneService))
                return (T)sceneService;

            if (_services.TryGetValue(type, out var service))
                return (T)service;

            Debug.LogError($"[ServiceLocator] Service {type.Name} not registered.");
            return null;
        }

        /// <summary>
        /// Try to retrieve a registered service without logging an error on miss.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);

            if (_sceneServices.TryGetValue(type, out var sceneObj))
            {
                service = (T)sceneObj;
                return true;
            }

            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = null;
            return false;
        }

        public static bool IsRegistered<T>() where T : class
            => _sceneServices.ContainsKey(typeof(T)) || _services.ContainsKey(typeof(T));

        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            _services.Remove(type);
            _sceneServices.Remove(type);
        }

        /// <summary>
        /// Clear scene-scoped services. Called automatically on scene transitions.
        /// </summary>
        public static void ClearSceneServices()
            => _sceneServices.Clear();

        /// <summary>
        /// Clear all registrations. Typically only called on application quit.
        /// </summary>
        public static void ClearAll()
        {
            _services.Clear();
            _sceneServices.Clear();
        }
    }
}
