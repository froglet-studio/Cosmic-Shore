using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Sizes one child <see cref="Image"/> to COVER this rect at its sprite's aspect, centred - the
    /// style-B hero frames show a vessel preview edge to edge at any screen width. uGUI's
    /// <c>preserveAspect</c> only contains, so it letterboxes; put a <see cref="Mask"/> on this object
    /// to crop what overflows. Re-fits when this rect changes size and whenever the image's sprite
    /// changes - through <see cref="SetSprite"/> or by code that writes <c>Image.sprite</c> directly
    /// (the arcade's game and weekly cards do: their scripts predate the kit).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Cover Image")]
    public class KitCoverImage : MonoBehaviour
    {
        [Tooltip("The image to size. The fit anchors it to this rect's centre.")]
        [SerializeField] private Image image;

        // The sprite the current fit was computed for; a different one means fit again.
        private Sprite _fitted;

        public Image Image => image;

        public void SetSprite(Sprite sprite)
        {
            if (!image) return;
            image.sprite = sprite;
            Fit();
        }

        private void OnEnable() => Fit();

        private void OnRectTransformDimensionsChange() => Fit();

        private void LateUpdate()
        {
            if (image && image.sprite != _fitted) Fit();
        }

        public void Fit()
        {
            if (!image) return;
            _fitted = image.sprite;
            if (!_fitted) return;

            var size = ((RectTransform)transform).rect.size;
            var spriteRect = image.sprite.rect;
            if (size.x <= 0f || size.y <= 0f || spriteRect.height <= 0f) return;

            float aspect = spriteRect.width / spriteRect.height;
            var fitted = size.x / size.y > aspect
                ? new Vector2(size.x, size.x / aspect)
                : new Vector2(size.y * aspect, size.y);

            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = fitted;
        }
    }
}
