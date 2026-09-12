using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Constrains a serialized field - necessarily typed as <c>UnityEngine.Object</c> or
    /// <c>MonoBehaviour</c>, because Unity cannot serialize an interface - to objects that
    /// implement <paramref name="interfaceType"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is only a MARKER; every useful behaviour lives in
    /// <c>CosmicShore.Editor.RequireInterfaceDrawer</c> - narrowing the object picker, resolving a
    /// dropped GameObject to the component that implements the interface, and refusing anything
    /// that does not. Without that drawer a marked field degrades SILENTLY into an ordinary object
    /// field that accepts anything: it compiles, it looks correct in the inspector, and it fails at
    /// runtime on the cast. The drawer is the load-bearing half and must stay under an
    /// <c>Editor/</c> folder, which is also what keeps it out of the player.
    /// </para>
    /// <para>
    /// A type that is not an interface is reported by the drawer, on the field itself, rather than
    /// thrown from this constructor: an exception raised during the editor's attribute-reflection
    /// pass is far worse than a visible error on one row, and a silent fallback to a plain object
    /// field is worse than both.
    /// </para>
    /// <para>
    /// First-party replacement for <c>Assets/SerializeInterface/</c>, an unattributable code drop
    /// with no vendor, no licence and no namespace (<c>Docs/THIRD_PARTY_REGISTER.md</c> §2, §6).
    /// The attribute NAME and constructor signature are deliberately unchanged, so no
    /// <c>[RequireInterface(typeof(T))]</c> call site moved.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [SerializeField, RequireInterface(typeof(IVessel))]
    /// Object _vessel;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class RequireInterfaceAttribute : PropertyAttribute
    {
        /// <summary>The interface an assigned object has to implement.</summary>
        public Type InterfaceType { get; }

        public RequireInterfaceAttribute(Type interfaceType) => InterfaceType = interfaceType;
    }
}
