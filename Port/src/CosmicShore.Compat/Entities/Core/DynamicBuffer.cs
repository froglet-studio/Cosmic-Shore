using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Unity.Entities
{
    /// <summary>
    /// A resizable per-entity array of <typeparamref name="T"/>. Backed by the store's list for
    /// that entity, so every copy of a <see cref="DynamicBuffer{T}"/> taken from one entity sees
    /// the same elements (the original's aliasing), and <c>Instantiate</c> deep-copies it.
    /// </summary>
    public struct DynamicBuffer<T> : IEnumerable<T> where T : struct
    {
        readonly BufferStorage<T> _storage;

        internal DynamicBuffer(BufferStorage<T> storage) { _storage = storage; }

        List<T> Items => _storage?.Items ?? throw new InvalidOperationException("The DynamicBuffer has not been created (default value).");

        public bool IsCreated => _storage != null;
        public int Length
        {
            get => Items.Count;
            set => ResizeUninitialized(value);
        }
        public int Capacity
        {
            get => Items.Capacity;
            set => Items.Capacity = Math.Max(value, Items.Count);
        }
        public bool IsEmpty => Items.Count == 0;

        public T this[int index]
        {
            get => Items[index];
            set => Items[index] = value;
        }

        /// <summary>A reference to the element at <paramref name="index"/> (valid until the buffer is resized).</summary>
        public ref T ElementAt(int index)
        {
            var items = Items;
            if ((uint)index >= (uint)items.Count) throw new IndexOutOfRangeException($"Index {index} is out of range of '{items.Count}' Length.");
            return ref CollectionsMarshal.AsSpan(items)[index];
        }

        public int Add(T element)
        {
            Items.Add(element);
            return Items.Count - 1;
        }

        public void Insert(int index, T element) => Items.Insert(index, element);
        public void RemoveAt(int index) => Items.RemoveAt(index);
        public void RemoveRange(int index, int count) => Items.RemoveRange(index, count);

        public void RemoveAtSwapBack(int index)
        {
            var items = Items;
            int last = items.Count - 1;
            items[index] = items[last];
            items.RemoveAt(last);
        }

        public void RemoveRangeSwapBack(int index, int count)
        {
            for (int i = 0; i < count; i++) RemoveAtSwapBack(index);
        }

        public void Clear() => Items.Clear();
        public void EnsureCapacity(int length) { if (Items.Capacity < length) Items.Capacity = length; }
        public void TrimExcess() => Items.TrimExcess();

        /// <summary>Resizes to <paramref name="length"/>; new elements are default(T) (the port zero-fills).</summary>
        public void ResizeUninitialized(int length)
        {
            var items = Items;
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length < items.Count) items.RemoveRange(length, items.Count - length);
            else while (items.Count < length) items.Add(default);
        }

        public void CopyFrom(T[] array)
        {
            Items.Clear();
            Items.AddRange(array);
        }

        public void CopyFrom(DynamicBuffer<T> other)
        {
            var source = new List<T>(other.Items);
            Items.Clear();
            Items.AddRange(source);
        }

        public T[] ToArray() => Items.ToArray();

        public List<T>.Enumerator GetEnumerator() => Items.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
    }
}
