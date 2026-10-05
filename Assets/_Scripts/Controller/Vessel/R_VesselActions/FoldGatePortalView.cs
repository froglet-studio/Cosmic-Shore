using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The far side of a Butterfly fold gate, drawn INSIDE its ring — the half of a seamless
    /// transit that happens before the pilot gets there (<c>BUTTERFLY_FOLD.md</c> § "Seamless
    /// transit").
    ///
    /// <para><b>What it renders.</b> The world as the gameplay camera would see it if the two
    /// mouths of a pair were one: the camera's pose carried through the pair
    /// (<see cref="FoldGateGeometry.Through"/> — a pure translation, since both ends share one
    /// axis), with the camera's own projection, and with an OBLIQUE near plane laid on the far
    /// mouth so nothing standing between that vantage and the far ring can get into the picture.
    /// The ring's window surface samples the result at its own screen position
    /// (<c>FoldGatePortal.shader</c>), so each pixel of the window is exactly the pixel the camera
    /// would see through it on the far side. The ship flying into the window is therefore already
    /// in the place it is about to be, and when the camera follows it through
    /// (<c>CustomCameraController.CarryThroughPortal</c>) it lands on the vantage this render was
    /// drawn from — which is the whole trick.</para>
    ///
    /// <para><b>Only a gate the viewer's DOMAIN may thread shows a window.</b> A view through the
    /// ring is a promise that you can go there; a rival sees an ordinary ring in the Butterfly's
    /// colour, which is what it is to them.</para>
    ///
    /// <para><b>It is not a second gameplay camera</b>, for the same four reasons as the Serpent's
    /// scope and the connecting panel's preview (Docs/REAR_VIEW.md §3.1.1): created at runtime,
    /// never tagged MainCamera, left DISABLED and stepped by hand, rendering only into a
    /// <see cref="RenderTexture"/>. It is outside the speed tunnel, the graphics-settings push,
    /// the background-colour push and <c>Camera.main</c> by construction — and it copies the
    /// live gameplay camera's field of view every frame instead, so the speed tunnel's narrowing
    /// reaches the window through the camera it is mimicking.</para>
    ///
    /// <para><b>It renders WITHOUT post-processing, and that is the one deliberate exception to
    /// <see cref="OffscreenCameraSetup"/>'s rule.</b> Every other off-screen camera in the project
    /// draws a picture that is shown AS a picture (a UI window), so it must be tonemapped itself.
    /// This one is composited INTO the world and then post-processed by the gameplay camera along
    /// with everything else; tonemapping it here as well would tonemap it twice. It renders HDR so
    /// the emissive world arrives intact for that single pass.</para>
    ///
    /// <para><b>A ship half-way through is drawn on BOTH sides.</b> The window covers everything
    /// beyond the near mouth, and the far render only contains what is beyond the far mouth — so
    /// a hull straddling a mouth would be sliced: its nose swallowed by the window before the
    /// transit, its tail missing after it. For the length of the straddle the followed ship is
    /// posed on the side of the pair each view is drawing — mapped through for the far render
    /// before a transit, mapped back for the gameplay camera's render during a carry — and put
    /// back before anything else runs. That is the "clone" every portal needs, done without a
    /// clone: the moves bracket single renders (the far render inside this driver's LateUpdate;
    /// the gameplay render between <c>beginCameraRendering</c> and <c>endCameraRendering</c>),
    /// so no physics step, network sample or ribbon update ever sees the borrowed pose.</para>
    ///
    /// <para><b>It renders only its own FOOTPRINT.</b> The window is usually a small disc on
    /// screen, and every pixel of the far-side render outside it was being drawn to be thrown
    /// away. So the window's on-screen rectangle is measured each frame (the disc's bounding
    /// square, projected), the far-side projection is CROPPED to exactly that rectangle - the
    /// same frustum, with its side planes pulled in, so culling drops everything the window cannot
    /// show as well - and the target is sized to the rectangle's pixels rather than the screen's.
    /// The shader maps its screen position into that rectangle (<c>_FoldGatePortalUV</c>). A
    /// distant gate now costs a render the size of a thumbnail; one that fills the screen (the
    /// approach, the carry through) costs what it always did, capped per device tier by
    /// <c>PlatformProfileSO.FoldGateWindowMaxRenderScale</c> (0.5 on MobileLow, no cap elsewhere).</para>
    ///
    /// <para><b>The cost, stated.</b> One extra render of the world per frame, of the window's
    /// footprint at <c>portalWindowRenderScale</c> of the gameplay camera's resolution, with no
    /// shadows, no anti-aliasing and no post — paid only while a threadable gate is on screen and
    /// within <c>portalWindowRange</c>, and for at most ONE gate at a time (the nearest). It
    /// refreshes every frame rather than at a capped rate: a window that lagged the camera by even
    /// a frame would shear against the ring as the pilot turned, which is exactly the seam it
    /// exists to hide. No colliders, no prisms, no per-prism anything.</para>
    /// </summary>
    public static class FoldGatePortalView
    {
        static readonly int PortalTexId = Shader.PropertyToID("_FoldGatePortalTex");
        static readonly int PortalUVId = Shader.PropertyToID("_FoldGatePortalUV");
        static readonly Plane[] FrustumPlanes = new Plane[6];

        /// <summary>Screen UV -> target UV for a target that covers the whole screen.</summary>
        static readonly Vector4 IdentityUV = new(1f, 1f, 0f, 0f);
        static readonly Rect FullViewport = new(0f, 0f, 1f, 1f);

        /// <summary>Pixels of margin around the measured footprint, so the bilinear tap at the
        /// window's rim never reads past the rendered rectangle.</summary>
        const float FootprintPadPixels = 3f;

        /// <summary>The target is sized in steps of this many texels, so a footprint growing a few
        /// pixels a frame does not reallocate it every frame.</summary>
        const int TexelQuantum = 32;

        /// <summary>Headroom a reallocated target takes over the footprint it was sized for, so an
        /// approaching gate grows into it instead of reallocating at every step.</summary>
        const float GrowHeadroom = 1.25f;

        /// <summary>A target this many times larger than the footprint needs (on both axes) is
        /// reallocated smaller - the receding gate stops paying for the size it had up close.</summary>
        const float ShrinkSlack = 1.6f;

        /// <summary>
        /// How far toward the vantage the oblique plane is pulled from the far mouth, world units —
        /// a sliver, so geometry lying exactly on the plane does not flicker between kept and
        /// clipped from one frame to the next.
        /// </summary>
        const float ClipPlaneOffset = 0.05f;

        /// <summary>
        /// Below this camera-space distance to the far plane an oblique projection degenerates
        /// (its far plane swings through the view), so the ordinary projection is used instead.
        /// Only reached while the camera is inside the mouth itself, where the window is being
        /// handed over anyway.
        /// </summary>
        const float MinObliqueDistance = 0.2f;

        static Camera _camera;
        static UnityEngine.Rendering.Universal.UniversalAdditionalCameraData _cameraData;
        static RenderTexture _texture;
        // The format the target was REQUESTED in. RenderTexture.format reports what DefaultHDR
        // resolved to on this device, so comparing against it would never match and would
        // reallocate the target every frame.
        static RenderTextureFormat _textureFormat;
        static GameObject _host;

        static Transform _viewerKey;
        static VesselStatus _viewerStatus;
        static Transform _subjectKey;
        static Transform _subjectRoot;
        static float _subjectRadius;

        // The borrowed pose for the gameplay camera's render during a carry (see the class
        // summary on why a straddling ship is drawn on both sides).
        static Camera _mainView;
        static FoldGate _carriedGate;
        static bool _mainMoved;
        static Vector3 _mainSaved;

        /// <summary>The gate whose window is currently live, or null.</summary>
        public static FoldGate Shown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // Statics survive play-mode exit in the editor.
            _camera = null;
            _cameraData = null;
            _texture = null;
            // A full-screen target is the identity mapping; set it so a window that draws before
            // the first far-side render samples sensibly rather than at uv 0 (an unset global
            // vector is zero).
            Shader.SetGlobalVector(PortalUVId, IdentityUV);
            _viewerKey = null;
            _viewerStatus = null;
            _subjectKey = null;
            _subjectRoot = null;
            _subjectRadius = 0f;
            _mainView = null;
            _carriedGate = null;
            _mainMoved = false;
            Shown = null;

            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;

            // HideInHierarchy (NOT HideAndDontSave - that exempts the object from play-mode-exit
            // cleanup), the pattern PrismOcclusionCorridor and VesselPlacementView use.
            _host = new GameObject("[FoldGatePortalView]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Driver>();
        }

        /// <summary>
        /// Ordered after every camera: the far-side vantage is derived from the gameplay camera's
        /// FINAL pose this frame (CustomCameraController poses in its own LateUpdate), and after
        /// the occlusion corridor publishes (order 10000), whose target this borrows for one render
        /// and hands back.
        /// </summary>
        [DefaultExecutionOrder(10100)]
        sealed class Driver : MonoBehaviour
        {
            void LateUpdate() => Tick();
            void OnDestroy() => Release();
        }

        static void Tick()
        {
            // A borrowed pose must never outlive the render it was borrowed for. endCameraRendering
            // restores it; this is the net under that, should a render have been skipped.
            RestoreMainPose();
            _carriedGate = null;
            _mainView = null;

            var live = FoldGate.Live;

            if (live.Count == 0)
            {
                Shown = null;
                // Nothing that could show a window exists; do not hold a screen-sized HDR target.
                if (_texture != null) ReleaseTexture();
                return;
            }

            var controller = ResolveController();
            var view = controller != null ? controller.Camera : null;
            FoldGate pick = null;
            bool carried = false;
            if (view != null && view.isActiveAndEnabled)
                pick = Select(controller, view, out carried);

            for (int i = 0; i < live.Count; i++)
            {
                var gate = live[i];
                if (!gate) continue;
                if (gate != pick) { gate.SetWindow(false, 0f); continue; }

                // While the camera is being carried through THIS gate the window is the only place
                // the pilot's ship can be seen, so it is fully opaque at once; otherwise it fades
                // in as the gate comes into range.
                float blend = carried
                    ? 1f
                    : Mathf.MoveTowards(gate.WindowBlend, 1f, Time.deltaTime / gate.WindowFadeSeconds);
                gate.SetWindow(true, blend);
            }

            Shown = pick;
            if (pick == null) return;
            ResolveSubject(controller);
            if (!Render(pick, view)) { pick.SetWindow(false, 0f); return; }

            if (carried) { _carriedGate = pick; _mainView = view; }
        }

        /// <summary>
        /// Which gate gets the one far-side render. The one the camera is being carried through
        /// always wins; otherwise the nearest paired gate the viewer's domain may thread that is
        /// on screen and in range.
        /// </summary>
        static FoldGate Select(CustomCameraController controller, Camera view, out bool carried)
        {
            carried = false;
            var live = FoldGate.Live;

            if (controller.IsCarryingThroughPortal)
            {
                Vector3 mouth = controller.CarryMouthCentre;
                for (int i = 0; i < live.Count; i++)
                {
                    var g = live[i];
                    if (!g || !g.Partner || g.IsRetiring || !g.Window) continue;
                    if ((g.Centre - mouth).sqrMagnitude > 1f) continue;
                    carried = true;
                    return g;
                }
            }

            if (!TryResolveViewerDomain(controller, out var domain)) return null;

            GeometryUtility.CalculateFrustumPlanes(view, FrustumPlanes);
            Vector3 eye = view.transform.position;
            float nearGuard = view.nearClipPlane * 4f;

            FoldGate best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < live.Count; i++)
            {
                var g = live[i];
                if (!g || g.IsRetiring || !g.Window) continue;
                var partner = g.Partner;
                if (!partner || partner.IsRetiring) continue;
                if (g.PlacerDomain != domain) continue;

                float sqr = (g.Centre - eye).sqrMagnitude;
                if (sqr > g.WindowRange * g.WindowRange || sqr >= bestSqr) continue;

                // A camera standing IN the plane would look along the window edge-on and through
                // its near clip; there is nothing to show from there.
                if (Mathf.Abs(FoldGateGeometry.Axial(eye, g.Centre, g.Axis)) < nearGuard) continue;

                float r = g.RingRadius;
                if (!GeometryUtility.TestPlanesAABB(FrustumPlanes,
                        new Bounds(g.Centre, new Vector3(r, r, r) * 2f))) continue;

                best = g;
                bestSqr = sqr;
            }
            return best;
        }

        static bool Render(FoldGate gate, Camera view)
        {
            var partner = gate.Partner;
            if (!partner) return false;

            // Only the window's own rectangle of the screen is rendered (see the class summary).
            Rect footprint = WindowFootprint(gate, view);
            if (footprint.width * view.pixelWidth < 1f || footprint.height * view.pixelHeight < 1f)
                return false;
            if (!EnsureCamera(view, footprint, gate.WindowRenderScale)) return false;

            Vector3 near = gate.Centre, far = partner.Centre;
            Vector3 nearAxis = gate.Axis, farAxis = partner.Axis;

            // --- the vantage: the gameplay camera, carried through the pair -----------------
            Transform eye = view.transform;
            Vector3 pos = FoldGateGeometry.Through(eye.position, near, nearAxis, far, farAxis);
            _camera.transform.SetPositionAndRotation(pos, eye.rotation);

            // WHAT it sees follows the live camera every frame (a culling-mask or clear change on
            // the gameplay camera must reach the window too), minus the UI layer, which would
            // otherwise draw the HUD inside the window one frame stale.
            _camera.clearFlags = view.clearFlags;
            _camera.backgroundColor = view.backgroundColor;
            int ui = LayerMask.NameToLayer("UI");
            _camera.cullingMask = ui >= 0 ? view.cullingMask & ~(1 << ui) : view.cullingMask;

            // The SAME projection, or the picture would not register with the screen it is sampled
            // at. Field of view is copied live, so the speed tunnel narrows the window with the view.
            _camera.fieldOfView = view.fieldOfView;
            _camera.nearClipPlane = view.nearClipPlane;
            _camera.farClipPlane = view.farClipPlane;
            _camera.aspect = view.aspect;
            _camera.ResetProjectionMatrix();

            // --- the oblique near plane on the far mouth -----------------------------------
            // Normal pointing AWAY from the vantage, so what is kept is the far side of the mouth.
            float side = Vector3.Dot(farAxis, far - pos);
            Vector3 normal = side >= 0f ? farAxis : -farAxis;
            Matrix4x4 worldToCamera = _camera.worldToCameraMatrix;
            Vector3 camPos = worldToCamera.MultiplyPoint(far);
            Vector3 camNormal = worldToCamera.MultiplyVector(normal);
            float camDist = -Vector3.Dot(camPos, camNormal) + ClipPlaneOffset;
            if (Mathf.Abs(camDist) > MinObliqueDistance)
                _camera.projectionMatrix = _camera.CalculateObliqueMatrix(
                    new Vector4(camNormal.x, camNormal.y, camNormal.z, camDist));

            // --- crop to the footprint ------------------------------------------------------
            // After the oblique plane, which only rewrites the z row: the crop rewrites x and y,
            // so the two compose without disturbing each other.
            CropProjection(footprint);

            // --- render -------------------------------------------------------------------
            // Neither window may appear in its own render: this one would sample the texture it is
            // being drawn into, and the far one sits on the clip plane and would flicker there.
            var nearWindow = gate.Window;
            var farWindow = partner.Window;
            bool nearWasOff = !nearWindow || nearWindow.forceRenderingOff;
            bool farWasOff = !farWindow || farWindow.forceRenderingOff;
            if (nearWindow) nearWindow.forceRenderingOff = true;
            if (farWindow) farWindow.forceRenderingOff = true;

            // The occlusion corridor opens onto the pilot's ship from the camera that is drawing.
            // For this render that camera is on the far side, so the ship is taken through the
            // same map — and handed straight back before the gameplay camera draws.
            if (PrismOcclusionCorridor.Target)
                PrismOcclusionCorridor.PublishTargetPosition(FoldGateGeometry.Through(
                    PrismOcclusionCorridor.ViewTargetPosition, near, nearAxis, far, farAxis));

            // A ship straddling the NEAR mouth (about to go through) is drawn on the far side for
            // this render, so its nose appears in the window as it disappears into it.
            bool moved = false;
            Vector3 saved = default;
            if (Straddles(gate))
            {
                saved = _subjectRoot.position;
                _subjectRoot.position = FoldGateGeometry.Through(saved, near, nearAxis, far, farAxis);
                moved = true;
            }

            _camera.Render();

            if (moved) _subjectRoot.position = saved;
            PrismOcclusionCorridor.RepublishTarget();
            if (nearWindow) nearWindow.forceRenderingOff = nearWasOff;
            if (farWindow) farWindow.forceRenderingOff = farWasOff;

            Shader.SetGlobalTexture(PortalTexId, _texture);
            // The window samples at its screen position; map that into the footprint the target
            // covers: uv' = (uv - min) / size.
            Shader.SetGlobalVector(PortalUVId, new Vector4(
                1f / footprint.width, 1f / footprint.height,
                -footprint.xMin / footprint.width, -footprint.yMin / footprint.height));
            return true;
        }

        /// <summary>
        /// The window's rectangle of the gameplay camera's viewport (0..1), padded and clamped to
        /// the screen. Measured from the window disc's own bounding square - its mesh is a unit
        /// disc in its local XY plane - projected through the camera that is about to draw it, so
        /// it follows the ring's bloom and the speed tunnel's field of view for free. A corner at
        /// or behind the near plane means the window wraps around the camera (the carry through),
        /// and then all of the screen may show it.
        /// </summary>
        static Rect WindowFootprint(FoldGate gate, Camera view)
        {
            var window = gate.Window;
            if (!window) return FullViewport;

            Transform t = window.transform;
            float near = view.nearClipPlane;
            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, 0f);
                Vector3 vp = view.WorldToViewportPoint(t.TransformPoint(corner));
                if (vp.z <= near) return FullViewport;
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

        /// <summary>
        /// Narrow the projection to <paramref name="footprint"/>: the sub-rectangle's NDC range is
        /// stretched onto the whole target. In clip space that is x' = (x - c.w) / h for the
        /// rectangle's NDC centre c and half-extent h, i.e. a rewrite of rows 0 and 1 against the
        /// w row - z and w are untouched, so depth and the oblique near plane are exactly as before.
        /// Culling follows the projection, so what the window cannot show is not drawn either.
        /// </summary>
        static void CropProjection(Rect footprint)
        {
            if (footprint.xMin <= 0f && footprint.yMin <= 0f && footprint.xMax >= 1f && footprint.yMax >= 1f)
                return;

            Matrix4x4 p = _camera.projectionMatrix;
            float cx = footprint.xMin + footprint.xMax - 1f, hx = footprint.width;
            float cy = footprint.yMin + footprint.yMax - 1f, hy = footprint.height;
            Vector4 row3 = p.GetRow(3);
            p.SetRow(0, (p.GetRow(0) - row3 * cx) / hx);
            p.SetRow(1, (p.GetRow(1) - row3 * cy) / hy);
            _camera.projectionMatrix = p;
        }

        static bool EnsureCamera(Camera view, Rect footprint, float renderScale)
        {
            float scale = Mathf.Min(renderScale, TierWindowMaxRenderScale);
            int capW = Mathf.Max(TexelQuantum, Mathf.RoundToInt(view.pixelWidth * scale));
            int capH = Mathf.Max(TexelQuantum, Mathf.RoundToInt(view.pixelHeight * scale));
            int needW = Mathf.Clamp(Mathf.CeilToInt(footprint.width * view.pixelWidth * scale), TexelQuantum, capW);
            int needH = Mathf.Clamp(Mathf.CeilToInt(footprint.height * view.pixelHeight * scale), TexelQuantum, capH);
            var format = view.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.Default;

            // Reuse the target while it covers the footprint and is not grossly larger than it;
            // the crop maps the footprint onto the whole target whatever its size, so a target a
            // little larger than needed only renders a little sharper.
            bool fits = _texture != null && _textureFormat == format
                        && _texture.width >= needW && _texture.height >= needH
                        && (_texture.width <= needW * ShrinkSlack || _texture.height <= needH * ShrinkSlack);
            if (_texture != null && !fits)
                ReleaseTexture();

            int width = Mathf.Min(capW, Quantize(needW * GrowHeadroom));
            int height = Mathf.Min(capH, Quantize(needH * GrowHeadroom));

            if (_texture == null)
            {
                _texture = new RenderTexture(width, height, 24, format)
                {
                    name = "FoldGatePortalRT",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                // Created outright: the window samples it in the same frame, and a sampled
                // uncreated target draws nothing.
                _texture.Create();
                _textureFormat = format;
            }

            if (_camera == null)
            {
                var go = new GameObject("[FoldGatePortalCamera]") { hideFlags = HideFlags.HideInHierarchy };
                go.transform.SetParent(_host ? _host.transform : null, false);
                // Deliberately NOT tagged MainCamera, and deliberately left disabled.
                _camera = go.AddComponent<Camera>();
                _camera.enabled = false;
                _camera.useOcclusionCulling = false;
                _camera.allowMSAA = false;

                // HOW it draws: HDR and the game's volume mask, and NO post-processing - see the
                // class summary on why this window is the one exception.
                OffscreenCameraSetup.AdoptGameCameraImage(_camera, postProcessing: false,
                                                          antiAliasing: false, shadows: false);
                _camera.allowHDR = view.allowHDR;
                _cameraData = UnityEngine.Rendering.Universal.CameraExtensions
                    .GetUniversalAdditionalCameraData(_camera);
            }

            // Re-asserted every render: a sweep that grants post to "every camera that presents to
            // the screen" would see this one as such for any frame it has no target, and hand the
            // window a second full post stack.
            if (_cameraData != null)
            {
                _cameraData.renderPostProcessing = false;
                _cameraData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            }

            _camera.targetTexture = _texture;
            return true;
        }

        /// <summary>The device tier's ceiling on the window's render scale (1 = none).</summary>
        static float TierWindowMaxRenderScale
        {
            get
            {
                var profile = CosmicShore.Core.PlatformProfile.Current;
                return profile ? profile.FoldGateWindowMaxRenderScale : 1f;
            }
        }

        static int Quantize(float texels) =>
            Mathf.Max(TexelQuantum, Mathf.CeilToInt(texels / TexelQuantum) * TexelQuantum);

        // ---- the straddling ship ------------------------------------------------------------

        /// <summary>The followed ship's root (the transform a teleport moves) and its hull radius.</summary>
        static void ResolveSubject(CustomCameraController controller)
        {
            var target = controller.FollowTarget;
            if (target != _subjectKey)
            {
                _subjectKey = target;
                var root = target ? target.GetComponentInParent<VesselTransformer>() : null;
                _subjectRoot = root ? root.transform : null;
                _subjectRadius = 0f;
            }
            if (_subjectRoot && _subjectRadius <= 0f)
            {
                // The occlusion corridor's own hull measurement; re-asked while it reads zero, since
                // a hull measured before its art is on answers 0.
                _subjectRadius = _subjectRoot
                    ? PrismOcclusionCorridor.MeasureCircumscribedRadius(_subjectRoot)
                    : 0f;
            }
        }

        /// <summary>Is the followed ship's hull cut by this gate's plane, inside its mouth?</summary>
        static bool Straddles(FoldGate gate)
        {
            if (!_subjectRoot || _subjectRadius <= 0f || !gate) return false;
            Vector3 p = _subjectRoot.position;
            return Mathf.Abs(FoldGateGeometry.Axial(p, gate.Centre, gate.Axis)) < _subjectRadius
                && FoldGateGeometry.Lateral(p, gate.Centre, gate.Axis) < gate.RingRadius;
        }

        /// <summary>
        /// The gameplay camera is about to draw while it is still on the near side of a portal its
        /// ship has already gone through. If the ship's hull is still cut by the FAR plane, draw it
        /// at the near mouth for this one render, so its tail is still in front of the window it
        /// is flying out of.
        /// </summary>
        static void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (_mainMoved || cam == null || cam != _mainView) return;
            var gate = _carriedGate;
            if (!gate) return;
            var partner = gate.Partner;
            if (!partner || !Straddles(partner)) return;

            _mainSaved = _subjectRoot.position;
            _subjectRoot.position = FoldGateGeometry.Through(_mainSaved, partner.Centre, partner.Axis,
                                                             gate.Centre, gate.Axis);
            _mainMoved = true;
        }

        static void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == _mainView) RestoreMainPose();
        }

        static void RestoreMainPose()
        {
            if (!_mainMoved) return;
            _mainMoved = false;
            if (_subjectRoot) _subjectRoot.position = _mainSaved;
        }

        /// <summary>The camera on screen, if it is one this can mimic.</summary>
        static CustomCameraController ResolveController()
        {
            var manager = CameraManager.Instance;
            if (manager == null) return null;
            return manager.GetActiveController() as CustomCameraController;
        }

        /// <summary>
        /// The domain of the pilot the camera is following — the only domain whose gates open a
        /// window for this viewer. A spectator's camera follows somebody else's ship and so sees
        /// THAT pilot's portals, which is what they are watching.
        /// </summary>
        static bool TryResolveViewerDomain(CustomCameraController controller, out Domains domain)
        {
            domain = Domains.Blue;
            var target = controller.FollowTarget;
            if (!target) return false;

            if (target != _viewerKey)
            {
                _viewerKey = target;
                _viewerStatus = target.GetComponentInParent<VesselStatus>();
            }
            if (_viewerStatus == null) return false;

            // IVesselStatus.Domain reads Player and logs when there is none; ask first.
            IVesselStatus status = _viewerStatus;
            if (status.Player == null) return false;
            domain = status.Domain;
            return true;
        }

        static void ReleaseTexture()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture == null) return;
            _texture.Release();
            Object.Destroy(_texture);
            _texture = null;
        }

        static void Release()
        {
            ReleaseTexture();
            if (_camera != null) Object.Destroy(_camera.gameObject);
            _camera = null;
            Shown = null;
        }
    }
}
