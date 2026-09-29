using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Rendering
{
    // ── Volume framework (UnityEngine.Rendering: SRP Core) ──────────────────

    /// <summary>A blendable post-process setting with an override toggle.</summary>
    public abstract class VolumeParameter
    {
        public bool overrideState { get; set; }
        public void Override(object value) { overrideState = true; SetBoxed(value); }
        internal abstract void SetBoxed(object value);
        internal abstract object GetBoxed();
    }

    [Serializable]
    public class VolumeParameter<T> : VolumeParameter
    {
        protected T m_Value;
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

        public VolumeProfile profile
        {
            get => _instance ??= sharedProfile != null ? Instantiate(sharedProfile) : ScriptableObject.CreateInstance<VolumeProfile>();
            set => _instance = value;
        }

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

    [Serializable] public sealed class FilmGrain : VolumeComponent { public ClampedFloatParameter intensity = new(0f, 0f, 1f); }
    [Serializable] public sealed class MotionBlur : VolumeComponent { public ClampedFloatParameter intensity = new(0f, 0f, 1f); }
    [Serializable] public sealed class DepthOfField : VolumeComponent { public MinFloatParameter focusDistance = new(10f, 0.1f); }
    [Serializable] public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature = new(0f, -100f, 100f); public ClampedFloatParameter tint = new(0f, -100f, 100f); }

    // ── pipeline assets / camera data ───────────────────────────────────────

    public class RenderPipelineAsset : ScriptableObject { }

    /// <summary>URP pipeline settings the game reads/writes at runtime (render scale, MSAA, upscaling…).</summary>
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public float renderScale { get; set; } = 1f;
        public int msaaSampleCount { get; set; } = 1;
        public UpscalingFilterSelection upscalingFilter { get; set; } = UpscalingFilterSelection.Auto;
        public bool supportsHDR { get; set; } = true;
        public float shadowDistance { get; set; } = 50f;
        public bool supportsCameraDepthTexture { get; set; }
        public bool supportsCameraOpaqueTexture { get; set; }
        public bool fsrOverrideSharpness { get; set; }
        public float fsrSharpness { get; set; } = 0.92f;
        public bool useSRPBatcher { get; set; } = true;
    }

    /// <summary>URP per-camera data (UniversalAdditionalCameraData).</summary>
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; } = true;
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; } = AntialiasingQuality.High;
        public CameraOverrideOption requiresDepthOption { get; set; } = CameraOverrideOption.UsePipelineSettings;
        public CameraOverrideOption requiresColorOption { get; set; } = CameraOverrideOption.UsePipelineSettings;
        public bool requiresDepthTexture { get; set; }
        public bool requiresColorTexture { get; set; }
        public CameraRenderType renderType { get; set; }
        public bool renderShadows { get; set; } = true;
        public LayerMask volumeLayerMask { get; set; } = 1;
        public Transform volumeTrigger { get; set; }
        public bool stopNaN { get; set; }
        public bool dithering { get; set; }
        public List<Camera> cameraStack { get; } = new();
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
