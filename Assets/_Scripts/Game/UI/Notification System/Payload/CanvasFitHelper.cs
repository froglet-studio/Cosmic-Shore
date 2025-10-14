using UnityEngine;

namespace CosmicShore.Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CanvasFitHelper : MonoBehaviour
    {
        RectTransform _rt;

        void Awake() => _rt = (RectTransform)transform;

        /// <summary>
        /// Given the on-screen anchoredPosition, returns an off-screen pos
        /// that fully hides the rect in the specified direction (plus padding).
        /// </summary>
        public Vector2 GetOffscreenPos(Vector2 showPos, SlideDirection dir, float paddingPx)
        {
            if (_rt == null) _rt = (RectTransform)transform;

            var size = _rt.rect.size; // already scaled by CanvasScaler
            switch (dir)
            {
                case SlideDirection.FromRight:
                    return showPos + new Vector2(size.x + paddingPx, 0f);
                case SlideDirection.FromLeft:
                    return showPos - new Vector2(size.x + paddingPx, 0f);
                case SlideDirection.FromTop:
                    return showPos + new Vector2(0f, size.y + paddingPx);
                case SlideDirection.FromBottom:
                    return showPos - new Vector2(0f, size.y + paddingPx);
                default:
                    return showPos;
            }
        }
    }
}