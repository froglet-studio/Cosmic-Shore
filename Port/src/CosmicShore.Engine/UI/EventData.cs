using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// One hit from a raycaster (original contract: RaycastResult). Depth is the
    /// graphic's draw order within its canvas (hierarchy traversal order — later
    /// siblings draw on top); sortingOrder is the owning canvas's.
    /// </summary>
    public struct RaycastResult
    {
        public GameObject gameObject;
        public BaseRaycaster module;
        public float distance;
        public int index;
        public int depth;
        public int sortingOrder;
        public Vector2 screenPosition;

        public bool isValid => module != null && gameObject != null;

        public void Clear()
        {
            gameObject = null;
            module = null;
            distance = 0f;
            index = 0;
            depth = 0;
            sortingOrder = 0;
            screenPosition = Vector2.zero;
        }
    }

    /// <summary>Base payload for non-pointer events (original contract: BaseEventData).</summary>
    public class BaseEventData
    {
        readonly EventSystem m_EventSystem;

        public BaseEventData(EventSystem eventSystem) => m_EventSystem = eventSystem;

        public bool used { get; private set; }
        public void Use() => used = true;
        public void Reset() => used = false;

        public GameObject selectedObject
        {
            get => m_EventSystem != null ? m_EventSystem.currentSelectedGameObject : null;
            set => m_EventSystem?.SetSelectedGameObject(value, this);
        }
    }

    /// <summary>
    /// Pointer event payload (original contract: PointerEventData) — one instance per
    /// pointer, mutated across the frame's enter/exit/down/up/drag processing so
    /// handlers see the full press/drag context.
    /// </summary>
    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left = 0, Right = 1, Middle = 2 }

        public PointerEventData(EventSystem eventSystem) : base(eventSystem) { }

        public int pointerId = -1;

        /// <summary>Current screen position in pixels.</summary>
        public Vector2 position;

        /// <summary>Screen movement since the last event.</summary>
        public Vector2 delta;

        /// <summary>Screen position at press time (drag threshold measures from here).</summary>
        public Vector2 pressPosition;

        public Vector2 scrollDelta;
        public InputButton button = InputButton.Left;

        public int clickCount;
        public float clickTime;

        /// <summary>The object the pointer is currently over (enter/exit tracking).</summary>
        public GameObject pointerEnter;

        /// <summary>The handler object that received OnPointerDown for the active press.</summary>
        public GameObject pointerPress;

        /// <summary>The raw hit object at press time (before the handler walk).</summary>
        public GameObject rawPointerPress;

        /// <summary>The object receiving drag events for the active press.</summary>
        public GameObject pointerDrag;

        public bool dragging;
        public bool useDragThreshold = true;

        /// <summary>False once a drag starts — a completed drag is not a click (original rule).</summary>
        public bool eligibleForClick;

        public RaycastResult pointerCurrentRaycast;
        public RaycastResult pointerPressRaycast;

        /// <summary>Ancestors of pointerEnter that received OnPointerEnter (exit in reverse).</summary>
        public List<GameObject> hovered = new();

        public bool IsPointerMoving() => delta.sqrMagnitude > 0f;
    }
}
