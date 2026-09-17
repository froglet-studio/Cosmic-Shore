using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The Serpent scope's <b>window</b>: a magnified view down the vessel's own nose, drawn into a
    /// panel in the corner of the screen while the pilot keeps flying with their ordinary chase
    /// view.
    ///
    /// <para><b>The inversion is the design; the SURFACE is not part of it.</b> Round 4 moved the
    /// magnification out of the flight camera and into this window — which was right, and read as
    /// right — and in the same pass it replaced the window's <c>RawImage</c> with a generated
    /// circular <c>MaskableGraphic</c>. Rounds 4, 5 and 6 were then spent debugging a surface that
    /// had never rendered, while the one that had was sitting in the history: the pilot's report
    /// across all three was the same four words, <i>"i no longer saw the pip"</i>, and the answer
    /// was that the window they had been seeing for three rounds was a `RawImage` in a 16:9 rect.
    /// So the surface, its size and its place are round 3's verbatim and only the CAMERA is round
    /// 4's. <b>When a change replaces a working surface and re-points it in the same pass, the
    /// report cannot tell you which half broke — so change one.</b>
    /// <c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 7.</para>
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
    /// <para><b>It draws the way the GAME'S camera draws, and that is not a quality setting.</b>
    /// A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's defaults — no post-processing,
    /// no volume layer mask, SDR — and this world is authored HDR-emissive against the gameplay
    /// volume's tonemapper, so an un-adopted camera renders a flat, near-black picture. Round 3's
    /// window declined post-processing and got away with it because it framed the pilot's own lit
    /// HULL, which is unmistakable even rendered wrong; this one frames open space, where the whole
    /// picture IS the skybox and the volume. Adopted through <see cref="OffscreenCameraSetup"/>,
    /// which exists because the same finding had already been written down at three other windows.
    /// <b>A picture that renders wrong and a picture that does not render are the same
    /// report.</b></para>
    ///
    /// <para><b>The cost is real and is stated rather than hidden.</b> Unlike the connecting
    /// panel's preview — which stands the gameplay camera DOWN because the panel covers the screen
    /// — this one is a genuine SECOND render of the world, because the first one is what the player
    /// is flying with. So it is paid everywhere EXCEPT the tonemapper: a modest render target, no
    /// shadows, no anti-aliasing, a capped refresh rate, and a lifetime of exactly as long as the
    /// trigger is held. It is off the moment the scope drops.</para>
    ///
    /// <para><b>The eye is MEASURED, not authored</b> — a multiple of the vessel's own
    /// circumscribing hull radius (<see cref="PrismOcclusionCorridor.MeasureCircumscribedRadius"/>,
    /// the same rotation-invariant hull-only measurement the occlusion corridor sizes itself from),
    /// so the camera sits just clear of its own geometry on a hull of any size rather than at a
    /// constant that is inside one ship and far ahead of another. It aims along the vessel's own
    /// forward, which is <b>the same vector the sniper's cone is cast along</b>
    /// (<c>SniperShotActionExecutor.ResolveShot</c>) — so the shot lands where the reticle is by
    /// construction rather than by tuning.</para>
    ///
    /// <para><b>The picture is 16:9</b>, matching the panel it is drawn into, so nothing is
    /// squashed and nothing is thrown away. A camera targeting a RenderTexture takes its aspect
    /// from that texture, so there is nothing else to keep in step.</para>
    /// </summary>
    public sealed class ScopePipView
    {
        readonly RawImage _surface;

        Camera _camera;
        RenderTexture _texture;
        Transform _measuredHull;
        float _hullRadius;
        float _nextRenderTime;
        bool _warnedNoHull;

        /// <summary>
        /// How far past the vessel's own circumscribing hull radius the eye sits, so the scope is
        /// never looking at the inside of its own ship. A multiple rather than a distance: see the
        /// class summary on why the eye is measured.
        /// </summary>
        const float EyeForwardHullRadii = 1.05f;

        /// <summary>
        /// The smallest distance the eye may sit ahead of the vessel origin, whatever the hull
        /// measures. Only ever reached when the measurement fails.
        /// </summary>
        const float MinimumEyeForward = 2f;

        /// <summary>
        /// Render HEIGHT in pixels; the width follows at 16:9. Round 3 ran 360 and read fine for a
        /// chase shot, but this window is magnified and half the screen tall, and a magnified
        /// picture that cannot be read is the same as no picture — so it is raised rather than
        /// left at a value chosen for a different subject.
        /// </summary>
        public int RenderHeight = 540;

        /// <summary>How often the window refreshes, in Hz. Capped for the same reason.</summary>
        public float RefreshHz = 30f;

        public ScopePipView(RawImage surface) => _surface = surface;

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

            _surface.texture = _texture;
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
            //
            // It is NOT latched on a ZERO, though, and that asymmetry is deliberate: the
            // measurement skips disabled renderers, so a hull asked before its art is switched on
            // answers 0 — and a latched 0 parks the eye on the vessel's own origin, inside its
            // geometry, for the whole life of the vessel. Re-asking while it reads 0 costs one
            // walk per frame in exactly the case where the alternative is a permanently black
            // window.
            if (_measuredHull != vessel || _hullRadius <= 0f)
            {
                _measuredHull = vessel;
                _hullRadius = PrismOcclusionCorridor.MeasureCircumscribedRadius(vessel);
                if (_hullRadius <= 0f && !_warnedNoHull)
                {
                    _warnedNoHull = true;
                    CSDebug.LogWarning($"[ScopePipView] '{vessel.name}' measures a zero hull " +
                                       "radius, so the scope eye has nothing to clear. It falls " +
                                       "back to a fixed offset; check that the vessel's mesh " +
                                       "renderers are enabled.");
                }
            }

            // A RIGID attachment, and both halves are load-bearing. The eye is written outright
            // every frame — any lag at all at zero distance puts the camera inside the hull it is
            // trying to see past — and it aims along the vessel's OWN forward rather than at
            // anything, because in the cockpit a look-at vector is very nearly zero.
            // Floored so a hull that cannot be measured still puts the eye in FRONT of the ship
            // rather than inside it. A fallback the pilot can see past beats a correct number
            // nobody supplied.
            float reach = Mathf.Max(MinimumEyeForward, _hullRadius * EyeForwardHullRadii);
            _camera.transform.SetPositionAndRotation(vessel.position + vessel.forward * reach,
                                                     vessel.rotation);

            _camera.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 170f);
        }

        /// <summary>
        /// Stand the camera and its render target up, once. It adopts the gameplay camera's IMAGE
        /// settings (see the class summary) and derives its own clip planes, because the planes are
        /// the one thing a borrowed camera must not inherit — this one sits on a hull looking down
        /// its nose at the whole arena, which is not the shot any of its siblings frame.
        /// </summary>
        bool EnsureCamera()
        {
            if (_camera != null && _texture != null) return true;

            if (_texture == null)
            {
                int height = Mathf.Clamp(RenderHeight, 180, 1080);
                _texture = new RenderTexture(Mathf.RoundToInt(height * 16f / 9f), height, 16)
                {
                    name = "SerpentScopeRT",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false,
                };
                // Created outright rather than left to first use: the surface binds this texture
                // in the same frame, and a RawImage sampling an uncreated target draws nothing.
                _texture.Create();
            }

            if (_camera == null)
            {
                var go = new GameObject("[SerpentScopeCamera]");
                // Deliberately NOT tagged MainCamera, and deliberately left disabled.
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;

                // WHAT it sees and clears to, minus the canvas this window is drawn on.
                OffscreenCameraSetup.AdoptGameCameraFraming(_camera, excludeUiLayer: true);
                // HOW it draws. Post-processing is ON and is the whole reason this call exists;
                // shadows and anti-aliasing are declined because this is a second full render of
                // the world and neither is legible at a few hundred pixels.
                OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: true,
                                                          antiAliasing: false, shadows: false);
                _camera.allowMSAA = false;
                _camera.useOcclusionCulling = false;

                // DERIVED, never borrowed: near hugs the eye so the pilot's own hull cannot fill
                // the window, and far reaches the arena. A borrowed near plane sized for a camera
                // 250 units back would clip everything this one is close to.
                _camera.nearClipPlane = 0.3f;
                _camera.farClipPlane = Mathf.Max(20000f, ResolveGameFarPlane());

                if (Camera.main == null)
                    CSDebug.LogWarning("[ScopePipView] No Camera.main when the scope window was " +
                                       "built, so it could not adopt the game's post-processing " +
                                       "volume. The picture will render flat until the scope is " +
                                       "re-raised.");
            }

            _camera.targetTexture = _texture;
            return true;
        }

        /// <summary>The gameplay camera's far plane, or a sane arena-sized default.</summary>
        static float ResolveGameFarPlane()
        {
            var main = Camera.main;
            return main != null ? main.farClipPlane : 20000f;
        }
    }
}
