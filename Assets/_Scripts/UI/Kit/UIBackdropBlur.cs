using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.UI
{
    /// <summary>
    /// The frosted glass behind style-B windows (Docs/MENU_REDESIGN_B.md §62, rev 11 option H): a small, blurred copy
    /// of the game camera's finished frame, kept in <c>_FrogletBackdropBlur</c> for the lattice shader
    /// (<c>CosmicShore/UI/Lattice</c>) to sample at each pixel's screen position. The menu's canvas is Screen Space
    /// Overlay, so it draws after the camera and reads this frame's copy.
    ///
    /// <para><b>Injected like <c>SilencePerimeterPass</c> and <c>BlackHoleLensPass</c>.</b> No renderer feature: the
    /// pass is enqueued from <see cref="RenderPipelineManager.beginCameraRendering"/> on the base game camera that
    /// draws to the screen, and only while at least one frosted backdrop is showing (<see cref="AddUser"/> /
    /// <see cref="RemoveUser"/>, called by <see cref="KitBackdropMotion"/>), so nothing is added to any asset and a
    /// frame with no window open pays nothing. <c>_FrogletBackdropBlurOn</c> tells the shader whether the copy is live;
    /// when it is not (no window, the setting off, the quality level too low) the shader draws no frost and the
    /// window's own dark veil shows instead, as before.</para>
    ///
    /// <para><b>Cost.</b> One downsample to 1/<see cref="UIBackdropBlurSettingsSO.Downsample"/> of the screen and
    /// <see cref="UIBackdropBlurSettingsSO.Passes"/> four-tap blur passes at that size, into two persistent render
    /// textures (they must outlive the camera's render graph: the menu reads them afterwards).</para>
    /// </summary>
    public static class UIBackdropBlur
    {
        public const string SettingsResource = "UIBackdropBlurSettings";
        static readonly int BlurId = Shader.PropertyToID("_FrogletBackdropBlur");
        static readonly int BlurOnId = Shader.PropertyToID("_FrogletBackdropBlurOn");
        static readonly int SourceTexelId = Shader.PropertyToID("_FrogletBlurSourceTexel");
        static readonly int TexelId = Shader.PropertyToID("_FrogletBlurTexel");

        static int s_users;
        static UIBackdropBlurSettingsSO s_settings;
        static bool s_settingsLoaded;
        static Material s_material;
        static BlurPass s_pass;

        /// <summary>How many frosted backdrops are showing right now.</summary>
        public static int Users => s_users;

        public static void AddUser() => s_users++;

        public static void RemoveUser() => s_users = Mathf.Max(0, s_users - 1);

        static UIBackdropBlurSettingsSO Settings
        {
            get
            {
                if (!s_settingsLoaded)
                {
                    s_settings = Resources.Load<UIBackdropBlurSettingsSO>(SettingsResource);
                    s_settingsLoaded = true;
                }
                return s_settings;
            }
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data == null || data.renderType != CameraRenderType.Base) return;

            var settings = Settings;
            bool live = s_users > 0 && settings && settings.Enabled && settings.BlurShader
                        && QualitySettings.GetQualityLevel() >= settings.MinQualityLevel;
            Shader.SetGlobalFloat(BlurOnId, live ? 1f : 0f);
            if (!live) return;

            if (!s_material) s_material = new Material(settings.BlurShader) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = data.scriptableRenderer;
            if (renderer == null) return;
            s_pass ??= new BlurPass();
            s_pass.Configure(settings, s_material);
            renderer.EnqueuePass(s_pass);
        }

        /// <summary>Play-mode (re)entry: no backdrop is showing yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_users = 0;
            s_settingsLoaded = false;
            Shader.SetGlobalFloat(BlurOnId, 0f);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        /// <summary>Downsample the finished frame, then blur it back and forth between two persistent textures.</summary>
        sealed class BlurPass : ScriptableRenderPass
        {
            /// <summary>The shader's passes, in the order <c>BackdropBlur.shader</c> declares them.</summary>
            const int DownsamplePass = 0;
            const int BlurShaderPass = 1;

            RTHandle _a, _b;
            int _downsample = 4, _passes = 2;
            Material _material;

            class BlitData
            {
                public TextureHandle source;
                public Material material;
                public int pass;
            }

            public BlurPass()
            {
                // After post-processing: the frost shows the world as the player sees it. The copy reads the camera
                // colour as a texture, which the backbuffer cannot be.
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
                profilingSampler = new ProfilingSampler("UIBackdropBlur");
            }

            public void Configure(UIBackdropBlurSettingsSO settings, Material material)
            {
                _downsample = Mathf.Clamp(settings.Downsample, 1, 16);
                _passes = Mathf.Clamp(settings.Passes, 1, 6);
                _material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !_material) return;
                var cam = frameData.Get<UniversalCameraData>().camera;
                if (cam == null) return;
                TextureHandle colour = resources.activeColorTexture;

                // Half floats keep the post-processed colour without banding; the size is the camera's, divided.
                int fullW = Mathf.Max(1, cam.pixelWidth), fullH = Mathf.Max(1, cam.pixelHeight);
                int width = Mathf.Max(1, fullW / _downsample), height = Mathf.Max(1, fullH / _downsample);
                var blurDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0) { msaaSamples = 1, useMipMap = false };
                RenderingUtils.ReAllocateHandleIfNeeded(ref _a, blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FrogletBackdropBlurA");
                RenderingUtils.ReAllocateHandleIfNeeded(ref _b, blurDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FrogletBackdropBlurB");

                _material.SetVector(SourceTexelId, new Vector4(1f / fullW, 1f / fullH, 0f, 0f));
                _material.SetVector(TexelId, new Vector4(1f / width, 1f / height, 0f, 0f));

                TextureHandle a = renderGraph.ImportTexture(_a);
                TextureHandle b = renderGraph.ImportTexture(_b);
                Blit(renderGraph, "Backdrop downsample", colour, a, DownsamplePass);
                TextureHandle from = a, to = b;
                for (int i = 0; i < _passes; i++)
                {
                    Blit(renderGraph, "Backdrop blur", from, to, BlurShaderPass);
                    (from, to) = (to, from);
                }
                // The last written texture is the frost; it is persistent, so the global outlives this graph.
                Shader.SetGlobalTexture(BlurId, (_passes % 2 == 0) ? _a : _b);
            }

            void Blit(RenderGraph renderGraph, string name, TextureHandle source, TextureHandle destination, int pass)
            {
                using var builder = renderGraph.AddRasterRenderPass<BlitData>(name, out var data, profilingSampler);
                data.source = source;
                data.material = _material;
                data.pass = pass;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((BlitData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }
        }
    }
}
