using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent scope's on-screen readout: a <b>reticle drawn at the sniper cone's true angular
    /// size</b>, a <b>recharge arc</b> around it, and the flight <b>PIP</b>.
    ///
    /// <para><b>Why this exists rather than the fleet's ability lockup.</b> The lockup's clockwise
    /// cooldown veil is the platform's answer to "is this ability ready", and it is drawn ON an
    /// ability ICON — but the Serpent binds 0 of its 4 ability icons
    /// (<c>Docs/ElementalAbilitySystem/FLEET_MAPS.md</c>), so its lockup renders four LOCKED cards
    /// and the veil has nothing to sit on. The push into it is correct and invisible, which is
    /// exactly the "the UI did not indicate if a shot was ready" report. The right long-term fix is
    /// to author that vessel's icons; this readout is what makes the weapon usable now, and it says
    /// something the row could not anyway — WHERE the shot goes.</para>
    ///
    /// <para><b>The reticle is a MEASUREMENT, not decoration.</b> Its radius is the cone's own
    /// half-angle projected through the camera's LIVE vertical field of view
    /// (<c>r = (h/2) · tan(halfAngle) / tan(fov/2)</c>), read every frame off the camera that is
    /// actually rendering — so it tracks the zoom, and it tracks the speed tunnel narrowing the
    /// view at speed. Anything inside the ring is inside the shot. An authored reticle sprite
    /// would be a claim about the weapon that stops being true the first time either number
    /// moves.</para>
    ///
    /// <para><b>Generated, not authored.</b> One runtime canvas, three
    /// <see cref="ScopeRingGraphic"/>s and a <c>RawImage</c>; no sprites, no prefab, no per-vessel
    /// wiring. It is built on first use by <see cref="SniperScopeActionExecutor"/> for the LOCAL
    /// PILOT only and torn down with the vessel.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SniperScopeOverlay : MonoBehaviour
    {
        const float ReadyFlashSeconds = 0.35f;
        const float ArcGapPixels = 18f;
        const float ArcThickness = 3f;
        const float RingThickness = 2f;
        const float DotRadius = 2.5f;
        const float DimAlpha = 0.3f;

        Canvas _canvas;
        ScopeRingGraphic _ring;
        ScopeRingGraphic _dot;
        ScopeRingGraphic _arc;
        RawImage _pipSurface;
        ScopePipView _pip;

        bool _wasReady = true;
        float _readyFlashUntil;

        /// <summary>
        /// Build the overlay on a new GameObject. Local pilot only — the caller owns that gate,
        /// because a camera and a screen are things one machine has.
        /// </summary>
        public static SniperScopeOverlay Create()
        {
            var go = new GameObject("[SerpentScopeOverlay]");
            var overlay = go.AddComponent<SniperScopeOverlay>();
            overlay.Build();
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
            // derived from the camera's field of view, and a reference-resolution scaler would
            // silently re-scale a measurement into a decoration.
            // Deliberately NO GraphicRaycaster either - a hit target over the middle of the screen
            // would eat presses meant for the world.

            _ring = MakeRing("Reticle", 40f, RingThickness);
            _dot = MakeRing("ReticleDot", DotRadius, DotRadius * 2f);
            _arc = MakeRing("ChargeArc", 58f, ArcThickness);

            var pipGo = new GameObject("Pip", typeof(RectTransform));
            pipGo.transform.SetParent(transform, false);
            var pipRect = pipGo.GetComponent<RectTransform>();
            // Bottom-right, out of the reticle's way and out of the goal stack's (top-left).
            pipRect.anchorMin = new Vector2(1f, 0f);
            pipRect.anchorMax = new Vector2(1f, 0f);
            pipRect.pivot = new Vector2(1f, 0f);
            pipRect.anchoredPosition = new Vector2(-24f, 24f);
            pipRect.sizeDelta = new Vector2(320f, 180f);
            _pipSurface = pipGo.AddComponent<RawImage>();
            _pipSurface.raycastTarget = false;
            _pipSurface.enabled = false;

            _pip = new ScopePipView(_pipSurface);
        }

        ScopeRingGraphic MakeRing(string name, float radius, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
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
        /// Drive one frame of the readout. <paramref name="cooldown01"/> is what is LEFT of the
        /// recharge (0 = ready), matching <c>SniperShotActionExecutor.CooldownRemaining01</c> and
        /// the fleet's own veil sense.
        /// </summary>
        public void Tick(float coneHalfAngleDegrees, float cooldown01, Color colour)
        {
            SetVisible(true);

            float radius = ResolveReticleRadiusPixels(coneHalfAngleDegrees);
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

            _arc.Radius = radius + ArcGapPixels;
            _arc.Thickness = ArcThickness;
            if (ready)
            {
                // Once ready the arc's job is done; it only reappears as the flash, expanding and
                // fading, so "ready" is an event and not another permanent ring to read.
                _arc.Sweep01 = 1f;
                _arc.Radius = radius + ArcGapPixels + (1f - flash01) * 10f;
                _arc.color = WithAlpha(colour, flash01 * 0.9f);
            }
            else
            {
                _arc.Sweep01 = 1f - cooldown01;
                _arc.color = WithAlpha(colour, 0.85f);
            }

            _pip.Tick();
        }

        /// <summary>
        /// The cone's half-angle in SCREEN PIXELS for the camera that is actually rendering. The
        /// live camera rather than an authored field of view, because the speed tunnel narrows it
        /// continuously with speed and the reticle has to mean the same thing at every speed.
        /// </summary>
        static float ResolveReticleRadiusPixels(float coneHalfAngleDegrees)
        {
            var controller = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            var cam = controller != null ? controller.Camera : Camera.main;

            float fov = cam != null && !cam.orthographic ? cam.fieldOfView : 60f;
            float halfFov = Mathf.Max(0.5f, fov * 0.5f) * Mathf.Deg2Rad;
            float half = Mathf.Max(0.01f, coneHalfAngleDegrees) * Mathf.Deg2Rad;

            float pixels = Screen.height * 0.5f * Mathf.Tan(half) / Mathf.Tan(halfFov);
            // Floored so the ring is still a ring on a phone at a wide field of view, and capped so
            // a badly-authored cone cannot draw a reticle the size of the screen.
            return Mathf.Clamp(pixels, 10f, Screen.height * 0.4f);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        public void Hide()
        {
            SetVisible(false);
            _pip?.Hide();
        }

        void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.enabled = visible;
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
