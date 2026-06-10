using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Base class for data assets. In the port, asset instances are plain objects
    /// created in code or deserialized from JSON by the asset registry (replacing
    /// .asset files). The <see cref="name"/> mirrors the original asset name so
    /// registry lookups stay stable across the port.
    /// </summary>
    public abstract class ScriptableObject
    {
        public string name;

        public static T CreateInstance<T>() where T : ScriptableObject, new()
            => new T { name = typeof(T).Name };

        public static ScriptableObject CreateInstance(Type type)
        {
            var instance = (ScriptableObject)Activator.CreateInstance(type);
            instance.name = type.Name;
            return instance;
        }

        public override string ToString() => $"{name} ({GetType().Name})";
    }
}
