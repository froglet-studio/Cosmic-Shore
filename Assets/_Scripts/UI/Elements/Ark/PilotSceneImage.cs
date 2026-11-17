using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A pilot's SCENE behind a page (Docs/Pilots/PORTRAITS.md §8): one widescreen picture, loaded by name while it
    /// is shown and let go when it is not, cut to fill its rectangle on any screen shape.
    ///
    /// <para><b>One picture in memory.</b> A scene is 1536x864 and there is one per callsign, so nothing references
    /// them: <see cref="Show"/> loads the one it is given from a Resources folder, and <see cref="Release"/> (or the
    /// next <see cref="Show"/>) unloads it. The new picture is loaded before the old one goes, so a step from pilot
    /// to pilot never shows the ground between.</para>
    ///
    /// <para><b>The cut</b> (<see cref="Frame"/>) is the scene kit's (<c>Tools/Build/pilot_art/scene.py</c>
    /// <c>crop_box</c>): the largest window of the view's shape, centred on the pilot's face from side to side with
    /// the face a little above the middle. On a view wider than the scene, where that would cut the top of a tall
    /// close-up, the window moves up until the whole close-up shows. Then, while the page's words sit beside the
    /// face, the window moves the least it takes for the face to stand in the part of the view the words leave
    /// clear.</para>
    ///
    /// <para><b>The screen's edges.</b> A page is laid out inside the safe area, and a picture is a background: it
    /// reaches the edges of the screen (the style guide's "backgrounds bleed to the edge"). <see cref="SafeAreaBleed"/>
    /// does that for an <see cref="Image"/> by copying it; a copy of this would be a second cut to keep in step, so
    /// the picture stretches its own rectangle instead. Nothing is laid out against it.</para>
    ///
    /// <para>The art never moves (style guide §7, "always still: avatars and art"): the fit is re-measured each frame
    /// the picture is shown, and writes only when the view or the words have moved.</para>
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class PilotSceneImage : MonoBehaviour
    {
        /// <summary>How far down the window the face's centre sits, from the top (the scene kit's crop_box).</summary>
        public const float FaceFromTop = 0.4f;

        [Header("Fit")]
        [Tooltip("Reach past the safe area to the screen's edges, as a background does.")]
        [SerializeField] private bool reachScreenEdges = true;

        [Tooltip("The page's words. While they sit beside the pilot's face, the cut keeps the face in the part of " +
                 "the view they leave clear. Its parent must fill this picture's parent. Optional.")]
        [SerializeField] private RectTransform keepClearOf;

        [Tooltip("Air kept between the pilot's face and the edges of the clear view, as a share of the view's width.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float faceMargin = 0.03f;

        const float EdgeTolerance = 0.01f;      // canvas units: a reach closer than this is not rewritten

        static readonly Vector3[] Corners = new Vector3[4];

        RawImage _raw;
        RectTransform _rect;
        Texture2D _texture;
        string _resource;
        Rect _face;

        /// <summary>True while a scene is on screen.</summary>
        public bool IsShowing => _texture;

        void Awake()
        {
            Cache();
            if (!_texture) Blank();
        }

        void OnDestroy() => Release();

        void LateUpdate() => Fit();

        /// <summary>
        /// Shows the scene at <paramref name="resourcePath"/> (under a Resources folder, no extension), cut around
        /// <paramref name="face"/> (0..1 from the bottom left). A path that names nothing shows nothing and says so.
        /// </summary>
        public void Show(string resourcePath, Rect face)
        {
            Cache();
            _face = face;
            if (!_texture || resourcePath != _resource)
            {
                var next = string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Texture2D>(resourcePath);
                if (!next && !string.IsNullOrEmpty(resourcePath))
                    CSDebug.LogWarning($"[PilotSceneImage] No scene at Resources/{resourcePath} " +
                                       "(run Tools/Build/author_pilot_scene_assets.py).", this);
                var old = _texture;
                _texture = next;
                _resource = next ? resourcePath : null;
                _raw.texture = next;
                if (old && old != next) Resources.UnloadAsset(old);
            }

            if (!_texture)
            {
                Blank();
                return;
            }
            _raw.enabled = true;
            enabled = true;
            Fit();
        }

        /// <summary>Lets the scene go: nothing is drawn and the picture leaves memory.</summary>
        public void Release()
        {
            var old = _texture;
            _texture = null;
            _resource = null;
            Blank();
            if (old) Resources.UnloadAsset(old);
        }

        void Cache()
        {
            if (_raw) return;
            TryGetComponent(out _raw);
            _rect = (RectTransform)transform;
        }

        // A RawImage with no texture draws a white rectangle, so it is switched off; so is this component, whose only
        // work is the fit of a picture that is shown.
        void Blank()
        {
            if (_raw)
            {
                _raw.texture = null;
                _raw.enabled = false;
            }
            enabled = false;
        }

        void Fit()
        {
            if (!_texture || !_raw) return;
            if (reachScreenEdges) Reach();

            Rect view = _rect.rect;
            if (view.width <= 0f || view.height <= 0f) return;
            Rect uv = Frame(view.width / view.height, (float)_texture.width / _texture.height, _face, Words(view), faceMargin);
            if (_raw.uvRect != uv) _raw.uvRect = uv;
        }

        // Full stretch, then out to the root canvas's edges. An offset is measured from the parent's own edge, so the
        // canvas's corners are brought into the parent's space.
        void Reach()
        {
            var parent = _rect.parent as RectTransform;
            Canvas canvas = _raw.canvas;
            if (!parent || !canvas) return;
            Vector3 scale = parent.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-5f || Mathf.Abs(scale.y) < 1e-5f) return;   // scaled to nothing: next frame

            ((RectTransform)canvas.rootCanvas.transform).GetWorldCorners(Corners);
            Vector2 min = parent.InverseTransformPoint(Corners[0]);
            Vector2 max = parent.InverseTransformPoint(Corners[2]);
            Rect inside = parent.rect;
            Vector2 offsetMin = min - inside.min;
            Vector2 offsetMax = max - inside.max;

            if (_rect.anchorMin != Vector2.zero) _rect.anchorMin = Vector2.zero;
            if (_rect.anchorMax != Vector2.one) _rect.anchorMax = Vector2.one;
            if ((_rect.offsetMin - offsetMin).sqrMagnitude > EdgeTolerance * EdgeTolerance) _rect.offsetMin = offsetMin;
            if ((_rect.offsetMax - offsetMax).sqrMagnitude > EdgeTolerance * EdgeTolerance) _rect.offsetMax = offsetMax;
        }

        // What the words cover, as a share of the view from its bottom left. Measured in the words' parent's own
        // space, which fills this picture's parent: a window's opening animation scales that parent, and a measure in
        // world space would slide the picture while it plays.
        Rect Words(Rect view)
        {
            if (!keepClearOf) return default;
            Rect words = keepClearOf.rect;
            Vector2 at = keepClearOf.localPosition;
            Vector2 mine = (Vector2)_rect.localPosition + view.min;
            return new Rect((at.x + words.xMin - mine.x) / view.width, (at.y + words.yMin - mine.y) / view.height,
                            words.width / view.width, words.height / view.height);
        }

        /// <summary>
        /// The part of a scene a view shows: a <see cref="RawImage.uvRect"/>, 0..1 from the bottom left.
        /// </summary>
        /// <param name="viewAspect">The view's width over its height.</param>
        /// <param name="textureAspect">The scene's width over its height.</param>
        /// <param name="face">The pilot's close-up in the scene, 0..1 from the bottom left. Empty: the cut is centred.</param>
        /// <param name="words">What the page's words cover, as a share of the view from its bottom left. Empty: none.</param>
        /// <param name="margin">Air kept between the face and the edges of the clear view, as a share of its width.</param>
        public static Rect Frame(float viewAspect, float textureAspect, Rect face, Rect words = default, float margin = 0f)
        {
            if (viewAspect <= 0f || textureAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);

            float w = 1f, h = 1f;
            if (viewAspect > textureAspect) h = textureAspect / viewAspect;     // a wider view: every column, fewer rows
            else w = viewAspect / textureAspect;                                // a narrower view: every row, fewer columns

            bool hasFace = face.width > 0f && face.height > 0f;
            float x = hasFace ? face.center.x - w * 0.5f : (1f - w) * 0.5f;
            float y = hasFace ? face.center.y - h * (1f - FaceFromTop) : (1f - h) * 0.5f;
            x = Mathf.Clamp(x, 0f, 1f - w);
            y = Mathf.Clamp(y, 0f, 1f - h);
            // A close-up shorter than the window shows whole: a wide view never trims the top of a tall one.
            if (hasFace && face.height <= h) y = Mathf.Clamp(y, face.yMax - h, face.yMin);

            if (hasFace && words.width > 0f && words.height > 0f)
            {
                // Only words BESIDE the face move the cut: on a tall view they sit under it.
                float faceBottom = (face.yMin - y) / h, faceTop = (face.yMax - y) / h;
                if (faceTop > words.yMin && faceBottom < words.yMax)
                {
                    // The clear part of the view is the wider side the words leave.
                    float clearMin = 0f, clearMax = 1f;
                    if (words.xMin >= 1f - words.xMax) clearMax = Mathf.Clamp01(words.xMin);
                    else clearMin = Mathf.Clamp01(words.xMax);
                    x = Mathf.Clamp(Slide(x, w, face.xMin, face.xMax, clearMin + margin, clearMax - margin), 0f, 1f - w);
                }
            }
            return new Rect(x, y, w, h);
        }

        // Where a window `size` wide starts so that [lo, hi] sits inside its clear part (from clearMin to clearMax of
        // the window), moving as little from `start` as that takes. A span wider than the clear part is centred in it.
        static float Slide(float start, float size, float lo, float hi, float clearMin, float clearMax)
        {
            if (clearMax <= clearMin) return start;
            float least = hi - size * clearMax, most = lo - size * clearMin;
            return least <= most
                ? Mathf.Clamp(start, least, most)
                : (lo + hi) * 0.5f - size * (clearMin + clearMax) * 0.5f;
        }
    }
}
