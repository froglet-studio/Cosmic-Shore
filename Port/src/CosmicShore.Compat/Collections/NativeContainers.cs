using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;

// Unity.Collections is mapped to this namespace by the Live source sync.
namespace CosmicShore.Engine.Collections
{

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)] public sealed class ReadOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)] public sealed class WriteOnlyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class DeallocateOnJobCompletionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class NativeDisableParallelForRestrictionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class NativeDisableContainerSafetyRestrictionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class NativeDisableUnsafePtrRestrictionAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Struct)] public sealed class NativeContainerAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class NativeSetThreadIndexAttribute : Attribute { }

    /// <summary>
    /// Original contract: a fixed-length unmanaged buffer. The port backs it with a managed
    /// array; semantics that matter to callers (length, value-type copies sharing storage,
    /// IsCreated/Dispose, sub-arrays viewing the same memory) are preserved.
    /// </summary>
    public struct NativeArray<T> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>> where T : struct
    {
        internal T[] m_Buffer;
        internal int m_Offset;
        internal int m_Length;

        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        { m_Buffer = new T[Math.Max(0, length)]; m_Offset = 0; m_Length = Math.Max(0, length); }

        public NativeArray(T[] array, Allocator allocator)
        { m_Buffer = (T[])array.Clone(); m_Offset = 0; m_Length = array.Length; }

        public NativeArray(NativeArray<T> array, Allocator allocator)
        { m_Buffer = array.ToArray(); m_Offset = 0; m_Length = m_Buffer.Length; }

        internal NativeArray(T[] shared, int offset, int length) { m_Buffer = shared; m_Offset = offset; m_Length = length; }

        public int Length => m_Length;
        public bool IsCreated => m_Buffer != null;

        public T this[int index]
        {
            get { Check(index); return m_Buffer[m_Offset + index]; }
            set { Check(index); m_Buffer[m_Offset + index] = value; }
        }

        void Check(int index) { if ((uint)index >= (uint)m_Length) throw new IndexOutOfRangeException($"Index {index} is out of range of '{m_Length}' Length."); }

        public void Dispose() { m_Buffer = null; m_Length = 0; }
        public JobHandle Dispose(JobHandle inputDeps) { Dispose(); return inputDeps; }

        public T[] ToArray() { var r = new T[m_Length]; if (m_Buffer != null) Array.Copy(m_Buffer, m_Offset, r, 0, m_Length); return r; }
        public void CopyFrom(T[] array) => Array.Copy(array, 0, m_Buffer, m_Offset, Math.Min(array.Length, m_Length));
        public void CopyFrom(NativeArray<T> array) => Array.Copy(array.m_Buffer, array.m_Offset, m_Buffer, m_Offset, Math.Min(array.m_Length, m_Length));
        public void CopyTo(T[] array) => Array.Copy(m_Buffer, m_Offset, array, 0, Math.Min(array.Length, m_Length));
        public void CopyTo(NativeArray<T> array) => array.CopyFrom(this);
        public static void Copy(NativeArray<T> src, NativeArray<T> dst) => dst.CopyFrom(src);
        public static void Copy(NativeArray<T> src, NativeArray<T> dst, int length) => Array.Copy(src.m_Buffer, src.m_Offset, dst.m_Buffer, dst.m_Offset, length);
        public static void Copy(NativeArray<T> src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) => Array.Copy(src.m_Buffer, src.m_Offset + srcIndex, dst.m_Buffer, dst.m_Offset + dstIndex, length);
        public static void Copy(T[] src, NativeArray<T> dst) => dst.CopyFrom(src);
        public static void Copy(NativeArray<T> src, T[] dst) => src.CopyTo(dst);
        public static void Copy(T[] src, int srcIndex, NativeArray<T> dst, int dstIndex, int length) => Array.Copy(src, srcIndex, dst.m_Buffer, dst.m_Offset + dstIndex, length);
        public static void Copy(NativeArray<T> src, int srcIndex, T[] dst, int dstIndex, int length) => Array.Copy(src.m_Buffer, src.m_Offset + srcIndex, dst, dstIndex, length);

        public NativeArray<T> GetSubArray(int start, int length) => new(m_Buffer, m_Offset + start, length);
        public NativeArray<U> Reinterpret<U>() where U : struct
        {
            if (typeof(U) == typeof(T)) return (NativeArray<U>)(object)this;
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new ReadOnlySpan<T>(m_Buffer, m_Offset, m_Length)).ToArray();
            var r = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, U>(bytes).ToArray();
            return new NativeArray<U>(r, Allocator.Temp);
        }

        public Span<T> AsSpan() => new(m_Buffer, m_Offset, m_Length);
        public ReadOnly AsReadOnly() => new(this);

        public Enumerator GetEnumerator() => new(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(NativeArray<T> other) => ReferenceEquals(m_Buffer, other.m_Buffer) && m_Offset == other.m_Offset && m_Length == other.m_Length;
        public override bool Equals(object obj) => obj is NativeArray<T> o && Equals(o);
        public override int GetHashCode() => (m_Buffer?.GetHashCode() ?? 0) ^ m_Offset ^ (m_Length << 8);
        public static bool operator ==(NativeArray<T> a, NativeArray<T> b) => a.Equals(b);
        public static bool operator !=(NativeArray<T> a, NativeArray<T> b) => !a.Equals(b);

        public struct Enumerator : IEnumerator<T>
        {
            readonly NativeArray<T> _a; int _i;
            public Enumerator(NativeArray<T> a) { _a = a; _i = -1; }
            public T Current => _a[_i];
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++_i < _a.Length;
            public void Reset() => _i = -1;
            public void Dispose() { }
        }

        public readonly struct ReadOnly : IEnumerable<T>
        {
            readonly NativeArray<T> _a;
            public ReadOnly(NativeArray<T> a) { _a = a; }
            public int Length => _a.Length;
            public T this[int index] => _a[index];
            public T[] ToArray() => _a.ToArray();
            public Enumerator GetEnumerator() => _a.GetEnumerator();
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => _a.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _a.GetEnumerator();
        }
    }

    /// <summary>Original contract: a growable unmanaged list (reference semantics across copies).</summary>
    public struct NativeList<T> : IDisposable, IEnumerable<T> where T : struct
    {
        sealed class Box { public List<T> list; }
        Box _box;

        public NativeList(Allocator allocator) : this(1, allocator) { }
        public NativeList(int initialCapacity, Allocator allocator) { _box = new Box { list = new List<T>(Math.Max(1, initialCapacity)) }; }

        public bool IsCreated => _box?.list != null;
        public int Length { get => _box.list.Count; set { var l = _box.list; if (value < l.Count) l.RemoveRange(value, l.Count - value); else while (l.Count < value) l.Add(default); } }
        public int Capacity { get => _box.list.Capacity; set => _box.list.Capacity = Math.Max(value, _box.list.Count); }
        public bool IsEmpty => !IsCreated || _box.list.Count == 0;
        public T this[int index] { get => _box.list[index]; set => _box.list[index] = value; }
        public void Add(T value) => _box.list.Add(value);
        public void AddNoResize(T value) => _box.list.Add(value);
        public void AddRange(NativeArray<T> array) => _box.list.AddRange(array.ToArray());
        public void AddRangeNoResize(NativeList<T> list) => _box.list.AddRange(list._box.list);
        public void Clear() => _box.list.Clear();
        public void RemoveAt(int index) => _box.list.RemoveAt(index);
        public void RemoveAtSwapBack(int index) { var l = _box.list; l[index] = l[l.Count - 1]; l.RemoveAt(l.Count - 1); }
        public void Insert(int index, T item) => _box.list.Insert(index, item);
        public void Resize(int length, NativeArrayOptions options) => Length = length;
        public void ResizeUninitialized(int length) => Length = length;
        public void SetCapacity(int capacity) => Capacity = capacity;
        public NativeArray<T> AsArray() => new(_box.list.ToArray(), Allocator.None);
        public NativeArray<T> AsDeferredJobArray() => AsArray();
        public T[] ToArray() => _box.list.ToArray();
        public NativeArray<T> ToArray(Allocator allocator) => new(_box.list.ToArray(), allocator);
        public void Dispose() { if (_box != null) _box.list = null; }
        public JobHandle Dispose(JobHandle inputDeps) { Dispose(); return inputDeps; }
        public ParallelWriter AsParallelWriter() => new(this);
        public List<T>.Enumerator GetEnumerator() => _box.list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _box.list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _box.list.GetEnumerator();
        public static implicit operator NativeArray<T>(NativeList<T> list) => list.AsArray();

        public readonly struct ParallelWriter
        {
            readonly NativeList<T> _l;
            public ParallelWriter(NativeList<T> l) { _l = l; }
            public void AddNoResize(T value) { lock (_l._box) _l._box.list.Add(value); }
        }
    }

    /// <summary>Original contract: a hash map (reference semantics across copies).</summary>
    public struct NativeHashMap<TKey, TValue> : IDisposable where TKey : struct, IEquatable<TKey> where TValue : struct
    {
        sealed class Box { public Dictionary<TKey, TValue> map; }
        Box _box;
        public NativeHashMap(int capacity, Allocator allocator) { _box = new Box { map = new Dictionary<TKey, TValue>(capacity) }; }
        public bool IsCreated => _box?.map != null;
        public int Count => _box.map.Count;
        public int Capacity { get => Math.Max(_box.map.Count, 16); set { } }
        public bool IsEmpty => _box.map.Count == 0;
        public TValue this[TKey key] { get => _box.map[key]; set => _box.map[key] = value; }
        public bool TryAdd(TKey key, TValue value) => _box.map.TryAdd(key, value);
        public void Add(TKey key, TValue value) => _box.map.Add(key, value);
        public bool Remove(TKey key) => _box.map.Remove(key);
        public bool TryGetValue(TKey key, out TValue value) => _box.map.TryGetValue(key, out value);
        public bool ContainsKey(TKey key) => _box.map.ContainsKey(key);
        public void Clear() => _box.map.Clear();
        public NativeArray<TKey> GetKeyArray(Allocator allocator) => new(new List<TKey>(_box.map.Keys).ToArray(), allocator);
        public NativeArray<TValue> GetValueArray(Allocator allocator) => new(new List<TValue>(_box.map.Values).ToArray(), allocator);
        public void Dispose() { if (_box != null) _box.map = null; }
        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _box.map.GetEnumerator();
    }

    public struct NativeParallelHashMap<TKey, TValue> : IDisposable where TKey : struct, IEquatable<TKey> where TValue : struct
    {
        NativeHashMap<TKey, TValue> _m;
        public NativeParallelHashMap(int capacity, Allocator allocator) { _m = new NativeHashMap<TKey, TValue>(capacity, allocator); }
        public bool IsCreated => _m.IsCreated;
        public int Count() => _m.Count;
        public TValue this[TKey key] { get => _m[key]; set => _m[key] = value; }
        public bool TryAdd(TKey key, TValue value) => _m.TryAdd(key, value);
        public bool Remove(TKey key) => _m.Remove(key);
        public bool TryGetValue(TKey key, out TValue value) => _m.TryGetValue(key, out value);
        public bool ContainsKey(TKey key) => _m.ContainsKey(key);
        public void Clear() => _m.Clear();
        public void Dispose() => _m.Dispose();
    }

    /// <summary>
    /// Original contract: a multi-value hash map iterated with TryGetFirstValue/TryGetNextValue.
    /// Values under one key come back most-recently-added first, like the original.
    /// </summary>
    public struct NativeParallelMultiHashMap<TKey, TValue> : IDisposable where TKey : struct, IEquatable<TKey> where TValue : struct
    {
        sealed class Box { public Dictionary<TKey, List<TValue>> map; public int count; }
        Box _box;

        public NativeParallelMultiHashMap(int capacity, Allocator allocator) { _box = new Box { map = new Dictionary<TKey, List<TValue>>(Math.Max(1, capacity)) }; }

        public bool IsCreated => _box?.map != null;
        public int Count() => _box.count;
        public int Capacity { get => Math.Max(_box.count, 16); set { } }
        public bool IsEmpty => _box.count == 0;

        public void Add(TKey key, TValue item)
        {
            if (!_box.map.TryGetValue(key, out var l)) _box.map[key] = l = new List<TValue>(2);
            l.Add(item);
            _box.count++;
        }

        public int Remove(TKey key)
        {
            if (!_box.map.Remove(key, out var l)) return 0;
            _box.count -= l.Count;
            return l.Count;
        }

        /// <summary>Removes the single value the iterator points at; iteration may continue from it (original contract).</summary>
        public void Remove(NativeParallelMultiHashMapIterator<TKey> it)
        {
            if (!_box.map.TryGetValue(it.key, out var l) || it.index < 0 || it.index >= l.Count) return;
            l.RemoveAt(it.index);
            _box.count--;
            if (l.Count == 0) _box.map.Remove(it.key);
        }

        public void Clear() { _box.map.Clear(); _box.count = 0; }
        public bool ContainsKey(TKey key) => _box.map.ContainsKey(key);
        public int CountValuesForKey(TKey key) => _box.map.TryGetValue(key, out var l) ? l.Count : 0;

        public bool TryGetFirstValue(TKey key, out TValue item, out NativeParallelMultiHashMapIterator<TKey> it)
        {
            it = new NativeParallelMultiHashMapIterator<TKey> { key = key, index = -1 };
            if (!_box.map.TryGetValue(key, out var l) || l.Count == 0) { item = default; return false; }
            it.index = l.Count - 1;
            item = l[it.index];
            return true;
        }

        public bool TryGetNextValue(out TValue item, ref NativeParallelMultiHashMapIterator<TKey> it)
        {
            if (!_box.map.TryGetValue(it.key, out var l) || it.index <= 0) { item = default; it.index = -1; return false; }
            it.index--;
            item = l[it.index];
            return true;
        }

        public NativeArray<TKey> GetKeyArray(Allocator allocator)
        {
            var keys = new List<TKey>(_box.count);
            foreach (var kv in _box.map) for (int i = 0; i < kv.Value.Count; i++) keys.Add(kv.Key);
            return new NativeArray<TKey>(keys.ToArray(), allocator);
        }

        public (NativeArray<TKey>, int) GetUniqueKeyArray(Allocator allocator)
        {
            var keys = new List<TKey>(_box.map.Keys);
            return (new NativeArray<TKey>(keys.ToArray(), allocator), keys.Count);
        }

        public void Dispose() { if (_box != null) _box.map = null; }
        public JobHandle Dispose(JobHandle inputDeps) { Dispose(); return inputDeps; }

        public ParallelWriter AsParallelWriter() => new(this);

        public readonly struct ParallelWriter
        {
            readonly NativeParallelMultiHashMap<TKey, TValue> _m;
            public ParallelWriter(NativeParallelMultiHashMap<TKey, TValue> m) { _m = m; }
            public void Add(TKey key, TValue item) { lock (_m._box) _m.Add(key, item); }
        }
    }

    public struct NativeParallelMultiHashMapIterator<TKey> where TKey : struct
    {
        internal TKey key;
        internal int index;
        public TKey GetKey() => key;
    }

    public struct NativeQueue<T> : IDisposable where T : struct
    {
        sealed class Box { public Queue<T> q; }
        Box _box;
        public NativeQueue(Allocator allocator) { _box = new Box { q = new Queue<T>() }; }
        public bool IsCreated => _box?.q != null;
        public int Count => _box.q.Count;
        public bool IsEmpty() => _box.q.Count == 0;
        public void Enqueue(T value) => _box.q.Enqueue(value);
        public T Dequeue() => _box.q.Dequeue();
        public bool TryDequeue(out T item) => _box.q.TryDequeue(out item);
        public T Peek() => _box.q.Peek();
        public void Clear() => _box.q.Clear();
        public NativeArray<T> ToArray(Allocator allocator) => new(_box.q.ToArray(), allocator);
        public void Dispose() { if (_box != null) _box.q = null; }
    }

    public struct NativeHashSet<T> : IDisposable where T : struct, IEquatable<T>
    {
        sealed class Box { public HashSet<T> s; }
        Box _box;
        public NativeHashSet(int capacity, Allocator allocator) { _box = new Box { s = new HashSet<T>(capacity) }; }
        public bool IsCreated => _box?.s != null;
        public int Count => _box.s.Count;
        public bool Add(T item) => _box.s.Add(item);
        public bool Remove(T item) => _box.s.Remove(item);
        public bool Contains(T item) => _box.s.Contains(item);
        public void Clear() => _box.s.Clear();
        public void Dispose() { if (_box != null) _box.s = null; }
        public HashSet<T>.Enumerator GetEnumerator() => _box.s.GetEnumerator();
    }

    public struct NativeReference<T> : IDisposable where T : struct
    {
        sealed class Box { public T v; }
        Box _box;
        public NativeReference(Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) { _box = new Box(); }
        public NativeReference(T value, Allocator allocator) { _box = new Box { v = value }; }
        public bool IsCreated => _box != null;
        public T Value { get => _box.v; set => _box.v = value; }
        public void Dispose() => _box = null;
    }

    public static class NativeArrayExtensions
    {
        public static bool Contains<T, U>(this NativeArray<T> array, U value) where T : struct, IEquatable<U> { for (int i = 0; i < array.Length; i++) if (array[i].Equals(value)) return true; return false; }
        public static int IndexOf<T, U>(this NativeArray<T> array, U value) where T : struct, IEquatable<U> { for (int i = 0; i < array.Length; i++) if (array[i].Equals(value)) return i; return -1; }
        public static void Sort<T>(this NativeArray<T> array) where T : struct, IComparable<T> => Array.Sort(array.m_Buffer, array.m_Offset, array.Length);
        public static void Sort<T, U>(this NativeArray<T> array, U comp) where T : struct where U : IComparer<T> => Array.Sort(array.m_Buffer, array.m_Offset, array.Length, comp);
        public static void Sort<T>(this NativeList<T> list) where T : struct, IComparable<T> { var a = list.ToArray(); Array.Sort(a); for (int i = 0; i < a.Length; i++) list[i] = a[i]; }
    }
}
