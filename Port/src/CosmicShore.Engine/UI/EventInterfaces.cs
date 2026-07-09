namespace CosmicShore.Engine.UI
{
    // Original contract: the event-system handler interfaces (the original engine's
    // EventSystems namespace folds into CosmicShore.Engine.UI alongside the UI types,
    // so ported files keep a single using). A component implements the interfaces it
    // cares about; the input module finds the nearest ancestor handler per event type
    // (ExecuteEvents.GetEventHandler) and dispatches.

    /// <summary>Marker base for all event-system handler interfaces.</summary>
    public interface IEventSystemHandler { }

    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }

    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }
    public interface IScrollHandler : IEventSystemHandler { void OnScroll(PointerEventData eventData); }

    public interface ISelectHandler : IEventSystemHandler { void OnSelect(BaseEventData eventData); }
    public interface IDeselectHandler : IEventSystemHandler { void OnDeselect(BaseEventData eventData); }
    public interface ISubmitHandler : IEventSystemHandler { void OnSubmit(BaseEventData eventData); }
    public interface ICancelHandler : IEventSystemHandler { void OnCancel(BaseEventData eventData); }
}
