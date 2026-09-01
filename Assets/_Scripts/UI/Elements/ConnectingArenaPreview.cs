using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The connecting screen's live window onto the arena being built — the same idea as the arcade
    /// card's preview, pointed at the world this scene is actually standing up.
    ///
    /// <para>It is the one thing on the panel that answers "is it doing anything?" honestly. The
    /// status line and the bar are readouts of counters; this is the arena itself, laying and
    /// blooming in, so a long load reads as a world being built rather than as a hang.</para>
    ///
    /// <para><b>It renders into a RenderTexture, never to the screen.</b> A camera with a
    /// <c>targetTexture</c> never draws to the display at all, so this one cannot fight the panel's
    /// own backdrop camera or the gameplay camera behind it — no depth ordering to get right.</para>
    ///
    /// <para><b>It renders ON DEMAND, not every frame.</b> This runs during the heaviest frames of
    /// the whole session — a second full pass over an arena of 50,000 prisms would roughly double
    /// the render cost of the load it is reporting on. The camera is left DISABLED and stepped by
    /// hand at <see cref="renderHz"/>; a world growing in reads perfectly well at 8 Hz, and the
    /// difference against 60 is most of the preview's cost.</para>
    /// </summary>
    public class ConnectingArenaPreview : MonoBehaviour
    {
        [Header("Surface")]
        [SerializeField, Tooltip("The RawImage the arena renders into. Without one this component " +
                                 "does nothing at all — the panel simply shows no preview.")]
        RawImage surface;

        [Header("Camera")]
        [SerializeField, Tooltip("Optional. Left empty, one is created at runtime and posed by " +
                                 "this component. Wire one only to pin a specific culling mask.")]
        Camera previewCamera;

        [SerializeField, Tooltip("Copied for culling mask and clear flags when the camera is made " +
                                 "at runtime. Normally the panel's own backdrop camera. Its CLIP " +
                                 "PLANES are deliberately NOT copied — see the note on framing.")]
        Camera settingsTemplate;

        [Header("Framing")]
        [SerializeField, Tooltip("How far back the camera sits, as a multiple of the arena radius. " +
                                 "UNDER 1 puts the camera INSIDE the boundary, which is the right " +
                                 "shot here: the radius reported by a cell is its MEMBRANE, a shell " +
                                 "that is far bigger than the mass being laid inside it — framed " +
                                 "from outside, the membrane exactly fills the frame and the arena " +
                                 "is a speck in the middle of it. The arcade card frames from " +
                                 "outside (1.95) because it is showing you a WORLD; this shows you " +
                                 "a BUILD, so it sits in the room the build is happening in.")]
        [Min(0.15f)] float framingFactor = 0.805f;

        [SerializeField, Tooltip("Lift above the arena's equator, as a multiple of its radius.")]
        float liftFactor = 0.22f;

        [SerializeField, Tooltip("Degrees per second around the arena. Slow: the subject is the " +
                                 "world appearing, and a fast orbit competes with it.")]
        float orbitDegreesPerSecond = 6f;

        [SerializeField, Tooltip("Narrower than the arcade card's 60 — the second half of the " +
                                 "zoom, spent optically rather than by moving the camera, so the " +
                                 "shot tightens without pushing the near geometry off frame.")]
        [Min(20f)] float fieldOfView = 45f;

        [SerializeField, Tooltip("How far out the camera may be pushed, as a fraction of the " +
                                 "membrane radius. Under 1 by definition: a camera outside the " +
                                 "membrane looks at the arena THROUGH the boundary shell, which is " +
                                 "the wall-of-membrane shot this framing exists to remove.")]
        [Range(0.1f, 0.99f)] float insideCellMargin = 0.9f;

        [SerializeField, Tooltip("Seconds the shot takes to catch up as the measured arena grows. " +
                                 "The extent arrives one clone batch at a time; tracking it raw " +
                                 "would jitter the camera every 256 prisms.")]
        [Min(0.05f)] float framingSmoothing = 1.2f;

        [Header("Cost")]
        [SerializeField, Tooltip("Preview renders per second. The camera is stepped by hand at this " +
                                 "rate rather than left enabled, because an enabled camera renders " +
                                 "the whole arena every frame of the heaviest load in the game.")]
        [Range(1f, 60f)] float renderHz = 20f;

        [SerializeField, Tooltip("Fallback render height when the surface cannot be measured. " +
                                 "Normally the surface's OWN height in real screen pixels is used, " +
                                 "so the preview is 1:1 with the panel rather than a blurry upscale.")]
        [Range(120, 2160)] int renderHeight = 720;

        [SerializeField, Tooltip("Ceiling on the measured render height.")]
        [Range(240, 2160)] int maxRenderHeight = 1080;

        [SerializeField, Tooltip("Render the preview with the game's own image quality - post " +
                                 "processing, anti-aliasing and shadows copied from the gameplay " +
                                 "camera. Affordable because the gameplay camera is stood down " +
                                 "while the panel covers the screen (see suppressGameplayCamera); " +
                                 "turn it off to trade the look back for frames.")]
        bool matchGameQuality = true;

        [SerializeField, Tooltip("Stand the GAMEPLAY camera's rendering down while the panel is up. " +
                                 "The panel covers the screen, so that camera is drawing the whole " +
                                 "arena behind an opaque cover every frame for nobody - which is " +
                                 "the single biggest cost the preview is competing with. Its " +
                                 "culling mask and post stack are stashed and restored exactly.")]
        bool suppressGameplayCamera = true;

        /// <summary>
        /// The menu cell's own membrane radius — a sane arena size when nothing reports one.
        /// Same fallback the arcade preview uses, for the same reason.
        /// </summary>
        const float DefaultFramingRadius = 1200f;

        /// <summary>How much of a nucleus-only cell to take in: the core plus the room around it.</summary>
        const float NucleusFramingMultiple = 3f;

        RenderTexture _renderTexture;
        Camera _ownedCamera;
        float _angle;
        float _renderAccumulator;
        bool _running;

        /// <summary>
        /// Tell the preview which camera is ALREADY doing another job on this panel, before it is
        /// brought up.
        ///
        /// <para>Wiring that camera as <see cref="previewCamera"/> is an easy and completely silent
        /// mistake: the preview takes it over — retargets it to a RenderTexture, so it stops drawing
        /// to the screen, and re-poses it, so it stops looking at what it was posed at. The panel's
        /// backdrop just disappears, and nothing says why. So the collision is DETECTED, corrected
        /// (the reserved camera becomes the settings template and the preview makes its own), and
        /// reported once.</para>
        /// </summary>
        public void ReserveCamera(Camera reserved)
        {
            if (!reserved || previewCamera != reserved) return;

            CSDebug.LogWarning(
                $"[ConnectingArenaPreview] '{name}' is wired to the same camera the panel uses for " +
                "its own backdrop. Taking it over would retarget and re-pose it, and the backdrop " +
                "would silently vanish. Using it as the settings template and creating a dedicated " +
                "preview camera instead - clear ConnectingArenaPreview.previewCamera to make this " +
                "the authored state.", this);

            if (!settingsTemplate) settingsTemplate = reserved;
            previewCamera = null;
        }

        /// <summary>Bring the preview up. Safe to call with nothing wired.</summary>
        public void Begin()
        {
            if (!surface) return;

            EnsureRenderTexture();
            var cam = EnsureCamera();
            if (!cam) return;

            cam.targetTexture = _renderTexture;
            // Left DISABLED on purpose - LateUpdate steps it. See the class note on cost.
            cam.enabled = false;

            surface.texture = _renderTexture;
            surface.enabled = true;

            SuppressGameplayCamera();

            _running = true;
            _framedFromLay = false;
            _framedRadius = 0f;
            _framedOrigin = Vector3.zero;
            _renderAccumulator = float.PositiveInfinity;   // draw the first frame immediately
            Frame(0f);
        }

        /// <summary>
        /// Take it down. Called when the panel hides, and from OnDisable/OnDestroy — a RenderTexture
        /// is a GPU allocation, and one left bound to a camera that outlives the panel is both a
        /// leak and a camera still rendering the world every frame of the match.
        /// </summary>
        public void End()
        {
            _running = false;
            RestoreGameplayCamera();

            if (previewCamera)
            {
                previewCamera.targetTexture = null;
                previewCamera.enabled = false;
            }

            if (surface)
            {
                surface.texture = null;
                // Disabled rather than cleared: a RawImage with a null texture draws its colour as
                // a solid rectangle, which is the "white box" a blank preview used to show.
                surface.enabled = false;
            }

            ReleaseRenderTexture();
        }

        // Eased, monotone framing state. Reset per Begin so a second load never opens on the
        // previous arena's shot.
        bool _framedFromLay;
        float _framedRadius;
        Vector3 _framedOrigin;

        void OnDisable() => End();

        void OnDestroy()
        {
            End();
            if (_ownedCamera) Destroy(_ownedCamera.gameObject);
        }

        void LateUpdate()
        {
            if (!_running || !previewCamera) return;

            float dt = Time.unscaledDeltaTime;
            Frame(dt);

            _renderAccumulator += dt;
            float interval = 1f / Mathf.Max(1f, renderHz);
            if (_renderAccumulator < interval) return;

            _renderAccumulator = 0f;
            previewCamera.Render();
        }

        /// <summary>
        /// Point the camera at whatever cell exists RIGHT NOW, framed so the WHOLE arena is in
        /// shot. Re-resolved every frame rather than once at Begin, because the panel comes up
        /// before the cell does — that is the whole point of the panel.
        ///
        /// <para><b>The framing is the bug this component shipped with, and it is the same one
        /// <c>ModePreviewArena.FramingRadius</c> already records.</b> <c>Cell.MembraneRadius</c>
        /// returns 0 until the membrane has actually spawned, and a camera parked at a fallback
        /// distance with the arena's real size unknown shows the skybox and a few distant slivers —
        /// which reads as "the preview does not work" rather than as "the camera is in the wrong
        /// place". So it falls back through the nucleus to a sane default, and re-reads every tick
        /// so the framing corrects itself the moment the membrane appears.</para>
        ///
        /// <para>The CLIP PLANES are derived from that distance rather than copied from the
        /// template. Copying them is the second half of the same failure: the panel's backdrop
        /// camera is posed a few units from a backdrop and its far plane is sized for that, so a
        /// preview inheriting it clips the entire arena away and shows — again — the skybox.</para>
        /// </summary>
        void Frame(float deltaTime)
        {
            var cam = previewCamera;
            if (!cam) return;

            _angle = Mathf.Repeat(_angle + orbitDegreesPerSecond * deltaTime, 360f);

            var cell = Cell.FindNearestActiveCell(Vector3.zero);
            Vector3 cellCentre = cell ? cell.transform.position : Vector3.zero;

            // Frame what was BUILT, not the boundary around it. The measured extent grows with the
            // lay, so the shot opens out as the arena does; only when nothing has been laid yet
            // (the dwell, or an authored environment prefab that is instantiated rather than laid)
            // does it fall back to the cell's own size.
            //
            // Once the measurement takes over it only ever GROWS, and it is eased: an arena is
            // measured from one clone batch at a time, so tracking it raw would jitter the shot
            // every 256 prisms, and letting it shrink would dolly the camera IN while the world
            // was getting bigger. Monotone + eased reads as one slow pull-back.
            bool haveLaid = PrismTrailBuilder.TryGetLaidBounds(out var laidCentre, out var laidRadius)
                            && laidRadius > 1f;

            Vector3 origin;
            float radius;
            if (haveLaid)
            {
                if (!_framedFromLay)
                {
                    // First measurement: adopt it outright rather than easing down from the
                    // fallback, which is the boundary and is much bigger than the first batch.
                    _framedFromLay = true;
                    _framedRadius = laidRadius;
                    _framedOrigin = laidCentre;
                }

                float ease = deltaTime <= 0f ? 1f : Mathf.Clamp01(deltaTime / framingSmoothing);
                _framedRadius = Mathf.Lerp(_framedRadius, Mathf.Max(_framedRadius, laidRadius), ease);
                _framedOrigin = Vector3.Lerp(_framedOrigin, laidCentre, ease);
                origin = _framedOrigin;
                radius = _framedRadius;
            }
            else
            {
                origin = cellCentre;
                radius = FramingRadius(cell);
            }

            var offset = Quaternion.Euler(0f, _angle, 0f) *
                         new Vector3(0f, radius * liftFactor, -radius * framingFactor);

            var t = cam.transform;
            t.position = ClampInsideCell(origin + offset, cellCentre, cell);
            t.rotation = Quaternion.LookRotation((origin - t.position).normalized, Vector3.up);

            cam.fieldOfView = fieldOfView;

            // Sized to the shot, never inherited: the far plane has to clear the far side of the
            // arena from outside it, and the near plane has to stay large enough not to wreck
            // depth precision at this distance.
            //
            // The far plane is sized against the CELL, not only against the arena being framed.
            // Sizing it off `radius` alone is wrong whenever the laid mass is small relative to
            // its boundary - which is most of a build, and every cell whose world is grown rather
            // than laid: the membrane's far wall then sits past the plane and the far half of the
            // shot is CLIPPED TO BLACK, with the near half rendering normally. That reads as a
            // shader or culling fault rather than as a camera setting, which is exactly how it
            // was reported.
            float distance = Vector3.Distance(t.position, origin);
            float cellReach = Vector3.Distance(t.position, cellCentre) +
                              (cell ? cell.MembraneRadius : 0f);
            cam.farClipPlane = Mathf.Max(Mathf.Max(distance + radius * 2f, cellReach * 1.1f), 1000f);
            cam.nearClipPlane = Mathf.Max(0.3f, distance * 0.005f);
        }

        /// <summary>
        /// How big the arena is, for framing: the membrane first (it is the playfield boundary, so
        /// it is what "the arena" means), then the nucleus, then a default the size of the menu's
        /// own membrane.
        /// </summary>
        /// <summary>
        /// Keep the camera INSIDE the cell.
        ///
        /// <para>A camera outside the membrane is looking at the arena through its own boundary
        /// shell - so the shot is a wall of membrane with the world behind it, which is exactly the
        /// failure this framing pass exists to remove. It is also the one place a preview can put
        /// something outside the playfield, and nothing belongs out there.</para>
        ///
        /// <para>Clamped against the CELL's centre rather than the arena's, because the membrane is
        /// centred on the cell and an arena measured off its own laid mass can sit well off-centre
        /// inside it.</para>
        /// </summary>
        Vector3 ClampInsideCell(Vector3 position, Vector3 cellCentre, Cell cell)
        {
            float membrane = cell ? cell.MembraneRadius : 0f;
            if (membrane <= 1f) return position;

            float ceiling = membrane * insideCellMargin;
            var fromCentre = position - cellCentre;
            if (fromCentre.sqrMagnitude <= ceiling * ceiling) return position;
            return cellCentre + fromCentre.normalized * ceiling;
        }

        static float FramingRadius(Cell cell)
        {
            if (cell)
            {
                float membrane = cell.MembraneRadius;
                if (membrane > 1f) return membrane;

                float nucleus = cell.ExpectedNucleusWorldRadius;
                if (nucleus > 1f) return nucleus * NucleusFramingMultiple;
            }

            return DefaultFramingRadius;
        }

        Camera EnsureCamera()
        {
            if (previewCamera) return previewCamera;

            var go = new GameObject("[ConnectingArenaPreviewCamera]");
            go.transform.SetParent(transform, worldPositionStays: false);

            _ownedCamera = go.AddComponent<Camera>();
            _ownedCamera.enabled = false;

            if (settingsTemplate)
            {
                _ownedCamera.cullingMask = settingsTemplate.cullingMask;
                _ownedCamera.clearFlags = settingsTemplate.clearFlags;
                _ownedCamera.backgroundColor = settingsTemplate.backgroundColor;
            }
            else
            {
                // Everything except UI: a preview that rendered the canvas would draw the panel
                // inside its own window, one frame stale, forever.
                int ui = LayerMask.NameToLayer("UI");
                _ownedCamera.cullingMask = ui >= 0 ? ~(1 << ui) : ~0;
                _ownedCamera.clearFlags = CameraClearFlags.Skybox;
            }

            AdoptUrpSettings(_ownedCamera);

            previewCamera = _ownedCamera;
            return previewCamera;
        }

        /// <summary>
        /// A bare <c>AddComponent&lt;Camera&gt;</c> comes up with URP's DEFAULTS, not the project's,
        /// so the preview would render a flat, bloom-free version of a world the game shows lit -
        /// which reads as the preview being broken rather than as a different camera
        /// (<c>ModePreviewArena.AdoptGameCameraSettings</c> records the same finding).
        ///
        /// <para>Post-processing, anti-aliasing and shadows ARE adopted when
        /// <see cref="matchGameQuality"/> is set. They were withheld while the gameplay camera was
        /// still drawing the whole arena behind the cover - two full renders of the same world was
        /// a cost nothing could absorb. With that camera stood down for the duration
        /// (<see cref="suppressGameplayCamera"/>) there is one render again, and it may as well be
        /// the one the player is looking at.</para>
        /// </summary>
        void AdoptUrpSettings(Camera target)
        {
            var source = Camera.main;
            if (!source) return;

            target.allowHDR = source.allowHDR;
            target.allowMSAA = source.allowMSAA;

            if (!source.TryGetComponent(out UniversalAdditionalCameraData from)) return;

            var to = target.GetUniversalAdditionalCameraData();
            if (!to) return;

            to.volumeLayerMask = from.volumeLayerMask;
            to.renderPostProcessing = matchGameQuality && from.renderPostProcessing;
            to.antialiasing = matchGameQuality ? from.antialiasing : AntialiasingMode.None;
            to.antialiasingQuality = from.antialiasingQuality;
            to.renderShadows = matchGameQuality && from.renderShadows;
        }

        // ── Standing the gameplay camera down ───────────────────────────────

        Camera _suppressed;
        int _suppressedMask;
        bool _suppressedPost;

        /// <summary>
        /// Stop the GAMEPLAY camera drawing while the panel covers the screen.
        ///
        /// <para>This is the biggest single cost the preview was competing with, and it buys
        /// nothing: the panel is opaque, so that camera renders the entire arena - the same ~50,000
        /// prisms the preview is rendering - every frame, for a viewer who cannot see any of it.
        /// It is the same argument the load gate already makes about its own tempo, applied to the
        /// one thing the gate does not touch.</para>
        ///
        /// <para>The camera is muted rather than DISABLED, deliberately: <c>Camera.main</c> returns
        /// the first ENABLED camera tagged MainCamera, so disabling it makes <c>Camera.main</c> null
        /// for everything in the project that reads it - a very large blast radius for a frame-rate
        /// fix. Zeroing the culling mask and switching post off leaves the camera live and every
        /// reference to it valid, while the expensive half of its frame simply has nothing to
        /// do.</para>
        /// </summary>
        void SuppressGameplayCamera()
        {
            if (!suppressGameplayCamera || _suppressed) return;

            var cam = Camera.main;
            if (!cam || cam == previewCamera || cam == settingsTemplate) return;

            _suppressed = cam;
            _suppressedMask = cam.cullingMask;
            cam.cullingMask = 0;

            if (cam.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                _suppressedPost = data.renderPostProcessing;
                data.renderPostProcessing = false;
            }
        }

        /// <summary>Give the gameplay camera back exactly what it had.</summary>
        void RestoreGameplayCamera()
        {
            if (!_suppressed) return;

            _suppressed.cullingMask = _suppressedMask;
            if (_suppressed.TryGetComponent(out UniversalAdditionalCameraData data))
                data.renderPostProcessing = _suppressedPost;

            _suppressed = null;
        }

        void EnsureRenderTexture()
        {
            if (_renderTexture) return;

            float aspect = 16f / 9f;
            if (surface && surface.rectTransform.rect.height > 1f)
                aspect = Mathf.Clamp(
                    surface.rectTransform.rect.width / surface.rectTransform.rect.height, 0.25f, 4f);

            int height = MeasuredRenderHeight();
            _renderTexture = new RenderTexture(
                Mathf.Max(64, Mathf.RoundToInt(height * aspect)), height, 24)
            {
                name = "ConnectingArenaPreviewRT",
                antiAliasing = matchGameQuality ? Mathf.Max(1, QualitySettings.antiAliasing) : 1,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
            };
        }

        /// <summary>
        /// The surface's height in REAL SCREEN PIXELS, so the preview renders 1:1 with the window
        /// it is displayed in rather than being upscaled from a fixed low resolution. A canvas
        /// reports its rect in reference units, so the canvas scale factor is what turns that into
        /// pixels - without it a 1080p screen shows a preview authored for 360.
        /// </summary>
        int MeasuredRenderHeight()
        {
            if (!surface || surface.rectTransform.rect.height <= 1f)
                return Mathf.Clamp(renderHeight, 120, maxRenderHeight);

            var canvas = surface.canvas;
            float scale = canvas ? canvas.scaleFactor : 1f;
            int measured = Mathf.RoundToInt(surface.rectTransform.rect.height * Mathf.Max(0.01f, scale));
            return Mathf.Clamp(measured, 240, maxRenderHeight);
        }

        void ReleaseRenderTexture()
        {
            if (!_renderTexture) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
