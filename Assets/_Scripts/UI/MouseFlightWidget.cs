using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The on-screen virtual stick for the desktop one-thumb mouse scheme
    /// (<see cref="SingleStickMouseInputStrategy"/>) — a centred reticle showing where the mouse
    /// has pushed the stick, with the HOLD ANNULUS drawn as a ring around the rim.
    ///
    /// <para><b>It is not decoration.</b> A bounded-cursor scheme has two regimes that fly
    /// completely differently — inside the annulus the spring is dead and the turn holds itself,
    /// outside it the spring is pulling you back — and nothing else on screen distinguishes them.
    /// Without the widget a player cannot tell a locked turn from a decaying one, which is the
    /// specific reason every shipped mouse-flight game (Freelancer, Elite's mouse widget, War
    /// Thunder) draws its stick. It is also the answer to the scheme's older failure mode: the
    /// mouse path can be silently disengaged (<see cref="MouseFlightDiagnostics"/>) while the
    /// vessel still flies on WASD, and a visible stick makes "am I on the mouse?" a glance.</para>
    ///
    /// <para><b>Generated, never sprited</b> — the same call as <c>TrapezoidGraphic</c>: the ring
    /// radii are live functions of the config's hold band, so art would have to be re-exported
    /// every time a dial moved. Everything is one mesh, so the whole widget is one draw call, and
    /// antialiasing is baked in as zero-alpha feather rings because a canvas gives a generated
    /// circle none.</para>
    ///
    /// <para><b>It self-installs and needs no scene wiring</b> (the <c>VesselSpeedTunnel</c> /
    /// <c>PrismOcclusionCorridor</c> precedent): the strategy calls <see cref="Report"/> each
    /// frame it owns the input, and the widget appears in whatever scene that happens in. It
    /// auto-hides when the reports stop, so every way of losing the scheme — pause, alt-tab, a
    /// vessel swap onto a two-stick hull, a scene load — puts it away with nothing to remember to
    /// call. It draws nothing at all when the player has turned joystick visuals off in settings,
    /// since that is exactly the setting this is.</para>
    /// </summary>
    [AddComponentMenu("")]   // self-installed; never authored onto a prefab
    public sealed class MouseFlightWidget : MaskableGraphic
    {
        // ------------------------------------------------------------------
        // Look. Alphas are fractions of the config's widget colour, which is neutral by default —
        // domain colour means TEAM everywhere else in the game (Docs/PALETTE.md), so an
        // instrument that wears one is making a claim it does not mean.

        const int Segments = 48;
        const float Feather = 1.1f;          // px of baked antialiasing on every generated edge

        const float RimThickness = 1.6f;
        const float RimAlpha = 0.30f;
        const float RimAlphaHeld = 0.85f;

        const float BandAlpha = 0.07f;       // the annulus, idle
        const float BandAlphaHeld = 0.20f;   // the annulus, parked in

        const float DeadZoneThickness = 1.1f;
        const float DeadZoneAlpha = 0.22f;
        const float DeadZoneMinPixels = 3.5f;

        const float NeedleWidth = 1.6f;
        const float NeedleAlpha = 0.30f;

        const float KnobRadius = 4.5f;
        const float KnobRadiusHeld = 6.0f;
        const float KnobAlpha = 0.95f;

        const float FadeInPerSecond = 8f;
        const float FadeOutPerSecond = 5f;

        /// <summary>Frames of silence tolerated before fading out. One is not enough: a frame in
        /// which the strategy is skipped (a paused input controller, a hitch) would flicker it.
        /// </summary>
        const int SilentFramesBeforeHiding = 2;

        // ------------------------------------------------------------------
        // Static surface

        static MouseFlightWidget s_instance;
        static bool s_installFailed;

        /// <summary>
        /// Show the widget at this stick position for this frame. Called every frame the mouse
        /// scheme owns the input; stopping is how it goes away.
        /// </summary>
        public static void Report(Vector2 stick, MouseFlightConfigSO config)
        {
            // showWidget is this widget's ONLY switch. It deliberately does NOT read
            // GameSetting.JoystickVisualsEnabled: that setting governs the TOUCH thumb rings
            // (ThumbPerimeter), and honouring it here made the mouse reticle disappear for a
            // reason a desktop player has no way to connect to it - a control they never touched,
            // on a device they are not using. A setting's SCOPE is part of its meaning.
            if (config == null || !config.ShowWidget) { Hide(); return; }

            var widget = Ensure();
            if (widget == null) return;

            widget.stick = stick;
            widget.holdOuter = config.HoldOuterRadius;
            widget.deadZone = config.DeadZone;
            widget.screenFraction = config.WidgetScreenFraction;
            widget.tint = config.WidgetColor;
            widget.lastReportFrame = Time.frameCount;
        }

        /// <summary>Put the widget away now rather than waiting for the reports to lapse — the
        /// strategy's explicit teardown (deactivate, pause), where the player has just been handed
        /// their cursor back and a lingering reticle would read as the scheme still flying.
        /// </summary>
        public static void Hide()
        {
            if (s_instance != null) s_instance.fadeTarget = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_installFailed = false;
        }

        /// <summary>
        /// Build the canvas and the graphic.
        ///
        /// <para><b>This body is the one that demonstrably worked, restored verbatim.</b> A later
        /// pass "improved" it — dropping the explicit <c>CanvasRenderer</c> to rely on
        /// <c>[RequireComponent]</c>, and moving the graphic from the GameObject constructor to an
        /// <c>AddComponent</c> after parenting, on the theory that a <c>Graphic</c> should not
        /// wake up parentless. The theory was reasonable and the widget stopped appearing.
        /// <i>Empirical known-good beats a tidier construction order every time; when something
        /// works and you cannot run it, do not refactor it for elegance.</i></para>
        /// </summary>
        static MouseFlightWidget Ensure()
        {
            if (s_instance != null) return s_instance;
            if (s_installFailed) return null;

            GameObject root = null;
            try
            {
                // HideInHierarchy, NOT HideAndDontSave - that exempts the object from
                // play-mode-exit cleanup and the widget would outlive the session that made it.
                // Same pattern as VesselSpeedTunnel's and PrismOcclusionCorridor's drivers.
                root = new GameObject("[MouseFlightWidget]", typeof(RectTransform), typeof(Canvas))
                {
                    hideFlags = HideFlags.HideInHierarchy
                };
                DontDestroyOnLoad(root);

                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Above the world and the vessel HUD, below the modal/pause layer - this is an
                // instrument, not a dialog, and it must never be the thing on top of a menu.
                canvas.sortingOrder = 50;

                // CanvasRenderer is named EXPLICITLY. Graphic declares it via [RequireComponent],
                // but leaning on that was exactly the change that made this stop drawing - and it
                // costs one word to not depend on it.
                var child = new GameObject("Stick",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(MouseFlightWidget))
                {
                    hideFlags = HideFlags.HideInHierarchy
                };
                child.transform.SetParent(root.transform, false);

                var widget = child.GetComponent<MouseFlightWidget>();
                if (widget == null)
                {
                    s_installFailed = true;
                    Destroy(root);
                    Debug.LogError("[MouseFlightWidget] The graphic did not come up on its own " +
                                   "GameObject; flight is unaffected.");
                    return null;
                }

                var rt = widget.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                widget.raycastTarget = false;

                s_instance = widget;
                widget.VerifyInstall();
                return s_instance;
            }
            catch (System.Exception e)
            {
                // The caller isolates this too, but retiring the installer here means a broken
                // environment costs one log line rather than one per frame forever.
                s_installFailed = true;
                if (root != null) Destroy(root);
                Debug.LogError($"[MouseFlightWidget] Could not install; flight is unaffected.\n{e}");
                return null;
            }
        }

        /// <summary>
        /// Say once, at install, if any of the four things a UGUI graphic needs to put pixels on
        /// screen is missing. The widget's failure mode is that it is simply absent, which looks
        /// identical to "the scheme is not running" - and this widget exists precisely so that
        /// question has a visible answer, so it cannot be allowed to fail silently itself.
        /// </summary>
        void VerifyInstall()
        {
            string missing = null;
            if (canvasRenderer == null) missing = "no CanvasRenderer";
            else if (canvas == null) missing = "no Canvas ancestor";
            else if (!isActiveAndEnabled) missing = "the graphic is disabled";

            if (missing != null)
                Debug.LogWarning($"[MouseFlightWidget] Installed but cannot draw: {missing}. " +
                                 "Flight is unaffected; the mouse stick just has no reticle.");
        }

        // ------------------------------------------------------------------
        // State

        Vector2 stick;
        float holdOuter = 0.9f;
        float deadZone = 0.04f;
        float screenFraction = 0.1f;
        Color tint = Color.white;
        int lastReportFrame = -1;

        float fade;
        float fadeTarget;

        Vector2 drawnStick;
        float drawnFade = -1f;
        float radiusPixels;

        /// <summary>
        /// Where the fade is heading, given how long it has been since the scheme last reported.
        ///
        /// <para>Pulled out as pure math for the reason <c>MouseVirtualStick</c> was: the first
        /// version raised the target on a report and <b>never lowered it</b> when the reports
        /// stopped, so the reticle stayed on screen after the scheme handed over — through the app
        /// shell, through a vessel swap, through anything that did not happen to call
        /// <see cref="Hide"/> explicitly. <i>An auto-hide that only ever auto-shows is not one</i>,
        /// and a one-way branch reads perfectly well until you ask it the other question.</para>
        /// </summary>
        public static float ResolveFadeTarget(int framesSinceReport, int silentFramesBeforeHiding)
            => framesSinceReport <= silentFramesBeforeHiding ? 1f : 0f;

        /// <summary>Frames of silence tolerated before fading out, exposed so the contract can be
        /// asserted against the shipped number rather than a copy of it.</summary>
        public static int SilentFrameTolerance => SilentFramesBeforeHiding;

        void LateUpdate()
        {
            fadeTarget = ResolveFadeTarget(Time.frameCount - lastReportFrame,
                                           SilentFramesBeforeHiding);

            float rate = fadeTarget > fade ? FadeInPerSecond : FadeOutPerSecond;
            fade = Mathf.MoveTowards(fade, fadeTarget, rate * Time.unscaledDeltaTime);

            if (fade <= 0f)
            {
                // Emit an empty mesh rather than disabling the component: LateUpdate does not run
                // on a disabled MonoBehaviour, so switching it off here is a one-way door the
                // widget can never come back through.
                if (drawnFade != 0f) SetVerticesDirty();
                return;
            }

            float r = Mathf.Min(Screen.width, Screen.height) * screenFraction;
            if (!Mathf.Approximately(r, radiusPixels))
            {
                radiusPixels = r;
                rectTransform.sizeDelta = new Vector2(r * 2f, r * 2f);
                SetVerticesDirty();
            }

            // Rebuilding a ~700-vert mesh every frame would be affordable and pointless; the
            // widget is static whenever the stick is.
            if ((stick - drawnStick).sqrMagnitude > 1e-6f
                || Mathf.Abs(fade - drawnFade) > 1e-3f)
                SetVerticesDirty();
        }

        // ------------------------------------------------------------------
        // Mesh

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            drawnStick = stick;
            drawnFade = fade;

            float r = radiusPixels;
            if (r <= 1f || fade <= 0f) return;

            float magnitude = Mathf.Min(stick.magnitude, 1f);
            // Held is a fact about the STATE, not about the report: the dead zone hides a
            // residual near centre, and nothing hides a parked stick.
            float held = holdOuter < 1f
                ? Mathf.InverseLerp(holdOuter - 0.06f, holdOuter, magnitude)
                : 0f;

            // The annulus, drawn as a filled band so "out here it holds" is a place on screen.
            if (holdOuter < 1f)
            {
                float bandInner = holdOuter * r;
                float bandMid = (bandInner + r) * 0.5f;
                AddRing(vh, bandMid, r - bandInner,
                        Alpha(Mathf.Lerp(BandAlpha, BandAlphaHeld, held)));
            }

            AddRing(vh, r, RimThickness, Alpha(Mathf.Lerp(RimAlpha, RimAlphaHeld, held)));
            AddRing(vh, Mathf.Max(deadZone * r, DeadZoneMinPixels), DeadZoneThickness,
                    Alpha(DeadZoneAlpha));

            Vector2 knob = stick * r;
            if (knob.sqrMagnitude > 1e-4f)
                AddNeedle(vh, knob, NeedleWidth, Alpha(NeedleAlpha));

            AddDisc(vh, knob, Mathf.Lerp(KnobRadius, KnobRadiusHeld, held), Alpha(KnobAlpha));
        }

        Color32 Alpha(float a)
        {
            var c = tint;
            c.a *= a * fade;
            return c;
        }

        /// <summary>
        /// A ring, emitted as three concentric strips across its thickness — a zero-alpha feather
        /// outside, the solid core, a zero-alpha feather inside. The bilinear interpolation
        /// between those vertex alphas IS the antialiasing, at no texture cost; a canvas does no
        /// MSAA and a 1.6 px generated circle is pure stair-steps without it.
        /// </summary>
        void AddRing(VertexHelper vh, float radius, float thickness, Color32 c)
        {
            float half = Mathf.Max(thickness, 0.1f) * 0.5f;
            var clear = c; clear.a = 0;

            float r0 = Mathf.Max(0f, radius - half - Feather);
            float r1 = Mathf.Max(0f, radius - half);
            float r2 = radius + half;
            float r3 = radius + half + Feather;

            int start = vh.currentVertCount;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                vh.AddVert(d * r0, clear, Vector2.zero);
                vh.AddVert(d * r1, c, Vector2.zero);
                vh.AddVert(d * r2, c, Vector2.zero);
                vh.AddVert(d * r3, clear, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + i * 4;
                int b = a + 4;
                for (int k = 0; k < 3; k++)
                {
                    vh.AddTriangle(a + k, a + k + 1, b + k + 1);
                    vh.AddTriangle(b + k + 1, b + k, a + k);
                }
            }
        }

        /// <summary>A filled disc with a feathered rim — the knob.</summary>
        void AddDisc(VertexHelper vh, Vector2 centre, float radius, Color32 c)
        {
            var clear = c; clear.a = 0;

            int start = vh.currentVertCount;
            vh.AddVert(centre, c, Vector2.zero);
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments * Mathf.PI * 2f;
                var d = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                vh.AddVert(centre + d * radius, c, Vector2.zero);
                vh.AddVert(centre + d * (radius + Feather), clear, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                int a = start + 1 + i * 2;
                int b = a + 2;
                vh.AddTriangle(start, a, b);          // fan
                vh.AddTriangle(a, a + 1, b + 1);      // feather
                vh.AddTriangle(b + 1, b, a);
            }
        }

        /// <summary>
        /// The line from centre to knob, fading out toward the centre so it reads as a direction
        /// the stick is pushed rather than as a second ring of chrome.
        /// </summary>
        void AddNeedle(VertexHelper vh, Vector2 knob, float width, Color32 c)
        {
            var clear = c; clear.a = 0;
            Vector2 dir = knob.normalized;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float half = width * 0.5f;

            int start = vh.currentVertCount;
            // Three strips across the width: feather, core, feather — same reason as AddRing.
            float[] offsets = { -(half + Feather), -half, half, half + Feather };
            for (int k = 0; k < 4; k++)
            {
                bool edge = k == 0 || k == 3;
                Vector2 o = side * offsets[k];
                vh.AddVert(o, clear, Vector2.zero);                       // centre end: invisible
                vh.AddVert(knob + o, edge ? clear : c, Vector2.zero);     // knob end
            }

            for (int k = 0; k < 3; k++)
            {
                int a = start + k * 2;
                int b = a + 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(b + 1, b, a);
            }
        }
    }
}
