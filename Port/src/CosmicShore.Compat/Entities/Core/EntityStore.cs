using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Unity.Entities
{
    /// <summary>
    /// The in-memory component store behind an <see cref="EntityManager"/>: entity slots with
    /// versions, each holding its components boxed by CLR type. No chunks, no archetype
    /// tables — the port needs Entities to BEHAVE (writes read back, clones copy, destroyed
    /// handles go stale), not to be fast. Structural and value semantics follow the
    /// original: value components copy on read and on instantiate, buffers deep-copy on
    /// instantiate, <see cref="Prefab"/> is stripped from clones, a disabled enableable
    /// component is still present (<c>HasComponent</c>) but a query does not match it.
    /// </summary>
    internal sealed class EntityStore
    {
        internal sealed class Record
        {
            public int Version;
            public bool Alive;
            public string Name;
            public readonly Dictionary<Type, object> Components = new();
            public HashSet<Type> DisabledComponents;
        }

        // Slot 0 is reserved so Entity.Null (0:0) never names a live entity.
        readonly List<Record> _slots = new() { null };
        readonly Stack<int> _free = new();

        public int Count { get; private set; }

        /// <summary>Bumped on every structural change (create/destroy/add/remove); queries use it to cache.</summary>
        public int StructuralVersion { get; private set; }

        public Entity Create()
        {
            int index;
            Record record;
            if (_free.Count > 0)
            {
                index = _free.Pop();
                record = _slots[index];
            }
            else
            {
                index = _slots.Count;
                record = new Record();
                _slots.Add(record);
            }
            record.Version++;
            record.Alive = true;
            record.Name = null;
            record.Components.Clear();
            record.DisabledComponents?.Clear();
            Count++;
            StructuralVersion++;
            return new Entity { Index = index, Version = record.Version };
        }

        public bool Exists(Entity entity) => TryGet(entity, out _);

        public bool TryGet(Entity entity, out Record record)
        {
            if (entity.Index > 0 && entity.Index < _slots.Count)
            {
                record = _slots[entity.Index];
                if (record.Alive && record.Version == entity.Version) return true;
            }
            record = null;
            return false;
        }

        public Record Get(Entity entity)
        {
            if (TryGet(entity, out var record)) return record;
            throw new ArgumentException(
                entity == Entity.Null
                    ? "Entity.Null does not exist."
                    : $"The entity {entity} does not exist (it was destroyed, or belongs to another world).");
        }

        public void Destroy(Entity entity)
        {
            if (!TryGet(entity, out var record)) return;
            record.Alive = false;
            record.Components.Clear();
            record.DisabledComponents?.Clear();
            record.Name = null;
            _free.Push(entity.Index);
            Count--;
            StructuralVersion++;
        }

        public void MarkStructural() => StructuralVersion++;

        public IEnumerable<Entity> All()
        {
            for (int i = 1; i < _slots.Count; i++)
            {
                var r = _slots[i];
                if (r.Alive) yield return new Entity { Index = i, Version = r.Version };
            }
        }

        public void Clear()
        {
            for (int i = 1; i < _slots.Count; i++)
            {
                var r = _slots[i];
                if (!r.Alive) continue;
                r.Alive = false;
                r.Components.Clear();
                r.DisabledComponents?.Clear();
                _free.Push(i);
            }
            Count = 0;
            StructuralVersion++;
        }

        /// <summary>
        /// Copy semantics for a stored component value: a boxed struct is re-boxed (so the
        /// clone and the source never share storage), a buffer is deep-copied, a managed
        /// component is cloned through <see cref="ICloneable"/> when it offers one and shared otherwise.
        /// </summary>
        public static object CopyValue(object value)
        {
            switch (value)
            {
                case null: return null;
                case IBufferStorage buffer: return buffer.Clone();
                case ICloneable cloneable when !value.GetType().IsValueType: return cloneable.Clone();
                default: return RuntimeHelpers.GetObjectValue(value);
            }
        }
    }

    /// <summary>Type-erased access to a buffer's backing list.</summary>
    internal interface IBufferStorage
    {
        IBufferStorage Clone();
        Type ElementType { get; }
    }

    internal sealed class BufferStorage<T> : IBufferStorage where T : struct
    {
        public readonly List<T> Items = new();
        public Type ElementType => typeof(T);

        public IBufferStorage Clone()
        {
            var copy = new BufferStorage<T>();
            copy.Items.AddRange(Items);
            return copy;
        }
    }
}
