using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A strategy-game mouse camera for test and sandbox scenes (Docs/BLACK_HOLE.md §7) — the
    /// Transport Fever shape: the camera looks at a PIVOT from a distance, and the mouse moves the
    /// pivot, turns around it and dollies toward it.
    ///
    /// <list type="bullet">
    /// <item><b>Pan</b> (right-drag): the world moves with the cursor, as if grabbed — exact at the
    /// pivot's depth, because the world-per-pixel is derived from the distance and the FOV.</item>
    /// <item><b>Orbit</b> (left-drag on empty space, or Alt + right-drag): yaw and pitch about the
    /// pivot, pitch clamped short of the poles.</item>
    /// <item><b>Zoom</b> (wheel, or middle-drag): exponential in the distance, so every notch is the
    /// same PROPORTION at any range; the wheel zooms toward the point under the cursor, keeping it
    /// fixed on screen (exact — see <see cref="ZoomPivotTowardPoint"/>).</item>
    /// <item><b>Keys</b>: WASD pan, Q/E orbit, Shift faster, F or Home frames the home view. Ignored
    /// while a text field has focus, so typing a console command never flies the camera.</item>
    /// </list>
    ///
    /// A drag only STARTS when the press is not on UI, so the scene's buttons and the
    /// DiagnosticsHUD still click; once started, it continues over UI. Every speed and binding is in
    /// <see cref="MouseOrbitCameraConfigSO"/>. The camera runs on unscaled time, so it still works
    /// while the game is paused.
    ///
    /// Owners (a test harness) frame the scene through <see cref="SetHome"/>, <see cref="FrameHome"/>
    /// and <see cref="SetDistance"/> rather than writing the transform, which this component owns.
    /// Not a gameplay camera: vessels fly on <c>CustomCameraController</c>; nothing here touches it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MouseOrbitCamera : MonoBehaviour
    {
        public const string ConfigResourcePath = "MouseOrbitCameraConfig";

        [Tooltip("Bindings and speeds. Falls back to Resources/MouseOrbitCameraConfig, then to the SO's defaults.")]
        [SerializeField] MouseOrbitCameraConfigSO config;

        [Tooltip("How far in front of the camera the pivot starts when no owner has framed the scene.")]
        [Min(0.1f)]
        [SerializeField] float initialDistance = 300f;

        enum Drag
        {
            None = 0,
            Pan = 1,
            Orbit = 2,
            Zoom = 3,
        }

        Camera _camera;

        // The state the camera eases toward, and the state it is drawn at.
        Vector3 _pivot, _pivotTarget;
        float _yaw, _yawTarget, _pitch, _pitchTarget, _distance, _distanceTarget;

        Vector3 _homePivot;
        float _homeYaw, _homePitch, _homeDistance;
        bool _hasHome;

        Drag _drag;
        CursorLockMode _savedLock;
        bool _savedVisible;
        bool _cursorCaptured;

        /// <summary>The point the camera looks at (target, not the eased value).</summary>
        public Vector3 Pivot => _pivotTarget;

        /// <summary>The distance the camera is heading to (target, not the eased value).</summary>
        public float Distance => _distanceTarget;

        MouseOrbitCameraConfigSO Config
        {
            get
            {
                if (config == null)
                {
                    config = Resources.Load<MouseOrbitCameraConfigSO>(ConfigResourcePath);
                    if (config == null) config = ScriptableObject.CreateInstance<MouseOrbitCameraConfigSO>();
                }
                return config;
            }
        }

        void Awake()
        {
            _camera = GetComponent<Camera>();
            if (!Config.IsSane)
                CSDebug.LogWarning("[MouseOrbitCamera] MouseOrbitCameraConfig binds two gestures to one button; one of them is unreachable.", this);

            // Adopt whatever pose the camera was authored with: the pivot sits in front of it.
            var euler = transform.rotation.eulerAngles;
            _yaw = _yawTarget = euler.y;
            _pitch = _pitchTarget = Mathf.Clamp(Mathf.DeltaAngle(0f, euler.x), Config.MinPitch, Config.MaxPitch);
            _distance = _distanceTarget = Mathf.Clamp(initialDistance, Config.MinDistance, Config.MaxDistance);
            _pivot = _pivotTarget = transform.position + transform.forward * _distance;
            if (!_hasHome) SetHome(_pivot, _distance, _yaw, _pitch);
            Apply();
        }

        void OnDisable()
        {
            _drag = Drag.None;
            ReleaseCursor();
        }

        // ---------------- Owner API ----------------

        /// <summary>Record the view F / Home returns to. Does not move the camera.</summary>
        public void SetHome(Vector3 pivot, float distance, float yaw = 0f, float pitch = 0f)
        {
            _homePivot = pivot;
            _homeDistance = distance;
            _homeYaw = yaw;
            _homePitch = pitch;
            _hasHome = true;
        }

        /// <summary>Return to the home view, eased or (<paramref name="snap"/>) at once.</summary>
        public void FrameHome(bool snap)
        {
            var c = Config;
            _pivotTarget = _homePivot;
            _yawTarget = _homeYaw;
            _pitchTarget = Mathf.Clamp(_homePitch, c.MinPitch, c.MaxPitch);
            _distanceTarget = Mathf.Clamp(_homeDistance, c.MinDistance, c.MaxDistance);
            if (snap) SnapToTarget();
        }

        /// <summary>Change only the distance (the pivot and the angles are the player's).</summary>
        public void SetDistance(float distance, bool snap = false)
        {
            _distanceTarget = Mathf.Clamp(distance, Config.MinDistance, Config.MaxDistance);
            if (snap) _distance = _distanceTarget;
        }

        void SnapToTarget()
        {
            _pivot = _pivotTarget;
            _yaw = _yawTarget;
            _pitch = _pitchTarget;
            _distance = _distanceTarget;
            Apply();
        }

        // ---------------- The loop ----------------

        void LateUpdate()
        {
            var c = Config;
            float dt = Time.unscaledDeltaTime;
            var mouse = Mouse.current;
            if (mouse != null) HandleMouse(mouse, c);
            HandleKeyboard(Keyboard.current, c, dt);

            // Ease toward the target. Exponential, frame-rate independent; 0 = instant.
            float k = c.Smoothing > 0f ? 1f - Mathf.Exp(-c.Smoothing * dt) : 1f;
            _pivot = Vector3.Lerp(_pivot, _pivotTarget, k);
            _yaw = Mathf.LerpAngle(_yaw, _yawTarget, k);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, k);
            // Distance eases in log space, so a zoom from 10 to 10,000 is not 99% done in one frame.
            _distance = Mathf.Exp(Mathf.Lerp(Mathf.Log(_distance), Mathf.Log(_distanceTarget), k));
            Apply();

            float needFar = _distance + c.FarClipMargin;
            if (_camera != null && _camera.farClipPlane < needFar) _camera.farClipPlane = needFar;
        }

        void Apply()
        {
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.SetPositionAndRotation(PositionFor(_pivot, rot, _distance), rot);
        }

        void HandleMouse(Mouse mouse, MouseOrbitCameraConfigSO c)
        {
            var keyboard = Keyboard.current;
            bool alt = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
            bool fast = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            float speed = fast ? c.FastMultiplier : 1f;

            // Start a drag only from a press that is not on UI; end it when its button lets go.
            if (_drag == Drag.None && !PointerOverUI())
            {
                if (Button(mouse, c.PanButton).wasPressedThisFrame)
                    BeginDrag(alt && c.AltTurnsPanIntoOrbit ? Drag.Orbit : Drag.Pan, c);
                else if (Button(mouse, c.OrbitButton).wasPressedThisFrame)
                    BeginDrag(Drag.Orbit, c);
                else if (Button(mouse, c.ZoomDragButton).wasPressedThisFrame)
                    BeginDrag(Drag.Zoom, c);
            }
            else if (_drag != Drag.None && !DragButtonHeld(mouse, c))
            {
                _drag = Drag.None;
                ReleaseCursor();
            }

            Vector2 delta = mouse.delta.ReadValue();
            switch (_drag)
            {
                case Drag.Pan:
                {
                    float wpp = WorldPerPixel(_distanceTarget, _camera != null ? _camera.fieldOfView : 60f, Screen.height);
                    var rot = Quaternion.Euler(_pitchTarget, _yawTarget, 0f);
                    // Grab the world: it follows the cursor, so the pivot moves the opposite way.
                    _pivotTarget -= (rot * Vector3.right * delta.x + rot * Vector3.up * delta.y) * (wpp * c.PanSpeed);
                    break;
                }
                case Drag.Orbit:
                    _yawTarget += delta.x * c.OrbitDegreesPerPixel;
                    _pitchTarget = Mathf.Clamp(_pitchTarget - delta.y * c.OrbitDegreesPerPixel, c.MinPitch, c.MaxPitch);
                    break;
                case Drag.Zoom:
                    // Drag up = in. Proportional to the distance, like the wheel.
                    _distanceTarget = Mathf.Clamp(_distanceTarget * Mathf.Exp(-delta.y * c.DragZoomPerPixel),
                        c.MinDistance, c.MaxDistance);
                    break;
            }

            if (_drag == Drag.None && !PointerOverUI())
            {
                float notches = WheelNotches(mouse.scroll.ReadValue().y);
                if (notches != 0f) WheelZoom(notches * speed, mouse.position.ReadValue(), c);
            }
        }

        void WheelZoom(float notches, Vector2 cursor, MouseOrbitCameraConfigSO c)
        {
            float oldDistance = _distanceTarget;
            float newDistance = Mathf.Clamp(oldDistance * Mathf.Pow(1f - c.WheelZoomStep, notches), c.MinDistance, c.MaxDistance);
            if (Mathf.Approximately(newDistance, oldDistance)) return;

            if (c.ZoomTowardCursor && _camera != null && TryCursorPointAtPivotDepth(cursor, out var point))
                _pivotTarget = ZoomPivotTowardPoint(_pivotTarget, point, oldDistance, newDistance);
            _distanceTarget = newDistance;
        }

        /// <summary>
        /// The point under the cursor on the plane through the (target) pivot facing the camera —
        /// the depth at which a pan is exact and a zoom toward it holds it fixed.
        /// </summary>
        bool TryCursorPointAtPivotDepth(Vector2 cursor, out Vector3 point)
        {
            // Cast from the TARGET pose (where the camera is heading), not the eased transform, so
            // successive notches during an ease compose exactly.
            var rot = Quaternion.Euler(_pitchTarget, _yawTarget, 0f);
            float u = cursor.x / Mathf.Max(1f, Screen.width);
            float v = cursor.y / Mathf.Max(1f, Screen.height);
            point = PointAtPivotDepth(_pivotTarget, rot, _distanceTarget, _camera.fieldOfView, _camera.aspect, u, v);
            return true;
        }

        void HandleKeyboard(Keyboard keyboard, MouseOrbitCameraConfigSO c, float dt)
        {
            if (keyboard == null || TextFieldHasFocus()) return;
            float speed = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? c.FastMultiplier : 1f;

            if (keyboard.fKey.wasPressedThisFrame || keyboard.homeKey.wasPressedThisFrame)
            {
                FrameHome(snap: false);
                return;
            }

            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            if (x != 0f || y != 0f)
            {
                var rot = Quaternion.Euler(_pitchTarget, _yawTarget, 0f);
                // W/S move across the view's own up, so "up" on screen is up whatever the pitch.
                _pivotTarget += (rot * Vector3.right * x + rot * Vector3.up * y) * (_distanceTarget * c.KeyboardPanSpeed * speed * dt);
            }

            float turn = (keyboard.eKey.isPressed ? 1f : 0f) - (keyboard.qKey.isPressed ? 1f : 0f);
            if (turn != 0f) _yawTarget += turn * c.KeyboardOrbitDegreesPerSecond * speed * dt;
        }

        // ---------------- Drag bookkeeping ----------------

        void BeginDrag(Drag drag, MouseOrbitCameraConfigSO c)
        {
            _drag = drag;
            if (drag == Drag.Orbit && c.LockCursorWhileOrbiting) CaptureCursor();
        }

        bool DragButtonHeld(Mouse mouse, MouseOrbitCameraConfigSO c)
        {
            switch (_drag)
            {
                // An Alt-orbit started on the pan button is held by the pan button.
                case Drag.Pan: return Button(mouse, c.PanButton).isPressed;
                case Drag.Orbit: return Button(mouse, c.OrbitButton).isPressed || Button(mouse, c.PanButton).isPressed;
                case Drag.Zoom: return Button(mouse, c.ZoomDragButton).isPressed;
                default: return false;
            }
        }

        void CaptureCursor()
        {
            if (_cursorCaptured) return;
            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorCaptured = true;
        }

        void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            _cursorCaptured = false;
        }

        static UnityEngine.InputSystem.Controls.ButtonControl Button(Mouse mouse, MouseOrbitCameraConfigSO.MouseButtonBinding b)
        {
            switch (b)
            {
                case MouseOrbitCameraConfigSO.MouseButtonBinding.Left: return mouse.leftButton;
                case MouseOrbitCameraConfigSO.MouseButtonBinding.Middle: return mouse.middleButton;
                default: return mouse.rightButton;
            }
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        static bool TextFieldHasFocus()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            return selected.TryGetComponent<UnityEngine.UI.InputField>(out _) ||
                   selected.TryGetComponent<TMPro.TMP_InputField>(out _);
        }

        // ---------------- Pure math (tested in MouseOrbitCameraTests) ----------------

        /// <summary>Camera position for a pivot, an orientation and a distance.</summary>
        public static Vector3 PositionFor(Vector3 pivot, Quaternion rotation, float distance) =>
            pivot - rotation * Vector3.forward * distance;

        /// <summary>
        /// World units one screen pixel spans at <paramref name="distance"/> in front of a perspective
        /// camera with vertical <paramref name="fovDegrees"/> over <paramref name="screenHeight"/>
        /// pixels — what makes a pan move the world exactly with the cursor at the pivot's depth.
        /// </summary>
        public static float WorldPerPixel(float distance, float fovDegrees, float screenHeight) =>
            2f * distance * Mathf.Tan(0.5f * fovDegrees * Mathf.Deg2Rad) / Mathf.Max(1f, screenHeight);

        /// <summary>
        /// The pivot after a zoom from <paramref name="oldDistance"/> to <paramref name="newDistance"/>
        /// that keeps <paramref name="point"/> (on the plane through the pivot facing the camera)
        /// fixed on screen: <c>pivot' = pivot + (point − pivot)(1 − new/old)</c>. Exact — the
        /// camera-to-point vector is scaled by <c>new/old</c>, so its direction, and so its pixel,
        /// is unchanged.
        /// </summary>
        public static Vector3 ZoomPivotTowardPoint(Vector3 pivot, Vector3 point, float oldDistance, float newDistance)
        {
            if (!(oldDistance > 0f)) return pivot;
            return pivot + (point - pivot) * (1f - newDistance / oldDistance);
        }

        /// <summary>
        /// The point under viewport coordinate (<paramref name="u"/>, <paramref name="v"/>) (0..1,
        /// origin bottom-left) on the plane through <paramref name="pivot"/> facing a perspective
        /// camera at <see cref="PositionFor"/>. That plane sits at exactly <paramref name="distance"/>
        /// along the view axis, so the point is the eye plus the viewport ray scaled to unit depth
        /// times the distance — no raycast, no division by a grazing angle.
        /// </summary>
        public static Vector3 PointAtPivotDepth(Vector3 pivot, Quaternion rotation, float distance,
            float fovDegrees, float aspect, float u, float v)
        {
            float tanHalf = Mathf.Tan(0.5f * fovDegrees * Mathf.Deg2Rad);
            var local = new Vector3((2f * u - 1f) * tanHalf * aspect, (2f * v - 1f) * tanHalf, 1f);
            return PositionFor(pivot, rotation, distance) + rotation * local * distance;
        }

        /// <summary>
        /// A wheel reading in NOTCHES. The Input System reports ±120 per notch on some platforms and
        /// settings and ±1 on others; anything at or above 10 is taken as the 120 scale.
        /// </summary>
        public static float WheelNotches(float raw)
        {
            if (raw == 0f) return 0f;
            return Mathf.Abs(raw) >= 10f ? raw / 120f : raw;
        }
    }
}
