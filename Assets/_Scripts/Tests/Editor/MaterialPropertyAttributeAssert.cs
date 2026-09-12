#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Shared gate for "this <c>IComponentData</c> still carries <c>[MaterialProperty]</c>", the
    /// attribute Entities Graphics needs in order to upload a per-instance override at all.
    ///
    /// <para>Matched by NAME rather than by referencing
    /// <c>Unity.Rendering.MaterialPropertyAttribute</c>, and that is deliberate: whether the
    /// package exposes the attribute's members as fields or properties is its business and has
    /// changed across versions, so binding these gates to its internals would make them fail on a
    /// package bump.</para>
    ///
    /// <para>But a name match has to allow for the C# ATTRIBUTE SUFFIX. <c>[MaterialProperty]</c>
    /// in source binds a class named <c>MaterialPropertyAttribute</c>, so the RUNTIME type name
    /// carries a suffix the SOURCE spelling omits. Comparing the source spelling against a runtime
    /// name is what made both copies of this assert unsatisfiable - they failed for every
    /// component, including ones that plainly carry the attribute a line above their
    /// declaration. Both spellings are accepted so the gate is right either way.</para>
    ///
    /// <para>The failure names the attributes that ARE on the type, so a future mismatch explains
    /// itself instead of reading as a missing attribute.</para>
    /// </summary>
    static class MaterialPropertyAttributeAssert
    {
        public static void IsDeclaredOn(Type type, string typeName, string consequence)
        {
            var attributes = type.GetCustomAttributes(false);
            bool found = attributes.Any(a =>
            {
                string n = a.GetType().Name;
                return n == "MaterialProperty" || n == "MaterialPropertyAttribute";
            });

            Assert.IsTrue(found,
                $"{typeName} has lost its [MaterialProperty] attribute — Entities Graphics will " +
                $"not upload it, so {consequence} Attributes actually on the type: " +
                (attributes.Length == 0
                    ? "(none)"
                    : string.Join(", ", attributes.Select(a => a.GetType().Name))) + ".");
        }
    }
}
#endif
