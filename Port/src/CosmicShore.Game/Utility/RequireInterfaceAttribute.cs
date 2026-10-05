using System;
using CosmicShore.Engine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Constrains a serialized Object field to implementations of an interface (marker;
    /// the editor drawer enforces it). Mirrors Assets/_Scripts/Utility/RequireInterfaceAttribute.cs,
    /// which is first-party game code — it lives with the game, not the engine.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequireInterfaceAttribute : PropertyAttribute
    {
        public Type InterfaceType { get; }
        public RequireInterfaceAttribute(Type interfaceType) => InterfaceType = interfaceType;
    }
}
