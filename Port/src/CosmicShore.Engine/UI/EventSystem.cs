using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// The event-system hub (original contract): owns selection state, the raycast
    /// fan-out over enabled raycasters, and the navigation-events flag the menu
    /// toggles for freestyle input ownership. Pointer processing lives in
    /// <see cref="StandaloneInputModule"/> (the original's module split) — hardware
    /// backends and headless tests both inject through the module's synthetic API.
    /// </summary>
    public class EventSystem : MonoBehaviour
    {
        /// <summary>The active event system (the original's singleton access pattern).</summary>
        public static EventSystem current { get; set; }

        /// <summary>Whether gamepad/keyboard navigation events are dispatched (menu freestyle gate).</summary>
        public bool sendNavigationEvents = true;

        /// <summary>Pixels a pointer must move from its press point before a drag starts.</summary>
        public int pixelDragThreshold = 10;

        public GameObject currentSelectedGameObject { get; private set; }

        bool m_SelectionGuard;

        void OnEnable()
        {
            if (current == null) current = this;
        }

        void OnDisable()
        {
            if (current == this) current = null;
        }

        public void SetSelectedGameObject(GameObject selected) => SetSelectedGameObject(selected, new BaseEventData(this));

        public void SetSelectedGameObject(GameObject selected, BaseEventData pointer)
        {
            if (m_SelectionGuard) return; // original rule: no re-entrant selection changes
            if (currentSelectedGameObject == selected) return;

            m_SelectionGuard = true;
            var previous = currentSelectedGameObject;
            currentSelectedGameObject = selected;
            ExecuteEvents.Execute(previous, pointer, ExecuteEvents.deselectHandler);
            ExecuteEvents.Execute(selected, pointer, ExecuteEvents.selectHandler);
            m_SelectionGuard = false;
        }

        /// <summary>
        /// Every hit under the event position across all enabled raycasters, sorted
        /// topmost-first (canvas sortingOrder desc, then draw depth desc).
        /// </summary>
        public void RaycastAll(PointerEventData eventData, List<RaycastResult> raycastResults)
        {
            raycastResults.Clear();
            foreach (var module in BaseRaycaster.ActiveRaycasters)
                module.Raycast(eventData, raycastResults);

            raycastResults.Sort(s_RaycastComparer);
        }

        static readonly Comparison<RaycastResult> s_RaycastComparer = (lhs, rhs) =>
        {
            if (lhs.sortingOrder != rhs.sortingOrder) return rhs.sortingOrder.CompareTo(lhs.sortingOrder);
            if (lhs.module == rhs.module && lhs.depth != rhs.depth) return rhs.depth.CompareTo(lhs.depth);
            return lhs.index.CompareTo(rhs.index);
        };

        /// <summary>True while a pointer processed by the module is over a raycast target.</summary>
        public bool IsPointerOverGameObject()
        {
            var module = gameObject.GetComponent<StandaloneInputModule>();
            return module != null && module.IsPointerOverGameObject();
        }
    }
}
