using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The jacks' view (the lab's Steady camera, <c>camUpdate</c>): a second, orthographic camera looking straight down the
    /// marlin's plane, <c>viewSpan</c> across the screen's short side, rendering the real world. It does not turn with the
    /// marlin: its heading is the last one laid onto the new plane (<see cref="MarlinJacksMath.SteadyForward"/>), and it pans
    /// only when the framed goal (the herd, weighted toward the marlin) leaves a slack box (<see cref="MarlinJacksMath.SteadyPan"/>).
    ///
    /// <para><b>Which is big</b> (the Wrasse's two views, <c>KiteNavigatorView</c>): running the jacks, this view has the
    /// screen and the game's own camera is lifted into the small square in the lower left; flying the marlin, the game's
    /// camera has the screen and this view is the small square. The game camera's rect and depth are restored the moment
    /// the human flies again, the camera changes, or the view is disposed.</para>
    /// </summary>
    sealed class MarlinJacksView
    {
        readonly MarlinJacksExecutor _ex;
        readonly GameObject _root;
        readonly Camera _cam;
        Vector3 _centre, _forward;
        bool _placed;

        Camera _pilot;
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;

        internal MarlinJacksView(MarlinJacksExecutor ex)
        {
            _ex = ex;
            _root = new GameObject("MarlinJacksView");   // at the scene root: the hull's scale and pose are not the view's
            _cam = _root.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.nearClipPlane = 0.1f;
            _cam.enabled = false;
        }

        internal Camera Camera => _cam;

        internal void Dispose()
        {
            RestorePilot();
            if (_root) Object.Destroy(_root);
        }

        internal void Frame(bool humanRunsJacks, float dt)
        {
            var cfg = _ex.Config;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot) { RestorePilot(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            _cam.enabled = true;
            if (_pilot)
            {
                _cam.cullingMask = _pilot.cullingMask;
                _cam.clearFlags = _pilot.clearFlags;
                _cam.backgroundColor = _pilot.backgroundColor;
            }

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float m = cfg.ViewMargin, h = cfg.PipSize, aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            var pip = new Rect(m / aspect, m, h / aspect, h);   // a square in the lower left
            if (humanRunsJacks)
            {
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.depth = baseDepth + 1f;
                if (_pilot)
                {
                    _pilot.rect = pip;
                    _pilot.depth = baseDepth + 2f;
                    _pilotLifted = true;
                }
            }
            else
            {
                RestorePilot();
                _cam.rect = pip;
                _cam.depth = baseDepth + 1f;
            }

            // viewSpan across the view's SHORT side
            float half = 0.5f * cfg.ViewSpan, camAspect = Mathf.Max(1e-3f, _cam.aspect);
            _cam.orthographicSize = camAspect >= 1f ? half : half / camAspect;
            _cam.farClipPlane = 2f * cfg.ViewDepth + 1f;

            Vector3 up = _ex.BoardUp;
            var goal = _ex.OnPlane(MarlinJacksMath.FrameGoal(_ex.HerdCentre, _ex.HerdCount, _ex.MarlinPosition, !humanRunsJacks));
            if (!_placed)
            {
                _centre = goal;
                _forward = _ex.BoardForward;
                _placed = true;
            }
            _forward = MarlinJacksMath.SteadyForward(_forward, up, _ex.BoardForward);
            _centre = _ex.OnPlane(_centre);
            var right = Vector3.Cross(up, _forward).normalized;
            float halfH = _cam.orthographicSize, halfW = halfH * camAspect, slack = cfg.CamSlack;
            var off = goal - _centre;
            var pan = MarlinJacksMath.SteadyPan(Vector3.Dot(off, right), Vector3.Dot(off, _forward), halfW * slack, halfH * slack, dt);
            _centre += right * pan.x + _forward * pan.y;

            _cam.transform.SetPositionAndRotation(_centre + up * cfg.ViewDepth, Quaternion.LookRotation(-up, _forward));
        }

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

        /// <summary>A screen pixel as the point under it on the marlin's plane.</summary>
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

        /// <summary>True when <paramref name="screen"/> is inside the small view in the lower left: this view while the human
        /// flies the marlin, the game's camera while they run the jacks.</summary>
        internal bool PipContains(Vector2 screen, bool humanRunsJacks)
        {
            var small = humanRunsJacks ? (_pilotLifted ? _pilot : null) : _cam;
            return small && small.enabled && small.pixelRect.Contains(screen);
        }

        // A double tap on the small view swaps seats (the Wrasse's touch swap, KiteNavigator.PipDoubleTapped): two SHORT taps,
        // the second soon after the first and near it, both inside the small view.
        const float PipTapMaxSeconds = 0.3f, PipDoubleTapWindowSeconds = 0.45f, PipDoubleTapMaxPixels = 120f;
        int _pipPressId = int.MinValue;
        float _pipPressAt, _pipLastTapAt = -99f;
        Vector2 _pipPressPos, _pipLastTapPos;

        internal bool PipDoubleTapped(bool humanRunsJacks)
        {
            bool doubled = false;
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.press.wasPressedThisFrame) PipPress(t.touchId.ReadValue(), t.position.ReadValue(), humanRunsJacks);
                    if (t.press.wasReleasedThisFrame) doubled |= PipRelease(t.touchId.ReadValue());
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) PipPress(-1, mouse.position.ReadValue(), humanRunsJacks);
                if (mouse.leftButton.wasReleasedThisFrame) doubled |= PipRelease(-1);
            }
            return doubled;
        }

        void PipPress(int id, Vector2 at, bool humanRunsJacks)
        {
            if (!PipContains(at, humanRunsJacks)) return;
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
