using System;
using System.Collections;
using System.Collections.Generic;

namespace CosmicShore.Engine.Soap
{
    /// <summary>
    /// Reactive list asset — the port's replacement for the SOAP ScriptableList.
    /// Raises granular events on mutation so UI can react without polling.
    /// </summary>
    public class ScriptableList<T> : ScriptableObject, IList<T>, IReadOnlyList<T>
    {
        readonly List<T> _list = new();

        public event Action<T> OnItemAdded;
        public event Action<T> OnItemRemoved;
        public event Action OnCleared;
        public event Action OnItemCountChanged;

        public int Count => _list.Count;
        public bool IsEmpty => _list.Count == 0;
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public void Add(T item)
        {
            _list.Add(item);
            OnItemAdded?.Invoke(item);
            OnItemCountChanged?.Invoke();
        }

        public bool Remove(T item)
        {
            if (!_list.Remove(item)) return false;
            OnItemRemoved?.Invoke(item);
            OnItemCountChanged?.Invoke();
            return true;
        }

        public void RemoveAt(int index)
        {
            T item = _list[index];
            _list.RemoveAt(index);
            OnItemRemoved?.Invoke(item);
            OnItemCountChanged?.Invoke();
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            OnItemAdded?.Invoke(item);
            OnItemCountChanged?.Invoke();
        }

        public void Clear()
        {
            if (_list.Count == 0) return;
            _list.Clear();
            OnCleared?.Invoke();
            OnItemCountChanged?.Invoke();
        }

        public bool Contains(T item) => _list.Contains(item);
        public int IndexOf(T item) => _list.IndexOf(item);
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();
    }
}
