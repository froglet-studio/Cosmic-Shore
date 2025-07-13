using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obvious.Soap
{
    public abstract class ScriptableList<T> : ScriptableListBase, IReset, IList<T>, IDrawObjectsInInspector
    {
        [Tooltip(
            "Clear the list when:" +
            " Scene Loaded : when a scene is loaded." +
            " Application Start : Once, when the application starts. Modifications persists between scenes")]
        [SerializeField]
        private ResetType _resetOn = ResetType.SceneLoaded;

        [SerializeField] protected List<T> _list = new List<T>();
        private readonly HashSet<T> _hashSet = new HashSet<T>();
        
        public int Count => _list.Count;
        public bool IsReadOnly => false;
        public bool IsEmpty => _list.Count == 0;
        public override Type GetGenericType => typeof(T);

        /// <summary>
        /// Indexer: Access an item in the list by index.
        /// </summary>
        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        /// <summary> Event raised when an item is added or removed from the list. </summary>
        public event Action OnItemCountChanged;

        /// <summary> Event raised  when an item is added to the list. </summary>
        public event Action<T> OnItemAdded;

        /// <summary> Event raised  when an item is removed from the list. </summary>
        public event Action<T> OnItemRemoved;

        /// <summary> Event raised  when multiple item are added to the list. </summary>
        public event Action<IEnumerable<T>> OnItemsAdded;

        /// <summary> Event raised  when multiple items are removed from the list. </summary>
        public event Action<IEnumerable<T>> OnItemsRemoved;

        /// <summary> Event raised  when the list is cleared. </summary>
        public event Action OnCleared;

        public int IndexOf(T item) => _list.IndexOf(item);
        public bool Contains(T item) => _hashSet.Contains(item);

        /// <summary>
        /// Adds an item to the list.
        /// Raises OnItemCountChanged and OnItemAdded event.
        /// </summary>
        public void Add(T item)
        {
            _list.Add(item);
            AddItemToHashAndRaiseEvents(item);
        }

        /// <summary>
        /// Adds an item to the list only if it's not in the list.
        /// If success, raises OnItemCountChanged and OnItemAdded event.
        /// </summary>
        public bool TryAdd(T item)
        {
            if (!_hashSet.Contains(item))
            {
                _list.Add(item);
                AddItemToHashAndRaiseEvents(item);
                return true;
            }

            return false;
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            AddItemToHashAndRaiseEvents(item);
        }
        
        private void AddItemToHashAndRaiseEvents(T item)
        {
            _hashSet.Add(item);
            OnItemCountChanged?.Invoke();
            OnItemAdded?.Invoke(item);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        /// <summary>
        /// Adds a range of items to the list.
        /// Raises OnItemCountChanged and OnItemsAdded event once, after all items have been added.
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            var collection = items.ToArray();
            if (collection.Length == 0)
                return;

            _list.AddRange(collection);
            _hashSet.UnionWith(collection);

            OnItemCountChanged?.Invoke();
            OnItemsAdded?.Invoke(collection);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        /// <summary>
        /// Adds a range of items to the list. An item is only added if its not in the list.
        /// Raises OnItemCountChanged and OnItemsAdded event once, after all items have been added.
        /// </summary>
        public bool TryAddRange(IEnumerable<T> items)
        {
            if (items == null)
                return false;

            var uniqueItems = items.Where(item => !_hashSet.Contains(item)).ToList();
            if (uniqueItems.Count > 0)
            {
                AddRange(uniqueItems);
                return true;
            }

            return false;
        }
        
        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes an item from the list only if it's in the list.
        /// If Success, raises OnItemCountChanged and OnItemRemoved event.
        /// </summary>
        /// <param name="item"></param>
        public bool Remove(T item)
        {
            if (!_hashSet.Contains(item))
                return false;

            var removedFromList = _list.Remove(item);
            if (removedFromList)
            {
                _hashSet.Remove(item);
                OnItemCountChanged?.Invoke();
                OnItemRemoved?.Invoke(item);
#if UNITY_EDITOR
                RepaintRequest?.Invoke();
#endif
                return true;
            }

            return false;
        }
        
        bool ICollection<T>.Remove(T item)
        {
            return _list.Remove(item);
        }

        /// <summary>
        /// Removes an item from the list at a specific index.
        /// </summary>
        /// <param name="index"></param>
        public void RemoveAt(int index)
        {
            var item = _list[index];
            _list.RemoveAt(index);
            _hashSet.Remove(item);
            OnItemCountChanged?.Invoke();
            OnItemRemoved?.Invoke(item);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }


        /// <summary>
        /// Removes a range of items from the list.
        /// Raises OnItemCountChanged and OnItemsAdded event once, after all items have been added.
        /// </summary>
        /// <param name="index">Starting Index</param>
        /// <param name="count">Amount of Items</param>
        public bool RemoveRange(int index, int count)
        {
            if (index < 0 || count < 0)
                return false;

            if (index + count > _list.Count)
                return false;
            
            var itemsToRemove = _list.GetRange(index, count);

            foreach (var itemToRemove in itemsToRemove)
                _hashSet.Remove(itemToRemove);

            _list.RemoveRange(index, count);
            OnItemCountChanged?.Invoke();
            OnItemsRemoved?.Invoke(itemsToRemove);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
            return true;
        }
      
        public void Clear()
        {
            _hashSet.Clear();
            _list.Clear();
            OnCleared?.Invoke();
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        private void Awake()
        {
            //Prevents from resetting if no reference in a scene
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        private void OnEnable()
        {
            Clear();

            if (_resetOn == ResetType.SceneLoaded)
                SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private void OnDisable()
        {
            if (_resetOn == ResetType.SceneLoaded)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                Clear();
        }

        public override void Reset()
        {
            _resetOn = ResetType.SceneLoaded;
            Clear();
        }

#if UNITY_EDITOR
        public void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (playModeStateChange == PlayModeStateChange.EnteredEditMode)
                Clear();
        }
#endif

        public void ResetToInitialValue() => Clear();

        public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void ForEach(Action<T> action)
        {
            for (var i = _list.Count - 1; i >= 0; i--)
                action(_list[i]);
        }

        public List<Object> GetAllObjects()
        {
            var list = new List<Object>(Count);
            list.AddRange(_list.OfType<Object>());
            return list;
        }
    }
}