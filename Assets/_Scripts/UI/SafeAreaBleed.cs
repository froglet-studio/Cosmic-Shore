using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Carries a background <see cref="Image"/> out to the screen edges, so a panel laid out inside
    /// the safe area on a notched phone still fills the screen, as it does on a desktop.
    ///
    /// <para>The content layer split (<c>Docs/UI_ARCHITECTURE_AUDIT.md</c> §1.3) insets a whole
    /// panel, background included. Its background then stopped at the notch margin and the rounded
    /// corners, the 3D scene showed around it ("leaking corners"), and the NavBar's band stopped
    /// short of the edges.</para>
    ///
    /// <para><b>How.</b> The source element's rect is left alone, so nothing laid out against it
    /// moves. This component lives on a copy of the image: the source's FIRST child, stretched past
    /// the source on each side that should reach the screen edge.
    /// <list type="bullet">
    /// <item>As a child, the copy follows the source's active state and any CanvasGroup fade; as the
    /// first child, it draws under the source's content.</item>
    /// <item>The source is hidden through its CanvasRenderer alpha, so a semi-transparent gradient
    /// does not stack into a darker one. Its own <see cref="Graphic.color"/> is untouched and it
    /// still blocks taps.</item>
    /// <item>Every frame the copy mirrors the source's colour, sprite and enabled state, so a fade
    /// or a hide written to the source still shows.</item>
    /// </list></para>
    ///
    /// <para><b>How far.</b> Each side reaches the screen edge: the gap between the source and its
    /// reference rect (a screen panel, or the content layer) plus that side's safe-area margin.
    /// That covers both full-width backgrounds and 1920-wide panels centred in a wider safe area.</para>
    ///
    /// <para><see cref="ScreenSwitcher"/> builds these, only once a display has margins, and calls
    /// <see cref="Apply"/> whenever the margins change. A display whose safe area is the screen
    /// never gets one.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SafeAreaBleed : MonoBehaviour
    {
        /// <summary>Name of every copy this creates.</summary>
        public const string CopyName = "Safe Area Bleed";

        private static readonly Vector3[] Corners = new Vector3[4];

        private Image _source;
        private Image _image;
        private RectTransform _rt;
        private RectTransform _reference;
        private bool _left, _bottom, _right, _top;

        // The last margins asked for, re-applied when a measurement had to wait (a zero scale).
        private Vector4 _inset;
        private Vector3 _canvasScale = Vector3.one;
        private bool _pending;

        /// <summary>
        /// True for an image worth bleeding: enabled, visibly coloured, drawn whole, and not already
        /// bled. Skipped:
        /// <list type="bullet">
        /// <item>a Filled image, because its copy would ignore the fill;</item>
        /// <item>a Mask's graphic, which is a stencil, not a background;</item>
        /// <item>a button's tint target, whose highlight lives on its CanvasRenderer and would not
        /// carry over to the copy.</item>
        /// </list>
        /// </summary>
        public static bool CanBleed(Image image)
        {
            if (!image || !image.enabled || image.color.a <= 0.01f || image.type == Image.Type.Filled) return false;
            if (image.GetComponent<Mask>()) return false;
            if (image.TryGetComponent<Selectable>(out var selectable) && selectable.targetGraphic == image) return false;
            Transform t = image.transform;
            return !(t.childCount > 0 && t.GetChild(0).name == CopyName);
        }

        /// <summary>
        /// Creates the copy under <paramref name="source"/>. Its extent is measured against
        /// <paramref name="reference"/>, the safe-area rect the source was laid out in.
        /// </summary>
        public static SafeAreaBleed Create(Image source, RectTransform reference,
                                           bool left, bool bottom, bool right, bool top)
        {
            var go = new GameObject(CopyName, typeof(RectTransform), typeof(Image), typeof(LayoutElement))
            {
                layer = source.gameObject.layer
            };
            var rt = (RectTransform)go.transform;
            rt.SetParent(source.rectTransform, false);
            rt.SetAsFirstSibling();
            SafeAreaLayer.Stretch(rt);
            go.GetComponent<LayoutElement>().ignoreLayout = true;

            var image = go.GetComponent<Image>();
            image.type = source.type;
            image.material = source.material;
            image.preserveAspect = source.preserveAspect;
            image.fillCenter = source.fillCenter;
            image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            image.maskable = source.maskable;
            image.raycastTarget = false;

            var bleed = go.AddComponent<SafeAreaBleed>();
            bleed._source = source;
            bleed._image = image;
            bleed._rt = rt;
            bleed._reference = reference;
            bleed._left = left;
            bleed._bottom = bottom;
            bleed._right = right;
            bleed._top = top;
            bleed.Sync();
            return bleed;
        }

        /// <summary>
        /// Sizes the copy for the margins <paramref name="inset"/>: (left, bottom, right, top) in
        /// the root canvas's units.
        /// </summary>
        public void Apply(Vector4 inset, Vector3 canvasLossyScale)
        {
            _inset = inset;
            _canvasScale = canvasLossyScale;
            _pending = !TryApply();
        }

        private bool TryApply()
        {
            if (!_rt || !_source || !_reference) return true;

            Vector3 src = _source.rectTransform.lossyScale;
            Vector3 refScale = _reference.lossyScale;
            // A window scaled to nothing (mid-animation) cannot be measured; try again next frame.
            if (Mathf.Abs(src.x) < 1e-5f || Mathf.Abs(src.y) < 1e-5f
                || Mathf.Abs(refScale.x) < 1e-5f || Mathf.Abs(refScale.y) < 1e-5f) return false;

            _source.rectTransform.GetWorldCorners(Corners);
            Vector2 min = _reference.InverseTransformPoint(Corners[0]);
            Vector2 max = _reference.InverseTransformPoint(Corners[2]);
            Rect r = _reference.rect;

            // Canvas units -> reference units, then reference units -> source units.
            float cx = _canvasScale.x / refScale.x, cy = _canvasScale.y / refScale.y;
            float rx = refScale.x / src.x, ry = refScale.y / src.y;

            float left = Mathf.Max(0f, (min.x - r.xMin) + _inset.x * cx) * rx;
            float bottom = Mathf.Max(0f, (min.y - r.yMin) + _inset.y * cy) * ry;
            float right = Mathf.Max(0f, (r.xMax - max.x) + _inset.z * cx) * rx;
            float top = Mathf.Max(0f, (r.yMax - max.y) + _inset.w * cy) * ry;

            _rt.offsetMin = new Vector2(_left ? -left : 0f, _bottom ? -bottom : 0f);
            _rt.offsetMax = new Vector2(_right ? right : 0f, _top ? top : 0f);
            return true;
        }

        private void LateUpdate()
        {
            Sync();
            if (_pending) _pending = !TryApply();
        }

        private void Sync()
        {
            if (!_source || !_image) return;

            if (_image.enabled != _source.enabled) _image.enabled = _source.enabled;
            if (_image.color != _source.color) _image.color = _source.color;
            if (_image.sprite != _source.sprite) _image.sprite = _source.sprite;

            // The copy draws the background now. A cross-fade on the source can restore its
            // CanvasRenderer alpha, so this is re-asserted every frame, not set once.
            if (_source.canvasRenderer.GetAlpha() > 0f) _source.canvasRenderer.SetAlpha(0f);
        }
    }
}
