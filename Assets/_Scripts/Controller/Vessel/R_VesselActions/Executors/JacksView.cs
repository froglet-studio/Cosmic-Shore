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
    /// <para><b>One view at a time</b> (Garrett, 2026-10-24: "we can remove the pip from both modes"): herding, this view has
    /// the whole screen and the game's camera sleeps under it (<see cref="PilotCameraPark"/>); netting, this view is off and
    /// the game's camera has the screen. Herding, it zooms out as the jacks spread so neither reaches the screen's edge
    /// (<see cref="JacksConfigSO.HerdJacksFill"/>).</para>
    ///
    /// <para><b>Herding</b> (Garrett, 2026-10-24: "the herding view should have a view that is zoomed in a bit more and
    /// follows the jacks. the player can use a button to zoom out to the whole cell if they want to have a view of more
    /// fauna"): the span is <see cref="JacksConfigSO.HerdViewZoom"/> of the thumb-framed one and the framing follows the
    /// jacks (<see cref="JacksConfigSO.HerdFollowJacks"/>); the SPACE ability, Wide (its card; gamepad Y, F), eases the view
    /// out to the whole cell and back. The view draws no buttons of its own (Garrett, 2026-10-25: "on PC the gamepad view
    /// should only every have the 5 standard ability icons and any additional active controls should be to the left of
    /// those ... labeled with a glyph. this is always true for vessels"): every control is an ability card.</para>
    ///
    /// <para>The rings and dots it shows are 2D marks, drawn through an <see cref="UnprocessedOverlay"/> so the view's bloom
    /// does not smear them (the world under them keeps it), and no Panini bows it (<see cref="OrthographicLens"/>).</para>
    /// </summary>
    sealed class JacksView
    {
        readonly JacksExecutor _ex;
        readonly GameObject _root;
        readonly Camera _cam;
        readonly UnprocessedOverlay _marks;
        Vector3 _centre, _forward;
        bool _placed;

        // the whole-cell view (Wide, the Space ability): 0 = the jacks' view, 1 = the cell, eased
        float _cell01;

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
            _marks.Dispose();
            if (_root) Object.Destroy(_root);
        }

        /// <summary>Frame this frame's view: herding, the board from above (out to the whole cell while
        /// <paramref name="wide"/>); netting, nothing (the game's camera has the screen).</summary>
        internal void Frame(bool herding, bool wide, float dt)
        {
            var cfg = _ex.Config;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot) { RestorePilot(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            // ONE view at a time (Garrett, 2026-10-24: "we can remove the pip from both modes"): herding, the jacks' camera
            // has the whole screen and the pilot's camera sleeps under it, drawing nothing (CrewSingleView's park);
            // netting, the jacks' camera is off and the pilot's has the screen
            _cam.enabled = herding;
            if (_pilot) AdoptPilotLook(_pilot);
            RestorePilot();
            if (herding)
            {
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.depth = (_pilot ? _pilotDepth : 0f) + 1f;
                _park.Park(_pilot, Color.black);
            }
            else _park.Release();

            // the span across the view's SHORT side: herding, the thumb-framed span zoomed in (HerdViewZoom), eased out to the
            // whole cell while Wide holds it there; netting, the thumb-framed span
            _cell01 = Mathf.MoveTowards(_cell01, herding && wide ? 1f : 0f, cfg.ViewZoomEase * dt);
            var cell = _cell01 > 0f ? _ex.CellHere : null;
            float jacksSpan = herding ? cfg.ViewSpan * cfg.HerdViewZoom : cfg.ViewSpan;
            float cellSpan = cell ? 2f * cell.MembraneRadius * cfg.CellViewMargin : jacksSpan;
            float zoom = Mathf.SmoothStep(0f, 1f, _cell01);
            float half = 0.5f * Mathf.Lerp(jacksSpan, Mathf.Max(jacksSpan, cellSpan), zoom), camAspect = Mathf.Max(1e-3f, _cam.aspect);
            float baseSize = camAspect >= 1f ? half : half / camAspect;
            // herding, the view zooms out as the jacks spread so neither ever reaches the screen's edge (Garrett, 2026-10-24),
            // quickly out and calmly back in; FitSize below measures what they need
            _fitSize = Mathf.Max(_fitSize, baseSize);
            _cam.orthographicSize = _fitSize;
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

            float need = herding ? Mathf.Max(baseSize, FitSize(cfg, right, camAspect)) : baseSize;
            float rate = need > _fitSize ? FitOutEase : cfg.ViewZoomEase;
            _fitSize = Mathf.Lerp(_fitSize, need, JacksMath.Ease(rate, dt));
            _cam.orthographicSize = _fitSize;

            float lift = Mathf.Max(cfg.ViewDepth, cell ? cell.MembraneRadius : 0f);
            _cam.transform.SetPositionAndRotation(_centre + up * lift, Quaternion.LookRotation(-up, _forward));
            _marks.Sync();
        }

        // the zoom-out that keeps the jacks on screen eases out this fast (1/s): a jack flung to the edge is still in view
        const float FitOutEase = 8f;
        float _fitSize;

        /// <summary>The orthographic half-height that keeps both jacks, and <see cref="JacksConfigSO.HerdJacksMargin"/> round
        /// each, inside <see cref="JacksConfigSO.HerdJacksFill"/> of the view from its centre.</summary>
        float FitSize(JacksConfigSO cfg, Vector3 right, float camAspect)
        {
            float fill = Mathf.Max(0.05f, cfg.HerdJacksFill), size = 0f;
            for (int k = 0; k < 2; k++)
            {
                var d = _ex.JackWorld(k) - _centre;
                float x = Mathf.Abs(Vector3.Dot(d, right)) + cfg.HerdJacksMargin;
                float y = Mathf.Abs(Vector3.Dot(d, _forward)) + cfg.HerdJacksMargin;
                size = Mathf.Max(size, Mathf.Max(y, x / camAspect) / fill);
            }
            return size;
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
