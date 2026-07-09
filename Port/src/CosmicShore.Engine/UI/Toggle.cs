using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// On/off control (original contract: Toggle : Selectable). The used surface —
    /// isOn / onValueChanged / SetIsOnWithoutNotify / interactable — is REAL; the
    /// checkmark shows through the graphic's alpha (the original CrossFades it —
    /// instant-apply headless, same deviation note as Selectable's tints). Toggle
    /// groups are unused in this project and intentionally not ported.
    /// </summary>
    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] bool m_IsOn = true;

        /// <summary>The checkmark graphic — alpha 1 when on, 0 when off.</summary>
        public Graphic graphic;

        public UnityEvent<bool> onValueChanged = new();

        public bool isOn
        {
            get => m_IsOn;
            set => Set(value, sendCallback: true);
        }

        /// <summary>State write without firing onValueChanged (UI sync paths).</summary>
        public void SetIsOnWithoutNotify(bool value) => Set(value, sendCallback: false);

        void Set(bool value, bool sendCallback)
        {
            if (m_IsOn == value) return;
            m_IsOn = value;
            PlayEffect();
            if (sendCallback) onValueChanged.Invoke(m_IsOn);
        }

        void PlayEffect()
        {
            if (graphic == null) return;
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, m_IsOn ? 1f : 0f);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            PlayEffect();
        }

        void InternalToggle()
        {
            if (!gameObject.activeInHierarchy || !IsInteractable()) return;
            isOn = !isOn;
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            InternalToggle();
        }

        public virtual void OnSubmit(BaseEventData eventData) => InternalToggle();
    }
}
