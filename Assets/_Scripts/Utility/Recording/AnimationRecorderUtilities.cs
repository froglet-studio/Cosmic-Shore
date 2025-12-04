// Hello. I have changed!

using UnityEngine;

namespace CosmicShore.Utility.Recording
{
    public static class AnimationRecorderUtilities
    {
        /// <summary>
        /// Gets a component of a specific type in a game object. If the component does not exist yet, create it
        /// then return it.
        /// </summary>
        /// <param name="gameObject">The game object to check and possibly modify.</param>
        /// <param name="name">The name of the new component, if needed.</param>
        /// <typeparam name="ComponentType">The specific type of component to look for.</typeparam>
        /// <returns></returns>
        public static ComponentType GetOrAddComponent<ComponentType>(this GameObject gameObject) where ComponentType : Component
        {
            ComponentType component = gameObject.GetComponent<ComponentType>();
            component ??= gameObject.AddComponent<ComponentType>();
            return component;
        }

        public static GameObject FindOrCreateGameObject(string name)
        {
            var go = GameObject.Find(name);
            go  ??= new GameObject(name);
            return go;
        }
    }
}