using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Turn, then glide (Docs/MENU_REDESIGN_B.md section 154, picked by Shombith 2026-11-08: "Turn then glide"). The phone
    /// turns the screen as it always has; then every posed piece of this canvas starts where the turn left it - at its old
    /// place on the glass, turned with the phone - and glides to its new place, level, over <see cref="glideSeconds"/>.
    ///
    /// <para>What glides: each <see cref="PortraitRect"/> that moves its rect or is a PC block fitted to the width
    /// (Menu_Main's screens and windows), and with <see cref="glideDirectChildren"/> the canvas's own children too (GameCanvas,
    /// which has no poses: its corner-anchored HUD re-lays itself). A piece inside another gliding piece rides its parent.</para>
    ///
    /// <para>How: while a turn is possible (<see cref="ScreenOrientationPolicy.PortraitAllowed"/>), the screen rect of each
    /// piece is noted every frame. On the switch (<see cref="AdaptiveCanvasScaler.PortraitLayoutChanging"/>) the UI is held
    /// out for <see cref="holdFrames"/> while the new layout settles (the fitted blocks re-fit a frame late); then each piece
    /// is put at its old screen place carried through the turn (<see cref="CarryAcrossTurn"/>), turned by the turn's angle
    /// and scaled to its old size, and eased back to its new pose. Input waits until the glide ends. Only this component's
    /// own CanvasGroup and the pieces' local position, rotation and scale are written, and each is put back exactly.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AdaptiveCanvasScaler))]
    [AddComponentMenu("Cosmic Shore/UI/Orientation Glide")]
    public class OrientationGlide : MonoBehaviour
    {
        [Tooltip("The root canvas's own CanvasGroup: out for the hold, input off until the glide ends. Nothing else may drive it.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("Frames the UI stays out after the switch while the new layout settles.")]
        [SerializeField, Min(0)] private int holdFrames = 2;

        [Tooltip("Seconds each piece takes to glide from where the turn left it to its new place.")]
        [SerializeField, Min(0.01f)] private float glideSeconds = 0.35f;

        [Tooltip("The glide's curve.")]
        [SerializeField] private KitEase ease = KitEase.OutCubic;

        [Tooltip("Also glide the canvas's own children (a canvas with no PortraitRect poses, like GameCanvas).")]
        [SerializeField] private bool glideDirectChildren;

        private struct Piece
        {
            public RectTransform Rect;
            public bool Seen;
            public Vector2 OldCenter, OldSize;          // screen px before the turn
            public Vector3 RestPos, StartPos, RestScale, StartScale;
            public Quaternion RestRot, StartRot;
        }

        private readonly List<Piece> _pieces = new();
        private readonly List<PortraitRect> _scratch = new();
        private AdaptiveCanvasScaler _scaler;
        private Canvas _canvas;
        private Vector2 _oldScreen;
        private ScreenOrientation _oldOrientation = ScreenOrientation.LandscapeLeft;
        private float _angle;
        private int _hold = -1;
        private float _clock = -1f;
        private bool _listDirty = true;

        /// <summary>Whether a turn is being smoothed right now.</summary>
        public bool Gliding => _hold >= 0 || _clock >= 0f;

        /// <summary>
        /// The turn of the glass from one orientation to another, in degrees about the screen's normal (screen space, y up):
        /// what was upright before the turn now reads turned by this much. Landscape-left is the phone turned a quarter
        /// counter-clockwise from upright, so turning it back upright carries the old picture a quarter clockwise (-90).
        /// </summary>
        public static float TurnAngle(ScreenOrientation from, ScreenOrientation to)
        {
            static float Of(ScreenOrientation o) => o switch
            {
                ScreenOrientation.LandscapeLeft => 90f,
                ScreenOrientation.LandscapeRight => -90f,
                ScreenOrientation.PortraitUpsideDown => 180f,
                _ => 0f,
            };
            return Mathf.DeltaAngle(0f, Of(to) - Of(from));
        }

        /// <summary>A point on the old glass carried through the turn onto the new glass: about the screens' centres,
        /// turned by <paramref name="angle"/> degrees.</summary>
        public static Vector2 CarryAcrossTurn(Vector2 point, Vector2 oldScreen, Vector2 newScreen, float angle)
        {
            var d = point - oldScreen * 0.5f;
            float r = angle * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return newScreen * 0.5f + new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        /// <summary>The glide's progress <paramref name="elapsed"/> seconds into a glide of <paramref name="seconds"/>.</summary>
        public static float ProgressAt(float elapsed, float seconds, KitEase ease) =>
            seconds <= 0f ? 1f : KitMotion.Ease(ease, Mathf.Clamp01(elapsed / seconds));

        private void Awake()
        {
            TryGetComponent(out _scaler);
            TryGetComponent(out _canvas);
        }

        private void OnEnable()
        {
            if (_scaler) _scaler.PortraitLayoutChanging += OnTurning;
            _listDirty = true;
        }

        private void OnDisable()
        {
            if (_scaler) _scaler.PortraitLayoutChanging -= OnTurning;
            Settle();
        }

        private Camera EventCamera => _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private void OnTurning(bool upright)
        {
            if (!group) return;
            if (Gliding) Settle();
            _angle = TurnAngle(_oldOrientation, upright ? ScreenOrientation.Portrait
                                   : _oldOrientation == ScreenOrientation.Portrait ? Landscape() : _oldOrientation);
            _hold = holdFrames;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        // the landscape the phone is turning into: what the OS reports, else landscape-left (the game's own)
        private static ScreenOrientation Landscape() =>
            Screen.orientation == ScreenOrientation.LandscapeRight ? ScreenOrientation.LandscapeRight : ScreenOrientation.LandscapeLeft;

        private void LateUpdate()
        {
            if (!group) return;
            if (_hold >= 0)
            {
                if (_hold-- > 0) return;
                Begin();
                return;
            }
            if (_clock >= 0f)
            {
                _clock += Time.unscaledDeltaTime;
                Step(ProgressAt(_clock, glideSeconds, ease));
                if (_clock >= glideSeconds) Settle();
                return;
            }
            if (ScreenOrientationPolicy.PortraitAllowed) Note();
        }

        // while a turn is possible: every piece's place on the glass, so the turn has somewhere to start from
        private void Note()
        {
            if (_listDirty) Collect();
            var cam = EventCamera;
            _oldScreen = new Vector2(Screen.width, Screen.height);
            var o = Screen.orientation;
            if (o is ScreenOrientation.Portrait or ScreenOrientation.LandscapeLeft or ScreenOrientation.LandscapeRight) _oldOrientation = o;
            else _oldOrientation = Screen.height > Screen.width ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                p.Seen = p.Rect && p.Rect.gameObject.activeInHierarchy && ScreenRect(p.Rect, cam, out p.OldCenter, out p.OldSize);
                _pieces[i] = p;
            }
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static bool ScreenRect(RectTransform rect, Camera cam, out Vector2 center, out Vector2 size)
        {
            rect.GetWorldCorners(Corners);
            var a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(cam, Corners[1]);
            var c = RectTransformUtility.WorldToScreenPoint(cam, Corners[3]);
            center = (a + RectTransformUtility.WorldToScreenPoint(cam, Corners[2])) * 0.5f;
            size = new Vector2(Vector2.Distance(a, c), Vector2.Distance(a, b));
            return size.x > 0.5f && size.y > 0.5f;
        }

        private void Collect()
        {
            _listDirty = false;
            _pieces.Clear();
            var roots = new List<RectTransform>();
            GetComponentsInChildren(true, _scratch);
            foreach (var pr in _scratch)
                if (pr.MovesRect || pr.DrawsAtPcScale) roots.Add((RectTransform)pr.transform);
            if (glideDirectChildren)
                foreach (Transform child in transform)
                    if (child is RectTransform r) roots.Add(r);
            foreach (var r in roots)
            {
                bool nested = false;
                foreach (var other in roots)
                    if (other != r && r.IsChildOf(other)) { nested = true; break; }
                if (!nested) _pieces.Add(new Piece { Rect = r });
            }
        }

        // the new layout has settled: start each piece where the turn left it
        private void Begin()
        {
            var cam = EventCamera;
            var newScreen = new Vector2(Screen.width, Screen.height);
            var turn = Quaternion.Euler(0f, 0f, _angle);
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (!p.Seen || !p.Rect || !p.Rect.gameObject.activeInHierarchy || !ScreenRect(p.Rect, cam, out _, out var newSize)
                    || p.Rect.parent is not RectTransform parent)
                {
                    p.Seen = false; _pieces[i] = p; continue;
                }
                p.RestPos = p.Rect.localPosition; p.RestRot = p.Rect.localRotation; p.RestScale = p.Rect.localScale;
                var at = CarryAcrossTurn(p.OldCenter, _oldScreen, newScreen, _angle);
                // the rest pose's centre sits at RestPos plus the pivot's offset; move the centre, keep that offset
                ScreenRect(p.Rect, cam, out var newCenter, out _);
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, at, cam, out var wAt)
                    && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, newCenter, cam, out var wNow))
                    p.StartPos = p.RestPos + parent.InverseTransformVector(wAt - wNow);
                else
                    p.StartPos = p.RestPos;
                p.StartRot = turn * p.RestRot;
                var k = new Vector2(Mathf.Clamp(p.OldSize.x / newSize.x, 0.25f, 4f), Mathf.Clamp(p.OldSize.y / newSize.y, 0.25f, 4f));
                p.StartScale = Vector3.Scale(p.RestScale, new Vector3(k.x, k.y, 1f));
                _pieces[i] = p;
            }
            _clock = 0f;
            Step(0f);
            group.alpha = 1f;
        }

        private void Step(float e)
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (!p.Seen || !p.Rect) continue;
                p.Rect.localPosition = Vector3.LerpUnclamped(p.StartPos, p.RestPos, e);
                p.Rect.localRotation = Quaternion.SlerpUnclamped(p.StartRot, p.RestRot, e);
                p.Rect.localScale = Vector3.LerpUnclamped(p.StartScale, p.RestScale, e);
            }
        }

        private void Settle()
        {
            if (_clock >= 0f) Step(1f);
            for (int i = 0; i < _pieces.Count; i++) { var p = _pieces[i]; p.Seen = false; _pieces[i] = p; }
            _hold = -1;
            _clock = -1f;
            _listDirty = true;
            if (!group) return;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        /// <summary>Wire the glide from code (tests and editor tooling).</summary>
        public void SetGroup(CanvasGroup canvasGroup, int frames = 2, float seconds = 0.35f, bool directChildren = false)
        {
            group = canvasGroup; holdFrames = frames; glideSeconds = seconds; glideDirectChildren = directChildren;
        }
    }
}
