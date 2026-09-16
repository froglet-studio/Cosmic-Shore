using CosmicShore.UI;
using UnityEngine;

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
    /// <para><b>It draws the way the GAME'S camera draws, and that is not a quality setting.</b>
    /// A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's defaults — no post-processing,
    /// no volume layer mask, SDR — and this world is authored HDR-emissive against the gameplay
    /// volume's tonemapper, so an un-adopted camera renders a flat, near-black picture. That is
    /// exactly how this window shipped and exactly how it was reported: <i>"I no longer saw the
    /// pip."</i> It only surfaced at round 5 because round 3's window framed the pilot's own lit
    /// HULL, which is unmistakable even rendered wrong, while this one frames open space, where
    /// the whole picture IS the skybox and the volume. Adopted through
    /// <see cref="OffscreenCameraSetup"/>, which exists because the same finding had already been
    /// written down at three other windows. <b>A picture that renders wrong and a picture that
    /// does not render are the same report.</b></para>
    ///
    /// <para><b>The cost is real and is stated rather than hidden.</b> Unlike the connecting
    /// panel's preview — which stands the gameplay camera DOWN because the panel covers the screen
    /// — this one is a genuine SECOND render of the world, because the first one is what the
    /// player is flying with. So it is paid everywhere EXCEPT the tonemapper: a small square
    /// render target, no shadows, no anti-aliasing, a capped refresh rate, and a lifetime of
    /// exactly as long as the trigger is held. It is off the moment the scope drops.</para>
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

            // ENABLED first, then bound: SetMaterialDirty no-ops on an inactive Graphic, so a
            // texture written while the disc is off would rely on OnEnable's SetAllDirty to pick
            // it up. It does — and depending on that ordering is a hazard with no upside.
            _surface.enabled = true;
            _surface.Source = _texture;

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
                int size = Mathf.Clamp(RenderSize, 128, 1024);
                _texture = new RenderTexture(size, size, 16)
                {
                    name = "SerpentScopeRT",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false,
                };
                // Created outright rather than left to first use: the surface binds this texture
                // in the same frame, and a canvas sampling an uncreated target draws nothing.
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
