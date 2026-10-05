// Needs the Unity.Collections shim (NativeArray / Allocator — mapped to CosmicShore.Engine.Collections
// by the live-src sync), which lands separately. Compiled only when COSMICSHORE_COMPAT_MATH is
// defined: once the Mathematics/Collections shims are merged, add
// <DefineConstants>$(DefineConstants);COSMICSHORE_COMPAT_MATH</DefineConstants> to
// CosmicShore.Compat.csproj (or delete this guard). Verified to compile against the standard API shapes.
#if COSMICSHORE_COMPAT_MATH
using System;
using System.Linq;
using CosmicShore.Engine.Collections;

namespace Unity.Entities
{
    public partial struct EntityManager
    {
        /// <summary>Fills <paramref name="outputEntities"/> with clones of <paramref name="srcEntity"/>.</summary>
        public void Instantiate(Entity srcEntity, NativeArray<Entity> outputEntities)
        {
            for (int i = 0; i < outputEntities.Length; i++)
                outputEntities[i] = Instantiate(srcEntity);
        }

        public NativeArray<Entity> Instantiate(Entity srcEntity, int instanceCount, Allocator allocator)
        {
            var result = new NativeArray<Entity>(instanceCount, allocator);
            Instantiate(srcEntity, result);
            return result;
        }

        public void DestroyEntity(NativeArray<Entity> entities)
        {
            for (int i = 0; i < entities.Length; i++) DestroyEntity(entities[i]);
        }

        public void AddComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            for (int i = 0; i < entities.Length; i++) AddComponent(entities[i], componentType);
        }

        public void AddComponent<T>(NativeArray<Entity> entities) => AddComponent(entities, ComponentType.ReadWrite<T>());

        public void RemoveComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            for (int i = 0; i < entities.Length; i++) RemoveComponent(entities[i], componentType);
        }

        public void RemoveComponent<T>(NativeArray<Entity> entities) => RemoveComponent(entities, ComponentType.ReadWrite<T>());

        /// <summary>Every live entity (prefabs and disabled entities included), as the original.</summary>
        public NativeArray<Entity> GetAllEntities(Allocator allocator = Allocator.Temp)
        {
            var all = GetAllEntitiesManaged().ToArray();
            var result = new NativeArray<Entity>(all.Length, allocator);
            for (int i = 0; i < all.Length; i++) result[i] = all[i];
            return result;
        }

        public NativeArray<ComponentType> GetComponentTypes(Entity entity, Allocator allocator = Allocator.Temp)
        {
            if (!TryGetRecord(entity, out var record))
                throw new ArgumentException($"The entity {entity} does not exist.");
            var types = record.Components.Keys.ToArray();
            var result = new NativeArray<ComponentType>(types.Length, allocator);
            for (int i = 0; i < types.Length; i++) result[i] = ComponentType.ReadWrite(types[i]);
            return result;
        }
    }

    public partial class EntityQuery
    {
        public NativeArray<Entity> ToEntityArray(Allocator allocator)
        {
            var all = ToEntityArrayManaged();
            var result = new NativeArray<Entity>(all.Length, allocator);
            for (int i = 0; i < all.Length; i++) result[i] = all[i];
            return result;
        }

        public NativeArray<T> ToComponentDataArray<T>(Allocator allocator) where T : struct, IComponentData
        {
            var all = ToEntityArrayManaged();
            var result = new NativeArray<T>(all.Length, allocator);
            for (int i = 0; i < all.Length; i++) result[i] = EntityManager.GetComponentData<T>(all[i]);
            return result;
        }
    }

    public static class DynamicBufferNativeExtensions
    {
        /// <summary>A COPY of the buffer (the original's <c>ToNativeArray</c>; <c>AsNativeArray</c>'s aliasing is not offered).</summary>
        public static NativeArray<T> ToNativeArray<T>(this DynamicBuffer<T> buffer, Allocator allocator) where T : struct
        {
            var result = new NativeArray<T>(buffer.Length, allocator);
            for (int i = 0; i < buffer.Length; i++) result[i] = buffer[i];
            return result;
        }

        public static void AddRange<T>(this DynamicBuffer<T> buffer, NativeArray<T> array) where T : struct
        {
            for (int i = 0; i < array.Length; i++) buffer.Add(array[i]);
        }
    }
}
#endif
