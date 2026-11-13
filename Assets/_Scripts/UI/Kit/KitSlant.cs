using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A leaning quad: a parallelogram with level top and bottom edges whose top edge sits to the right of its
    /// bottom edge by <c>tan(lean) x height</c> (Rev 94 C, Docs/MENU_REDESIGN_B.md section 177.2: the domain bands
    /// and the captains' cards lean 14 degrees). It fills its rect's height; the rect's width is the BOX of the
    /// lean, so the shape itself is the rect's width less the lean's reach.
    ///
    /// <para>With a <see cref="sprite"/> it shows the picture cropped to the shape, cover-fitted with
    /// <see cref="focus"/> as the point of the picture kept in view (CSS object-position), so a portrait needs no
    /// mask and no stencil. Without one it is a flat field in the graphic's colour.</para>
    ///
    /// <para><see cref="Paint"/> gives the shape a colour of its own, carried on the vertices: a colour that
    /// comes from the worn colour set at runtime (a domain's rim or face) is painted that way, so the graphic's
    /// serialized colour stays a theme token and the UI theme's sweep never reads a domain colour as one of its
    /// own.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Slant")]
    public class KitSlant : MaskableGraphic
    {
        [Tooltip("How far the shape leans, in degrees from upright. Positive leans the top to the right.")]
        [SerializeField, Range(-60f, 60f)] float leanDegrees = 14f;
        [Tooltip("Optional picture, cropped to the shape and cover-fitted. Empty draws a flat field.")]
        [SerializeField] Sprite sprite;
        [Tooltip("The point of the picture kept in view when it is cropped: (0.5, 0.5) is its centre, y = 1 its top.")]
        [SerializeField] Vector2 focus = new(0.5f, 0.5f);

        bool _painted;
        Color _paint;

        public override Texture mainTexture => sprite ? sprite.texture : s_WhiteTexture;

        /// <summary>The picture (null draws a flat field).</summary>
        public Sprite Sprite
        {
            get => sprite;
            set
            {
                if (sprite == value) return;
                sprite = value;
                SetAllDirty();
            }
        }

        /// <summary>Paint the shape in <paramref name="colour"/> instead of the graphic's own colour.</summary>
        public void Paint(Color colour)
        {
            if (_painted && _paint == colour) return;
            _painted = true;
            _paint = colour;
            SetVerticesDirty();
        }

        /// <summary>How far the top edge sits to the right of the bottom edge for a shape this tall.</summary>
        public static float Reach(float leanDegrees, float height) => Mathf.Tan(leanDegrees * Mathf.Deg2Rad) * height;

        /// <summary>
        /// The shape's four corners in a rect, in the order bottom-left, top-left, top-right, bottom-right. The
        /// lean's reach is taken out of the rect's width, so the shape never leaves its rect.
        /// </summary>
        public static void Corners(Rect rect, float leanDegrees, Vector2[] corners)
        {
            float reach = Mathf.Clamp(Reach(leanDegrees, rect.height), -rect.width, rect.width);
            float left = reach > 0f ? 0f : -reach, top = reach > 0f ? reach : 0f;
            float width = rect.width - Mathf.Abs(reach);
            corners[0] = new Vector2(rect.xMin + left, rect.yMin);
            corners[1] = new Vector2(rect.xMin + top, rect.yMax);
            corners[2] = new Vector2(rect.xMin + top + width, rect.yMax);
            corners[3] = new Vector2(rect.xMin + left + width, rect.yMin);
        }

        /// <summary>
        /// The part of a picture (0..1 on both axes) that covers a rect of <paramref name="rectAspect"/> (width
        /// over height) without stretching, keeping <paramref name="focus"/> in view: (x, y, width, height).
        /// </summary>
        public static Rect CoverWindow(float pictureAspect, float rectAspect, Vector2 focus)
        {
            if (pictureAspect <= 0f || rectAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float w = 1f, h = 1f;
            if (pictureAspect > rectAspect) w = rectAspect / pictureAspect;   // the picture is wider: crop its sides
            else h = pictureAspect / rectAspect;                              // taller: crop its top and bottom
            return new Rect((1f - w) * Mathf.Clamp01(focus.x), (1f - h) * Mathf.Clamp01(focus.y), w, h);
        }

        static readonly Vector2[] s_corners = new Vector2[4];

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            Corners(rect, leanDegrees, s_corners);

            // The picture's window, then where each corner falls in the rect, so the picture stays upright.
            Rect window = new(0f, 0f, 1f, 1f);
            Vector4 outer = new(0f, 0f, 1f, 1f);
            if (sprite)
            {
                outer = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
                Rect pixels = sprite.rect;
                window = CoverWindow(pixels.width / Mathf.Max(1f, pixels.height), rect.width / rect.height, focus);
            }

            Color32 colour = _painted ? _paint : color;
            for (int i = 0; i < 4; i++)
            {
                float u = window.x + window.width * Mathf.InverseLerp(rect.xMin, rect.xMax, s_corners[i].x);
                float v = window.y + window.height * Mathf.InverseLerp(rect.yMin, rect.yMax, s_corners[i].y);
                vh.AddVert(s_corners[i], colour,
                           new Vector2(Mathf.Lerp(outer.x, outer.z, u), Mathf.Lerp(outer.y, outer.w, v)));
            }
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
