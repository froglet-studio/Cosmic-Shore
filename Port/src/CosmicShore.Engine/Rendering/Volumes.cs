using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Rendering
{
    // ── Volume framework (UnityEngine.Rendering: SRP Core) ──────────────────

    /// <summary>A blendable post-process setting with an override toggle.</summary>
    public abstract class VolumeParameter
    {
        [SerializeField] protected bool m_OverrideState;
        public bool overrideState { get => m_OverrideState; set => m_OverrideState = value; }
        public void Override(object value) { overrideState = true; SetBoxed(value); }
        internal abstract void SetBoxed(object value);
        internal abstract object GetBoxed();
    }

    [Serializable]
    public class VolumeParameter<T> : VolumeParameter
    {
        [SerializeField] protected T m_Value;
        public VolumeParameter() { }
        public VolumeParameter(T value, bool overrideState = false) { m_Value = value; this.overrideState = overrideState; }
        public virtual T value { get => m_Value; set => m_Value = value; }
        public void Override(T x) { overrideState = true; value = x; }
        internal override void SetBoxed(object v) => value = (T)v;
        internal override object GetBoxed() => value;
        public static implicit operator T(VolumeParameter<T> prop) => prop.m_Value;
    }

    [Serializable] public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) : base(value, overrideState) { } }
    [Serializable] public class IntParameter : VolumeParameter<int> { public IntParameter(int value, bool overrideState = false) : base(value, overrideState) { } }
    [Serializable] public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) : base(value, overrideState) { } }
    [Serializable] public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color value, bool overrideState = false) : base(value, overrideState) { } public ColorParameter(Color value, bool hdr, bool showAlpha, bool showEyeDropper, bool overrideState = false) : base(value, overrideState) { } }
    [Serializable] public class Vector2Parameter : VolumeParameter<Vector2> { public Vector2Parameter(Vector2 value, bool overrideState = false) : base(value, overrideState) { } }
    [Serializable] public class TextureParameter : VolumeParameter<Texture> { public TextureParameter(Texture value, bool overrideState = false) : base(value, overrideState) { } }

    [Serializable]
    public class ClampedFloatParameter : FloatParameter
    {
        public float min, max;
        public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { this.min = min; this.max = max; }
        public override float value { get => m_Value; set => m_Value = Math.Clamp(value, min, max); }
    }

    [Serializable]
    public class MinFloatParameter : FloatParameter
    {
        public float min;
        public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { this.min = min; }
        public override float value { get => m_Value; set => m_Value = Math.Max(value, min); }
    }

    [Serializable]
    public class ClampedIntParameter : IntParameter
    {
        public int min, max;
        public ClampedIntParameter(int value, int min, int max, bool overrideState = false) : base(value, overrideState) { this.min = min; this.max = max; }
        public override int value { get => m_Value; set => m_Value = Math.Clamp(value, min, max); }
    }

    /// <summary>One post-process block inside a profile (Bloom, Tonemapping…).</summary>
    [Serializable]
    public abstract class VolumeComponent : ScriptableObject
    {
        public bool active = true;
        public virtual bool IsActive() => active;
        public string displayName { get; set; } = string.Empty;
        public IEnumerable<VolumeParameter> parameters
        {
            get
            {
                foreach (var f in GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                    if (f.GetValue(this) is VolumeParameter p) yield return p;
            }
        }
        public void SetAllOverridesTo(bool state) { foreach (var p in parameters) p.overrideState = state; }
    }

    /// <summary>A post-process profile asset: an ordered list of components.</summary>
    public class VolumeProfile : ScriptableObject
    {
        public List<VolumeComponent> components = new();
        public bool isDirty { get; set; }

        public T Add<T>(bool overrides = false) where T : VolumeComponent
        {
            if (Has<T>()) throw new InvalidOperationException($"Component {typeof(T).Name} already exists in the profile");
            var c = CreateInstance<T>();
            c.SetAllOverridesTo(overrides);
            components.Add(c);
            isDirty = true;
            return c;
        }

        public VolumeComponent Add(Type type, bool overrides = false)
        {
            var c = (VolumeComponent)CreateInstance(type);
            c.SetAllOverridesTo(overrides);
            components.Add(c);
            return c;
        }

        public void Remove<T>() where T : VolumeComponent => components.RemoveAll(c => c is T);
        public bool Has<T>() where T : VolumeComponent => components.Exists(c => c is T);
        public bool Has(Type type) => components.Exists(type.IsInstanceOfType);

        public bool TryGet<T>(out T component) where T : VolumeComponent
        {
            foreach (var c in components) if (c is T t) { component = t; return true; }
            component = null;
            return false;
        }

        public bool TryGet<T>(Type type, out T component) where T : VolumeComponent
        {
            foreach (var c in components) if (type.IsInstanceOfType(c)) { component = c as T; return component != null; }
            component = null;
            return false;
        }

        public bool TryGetSubclassOf<T>(Type type, out T component) where T : VolumeComponent => TryGet(type, out component);

        public void Reset() => components.Clear();
    }

    /// <summary>A post-process volume in the scene (global or bounded by colliders).</summary>
    public class Volume : MonoBehaviour
    {
        [SerializeField] VolumeProfile sharedProfile;
        VolumeProfile _instance;
        public bool isGlobal = true;
        public float priority;
        public float weight = 1f;
        public float blendDistance;

        /// <summary>
        /// This volume's own profile (original contract): the first read builds a private
        /// profile holding a copy of every shared component, so runtime edits never touch the asset.
        /// </summary>
        public VolumeProfile profile
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = ScriptableObject.CreateInstance<VolumeProfile>();
                if (sharedProfile != null)
                {
                    _instance.name = sharedProfile.name;
                    foreach (var c in sharedProfile.components)
                        if (c != null) _instance.components.Add(Instantiate(c));
                }
                return _instance;
            }
            set => _instance = value;
        }

        /// <summary>The profile the renderer reads: the instance once one exists, else the shared asset.</summary>
        public VolumeProfile profileRef => _instance != null ? _instance : sharedProfile;

        public VolumeProfile sharedProfileRef { get => sharedProfile; set => sharedProfile = value; }
        public bool HasInstantiatedProfile() => _instance != null;

        static readonly List<Volume> s_Active = new();
        public static IReadOnlyList<Volume> Active => s_Active;
        void OnEnable() { if (!s_Active.Contains(this)) s_Active.Add(this); }
        void OnDisable() => s_Active.Remove(this);
    }

    public enum UpscalingFilterSelection { Auto = 0, Linear = 1, Point = 2, FSR = 3, STP = 4 }
    public enum MsaaQuality { Disabled = 1, _2x = 2, _4x = 4, _8x = 8 }
    public enum AntialiasingMode { None = 0, FastApproximateAntialiasing = 1, SubpixelMorphologicalAntiAliasing = 2, TemporalAntiAliasing = 3 }
    public enum AntialiasingQuality { Low = 0, Medium = 1, High = 2 }
    public enum CameraRenderType { Base = 0, Overlay = 1 }
    public enum CameraOverrideOption { Off = 0, On = 1, UsePipelineSettings = 2 }
    public enum TonemappingMode { None = 0, Neutral = 1, ACES = 2 }
    public enum BloomDownscaleMode { Half = 0, Quarter = 1 }

    // ── URP post-process components (UnityEngine.Rendering.Universal) ─────

    [Serializable] public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new(0.9f, 0f);
        public MinFloatParameter intensity = new(0f, 0f);
        public ClampedFloatParameter scatter = new(0.7f, 0f, 1f);
        public MinFloatParameter clamp = new(65472f, 0f);
        public ColorParameter tint = new(Color.white);
        public BoolParameter highQualityFiltering = new(false);
        public ClampedIntParameter skipIterations = new(1, 0, 16);
        public VolumeParameter<BloomDownscaleMode> downscale = new(BloomDownscaleMode.Half);
        public ClampedIntParameter maxIterations = new(6, 2, 8);
        public TextureParameter dirtTexture = new(null);
        public MinFloatParameter dirtIntensity = new(0f, 0f);
        public override bool IsActive() => intensity.value > 0f;
    }

    [Serializable] public sealed class Tonemapping : VolumeComponent
    {
        public VolumeParameter<TonemappingMode> mode = new(TonemappingMode.None);
        public override bool IsActive() => mode.value != TonemappingMode.None;
    }

    [Serializable] public sealed class PaniniProjection : VolumeComponent
    {
        public ClampedFloatParameter distance = new(0f, 0f, 1f);
        public ClampedFloatParameter cropToFit = new(1f, 0f, 1f);
        public override bool IsActive() => distance.value > 0f;
    }

    [Serializable] public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color = new(Color.black);
        public Vector2Parameter center = new(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        public ClampedFloatParameter smoothness = new(0.2f, 0.01f, 1f);
        public BoolParameter rounded = new(false);
        public override bool IsActive() => intensity.value > 0f;
    }

    [Serializable] public sealed class ChromaticAberration : VolumeComponent
    {
        public ClampedFloatParameter intensity = new(0f, 0f, 1f);
        public override bool IsActive() => intensity.value > 0f;
    }

    [Serializable] public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure = new(0f);
        public ClampedFloatParameter contrast = new(0f, -100f, 100f);
        public ColorParameter colorFilter = new(Color.white);
        public ClampedFloatParameter hueShift = new(0f, -180f, 180f);
        public ClampedFloatParameter saturation = new(0f, -100f, 100f);
    }

    [Serializable] public sealed class LensDistortion : VolumeComponent
    {
        public ClampedFloatParameter intensity = new(0f, -1f, 1f);
        public ClampedFloatParameter xMultiplier = new(1f, 0f, 1f);
        public ClampedFloatParameter yMultiplier = new(1f, 0f, 1f);
        public Vector2Parameter center = new(new Vector2(0.5f, 0.5f));
        public ClampedFloatParameter scale = new(1f, 0.01f, 5f);
    }

    // Components the project's profiles author that the port does not render; they load so
    // a profile's component list matches the asset (TryGet finds them, overrides round-trip).
    [Serializable] public sealed class ColorCurves : VolumeComponent { }
    [Serializable] public sealed class LiftGammaGain : VolumeComponent { }
    [Serializable] public sealed class ShadowsMidtonesHighlights : VolumeComponent { }
    [Serializable] public sealed class SplitToning : VolumeComponent { }
    [Serializable] public sealed class ChannelMixer : VolumeComponent { }
    [Serializable] public sealed class ColorLookup : VolumeComponent { }
    [Serializable] public sealed class ScreenSpaceLensFlare : VolumeComponent { }
    [Serializable] public sealed class ProbeVolumesOptions : VolumeComponent { }

    [Serializable] public sealed class FilmGrain : VolumeComponent { public ClampedFloatParameter intensity = new(0f, 0f, 1f); }
    [Serializable] public sealed class MotionBlur : VolumeComponent { public ClampedFloatParameter intensity = new(0f, 0f, 1f); }
    [Serializable] public sealed class DepthOfField : VolumeComponent { public MinFloatParameter focusDistance = new(10f, 0.1f); }
    [Serializable] public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature = new(0f, -100f, 100f); public ClampedFloatParameter tint = new(0f, -100f, 100f); }

    // ── pipeline assets / camera data ───────────────────────────────────────

    public class RenderPipelineAsset : ScriptableObject { }

    /// <summary>URP pipeline settings the game reads/writes at runtime (render scale, MSAA, upscaling…).</summary>
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] float m_RenderScale = 1f;
        [SerializeField] int m_MSAA = 1;
        [SerializeField] UpscalingFilterSelection m_UpscalingFilter = UpscalingFilterSelection.Auto;
        [SerializeField] bool m_SupportsHDR = true;
        [SerializeField] float m_ShadowDistance = 50f;
        [SerializeField] bool m_RequireDepthTexture;
        [SerializeField] bool m_RequireOpaqueTexture;
        [SerializeField] bool m_FsrOverrideSharpness;
        [SerializeField] float m_FsrSharpness = 0.92f;
        [SerializeField] bool m_UseSRPBatcher = true;

        public float renderScale { get => m_RenderScale; set => m_RenderScale = value; }
        public int msaaSampleCount { get => m_MSAA; set => m_MSAA = value; }
        public UpscalingFilterSelection upscalingFilter { get => m_UpscalingFilter; set => m_UpscalingFilter = value; }
        public bool supportsHDR { get => m_SupportsHDR; set => m_SupportsHDR = value; }
        public float shadowDistance { get => m_ShadowDistance; set => m_ShadowDistance = value; }
        public bool supportsCameraDepthTexture { get => m_RequireDepthTexture; set => m_RequireDepthTexture = value; }
        public bool supportsCameraOpaqueTexture { get => m_RequireOpaqueTexture; set => m_RequireOpaqueTexture = value; }
        public bool fsrOverrideSharpness { get => m_FsrOverrideSharpness; set => m_FsrOverrideSharpness = value; }
        public float fsrSharpness { get => m_FsrSharpness; set => m_FsrSharpness = value; }
        public bool useSRPBatcher { get => m_UseSRPBatcher; set => m_UseSRPBatcher = value; }
        /// <summary>The default renderer (URP's asset.scriptableRenderer): passes enqueued here are counted, not run (RenderGraph.cs).</summary>
        public ScriptableRenderer scriptableRenderer { get; } = new();
    }

    /// <summary>URP per-camera data (UniversalAdditionalCameraData).</summary>
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        [SerializeField] bool m_RenderPostProcessing;
        [SerializeField] LayerMask m_VolumeLayerMask = 1;
        [SerializeField] bool m_RenderShadows = true;
        public bool renderPostProcessing { get => m_RenderPostProcessing; set => m_RenderPostProcessing = value; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; } = AntialiasingQuality.High;
        public CameraOverrideOption requiresDepthOption { get; set; } = CameraOverrideOption.UsePipelineSettings;
        public CameraOverrideOption requiresColorOption { get; set; } = CameraOverrideOption.UsePipelineSettings;
        public bool requiresDepthTexture { get; set; }
        public bool requiresColorTexture { get; set; }
        public CameraRenderType renderType { get; set; }
        public bool renderShadows { get => m_RenderShadows; set => m_RenderShadows = value; }
        public LayerMask volumeLayerMask { get => m_VolumeLayerMask; set => m_VolumeLayerMask = value; }
        public Transform volumeTrigger { get; set; }
        public bool stopNaN { get; set; }
        public bool dithering { get; set; }
        public List<Camera> cameraStack { get; } = new();
        /// <summary>The camera's renderer (URP): passes enqueued here are counted, not run (RenderGraph.cs).</summary>
        public ScriptableRenderer scriptableRenderer { get; } = new();
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera)
            => camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
    }

    /// <summary>The render-graph submission context (opaque here).</summary>
    public struct ScriptableRenderContext { }

    /// <summary>Per-camera render callbacks (UnityEngine.Rendering.RenderPipelineManager); the port's renderer raises them.</summary>
    public static class RenderPipelineManager
    {
        public static event Action<ScriptableRenderContext, Camera> beginCameraRendering;
        public static event Action<ScriptableRenderContext, Camera> endCameraRendering;
        public static event Action<ScriptableRenderContext, List<Camera>> beginContextRendering;
        public static event Action<ScriptableRenderContext, List<Camera>> endContextRendering;
        public static RenderPipelineAsset currentPipeline => GraphicsSettings.currentRenderPipeline;
        public static void RaiseBeginCamera(Camera c) => beginCameraRendering?.Invoke(default, c);
        public static void RaiseEndCamera(Camera c) => endCameraRendering?.Invoke(default, c);
        public static void RaiseBeginContext(List<Camera> c) => beginContextRendering?.Invoke(default, c);
        public static void RaiseEndContext(List<Camera> c) => endContextRendering?.Invoke(default, c);
    }

    /// <summary>Project graphics settings (UnityEngine.Rendering.GraphicsSettings).</summary>
    public static class GraphicsSettings
    {
        public static RenderPipelineAsset defaultRenderPipeline { get; set; }
        public static RenderPipelineAsset currentRenderPipeline => QualityRenderPipeline ?? defaultRenderPipeline;
        internal static RenderPipelineAsset QualityRenderPipeline { get; set; }
        public static bool useScriptableRenderPipelineBatching { get; set; } = true;
        public static bool lightsUseLinearIntensity { get; set; } = true;
    }
}
