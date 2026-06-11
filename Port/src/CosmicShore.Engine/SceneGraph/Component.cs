using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>Base for everything attachable to a <see cref="GameObject"/>.</summary>
    public abstract class Component : Object
    {
        public GameObject gameObject { get; internal set; }

        public Transform transform => gameObject.transform;

        /// <summary>Component name proxies its GameObject's name (original engine contract).</summary>
        public override string name
        {
            get => gameObject is null ? base.name : gameObject.name;
            set { if (gameObject is null) base.name = value; else gameObject.name = value; }
        }

        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public bool TryGetComponent<T>(out T component) where T : class => gameObject.TryGetComponent(out component);
        public T[] GetComponents<T>() where T : class => gameObject.GetComponents<T>();
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class => gameObject.GetComponentInChildren<T>(includeInactive);
        public List<T> GetComponentsInChildren<T>(bool includeInactive = false) where T : class => gameObject.GetComponentsInChildren<T>(includeInactive);
        public T GetComponentInParent<T>(bool includeInactive = false) where T : class => gameObject.GetComponentInParent<T>(includeInactive);
        public T AddComponent<T>() where T : Component => gameObject.AddComponent<T>();

        /// <summary>Immediate destruction path used by DestroyImmediate and the end-of-frame queue.</summary>
        internal virtual void DestroyComponentNow()
        {
            gameObject?.RemoveComponentInternal(this);
            destroyedFlag = true;
        }
    }

    /// <summary>A component that can be enabled/disabled.</summary>
    public abstract class Behaviour : Component
    {
        bool _enabledSelf = true;

        public bool enabled
        {
            get => _enabledSelf;
            set
            {
                if (_enabledSelf == value) return;
                _enabledSelf = value;
                OnEnabledChanged(value);
            }
        }

        public bool isActiveAndEnabled => _enabledSelf && gameObject is not null && gameObject.activeInHierarchy && !destroyedFlag;

        internal virtual void OnEnabledChanged(bool value) { }
    }
}
