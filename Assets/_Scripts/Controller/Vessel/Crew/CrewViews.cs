using System;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The two views of a crew vessel, laid out one way for every seat (the Wrasse's round 4, Garrett 2026-10-18: "the
    /// kite view when flying the pilot was good; use that"). While the human flies the hull, the seat's camera is a small
    /// square in the lower left over the pilot's view. While the human flies the seat, the seat's camera fills the screen
    /// and the pilot's camera (<see cref="CameraManager.ViewCamera"/>) is lifted into the same small square. The pilot
    /// camera's rect and depth are restored the moment the human flies the hull again, the camera changes, or the seat
    /// lets go (<see cref="Restore"/>). The seat camera draws what the pilot camera draws (its culling mask, clear flags
    /// and background), so it is the game's renderer, not a picture of it.
    /// </summary>
    public sealed class CrewPictureInPicture
    {
        Camera _pilot;
        Rect _pilotRect;
        float _pilotDepth;
        bool _pilotLifted;

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
            if (pilot != _pilot) { Restore(); _pilot = pilot; if (_pilot) { _pilotRect = _pilot.rect; _pilotDepth = _pilot.depth; } }

            seat.enabled = true;
            if (_pilot)
            {
                seat.cullingMask = _pilot.cullingMask;
                seat.clearFlags = _pilot.clearFlags;
                seat.backgroundColor = _pilot.backgroundColor;
            }

            float baseDepth = _pilot ? _pilotDepth : 0f;
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            var square = new Rect(margin / aspect, margin, pipSize / aspect, pipSize);   // a square in the lower left
            if (humanInSeat)
            {
                seat.rect = new Rect(0f, 0f, 1f, 1f);
                seat.depth = baseDepth + 1f;
                if (_pilot)
                {
                    _pilot.rect = square;   // the pilot's view in the seat view's own small square
                    _pilot.depth = baseDepth + 2f;
                    _pilotLifted = true;
                }
            }
            else
            {
                Restore();
                seat.rect = square;
                seat.depth = baseDepth + 1f;
            }
        }

        /// <summary>Give the pilot's camera back its own rect and depth.</summary>
        public void Restore()
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
