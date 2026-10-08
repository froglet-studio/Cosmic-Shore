using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One end of a <b>wormhole</b> — a sphere whose inside is the inside of its partner
    /// (<see cref="WormholeGeometry"/> states the model). Fly into either and you come out of the
    /// other, still flying the way you were. Every Butterfly fold leaves a pair: one mouth where
    /// it left, one where it arrived (<c>FoldActionExecutor</c>, <c>BUTTERFLY_FOLD.md</c> § "The
    /// gates became wormholes", <c>Docs/WORMHOLES.md</c>).
    ///
    /// <para><b>Two cameras ride on every mouth</b>, and they are what the sphere's surface shows
    /// — the PARTNER's cameras, since through A you see the place around B:</para>
    /// <list type="bullet">
    /// <item><b>The panorama eye</b> sits at the mouth's centre and captures its surroundings in
    /// all six directions (a 90° camera, one face at a time, into a six-slice texture array).
    /// The partner's sphere projects that capture back out in every direction, so the mouth reads
    /// as a window onto somewhere else from any side, for ANY camera — a spectator rig, a
    /// preview camera, the editor's scene view. A capture has no depth, so near things
    /// slide a little against the far ones; that is the cost of a picture taken from one
    /// point.</item>
    /// <item><b>The exact eye</b> is the "player camera cheat", and it is what makes the transit
    /// seamless. Each frame it takes the player's camera pose carried through the pair, copies
    /// its projection, clips everything between that vantage and the far ball, and renders only
    /// this mouth's own footprint of the screen. The surface samples that picture at its own
    /// screen position, so every pixel is exactly what the player would see if the two mouths
    /// were one place — near things included — and the pilot flies INTO the place they are
    /// about to be. It is used by the player's camera only, inside
    /// <see cref="ExactRange"/>, and crossfades to the panorama beyond it.</item>
    /// </list>
    ///
    /// <para><b>Who detects, who moves</b> — the rule the Butterfly's ring gates had: each
    /// machine tests only the vessels it OWNS and writes their pose
    /// through <c>IVessel.SetPose</c>, which replicates. The pose write reaches
    /// <c>VesselTransformer.SetPose</c> on every peer, where <see cref="TeleportContinuity"/> cuts
    /// the ribbons at the two mouths and carries the camera through
    /// (<c>CustomCameraController.CarryThroughSphere</c>). The wormhole needs no networking of its
    /// own: every peer lays both mouths itself, from the fold's two replicated poses.</para>
    ///
    /// <para><b>It has no collider.</b> Nothing physical touches it; a transit is decided from the
    /// vessel's own step, like a gate's. A prism laid inside the ball is ordinary mass in the shared
    /// interior — visible through either mouth's exact view, hidden from outside behind both
    /// surfaces.</para>
    ///
    /// <para><b>Domain-locked.</b> A <see cref="Settings.DomainLocked"/> mouth (every fold pair)
    /// carries only vessels of its <see cref="Domain"/> — its owning Butterfly's live domain — and
    /// to a viewer of any other domain it is SEALED: a fresnel outline in the domain's colour with
    /// no view through it, because a view through is a promise you can go there. Either way the rim
    /// wears <see cref="Settings.RimTint"/>, the domain's hue.</para>
    /// </summary>
    public sealed class WormholeMouth : MonoBehaviour
    {
        /// <summary>Every built mouth, oldest first — read by <see cref="WormholeView"/> and by
        /// <see cref="TryResolveTransit"/> instead of a scene search.</summary>
        public static readonly List<WormholeMouth> Live = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Live.Clear();

        static readonly int SphereId = Shader.PropertyToID("_WormholeSphere");
        static readonly int ExactTexId = Shader.PropertyToID("_WormholeExactTex");
        static readonly int ExactUVId = Shader.PropertyToID("_WormholeExactUV");
        static readonly int ExactBlendId = Shader.PropertyToID("_WormholeExactBlend");
        static readonly int PanoramaId = Shader.PropertyToID("_WormholePanorama");
        static readonly int PanoramaReadyId = Shader.PropertyToID("_WormholePanoramaReady");
        static readonly int FlareId = Shader.PropertyToID("_WormholeFlare");
        static readonly int RimTintId = Shader.PropertyToID("_WormholeRimTint");
        static readonly int SealedId = Shader.PropertyToID("_WormholeSealed");

        /// <summary>
        /// Everything a mouth is built with, copied at <see cref="Build"/>. A struct rather than a
        /// reference to the fold's tuning asset, so the mouth does not depend on the ability that
        /// places it (and the tuning a pair was laid with cannot change under it).
        /// </summary>
        public struct Settings
        {
            /// <summary>The sphere's material (CosmicShore/Wormhole).</summary>
            public Material SurfaceMaterial;
            /// <summary>Seconds to bloom in from nothing; also the default wither on retire.</summary>
            public float BloomSeconds;
            /// <summary>Furthest the player's camera may be for an exact view.</summary>
            public float ExactRange;
            /// <summary>Distance past <see cref="ExactRange"/> over which it fades to the panorama.</summary>
            public float ExactFadeBand;
            /// <summary>Exact view resolution as a fraction of the gameplay camera's.</summary>
            public float ExactRenderScale;
            /// <summary>Texels per side of each panorama face.</summary>
            public int PanoramaFaceSize;
            /// <summary>FMOD event at the exit of a transit. Empty = silence.</summary>
            public FMODUnity.EventReference TransitEvent;
            /// <summary>The rim's hue — the owning domain's colour (sRGB, as the theme authors it).</summary>
            public Color RimTint;
            /// <summary>Carry only vessels of <see cref="Domain"/>, and seal the view for every other.</summary>
            public bool DomainLocked;
            /// <summary>The domain that owns the mouth (its rim colour, and its lock if locked) when
            /// it has no <see cref="Owner"/>; with one, the owner's LIVE domain wins.</summary>
            public Domains Domain;
            /// <summary>
            /// The pilot whose mouth this is (the Butterfly whose fold laid it), or null.
            /// The owner is ALWAYS carried and always sees through, and the lock follows the owner's
            /// domain as it is NOW — so a Butterfly that changes domain keeps its pair, and no
            /// capture-time reading of a domain can lock the owner out of its own wormhole.
            /// </summary>
            public IVesselStatus Owner;
            /// <summary>Theme the rim's domain hue is read from when the owner's domain changes.</summary>
            public ThemeManagerDataContainerSO Theme;
        }

        static readonly Vector4 IdentityUV = new(1f, 1f, 0f, 0f);

        /// <summary>How far toward the vantage the clip plane is pulled from the far ball, so
        /// geometry lying on the plane does not flicker between kept and clipped.</summary>
        const float ClipPlaneOffset = 0.05f;

        /// <summary>Below this camera-space distance to the plane an oblique projection
        /// degenerates; only reached with the vantage at the far ball's own surface.</summary>
        const float MinObliqueDistance = 0.2f;

        const float FootprintPadPixels = 3f;

        Settings _settings;
        IReadOnlyList<IPlayer> _players;
        bool _retiring;
        float _retireSeconds = 0.5f;
        float _radius = 1f;
        float _bloom;
        float _flare;
        WormholeMouth _partner;

        Transform _surface;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        Camera _exactEye;
        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _exactEyeData;
        RenderTexture _exactTex;
        RenderTextureFormat _exactFormat;
        Vector4 _exactUV = IdentityUV;

        Camera _panoramaEye;
        UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _panoramaEyeData;
        RenderTexture _panorama;
        RenderTexture _panoramaFace;
        // The format REQUESTED: RenderTexture.format reports what DefaultHDR resolved to on this
        // device, so comparing against that would never match and reallocate every frame.
        RenderTextureFormat _panoramaFormat;
        int _panoramaFacesCaptured;   // bit per face; ready once all six have landed once
        int _nextPanoramaFace;

        readonly Dictionary<IVessel, Vector3> _lastPos = new();
        readonly List<IVessel> _scratchDead = new();

        /// <summary>The other end, or null — an unpaired mouth is inert, never broken.</summary>
        public WormholeMouth Partner => _partner;

        /// <summary>The mouth's centre in world space.</summary>
        public Vector3 Centre => transform.position;

        /// <summary>
        /// The ball's WORLD radius: the authored radius, times the bloom it is growing through,
        /// times whatever scale its parents carry, so a mouth parented under something that scales
        /// shrinks with it rather than keeping a stale size.
        /// </summary>
        public float Radius => _radius * _bloom * Mathf.Abs(transform.lossyScale.x);

        /// <summary>True once the sphere has fully bloomed — only then does it carry anyone.</summary>
        public bool IsOpen => !_retiring && _bloom >= 1f && _partner && _partner._bloom >= 1f;

        /// <summary>True once <see cref="Retire"/> has run — a closing mouth is never a passage.</summary>
        public bool IsRetiring => _retiring;

        /// <summary>The domain that owns the mouth (its rim's hue; its lock, if locked).</summary>
        public Domains Domain
        {
            get
            {
                var owner = _settings.Owner;
                // IVesselStatus.Domain reads Player and logs when there is none; ask first.
                return owner != null && owner.Player != null ? owner.Domain : _settings.Domain;
            }
        }

        /// <summary>The pilot whose mouth this is, or null for an unowned mouth.</summary>
        public IVesselStatus Owner => _settings.Owner;

        // The domain the rim tint was last painted for, so an owner's domain change repaints it.
        Domains _tintDomain;
        bool _tintPainted;

        /// <summary>Does this mouth carry only its own domain?</summary>
        public bool DomainLocked => _settings.DomainLocked;

        /// <summary>Who this mouth may carry — and draw carried through in a view of it.</summary>
        public IReadOnlyList<IPlayer> Players => _players;

        /// <summary>
        /// Set each frame by <see cref="WormholeView"/>: the viewer on this machine may not thread
        /// this mouth, so it shows no view through — only its domain-coloured outline.
        /// </summary>
        public bool Sealed { get; set; }

        /// <summary>The sphere's own renderer, hidden by <see cref="WormholeView"/> inside every
        /// wormhole render (its surface samples the very targets those renders draw into).</summary>
        public MeshRenderer Surface => _renderer;

        /// <summary>Furthest a viewer may be for this mouth to get an exact view.</summary>
        public float ExactRange => _settings.ExactRange;

        /// <summary>Distance over which the exact view crossfades to the panorama.</summary>
        public float ExactFadeBand => Mathf.Max(1f, _settings.ExactFadeBand);

        /// <summary>How much of this frame the surface shows the exact view, 0..1.</summary>
        public float ExactBlend { get; set; }

        /// <summary>Set by <see cref="WormholeView"/> when something on screen is looking through
        /// the PARTNER, i.e. at this mouth's panorama.</summary>
        public bool PanoramaWanted { get; set; }

        /// <summary>All six faces have been captured at least once.</summary>
        public bool PanoramaReady => _panoramaFacesCaptured == (1 << WormholeGeometry.FaceCount) - 1;

        // ---- build ---------------------------------------------------------------------------

        /// <summary>Lay the mouth. Call immediately after AddComponent; it blooms in from nothing.</summary>
        public void Build(Settings settings, IReadOnlyList<IPlayer> players, float radius)
        {
            _settings = settings;
            _players = players;
            _radius = Mathf.Max(1f, radius);
            _bloom = 0f;

            var surface = new GameObject("Surface");
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = SharedSphereMesh();
            _renderer = surface.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = settings.SurfaceMaterial;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _surface = surface.transform;
            _block = new MaterialPropertyBlock();

            if (!_renderer.sharedMaterial)
                CSDebug.LogWarning($"[Wormhole] {name}: built with no surface material - the mouth " +
                                   "will carry pilots but draw nothing.", this);

            // GPU resources only exist in play: a mouth built in edit mode (a test, an editor
            // tool) must not leave render targets behind.
            if (Application.isPlaying) BuildEyes();

            ApplyBloom();
            if (!Live.Contains(this)) Live.Add(this);
        }

        /// <summary>Join the two ends. Symmetric, so one call wires both.</summary>
        public static void Pair(WormholeMouth a, WormholeMouth b)
        {
            if (!a || !b || a == b) return;
            a._partner = b;
            b._partner = a;
        }

        /// <summary>
        /// Wither this mouth away over <paramref name="seconds"/> and destroy it — the only removal
        /// path, and only ever caused by a player's act (a Butterfly folding again replaces its
        /// pair) or by the world it belongs to going away. It unpairs at once, so a half-retired
        /// pair can never carry anyone, and an unpaired mouth reads as sealed while it shrinks.
        /// </summary>
        public void Retire(float seconds)
        {
            if (_retiring) return;
            _retiring = true;
            _retireSeconds = Mathf.Max(0.05f, seconds);
            if (_partner && _partner._partner == this) _partner._partner = null;
            _partner = null;
        }

        /// <summary>Would this mouth carry <paramref name="vessel"/>? Its domain, if it is locked.</summary>
        public bool CanCarry(IVessel vessel)
        {
            if (vessel == null) return false;
            if (!_settings.DomainLocked) return true;
            var status = vessel.VesselStatus;
            if (status == null) return false;
            // The owner is never locked out of its own wormhole, whatever the domain reads say.
            if (_settings.Owner != null && ReferenceEquals(status, _settings.Owner)) return true;
            // IVesselStatus.Domain reads Player and logs when there is none; ask first.
            return status.Player != null && status.Domain == Domain;
        }

        void BuildEyes()
        {
            _exactEye = MakeEye("ExactEye", out _exactEyeData);
            _panoramaEye = MakeEye("PanoramaEye", out _panoramaEyeData);
            // The panorama is a fixed 90° square frustum per face.
            _panoramaEye.fieldOfView = 90f;
            _panoramaEye.aspect = 1f;
        }

        /// <summary>
        /// A camera attached to this mouth: never tagged MainCamera, left DISABLED and stepped by
        /// hand into a render target — outside the speed tunnel, the graphics-settings push and
        /// <c>Camera.main</c> by construction (the off-screen camera rules, REAR_VIEW.md
        /// §3.1.1). No post-processing: its picture is composited INTO the world and the gameplay
        /// camera's own post runs over it once, with everything else.
        /// </summary>
        Camera MakeEye(string eyeName, out UnityEngine.Rendering.Universal.UniversalAdditionalCameraData data)
        {
            var go = new GameObject(eyeName);
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.useOcclusionCulling = false;
            cam.allowMSAA = false;
            OffscreenCameraSetup.AdoptGameCameraImage(cam, postProcessing: false,
                                                      antiAliasing: false, shadows: false);
            data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            return cam;
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (_partner && _partner._partner == this) _partner._partner = null;
            _partner = null;
            ReleaseExact();
            ReleasePanorama();
        }

        // ---- per frame -----------------------------------------------------------------------

        void Update()
        {
            if (_retiring)
            {
                _bloom = Mathf.MoveTowards(_bloom, 0f, Time.deltaTime / _retireSeconds);
                ApplyBloom();
                if (_bloom <= 0f) Destroy(gameObject);
                return;
            }

            if (_bloom < 1f)
            {
                float seconds = Mathf.Max(0.01f, _settings.BloomSeconds);
                _bloom = Mathf.MoveTowards(_bloom, 1f, Time.deltaTime / seconds);
                ApplyBloom();
            }

            if (_flare > 0f)
                _flare = Mathf.MoveTowards(_flare, 0f, Time.deltaTime / FlareSeconds);

            if (!IsOpen || _players == null) return;

            Vector3 centre = Centre;
            float radius = Radius;

            for (int i = 0; i < _players.Count; i++)
            {
                var vessel = _players[i]?.Vessel;
                if (vessel == null) continue;

                // Only the machine that OWNS a vessel decides that vessel moved: the pose write
                // replicates, so a peer acting too would be two machines teleporting one ship.
                if (!vessel.IsNetworkOwner) continue;
                if (!CanCarry(vessel)) continue;

                Vector3 cur = vessel.Transform.position;
                bool first = !_lastPos.TryGetValue(vessel, out var prev);
                _lastPos[vessel] = cur;
                if (first) continue;    // two samples are needed to test an entry

                // Entering = starting outside and reaching the ball. A vessel that starts INSIDE
                // (one just carried into this mouth from the other) is never taken back until it
                // has flown out — the geometric latch the fold gate's arming is, with no clock.
                if (!WormholeGeometry.SegmentEntersBall(prev, cur, centre, radius)) continue;

                Transit(vessel, cur);
                return;                 // one transit per mouth per frame
            }

            // A despawned vessel leaves its sample behind, and a wormhole outlives many of them.
            if (_lastPos.Count > _players.Count) PruneDead();
        }

        /// <summary>
        /// Put the pilot through: the same position relative to the partner as it had relative to
        /// this mouth. Rotation and speed untouched — a wormhole moves you, it does not fly you.
        /// Both ends are re-seeded at where the vessel actually landed, so neither reads the jump
        /// itself as a crossing.
        /// </summary>
        void Transit(IVessel vessel, Vector3 cur)
        {
            var partner = _partner;
            if (!partner) return;

            Vector3 exit = WormholeGeometry.Through(cur, Centre, partner.Centre);
            vessel.SetPose(new Pose(exit, vessel.Transform.rotation));

            Vector3 now = vessel.Transform.position;
            _lastPos[vessel] = now;
            partner._lastPos[vessel] = now;

            _flare = 1f;
            partner._flare = 1f;

            if (!_settings.TransitEvent.IsNull && CosmicShore.Core.AudioSystem.Instance)
                CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(_settings.TransitEvent, now);
        }

        void PruneDead()
        {
            _scratchDead.Clear();
            foreach (var key in _lastPos.Keys)
            {
                bool alive = false;
                for (int i = 0; i < _players.Count && !alive; i++)
                    alive = _players[i] != null && ReferenceEquals(_players[i].Vessel, key);
                if (!alive) _scratchDead.Add(key);
            }
            for (int i = 0; i < _scratchDead.Count; i++) _lastPos.Remove(_scratchDead[i]);
        }

        // ---- transit resolution, asked on EVERY peer -----------------------------------------

        /// <summary>
        /// Was the jump <paramref name="from"/> → <paramref name="to"/> a wormhole transit? Asked
        /// by <see cref="TeleportContinuity"/> from inside every pose write, on every machine —
        /// only the owner runs the detector, everyone else only sees the pose arrive.
        ///
        /// <para>Tolerant, as the fold gate's resolver is: on a peer <paramref name="from"/> is an
        /// interpolated replica pose that may sit a little short of the mouth, so the test asks
        /// that the jump is the pair's translation to within one radius, from somewhere near the
        /// near ball. Two mouths are many radii apart, so no other teleport lands inside that.</para>
        ///
        /// <para>The points returned are where the ribbon is cut: on the near sphere where the
        /// vessel went in, and the same spot on the far sphere.</para>
        /// </summary>
        public static bool TryResolveTransit(Vector3 from, Vector3 to, out WormholeMouth near,
                                             out Vector3 departPoint, out Vector3 arrivePoint)
        {
            near = null;
            departPoint = arrivePoint = default;
            float bestErr = float.MaxValue;

            for (int i = 0; i < Live.Count; i++)
            {
                var m = Live[i];
                if (!m) continue;
                var p = m._partner;
                if (!p) continue;

                Vector3 c = m.Centre;
                float r = m.Radius;
                if (r <= 0f) continue;
                if ((from - c).sqrMagnitude > 9f * r * r) continue;

                Vector3 mapped = WormholeGeometry.Through(from, c, p.Centre);
                float err = (mapped - to).sqrMagnitude;
                if (err > r * r || err >= bestErr) continue;

                bestErr = err;
                near = m;
                departPoint = WormholeGeometry.NearestSurfacePoint(from, c, r);
                arrivePoint = WormholeGeometry.Through(departPoint, c, p.Centre);
            }
            return near != null;
        }

        // ---- the exact view ------------------------------------------------------------------

        /// <summary>
        /// Render what <paramref name="view"/> would see through this mouth if the two were one
        /// place, into this mouth's exact target, cropped to the mouth's footprint on screen.
        /// <paramref name="maxRenderScale"/> is the device tier's ceiling. Returns false (and the
        /// surface falls back to the panorama) when there is nothing to draw.
        /// </summary>
        public bool RenderExact(Camera view, float maxRenderScale)
        {
            var partner = _partner;
            if (!partner || !_exactEye || !view) return false;

            Rect footprint = Footprint(view);
            if (footprint.width * view.pixelWidth < 1f || footprint.height * view.pixelHeight < 1f)
                return false;
            if (!EnsureExactTarget(view, footprint, maxRenderScale)) return false;

            Transform eye = view.transform;
            Vector3 pos = WormholeGeometry.Through(eye.position, Centre, partner.Centre);
            _exactEye.transform.SetPositionAndRotation(pos, eye.rotation);

            // WHAT it sees follows the live camera every frame, minus the UI layer (which would
            // otherwise draw the HUD inside the window one frame stale).
            _exactEye.clearFlags = view.clearFlags;
            _exactEye.backgroundColor = view.backgroundColor;
            _exactEye.cullingMask = WithoutUI(view.cullingMask);

            // The SAME projection, or the picture would not register with the screen it is
            // sampled at; field of view copied live so the speed tunnel narrows it too.
            _exactEye.fieldOfView = view.fieldOfView;
            _exactEye.nearClipPlane = view.nearClipPlane;
            _exactEye.farClipPlane = view.farClipPlane;
            _exactEye.aspect = view.aspect;
            _exactEye.allowHDR = view.allowHDR;
            _exactEye.ResetProjectionMatrix();

            // Everything between the carried vantage and the far ball is on the near side of the
            // pair and must not appear in the window: an oblique near plane tangent to the far
            // ball, normal pointing away from the vantage.
            if (WormholeGeometry.TryNearCapPlane(pos, partner.Centre, partner.Radius,
                                                 out var normal, out var point))
            {
                Matrix4x4 worldToCamera = _exactEye.worldToCameraMatrix;
                Vector3 camPos = worldToCamera.MultiplyPoint(point);
                Vector3 camNormal = worldToCamera.MultiplyVector(normal);
                float camDist = -Vector3.Dot(camPos, camNormal) + ClipPlaneOffset;
                if (Mathf.Abs(camDist) > MinObliqueDistance)
                    _exactEye.projectionMatrix = _exactEye.CalculateObliqueMatrix(
                        new Vector4(camNormal.x, camNormal.y, camNormal.z, camDist));
            }

            // After the oblique plane, which only rewrites the z row.
            _exactEye.projectionMatrix = WormholeGeometry.Crop(_exactEye.projectionMatrix, footprint);

            if (_exactEyeData != null)
            {
                _exactEyeData.renderPostProcessing = false;
                _exactEyeData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            }
            _exactEye.targetTexture = _exactTex;
            _exactEye.Render();

            // The surface samples at its screen position; map that into the footprint the
            // target covers: uv' = (uv - min) / size.
            _exactUV = new Vector4(1f / footprint.width, 1f / footprint.height,
                                   -footprint.xMin / footprint.width, -footprint.yMin / footprint.height);
            return true;
        }

        /// <summary>
        /// The sphere's rectangle of <paramref name="view"/>'s viewport (0..1), padded and clamped:
        /// the projected box around the ball, so it is conservative. A corner at or behind the near
        /// plane means the sphere wraps the camera, and then the whole screen may show it.
        /// </summary>
        Rect Footprint(Camera view)
        {
            Vector3 c = Centre;
            float r = Radius;
            float near = view.nearClipPlane;
            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -r : r, (i & 2) == 0 ? -r : r, (i & 4) == 0 ? -r : r);
                Vector3 vp = view.WorldToViewportPoint(corner);
                if (vp.z <= near) return new Rect(0f, 0f, 1f, 1f);
                xMin = Mathf.Min(xMin, vp.x);
                yMin = Mathf.Min(yMin, vp.y);
                xMax = Mathf.Max(xMax, vp.x);
                yMax = Mathf.Max(yMax, vp.y);
            }

            float padX = FootprintPadPixels / Mathf.Max(1, view.pixelWidth);
            float padY = FootprintPadPixels / Mathf.Max(1, view.pixelHeight);
            return Rect.MinMaxRect(Mathf.Clamp01(xMin - padX), Mathf.Clamp01(yMin - padY),
                                   Mathf.Clamp01(xMax + padX), Mathf.Clamp01(yMax + padY));
        }

        bool EnsureExactTarget(Camera view, Rect footprint, float maxRenderScale)
        {
            float scale = Mathf.Min(Mathf.Clamp(_settings.ExactRenderScale, 0.25f, 1f), maxRenderScale);
            int capW = Mathf.Max(WormholeGeometry.TexelQuantum, Mathf.RoundToInt(view.pixelWidth * scale));
            int capH = Mathf.Max(WormholeGeometry.TexelQuantum, Mathf.RoundToInt(view.pixelHeight * scale));
            int needW = Mathf.Clamp(Mathf.CeilToInt(footprint.width * view.pixelWidth * scale), WormholeGeometry.TexelQuantum, capW);
            int needH = Mathf.Clamp(Mathf.CeilToInt(footprint.height * view.pixelHeight * scale), WormholeGeometry.TexelQuantum, capH);
            var format = TargetFormat(view);

            bool fits = _exactTex != null && _exactFormat == format
                        && WormholeGeometry.TargetFits(_exactTex.width, _exactTex.height, needW, needH);
            if (_exactTex != null && !fits) ReleaseExact();
            if (_exactTex != null) return true;

            _exactTex = new RenderTexture(WormholeGeometry.TargetSize(needW, capW),
                                          WormholeGeometry.TargetSize(needH, capH), 24, format)
            {
                name = $"{name} ExactView",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            // Created outright: the surface samples it in the same frame.
            _exactTex.Create();
            _exactFormat = format;
            return true;
        }

        void ReleaseExact()
        {
            if (_exactEye) _exactEye.targetTexture = null;
            if (_exactTex == null) return;
            _exactTex.Release();
            Destroy(_exactTex);
            _exactTex = null;
        }

        // ---- the panorama --------------------------------------------------------------------

        /// <summary>
        /// Capture the next face of this mouth's surroundings — the picture the PARTNER projects.
        /// One face per call, so a whole panorama is spread across frames; a mouth's surroundings
        /// are refreshed every six of its turns.
        /// </summary>
        public void RenderNextPanoramaFace(Camera view)
        {
            if (!_panoramaEye || !view) return;
            if (!EnsurePanorama(view)) return;

            int face = _nextPanoramaFace;
            _nextPanoramaFace = (_nextPanoramaFace + 1) % WormholeGeometry.FaceCount;

            _panoramaEye.transform.SetPositionAndRotation(Centre, WormholeGeometry.FaceRotation(face));
            _panoramaEye.clearFlags = view.clearFlags;
            _panoramaEye.backgroundColor = view.backgroundColor;
            _panoramaEye.cullingMask = WithoutUI(view.cullingMask);
            _panoramaEye.nearClipPlane = Mathf.Max(0.1f, view.nearClipPlane);
            _panoramaEye.farClipPlane = view.farClipPlane;
            _panoramaEye.allowHDR = view.allowHDR;
            _panoramaEye.fieldOfView = 90f;
            _panoramaEye.aspect = 1f;
            _panoramaEye.ResetProjectionMatrix();

            if (_panoramaEyeData != null)
            {
                _panoramaEyeData.renderPostProcessing = false;
                _panoramaEyeData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            }
            _panoramaEye.targetTexture = _panoramaFace;
            _panoramaEye.Render();

            // Into the face's slice of the array. Face images and the shader's face table are
            // both this file's (WormholeGeometry), so no cubemap orientation convention is
            // involved anywhere.
            Graphics.CopyTexture(_panoramaFace, 0, 0, _panorama, face, 0);
            _panoramaFacesCaptured |= 1 << face;
        }

        bool EnsurePanorama(Camera view)
        {
            // A 2D target copied into an array slice is a copy between texture TYPES.
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) == 0) return false;

            int size = Mathf.Clamp(_settings.PanoramaFaceSize, 64, 1024);
            var format = TargetFormat(view);
            if (_panorama != null && _panorama.width == size && _panoramaFormat == format)
                return true;

            ReleasePanorama();
            _panorama = new RenderTexture(size, size, 0, format)
            {
                name = $"{name} Panorama",
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = WormholeGeometry.FaceCount,
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            _panorama.Create();
            _panoramaFace = new RenderTexture(size, size, 24, format)
            {
                name = $"{name} PanoramaFace",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
            };
            _panoramaFace.Create();
            _panoramaFormat = format;
            _panoramaFacesCaptured = 0;
            _nextPanoramaFace = 0;
            return true;
        }

        void ReleasePanorama()
        {
            if (_panoramaEye) _panoramaEye.targetTexture = null;
            if (_panorama != null) { _panorama.Release(); Destroy(_panorama); _panorama = null; }
            if (_panoramaFace != null) { _panoramaFace.Release(); Destroy(_panoramaFace); _panoramaFace = null; }
            _panoramaFacesCaptured = 0;
        }

        // ---- the surface ---------------------------------------------------------------------

        /// <summary>Push this frame's state to the sphere: where it is, the exact view and how much
        /// of it to show, and the PARTNER's panorama for every other angle.</summary>
        public void ApplySurface()
        {
            if (!_renderer) return;
            _renderer.GetPropertyBlock(_block);
            Vector3 c = Centre;
            _block.SetVector(SphereId, new Vector4(c.x, c.y, c.z, Radius));
            if (_exactTex != null) _block.SetTexture(ExactTexId, _exactTex);
            _block.SetVector(ExactUVId, _exactUV);
            _block.SetFloat(ExactBlendId, _exactTex != null ? Mathf.Clamp01(ExactBlend) : 0f);

            var partner = _partner;
            bool ready = partner && partner._panorama != null && partner.PanoramaReady;
            if (ready) _block.SetTexture(PanoramaId, partner._panorama);
            _block.SetFloat(PanoramaReadyId, ready ? 1f : 0f);
            _block.SetFloat(FlareId, _flare);
            // SetColor, not SetVector: the theme authors sRGB, and SetColor converts to the
            // project's linear space. Alpha 1 says "a tint is set"; 0 keeps the material's rim.
            var domain = Domain;
            if (_settings.Owner != null && (!_tintPainted || domain != _tintDomain))
            {
                // The owner's domain is live, so its hue is too.
                _settings.RimTint = ToyFactory.DomainAccentColor(_settings.Theme, domain);
                _tintDomain = domain;
                _tintPainted = true;
            }
            var tint = _settings.RimTint;
            _block.SetColor(RimTintId, new Color(tint.r, tint.g, tint.b, tint.a > 0f ? 1f : 0f));
            _block.SetFloat(SealedId, Sealed || !partner ? 1f : 0f);
            _renderer.SetPropertyBlock(_block);
        }

        void ApplyBloom()
        {
            if (_surface) _surface.localScale = Vector3.one * (_radius * 2f * _bloom);
        }

        const float FlareSeconds = 0.6f;

        static int WithoutUI(int mask)
        {
            int ui = LayerMask.NameToLayer("UI");
            return ui >= 0 ? mask & ~(1 << ui) : mask;
        }

        /// <summary>
        /// HDR only when the PIPELINE renders HDR too — a device tier that turned the URP asset's
        /// HDR off must not pay for HDR targets here.
        /// </summary>
        static RenderTextureFormat TargetFormat(Camera view)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool hdr = view.allowHDR && (!pipeline || pipeline.supportsHDR)
                       && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            return hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }

        // ---- the sphere mesh -----------------------------------------------------------------

        static Mesh s_sphere;

        /// <summary>A unit-diameter UV sphere, shared by every mouth (size is the transform's).</summary>
        static Mesh SharedSphereMesh()
        {
            if (s_sphere) return s_sphere;
            const int rings = 48, segments = 96;
            var verts = new Vector3[(rings + 1) * (segments + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            int v = 0;
            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;
                for (int s = 0; s <= segments; s++, v++)
                {
                    float theta = 2f * Mathf.PI * s / segments;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi),
                                        Mathf.Sin(phi) * Mathf.Sin(theta));
                    verts[v] = n * 0.5f;
                    normals[v] = n;
                    uvs[v] = new Vector2((float)s / segments, 1f - (float)r / rings);
                }
            }

            var tris = new int[rings * segments * 6];
            int t = 0;
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s, b = a + segments + 1;
                // Wound so the OUTSIDE faces the camera (Unity: clockwise = front).
                tris[t++] = a; tris[t++] = a + 1; tris[t++] = b;
                tris[t++] = b; tris[t++] = a + 1; tris[t++] = b + 1;
            }

            s_sphere = new Mesh { name = "WormholeSphere", hideFlags = HideFlags.HideAndDontSave };
            s_sphere.vertices = verts;
            s_sphere.normals = normals;
            s_sphere.uv = uvs;
            s_sphere.triangles = tris;
            s_sphere.RecalculateBounds();
            return s_sphere;
        }
    }
}
