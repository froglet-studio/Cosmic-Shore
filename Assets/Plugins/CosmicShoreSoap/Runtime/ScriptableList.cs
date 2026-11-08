using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Soap
{
    /// <summary>
    /// A list in an asset that raises events as it changes, so UI can react without polling.
    /// It starts every Play session empty, and with Reset On = Scene Loaded it also empties
    /// whenever a scene loads in Single mode. Change it from the main thread only.
    /// Setting an item through the indexer raises no event; RemoveAt + Insert does.
    /// </summary>
    public abstract class ScriptableList<T> : SoapAsset, IList<T>, IReadOnlyList<T>
    {
        [Tooltip("Scene Loaded: empties whenever a scene loads in Single mode.\n" +
                 "Application Starts: keeps its items across scene loads.")]
        [SerializeField] ResetType _resetOn = ResetType.SceneLoaded;

        [SerializeField] protected List<T> _list = new();

        /// <summary>After one item is added or removed, or a range is.</summary>
        public event Action OnItemCountChanged;
        public event Action<T> OnItemAdded;
        public event Action<T> OnItemRemoved;
        public event Action<IEnumerable<T>> OnItemsAdded;
        public event Action<IEnumerable<T>> OnItemsRemoved;
        public event Action OnCleared;

        public int Count => _list.Count;
        public bool IsEmpty => _list.Count == 0;
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => _list[index];
            set => _list[index] = value;
        }

        public int IndexOf(T item) => _list.IndexOf(item);
        public bool Contains(T item) => _list.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

        public void Add(T item)
        {
            _list.Add(item);
            RaiseAdded(item);
        }

        /// <summary>Adds the item unless the list already holds it.</summary>
        public bool TryAdd(T item)
        {
            if (_list.Contains(item)) return false;
            Add(item);
            return true;
        }

        public void Insert(int index, T item)
        {
            _list.Insert(index, item);
            RaiseAdded(item);
        }

        /// <summary>Adds every item, raising <see cref="OnItemsAdded"/> once.</summary>
        public void AddRange(IEnumerable<T> items)
        {
            var added = items.ToArray();
            if (added.Length == 0) return;
            _list.AddRange(added);
            OnItemCountChanged?.Invoke();
            OnItemsAdded?.Invoke(added);
        }

        public bool Remove(T item)
        {
            int index = _list.IndexOf(item);
            if (index < 0) return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            var item = _list[index];
            _list.RemoveAt(index);
            OnItemCountChanged?.Invoke();
            OnItemRemoved?.Invoke(item);
        }

        /// <summary>Removes <paramref name="count"/> items from <paramref name="index"/>, raising <see cref="OnItemsRemoved"/> once.</summary>
        public bool RemoveRange(int index, int count)
        {
            if (index < 0 || count < 0 || index + count > _list.Count) return false;
            var removed = _list.GetRange(index, count);
            _list.RemoveRange(index, count);
            OnItemCountChanged?.Invoke();
            OnItemsRemoved?.Invoke(removed);
            return true;
        }

        /// <summary>Empties the list and raises <see cref="OnCleared"/> (not <see cref="OnItemCountChanged"/>).</summary>
        public void Clear()
        {
            _list.Clear();
            OnCleared?.Invoke();
        }

        public void ResetToInitialValue() => Clear();

        public List<T>.Enumerator GetEnumerator() => _list.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        void RaiseAdded(T item)
        {
            OnItemCountChanged?.Invoke();
            OnItemAdded?.Invoke(item);
        }

        void Awake()
        {
            // Keep the runtime items alive when no loaded scene references this asset.
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        void OnEnable()
        {
            Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_resetOn == ResetType.SceneLoaded) SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Clear();
        }

#if UNITY_EDITOR
        // Play mode runs without a domain reload, so subscribers are dropped at both
        // boundaries; leaving Play also empties the list (subscribers first, so a dead
        // handler never hears it).
        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change is not (PlayModeStateChange.ExitingEditMode or PlayModeStateChange.EnteredEditMode)) return;
            OnItemCountChanged = null;
            OnItemAdded = null;
            OnItemRemoved = null;
            OnItemsAdded = null;
            OnItemsRemoved = null;
            OnCleared = null;
            if (change == PlayModeStateChange.EnteredEditMode) _list.Clear();
        }
#endif
    }
}
