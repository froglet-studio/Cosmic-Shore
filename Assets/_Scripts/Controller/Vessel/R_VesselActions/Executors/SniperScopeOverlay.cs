using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent scope's on-screen instrument: a <b>round eyepiece</b> carrying the magnified
    /// view, a <b>reticle drawn at the sniper cone's true angular size</b> inside it, and the
    /// <b>recharge ring</b> around its rim.
    ///
    /// <para><b>It is ONE object, and that is the point.</b> Round 3 drew the reticle over the
    /// middle of the screen and the picture in a corner, because the middle of the screen WAS the
    /// scope. Now the magnification lives in the window (<see cref="ScopePipView"/>) and the
    /// flight view is left alone, so the reticle has to live where the magnified picture is or it
    /// would be a measurement of a view nobody is looking through. Everything the scope says is
    /// therefore said in one place the pilot is already looking at.</para>
    ///
    /// <para><b>The reticle is a MEASUREMENT, not decoration.</b> Its radius is the cone's own
    /// half-angle projected through the WINDOW's live vertical field of view
    /// (<c>r = R · tan(halfAngle) / tan(fov/2)</c>, where <c>R</c> is the eyepiece's own radius) —
    /// so it tracks the zoom exactly. Anything inside the ring is inside the shot. An authored
    /// reticle sprite would be a claim about the weapon that stops being true the first time
    /// either number moves.</para>
    ///
    /// <para><b>The recharge ring is duplicated here on purpose.</b> The fleet's ability lockup
    /// carries it too — its clockwise veil now draws on a LOCKED card, which is what finally put
    /// the Serpent's recharge on the HUD row at all — but a pilot reading the eyepiece is not
    /// reading the bottom-right of the screen, and a sight that cannot say whether it is loaded is
    /// not a sight.</para>
    ///
    /// <para><b>Generated, not authored.</b> One runtime canvas, four
    /// <see cref="ScopeRingGraphic"/>s and one <see cref="ScopeDiscGraphic"/>; no sprites, no
    /// prefab, no per-vessel wiring. It is built on first use by
    /// <see cref="SniperScopeActionExecutor"/> for the LOCAL PILOT only and torn down with the
    /// vessel.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SniperScopeOverlay : MonoBehaviour
    {
        const float ReadyFlashSeconds = 0.35f;
        const float RimThickness = 4f;
        const float ArcGapPixels = 10f;
        const float ArcThickness = 5f;
        const float RingThickness = 2f;
        const float DotRadius = 2.5f;
        const float DimAlpha = 0.3f;
        const float TrackAlpha = 0.22f;
        const float RimAlpha = 0.9f;

        // The eyepiece's DIAMETER as a fraction of SCREEN HEIGHT, not a pixel size: this canvas
        // has no CanvasScaler (every other number in it is a real screen measurement), so a fixed
        // window is a quarter of a phone screen and a postage stamp on a monitor.
        const float WindowHeightFraction = 0.5f;

        // Clearance for the goal stack, which anchors at (16, -52) and runs up to three
        // 48-unit rows (Tools/Build/author_goal_stack.py). Sized for the full three so a mode
        // that authors secondary goals cannot land one behind this window. Plus the recharge
        // ring, which stands outside the rim.
        const float WindowTopMargin = 220f;
        const float WindowLeftMargin = 16f;

        Canvas _canvas;
        RectTransform _window;
        ScopeDiscGraphic _backing;
        ScopeDiscGraphic _disc;
        ScopeRingGraphic _rim;
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
            // Hidden by the WINDOW, never by the canvas - see SetVisible.
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

            var windowGo = new GameObject("ScopeWindow", typeof(RectTransform));
            windowGo.transform.SetParent(transform, false);
            _window = windowGo.GetComponent<RectTransform>();
            // TOP-LEFT, under the goal stack. Its PIVOT is the eyepiece's CENTRE, so every ring
            // below is centred at zero in its frame and the whole instrument moves as one.
            _window.anchorMin = new Vector2(0f, 1f);
            _window.anchorMax = new Vector2(0f, 1f);
            _window.pivot = new Vector2(0.5f, 0.5f);
            _window.sizeDelta = Vector2.zero;

            // The BACKING is drawn first, is opaque, and is on from the moment the scope is
            // raised - so the eyepiece is an OBJECT on screen even before the first render lands
            // and even when what it is pointed at is empty space. A window showing nothing and no
            // window at all must not look the same, which is exactly how round 4's flat picture
            // was reported: "i no longer saw the pip".
            _backing = MakeDisc("Backing");
            _backing.color = new Color(0.02f, 0.03f, 0.05f, 0.92f);

            _disc = MakeDisc("Picture");
            _disc.color = Color.white;   // the picture is the colour; this is a tint, not a wash
            _disc.enabled = false;

            // Order matters: rim, then the recharge bed, then the arc, then the reticle on top -
            // UGUI draws siblings in order and the reticle is the thing being aimed with.
            _rim = MakeRing("Rim", 120f, RimThickness);
            _track = MakeRing("ChargeTrack", 130f, ArcThickness);
            _arc = MakeRing("ChargeArc", 130f, ArcThickness);
            _ring = MakeRing("Reticle", 40f, RingThickness);
            _dot = MakeRing("ReticleDot", DotRadius, DotRadius * 2f);

            _pip = new ScopePipView(_disc);
        }

        /// <summary>
        /// A disc filling the window's rect. Two of them: the opaque backing and the picture on
        /// top of it. They are CHILDREN rather than a graphic on the window itself because UGUI
        /// draws a parent before its children, which gives exactly one slot in the order - and the
        /// eyepiece needs two before the rings.
        /// </summary>
        ScopeDiscGraphic MakeDisc(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_window, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var disc = go.AddComponent<ScopeDiscGraphic>();
            disc.raycastTarget = false;
            return disc;
        }

        ScopeRingGraphic MakeRing(string name, float radius, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_window, false);
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

            float windowRadius = LayOutWindow();
            float radius = ReticleRadiusPixels(coneHalfAngleDegrees, fieldOfView, windowRadius);
            bool ready = cooldown01 <= 0.0001f;

            // The instant it comes back: a one-shot flash, so a pilot watching the target rather
            // than the arc still sees the weapon arrive.
            if (ready && !_wasReady) _readyFlashUntil = Time.unscaledTime + ReadyFlashSeconds;
            _wasReady = ready;

            float flash01 = Mathf.Clamp01((_readyFlashUntil - Time.unscaledTime) / ReadyFlashSeconds);

            _rim.Radius = windowRadius;
            _rim.Thickness = RimThickness;
            _rim.Sweep01 = 1f;
            _rim.color = WithAlpha(colour, RimAlpha);

            _ring.Radius = radius;
            _ring.Thickness = RingThickness;
            _ring.Sweep01 = 1f;
            _ring.color = WithAlpha(colour, ready ? 1f : DimAlpha);

            _dot.Radius = DotRadius;
            _dot.Thickness = DotRadius * 2f;
            _dot.Sweep01 = 1f;
            _dot.color = WithAlpha(colour, ready ? 1f : DimAlpha * 0.7f);

            float arcRadius = windowRadius + ArcGapPixels;

            // The BED: always drawn, always a full ring, so "recharging" reads as a ring
            // FILLING rather than as one appearing out of nowhere - and so the readout is
            // present on screen at 0%, which is the frame the pilot most wants it. Same rule the
            // goal stack's progress bar records (Docs/GAME_MODE_TOPBAR.md): a progress bar needs
            // a bed.
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

            SelfCheck(windowRadius);
        }

        /// <summary>
        /// Size and place the eyepiece against the LIVE screen, every frame, and hand back its
        /// radius. It is a fraction of screen height rather than an authored pixel size because
        /// this canvas deliberately has no <c>CanvasScaler</c> - every other number in it is a
        /// real screen measurement - so a fixed rect would be a different fraction of the display
        /// on every device and would not follow a resize.
        /// </summary>
        float LayOutWindow()
        {
            float diameter = Mathf.Max(120f, Screen.height * WindowHeightFraction);
            float radius = diameter * 0.5f;

            // The rect is the window's own square; its pivot is the centre, so the anchored
            // position is the centre too and the margins have to clear the radius plus whatever
            // the recharge ring stands outside it by.
            float outer = radius + ArcGapPixels + ArcThickness;
            _window.sizeDelta = new Vector2(diameter, diameter);
            _window.anchoredPosition = new Vector2(WindowLeftMargin + outer,
                                                   -(WindowTopMargin + outer));

            _backing.Radius = radius;
            _disc.Radius = radius;
            return radius;
        }

        /// <summary>
        /// The cone's half-angle in pixels INSIDE the eyepiece: the same projection the screen
        /// version used, with the window's radius standing in for half the screen height and the
        /// window's own field of view standing in for the camera's.
        /// </summary>
        static float ReticleRadiusPixels(float coneHalfAngleDegrees, float fieldOfView,
                                         float windowRadius)
        {
            float halfFov = Mathf.Clamp(fieldOfView * 0.5f, 0.5f, 89f) * Mathf.Deg2Rad;
            float half = Mathf.Max(0.01f, coneHalfAngleDegrees) * Mathf.Deg2Rad;

            float pixels = windowRadius * Mathf.Tan(half) / Mathf.Tan(halfFov);
            // Floored so the ring is still a ring at the wide end of the dial, and capped so a
            // badly-authored cone cannot draw a reticle that fills the eyepiece.
            return Mathf.Clamp(pixels, 6f, windowRadius * 0.8f);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public void Hide()
        {
            SetVisible(false);
            _pip?.Hide();
        }

        /// <summary>
        /// Show or hide the instrument by toggling the WINDOW GameObject, and never by toggling
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
            if (_window != null) _window.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Check ONCE, a couple of frames in, that the instrument this class just drew can
        /// actually be seen — and warn if not.
        ///
        /// <para>It exists because everything below is silent when it fails. A disabled canvas, a
        /// graphic with no cached canvas, a zero radius, a transparent backing and a window parked
        /// off the edge of the screen all produce the identical report from a pilot: nothing is
        /// there. None of them throws, and none of them is visible in a prefab or a scene, because
        /// the whole instrument is generated at runtime.</para>
        ///
        /// <para>It runs on the second tick rather than the first so the canvas update pass has
        /// had a frame to build the meshes, and it never runs again: these are construction facts,
        /// not per-frame ones, so a repeat check would be per-frame work in exchange for nothing.
        /// If it passes, the one line it emits goes on a channel — the fault cases are the loud
        /// ones.</para>
        /// </summary>
        void SelfCheck(float radius)
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
            if (_rim != null && !_rim.IsActive())
            {
                SniperScopeDiagnostics.Unusable(
                    "its graphics report IsActive() false with the window up, so Graphic.m_Canvas " +
                    "is stale and every mesh rebuild is a silent no-op - the rings are frozen at " +
                    "whatever size they were built with. Something is toggling Canvas.enabled " +
                    "under them; see SniperScopeOverlay.SetVisible.");
                return;
            }

            if (radius <= 1f)
            {
                SniperScopeDiagnostics.Unusable(
                    $"its eyepiece radius resolved to {radius:0.##} px. Screen.height reads " +
                    $"{Screen.height}.");
                return;
            }

            if (_backing != null && _backing.color.a <= 0.01f)
            {
                SniperScopeDiagnostics.Unusable("its backing disc is fully transparent.");
                return;
            }

            // The window's pivot is the eyepiece's CENTRE, so this is the centre in screen
            // coordinates measured from the bottom-left - which is what the reporter tool prints.
            Vector2 centre = new(_window.anchoredPosition.x,
                                 Screen.height + _window.anchoredPosition.y);
            if (centre.x + radius < 0f || centre.x - radius > Screen.width ||
                centre.y + radius < 0f || centre.y - radius > Screen.height)
            {
                SniperScopeDiagnostics.Unusable(
                    $"its eyepiece is off screen: centre {centre}, radius {radius:0.#}, screen " +
                    $"{Screen.width}x{Screen.height}.");
                return;
            }

            SniperScopeDiagnostics.Drawing(centre, radius, new Vector2(Screen.width, Screen.height));
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
