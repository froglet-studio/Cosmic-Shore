using CosmicShore.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The Serpent scope's <b>window</b>: a magnified view down the vessel's own nose, drawn into
    /// a round eyepiece in the corner of the screen while the pilot keeps flying with their
    /// ordinary chase view.
    ///
    /// <para><b>The inversion is the whole design.</b> The first cut did the obvious thing — take
    /// the gameplay camera into the cockpit and narrow its field of view — and it read as
    /// nauseating, because a magnified view is a lever on every motion that reaches it: a 22°
    /// scope multiplies the pilot's own turn, the vessel's roll and the camera's own settle by the
    /// same ~4× it multiplies the target. Putting the magnification in a WINDOW leaves the flight
    /// view at 1× where the pilot reads their motion, and confines the lever to the picture they
    /// are aiming with. <c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4.</para>
    ///
    /// <para><b>It is NOT a second gameplay camera</b>, which the platform forbids for four
    /// concrete reasons (Docs/REAR_VIEW.md §3): the speed tunnel resolves <c>CameraManager</c>'s
    /// ACTIVE controller, <c>ApplyCameraGraphicsSettings</c> pushes the player's FOV and AA onto
    /// three managed cameras and no others, background colour is applied per camera, and
    /// <c>Camera.main</c> returns the first ENABLED camera tagged MainCamera. This camera is
    /// created at runtime, is <b>never tagged MainCamera</b>, renders only into a
    /// <c>RenderTexture</c> and is left <b>disabled</b> — it is stepped by hand — so it is outside
    /// all four systems by construction and cannot draw to the display at all. It is the same
    /// shape the connecting panel's arena preview uses (<c>ConnectingArenaPreview</c>), and §3.1.1
    /// is the carve-out it was written for. <b>Because it is outside the speed tunnel, the scope's
    /// magnification is a function of the trigger alone</b> — the thing the pilot asked for.</para>
    ///
    /// <para><b>The cost is real and is stated rather than hidden.</b> Unlike that preview — which
    /// stands the gameplay camera DOWN because the panel covers the screen — this one is a genuine
    /// SECOND render of the world, because the first one is what the player is flying with. So it
    /// is paid for the only way left: a small square render target, no post-processing, no
    /// shadows, no anti-aliasing, a capped refresh rate, and a lifetime of exactly as long as the
    /// trigger is held. It is off the moment the scope drops.</para>
    ///
    /// <para><b>The eye is MEASURED, not authored</b> — a multiple of the vessel's own
    /// circumscribing hull radius (<see cref="PrismOcclusionCorridor.MeasureCircumscribedRadius"/>,
    /// the same rotation-invariant hull-only measurement the occlusion corridor sizes itself
    /// from), so the camera sits just clear of its own geometry on a hull of any size rather than
    /// at a constant that is inside one ship and far ahead of another. It aims along the vessel's
    /// own forward, which is <b>the same vector the sniper's cone is cast along</b>
    /// (<c>SniperShotActionExecutor.ResolveShot</c>) — so the shot lands where the reticle is by
    /// construction rather than by tuning.</para>
    ///
    /// <para><b>The picture is SQUARE</b>, because the window is round: the render target is 1:1
    /// and the camera therefore renders at aspect 1, so <see cref="ScopeDiscGraphic"/> can sample
    /// the unit circle straight onto [0,1]² with nothing squashed and nothing thrown away.</para>
    /// </summary>
    public sealed class ScopePipView
    {
        readonly ScopeDiscGraphic _surface;

        Camera _camera;
        RenderTexture _texture;
        Transform _measuredHull;
        float _hullRadius;
        float _nextRenderTime;

        /// <summary>
        /// How far past the vessel's own circumscribing hull radius the eye sits, so the scope is
        /// never looking at the inside of its own ship. A multiple rather than a distance: see the
        /// class summary on why the eye is measured.
        /// </summary>
        const float EyeForwardHullRadii = 1.05f;

        /// <summary>
        /// Render size in pixels, square. Small on purpose — see the class note on cost — but it
        /// has to track the WINDOW: an eyepiece that is a quarter of the screen tall and is fed a
        /// 216px texture is visibly soft, and a magnified picture that cannot be read is the same
        /// as no picture.
        /// </summary>
        public int RenderSize = 512;

        /// <summary>How often the window refreshes, in Hz. Capped for the same reason.</summary>
        public float RefreshHz = 30f;

        public ScopePipView(ScopeDiscGraphic surface) => _surface = surface;

        public bool Visible => _surface != null && _surface.enabled;

        /// <summary>
        /// Show the window and advance it, at most <see cref="RefreshHz"/> times a second. Safe to
        /// call every frame.
        /// </summary>
        /// <param name="vessel">The local pilot's hull. The eye rides just past its nose.</param>
        /// <param name="fieldOfView">The magnified vertical field of view, in degrees. A pure
        /// function of the pilot's trigger depth — see the class summary.</param>
        public void Tick(Transform vessel, float fieldOfView)
        {
            if (_surface == null) return;
            if (vessel == null) { Hide(); return; }
            if (!EnsureCamera()) { Hide(); return; }

            _surface.Source = _texture;
            _surface.enabled = true;

            PoseCamera(vessel, fieldOfView);

            // Stepped by hand rather than left enabled: an enabled camera renders every frame, and
            // this one is drawing the whole arena a second time.
            if (Time.unscaledTime < _nextRenderTime) return;
            _nextRenderTime = Time.unscaledTime + 1f / Mathf.Max(1f, RefreshHz);
            _camera.Render();
        }

        public void Hide()
        {
            if (_surface != null) _surface.enabled = false;
        }

        /// <summary>
        /// Tear the window down completely. Called when the vessel goes away, not merely when the
        /// trigger is released — a RenderTexture that outlives its owner is a leak nothing reports.
        /// </summary>
        public void Dispose()
        {
            Hide();
            if (_camera != null)
            {
                _camera.targetTexture = null;
                Object.Destroy(_camera.gameObject);
                _camera = null;
            }
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }
            _measuredHull = null;
            _hullRadius = 0f;
        }

        void PoseCamera(Transform vessel, float fieldOfView)
        {
            // Measured once per hull, not per frame: it is a property of the ship, and the
            // measurement walks every renderer on it.
            if (_measuredHull != vessel)
            {
                _measuredHull = vessel;
                _hullRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(vessel);
            }

            // A RIGID attachment, and both halves are load-bearing. The eye is written outright
            // every frame — any lag at all at zero distance puts the camera inside the hull it is
            // trying to see past — and it aims along the vessel's OWN forward rather than at
            // anything, because in the cockpit a look-at vector is very nearly zero.
            _camera.transform.SetPositionAndRotation(
                vessel.position + vessel.forward * (_hullRadius * EyeForwardHullRadii),
                vessel.rotation);

            _camera.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 170f);
        }

        bool EnsureCamera()
        {
            if (_camera != null && _texture != null) return true;

            if (_texture == null)
            {
                int size = Mathf.Clamp(RenderSize, 128, 1024);
                _texture = new RenderTexture(size, size, 16)
                {
                    name = "SerpentScopeRT",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false,
                };
            }

            if (_camera == null)
            {
                var go = new GameObject("[SerpentScopeCamera]");
                // Deliberately NOT tagged MainCamera, and deliberately left disabled.
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;

                var source = Camera.main;
                if (source != null)
                {
                    _camera.clearFlags = source.clearFlags;
                    _camera.backgroundColor = source.backgroundColor;
                    _camera.cullingMask = source.cullingMask;
                    _camera.allowHDR = source.allowHDR;
                    // The clip planes ARE borrowed here, unlike the connecting panel's preview
                    // which derives its own: that one frames a whole arena from outside and clips
                    // out of a borrowed far plane, while this camera sits on the vessel looking
                    // down the same line the gameplay camera already renders. Its planes are
                    // correct for this shot by construction.
                    _camera.nearClipPlane = source.nearClipPlane;
                    _camera.farClipPlane = source.farClipPlane;
                }
                else
                {
                    _camera.clearFlags = CameraClearFlags.Skybox;
                    _camera.cullingMask = ~0;
                    _camera.nearClipPlane = 0.3f;
                    _camera.farClipPlane = 20000f;
                }

                // Everything except UI: a window that rendered the canvas would draw itself inside
                // itself, one frame stale, forever.
                int ui = LayerMask.NameToLayer("UI");
                if (ui >= 0) _camera.cullingMask &= ~(1 << ui);

                _camera.allowMSAA = false;

                var data = _camera.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    // The three expensive passes are OFF here, unlike the connecting panel's
                    // preview, because that one replaces the gameplay render and this one is
                    // ADDED to it.
                    data.renderPostProcessing = false;
                    data.antialiasing = AntialiasingMode.None;
                    data.renderShadows = false;
                }
            }

            _camera.targetTexture = _texture;
            return true;
        }
    }
}
