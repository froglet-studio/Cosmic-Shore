using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Pool
{
    /// <summary>
    /// E9 (VESSEL_LAYER.md): stack-based object pool with the original engine's
    /// Pool API contract — create/get/release/destroy callbacks, optional
    /// double-release detection, capped retention.
    /// </summary>
    public class ObjectPool<T> : IDisposable where T : class
    {
        readonly Func<T> _createFunc;
        readonly Action<T> _actionOnGet;
        readonly Action<T> _actionOnRelease;
        readonly Action<T> _actionOnDestroy;
        readonly bool _collectionCheck;
        readonly int _maxSize;
        readonly Stack<T> _inactive;

        public int CountInactive => _inactive.Count;
        public int CountAll { get; private set; }
        public int CountActive => CountAll - CountInactive;

        public ObjectPool(
            Func<T> createFunc,
            Action<T> actionOnGet = null,
            Action<T> actionOnRelease = null,
            Action<T> actionOnDestroy = null,
            bool collectionCheck = true,
            int defaultCapacity = 10,
            int maxSize = 10000)
        {
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            if (maxSize <= 0) throw new ArgumentException("Max Size must be greater than 0", nameof(maxSize));
            _actionOnGet = actionOnGet;
            _actionOnRelease = actionOnRelease;
            _actionOnDestroy = actionOnDestroy;
            _collectionCheck = collectionCheck;
            _maxSize = maxSize;
            _inactive = new Stack<T>(defaultCapacity);
        }

        public T Get()
        {
            T element;
            if (_inactive.Count == 0)
            {
                element = _createFunc();
                CountAll++;
            }
            else
            {
                element = _inactive.Pop();
            }
            _actionOnGet?.Invoke(element);
            return element;
        }

        public void Release(T element)
        {
            if (_collectionCheck && _inactive.Contains(element))
                throw new InvalidOperationException(
                    "Trying to release an object that has already been released to the pool.");

            _actionOnRelease?.Invoke(element);
            if (_inactive.Count < _maxSize)
            {
                _inactive.Push(element);
            }
            else
            {
                CountAll--;
                _actionOnDestroy?.Invoke(element);
            }
        }

        public void Clear()
        {
            if (_actionOnDestroy != null)
                foreach (var element in _inactive)
                    _actionOnDestroy(element);
            _inactive.Clear();
            CountAll = 0;
        }

        public void Dispose() => Clear();
    }
}
