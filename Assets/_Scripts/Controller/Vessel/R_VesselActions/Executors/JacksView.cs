using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The herding view (the lab's Steady camera, <c>camUpdate</c>): a second, orthographic camera looking straight down the
    /// hull's plane, <see cref="JacksConfigSO.ViewSpan"/> across the screen's short side (framed so a big thumb
    /// barely covers the smallest ring on a full-size phone), rendering the real world. It does not turn with the
    /// hull: its heading is the last one laid onto the new plane (<see cref="JacksMath.SteadyForward"/>), and it pans
    /// only when the framed goal (the herd, weighted toward the hull while flying) leaves a slack box (<see cref="JacksMath.SteadyPan"/>).
    ///
    /// <para><b>Which is big</b> (the Wrasse's two views, <c>KiteNavigatorView</c>): herding, this view has the screen and
    /// the game's own camera is lifted into the small square in the lower left; flying, the game's camera has the screen and
    /// this view is the small square. The game camera's rect and depth are restored the moment the jacks fly again, the
    /// camera changes, or the view is disposed.</para>
    ///
    /// <para><b>Herding</b> (Garrett, 2026-10-24: "the herding view should have a view that is zoomed in a bit more and
    /// follows the jacks. the player can use a button to zoom out to the whole cell if they want to have a view of more
    /// fauna"): the span is <see cref="JacksConfigSO.HerdViewZoom"/> of the thumb-framed one and the framing follows the
    /// jacks (<see cref="JacksConfigSO.HerdFollowJacks"/>); the zoom button (top right; gamepad Y, F) eases the view out to
    /// the whole cell and back, and the call button under it is the herding call on a phone.</para>
    ///
    /// <para>The rings and dots it shows are 2D marks, drawn through an <see cref="UnprocessedOverlay"/> so the view's bloom
    /// does not smear them (the world under them keeps it), and no Panini bows it (<see cref="OrthographicLens"/>). The small
    /// view wears the style guide's frame (<see cref="PipViewFrame"/>).</para>
    /// </summary>
    sealed class JacksView
    {
        readonly JacksExecutor _ex;
        readonly GameObject _root;
        readonly Camera _cam;
        readonly UnprocessedOverlay _marks;
        readonly PipViewFrame _frame = new();
        Vector3 _centre, _forward;
        bool _placed;

        // the whole-cell view (the zoom button): 0 = the jacks' view, 1 = the cell, eased
        bool _cellView;
        float _cell01;

        /// <summary>The herding view's two buttons (a phone's way to the call and the zoom; the pad and keys have their own).</summary>
        internal enum HerdButton { None, Zoom, Call }

        readonly Button _zoomButton = new(), _callButton = new();
        bool _herdingNow;

        Camera _pilot;
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;

        internal JacksView(JacksExecutor ex)
        {
            _ex = ex;
            _root = new GameObject("JacksView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _root.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.enabled = false;
            _marks = new UnprocessedOverlay(_cam, "JacksMarks");
        }

        internal Camera Camera => _cam;

        /// <summary>The camera, and the layer, the view's 2D marks are drawn for: after its post-processing.</summary>
        internal Camera MarksCamera => _marks.Camera;
        internal int MarksLayer => _marks.Layer;

        internal void Dispose()
        {
            RestorePilot();
            _park.Release();
            _frame.Dispose();
            _zoomButton.Dispose();
            _callButton.Dispose();
            _marks.Dispose();
            if (_root) Object.Destroy(_root);
        }

        internal void Frame(bool herding, float dt)
        {
            var cfg = _ex.Config;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot) { RestorePilot(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            // one view (the lowest preset, CrewSingleView): the jacks' camera draws only while the human herds
            bool single = CrewSingleView.On;
            _cam.enabled = herding || !single;
            if (_pilot) AdoptPilotLook(_pilot);

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float m = cfg.ViewMargin, h = cfg.PipSize, aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            var pip = new Rect(m / aspect, m, h / aspect, h);   // a square in the lower left
            if (herding)
            {
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.depth = baseDepth + 1f;
                if (single)
                {
                    // nothing over the herd's view: the pilot's camera, hidden under it, draws nothing
                    RestorePilot();
                    _park.Park(_pilot, Color.black);
                }
                else if (_pilot)
                {
                    _park.Release();
                    _pilot.rect = pip;
                    _pilot.depth = baseDepth + 2f;
                    _pilotLifted = true;
                }
            }
            else
            {
                RestorePilot();
                _park.Release();
                _cam.rect = pip;
                _cam.depth = baseDepth + 1f;
            }

            // the span across the view's SHORT side: herding, the thumb-framed span zoomed in (HerdViewZoom), eased out to the
            // whole cell while the zoom button holds it there; netting, the thumb-framed span
            _herdingNow = herding;
            if (!herding) _cellView = false;
            _cell01 = Mathf.MoveTowards(_cell01, _cellView ? 1f : 0f, cfg.ViewZoomEase * dt);
            var cell = _cell01 > 0f ? _ex.CellHere : null;
            float jacksSpan = herding ? cfg.ViewSpan * cfg.HerdViewZoom : cfg.ViewSpan;
            float cellSpan = cell ? 2f * cell.MembraneRadius * cfg.CellViewMargin : jacksSpan;
            float zoom = Mathf.SmoothStep(0f, 1f, _cell01);
            float half = 0.5f * Mathf.Lerp(jacksSpan, Mathf.Max(jacksSpan, cellSpan), zoom), camAspect = Mathf.Max(1e-3f, _cam.aspect);
            _cam.orthographicSize = camAspect >= 1f ? half : half / camAspect;
            _cam.farClipPlane = 2f * Mathf.Max(cfg.ViewDepth, cell ? cell.MembraneRadius : 0f) + 1f;

            Vector3 up = _ex.BoardUp;
            // Herding, the view follows the jacks (HerdFollowJacks; the herd alone at 0), so the herd under the thumbs stays
            // in view; netting, the herd weighted toward the hull. The whole cell's view frames the cell.
            var noHerd = herding ? _ex.JacksCentre : _ex.HullPosition;
            float lean = herding ? cfg.HerdHullWeight : cfg.FlightHullWeight;
            var goal = _ex.OnPlane(JacksMath.FrameGoal(_ex.HerdCentre, _ex.HerdCount, _ex.HullPosition, lean, noHerd));
            if (herding) goal = Vector3.Lerp(goal, _ex.OnPlane(_ex.JacksCentre), cfg.HerdFollowJacks);
            if (cell) goal = Vector3.Lerp(goal, _ex.OnPlane(cell.transform.position), zoom);
            if (!_placed)
            {
                _centre = goal;
                _forward = _ex.BoardForward;
                _placed = true;
            }
            _forward = JacksMath.SteadyForward(_forward, up, _ex.BoardForward);
            _centre = _ex.OnPlane(_centre);
            var right = Vector3.Cross(up, _forward).normalized;
            float halfH = _cam.orthographicSize, halfW = halfH * camAspect, slack = cfg.CamSlack;
            var off = goal - _centre;
            var pan = JacksMath.SteadyPan(Vector3.Dot(off, right), Vector3.Dot(off, _forward), halfW * slack, halfH * slack, dt,
                                                cfg.ViewPanEase);
            _centre += right * pan.x + _forward * pan.y;

            float lift = Mathf.Max(cfg.ViewDepth, cell ? cell.MembraneRadius : 0f);
            _cam.transform.SetPositionAndRotation(_centre + up * lift, Quaternion.LookRotation(-up, _forward));
            _marks.Sync();
            _frame.Show(herding ? (_pilotLifted ? _pilot : null) : _cam);
            PlaceButtons(cfg, herding);
        }

        /// <summary>The zoom button: ease out to the whole cell, or back to the jacks (herding only).</summary>
        internal void ToggleCellView()
        {
            if (_herdingNow) _cellView = !_cellView;
        }

        /// <summary>Which of the herding view's buttons is under <paramref name="screen"/> (none while netting).</summary>
        internal HerdButton ButtonAt(Vector2 screen)
        {
            if (!_herdingNow) return HerdButton.None;
            if (_zoomButton.Contains(screen)) return HerdButton.Zoom;
            if (_callButton.Contains(screen)) return HerdButton.Call;
            return HerdButton.None;
        }

        // top right, the zoom button over the call button, in the style guide's frame (PipViewFrame), each with its
        // optional authored icon
        void PlaceButtons(JacksConfigSO cfg, bool herding)
        {
            if (!herding)
            {
                _zoomButton.Hide();
                _callButton.Hide();
                return;
            }
            float size = cfg.ZoomButtonSize * Screen.height, margin = cfg.ViewMargin * Screen.height;
            var zoom = new Rect(Screen.width - margin - size, Screen.height - margin - size, size, size);
            var call = new Rect(zoom.x, zoom.y - margin - size, size, size);
            _zoomButton.Show(zoom, cfg.ZoomButtonIcon);
            _callButton.Show(call, cfg.CallButtonIcon);
        }

        /// <summary>One of the herding view's buttons: a style-guide frame over a screen rect, an optional icon inside it. It
        /// takes no taps itself (the executor reads the touch and asks <see cref="ButtonAt"/>).</summary>
        sealed class Button
        {
            readonly PipViewFrame _frame = new();
            GameObject _iconGo;
            UnityEngine.UI.Image _icon;
            Rect _rect;
            bool _shown;

            internal bool Contains(Vector2 screen) => _shown && _rect.Contains(screen);

            internal void Show(Rect pixels, Sprite icon)
            {
                _rect = pixels;
                _shown = true;
                _frame.ShowRect(pixels);
                if (!icon) { if (_iconGo) _iconGo.SetActive(false); return; }
                if (!_icon)
                {
                    var canvas = PipViewFrame.Canvas;
                    if (!canvas) return;
                    _iconGo = new GameObject("JacksViewButtonIcon", typeof(RectTransform));
                    _iconGo.transform.SetParent(canvas.transform, false);
                    var rt = (RectTransform)_iconGo.transform;
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot = Vector2.zero;
                    _icon = _iconGo.AddComponent<UnityEngine.UI.Image>();
                    _icon.raycastTarget = false;
                    _icon.preserveAspect = true;
                }
                _iconGo.SetActive(true);
                _icon.sprite = icon;
                var r = (RectTransform)_iconGo.transform;
                float inset = 0.2f * pixels.width;
                r.anchoredPosition = pixels.position + Vector2.one * inset;
                r.sizeDelta = pixels.size - Vector2.one * (2f * inset);
            }

            internal void Hide()
            {
                _shown = false;
                _frame.Hide();
                if (_iconGo) _iconGo.SetActive(false);
            }

            internal void Dispose()
            {
                _frame.Dispose();
                if (_iconGo) Object.Destroy(_iconGo);
            }
        }

        /// <summary>
        /// Draw the way the game camera draws (Garrett, 2026-10-21: "the jacks view needs post processing for bloom at
        /// least"). A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's defaults - no post-processing, no
        /// anti-aliasing - and image quality lives on <see cref="UniversalAdditionalCameraData"/>, not on the
        /// <see cref="UnityEngine.Camera"/> (<c>MaelstromPreviewHost.AdoptGameCameraSettings</c> is the pattern). Read every
        /// frame, so a graphics setting that turns post-processing off for the game camera turns it off here too. The volume
        /// trigger is the game camera, so this view blends exactly the volumes the pilot's view does even though its lens
        /// sits far above the plane.
        /// </summary>
        void AdoptPilotLook(Camera pilot)
        {
            _cam.cullingMask = _park.CullingMask(pilot);
            _cam.clearFlags = _park.ClearFlags(pilot);
            _cam.backgroundColor = _park.Background(pilot);
            _cam.allowHDR = pilot.allowHDR;
            _cam.allowMSAA = pilot.allowMSAA;
            if (!pilot.TryGetComponent(out UniversalAdditionalCameraData from)) return;
            _data ??= _cam.GetUniversalAdditionalCameraData();
            if (!_data) return;
            _data.renderPostProcessing = _park.PostProcessing(pilot, from);
            _data.antialiasing = from.antialiasing;
            _data.antialiasingQuality = from.antialiasingQuality;
            _data.renderShadows = _park.Shadows(pilot, from);
            _data.volumeLayerMask = from.volumeLayerMask;
            _data.volumeTrigger = from.volumeTrigger ? from.volumeTrigger : pilot.transform;
        }

        UniversalAdditionalCameraData _data;
        // the pilot's camera asleep under the herd's view on the lowest preset (CrewSingleView)
        readonly PilotCameraPark _park = new();

        void RestorePilot()
        {
            if (_pilotLifted && _pilot)
            {
                _pilot.rect = _pilotRect;
                _pilot.depth = _pilotDepth;
            }
            _pilotLifted = false;
        }

        internal Vector2 WorldToScreen(Vector3 world) => _cam.WorldToScreenPoint(world);

        /// <summary>A screen pixel as the point under it on the hull's plane (the board).</summary>
        internal Vector3 ScreenToPlane(Vector2 screen) =>
            _ex.OnPlane(_cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, _cam.nearClipPlane)));

        /// <summary>A jack's stick run (world units along the view's right and up), kept inside the view less its margin: a
        /// stick never drives a jack off the screen.</summary>
        internal Vector3 RunOnScreen(Vector3 jack, Vector2 run)
        {
            var t = _cam.transform;
            var to = jack + t.right * run.x + t.up * run.y;
            var vp = _cam.WorldToViewportPoint(to);
            float m = _ex.Config.ViewMargin;
            vp.x = Mathf.Clamp(vp.x, m, 1f - m);
            vp.y = Mathf.Clamp(vp.y, m, 1f - m);
            return _ex.OnPlane(_cam.ViewportToWorldPoint(vp));
        }

        /// <summary>True when <paramref name="screen"/> is inside the small view in the lower left: this view while the jacks
        /// fly, the game's camera while they herd.</summary>
        internal bool PipContains(Vector2 screen, bool herding)
        {
            var small = herding ? (_pilotLifted ? _pilot : null) : _cam;
            return small && small.enabled && small.pixelRect.Contains(screen);
        }

        // A double tap on the small view switches between herding and flying (the Wrasse's touch swap, KiteNavigator.PipDoubleTapped): two SHORT taps,
        // the second soon after the first and near it, both inside the small view.
        const float PipTapMaxSeconds = 0.3f, PipDoubleTapWindowSeconds = 0.45f, PipDoubleTapMaxPixels = 120f;
        int _pipPressId = int.MinValue;
        float _pipPressAt, _pipLastTapAt = -99f;
        Vector2 _pipPressPos, _pipLastTapPos;

        internal bool PipDoubleTapped(bool herding)
        {
            bool doubled = false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.press.wasPressedThisFrame) PipPress(t.touchId.ReadValue(), t.position.ReadValue(), herding);
                    if (t.press.wasReleasedThisFrame) doubled |= PipRelease(t.touchId.ReadValue());
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) PipPress(-1, mouse.position.ReadValue(), herding);
                if (mouse.leftButton.wasReleasedThisFrame) doubled |= PipRelease(-1);
            }
            return doubled;
        }

        void PipPress(int id, Vector2 at, bool herding)
        {
            if (!PipContains(at, herding)) return;
            _pipPressId = id;
            _pipPressAt = Time.unscaledTime;
            _pipPressPos = at;
        }

        bool PipRelease(int id)
        {
            if (id != _pipPressId) return false;
            _pipPressId = int.MinValue;
            float now = Time.unscaledTime;
            if (now - _pipPressAt > PipTapMaxSeconds) return false;
            bool doubled = _pipPressAt - _pipLastTapAt <= PipDoubleTapWindowSeconds
                           && (_pipPressPos - _pipLastTapPos).sqrMagnitude <= PipDoubleTapMaxPixels * PipDoubleTapMaxPixels;
            _pipLastTapAt = doubled ? -99f : now;
            _pipLastTapPos = _pipPressPos;
            return doubled;
        }
    }
}
