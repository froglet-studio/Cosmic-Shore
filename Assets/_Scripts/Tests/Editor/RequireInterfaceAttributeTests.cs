#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="RequireInterfaceAttribute"/> is a marker whose whole value is its property
    /// drawer, and its failure mode is SILENT: a marked field whose preconditions are not met
    /// degrades into an ordinary object field that accepts anything, which compiles, looks right in
    /// the inspector, and throws on the cast at runtime. Nothing about that shows up in a build.
    ///
    /// So these assert the preconditions the drawer needs, over every marked field in the project,
    /// by reflection - a consumer that drifts out of them fails here instead of in play. They do
    /// NOT prove the picker filters correctly; only clicking the field does that.
    ///
    /// Written alongside the first-party rewrite that replaced Assets/SerializeInterface/
    /// (Docs/THIRD_PARTY_REGISTER.md §2, §6).
    /// </summary>
    public class RequireInterfaceAttributeTests
    {
        // The six live consumers at the time of the rewrite: PlayerSpawner, ImpactCollider,
        // VesselStatus, GunTransformer, VesselCollider, ObjectiveIndicator. AIGunner carries a
        // SEVENTH usage that is commented out, so reflection cannot and should not see it.
        const int KnownLiveConsumers = 6;

        static List<FieldInfo> MarkedFields()
        {
            var fields = new List<FieldInfo>();

            // Every reflection step here is allowed to fail without failing the TEST: an
            // assembly with an unresolvable optional dependency, or a field whose type will not
            // load, is not evidence about [RequireInterface]. A false failure in a structural
            // guard is worse than a slightly narrower sweep.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch (Exception) { continue; }

                foreach (var type in types)
                {
                    FieldInfo[] declared;
                    try
                    {
                        declared = type.GetFields(BindingFlags.Instance | BindingFlags.Static
                                                  | BindingFlags.Public | BindingFlags.NonPublic
                                                  | BindingFlags.DeclaredOnly);
                    }
                    catch (Exception) { continue; }

                    foreach (var field in declared)
                    {
                        try
                        {
                            if (field.GetCustomAttribute<RequireInterfaceAttribute>() != null) fields.Add(field);
                        }
                        catch (Exception) { /* unloadable attribute or field type */ }
                    }
                }
            }

            return fields;
        }

        static Type ElementTypeOf(Type fieldType)
        {
            if (fieldType.IsArray) return fieldType.GetElementType();

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                return fieldType.GetGenericArguments()[0];

            return fieldType;
        }

        static string Name(FieldInfo f) => $"{f.DeclaringType?.FullName}.{f.Name}";

        [Test]
        public void TheAttributeIsStillReachedByTheProjectsOwnFields()
        {
            // Guards the rewrite itself: if the attribute were moved, renamed, or left in a
            // namespace the consumers do not import, this count collapses and every marked field
            // silently loses its drawer.
            Assert.GreaterOrEqual(MarkedFields().Count, KnownLiveConsumers,
                $"Expected at least {KnownLiveConsumers} fields marked [RequireInterface]. " +
                "A drop means a consumer stopped resolving the attribute, not that the field is gone.");
        }

        [Test]
        public void EveryRequiredTypeIsAnInterface()
        {
            foreach (var field in MarkedFields())
            {
                var required = field.GetCustomAttribute<RequireInterfaceAttribute>().InterfaceType;

                Assert.IsNotNull(required, $"{Name(field)} passes null to [RequireInterface].");
                Assert.IsTrue(required.IsInterface,
                    $"{Name(field)} requires '{required.Name}', which is not an interface.");
            }
        }

        [Test]
        public void EveryMarkedFieldCanHoldAnObjectReference()
        {
            // The drawer writes through SerializedProperty.objectReferenceValue, so the field (or
            // its element type) has to be a UnityEngine.Object. Anything else serialises as a
            // different property type and the drawer's assignment silently does nothing.
            foreach (var field in MarkedFields())
            {
                var elementType = ElementTypeOf(field.FieldType);

                Assert.IsTrue(typeof(Object).IsAssignableFrom(elementType),
                    $"{Name(field)} is typed '{elementType.Name}', which is not a UnityEngine.Object. " +
                    "[RequireInterface] can only constrain an object reference.");
            }
        }

        [Test]
        public void EveryMarkedFieldIsActuallySerialized()
        {
            // A property drawer only ever runs on a SERIALIZED field. A private field with no
            // [SerializeField] is the quietest possible way for the attribute to do nothing at all.
            foreach (var field in MarkedFields())
            {
                bool serialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;

                Assert.IsTrue(serialized,
                    $"{Name(field)} is marked [RequireInterface] but is neither public nor " +
                    "[SerializeField], so Unity never serialises it and the drawer never runs.");
                Assert.IsFalse(field.IsStatic,
                    $"{Name(field)} is static, so Unity never serialises it and the drawer never runs.");
            }
        }

        [Test]
        public void TheInterfaceIsSatisfiableByTheFieldsDeclaredType()
        {
            // Catches the reverse mistake: a field typed to a concrete class that cannot possibly
            // implement the interface asked for. An Object/MonoBehaviour-typed field is the normal
            // case and is always satisfiable, because the concrete value decides.
            foreach (var field in MarkedFields())
            {
                var required = field.GetCustomAttribute<RequireInterfaceAttribute>().InterfaceType;
                var elementType = ElementTypeOf(field.FieldType);

                bool openEnough = elementType == typeof(Object)
                                  || elementType == typeof(MonoBehaviour)
                                  || elementType == typeof(ScriptableObject)
                                  || elementType == typeof(Component)
                                  || !elementType.IsSealed
                                  || required.IsAssignableFrom(elementType);

                Assert.IsTrue(openEnough,
                    $"{Name(field)} is typed '{elementType.Name}', which is sealed and does not " +
                    $"implement '{required.Name}', so no value can ever satisfy the constraint.");
            }
        }
    }
}
#endif
