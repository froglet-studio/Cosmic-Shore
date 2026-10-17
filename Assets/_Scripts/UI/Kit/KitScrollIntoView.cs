using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Scrolls the nearest parent <see cref="ScrollRect"/> so this control is fully in view when
    /// gamepad or keyboard focus lands on it. Without it the pad walks happily into rows the
    /// viewport has clipped away and the player loses the focus ring off the bottom of the screen
    /// (the arcade grid's rows 4-7, before the style-B rebuild).
    ///
    /// <para>A pointer selection is ignored: whatever was clicked is already under the pointer.
    /// Vertical scrolling only, and instant - the move is a reveal, not a motion.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Scroll Into View")]
    public class KitScrollIntoView : MonoBehaviour, ISelectHandler
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        [Tooltip("The rect to bring into view - a card when this sits on a control inside it. Empty: this object.")]
        [SerializeField] private RectTransform target;
        [Tooltip("Space kept between the revealed rect and the viewport edge (canvas units).")]
        [SerializeField] private float margin = 12f;

        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is PointerEventData) return;
            Reveal();
        }

        /// <summary>Bring the target fully into its scroll view now.</summary>
        public void Reveal()
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.vertical || !scroll.content) return;

            var content = scroll.content;
            var viewport = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            float contentHeight = content.rect.height;
            float viewHeight = viewport.rect.height;
            if (contentHeight <= viewHeight) return;

            // Distances from the content's top edge, in its own space.
            var rect = target ? target : (RectTransform)transform;
            rect.GetWorldCorners(Corners);
            float top = content.rect.yMax - content.InverseTransformPoint(Corners[1]).y - margin;
            float bottom = content.rect.yMax - content.InverseTransformPoint(Corners[0]).y + margin;

            float scrollable = contentHeight - viewHeight;
            float offset = (1f - scroll.verticalNormalizedPosition) * scrollable;
            if (top < offset) offset = top;
            else if (bottom > offset + viewHeight) offset = bottom - viewHeight;
            else return;

            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
        }
    }
}
