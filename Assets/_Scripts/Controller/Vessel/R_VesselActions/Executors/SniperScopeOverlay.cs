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
    /// <para><b>The reticle moved INTO the eyepiece and stays there.</b> Round 3 drew it over the
    /// middle of the screen because the middle of the screen was the scope. It is not any more, so
    /// a reticle there would be a measurement of a view nobody is looking through. Everything the
    /// scope says is therefore said inside the one shape the pilot is aiming with — which is also
    /// what a real scope's reticle furniture looks like. <b>A shaped window cannot bound its own
    /// furniture by its bounding box</b>, so every radius drawn inside it is capped by the petal's
    /// measured INRADIUS (<see cref="ScopePetalGeometry.Inradius01"/>, 0.59× the half-side, set by
    /// the two long edges running down to the apex) rather than by a fraction of the height.</para>
    ///
    /// <para><b>The reticle is a MEASUREMENT, not decoration.</b> Its radius is the cone's own
    /// half-angle projected through the WINDOW's live vertical field of view
    /// (<c>r = H · tan(halfAngle) / tan(fov/2)</c>, where <c>H</c> is half the eyepiece's HEIGHT,
    /// because a vertical field of view is what its vertical extent subtends) — so it
    /// tracks the zoom exactly. Anything inside the ring is inside the shot. An authored reticle
    /// sprite would be a claim about the weapon that stops being true the first time either number
    /// moves.</para>
    ///
    /// <para><b>The recharge ring is duplicated here on purpose.</b> The fleet's ability lockup
    /// carries it too — its clockwise veil now draws on a LOCKED card, which is what finally put
    /// the Serpent's recharge on the HUD row at all — but a pilot reading the eyepiece is not
    /// reading the bottom-right of the screen, and a sight that cannot say whether it is loaded is
    /// not a sight.</para>
    ///
    /// <para><b>Generated, not authored.</b> One runtime canvas, two <see cref="ScopePetalImage"/>s
    /// (the opaque backing and the picture, the same component so the frame is the petal's own
    /// outline) and four <see cref="ScopeRingGraphic"/>s; no sprites, no prefab, no per-vessel
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

        Canvas _canvas;
        RectTransform _pipRect;
        ScopePetalImage _backing;
        ScopePetalImage _pipSurface;
        ScopeRingGraphic _ring;
        ScopeRingGraphic _dot;
        ScopeRingGraphic _track;
        ScopeRingGraphic _arc;
        ScopePipView _pip;

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
            _track = MakeRing("ChargeTrack", 130f, ArcThickness);
            _arc = MakeRing("ChargeArc", 130f, ArcThickness);
            _ring = MakeRing("Reticle", 40f, RingThickness);
            _dot = MakeRing("ReticleDot", DotRadius, DotRadius * 2f);

            _pip = new ScopePipView(_pipSurface);
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
        /// A ring centred on the EYEPIECE. Anchored at the middle of the parent rect, which is a
        /// fraction of that rect and so is independent of the eyepiece's own top-left pivot.
        /// </summary>
        ScopeRingGraphic MakeRing(string name, float radius, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_pipRect, false);
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
        /// <param name="coneHalfAngleDegrees">The sniper cone's half-angle, which the reticle IS.</param>
        /// <param name="cooldown01">What is LEFT of the recharge (0 = ready), matching
        /// <c>SniperShotActionExecutor.CooldownRemaining01</c> and the fleet's own veil sense.</param>
        /// <param name="colour">The pilot's domain signal colour, shared with the shot's tracer so
        /// the mark you aim with and the mark you leave cannot disagree.</param>
        public void Tick(Transform vessel, float fieldOfView, float coneHalfAngleDegrees,
                         float cooldown01, Color colour)
        {
            SetVisible(true);

            float halfHeight = LayOutPip();
            float radius = ReticleRadiusPixels(coneHalfAngleDegrees, fieldOfView, halfHeight);
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
        /// The cone's half-angle in pixels INSIDE the eyepiece: the same projection the screen
        /// version used, with half the eyepiece's HEIGHT standing in for half the screen height and
        /// the window's own field of view standing in for the camera's.
        /// </summary>
        static float ReticleRadiusPixels(float coneHalfAngleDegrees, float fieldOfView,
                                         float halfHeight)
        {
            float halfFov = Mathf.Clamp(fieldOfView * 0.5f, 0.5f, 89f) * Mathf.Deg2Rad;
            float half = Mathf.Max(0.01f, coneHalfAngleDegrees) * Mathf.Deg2Rad;

            float pixels = halfHeight * Mathf.Tan(half) / Mathf.Tan(halfFov);
            // Floored so the ring is still a ring at the wide end of the dial. The CEILING is the
            // petal's own (see ReticleBudgetPixels, applied by the caller), not a fraction of the
            // half-height - a shaped window cannot be capped by its bounding box.
            return Mathf.Max(6f, pixels);
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
