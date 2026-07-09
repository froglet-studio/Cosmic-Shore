using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// The pointer state machine (original contract: the standalone input module —
    /// enter/exit tracking, press/click pairing, drag threshold and drag dispatch).
    /// First-party seam: hardware backends (Arc H's Silk.NET mouse) and headless tests
    /// both drive it through the synthetic API — <see cref="PointerMove"/>,
    /// <see cref="PointerDown"/>, <see cref="PointerUp"/>, <see cref="Scroll"/> — so
    /// the dispatch rules are proven long before a window exists.
    ///
    /// Dispatch rules preserved from the original:
    /// - Press target = first IPointerDownHandler up the chain, else the click
    ///   handler's owner (so a click on a Button's child icon lands on the Button).
    /// - Click fires only when the press target still owns the click handler under
    ///   the pointer at release AND the press stayed click-eligible.
    /// - A drag starting on a DIFFERENT object than the press releases the press and
    ///   kills click eligibility; enter/exit walk the hover chain to the common root.
    /// - Pointer-down deselects the current selection unless the press lands on it
    ///   (the pressed Selectable then selects itself in OnPointerDown).
    /// </summary>
    public class StandaloneInputModule : MonoBehaviour
    {
        EventSystem m_EventSystem;
        EventSystem eventSystem => m_EventSystem ??= gameObject.GetComponent<EventSystem>();

        PointerEventData m_PointerData;
        readonly List<RaycastResult> m_RaycastResults = new();

        PointerEventData pointerData => m_PointerData ??= new PointerEventData(eventSystem) { pointerId = -1 };

        /// <summary>True when the pointer's last raycast hit anything.</summary>
        public bool IsPointerOverGameObject() => m_PointerData is { pointerCurrentRaycast: { isValid: true } };

        // ── synthetic injection API ──────────────────────────────────

        public void PointerMove(Vector2 position)
        {
            var e = pointerData;
            e.delta = position - e.position;
            e.position = position;
            e.pointerCurrentRaycast = RaycastAt(position);

            HandlePointerExitAndEnter(e, e.pointerCurrentRaycast.gameObject);
            ProcessDrag(e);
        }

        public void PointerDown(Vector2 position, PointerEventData.InputButton button = PointerEventData.InputButton.Left)
        {
            var e = pointerData;
            e.delta = Vector2.zero;
            e.position = position;
            e.button = button;
            e.pointerCurrentRaycast = RaycastAt(position);
            HandlePointerExitAndEnter(e, e.pointerCurrentRaycast.gameObject);

            var currentOverGo = e.pointerCurrentRaycast.gameObject;
            e.eligibleForClick = true;
            e.dragging = false;
            e.useDragThreshold = true;
            e.pressPosition = position;
            e.pointerPressRaycast = e.pointerCurrentRaycast;

            // Original rule: pressing outside the current selection deselects it; the
            // pressed Selectable (if any) re-selects itself in OnPointerDown.
            var selectHandler = ExecuteEvents.GetEventHandler<ISelectHandler>(currentOverGo);
            if (eventSystem != null && selectHandler != eventSystem.currentSelectedGameObject)
                eventSystem.SetSelectedGameObject(null, e);

            var newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, e, ExecuteEvents.pointerDownHandler)
                             ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);

            float time = Time.unscaledTime;
            if (newPressed == e.pointerPress && time - e.clickTime < 0.3f) e.clickCount++;
            else e.clickCount = 1;
            e.clickTime = time;

            e.pointerPress = newPressed;
            e.rawPointerPress = currentOverGo;
            e.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
        }

        public void PointerUp(Vector2 position)
        {
            var e = pointerData;
            e.position = position;
            e.pointerCurrentRaycast = RaycastAt(position);
            var currentOverGo = e.pointerCurrentRaycast.gameObject;

            ExecuteEvents.Execute(e.pointerPress, e, ExecuteEvents.pointerUpHandler);

            // Click only when the release still lands on the pressed handler's object.
            var pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
            if (e.pointerPress == pointerUpHandler && e.eligibleForClick)
                ExecuteEvents.Execute(e.pointerPress, e, ExecuteEvents.pointerClickHandler);

            if (e.pointerDrag != null && e.dragging)
                ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.endDragHandler);

            e.eligibleForClick = false;
            e.pointerPress = null;
            e.rawPointerPress = null;
            e.dragging = false;
            e.pointerDrag = null;

            HandlePointerExitAndEnter(e, currentOverGo);
        }

        /// <summary>
        /// Steps the selection along the navigation graph (gamepad dpad/stick, arrow
        /// keys). Gated on <see cref="EventSystem.sendNavigationEvents"/> — the menu
        /// turns that off in freestyle so the pad flies the ship, not the UI.
        /// </summary>
        public void Move(MoveDirection direction)
        {
            if (eventSystem == null || !eventSystem.sendNavigationEvents) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;

            var axisData = new AxisEventData(eventSystem)
            {
                moveDir = direction,
                moveVector = direction switch
                {
                    MoveDirection.Left => Vector2.left,
                    MoveDirection.Right => Vector2.right,
                    MoveDirection.Up => Vector2.up,
                    MoveDirection.Down => Vector2.down,
                    _ => Vector2.zero,
                },
            };
            ExecuteEvents.Execute(selected, axisData, ExecuteEvents.moveHandler);
        }

        /// <summary>Submit (gamepad A / Enter) on the current selection — nav-gated.</summary>
        public void Submit()
        {
            if (eventSystem == null || !eventSystem.sendNavigationEvents) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        }

        /// <summary>Cancel (gamepad B / Escape) on the current selection — nav-gated.</summary>
        public void Cancel()
        {
            if (eventSystem == null || !eventSystem.sendNavigationEvents) return;
            var selected = eventSystem.currentSelectedGameObject;
            if (selected == null) return;
            ExecuteEvents.Execute(selected, new BaseEventData(eventSystem), ExecuteEvents.cancelHandler);
        }

        public void Scroll(Vector2 scrollDelta, Vector2 position)
        {
            var e = pointerData;
            e.position = position;
            e.scrollDelta = scrollDelta;
            e.pointerCurrentRaycast = RaycastAt(position);
            var hit = e.pointerCurrentRaycast.gameObject;
            ExecuteEvents.ExecuteHierarchy(hit, e, ExecuteEvents.scrollHandler);
        }

        // ── internals ────────────────────────────────────────────────

        RaycastResult RaycastAt(Vector2 position)
        {
            if (eventSystem == null) return default;
            var probe = pointerData;
            probe.position = position;
            eventSystem.RaycastAll(probe, m_RaycastResults);
            return m_RaycastResults.Count > 0 ? m_RaycastResults[0] : default;
        }

        void ProcessDrag(PointerEventData e)
        {
            if (e.pointerDrag == null) return;

            if (!e.dragging && ShouldStartDrag(e))
            {
                // Original rule: dragging a different object than the press releases
                // the press and makes the gesture a drag, not a click.
                if (e.pointerPress != e.pointerDrag)
                {
                    ExecuteEvents.Execute(e.pointerPress, e, ExecuteEvents.pointerUpHandler);
                    e.eligibleForClick = false;
                    e.pointerPress = null;
                    e.rawPointerPress = null;
                }
                ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.beginDragHandler);
                e.dragging = true;
            }

            if (e.dragging && e.IsPointerMoving())
                ExecuteEvents.Execute(e.pointerDrag, e, ExecuteEvents.dragHandler);
        }

        bool ShouldStartDrag(PointerEventData e)
        {
            if (!e.eligibleForClick && e.pointerPress == null && !e.dragging) return false;
            if (e.pointerDrag == null) return false;
            if (!e.useDragThreshold) return true;
            float threshold = eventSystem != null ? eventSystem.pixelDragThreshold : 10;
            return (e.pressPosition - e.position).sqrMagnitude >= threshold * threshold;
        }

        /// <summary>Exit the old hover chain up to the common root, then enter the new chain.</summary>
        void HandlePointerExitAndEnter(PointerEventData e, GameObject newEnterTarget)
        {
            if (e.pointerEnter == newEnterTarget) return;

            var commonRoot = FindCommonRoot(e.pointerEnter, newEnterTarget);

            // Exit from the old target up to (excluding) the common root.
            if (e.pointerEnter != null)
            {
                for (var t = e.pointerEnter.transform; t is not null; t = t.parent)
                {
                    if (commonRoot != null && commonRoot.transform == t) break;
                    ExecuteEvents.Execute(t.gameObject, e, ExecuteEvents.pointerExitHandler);
                    e.hovered.Remove(t.gameObject);
                }
            }

            e.pointerEnter = newEnterTarget;

            // Enter from the new target up to (excluding) the common root.
            if (newEnterTarget != null)
            {
                for (var t = newEnterTarget.transform; t is not null; t = t.parent)
                {
                    if (commonRoot != null && commonRoot.transform == t) break;
                    ExecuteEvents.Execute(t.gameObject, e, ExecuteEvents.pointerEnterHandler);
                    e.hovered.Add(t.gameObject);
                }
            }
        }

        static GameObject FindCommonRoot(GameObject a, GameObject b)
        {
            if (a == null || b == null) return null;
            for (var ta = a.transform; ta is not null; ta = ta.parent)
                for (var tb = b.transform; tb is not null; tb = tb.parent)
                    if (ReferenceEquals(ta, tb)) return ta.gameObject;
            return null;
        }
    }
}
