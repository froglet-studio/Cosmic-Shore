namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Base of the UI components (original: UnityEngine.EventSystems.UIBehaviour) — the
    /// lifecycle messages are protected virtuals so derived UI code writes
    /// <c>protected override void OnEnable() { base.OnEnable(); … }</c>.
    /// </summary>
    public abstract class UIBehaviour : MonoBehaviour
    {
        protected virtual void Awake() { }
        protected virtual void OnEnable() { }
        protected virtual void Start() { }
        protected virtual void OnDisable() { }
        protected virtual void OnDestroy() { }
        protected virtual void OnValidate() { }
        protected virtual void Reset() { }
        protected virtual void OnRectTransformDimensionsChange() { }
        protected virtual void OnBeforeTransformParentChanged() { }
        protected virtual void OnTransformParentChanged() { }
        protected virtual void OnDidApplyAnimationProperties() { }
        protected virtual void OnCanvasGroupChanged() { }
        protected virtual void OnCanvasHierarchyChanged() { }

        public virtual bool IsActive() => isActiveAndEnabled;
        public bool IsDestroyed() => this == null;
    }
}
