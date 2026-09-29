using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Termite queen's COMMANDER input and view — the local pilot's half of her flight model
    /// (<see cref="TermiteCommandTransformer"/> is the half every machine runs). Design record:
    /// <c>R_VesselActions/TERMITE.md</c> §4.
    ///
    /// <para><b>Point, and she goes. Drag, and the view turns.</b></para>
    /// <list type="table">
    /// <item><term>Mouse</term><description>Left CLICK a place → she flies there. Left or right
    /// DRAG → orbit the perspective. Scroll → zoom.</description></item>
    /// <item><term>Touch</term><description>TAP → go there. One-finger DRAG → orbit. Pinch →
    /// zoom.</description></item>
    /// <item><term>Pad</term><description>Left stick → steer the command point relative to the
    /// view. Right stick → orbit. D-pad up/down → zoom.</description></item>
    /// <item><term>Keyboard</term><description>WASD → steer the command point. Arrow keys →
    /// orbit. <c>=</c>/<c>-</c> → zoom.</description></item>
    /// </list>
    /// <para>A click and a drag share the left mouse button, told apart by DISTANCE: a press that
    /// travels less than <see cref="dragThresholdPixels"/> before release is a click. That is what
    /// the brief asked for ("click and drag to change the perspective") and it keeps the right
    /// button free for pilots who prefer to orbit on it.</para>
    ///
    /// <para><b>Polled directly, like <see cref="RearViewGesture"/>, rather than routed through
    /// <c>InputEvents</c>.</b> Pointing and orbiting are a COMMANDER's view controls, not vessel
    /// abilities: putting them in the ability map would make them something the map could fail to
    /// author, and would occupy the four slots the deck needs. The ability cards still arrive
    /// through the ordinary replicated press path.</para>
    ///
    /// <para><b>Local human pilot only, and it cleans up after itself.</b> Engagement is
    /// edge-detected every frame from <c>Player.IsLocalPilot</c> and autopilot, so a pilot swap, a
    /// vessel swap, an AI takeover or a return to the menu's lava lamp all release the flight back
    /// to the stick and hand the camera back its ordinary frame — the camera's
    /// <see cref="CustomCameraController.CommanderFrame"/> is only ever cleared by the commander
    /// that set it (identity-guarded, the platform laws' rule).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TermiteCommander : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] TermiteCommandTransformer transformer;

        [Header("Pointing")]
        [Tooltip("A left press that travels less than this many pixels before release is a CLICK " +
                 "(a command); further is a DRAG (an orbit).")]
        [SerializeField, Min(1f)] float dragThresholdPixels = 10f;
        [Tooltip("How far ahead of the queen the pad / keyboard steering puts the command point, " +
                 "world units. Far enough that she reaches cruise before she arrives.")]
        [SerializeField, Min(5f)] float steerLead = 140f;

        [Header("Orbit")]
        [SerializeField] float orbitDegreesPerPixel = 0.25f;
        [SerializeField] float orbitStickDegreesPerSecond = 120f;
        [Tooltip("Lowest and highest the view may pitch, degrees. Negative looks up from below.")]
        [SerializeField] Vector2 pitchLimits = new(-55f, 70f);

        [Header("Zoom")]
        [SerializeField] Vector2 zoomLimits = new(0.45f, 2.6f);
        [SerializeField] float zoomPerScrollNotch = 0.1f;
        [SerializeField] float zoomPerSecond = 1.2f;

        [Header("Command marker")]
        [Tooltip("World radius of the ring drawn at the command point.")]
        [SerializeField, Min(0.5f)] float markerRadius = 6f;
        [SerializeField, Min(0.05f)] float markerWidth = 0.6f;
        [SerializeField] Color markerColor = new(1f, 1f, 1f, 0.75f);

        IVesselStatus _status;
        bool _engaged;
        CustomCameraController _camera;
        float _yaw, _pitch, _zoom = 1f;

        bool _mouseDown;
        bool _mouseDragging;
        Vector2 _mouseDownAt;
        bool _touchDragging;
        Vector2 _touchDownAt;
        float _pinchStart;
        float _zoomAtPinchStart;
        bool _steering;

        LineRenderer _marker;
        float _markerAlpha;
        static Material _markerMaterial;
        const int MarkerSegments = 40;

        /// <summary>The last point the pilot commanded, in world space — the "node of your
        /// choosing" the Teleport card resolves against. Null until the pilot has pointed.</summary>
        public Vector3? LastCommandPoint { get; private set; }

        /// <summary>True while this commander owns the queen's flight and the camera frame.</summary>
        public bool IsEngaged => _engaged;

        void Awake()
        {
            _status = GetComponent<IVesselStatus>();
            if (!transformer) transformer = GetComponent<TermiteCommandTransformer>();
        }

        void OnDisable()
        {
            Release();
            if (_marker) _marker.enabled = false;
        }

        void OnDestroy()
        {
            Release();
            // The marker lives outside the vessel's hierarchy (it marks a PLACE), so it is not
            // taken down with the vessel — this is its one owner.
            if (_marker) Destroy(_marker.gameObject);
            _marker = null;
        }

        void Update()
        {
            bool wantEngaged = IsLocalHumanPilot();
            if (wantEngaged != _engaged)
            {
                if (wantEngaged) Engage();
                else Release();
            }
            if (!_engaged) { FadeMarker(); return; }

            var cam = ResolveCamera();
            if (cam == null) { FadeMarker(); return; }
            _camera = cam;

            if (!InputPaused())
            {
                HandleMouse(cam.Camera);
                HandleTouch(cam.Camera);
                HandlePadAndKeyboard(cam.Camera);
            }

            _pitch = Mathf.Clamp(_pitch, pitchLimits.x, pitchLimits.y);
            _zoom = Mathf.Clamp(_zoom, zoomLimits.x, zoomLimits.y);
            cam.CommanderFrame = Quaternion.Euler(_pitch, _yaw, 0f);
            cam.CommanderZoom = _zoom;

            UpdateMarker(cam.Camera);
        }

        // ------------------------------------------------------------------ engagement

        bool IsLocalHumanPilot()
        {
            if (_status == null || !transformer) return false;
            var player = _status.Player;
            if (player == null || !player.IsLocalPilot) return false;
            if (_status.AutoPilotEnabled) return false;
            return true;
        }

        void Engage()
        {
            _engaged = true;
            transformer.SetCommanderEnabled(true);
            // Start the view where the chase camera already is: behind her, level. The first
            // frame of a commander must not cut the picture.
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;
            _zoom = 1f;
            LastCommandPoint = null;
        }

        void Release()
        {
            if (!_engaged) return;
            _engaged = false;
            if (transformer) transformer.SetCommanderEnabled(false);
            if (_camera != null && FollowsThisVessel(_camera))
            {
                _camera.CommanderFrame = null;
                _camera.CommanderZoom = 1f;
            }
            _camera = null;
            _mouseDown = _mouseDragging = _touchDragging = _steering = false;
            LastCommandPoint = null;
        }

        bool InputPaused()
        {
            if (PauseSystem.Paused) return true;
            return _status.InputStatus != null && _status.InputStatus.Paused;
        }

        CustomCameraController ResolveCamera()
        {
            var manager = CameraManager.Instance;
            if (manager == null) return null;
            var active = manager.GetActiveController() as CustomCameraController;
            if (active == null) return null;   // the menu rig — expected
            return FollowsThisVessel(active) ? active : null;
        }

        bool FollowsThisVessel(CustomCameraController cam)
        {
            var target = cam ? cam.FollowTarget : null;
            return target && (target == transform || target.IsChildOf(transform));
        }

        // ------------------------------------------------------------------ mouse

        void HandleMouse(Camera cam)
        {
            var mouse = Mouse.current;
            if (mouse == null || cam == null) return;

            Vector2 pos = mouse.position.ReadValue();

            // Scroll zoom (a notch reads as 120 on most platforms; normalise to notches).
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi())
                _zoom *= 1f - Mathf.Sign(scroll) * zoomPerScrollNotch;

            // Right drag always orbits.
            if (mouse.rightButton.isPressed)
                Orbit(mouse.delta.ReadValue());

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // A press that starts over UI (an ability card, a menu) belongs to the UI.
                _mouseDown = !PointerOverUi();
                _mouseDragging = false;
                _mouseDownAt = pos;
            }

            if (_mouseDown && mouse.leftButton.isPressed)
            {
                if (!_mouseDragging && (pos - _mouseDownAt).sqrMagnitude > dragThresholdPixels * dragThresholdPixels)
                    _mouseDragging = true;
                if (_mouseDragging) Orbit(mouse.delta.ReadValue());
            }

            if (_mouseDown && mouse.leftButton.wasReleasedThisFrame)
            {
                if (!_mouseDragging) CommandAt(cam, pos);
                _mouseDown = false;
                _mouseDragging = false;
            }
        }

        // ------------------------------------------------------------------ touch

        void HandleTouch(Camera cam)
        {
            var screen = Touchscreen.current;
            if (screen == null || cam == null) return;

            var touches = screen.touches;
            int active = 0;
            for (int i = 0; i < touches.Count && active < 2; i++)
                if (touches[i].isInProgress) active++;

            if (active >= 2)
            {
                // Pinch: the distance between the first two live touches.
                Vector2 a = default, b = default;
                int found = 0;
                for (int i = 0; i < touches.Count && found < 2; i++)
                {
                    if (!touches[i].isInProgress) continue;
                    if (found == 0) a = touches[i].position.ReadValue();
                    else b = touches[i].position.ReadValue();
                    found++;
                }
                float d = Vector2.Distance(a, b);
                if (_pinchStart <= 0f) { _pinchStart = d; _zoomAtPinchStart = _zoom; }
                else if (d > 1f) _zoom = _zoomAtPinchStart * (_pinchStart / d);
                _touchDragging = true;   // a pinch never ends in a tap
                return;
            }
            _pinchStart = 0f;

            var primary = screen.primaryTouch;
            Vector2 pos = primary.position.ReadValue();
            if (primary.press.wasPressedThisFrame)
            {
                _touchDownAt = pos;
                _touchDragging = PointerOverUi();   // a press on UI is never a command
            }
            if (primary.press.isPressed)
            {
                if (!_touchDragging && (pos - _touchDownAt).sqrMagnitude > dragThresholdPixels * dragThresholdPixels * 4f)
                    _touchDragging = true;
                if (_touchDragging) Orbit(primary.delta.ReadValue());
            }
            if (primary.press.wasReleasedThisFrame)
            {
                if (!_touchDragging) CommandAt(cam, pos);
                _touchDragging = false;
            }
        }

        // ------------------------------------------------------------------ pad + keyboard

        void HandlePadAndKeyboard(Camera cam)
        {
            Vector2 steer = Vector2.zero;
            Vector2 orbit = Vector2.zero;
            float zoom = 0f;

            var pad = Gamepad.current;
            if (pad != null)
            {
                steer += pad.leftStick.ReadValue();
                orbit += pad.rightStick.ReadValue();
                if (pad.dpad.up.isPressed) zoom -= 1f;
                if (pad.dpad.down.isPressed) zoom += 1f;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                steer += new Vector2((kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                                     (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
                orbit += new Vector2((kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f),
                                     (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f));
                if (kb.equalsKey.isPressed) zoom -= 1f;
                if (kb.minusKey.isPressed) zoom += 1f;
            }

            if (orbit.sqrMagnitude > 0.04f)
            {
                _yaw += orbit.x * orbitStickDegreesPerSecond * Time.deltaTime;
                _pitch -= orbit.y * orbitStickDegreesPerSecond * Time.deltaTime;
            }
            if (Mathf.Abs(zoom) > 0f) _zoom *= 1f + zoom * zoomPerSecond * Time.deltaTime;

            // Steering: the point rides out ahead of her in the direction pushed, relative to the
            // VIEW, so "up" is always into the picture whichever way the pilot has orbited.
            if (steer.sqrMagnitude > 0.04f && cam != null)
            {
                steer = Vector2.ClampMagnitude(steer, 1f);
                Vector3 dir = cam.transform.right * steer.x + cam.transform.forward * steer.y;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    Command(transform.position + dir.normalized * steerLead * steer.magnitude);
                    _steering = true;
                }
            }
            else if (_steering)
            {
                // Let go of the stick: she stops where she is rather than finishing a lead point
                // the pilot never chose.
                _steering = false;
                transformer.ClearCommandTarget();
            }
        }

        // ------------------------------------------------------------------ commands

        void Orbit(Vector2 pixelDelta)
        {
            _yaw += pixelDelta.x * orbitDegreesPerPixel;
            _pitch -= pixelDelta.y * orbitDegreesPerPixel;
        }

        /// <summary>
        /// Resolve a screen point to a world point on the plane through the queen facing the
        /// camera — "that place, at her depth". A click therefore always commands a reachable point
        /// in front of the lens, and orbiting the view is how a pilot reaches any direction in 3D.
        /// </summary>
        void CommandAt(Camera cam, Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(-cam.transform.forward, transform.position);
            if (!plane.Raycast(ray, out float distance)) return;
            Command(ray.GetPoint(distance));
        }

        void Command(Vector3 worldPoint)
        {
            transformer.SetCommandTarget(worldPoint);
            LastCommandPoint = transformer.CommandTarget;
            _markerAlpha = 1f;
        }

        static bool PointerOverUi()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------------ marker

        /// <summary>
        /// A ring at the command point, facing the camera, in the queen's domain colour where it
        /// resolves. It FADES rather than blinking out (continuity of existence applies to UI):
        /// it holds while she is travelling and fades once she has arrived.
        /// </summary>
        void UpdateMarker(Camera cam)
        {
            var target = transformer.CommandTarget;
            if (!target.HasValue || cam == null) { FadeMarker(); return; }

            EnsureMarker();
            if (!_marker) return;

            float goal = transformer.HasArrived ? 0f : 1f;
            _markerAlpha = Mathf.MoveTowards(_markerAlpha, goal, Time.deltaTime * (goal > 0f ? 4f : 1.2f));

            Vector3 centre = target.Value;
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            for (int i = 0; i < MarkerSegments; i++)
            {
                float a = i / (float)MarkerSegments * Mathf.PI * 2f;
                _marker.SetPosition(i, centre + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * markerRadius * pulse);
            }
            ApplyMarkerColor();
        }

        void FadeMarker()
        {
            if (!_marker) return;
            _markerAlpha = Mathf.MoveTowards(_markerAlpha, 0f, Time.deltaTime * 2f);
            ApplyMarkerColor();
        }

        void ApplyMarkerColor()
        {
            var c = markerColor;
            c.a *= _markerAlpha;
            _marker.startColor = c;
            _marker.endColor = c;
            _marker.enabled = _markerAlpha > 0.001f;
        }

        void EnsureMarker()
        {
            if (_marker) return;
            var material = MarkerMaterial();
            if (!material) return;
            var go = new GameObject("TermiteCommandMarker");
            // NOT parented to the queen: the point is a place in the world, and a child would ride
            // along with her while she flies toward it.
            _marker = go.AddComponent<LineRenderer>();
            _marker.useWorldSpace = true;
            _marker.loop = true;
            _marker.positionCount = MarkerSegments;
            _marker.widthMultiplier = markerWidth;
            _marker.sharedMaterial = material;
            _marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _marker.receiveShadows = false;
            _marker.enabled = false;
        }

        /// <summary>One shared material for every commander — a <c>new Material</c> per marker would
        /// leak one per spawn. The route <c>SniperBeam</c> takes.</summary>
        static Material MarkerMaterial()
        {
            if (_markerMaterial) return _markerMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            _markerMaterial = new Material(shader) { name = "TermiteCommandMarker", renderQueue = 3000 };
            return _markerMaterial;
        }
    }
}
