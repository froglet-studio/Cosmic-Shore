using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a player SEES of a <see cref="CrystalWormhole"/> (Docs/CRYSTAL_WORMHOLE.md §2): the light of its warp
    /// field, traced per pixel by <c>CrystalWormholeLens.shader</c> through both necks and their glued throats.
    /// This component is its carrier and its eyes:
    /// <list type="bullet">
    /// <item><b>The lens sphere</b> — one sphere around the whole pair, drawn from INSIDE (its far faces), so it
    /// covers every pixel whose ray crosses the warp from any viewpoint, including the pilot's own seat inside it.
    /// Sized each frame to half the separation plus the field's reach; past it space is exactly flat.</item>
    /// <item><b>The far eye</b> — one camera at the gameplay camera's pose taken through the near throat
    /// (<see cref="CrystalWormhole.Through"/>), with an oblique near plane on the far throat, rendering colour and
    /// depth: what every ray that went through shows, near mass included. When the camera reaches the throat the
    /// far eye is exactly where it crosses to, which is what makes the hand-over invisible.</item>
    /// <item><b>Two panoramas</b> — six faces captured at each pole, one face per pole per frame: every direction
    /// the far eye's frame misses (the wound rings at the crystal ball's edge, a camera that is not the player's).</item>
    /// </list>
    /// Its cameras never draw a lens themselves (<see cref="BlackHoleLensPass"/> excludes them): the gameplay
    /// camera's own lens pass composites their pictures, once.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10160)]   // after every camera has posed (and after WormholeView, 10150)
    public sealed class CrystalWormholeView : MonoBehaviour
    {
        public const string MaterialResourcePath = "CrystalWormholeLens";

        /// <summary>The far eye sees this much wider than the gameplay camera: bent rays land outside its frame.</summary>
        const float FarEyeFovScale = 1.35f;
        const float FarEyeMaxFov = 150f;
        /// <summary>The far eye's near plane sits this far beyond the far throat's tangent plane.</summary>
        const float ClipPlaneOffset = 0.05f;
        const float MinObliqueDistance = 0.2f;

        static readonly int PoleAId = Shader.PropertyToID("_CWPoleA");
        static readonly int PoleBId = Shader.PropertyToID("_CWPoleB");
        static readonly int NeckId = Shader.PropertyToID("_CWNeck");
        static readonly int TaperId = Shader.PropertyToID("_CWTaper");
        static readonly int LensId = Shader.PropertyToID("_CWLens");
        static readonly int FarPosId = Shader.PropertyToID("_CWFarPos");
        static readonly int FarVPId = Shader.PropertyToID("_CWFarVP");
        static readonly int FarUvToViewId = Shader.PropertyToID("_CWFarUvToView");
        static readonly int FarSizeId = Shader.PropertyToID("_CWFarSize");
        static readonly int FarTexId = Shader.PropertyToID("_CWFarTex");
        static readonly int FarDepthId = Shader.PropertyToID("_CWFarDepth");
        static readonly int PanoAId = Shader.PropertyToID("_CWPanoA");
        static readonly int PanoBId = Shader.PropertyToID("_CWPanoB");
        static readonly int PanoReadyId = Shader.PropertyToID("_CWPanoReady");
        static readonly int MainViewId = Shader.PropertyToID("_CWMainView");

        static Material s_material;
        static bool s_materialResolved;

        CrystalWormhole _wormhole;
        GameObject _lensObject;
        MeshRenderer _lens;
        MaterialPropertyBlock _block;
        bool _acquired;

        Camera _farEye;
        UniversalAdditionalCameraData _farEyeData;
        RenderTexture _farTex;
        // The formats REQUESTED: RenderTexture.format reports what DefaultHDR resolved to, so comparing
        // against it would reallocate every frame.
        RenderTextureFormat _farFormat;
        RenderTextureFormat _panoramaFormat;
        bool _farValid;
        Camera _mainView;

        Camera _panoramaEye;
        UniversalAdditionalCameraData _panoramaEyeData;
        RenderTexture _panoramaFace;
        readonly RenderTexture[] _panorama = new RenderTexture[2];
        readonly int[] _facesCaptured = new int[2];
        readonly int[] _nextFace = new int[2];
        int _nextPole;

        /// <summary>The shared lens material, or null when it cannot draw (then the pair is invisible but whole).</summary>
        public static Material SharedMaterial
        {
            get
            {
                if (!s_materialResolved)
                {
                    s_material = Resources.Load<Material>(MaterialResourcePath);
                    if (!BlackHoleLens.IsDrawable(s_material, out string reason))
                    {
                        CSDebug.LogWarning($"[CrystalWormhole] The lens cannot draw: {reason}. The pair warps space unseen.");
                        s_material = null;
                    }
                    s_materialResolved = true;
                }
                return s_material;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_material = null;
            s_materialResolved = false;
        }

        internal void Bind(CrystalWormhole wormhole)
        {
            _wormhole = wormhole;
            _block = new MaterialPropertyBlock();
            var material = SharedMaterial;
            if (material)
            {
                _lensObject = new GameObject("[CrystalWormholeLens]");
                _lensObject.AddComponent<MeshFilter>().sharedMesh = BlackHoleLens.LensSphere();
                _lens = _lensObject.AddComponent<MeshRenderer>();
                _lens.sharedMaterial = material;
                _lens.shadowCastingMode = ShadowCastingMode.Off;
                _lens.receiveShadows = false;
                _lens.lightProbeUsage = LightProbeUsage.Off;
                _lens.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _lens.enabled = false;
            }
            _farEye = MakeEye("[CrystalWormhole FarEye]", out _farEyeData);
            _panoramaEye = MakeEye("[CrystalWormhole PanoramaEye]", out _panoramaEyeData);

            BlackHoleLens.CameraSupport.Acquire();
            BlackHoleSky.Acquire();
            BlackHoleLensPass.Acquire();
            _acquired = true;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        }

        /// <summary>The pair is gone: take the lens and the eyes away.</summary>
        internal void Retire() => Destroy(this);

        Camera MakeEye(string eyeName, out UniversalAdditionalCameraData data)
        {
            var go = new GameObject(eyeName) { hideFlags = HideFlags.HideInHierarchy };
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;              // rendered on demand, never by the pipeline's own loop
            cam.useOcclusionCulling = false;
            cam.allowMSAA = false;
            data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = false;
                data.requiresDepthOption = CameraOverrideOption.Off;
                data.requiresColorOption = CameraOverrideOption.Off;
            }
            BlackHoleLensPass.Exclude(cam);
            return cam;
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (_acquired)
            {
                BlackHoleLens.CameraSupport.Release();
                BlackHoleSky.Release();
                BlackHoleLensPass.Release();
                _acquired = false;
            }
            if (_farEye) BlackHoleLensPass.Include(_farEye);
            if (_panoramaEye) BlackHoleLensPass.Include(_panoramaEye);
            Release(ref _farTex);
            Release(ref _panoramaFace);
            Release(ref _panorama[0]);
            Release(ref _panorama[1]);
            if (_farEye) Destroy(_farEye.gameObject);
            if (_panoramaEye) Destroy(_panoramaEye.gameObject);
            if (_lensObject) Destroy(_lensObject);
        }

        static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam) =>
            Shader.SetGlobalFloat(MainViewId, cam != null && cam == _mainView && _farValid ? 1f : 0f);

        void LateUpdate()
        {
            if (!_wormhole || !_lens) return;
            var a = _wormhole.Attractor;
            var b = _wormhole.Repulsor;
            if (!a || !b) { _lens.enabled = false; return; }

            // The field the light follows: the cell's ThroatWarp (its numbers are the shader's), eased by the
            // field's weight; without one, only the throats' own scale.
            var throatWarp = WarpFieldRuntime.Field as ThroatWarp;
            float weight = WarpFieldRuntime.Weight;
            float neck = throatWarp ? throatWarp.ThroatRadius : _wormhole.FullThroat;
            float scale = throatWarp ? throatWarp.ThroatScale : Mathf.Max(0.02f, _wormhole.ThroatScaleFallback);
            float felt = neck / Mathf.Max(scale, 1e-3f);
            float lambda = Mathf.Max(felt - neck, 1e-3f);
            float taperIn = neck + ThroatWarp.TaperStart * lambda;
            float taperOut = neck + ThroatWarp.TaperEnd * lambda;
            float ampA = throatWarp ? a.Amplitude * weight : 0f;
            float ampB = throatWarp ? b.Amplitude * weight : 0f;
            float throat = _wormhole.Throat;

            bool live = !_wormhole.IsGone && (ampA + ampB > 1e-4f || throat > 0f);
            _lens.enabled = live;
            if (!live) { _farValid = false; return; }

            Vector3 pa = a.transform.position, pb = b.transform.position;
            Vector3 centre = (pa + pb) * 0.5f;
            float radius = 0.5f * Vector3.Distance(pa, pb) + taperOut;
            var lensTransform = _lensObject.transform;
            lensTransform.SetPositionAndRotation(centre, Quaternion.identity);
            lensTransform.localScale = Vector3.one * (2f * radius);

            _mainView = ResolveMainView();
            RenderFarEye(throat);
            RenderPanoramaFace();

            _lens.GetPropertyBlock(_block);
            _block.SetVector(PoleAId, new Vector4(pa.x, pa.y, pa.z, ampA));
            _block.SetVector(PoleBId, new Vector4(pb.x, pb.y, pb.z, ampB));
            _block.SetVector(NeckId, new Vector4(throat, neck, felt, Mathf.Clamp(_wormhole.LensSteps, 16, 256)));
            float farSide = _mainView && _wormhole.NearerPole(_mainView.transform.position) == a ? 2f : 1f;
            _block.SetVector(TaperId, new Vector4(taperIn, taperOut, Mathf.Max(_wormhole.ProxyRadius, radius), farSide));
            _block.SetVector(LensId, new Vector4(centre.x, centre.y, centre.z, radius));
            int faceSize = _panorama[0] ? _panorama[0].width : 1;
            _block.SetVector(PanoReadyId, new Vector4(PanoramaReady(0) ? 1f : 0f, PanoramaReady(1) ? 1f : 0f, faceSize, 0f));
            if (_panorama[0]) _block.SetTexture(PanoAId, _panorama[0]);
            if (_panorama[1]) _block.SetTexture(PanoBId, _panorama[1]);
            if (_farValid && _farTex)
            {
                _block.SetTexture(FarTexId, _farTex);
                _block.SetTexture(FarDepthId, _farTex, RenderTextureSubElement.Depth);
                var p = _farEye.transform.position;
                _block.SetVector(FarPosId, new Vector4(p.x, p.y, p.z, 1f));
                _block.SetMatrix(FarVPId, _farEye.projectionMatrix * _farEye.worldToCameraMatrix);
                _block.SetMatrix(FarUvToViewId, UvToView(_farEye.projectionMatrix));
                float texelsPerRadian = _farTex.height / (2f * Mathf.Tan(_farEye.fieldOfView * 0.5f * Mathf.Deg2Rad));
                _block.SetVector(FarSizeId, new Vector4(_farTex.width, _farTex.height, texelsPerRadian, 0f));
            }
            else
            {
                _block.SetVector(FarPosId, Vector4.zero);
            }
            _lens.SetPropertyBlock(_block);
        }

        bool PanoramaReady(int pole) => _facesCaptured[pole] == (1 << WormholeGeometry.FaceCount) - 1;

        // ---- the far eye ---------------------------------------------------------------------

        void RenderFarEye(float throat)
        {
            _farValid = false;
            var view = _mainView;
            if (!view || !_farEye || throat <= 0f) return;

            var eye = view.transform;
            var through = _wormhole.Through(new Pose(eye.position, eye.rotation));
            var farPole = _wormhole.NearerPole(eye.position) == _wormhole.Attractor ? _wormhole.Repulsor : _wormhole.Attractor;
            if (!farPole) return;

            float scale = Mathf.Clamp(_wormhole.FarEyeRenderScale, 0.25f, 1f);
            var profile = CosmicShore.Core.PlatformProfile.Current;
            if (profile) scale = Mathf.Min(scale, profile.FoldGateWindowMaxRenderScale);
            int w = Mathf.Max(64, Mathf.RoundToInt(view.pixelWidth * scale));
            int h = Mathf.Max(64, Mathf.RoundToInt(view.pixelHeight * scale));
            var format = TargetFormat(view);
            if (_farTex == null || _farTex.width != w || _farTex.height != h || _farFormat != format)
            {
                Release(ref _farTex);
                _farTex = new RenderTexture(w, h, 24, format)
                {
                    name = "CrystalWormhole FarEye",
                    useMipMap = true,
                    autoGenerateMips = true,
                    antiAliasing = 1,
                    filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                _farTex.Create();
                _farFormat = format;
            }

            _farEye.transform.SetPositionAndRotation(through.position, through.rotation);
            _farEye.clearFlags = view.clearFlags;
            _farEye.backgroundColor = view.backgroundColor;
            _farEye.cullingMask = WithoutUI(view.cullingMask);
            _farEye.fieldOfView = Mathf.Min(FarEyeMaxFov, view.fieldOfView * FarEyeFovScale);
            _farEye.aspect = view.aspect;
            _farEye.nearClipPlane = view.nearClipPlane;
            _farEye.farClipPlane = view.farClipPlane;
            _farEye.allowHDR = view.allowHDR;
            _farEye.ResetProjectionMatrix();

            // Everything between this vantage and the far throat is on the near side of the pair: an oblique
            // near plane tangent to the far throat, facing away from the vantage.
            if (WormholeGeometry.TryNearCapPlane(through.position, farPole.transform.position, throat,
                                                 out var normal, out var point))
            {
                Matrix4x4 worldToCamera = _farEye.worldToCameraMatrix;
                Vector3 camPos = worldToCamera.MultiplyPoint(point);
                Vector3 camNormal = worldToCamera.MultiplyVector(normal);
                float camDist = -Vector3.Dot(camPos, camNormal) + ClipPlaneOffset;
                if (Mathf.Abs(camDist) > MinObliqueDistance)
                    _farEye.projectionMatrix = _farEye.CalculateObliqueMatrix(
                        new Vector4(camNormal.x, camNormal.y, camNormal.z, camDist));
            }

            _farEye.targetTexture = _farTex;
            _farEye.Render();
            _farValid = true;
        }

        /// <summary>
        /// (uv, raw device depth, 1) → the far eye's view-space position, homogeneous: the inverse of the
        /// projection the GPU actually used (oblique near plane, reversed Z, a flipped y when it renders into a
        /// texture) after the 0..1 uv and the device depth are put back into its clip space.
        /// </summary>
        static Matrix4x4 UvToView(Matrix4x4 projection)
        {
            var gpu = GL.GetGPUProjectionMatrix(projection, true);
            float flip = Mathf.Sign(gpu.m11) == Mathf.Sign(projection.m11) ? 1f : -1f;
            var uvToNdc = Matrix4x4.identity;
            uvToNdc.m00 = 2f; uvToNdc.m03 = -1f;
            uvToNdc.m11 = 2f * flip; uvToNdc.m13 = -flip;
            return gpu.inverse * uvToNdc;
        }

        // ---- the panoramas -------------------------------------------------------------------

        /// <summary>One face of one pole's surroundings per frame, alternating poles; mips regenerated per face.</summary>
        void RenderPanoramaFace()
        {
            var view = _mainView;
            if (!view || !_panoramaEye) return;
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) == 0) return;

            int pole = _nextPole;
            _nextPole = 1 - _nextPole;
            var well = pole == 0 ? _wormhole.Attractor : _wormhole.Repulsor;
            if (!well) return;

            int size = Mathf.Clamp(_wormhole.PanoramaFaceSize, 64, 1024);
            var format = TargetFormat(view);
            if (!EnsurePanorama(pole, size, format)) return;

            int face = _nextFace[pole];
            _nextFace[pole] = (face + 1) % WormholeGeometry.FaceCount;

            _panoramaEye.transform.SetPositionAndRotation(well.transform.position, WormholeGeometry.FaceRotation(face));
            _panoramaEye.clearFlags = view.clearFlags;
            _panoramaEye.backgroundColor = view.backgroundColor;
            _panoramaEye.cullingMask = WithoutUI(view.cullingMask);
            _panoramaEye.nearClipPlane = Mathf.Max(0.1f, view.nearClipPlane);
            _panoramaEye.farClipPlane = view.farClipPlane;
            _panoramaEye.allowHDR = view.allowHDR;
            _panoramaEye.fieldOfView = 90f;
            _panoramaEye.aspect = 1f;
            _panoramaEye.ResetProjectionMatrix();
            _panoramaEye.targetTexture = _panoramaFace;
            _panoramaEye.Render();

            Graphics.CopyTexture(_panoramaFace, 0, 0, _panorama[pole], face, 0);
            _panorama[pole].GenerateMips();
            _facesCaptured[pole] |= 1 << face;
        }

        bool EnsurePanorama(int pole, int size, RenderTextureFormat format)
        {
            if (_panoramaFace == null || _panoramaFace.width != size || _panoramaFormat != format)
            {
                Release(ref _panorama[0]);
                Release(ref _panorama[1]);
                Release(ref _panoramaFace);
                _panoramaFace = new RenderTexture(size, size, 24, format)
                {
                    name = "CrystalWormhole PanoramaFace",
                    antiAliasing = 1,
                    useMipMap = false,
                };
                _panoramaFace.Create();
                _panoramaFormat = format;
            }
            var pano = _panorama[pole];
            if (pano != null) return true;
            Release(ref _panorama[pole]);
            pano = new RenderTexture(size, size, 0, format)
            {
                name = pole == 0 ? "CrystalWormhole Panorama Attractor" : "CrystalWormhole Panorama Repulsor",
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = WormholeGeometry.FaceCount,
                antiAliasing = 1,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            pano.Create();
            _panorama[pole] = pano;
            _facesCaptured[pole] = 0;
            _nextFace[pole] = 0;
            return true;
        }

        // ---- helpers -------------------------------------------------------------------------

        static Camera ResolveMainView()
        {
            var manager = CameraManager.Instance;
            var controller = manager != null ? manager.GetActiveController() as CustomCameraController : null;
            var cam = controller != null && controller.Camera ? controller.Camera : Camera.main;
            return cam && cam.isActiveAndEnabled ? cam : null;
        }

        static int WithoutUI(int mask)
        {
            int ui = LayerMask.NameToLayer("UI");
            return ui >= 0 ? mask & ~(1 << ui) : mask;
        }

        /// <summary>HDR only when the pipeline renders HDR too (the same rule as the Butterfly fold's mouths).</summary>
        static RenderTextureFormat TargetFormat(Camera view)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            bool hdr = view.allowHDR && (!pipeline || pipeline.supportsHDR)
                       && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
            return hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }
    }
}
