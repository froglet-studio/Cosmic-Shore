using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The drawing half of a guided path (<see cref="MenuGuide"/>): a semi-transparent dim over the
    /// whole menu with a cut-out around the ONE control the player should press next, a pulsing
    /// call-to-action frame on that cut-out, and an optional caption beside it.
    ///
    /// <para><b>Dimmed means dead.</b> The dim is a raycast target everywhere except its cut-outs,
    /// so a press anywhere else lands on the dim and does nothing, while a press inside a cut-out
    /// falls through to the real control underneath (<see cref="ICanvasRaycastFilter"/>). The
    /// player presses the real button; this component presses nothing.</para>
    ///
    /// <para><b>Settings is always cut out</b> (<see cref="MenuGuide.CollectAlwaysAvailable"/>),
    /// and while Settings is open the whole spotlight stands aside.</para>
    ///
    /// <para><b>The pad is held on the path</b>: while a gamepad is connected, a selection that
    /// wanders off the target (or a Settings control) is put back on the target, so A presses the
    /// right thing and the D-pad cannot reach a dimmed one.</para>
    ///
    /// <para>Built entirely in code on its own screen-space overlay canvas, so no scene has to
    /// carry one and none can be missing it. It FADES in and out and the cut-out GLIDES between
    /// targets - continuity of existence applies to UI too.</para>
    /// </summary>
    public class MenuSpotlight : MonoBehaviour
    {
        const int SortingOrder = 32000;
        const float FadeInSeconds = 0.35f;
        const float FadeOutSeconds = 0.2f;
        const float GlideSharpness = 10f;
        const float AllowedRefreshSeconds = 1f;

        static MenuSpotlight _instance;

        Canvas _canvas;
        CanvasGroup _group;
        SpotlightDimGraphic _dim;
        SpotlightFrameGraphic _frame;
        TextMeshProUGUI _caption;

        RectTransform _target;
        string _captionText;
        bool _wantVisible;
        bool _hasHole;
        Rect _hole;
        float _allowedRefreshAt;
        readonly List<RectTransform> _allowed = new();
        readonly List<Rect> _holes = new();

        /// <summary>The spotlight in the active scene, built on first use.</summary>
        public static MenuSpotlight Ensure()
        {
            if (_instance) return _instance;

            var go = new GameObject("MenuSpotlight", typeof(RectTransform));
            _instance = go.AddComponent<MenuSpotlight>();
            _instance.Build();
            return _instance;
        }

        /// <summary>
        /// Point the spotlight at <paramref name="target"/>. Called every frame by the driver with
        /// whatever the current step is; re-pointing glides the cut-out rather than jumping it.
        /// </summary>
        public void Show(RectTransform target, string caption, Color cta, float dimAlpha)
        {
            _wantVisible = target;
            _target = target;
            _captionText = caption;
            _dim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(dimAlpha));
            _frame.color = new Color(cta.r, cta.g, cta.b, 1f);
            _caption.color = new Color(1f, 1f, 1f, 1f);
        }

        /// <summary>Fade the spotlight out (the player is flying, Settings is open, or the path ended).</summary>
        public void Hide()
        {
            _wantVisible = false;
            _target = null;
        }

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = SortingOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _dim = MakeChild<SpotlightDimGraphic>("Dim");
            _dim.raycastTarget = true;
            _dim.Holes = _holes;

            _frame = MakeChild<SpotlightFrameGraphic>("CallToAction");
            _frame.raycastTarget = false;

            var captionRect = new GameObject("Caption", typeof(RectTransform)).GetComponent<RectTransform>();
            captionRect.SetParent(transform, false);
            captionRect.anchorMin = captionRect.anchorMax = Vector2.zero;
            captionRect.pivot = new Vector2(0.5f, 0.5f);
            _caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
            _caption.alignment = TextAlignmentOptions.Center;
            _caption.raycastTarget = false;
            _caption.textWrappingMode = TextWrappingModes.Normal;
        }

        T MakeChild<T>(string childName) where T : Graphic
        {
            var rect = new GameObject(childName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.zero;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect.gameObject.AddComponent<T>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            bool visible = _wantVisible && _target && _target.gameObject.activeInHierarchy;

            float rate = dt / (visible ? FadeInSeconds : FadeOutSeconds);
            _group.alpha = Mathf.MoveTowards(_group.alpha, visible ? 1f : 0f, rate);
            _group.blocksRaycasts = visible;
            if (!visible)
            {
                if (_group.alpha <= 0f) _hasHole = false;
                return;
            }

            if (!TryScreenRect(_target, out var rect)) return;

            float scale = Mathf.Max(0.5f, Screen.height / 1080f);
            rect = Inflate(rect, 10f * scale);

            _hole = _hasHole && _group.alpha > 0f
                ? Lerp(_hole, rect, 1f - Mathf.Exp(-GlideSharpness * dt))
                : rect;
            _hasHole = true;

            if (Time.unscaledTime >= _allowedRefreshAt)
            {
                MenuGuide.CollectAlwaysAvailable(_allowed);
                _allowedRefreshAt = Time.unscaledTime + AllowedRefreshSeconds;
            }

            _holes.Clear();
            _holes.Add(_hole);
            foreach (var allowed in _allowed)
                if (allowed && TryScreenRect(allowed, out var a))
                    _holes.Add(Inflate(a, 4f * scale));
            _dim.SetVerticesDirty();

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.8f);
            _frame.Inner = Inflate(_hole, pulse * 6f * scale);
            _frame.Thickness = 4f * scale;
            _frame.Glow = 22f * scale;
            var c = _frame.color;
            c.a = Mathf.Lerp(0.6f, 1f, pulse);
            _frame.color = c;
            _frame.SetVerticesDirty();

            PlaceCaption(scale);
            HoldPadOnPath();
        }

        void PlaceCaption(float scale)
        {
            bool show = !string.IsNullOrEmpty(_captionText);
            _caption.gameObject.SetActive(show);
            if (!show) return;

            if (_caption.text != _captionText) _caption.text = _captionText;
            if (!_caption.font || _caption.font == TMP_Settings.defaultFontAsset)
            {
                var font = ResolveFont();
                if (font) _caption.font = font;
            }

            float width = Mathf.Min(760f * scale, Screen.width - 40f * scale);
            float height = 120f * scale;
            _caption.fontSize = 36f * scale;
            var rect = _caption.rectTransform;
            rect.sizeDelta = new Vector2(width, height);

            float gap = 28f * scale + height * 0.5f;
            float above = _hole.yMax + gap;
            float below = _hole.yMin - gap;
            float y = above + height * 0.5f <= Screen.height ? above : below;
            float x = Mathf.Clamp(_hole.center.x, width * 0.5f, Screen.width - width * 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        TMP_FontAsset ResolveFont()
        {
            var near = _target ? _target.GetComponentInChildren<TMP_Text>(true) : null;
            if (near && near.font) return near.font;
            var any = FindFirstObjectByType<TMP_Text>();
            return any && any != _caption ? any.font : null;
        }

        /// <summary>Put a pad selection that left the path back on the target.</summary>
        void HoldPadOnPath()
        {
            if (Gamepad.current == null) return;
            var es = EventSystem.current;
            if (!es || !es.sendNavigationEvents) return;

            var selected = es.currentSelectedGameObject;
            if (selected && OnPath(selected.transform)) return;

            var selectable = _target.GetComponentInChildren<Selectable>();
            if (!selectable) selectable = _target.GetComponentInParent<Selectable>();
            if (selectable && selectable.gameObject != selected)
                es.SetSelectedGameObject(selectable.gameObject);
        }

        bool OnPath(Transform t)
        {
            if (t.IsChildOf(_target) || _target.IsChildOf(t)) return true;
            foreach (var allowed in _allowed)
                if (allowed && t.IsChildOf(allowed)) return true;
            return false;
        }

        // ── Geometry ────────────────────────────────────────────────────────

        static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>
        /// <paramref name="rect"/>'s on-screen rectangle in pixels, clipped to any mask above it (a
        /// card half inside a scroll view is pressable only on the half that is drawn).
        /// </summary>
        public static bool TryScreenRect(RectTransform rect, out Rect screen)
        {
            screen = ToScreen(rect);
            for (var p = rect.parent; p; p = p.parent)
            {
                if (p.GetComponent<RectMask2D>() || p.GetComponent<Mask>())
                {
                    var clip = ToScreen((RectTransform)p);
                    float xMin = Mathf.Max(screen.xMin, clip.xMin), yMin = Mathf.Max(screen.yMin, clip.yMin);
                    float xMax = Mathf.Min(screen.xMax, clip.xMax), yMax = Mathf.Min(screen.yMax, clip.yMax);
                    screen = Rect.MinMaxRect(xMin, yMin, Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
                }
            }
            return screen.width > 1f && screen.height > 1f;
        }

        static Rect ToScreen(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

            rect.GetWorldCorners(Corners);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Rect Inflate(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        static Rect Lerp(Rect a, Rect b, float t) =>
            Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
                            Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));
    }
}
