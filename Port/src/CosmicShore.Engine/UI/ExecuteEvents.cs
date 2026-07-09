using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Handler dispatch (original contract: ExecuteEvents). Execute invokes every
    /// enabled component on ONE object implementing the handler interface;
    /// ExecuteHierarchy bubbles up the parent chain to the first object that handles;
    /// GetEventHandler finds that object without invoking — the input module pairs
    /// press/click/drag targets with it so a click on a Button's child icon still
    /// lands on the Button.
    /// </summary>
    public static class ExecuteEvents
    {
        public delegate void EventFunction<T>(T handler, BaseEventData eventData) where T : class, IEventSystemHandler;

        public static EventFunction<IPointerEnterHandler> pointerEnterHandler { get; } =
            (handler, data) => handler.OnPointerEnter((PointerEventData)data);
        public static EventFunction<IPointerExitHandler> pointerExitHandler { get; } =
            (handler, data) => handler.OnPointerExit((PointerEventData)data);
        public static EventFunction<IPointerDownHandler> pointerDownHandler { get; } =
            (handler, data) => handler.OnPointerDown((PointerEventData)data);
        public static EventFunction<IPointerUpHandler> pointerUpHandler { get; } =
            (handler, data) => handler.OnPointerUp((PointerEventData)data);
        public static EventFunction<IPointerClickHandler> pointerClickHandler { get; } =
            (handler, data) => handler.OnPointerClick((PointerEventData)data);
        public static EventFunction<IBeginDragHandler> beginDragHandler { get; } =
            (handler, data) => handler.OnBeginDrag((PointerEventData)data);
        public static EventFunction<IDragHandler> dragHandler { get; } =
            (handler, data) => handler.OnDrag((PointerEventData)data);
        public static EventFunction<IEndDragHandler> endDragHandler { get; } =
            (handler, data) => handler.OnEndDrag((PointerEventData)data);
        public static EventFunction<IScrollHandler> scrollHandler { get; } =
            (handler, data) => handler.OnScroll((PointerEventData)data);
        public static EventFunction<ISelectHandler> selectHandler { get; } =
            (handler, data) => handler.OnSelect(data);
        public static EventFunction<IDeselectHandler> deselectHandler { get; } =
            (handler, data) => handler.OnDeselect(data);
        public static EventFunction<ISubmitHandler> submitHandler { get; } =
            (handler, data) => handler.OnSubmit(data);
        public static EventFunction<ICancelHandler> cancelHandler { get; } =
            (handler, data) => handler.OnCancel(data);

        /// <summary>Invokes the handler on every eligible component of this ONE object.</summary>
        public static bool Execute<T>(GameObject target, BaseEventData eventData, EventFunction<T> functor)
            where T : class, IEventSystemHandler
        {
            if (target == null || target.IsDestroyed) return false;

            bool any = false;
            foreach (var component in target.GetComponents<T>())
            {
                if (!IsEligible(component)) continue;
                functor((T)component, eventData);
                any = true;
            }
            return any;
        }

        /// <summary>Bubbles up from <paramref name="root"/>, invoking on the FIRST object that handles.</summary>
        public static GameObject ExecuteHierarchy<T>(GameObject root, BaseEventData eventData, EventFunction<T> functor)
            where T : class, IEventSystemHandler
        {
            for (var t = root != null ? root.transform : null; t is not null; t = t.parent)
            {
                if (Execute(t.gameObject, eventData, functor))
                    return t.gameObject;
            }
            return null;
        }

        /// <summary>The nearest object at-or-above <paramref name="root"/> with an eligible handler (no invoke).</summary>
        public static GameObject GetEventHandler<T>(GameObject root) where T : class, IEventSystemHandler
        {
            for (var t = root != null ? root.transform : null; t is not null; t = t.parent)
            {
                if (CanHandleEvent<T>(t.gameObject))
                    return t.gameObject;
            }
            return null;
        }

        public static bool CanHandleEvent<T>(GameObject target) where T : class, IEventSystemHandler
        {
            if (target == null || target.IsDestroyed) return false;
            foreach (var component in target.GetComponents<T>())
                if (IsEligible(component)) return true;
            return false;
        }

        // Original rule: disabled Behaviours don't receive events; plain components
        // (no enabled flag) always do.
        static bool IsEligible(object component) =>
            component is not Behaviour { isActiveAndEnabled: false };
    }
}
