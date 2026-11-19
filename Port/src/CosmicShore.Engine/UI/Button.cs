using System;
using CosmicShore.Engine.Events;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Clickable control (original contract: Button : Selectable). Grown in Arc D from
    /// the headless shim — the shim's contract is preserved exactly (ported code wires
    /// <see cref="onClick"/> listeners, toggles <see cref="Selectable.interactable"/>,
    /// and harnesses/tests may still call <c>onClick.Invoke()</c> directly) — and the
    /// event system now ALSO drives it: a raycast click or a submit lands here through
    /// the module and fires onClick when active + interactable.
    /// </summary>
    public class Button : Selectable, IPointerClickHandler, ISubmitHandler
    {
        /// <summary>Original contract: the onClick event's own type, so game code can replace the event wholesale.</summary>
        [Serializable] public class ButtonClickedEvent : UnityEvent { }

        public ButtonClickedEvent onClick = new();

        void Press()
        {
            if (!gameObject.activeInHierarchy || !IsInteractable()) return;
            onClick.Invoke();
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Press();
        }

        public virtual void OnSubmit(BaseEventData eventData) => Press();
    }
}
