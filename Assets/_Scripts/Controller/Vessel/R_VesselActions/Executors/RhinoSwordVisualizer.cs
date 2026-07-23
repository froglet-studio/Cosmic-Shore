using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the Rhino energy sword's LOOK, owned and ticked by <see cref="ShieldSkimmerScaleDriver"/>.
    /// Two responsibilities, both configured through <see cref="ShieldSkimmerScaleConfigSO"/>:
    ///
    ///  1. The blade body reads twice as visible and shifts from its authored blue/teal to white
    ///     when energized. The blade uses the SHARED FresnelMaterial, so this never touches
    ///     <c>renderer.material</c> — it drives <c>_Color</c> through a MaterialPropertyBlock
    ///     (per-renderer, no material instance leak).
    ///  2. Motion tracers streak along the blade's two tips and recolour with the body.
    ///
    /// It is a plain class (not a MonoBehaviour) so no extra prefab component is needed; the driver
    /// creates it, calls <see cref="Setup"/> once, <see cref="Tick"/> each frame, and
    /// <see cref="Teardown"/> on disable. See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public sealed class RhinoSwordVisualizer
    {
        static readonly int ColorId     = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Transform _skimmerRoot;
        ShieldSkimmerScaleConfigSO _config;

        MeshRenderer _bodyRenderer;
        MaterialPropertyBlock _mpb;
        bool _hasColorProp;

        Color _baseVisible;      // authored teal * visibility
        Color _energizedVisible; // energized colour * visibility
        float _blend;            // 0 = blue, 1 = white

        readonly List<TrailRenderer> _tracers = new();
        Material _tracerMaterial;
        Color _appliedTracerColor;
        bool _tracerColorApplied;

        public void Setup(Transform skimmerRoot, ShieldSkimmerScaleConfigSO config)
        {
            _skimmerRoot = skimmerRoot;
            _config = config;
            if (!_skimmerRoot || _config == null) return;

            _mpb = new MaterialPropertyBlock();
            _bodyRenderer = _skimmerRoot.GetComponent<MeshRenderer>();

            float vis = _config.VisibilityMultiplier;

            Color authored = new Color(0.055f, 0.755f, 0.712f, 1f); // FresnelMaterial teal fallback
            if (_bodyRenderer && _bodyRenderer.sharedMaterial)
            {
                var mat = _bodyRenderer.sharedMaterial;
                _hasColorProp = mat.HasProperty(ColorId);
                if (_hasColorProp) authored = mat.GetColor(ColorId);
            }

            _baseVisible      = ScaleRgb(authored, vis);
            _energizedVisible = ScaleRgb(_config.EnergizedColor, vis);
            _blend = 0f;

            ApplyBodyColor(_baseVisible);
            BuildTracers();
            UpdateTracerTransforms(); // seat them at the tips before they start recording
            for (int i = 0; i < _tracers.Count; i++)
                if (_tracers[i]) _tracers[i].Clear();
        }

        public void Tick(bool isEnergized, float dt)
        {
            if (_config == null) return;

            float target = isEnergized ? 1f : 0f;
            _blend = Mathf.MoveTowards(_blend, target, dt / _config.ColorTransitionSeconds);

            Color color = Color.Lerp(_baseVisible, _energizedVisible, _blend);
            ApplyBodyColor(color);

            if (_tracers.Count == 0) return;
            UpdateTracerTransforms();
            ApplyTracerColor(color);
        }

        public void Teardown()
        {
            // Drop the per-renderer colour override so the shared material shows through again
            // (the next vessel's driver re-applies its own on Setup).
            if (_bodyRenderer) _bodyRenderer.SetPropertyBlock(null);
            _tracerColorApplied = false;

            for (int i = 0; i < _tracers.Count; i++)
                if (_tracers[i]) Object.Destroy(_tracers[i].gameObject);
            _tracers.Clear();

            if (_tracerMaterial) Object.Destroy(_tracerMaterial);
            _tracerMaterial = null;
            _bodyRenderer = null;
            _skimmerRoot = null;
        }

        // ── internals ──────────────────────────────────────────────────────────

        void ApplyBodyColor(Color color)
        {
            if (!_bodyRenderer || !_hasColorProp) return;
            _bodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            _bodyRenderer.SetPropertyBlock(_mpb);
        }

        void BuildTracers()
        {
            if (!_config.TracersEnabled || !_skimmerRoot) return;

            _tracerMaterial = CreateTracerMaterial();
            if (!_tracerMaterial) return; // no usable shader in this build — skip tracers, keep the blade
            var parent = _skimmerRoot.parent; // fuselage — stable ~unit scale, unlike the growing blade

            for (int i = 0; i < 2; i++) // one streak per blade tip
            {
                var go = new GameObject("RhinoSwordTracer" + i);
                go.transform.SetParent(parent, false);

                var tr = go.AddComponent<TrailRenderer>();
                tr.time = _config.TracerTimeSeconds;
                tr.widthMultiplier = _config.TracerWidth;
                tr.numCapVertices = 2;
                tr.numCornerVertices = 2;
                tr.minVertexDistance = 0.5f;
                tr.alignment = LineAlignment.View;
                tr.textureMode = LineTextureMode.Stretch;
                tr.shadowCastingMode = ShadowCastingMode.Off;
                tr.receiveShadows = false;
                tr.lightProbeUsage = LightProbeUsage.Off;
                tr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                tr.material = _tracerMaterial;
                tr.emitting = true;

                _tracers.Add(tr);
            }
        }

        void UpdateTracerTransforms()
        {
            if (_tracers.Count < 2 || !_skimmerRoot) return;
            Vector3 center = _skimmerRoot.position;
            Vector3 up = _skimmerRoot.up;
            // Unit capsule spans local y ∈ [-1, 1]; world tip offset = up * lossyScale.y.
            float half = _skimmerRoot.lossyScale.y * 0.95f;
            if (_tracers[0]) _tracers[0].transform.position = center + up * half;
            if (_tracers[1]) _tracers[1].transform.position = center - up * half;
        }

        void ApplyTracerColor(Color color)
        {
            // Steady state holds one colour for long stretches, so only rebuild the gradient when
            // the colour actually moved (during the blue↔white blend) — no per-frame allocation.
            if (_tracerColorApplied && ColorsClose(color, _appliedTracerColor)) return;
            _appliedTracerColor = color;
            _tracerColorApplied = true;

            // Additive tracer: drive both the material colour (property shaders) and the
            // vertex gradient (vertex-colour shaders) so it shows regardless of which fallback
            // shader we landed on. Fade alpha to 0 along the streak.
            if (_tracerMaterial)
            {
                if (_tracerMaterial.HasProperty(BaseColorId)) _tracerMaterial.SetColor(BaseColorId, color);
                if (_tracerMaterial.HasProperty(ColorId))     _tracerMaterial.SetColor(ColorId, color);
            }

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

            for (int i = 0; i < _tracers.Count; i++)
                if (_tracers[i]) _tracers[i].colorGradient = grad;
        }

        static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;

        static Material CreateTracerMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (!sh) sh = Shader.Find("Sprites/Default");
            if (!sh) sh = Shader.Find("Unlit/Color");
            if (!sh) return null;

            var m = new Material(sh) { name = "RhinoSwordTracer (runtime)" };

            // Best-effort additive transparency across whichever shader we got.
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);   // URP: transparent
            if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 1f);     // URP: additive
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.One);
            if (m.HasProperty("_ZWrite"))   m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_EMISSION");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Color ScaleRgb(Color c, float mul) => new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
    }
}
