using UnityEngine;
using UnityEngine.VFX;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A lightweight stand-in for <c>vfxgraph_arclightning</c> (VfxArcLightning.prefab, nested in the Rhino) on a
    /// device where VFX Graph cannot run: no compute shaders, no structured buffers in the vertex stage - WebGL2 and
    /// OpenGL ES 3.0. There the VisualEffect simulates and draws nothing, so the arc would simply be missing.
    ///
    /// <para>
    /// Where VFX Graph runs, this component switches itself off in <c>Awake</c> and costs nothing: the VFX path is
    /// untouched. Where it cannot, it draws the graph's two strands as two jagged <see cref="LineRenderer"/>s, from the
    /// same values the prefab feeds the effect:
    /// <list type="bullet">
    /// <item>the strand shapes - each strand is the graph's cubic Bezier (SampleBezier A..D) through four transforms,
    /// the same ones the prefab's VFXPositionBinders bind to Pos1..Pos4 (the "Blue Strand") and Pos5..Pos8 (the
    /// "White Strand"), read every frame;</item>
    /// <item>the blue strand's look - the effect's exposed <c>Color</c>, <c>Thickness</c>, <c>NoiseSpeed</c>,
    /// <c>Noise Frequency</c>, <c>NoiseOctaves</c> and <c>NoiseRoughness</c>, read from the VisualEffect at enable;
    /// the white strand uses the graph's own constants (it exposes none);</item>
    /// <item>visibility - a strand shows only while the effect itself would: its GameObject active and its renderer
    /// enabled. The Rhino ships the effect switched OFF (its nested instance inactive, the VFXRenderer disabled), so
    /// on the Rhino today this stand-in, like the effect, shows nothing on any platform.</item>
    /// </list>
    /// The graph displaces each strip particle by an accumulated noise flow, which a polyline cannot reproduce; the
    /// stand-in jitters its points with a fractal noise driven by the same speed / frequency / octave / roughness
    /// values, tapered to zero at the strand's ends, at <see cref="jitter"/> world units.
    /// </para>
    /// </summary>
    public sealed class ArcLightningFallback : MonoBehaviour
    {
        [Header("The effect this stands in for")]
        [Tooltip("The arc's VisualEffect. Its exposed values drive the stand-in, and the stand-in shows only while the " +
                 "effect's GameObject is active and its renderer enabled.")]
        [SerializeField] VisualEffect visualEffect;

        [Header("Strand control points (the VFXPositionBinder targets)")]
        [Tooltip("The four transforms bound to Pos1..Pos4: the Bezier control points of the blue strand.")]
        [SerializeField] Transform[] blueStrand = new Transform[4];
        [Tooltip("The four transforms bound to Pos5..Pos8: the Bezier control points of the white strand.")]
        [SerializeField] Transform[] whiteStrand = new Transform[4];

        [Header("Stand-in shape")]
        [Tooltip("Points per strand. The graph's strips hold 200 particles; a line needs far fewer to read as an arc.")]
        [SerializeField, Range(4, 64)] int segments = 24;
        [Tooltip("World-unit amplitude of the jagged displacement at the middle of a strand (zero at its ends).")]
        [SerializeField, Min(0f)] float jitter = 0.35f;
        [Tooltip("Seconds a strand takes to grow to full width when the effect switches on, and to thin away when it " +
                 "switches off (continuity of existence: nothing pops).")]
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.2f;

        [Header("White strand (vfxgraph_arclightning's own constants: it exposes no colour or size)")]
        [Tooltip("The white strand's colour: the graph's Set Color constant.")]
        [SerializeField] Color whiteColor = new(0.4921569f, 0.4960784f, 0.1254902f, 1f);
        [Tooltip("The white strand's width: the graph's Set Size constant.")]
        [SerializeField, Min(0f)] float whiteWidth = 0.5f;

        // the effect's exposed property names (vfxgraph_arclightning.vfx)
        const string ColorProperty = "Color", ThicknessProperty = "Thickness", NoiseSpeedProperty = "NoiseSpeed";
        const string NoiseFrequencyProperty = "Noise Frequency", NoiseOctavesProperty = "NoiseOctaves";
        const string NoiseRoughnessProperty = "NoiseRoughness";
        const float Lacunarity = 2.1f;   // the graph's noise operator constant

        static Material s_lineMaterial;

        LineRenderer _blue, _white;
        Renderer _effectRenderer;
        Vector3[] _points;
        Color _blueColor = new(0.5314704f, 0.7752642f, 0.7702202f, 1f);   // the prefab's authored values, used only
        float _blueWidth = 0.1f, _speed = 0.86f, _frequency = 3.81f;      // if the effect cannot answer for them
        int _octaves = 6;
        float _roughness = 0.46f;
        float _presence;   // 0..1: the strands' width fade

        /// <summary>VFX Graph simulates in compute shaders and its outputs read particle buffers in the vertex stage.</summary>
        public static bool VfxGraphSupported =>
            SystemInfo.supportsComputeShaders && SystemInfo.maxComputeBufferInputsVertex > 0;

        void Awake()
        {
            if (VfxGraphSupported || !visualEffect || !LineMaterial()) { enabled = false; return; }
            visualEffect.TryGetComponent(out _effectRenderer);
            ReadEffectValues();
            _points = new Vector3[Mathf.Max(4, segments)];
            _blue = BuildLine("ArcLightningFallback (blue)", _blueColor, _blueWidth);
            _white = BuildLine("ArcLightningFallback (white)", whiteColor, whiteWidth);
        }

        void OnDisable()
        {
            _presence = 0f;
            if (_blue) _blue.enabled = false;
            if (_white) _white.enabled = false;
        }

        void LateUpdate()
        {
            bool shown = visualEffect && visualEffect.isActiveAndEnabled && (!_effectRenderer || _effectRenderer.enabled);
            _presence = Mathf.MoveTowards(_presence, shown ? 1f : 0f, Time.deltaTime / fadeSeconds);
            float t = Time.time * _speed;
            Strand(_blue, blueStrand, _blueWidth, t, 0f);
            Strand(_white, whiteStrand, whiteWidth, t, 17.3f);
        }

        void ReadEffectValues()
        {
            if (visualEffect.HasVector4(ColorProperty))
            {
                Vector4 c = visualEffect.GetVector4(ColorProperty);
                _blueColor = new Color(c.x, c.y, c.z, 1f);
            }
            if (visualEffect.HasFloat(ThicknessProperty)) _blueWidth = visualEffect.GetFloat(ThicknessProperty);
            if (visualEffect.HasFloat(NoiseSpeedProperty)) _speed = visualEffect.GetFloat(NoiseSpeedProperty);
            if (visualEffect.HasFloat(NoiseFrequencyProperty)) _frequency = visualEffect.GetFloat(NoiseFrequencyProperty);
            if (visualEffect.HasFloat(NoiseOctavesProperty)) _octaves = Mathf.RoundToInt(visualEffect.GetFloat(NoiseOctavesProperty));
            if (visualEffect.HasFloat(NoiseRoughnessProperty)) _roughness = visualEffect.GetFloat(NoiseRoughnessProperty);
            _octaves = Mathf.Clamp(_octaves, 1, 6);
            _roughness = Mathf.Clamp01(_roughness);
        }

        LineRenderer BuildLine(string lineName, Color color, float width)
        {
            // a renderer of its own: the effect's GameObject already carries the VFXRenderer
            var go = new GameObject(lineName) { layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = _points.Length;
            line.widthMultiplier = width;
            line.startColor = line.endColor = color;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCapVertices = 2;
            line.sharedMaterial = LineMaterial();
            line.enabled = false;
            return line;
        }

        void Strand(LineRenderer line, Transform[] control, float width, float time, float seed)
        {
            if (!line) return;
            bool valid = _presence > 0f && control is { Length: >= 4 } && control[0] && control[1] && control[2] && control[3];
            if (line.enabled != valid) line.enabled = valid;
            if (!valid) return;
            line.widthMultiplier = width * _presence;
            Vector3 a = control[0].position, b = control[1].position, c = control[2].position, d = control[3].position;
            int n = _points.Length;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float envelope = Mathf.Sin(Mathf.PI * u);
                _points[i] = Bezier(a, b, c, d, u) + jitter * envelope * Jag(u * _frequency, time, seed);
            }
            line.SetPositions(_points);
        }

        // SampleBezier: the cubic through A..D at t
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1f - t;
            return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
        }

        // fractal noise in [-1, 1] per axis: octaves at Lacunarity x the frequency, each Roughness x the amplitude
        Vector3 Jag(float x, float time, float seed)
        {
            Vector3 sum = Vector3.zero;
            float amp = 1f, freq = 1f, norm = 0f;
            for (int o = 0; o < _octaves; o++)
            {
                float px = x * freq + seed, py = time * freq;
                sum.x += amp * (Mathf.PerlinNoise(px, py) * 2f - 1f);
                sum.y += amp * (Mathf.PerlinNoise(px + 31.7f, py + 11.3f) * 2f - 1f);
                sum.z += amp * (Mathf.PerlinNoise(px + 57.1f, py + 47.9f) * 2f - 1f);
                norm += amp;
                amp *= _roughness;
                freq *= Lacunarity;
            }
            return norm > 0f ? sum / norm : sum;
        }

        // one shared vertex-coloured material, as SniperBeam's and SpawnableCord's lines use
        static Material LineMaterial()
        {
            if (s_lineMaterial) return s_lineMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_lineMaterial = new Material(shader) { name = "ArcLightningFallback" };
            return s_lineMaterial;
        }
    }
}
