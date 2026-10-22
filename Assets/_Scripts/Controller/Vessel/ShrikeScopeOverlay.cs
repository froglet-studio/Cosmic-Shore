using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's sight, for the LOCAL pilot only (design: <c>R_VesselActions/SHRIKE.md</c> §3.4). It works like the
    /// Serpent's scope (Garrett, 2026-10-16: "the zoom should work like the serpent's zoom"): the flight view is never
    /// magnified; the zoom is an EYEPIECE in the top left — a second camera riding the hull's nose, rendered into a window
    /// by the Serpent's own <see cref="ScopePipView"/> — and the trigger's depth dials its field of view.
    ///
    /// <list type="bullet">
    /// <item><b>The window is the selected mode's ELEMENT petal</b> ("the shape of the zoom window should match the
    /// element of the current mode"): Space mode a kite, Charge mode the gem, Time mode a diamond
    /// (<see cref="ElementPetalGeometry"/>, traced off the element flower's sprites; the Serpent's window is the Charge
    /// petal by the same rule).</item>
    /// <item><b>Every mark is on the WEAPONS' axis</b> ("the camera, weapons, zoom, etc. need to be aligned"): the
    /// eyepiece's camera looks down the hull's forward from its nose, which is the line the lance, the tether and the
    /// lock cone all use; the flight reticle is projected at <c>hull.position + hull.forward × range</c>, which the
    /// Shrike's on-axis chase camera puts at screen centre in the steady state, and the projection keeps it honest through
    /// the camera's lag.</item>
    /// <item><b>Charge mode's ring IS the lock cone</b>, its half-angle projected through each view's own field of view
    /// (the Serpent's <c>ReticlePixels</c> law), so anything inside the ring can be painted.</item>
    /// <item><b>The painted target carries a marker</b>: a spinning, pulsing diamond at its screen position.</item>
    /// <item><b>Both views carry a CROSSHAIR</b> (Garrett, 2026-10-16: "the zoom views needs crosshairs so i know where
    /// it is aiming in there"): full arms across the eyepiece to the petal's inradius, short ones on the flight view,
    /// each a bright line over a dark underlay so it reads against bright mass and empty space alike. Built from plain
    /// <see cref="Image"/> quads on purpose — the Serpent's eyepiece showed no generated ring or post for four
    /// playtests and its reticle first appeared when plain <c>Image</c> quads were added (SERPENT_SNIPER_SCOPE.md
    /// rounds 9-11), so the mark the pilot aims with is drawn by the component that provably renders. The eyepiece's
    /// camera rides the hull's nose looking down its forward, so the window's centre IS the weapons' axis.</item>
    /// </list>
    ///
    /// <para>Generated at runtime: one screen-space canvas with no scaler (every number is a screen pixel), the petal
    /// windows, the Serpent's ring and crosshair graphics, one sprite drawn once per process. Built on first use by
    /// <see cref="ShrikeExecutor"/> and disposed with it.</para>
    /// </summary>
    public sealed class ShrikeScopeOverlay : MonoBehaviour
    {
        // The Serpent eyepiece's layout (SniperScopeOverlay), so the two sights sit in the same place.
        const float PipHeightFraction = 0.5f;
        const float PipTopMargin = 220f;
        const float PipLeftMargin = 16f;
        const float BorderPixels = 3f;
        const float GraphicRectPixels = 512f;
        const float RingThickness = 2f;
        // The crosshair: line width, its dark underlay's margin, the centre pip, and each view's gap and arm.
        const float CrosshairThickness = 2f;
        const float CrosshairUnderlayPixels = 1.5f;
        const float CrosshairPipPixels = 4f;
        const float PipGapPixels = 10f;
        const float FlightGapPixels = 7f;
        const float FlightArmPixels = 24f;
        static readonly Color UnderlayColour = new(0f, 0f, 0f, 0.6f);
        const int MarkerTextureSize = 256;

        static Sprite _markerSprite;

        RectTransform _pipRect;
        ElementPetalImage _backing, _picture;
        ScopePipView _pip;
        ScopeRingGraphic _pipRing;
        RectTransform _flightRect;
        ScopeRingGraphic _flightRing;
        Crosshair _flightCrosshair, _pipCrosshair;
        Image _marker;
        RectTransform _markerRect;
        float _markerSpin;

        public static ShrikeScopeOverlay Create()
        {
            var go = new GameObject("[ShrikeScopeOverlay]");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var overlay = go.AddComponent<ShrikeScopeOverlay>();
            overlay.Build();
            return overlay;
        }

        void Build()
        {
            // The flight reticle first (the eyepiece covers it on the rare frame the aim point projects behind it).
            var flight = new GameObject("FlightReticle", typeof(RectTransform));
            flight.transform.SetParent(transform, false);
            _flightRect = (RectTransform)flight.transform;
            _flightRect.anchorMin = _flightRect.anchorMax = Vector2.zero;
            _flightRect.pivot = new Vector2(0.5f, 0.5f);
            _flightRect.sizeDelta = Vector2.zero;
            _flightRing = MakeRing(_flightRect, "Ring", 4f, RingThickness);
            _flightCrosshair = new Crosshair(_flightRect, "Crosshair");

            var pip = new GameObject("Pip", typeof(RectTransform));
            pip.transform.SetParent(transform, false);
            _pipRect = (RectTransform)pip.transform;
            _pipRect.anchorMin = _pipRect.anchorMax = new Vector2(0f, 1f);
            _pipRect.pivot = new Vector2(0f, 1f);
            _backing = MakePetal("Backing", 0f);
            _backing.color = new Color(0.02f, 0.03f, 0.05f, 0.95f);
            _picture = MakePetal("Picture", BorderPixels);
            _picture.enabled = false;
            _pipRing = MakeRing(_pipRect, "Ring", 30f, RingThickness);
            _pipCrosshair = new Crosshair(_pipRect, "Crosshair");   // after the picture: drawn over it
            _pip = new ScopePipView(_picture) { Label = "ShrikeScope" };

            EnsureMarkerSprite();
            var marker = new GameObject("LockMarker", typeof(RectTransform));
            marker.transform.SetParent(transform, false);
            _markerRect = (RectTransform)marker.transform;
            _markerRect.anchorMin = _markerRect.anchorMax = Vector2.zero;
            _markerRect.pivot = new Vector2(0.5f, 0.5f);
            _markerRect.sizeDelta = new Vector2(64f, 64f);
            _marker = marker.AddComponent<Image>();
            _marker.sprite = _markerSprite;
            _marker.raycastTarget = false;
            _marker.enabled = false;

            SetScoped(false);
        }

        ElementPetalImage MakePetal(string label, float inset)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(_pipRect, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var petal = go.AddComponent<ElementPetalImage>();
            petal.raycastTarget = false;
            return petal;
        }

        static ScopeRingGraphic MakeRing(RectTransform parent, string label, float radius, float thickness)
        {
            var rect = Centred(parent, label);
            var ring = rect.gameObject.AddComponent<ScopeRingGraphic>();
            ring.raycastTarget = false;
            ring.Radius = radius;
            ring.Thickness = thickness;
            return ring;
        }

        static RectTransform Centred(RectTransform parent, string label)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(GraphicRectPixels, GraphicRectPixels);
            return rect;
        }

        void SetScoped(bool on)
        {
            _pipRect.gameObject.SetActive(on);
            if (!on) _pip?.Hide();
        }

        /// <summary>
        /// One frame of the sight.
        /// </summary>
        /// <param name="hull">The local pilot's hull: the eyepiece rides its nose and the reticle sits on its axis.</param>
        /// <param name="scoped">Whether the eyepiece is up (the zoom trigger is held).</param>
        /// <param name="fieldOfView">The eyepiece's vertical field of view this frame (the trigger's depth).</param>
        /// <param name="element">The selected mode's element: the window's petal.</param>
        /// <param name="colour">The pilot's domain signal colour (the lance's beam and the reticles share it).</param>
        /// <param name="coneHalfAngleDegrees">Charge mode's lock cone (both views draw it as a ring); 0 for none.</param>
        /// <param name="range">How far the selected weapon reaches: the point on the axis the flight reticle marks.</param>
        /// <param name="hasLock">Whether a painted target exists.</param>
        /// <param name="lockWorld">The painted target's world position.</param>
        /// <param name="flightCamera">The gameplay camera.</param>
        public void Drive(Transform hull, bool scoped, float fieldOfView, Element element, Color colour,
            float coneHalfAngleDegrees, float range, bool hasLock, Vector3 lockWorld, Camera flightCamera)
        {
            if (!hull) { Hide(); return; }

            // The flight reticle, on the weapons' axis.
            bool flightShown = false;
            if (flightCamera)
            {
                Vector3 aim = hull.position + hull.forward * range;
                Vector3 screen = flightCamera.WorldToScreenPoint(aim);
                if (screen.z > 0f)
                {
                    flightShown = true;
                    _flightRect.anchoredPosition = new Vector2(screen.x, screen.y);
                    float r = coneHalfAngleDegrees > 0f
                        ? ReticlePixels(coneHalfAngleDegrees, flightCamera.fieldOfView, flightCamera.pixelHeight * 0.5f)
                        : 4f;
                    PaintRing(_flightRing, r, colour, coneHalfAngleDegrees > 0f);
                    _flightCrosshair.Lay(FlightGapPixels, FlightArmPixels, colour);
                }
            }
            _flightRect.gameObject.SetActive(flightShown);

            // The eyepiece.
            SetScoped(scoped);
            if (scoped)
            {
                float side = Mathf.Max(120f, Screen.height * PipHeightFraction);
                _pipRect.sizeDelta = new Vector2(side, side);
                _pipRect.anchoredPosition = new Vector2(PipLeftMargin, -PipTopMargin);
                _backing.Element = element;
                _picture.Element = element;
                _pip.Tick(hull, fieldOfView);
                // The arms reach to just inside the petal's inradius, so they never poke out through its taper.
                float reach = Mathf.Max(24f, ElementPetalGeometry.Inradius01(element) * side - BorderPixels - 6f);
                float r = coneHalfAngleDegrees > 0f
                    ? Mathf.Min(reach - 4f, ReticlePixels(coneHalfAngleDegrees, fieldOfView, side * 0.5f))
                    : 4f;
                PaintRing(_pipRing, r, colour, coneHalfAngleDegrees > 0f);
                _pipCrosshair.Lay(PipGapPixels, reach, colour);
            }

            // The painted target.
            bool markerShown = false;
            if (hasLock && flightCamera)
            {
                Vector3 screen = flightCamera.WorldToScreenPoint(lockWorld);
                if (screen.z > 0f)
                {
                    markerShown = true;
                    _markerRect.anchoredPosition = new Vector2(screen.x, screen.y);
                    _markerSpin += Time.unscaledDeltaTime * 90f;
                    _markerRect.localRotation = Quaternion.Euler(0f, 0f, _markerSpin);
                    _markerRect.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 8f));
                    _marker.color = colour;
                }
            }
            _marker.enabled = markerShown;
        }

        static void PaintRing(ScopeRingGraphic ring, float radius, Color colour, bool show)
        {
            ring.Radius = radius;
            ring.Sweep01 = 1f;
            ring.color = colour;
            ring.enabled = show;
        }

        /// <summary>
        /// A crosshair of plain <see cref="Image"/> quads centred on its parent: four arms from a centre gap out to a
        /// reach, and a centre pip, each a bright line over a darker, wider underlay.
        /// </summary>
        sealed class Crosshair
        {
            readonly RectTransform[] _under = new RectTransform[5];
            readonly RectTransform[] _line = new RectTransform[5];
            readonly Image[] _lineImages = new Image[5];

            public Crosshair(RectTransform parent, string label)
            {
                var root = Centred(parent, label);
                root.sizeDelta = Vector2.zero;
                for (int i = 0; i < 5; i++) _under[i] = Quad(root, "Under" + i, UnderlayColour, out _);
                for (int i = 0; i < 5; i++) _line[i] = Quad(root, "Line" + i, Color.white, out _lineImages[i]);
            }

            static RectTransform Quad(RectTransform root, string label, Color colour, out Image image)
            {
                var go = new GameObject(label, typeof(RectTransform));
                go.transform.SetParent(root, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                image = go.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = colour;
                return rect;
            }

            /// <summary>Arms from <paramref name="gap"/> to <paramref name="reach"/> pixels off centre.</summary>
            public void Lay(float gap, float reach, Color colour)
            {
                float length = Mathf.Max(2f, reach - gap), mid = gap + length * 0.5f;
                Vector2 across = new(length, CrosshairThickness), upright = new(CrosshairThickness, length);
                Vector2[] centre = { new(mid, 0f), new(-mid, 0f), new(0f, mid), new(0f, -mid), Vector2.zero };
                Vector2[] size = { across, across, upright, upright, Vector2.one * CrosshairPipPixels };
                Color line = new(colour.r, colour.g, colour.b, 1f);
                for (int i = 0; i < 5; i++)
                {
                    _line[i].anchoredPosition = centre[i];
                    _line[i].sizeDelta = size[i];
                    _lineImages[i].color = line;
                    _under[i].anchoredPosition = centre[i];
                    _under[i].sizeDelta = size[i] + Vector2.one * (2f * CrosshairUnderlayPixels);
                }
            }
        }

        /// <summary>A cone's half-angle in pixels through one view (the Serpent overlay's law):
        /// <c>r = H · tan(halfAngle) / tan(fov / 2)</c>, <c>H</c> half that view's height.</summary>
        static float ReticlePixels(float halfAngleDegrees, float fieldOfView, float halfHeight)
            => Mathf.Max(3f, halfHeight * Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad)
                             / Mathf.Max(1e-4f, Mathf.Tan(Mathf.Clamp(fieldOfView, 1f, 170f) * 0.5f * Mathf.Deg2Rad)));

        public void Hide()
        {
            if (_flightRect) _flightRect.gameObject.SetActive(false);
            if (_pipRect) SetScoped(false);
            if (_marker) _marker.enabled = false;
        }

        /// <summary>Tear down: a RenderTexture that outlives its owner is a leak nothing reports.</summary>
        public void Dispose()
        {
            _pip?.Dispose();
            _pip = null;
            Destroy(gameObject);
        }

        // The painted-target marker: a diamond outline with four ticks pointing in (anti-aliased, white, tinted).
        static void EnsureMarkerSprite()
        {
            if (_markerSprite) return;
            int n = MarkerTextureSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "ShrikeLockMarker",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[n * n];
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                    float diamond = Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - 96f) / Mathf.Sqrt(2f);
                    float a = Stroke(diamond, 6f);
                    float ticks = Mathf.Min(Mathf.Min(Segment(p, new Vector2(0, 120), new Vector2(0, 70)), Segment(p, new Vector2(0, -120), new Vector2(0, -70))),
                                            Mathf.Min(Segment(p, new Vector2(120, 0), new Vector2(70, 0)), Segment(p, new Vector2(-120, 0), new Vector2(-70, 0))));
                    a = Mathf.Max(a, Stroke(ticks, 5f));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _markerSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        static float Stroke(float d, float w) => Mathf.Clamp01(w + 0.5f - Mathf.Abs(d));

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }
    }
}
