using System;
using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One view on the screen at a time: the lowest graphics preset's rule for every crew vessel (Garrett, 2026-10-22: "if
    /// the lowest graphics setting are selected don't display pips. start the player in the wrasse and the ratel. in this
    /// setting very little of the game should even need to be processed, so the ratel should run fine on very old
    /// phones"). It is the preset row's <c>singleView</c> cut (<see cref="GraphicsCuts.SingleView"/>, on for Very Low).
    /// While it holds:
    /// <list type="bullet">
    /// <item>no picture-in-picture is drawn: the seat camera of a seat nobody sits in is OFF (not a small square), and the
    /// pilot's camera is never lifted into one;</item>
    /// <item>the pilot's camera, hidden under a full-screen seat view, is PARKED (<see cref="PilotCameraPark"/>): it draws
    /// nothing, with no post-processing and no shadows, so it costs a clear;</item>
    /// <item>the person starts in the crew seat whose view is the cheapest: the Wrasse (its one top-down camera) and the
    /// Ratel (a UI board, with the world not drawn at all).</item>
    /// </list>
    /// A swap still works from the keys and the D-pad, and on the Honeyguide from its Fly and Ratel buttons; a double tap
    /// has no small view to land on.
    /// </summary>
    public static class CrewSingleView
    {
        /// <summary>True while the preset in force draws one view at a time.</summary>
        public static bool On => GraphicsCuts.SingleView;
    }

    /// <summary>
    /// The pilot's camera put to sleep under a full-screen seat view (<see cref="CrewSingleView"/>): no layer drawn, a plain
    /// clear, no post-processing and no shadows, and everything put back on <see cref="Release"/>. The camera stays enabled,
    /// so <c>Camera.main</c> and everything that reads the game camera keep working. A seat that copies the pilot's look
    /// into its own camera reads it through <see cref="CullingMask"/>, <see cref="ClearFlags"/> and
    /// <see cref="Background"/>, which give the pilot's own values while it is parked.
    /// </summary>
    public sealed class PilotCameraPark
    {
        Camera _cam;
        UniversalAdditionalCameraData _data;
        int _mask;
        CameraClearFlags _clear;
        Color _background;
        bool _post, _shadows;

        /// <summary>True while a camera is parked.</summary>
        public bool Parked => _cam;

        /// <summary>Park <paramref name="pilot"/>, cleared to <paramref name="backdrop"/>. Parking another camera releases the
        /// first; parking the same one again does nothing.</summary>
        public void Park(Camera pilot, Color backdrop)
        {
            if (!pilot || pilot == _cam) return;
            Release();
            _cam = pilot;
            _mask = pilot.cullingMask;
            _clear = pilot.clearFlags;
            _background = pilot.backgroundColor;
            pilot.cullingMask = 0;
            pilot.clearFlags = CameraClearFlags.SolidColor;
            pilot.backgroundColor = backdrop;
            if (pilot.TryGetComponent(out _data))
            {
                _post = _data.renderPostProcessing;
                _shadows = _data.renderShadows;
                _data.renderPostProcessing = false;
                _data.renderShadows = false;
            }
        }

        /// <summary>Give the parked camera back everything it drew with. A destroyed camera is forgotten.</summary>
        public void Release()
        {
            if (_cam)
            {
                _cam.cullingMask = _mask;
                _cam.clearFlags = _clear;
                _cam.backgroundColor = _background;
                if (_data)
                {
                    _data.renderPostProcessing = _post;
                    _data.renderShadows = _shadows;
                }
            }
            _cam = null;
            _data = null;
        }

        /// <summary>The layers <paramref name="pilot"/> draws when awake.</summary>
        public int CullingMask(Camera pilot) => pilot == _cam ? _mask : pilot.cullingMask;

        /// <summary>How <paramref name="pilot"/> clears when awake.</summary>
        public CameraClearFlags ClearFlags(Camera pilot) => pilot == _cam ? _clear : pilot.clearFlags;

        /// <summary>The colour <paramref name="pilot"/> clears to when awake.</summary>
        public Color Background(Camera pilot) => pilot == _cam ? _background : pilot.backgroundColor;

        /// <summary>Whether <paramref name="pilot"/> post-processes when awake.</summary>
        public bool PostProcessing(Camera pilot, UniversalAdditionalCameraData data) =>
            pilot == _cam && _data ? _post : data.renderPostProcessing;

        /// <summary>Whether <paramref name="pilot"/> draws shadows when awake.</summary>
        public bool Shadows(Camera pilot, UniversalAdditionalCameraData data) =>
            pilot == _cam && _data ? _shadows : data.renderShadows;
    }

    /// <summary>
    /// The Wrasse is played in PORTRAIT on a phone (Garrett, 2026-10-22: "assume that the wrasse is played in portrait mode
    /// on mobile"): the one-thumb seat turns the screen upright while a human holds it, and the hull's two-thumb seat turns
    /// it back to the landscape the rest of the game runs in. A phone only: a desktop window and a browser page keep their
    /// shape. The project allows portrait so iOS accepts the request; <see cref="NoPortraitAtBoot"/> keeps every other
    /// screen in landscape, as before.
    /// </summary>
    public static class SeatOrientation
    {
        static bool Phone => Application.platform is RuntimePlatform.Android or RuntimePlatform.IPhonePlayer;

        /// <summary>The project allows portrait only so a seat can ask for it: before the first scene loads, the screen's own
        /// auto-rotation is kept to the two landscapes it always had (<c>DeviceOrientationHandler</c> then locks
        /// landscape-left when the input starts).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void NoPortraitAtBoot()
        {
            if (!Phone) return;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
        }

        /// <summary>Turn a phone's screen upright (<paramref name="portrait"/>) or back to landscape-left.</summary>
        public static void Portrait(bool portrait)
        {
            if (!Phone) return;
            var want = portrait ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;
            if (Screen.orientation != want) Screen.orientation = want;
        }
    }

    /// <summary>
    /// The two views of a crew vessel, laid out one way for every seat (the Wrasse's round 4, Garrett 2026-10-18: "the
    /// kite view when flying the pilot was good; use that"). While the human flies the hull, the seat's camera is a small
    /// square in the lower left over the pilot's view. While the human flies the seat, the seat's camera fills the screen
    /// and the pilot's camera (<see cref="CameraManager.ViewCamera"/>) is lifted into the same small square. The pilot
    /// camera's rect and depth are restored the moment the human flies the hull again, the camera changes, or the seat
    /// lets go (<see cref="Restore"/>). The seat camera draws what the pilot camera draws (its culling mask, clear flags
    /// and background), so it is the game's renderer, not a picture of it. The small square wears the style guide's
    /// frame (<see cref="PipViewFrame"/>). On the lowest preset there is no small square (<see cref="CrewSingleView"/>).
    /// </summary>
    public sealed class CrewPictureInPicture
    {
        Camera _pilot;
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;
        readonly PipViewFrame _frame = new();
        readonly PilotCameraPark _park = new();

        /// <summary>The pilot's camera the layout last saw.</summary>
        public Camera Pilot => _pilot;

        /// <summary>
        /// Lay out <paramref name="seat"/> and the pilot's camera for the seat the human is in.
        /// <paramref name="pipSize"/> is the small view's side as a share of the screen height, <paramref name="margin"/> its
        /// gap from the screen's edges (the Kite's pipSize 0.32 and viewMargin 0.015).
        /// </summary>
        public void Layout(Camera seat, bool humanInSeat, float pipSize, float margin)
        {
            if (!seat) return;
            var pilot = CameraManager.ViewCamera;
            if (pilot != _pilot) { RestorePilot(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            if (_pilot)
            {
                seat.cullingMask = _park.CullingMask(_pilot);
                seat.clearFlags = _park.ClearFlags(_pilot);
                seat.backgroundColor = _park.Background(_pilot);
            }

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            var square = new Rect(margin / aspect, margin, pipSize / aspect, pipSize);   // a square in the lower left
            bool single = CrewSingleView.On;
            seat.enabled = humanInSeat || !single;   // one view: a seat nobody sits in draws nothing
            if (humanInSeat)
            {
                seat.rect = new Rect(0f, 0f, 1f, 1f);
                seat.depth = baseDepth + 1f;
                if (single) { RestorePilot(); _park.Park(_pilot, Color.black); }
                else if (_pilot)
                {
                    _park.Release();
                    _pilot.rect = square;   // the pilot's view in the seat view's own small square
                    _pilot.depth = baseDepth + 2f;
                    _pilotLifted = true;
                }
            }
            else
            {
                RestorePilot();
                _park.Release();
                seat.rect = square;
                seat.depth = baseDepth + 1f;
            }
            _frame.Show(SmallView(seat, humanInSeat));
        }

        /// <summary>Give the pilot's camera back its own rect and depth (and wake it), and take the small square's frame
        /// down.</summary>
        public void Restore()
        {
            RestorePilot();
            _park.Release();
            _frame.Hide();
        }

        /// <summary><see cref="Restore"/>, and destroy the frame (a later <see cref="Layout"/> builds a new one).</summary>
        public void Dispose()
        {
            Restore();
            _frame.Dispose();
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

        /// <summary>The camera drawn in the small square for the seat the human is in, or null when none is.</summary>
        public Camera SmallView(Camera seat, bool humanInSeat)
        {
            var small = humanInSeat ? (_pilotLifted ? _pilot : null) : seat;
            return small && small.enabled ? small : null;
        }

        /// <summary>True when <paramref name="screen"/> (pixels) falls inside the small square.</summary>
        public bool SmallViewContains(Camera seat, bool humanInSeat, Vector2 screen)
        {
            var small = SmallView(seat, humanInSeat);
            return small && small.pixelRect.Contains(screen);
        }
    }

    /// <summary>
    /// A double tap on a crew vessel's small view: the touch swap (Garrett, 2026-10-19: "when I double tap the pip window to
    /// the other vessel it should switch"). Two SHORT taps, the second starting soon after the first and near it, both
    /// inside the small view. Short is what keeps a flying thumb out: the left thumb lands in that corner, and re-planting
    /// it quickly is a drift, not a swap - but a planted thumb stays down, so it never counts as a tap. The numbers are the
    /// Wrasse's (<c>KiteNavigator.PipDoubleTapped</c>). Poll it every frame: it tracks presses and releases across frames.
    /// </summary>
    public sealed class CrewSmallViewDoubleTap
    {
        const float TapMaxSeconds = 0.3f, DoubleTapWindowSeconds = 0.45f, DoubleTapMaxPixels = 120f;
        int _pressId = int.MinValue;
        float _pressAt, _lastTapAt = -99f;
        Vector2 _pressPos, _lastTapPos;

        /// <summary>True on the frame the second tap of a double tap on <paramref name="small"/> lifts. Null = no small view.</summary>
        public bool Poll(Camera small)
        {
            bool doubled = false;

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.press.wasPressedThisFrame) Press(small, t.touchId.ReadValue(), t.position.ReadValue());
                    if (t.press.wasReleasedThisFrame) doubled |= Release(t.touchId.ReadValue());
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) Press(small, -1, mouse.position.ReadValue());
                if (mouse.leftButton.wasReleasedThisFrame) doubled |= Release(-1);
            }
            return doubled;
        }

        void Press(Camera small, int id, Vector2 at)
        {
            if (!small || !small.pixelRect.Contains(at)) return;
            _pressId = id;
            _pressAt = Time.unscaledTime;
            _pressPos = at;
        }

        bool Release(int id)
        {
            if (id != _pressId) return false;
            _pressId = int.MinValue;
            float now = Time.unscaledTime;
            if (now - _pressAt > TapMaxSeconds) return false;

            bool doubled = _pressAt - _lastTapAt <= DoubleTapWindowSeconds
                           && (_pressPos - _lastTapPos).sqrMagnitude <= DoubleTapMaxPixels * DoubleTapMaxPixels;
            _lastTapAt = doubled ? -99f : now;
            _lastTapPos = _pressPos;
            return doubled;
        }
    }

    /// <summary>
    /// How a seat camera sees the game's prisms, for that camera's render only (the Wrasse's round 4d, Garrett
    /// 2026-10-18: "use the normal prism depth shader instead but use distance from the plane for depth ... use the dither
    /// effect in a cone to preserve the viewing area visibility"). Two global looks are re-pointed in
    /// <c>beginCameraRendering</c> and restored in <c>endCameraRendering</c>, so every other camera renders exactly as before:
    /// <list type="bullet">
    /// <item>the prisms' own depth look (their faces part with distance, <see cref="PrismDepthPlane"/>) measures distance OUT
    /// OF THE SEAT'S PLANE, so what the seat can touch reads crisp;</item>
    /// <item>the occlusion corridor (the screen-door dither the game uses so its own camera sees its vessel,
    /// <see cref="PrismOcclusionCorridor.BeginViewOverride"/>) is re-aimed for the seat's view, so mass in front of what the
    /// seat must see dithers instead of hiding it.</item>
    /// </list>
    /// </summary>
    public sealed class CrewViewRender : IDisposable
    {
        /// <summary>One render's look, supplied by the seat each time its camera renders.</summary>
        public struct Look
        {
            public Vector3 PlanePoint, PlaneNormal;
            public float FullSpreadAt;
            /// <summary>The bound hull (the corridor only lets its own vessel re-shape it); null = no corridor.</summary>
            public Transform Hull;
            public Vector3 CorridorTarget;
            public float OuterRadius, InnerRadius, CoreAlpha, NearRadius;
        }

        readonly Camera _cam;
        readonly Func<Look> _look;
        bool _plane, _corridor;

        public CrewViewRender(Camera cam, Func<Look> look)
        {
            _cam = cam;
            _look = look;
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        void Begin(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam || !_cam || !_cam.enabled) return;
            var look = _look();
            PrismDepthPlane.Begin(look.PlanePoint, look.PlaneNormal, look.FullSpreadAt);
            _plane = true;
            if (look.Hull)
                _corridor = PrismOcclusionCorridor.BeginViewOverride(look.Hull, look.CorridorTarget, look.OuterRadius,
                                                                     look.InnerRadius, look.CoreAlpha, look.NearRadius);
        }

        void End(ScriptableRenderContext _, Camera cam)
        {
            if (cam != _cam) return;
            if (_plane) PrismDepthPlane.End();
            if (_corridor) PrismOcclusionCorridor.EndViewOverride();
            _plane = _corridor = false;
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.endCameraRendering -= End;
            End(default, _cam);
        }
    }
}
