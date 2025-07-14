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
    public abstract class ScriptableDictionary<TKey, TValue> : ScriptableDictionaryBase, IReset, IDictionary<TKey, TValue>, IDrawObjectsInInspector
    {
        [Tooltip(
            "Clear the dictionary when:" +
            " Scene Loaded : when a scene is loaded." +
            " Application Start : Once, when the application starts. Modifications persist between scenes")]
        [SerializeField] private ResetType _resetOn = ResetType.SceneLoaded;

        [SerializeField] private List<TKey> _keys = new List<TKey>();
        [SerializeField] private List<TValue> _values = new List<TValue>();

        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

        // IDictionary
        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                if (_dictionary.ContainsKey(key))
                {
                    // Update existing
                    _dictionary[key] = value;
                    var idx = _keys.IndexOf(key);
                    if (idx >= 0) _values[idx] = value;
                    OnValueChanged?.Invoke(key, value);
#if UNITY_EDITOR
                    RepaintRequest?.Invoke();
#endif
                }
                else
                {
                    Add(key, value);
                }
            }
        }

        // events
        public event Action OnCountChanged;
        public event Action<TKey, TValue> OnPairAdded;
        public event Action<TKey, TValue> OnPairRemoved;
        public event Action<IEnumerable<KeyValuePair<TKey, TValue>>> OnPairsAdded;
        public event Action<IEnumerable<KeyValuePair<TKey, TValue>>> OnPairsRemoved;
        public event Action OnCleared;
        public event Action<TKey, TValue> OnValueChanged;

        public override Type GetGenericType => typeof(KeyValuePair<TKey, TValue>);

        #region Add

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            _keys.Add(key);
            _values.Add(value);
            OnCountChanged?.Invoke();
            OnPairAdded?.Invoke(key, value);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        public void Add(KeyValuePair<TKey, TValue> pair) => Add(pair.Key, pair.Value);

        public bool TryAdd(TKey key, TValue value)
        {
            if (_dictionary.ContainsKey(key)) return false;
            Add(key, value);
            return true;
        }

        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            var arr = items.ToArray();
            if (arr.Length == 0) return;

            foreach (var kv in arr)
            {
                _dictionary.Add(kv.Key, kv.Value);
                _keys.Add(kv.Key);
                _values.Add(kv.Value);
            }

            OnCountChanged?.Invoke();
            OnPairsAdded?.Invoke(arr);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        public bool TryAddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items == null) return false;
            var unique = items.Where(kv => !_dictionary.ContainsKey(kv.Key)).ToArray();
            if (unique.Length == 0) return false;
            AddRange(unique);
            return true;
        }

        #endregion

        #region Remove

        public bool Remove(TKey key)
        {
            if (!_dictionary.TryGetValue(key, out var val))
                return false;

            _dictionary.Remove(key);
            var idx = _keys.IndexOf(key);
            _keys.RemoveAt(idx);
            _values.RemoveAt(idx);

            OnCountChanged?.Invoke();
            OnPairRemoved?.Invoke(key, val);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
            return true;
        }

        public bool Remove(KeyValuePair<TKey, TValue> pair)
        {
            if (_dictionary.TryGetValue(pair.Key, out var existing) &&
                EqualityComparer<TValue>.Default.Equals(existing, pair.Value))
            {
                return Remove(pair.Key);
            }
            return false;
        }

        public bool RemoveRange(IEnumerable<TKey> keys)
        {
            var toRemove = keys.Where(k => _dictionary.ContainsKey(k)).ToArray();
            if (toRemove.Length == 0) return false;

            var removedPairs = new List<KeyValuePair<TKey, TValue>>(toRemove.Length);
            foreach (var k in toRemove)
            {
                var v = _dictionary[k];
                removedPairs.Add(new KeyValuePair<TKey, TValue>(k, v));
                _dictionary.Remove(k);
                var idx = _keys.IndexOf(k);
                _keys.RemoveAt(idx);
                _values.RemoveAt(idx);
            }

            OnCountChanged?.Invoke();
            OnPairsRemoved?.Invoke(removedPairs);
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
            return true;
        }

        #endregion

        #region IReadOnly IDictionary & ICollection

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
        public bool Contains(KeyValuePair<TKey, TValue> pair) => _dictionary.TryGetValue(pair.Key, out var v)
                                                              && EqualityComparer<TValue>.Default.Equals(v, pair.Value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var kv in _dictionary)
                array[arrayIndex++] = kv;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Clear & Reset

        public void Clear()
        {
            _dictionary.Clear();
            _keys.Clear();
            _values.Clear();
            OnCleared?.Invoke();
#if UNITY_EDITOR
            RepaintRequest?.Invoke();
#endif
        }

        private void Awake()
        {
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

        public void ResetToInitialValue() => Clear();

#if UNITY_EDITOR
        public void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                Clear();
        }
#endif

        #endregion

        #region Inspector Helpers

        // For IDrawObjectsInInspector: gather any UnityEngine.Object held in keys or values
        public List<Object> GetAllObjects()
        {
            var list = new List<Object>(Count * 2);
            list.AddRange(_keys.OfType<Object>());
            list.AddRange(_values.OfType<Object>());
            return list;
        }

        #endregion
    }
}