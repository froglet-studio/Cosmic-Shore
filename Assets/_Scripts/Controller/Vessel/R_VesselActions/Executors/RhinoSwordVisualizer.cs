using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drives the Rhino energy sword's LOOK, owned and ticked by <see cref="ShieldSkimmerScaleDriver"/>.
    /// Three layers, all configured through <see cref="ShieldSkimmerScaleConfigSO"/>:
    ///
    ///  1. HEAT RAMP — the blade blends from its authored teal toward the full-energy colour and
    ///     brightens as stored energy fills, so the sword's power is readable at a glance. The blade
    ///     uses the SHARED FresnelMaterial, so this never touches <c>renderer.material</c> — it
    ///     drives <c>_Color</c> through a MaterialPropertyBlock (per-renderer, no material leak;
    ///     RGB above 1 feeds gameplay bloom — the AstroLeagueBall impact-flash precedent).
    ///  2. IMPACT FLASH — a decaying white-out pulse on every prism the sword destroys (small for a
    ///     normal pop, full for a super-shield pop or crystal burst), layered over the heat ramp.
    ///  3. TIP TRACERS — two motion streaks seated at the blade tips (parented to the fuselage so
    ///     their width doesn't scale with the growing blade), tinted with the live blade colour.
    ///     The tracer material comes from the config (instanced per sword so tinting never mutates
    ///     the shared asset); a runtime unlit fallback covers a missing reference.
    ///
    /// A plain class (not a MonoBehaviour) so no extra prefab component is needed; the driver
    /// creates it, calls <see cref="Setup"/> once, <see cref="Tick"/> each frame, and
    /// <see cref="Teardown"/> on disable. See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public sealed class RhinoSwordVisualizer
    {
        static readonly int ColorId     = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int Color0Id    = Shader.PropertyToID("_Color0");
        static readonly int Color1Id    = Shader.PropertyToID("_Color1");

        Transform _skimmerRoot;
        ShieldSkimmerScaleConfigSO _config;

        MeshRenderer _bodyRenderer;
        MaterialPropertyBlock _mpb;
        bool _hasColorProp;
        Color _appliedBodyColor;
        bool _bodyColorApplied;

        Color _baseVisible;       // authored teal * visibility
        Color _fullEnergyVisible; // full-energy colour * visibility
        float _flash;             // 0 = none, 1 = full white-out; decays each Tick

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

            _baseVisible       = ScaleRgb(authored, vis);
            _fullEnergyVisible = ScaleRgb(_config.FullEnergyColor, vis);
            _flash = 0f;
            _bodyColorApplied = false; // the MPB was cleared on Teardown — force the first apply

            ApplyBodyColor(_baseVisible);
            BuildTracers();
            UpdateTracerTransforms(); // seat them at the tips before they start recording
            for (int i = 0; i < _tracers.Count; i++)
                if (_tracers[i]) _tracers[i].Clear();
        }

        /// <summary>Kick an impact flash; stronger pulses override weaker ones mid-decay.</summary>
        public void Flash(float amount01) => _flash = Mathf.Max(_flash, Mathf.Clamp01(amount01));

        public void Tick(float energy01, float dt)
        {
            if (_config == null) return;

            _flash = Mathf.MoveTowards(_flash, 0f, dt / _config.FlashDecaySeconds);

            // Heat ramp: colour AND brightness climb with stored energy…
            Color ramp = Color.Lerp(_baseVisible, _fullEnergyVisible, energy01);
            float brightness = Mathf.Lerp(1f, _config.FullEnergyBrightness, energy01);
            Color color = ScaleRgb(ramp, brightness);

            // …with the impact flash layered on top.
            if (_flash > 0f)
                color = Color.Lerp(color, _config.FlashColor, _flash);

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
            _bodyColorApplied = false;
            _flash = 0f;

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
            // Steady state (fixed energy, no flash) holds one colour — skip the MPB round-trip.
            if (_bodyColorApplied && ColorsClose(color, _appliedBodyColor)) return;
            _appliedBodyColor = color;
            _bodyColorApplied = true;

            _bodyRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            _bodyRenderer.SetPropertyBlock(_mpb);
        }

        void BuildTracers()
        {
            if (!_config.TracersEnabled || !_skimmerRoot) return;

            _tracerMaterial = CreateTracerMaterial(_config.TracerMaterial);
            if (!_tracerMaterial) return; // no material and no usable shader — skip tracers, keep the blade
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
            // the colour actually moved (energy ramp / flash decay) — no per-frame allocation.
            if (_tracerColorApplied && ColorsClose(color, _appliedTracerColor)) return;
            _appliedTracerColor = color;
            _tracerColorApplied = true;

            // Drive every colour property the wired material might expose (TrailViewer's
            // _Color0/_Color1, generic _Color/_BaseColor) plus the vertex gradient, so the tint
            // lands regardless of which shader the config references. Alpha fades along the streak.
            if (_tracerMaterial)
            {
                if (_tracerMaterial.HasProperty(BaseColorId)) _tracerMaterial.SetColor(BaseColorId, color);
                if (_tracerMaterial.HasProperty(ColorId))     _tracerMaterial.SetColor(ColorId, color);
                if (_tracerMaterial.HasProperty(Color0Id))    _tracerMaterial.SetColor(Color0Id, color);
                if (_tracerMaterial.HasProperty(Color1Id))    _tracerMaterial.SetColor(Color1Id, color);
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

        static Material CreateTracerMaterial(Material authored)
        {
            // Preferred: instance the config-authored material (never tint the shared asset).
            if (authored) return new Material(authored) { name = authored.name + " (sword tracer)" };

            // Fallback: build an additive unlit material from whatever shader the build ships.
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (!sh) sh = Shader.Find("Sprites/Default");
            if (!sh) sh = Shader.Find("Unlit/Color");
            if (!sh) return null;

            var m = new Material(sh) { name = "RhinoSwordTracer (runtime)" };
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_Surface"))  m.SetFloat("_Surface", 1f);   // URP: transparent
            if (m.HasProperty("_Blend"))    m.SetFloat("_Blend", 1f);     // URP: additive
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.One);
            if (m.HasProperty("_ZWrite"))   m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Color ScaleRgb(Color c, float mul) => new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
    }
}
