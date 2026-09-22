using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent scope's on-screen instrument: an eyepiece in the top left <b>shaped like the
    /// CHARGE petal</b> carrying the magnified view, a <b>reticle drawn at the sniper cone's true
    /// angular size</b> inside it, and the <b>recharge ring</b> around that reticle.
    ///
    /// <para><b>The shape is one petal of the element flower</b>
    /// (<see cref="ScopePetalGeometry"/>), traced off the shipped <c>charge_petal</c> sprite — which
    /// is the element that owns this weapon, so the window says which element is firing without a
    /// label. Its 72° apex is the same 72° the five-petal flower is built from; that is not a
    /// coincidence the code relies on, but it is how the measurement was confirmed.</para>
    ///
    /// <para><b>The surface is still the one that renders, and that is round 7's rule carried
    /// into round 8.</b> Round 4 was right to move the magnification into the window and wrong to
    /// rebuild the window in the same pass: the surface it substituted (a generated circular
    /// graphic) had never rendered, and rounds 5 and 6 were spent adopting URP settings and
    /// hardening lifetimes on a panel that was not on screen to benefit from either. <b>When one
    /// change both replaces a surface and re-points it, the pilot's report cannot say which half
    /// broke</b> — and here it did not: all three rounds came back as the same four words. Round 7
    /// therefore put round 3's <c>RawImage</c> back verbatim, and round 8 re-shaped it into the
    /// petal by <b>subclassing that component</b> (<see cref="ScopePetalImage"/>) rather than
    /// writing a new one — so the only thing that changed is the vertex list, and a bad emit reads
    /// as a wrong SHAPE rather than as an absence. The half-screen height and the 16/220 margins
    /// are round 3's, untouched; the rect went 16:9 → SQUARE with the shape.
    /// <c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> rounds 7–8.</para>
    ///
    /// <para><b>There are TWO reticles, and they are the same measurement through different
    /// optics.</b> One sits inside the eyepiece and one is drawn over the FLIGHT view, because a
    /// scoped pilot is using both pictures: the eyepiece to read the target and the flight view to
    /// keep the shot on it while they fly. Both are the sniper cone's own angular size projected
    /// through the view they are drawn in — one function,
    /// <see cref="ReticlePixels"/>, handed each view's half-height and its own field of view — so
    /// the eyepiece's <b>GROWS as the pilot zooms</b> (its field of view narrows while the cone
    /// does not) while the flight view's <b>stays small</b>, since the gameplay camera's field of
    /// view is not the scope's dial. Two circles the pilot can compare, both of them true.</para>
    ///
    /// <para><b>Round 3 drew a reticle over the middle of the screen because the middle of the
    /// screen WAS the scope; this one is not that.</b> It is not centred and it is not a
    /// decoration left behind — it is projected at the point the shot actually reaches
    /// (<c>hull.position + hull.forward × range</c>), so it marks where the round goes rather than
    /// where the camera happens to look. On the Serpent's authored follow offset — a pure
    /// <c>(0, 0, −250)</c>, camera exactly behind the hull on the shot's own axis — that lands on
    /// screen centre in the steady state, and the projection is what keeps it honest through the
    /// camera's smoothing lag, through a rear-view flip (where the aim point falls behind the
    /// camera and the reticle is stood down outright) and on any future follow offset that is not
    /// on the axis.</para>
    ///
    /// <para><b>A shaped window cannot bound its own furniture by its bounding box</b>, so every
    /// radius drawn inside the EYEPIECE is capped by the petal's measured INRADIUS
    /// (<see cref="ScopePetalGeometry.Inradius01"/>, 0.59× the half-side, set by the two long
    /// edges running down to the apex) rather than by a fraction of the height. The flight view's
    /// reticle has no such shape to sit in and takes no cap.</para>
    ///
    /// <para><b>A reticle is a MEASUREMENT, not decoration.</b> Its radius is the cone's own
    /// half-angle projected through its view's live vertical field of view
    /// (<c>r = H · tan(halfAngle) / tan(fov/2)</c>, where <c>H</c> is half that view's HEIGHT,
    /// because a vertical field of view is what a vertical extent subtends) — so the eyepiece's
    /// tracks the zoom exactly and the flight view's tracks whatever the speed tunnel has done to
    /// the gameplay camera. Anything inside either ring is inside the shot. An authored reticle
    /// sprite would be a claim about the weapon that stops being true the first time any of those
    /// numbers moves.</para>
    ///
    /// <para><b>Both reticles are up only while the scope is.</b> They are built with the rest of
    /// the instrument, shown by <see cref="Tick"/> and taken down by <see cref="Hide"/>, so a
    /// pilot who is not scoped has no mark on their screen at all — which is what makes the mark
    /// mean "I am aiming" rather than "I am a Serpent".</para>
    ///
    /// <para><b>The recharge ring is duplicated here on purpose.</b> The fleet's ability lockup
    /// carries it too — its clockwise veil now draws on a LOCKED card, which is what finally put
    /// the Serpent's recharge on the HUD row at all — but a pilot reading the eyepiece is not
    /// reading the bottom-right of the screen, and a sight that cannot say whether it is loaded is
    /// not a sight.</para>
    ///
    /// <para><b>Generated, not authored.</b> One runtime canvas, two <see cref="ScopePetalImage"/>s
    /// (the opaque backing and the picture, the same component so the frame is the petal's own
    /// outline) and six <see cref="ScopeRingGraphic"/>s; no sprites, no prefab, no per-vessel
    /// wiring — the petal is traced geometry, not the <c>charge_petal</c> PNG, so it is exact at
    /// any size. It is built on first use by <see cref="SniperScopeActionExecutor"/> for the LOCAL
    /// PILOT only and torn down with the vessel.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SniperScopeOverlay : MonoBehaviour
    {
        const float ReadyFlashSeconds = 0.35f;
        const float ArcThickness = 5f;
        const float RingThickness = 2f;
        const float DotRadius = 2.5f;
        const float DimAlpha = 0.3f;
        const float TrackAlpha = 0.22f;

        // The dark backing shows through as a FRAME around the picture, which is what makes the
        // panel read as an instrument rather than as a hole in the screen.
        const float BorderPixels = 3f;

        // How far inside the petal's own edges the recharge ring sits.
        const float ArcInsetPixels = 8f;

        // Clearance between the recharge ring and the largest reticle it may have to sit outside.
        const float ReticleClearancePixels = 4f;

        // The eyepiece's SIDE as a fraction of SCREEN HEIGHT, not a pixel size: this canvas has no
        // CanvasScaler (every other number in it is a real screen measurement), so a fixed panel is
        // half a phone screen and a postage stamp on a monitor. Round 3's value, and the petal is
        // square to within 0.5% so it keeps the height the pilot already reads.
        const float PipHeightFraction = 0.5f;

        // Clearance for the goal stack, which anchors at (16, -52) and runs up to three 48-unit
        // rows (Tools/Build/author_goal_stack.py). Sized for the full three so a mode that authors
        // secondary goals cannot land one behind this panel. Round 3's values.
        const float PipTopMargin = 220f;
        const float PipLeftMargin = 16f;

        // The flight view's reticle floors lower than the eyepiece's: it is SUPPOSED to be small
        // (the gameplay camera is wide), so a floor generous enough to keep the eyepiece's ring
        // readable would here be an inflation of the very number the ring exists to state. Three
        // pixels is the smallest radius at which ScopeRingGraphic still emits a ring rather than a
        // smudge, and at the widest field of view the fleet runs it does not bind.
        const float FlightReticleMinPixels = 3f;

        Canvas _canvas;
        RectTransform _pipRect;
        ScopePetalImage _backing;
        ScopePetalImage _pipSurface;
        ScopeRingGraphic _ring;
        ScopeRingGraphic _dot;
        ScopeRingGraphic _track;
        ScopeRingGraphic _arc;
        ScopePipView _pip;

        // The flight view's reticle. Its own root, so it can be placed in SCREEN coordinates and
        // stood down on its own (a rear-view flip hides it while the eyepiece keeps working).
        RectTransform _flightRect;
        ScopeRingGraphic _flightRing;
        ScopeRingGraphic _flightDot;

        bool _wasReady = true;
        float _readyFlashUntil;
        int _ticks;
        bool _selfChecked;

        /// <summary>
        /// Build the overlay on a new GameObject. Local pilot only — the caller owns that gate,
        /// because a camera and a screen are things one machine has.
        /// </summary>
        public static SniperScopeOverlay Create()
        {
            var go = new GameObject("[SerpentScopeOverlay]");
            var overlay = go.AddComponent<SniperScopeOverlay>();
            overlay.Build();
            // Hidden by the PANEL, never by the canvas - see SetVisible.
            overlay.SetVisible(false);
            return overlay;
        }

        void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the vessel HUD, below nothing that matters: this is a sight, so anything that
            // covered it would be the bug.
            _canvas.sortingOrder = 500;
            // Deliberately NO CanvasScaler: every number here is already in real screen pixels,
            // derived from a field of view, and a reference-resolution scaler would silently
            // re-scale a measurement into a decoration.
            // Deliberately NO GraphicRaycaster either - a hit target over the flight view would
            // eat presses meant for the world.

            // FIRST, so it is the EARLIEST sibling: UGUI draws siblings in order, and on the rare
            // frame the aim point projects into the top-left corner the opaque eyepiece should
            // cover the flight reticle rather than have a stray ring floating over the picture.
            BuildFlightReticle();

            var pipGo = new GameObject("Pip", typeof(RectTransform));
            pipGo.transform.SetParent(transform, false);
            _pipRect = pipGo.GetComponent<RectTransform>();
            // TOP-LEFT, pivoted on its own top-left corner, so the margins below are read straight
            // off the screen edges with no size arithmetic in them. Round 3's anchors and margins
            // exactly; only the rect's WIDTH changed, when the window became a petal.
            _pipRect.anchorMin = new Vector2(0f, 1f);
            _pipRect.anchorMax = new Vector2(0f, 1f);
            _pipRect.pivot = new Vector2(0f, 1f);

            // The BACKING is opaque, is drawn first, and is on from the moment the scope is raised
            // - so the eyepiece is an OBJECT on screen before the first render lands and even when
            // what it is pointed at is empty space. A window showing nothing and no window at all
            // must not look the same. It is the SAME component as the picture with no texture
            // assigned, which is what makes the frame the petal's own outline rather than a
            // rectangle behind it: RawImage falls back to a white texture, so a textureless
            // instance is a flat shape in its own colour.
            _backing = MakePetal("Backing", 0f);
            _backing.color = new Color(0.02f, 0.03f, 0.05f, 0.95f);

            _pipSurface = MakePetal("Picture", BorderPixels);
            _pipSurface.enabled = false;

            // Order matters: the recharge bed, then the arc, then the reticle on top - UGUI draws
            // siblings in order and the reticle is the thing being aimed with.
            _track = MakeRing(_pipRect, "ChargeTrack", 130f, ArcThickness);
            _arc = MakeRing(_pipRect, "ChargeArc", 130f, ArcThickness);
            _ring = MakeRing(_pipRect, "Reticle", 40f, RingThickness);
            _dot = MakeRing(_pipRect, "ReticleDot", DotRadius, DotRadius * 2f);

            _pip = new ScopePipView(_pipSurface);
        }

        /// <summary>
        /// The reticle drawn over the FLIGHT view: a root placed in screen coordinates every
        /// frame, carrying the same ring-and-pip pair the eyepiece uses.
        ///
        /// <para>Its root is anchored to the canvas's bottom-left corner with a CENTRED pivot and
        /// a zero size, which is what makes <c>anchoredPosition</c> a screen pixel coordinate
        /// outright: this canvas is ScreenSpaceOverlay with deliberately no <c>CanvasScaler</c>,
        /// so a canvas unit IS a screen pixel and <c>Camera.WorldToScreenPoint</c>'s answer can be
        /// written straight in with no mapping to keep in step.</para>
        /// </summary>
        void BuildFlightReticle()
        {
            var go = new GameObject("FlightReticle", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            _flightRect = go.GetComponent<RectTransform>();
            _flightRect.anchorMin = Vector2.zero;
            _flightRect.anchorMax = Vector2.zero;
            _flightRect.pivot = new Vector2(0.5f, 0.5f);
            _flightRect.sizeDelta = Vector2.zero;

            _flightRing = MakeRing(_flightRect, "Reticle", FlightReticleMinPixels, RingThickness);
            _flightDot = MakeRing(_flightRect, "ReticleDot", DotRadius, DotRadius * 2f);
        }

        /// <summary>
        /// One petal-shaped layer filling the eyepiece's rect, inset by <paramref name="inset"/> on
        /// every side. Insetting the RECT rather than the outline is deliberate: it shrinks the
        /// petal about its own centre, so the backing shows through as a frame that follows every
        /// edge including the apex, with no second outline to keep in step.
        /// </summary>
        ScopePetalImage MakePetal(string name, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_pipRect, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);

            var petal = go.AddComponent<ScopePetalImage>();
            petal.raycastTarget = false;
            return petal;
        }

        /// <summary>
        /// A ring centred on <paramref name="parent"/>. Anchored at the middle of that rect, which
        /// is a FRACTION of it and so is independent of whatever pivot the parent carries — the
        /// eyepiece's is its own top-left corner and the flight reticle's root is its centre.
        /// </summary>
        ScopeRingGraphic MakeRing(RectTransform parent, string name, float radius, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var ring = go.AddComponent<ScopeRingGraphic>();
            ring.raycastTarget = false;
            ring.Radius = radius;
            ring.Thickness = thickness;
            return ring;
        }

        /// <summary>
        /// Drive one frame of the instrument.
        /// </summary>
        /// <param name="vessel">The local pilot's hull — the window's eye rides past its nose.</param>
        /// <param name="fieldOfView">The window's magnified vertical field of view this frame. A
        /// pure function of the pilot's trigger depth.</param>
        /// <param name="coneHalfAngleDegrees">The sniper cone's half-angle, which both reticles
        /// ARE.</param>
        /// <param name="rangeUnits">How far the round reaches. Only the FLIGHT view's reticle uses
        /// it, as the point along the shot's axis to project — the eyepiece's camera sits on that
        /// axis, so there every range projects to the same place.</param>
        /// <param name="cooldown01">What is LEFT of the recharge (0 = ready), matching
        /// <c>SniperShotActionExecutor.CooldownRemaining01</c> and the fleet's own veil sense.</param>
        /// <param name="colour">The pilot's domain signal colour, shared with the shot's tracer so
        /// the mark you aim with and the mark you leave cannot disagree.</param>
        public void Tick(Transform vessel, float fieldOfView, float coneHalfAngleDegrees,
                         float rangeUnits, float cooldown01, Color colour)
        {
            SetVisible(true);

            float halfHeight = LayOutPip();
            float radius = ReticlePixels(coneHalfAngleDegrees, TanHalf(fieldOfView), halfHeight,
                                         minPixels: 6f);
            radius = Mathf.Min(radius, ReticleBudgetPixels(halfHeight));
            bool ready = cooldown01 <= 0.0001f;

            // The instant it comes back: a one-shot flash, so a pilot watching the target rather
            // than the arc still sees the weapon arrive.
            if (ready && !_wasReady) _readyFlashUntil = Time.unscaledTime + ReadyFlashSeconds;
            _wasReady = ready;

            float flash01 = Mathf.Clamp01((_readyFlashUntil - Time.unscaledTime) / ReadyFlashSeconds);

            _ring.Radius = radius;
            _ring.Thickness = RingThickness;
            _ring.Sweep01 = 1f;
            _ring.color = WithAlpha(colour, ready ? 1f : DimAlpha);

            _dot.Radius = DotRadius;
            _dot.Thickness = DotRadius * 2f;
            _dot.Sweep01 = 1f;
            _dot.color = WithAlpha(colour, ready ? 1f : DimAlpha * 0.7f);

            // INSIDE the petal, against its own tightest edges - which are the two long ones
            // running down to the apex, not the top edge, so this is meaningfully smaller than the
            // half-height (measured 0.59x it). A circle drawn at the half-height would poke out
            // through the taper, and one drawn OUTSIDE the shape needs 1.18x the half-height, which
            // at this window's authored margins pushes the instrument off the left of the screen.
            float arcRadius = Mathf.Max(radius + ArcThickness * 2f,
                                        ReticleBudgetPixels(halfHeight) + ArcThickness
                                            + ReticleClearancePixels);

            // The BED: always drawn, always a full ring, so "recharging" reads as a ring FILLING
            // rather than as one appearing out of nowhere - and so the readout is present on screen
            // at 0%, which is the frame the pilot most wants it. Same rule the goal stack's
            // progress bar records (Docs/GAME_MODE_TOPBAR.md): a progress bar needs a bed.
            _track.Radius = arcRadius;
            _track.Thickness = ArcThickness;
            _track.Sweep01 = 1f;
            _track.color = WithAlpha(colour, TrackAlpha);

            _arc.Thickness = ArcThickness;
            if (ready)
            {
                // READY is a COMPLETE bright ring, not the absence of one. The flash is an
                // expansion on top of it, so the arrival is an event and the state is a state.
                _arc.Sweep01 = 1f;
                _arc.Radius = arcRadius + (1f - flash01) * 10f;
                _arc.color = WithAlpha(colour, Mathf.Lerp(0.95f, 1f, flash01));
            }
            else
            {
                _arc.Radius = arcRadius;
                _arc.Sweep01 = 1f - cooldown01;
                _arc.color = WithAlpha(colour, 0.9f);
            }

            _pip.Tick(vessel, fieldOfView);

            DrawFlightReticle(vessel, coneHalfAngleDegrees, rangeUnits, ready, colour);

            SelfCheck(halfHeight);
        }

        /// <summary>
        /// Size and place the eyepiece against the LIVE screen, every frame, and hand back half its
        /// side. It is a fraction of screen height rather than an authored pixel size because this
        /// canvas deliberately has no <c>CanvasScaler</c> - every other number in it is a real
        /// screen measurement - so a fixed rect would be a different fraction of the display on
        /// every device and would not follow a resize.
        ///
        /// <para>The rect is SQUARE where round 7's was 16:9, because the petal's own bounding box
        /// is square to within 0.5% and its coordinates double as the picture's UVs - so a square
        /// rect against a square render target samples the view with nothing squashed and nothing
        /// thrown away. The HEIGHT and the margins are round 3's, unchanged, which is what keeps the
        /// eyepiece the size and in the place the pilot already reads.</para>
        /// </summary>
        float LayOutPip()
        {
            float side = Mathf.Max(120f, Screen.height * PipHeightFraction);
            _pipRect.sizeDelta = new Vector2(side, side);
            _pipRect.anchoredPosition = new Vector2(PipLeftMargin, -PipTopMargin);
            return side * 0.5f;
        }

        /// <summary>
        /// The largest radius anything drawn as a circle at the eyepiece's optical centre may take
        /// and still sit inside the petal, with the recharge ring's own band and clearance taken
        /// out. DERIVED from the outline rather than authored, so re-tracing the sprite moves it.
        /// </summary>
        static float ReticleBudgetPixels(float halfSide)
        {
            // Inradius01 is a fraction of the SIDE; halfSide is half of it.
            float inradius = ScopePetalGeometry.Inradius01 * halfSide * 2f;
            return Mathf.Max(8f, inradius - BorderPixels - ArcInsetPixels
                                 - ArcThickness - ReticleClearancePixels);
        }

        /// <summary>
        /// The cone's half-angle in pixels through ONE view's optics — the whole of what both
        /// reticles are.
        ///
        /// <para>A vertical field of view is what a view's vertical extent subtends, so half that
        /// extent in pixels divided by <c>tan(fov/2)</c> is the pixels-per-unit-tangent of that
        /// picture, and the cone's own tangent scales straight through it. The eyepiece passes
        /// half its HEIGHT and the field of view the scope is driving its window at; the flight
        /// view passes half the gameplay camera's pixel height and that camera's live field of
        /// view. Same function, and the difference between the two pictures is entirely in the
        /// arguments — which is exactly the claim the instrument is making.</para>
        ///
        /// <para>The floor is the CALLER's because the two views want different ones: the
        /// eyepiece's ring must stay readable at the wide end of its dial, while the flight view's
        /// is supposed to be small and a generous floor there would inflate the number the ring
        /// exists to state. Neither view takes a CEILING here — the eyepiece's is the petal's own
        /// inradius (see <see cref="ReticleBudgetPixels"/>, applied by its caller), because a
        /// shaped window cannot be capped by its bounding box, and the flight view has no shape to
        /// sit inside at all.</para>
        /// </summary>
        static float ReticlePixels(float coneHalfAngleDegrees, float tanHalfFov, float halfHeight,
                                   float minPixels)
        {
            float half = Mathf.Max(0.01f, coneHalfAngleDegrees) * Mathf.Deg2Rad;
            float pixels = halfHeight * Mathf.Tan(half) / Mathf.Max(0.0001f, tanHalfFov);
            return Mathf.Max(minPixels, pixels);
        }

        /// <summary>
        /// <c>tan(fov / 2)</c> for a vertical field of view in degrees, clamped away from both
        /// degenerate ends so no camera state can divide the projection above by zero or drive it
        /// to infinity.
        /// </summary>
        static float TanHalf(float fieldOfViewDegrees) =>
            Mathf.Tan(Mathf.Clamp(fieldOfViewDegrees * 0.5f, 0.5f, 89f) * Mathf.Deg2Rad);

        /// <summary>
        /// Place and size the FLIGHT view's reticle for this frame, or stand it down.
        ///
        /// <para>Its centre is the screen projection of the point the round actually reaches, and
        /// that is what makes it a mark on the SHOT rather than on the camera: the gameplay camera
        /// is not guaranteed to sit on the shot's axis — the Serpent's authored offset puts it
        /// there in the steady state, its smoothing takes it off through every turn, and the
        /// rear-view mirror puts it on the wrong side entirely — so the projection is re-measured
        /// every frame rather than assumed to be screen centre.</para>
        ///
        /// <para>It is stood down, rather than clamped to an edge, whenever the aim point falls
        /// behind the camera (<c>WorldToScreenPoint</c>'s <c>z</c> is the view-space depth, and a
        /// negative one projects to a MIRRORED position that is a plausible-looking lie). An
        /// orthographic or absent camera stands it down too, loudly once, because neither can
        /// project an angle — while the eyepiece, which carries its own camera, keeps working.</para>
        /// </summary>
        void DrawFlightReticle(Transform vessel, float coneHalfAngleDegrees, float rangeUnits,
                               bool ready, Color colour)
        {
            if (_flightRect == null) return;

            if (vessel == null)
            {
                _flightRect.gameObject.SetActive(false);
                return;
            }

            var cam = Camera.main;
            if (cam == null || cam.orthographic)
            {
                SniperScopeDiagnostics.Decline(SniperScopeDiagnostics.Reason.NoGameCamera);
                _flightRect.gameObject.SetActive(false);
                return;
            }

            // The same origin and axis SniperShotActionExecutor.ResolveShot casts along, so the
            // mark cannot drift from the round.
            Vector3 aim = vessel.position + vessel.forward * Mathf.Max(1f, rangeUnits);
            Vector3 projected = cam.WorldToScreenPoint(aim);
            if (projected.z <= 0f)
            {
                _flightRect.gameObject.SetActive(false);
                return;
            }

            _flightRect.gameObject.SetActive(true);
            _flightRect.anchoredPosition = new Vector2(projected.x, projected.y);

            // pixelHeight rather than Screen.height: it is the height the projection above is
            // expressed in, so the two agree even for a camera drawing into a sub-viewport.
            float radius = ReticlePixels(coneHalfAngleDegrees, TanHalf(cam.fieldOfView),
                                         cam.pixelHeight * 0.5f, FlightReticleMinPixels);

            // Both derived from the radius rather than authored, so the mark keeps reading as a
            // ring with a pip in it at whatever size the cone works out to: a fixed 2 px band and
            // a fixed 2.5 px dot would together fill a 5 px reticle solid.
            _flightRing.Radius = radius;
            _flightRing.Thickness = Mathf.Min(RingThickness, radius * 0.5f);
            _flightRing.Sweep01 = 1f;
            _flightRing.color = WithAlpha(colour, ready ? 1f : DimAlpha);

            float dot = Mathf.Clamp(radius * 0.3f, 1f, DotRadius);
            _flightDot.Radius = dot;
            _flightDot.Thickness = dot * 2f;
            _flightDot.Sweep01 = 1f;
            _flightDot.color = WithAlpha(colour, ready ? 1f : DimAlpha * 0.7f);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public void Hide()
        {
            SetVisible(false);
            _pip?.Hide();
        }

        /// <summary>
        /// Show or hide the instrument by toggling the PANEL GameObject, and never by toggling
        /// <c>Canvas.enabled</c>.
        ///
        /// <para><b>The two are not interchangeable, and only one of them recovers.</b> A UGUI
        /// <c>Graphic</c> caches its canvas in <c>m_Canvas</c>, and <c>Graphic.IsActive()</c> is
        /// <c>base.IsActive() &amp;&amp; m_Canvas != null</c> — so every <c>SetVerticesDirty</c> /
        /// <c>SetMaterialDirty</c> is a no-op while that cache is null. When a canvas is disabled
        /// underneath them, <c>OnCanvasHierarchyChanged</c> nulls the cache and then tests
        /// <c>IsActive()</c>, which is false BECAUSE it just nulled it, so it returns without
        /// re-caching — and it does the same thing on the way back up. The graphics are then
        /// permanently unable to rebuild: they go on drawing whatever mesh they happened to have
        /// when the canvas went down, at whatever size that was, for the rest of the session.
        /// Toggling the GameObject instead runs <c>Graphic.OnEnable</c>, which calls
        /// <c>CacheCanvas()</c> and <c>SetAllDirty()</c> — so it recovers by construction.</para>
        ///
        /// <para>Which is why <see cref="Tick"/> calls this FIRST, before it writes a single
        /// radius: a graphic still has to be active at the moment it is told its geometry changed.
        /// That ordering is part of the contract, not incidental.</para>
        /// </summary>
        void SetVisible(bool visible)
        {
            if (_pipRect != null) _pipRect.gameObject.SetActive(visible);

            // The flight reticle is only ever SWITCHED OFF here, never on, and the asymmetry is
            // deliberate: its visibility has two levels — the scope is up, AND the aim point can
            // be projected this frame — and <see cref="DrawFlightReticle"/> owns the second. Were
            // this to re-activate it every tick, a frame it had just stood down (the rear view)
            // would be re-activated on the next one and stood down again, and each of those
            // toggles runs Graphic.OnEnable -> SetAllDirty: a rebuild of both its meshes, every
            // frame, for as long as the pilot is looking backwards. SetActive with the value an
            // object already has is a no-op, so leaving the ON case to that method costs nothing.
            if (!visible && _flightRect != null) _flightRect.gameObject.SetActive(false);
        }

        /// <summary>
        /// Check ONCE, a couple of frames in, that the instrument this class just drew can actually
        /// be seen — and warn if not.
        ///
        /// <para>It exists because everything below is silent when it fails. A disabled canvas, a
        /// graphic with no cached canvas, a zero size, a transparent backing and a panel parked off
        /// the edge of the screen all produce the identical report from a pilot: nothing is there.
        /// None of them throws, and none of them is visible in a prefab or a scene, because the
        /// whole instrument is generated at runtime.</para>
        ///
        /// <para>It runs on the second tick rather than the first so the canvas update pass has had
        /// a frame to build the meshes, and it never runs again: these are construction facts, not
        /// per-frame ones, so a repeat check would be per-frame work in exchange for nothing. If it
        /// passes, the one line it emits goes on a channel — the fault cases are the loud
        /// ones.</para>
        /// </summary>
        void SelfCheck(float halfHeight)
        {
            if (_selfChecked) return;
            if (++_ticks < 2) return;
            _selfChecked = true;

            if (_canvas == null || !_canvas.isActiveAndEnabled)
            {
                SniperScopeDiagnostics.Unusable(
                    "its Canvas is null or disabled, so nothing under it is submitted at all.");
                return;
            }

            // The exact hazard SetVisible's doc comment is about. Graphic.IsActive() reads the
            // m_Canvas FIELD, so it is false while that cache is stale - which is the state in
            // which every mesh rebuild silently does nothing and the rings stay frozen at their
            // build-time size. Deliberately IsActive() and not the `canvas` PROPERTY: that getter
            // re-caches on read, so asking it would heal the very thing being tested and report
            // clean. A standing guard against the visibility toggle regressing to Canvas.enabled.
            if (_backing != null && !_backing.IsActive())
            {
                SniperScopeDiagnostics.Unusable(
                    "its graphics report IsActive() false with the eyepiece up, so Graphic.m_Canvas " +
                    "is stale and every mesh rebuild is a silent no-op - the rings are frozen at " +
                    "whatever size they were built with. Something is toggling Canvas.enabled " +
                    "under them; see SniperScopeOverlay.SetVisible.");
                return;
            }

            if (halfHeight <= 1f)
            {
                SniperScopeDiagnostics.Unusable(
                    $"its eyepiece resolved to {halfHeight * 2f:0.##} px across. Screen.height reads " +
                    $"{Screen.height}.");
                return;
            }

            if (_backing != null && _backing.color.a <= 0.01f)
            {
                SniperScopeDiagnostics.Unusable("its backing is fully transparent.");
                return;
            }

            // The eyepiece's pivot is its TOP-LEFT corner, so this is that corner in screen
            // coordinates measured from the bottom-left - which is what the reporter tool prints.
            Vector2 size = _pipRect.sizeDelta;
            Vector2 topLeft = new(_pipRect.anchoredPosition.x,
                                  Screen.height + _pipRect.anchoredPosition.y);
            Vector2 centre = new(topLeft.x + size.x * 0.5f, topLeft.y - size.y * 0.5f);
            if (topLeft.x > Screen.width || topLeft.x + size.x < 0f ||
                topLeft.y < 0f || topLeft.y - size.y > Screen.height)
            {
                SniperScopeDiagnostics.Unusable(
                    $"its eyepiece is off screen: top-left {topLeft}, size {size}, screen " +
                    $"{Screen.width}x{Screen.height}.");
                return;
            }

            SniperScopeDiagnostics.Drawing(centre, halfHeight, new Vector2(Screen.width, Screen.height));
        }

        void OnDestroy() => _pip?.Dispose();

        /// <summary>Destroy the overlay outright. Called when the vessel goes away.</summary>
        public void Dispose()
        {
            _pip?.Dispose();
            if (this != null) Destroy(gameObject);
        }
    }
}
