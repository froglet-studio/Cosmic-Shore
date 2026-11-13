using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The look of a Grizzly TRIGGER BOMB in flight (GRIZZLY_TRIGGER_BOMBS.md) - on the root of
    /// GrizzlyBomb.prefab, driven by <see cref="GrizzlyTriggerBombExecutor"/>.
    ///
    /// <para><b>A bomb reads as a bomb by its SILHOUETTE and its PULSE.</b> The body is a sea
    /// mine: a small faceted core ringed by twelve spikes (a generated mesh on the
    /// <see cref="spikes"/> child) that tumble as it flies, both in the blast look's
    /// <c>CosmicShore/BlastFacet</c> two-tone (BLAST_LOOK.md): an edge-on facet is the dark body, a
    /// square-on facet the bomb's colour, and the breath is HEAT on those facets, so the pulse is
    /// the solid itself glowing, never a glow sprite stuck on it. A faceted shell (the blast's own
    /// shock-front shader, <c>CosmicShore/BlastShell</c>) PINGS outward from it like a sonar
    /// return, and it drags a plasma comet (the Astro League ball's <c>CosmicShore/BallTrail</c>
    /// shader, tinted per shot) behind it. It cruises at constant speed with no fuse - only the
    /// trigger detonates it - so every one of those beats is steady for as long as it flies.</para>
    ///
    /// <para><b>Freezing it ARMS it</b> (the trigger's second pull, or the bomb touching another
    /// vessel): the comet stops, the spikes SNAP out to full length with an overshoot, the facets
    /// flash white-hot and then strobe hot at the armed rate, the tumble slows, and the shell
    /// reverses - it COLLAPSES onto the bomb instead of pinging away from it, brightening as it
    /// closes, the space pulled in: "this one is about to go". Every bomb is DANGER-coloured, LT's
    /// and RT's turned a little apart on the hue wheel so the two read as two
    /// (<see cref="GrizzlyTriggerBombConfigSO.BombColor"/>). Until 2026-11-13 the pulse was an
    /// additive glow billboard and the ping a ring billboard; on film they read as a white
    /// four-point star and a flat starburst (the footage critique), so both went.</para>
    ///
    /// <para><b>It lights what it passes through.</b> A bomb flies THROUGH prisms rather than
    /// stopping on them, and a hot body sliding through solid mass with no reaction reads as a
    /// clipping bug. So the bomb publishes a LIT volume (Docs/LIT.md) - a cylinder over the
    /// stretch it covered in the last <see cref="litWakeSeconds"/>, a sphere once it is at rest -
    /// and every prism inside is drawn lit in the firing pilot's DOMAIN colour (LIT rule 3: a
    /// light says WHOSE force is in that mass). Visual only; nothing reads it to decide an
    /// outcome.</para>
    ///
    /// <para><b>Small before, big after</b> is the design owner's ask: the bomb itself is a few
    /// units across (its scale is set per shot by the executor from the squeeze) and the blast
    /// it becomes is tens of times that. The shell is the only part allowed to swell, and it is
    /// light, not body.</para>
    ///
    /// <para><b>The spikes share the core's shader and its vertex push.</b> The bomb's
    /// <c>BlastFacet</c> material pushes every vertex one WORLD unit along its normal
    /// (<c>_Spread</c> 1, what the old <c>Custom/SpreadFresnelShader</c> did, so the bomb keeps
    /// its shipped size), so the spike mesh carries RADIAL normals (the sphere's own at each
    /// vertex) rather than cone normals: the whole spike then rides the same push the sphere
    /// does and stays seated on it, where cone normals would bloat every spike sideways by a
    /// unit. The facets themselves come from the shader (screen-space derivatives), not the
    /// normals. The bases sit inside the sphere, so retracting the spikes is a uniform scale of
    /// the child. The core's mesh is swapped at Awake for the blast look's low-poly icosphere
    /// (<see cref="BlastLookMeshes.Core"/>, the same 0.5 radius), so its facets read.</para>
    ///
    /// <para>All colour rides a <see cref="MaterialPropertyBlock"/> on shared materials - the
    /// core and spikes' facet material (<c>_BrightColor</c> facet, <c>_DarkColor</c> body,
    /// <c>_HotColor</c> and <c>_Heat</c>), the shell's (<c>_BrightColor</c>, <c>_HotColor</c>,
    /// <c>_Heat</c>, <c>_Opacity</c>), the comet's <c>_Color</c> - never
    /// <c>renderer.material</c>. The comet's gradient is white with alpha running 1 at the head
    /// to 0 at the tail, which is that shader's along-trail coordinate, not an opacity
    /// (ASTROLEAGUE.md). Pooled: <see cref="Arm"/> resets every field, and the visual goes idle
    /// on disable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrizzlyBombVisual : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField, Tooltip("The bomb's hot core - the root sphere.")]
        Renderer core;
        [SerializeField, Tooltip("The spike crown's transform. Tumbles in flight; its uniform scale retracts and deploys the spikes (their bases sit inside the core).")]
        Transform spikes;
        [SerializeField, Tooltip("The spike crown's MeshFilter. Its mesh is generated once and shared by every bomb (twelve cones on the icosahedron's axes).")]
        MeshFilter spikesFilter;
        [SerializeField, Tooltip("The spike crown's renderer - the core's facet material, tinted with the core's property block.")]
        Renderer spikesRenderer;
        [SerializeField, Tooltip("A faceted shell (CosmicShore/BlastShell on the blast look's icosphere, set at Awake) that pings outward in flight and collapses onto the bomb when armed.")]
        Transform ring;
        [SerializeField, Tooltip("The shell's renderer (CosmicShore/BlastShell, tinted through its property block).")]
        Renderer ringRenderer;
        [SerializeField, Tooltip("The plasma comet behind the flying bomb (CosmicShore/BallTrail). Stops emitting when the bomb is frozen.")]
        TrailRenderer trail;
        [SerializeField, Tooltip("Comet width as a fraction of the bomb's diameter. A trail's width is in WORLD units, so it is re-sized per shot from the bomb's own scale.")]
        float trailWidthFactor = 1.4f;
        [SerializeField, Tooltip("How far the comet's three braided filaments unravel down the wake (the shader's _Speed01): 0 a tight rope, 1 wide apart.")]
        [Range(0f, 1f)] float trailBraidSpread = 0.7f;
        [SerializeField, Tooltip("Comet brightness (the shader's _Intensity). Gameplay bloom is clamped, so this mostly buys colour, not glare.")]
        float trailIntensity = 1.2f;

        [Header("Pulse")]
        [FormerlySerializedAs("pulseHzAtLaunch")]
        [SerializeField, Tooltip("Breaths per second while the bomb flies (heat on the facets).")]
        float pulseHz = 4f;
        [SerializeField, Tooltip("Facet heat at the peak of a breath while flying (0 the two-tone, 1 white-hot).")]
        [Range(0f, 1f)] float flightHeatPeak = 0.22f;
        [SerializeField, Tooltip("Facet heat at the peak of a breath once armed - the strobe.")]
        [Range(0f, 1f)] float armedHeatPeak = 0.75f;
        [SerializeField, Tooltip("HDR glow of a white-hot facet (the facet material's Heat Intensity).")]
        float heatIntensity = 5f;

        [Header("Spikes")]
        [SerializeField, Tooltip("Spike crown scale while flying - the spikes are half-retracted, short horns.")]
        float spikesFlightScale = 0.8f;
        [SerializeField, Tooltip("Spike crown scale once armed - the spikes stand out full length.")]
        float spikesArmedScale = 1.05f;
        [SerializeField, Tooltip("Seconds the spikes take to snap out when the bomb is armed (with an elastic overshoot).")]
        float spikesDeploySeconds = 0.22f;
        [SerializeField, Tooltip("Tumble rate of the spike crown while flying, degrees per second.")]
        float spinDegreesPerSecond = 240f;
        [SerializeField, Tooltip("Tumble rate once armed, degrees per second - it slows to a menacing turn.")]
        float armedSpinDegreesPerSecond = 45f;

        [Header("Shell (the ping and the pull)")]
        [SerializeField, Tooltip("Shell pings per second while the bomb flies.")]
        float ringPingHz = 1.6f;
        [SerializeField, Tooltip("Shell diameter relative to the core where a ping is born (flying) or where a collapse ends (armed).")]
        float ringMinScale = 1.4f;
        [SerializeField, Tooltip("Shell diameter relative to the core where a ping fades out.")]
        float ringMaxScale = 7f;
        [SerializeField, Tooltip("Shell collapses per second once armed.")]
        float armedRingHz = 3.5f;
        [SerializeField, Tooltip("Shell diameter relative to the core where an armed collapse begins.")]
        float armedRingMaxScale = 6f;
        [SerializeField, Tooltip("Shell brightness at its brightest (a fresh ping, or a collapse about to close): the BlastShell material's Intensity.")]
        float ringIntensity = 2.4f;

        [Header("Armed (frozen)")]
        [SerializeField, Tooltip("Strobe rate of a frozen bomb, breaths per second.")]
        float armedPulseHz = 14f;
        [SerializeField, Tooltip("The flash at the instant of arming: the facets go this much hotter (on top of the strobe) and settle over Spikes Deploy Seconds.")]
        float armFlash = 1f;

        [Header("Colour")]
        [SerializeField, Tooltip("HDR multiplier on the bomb colour at the peak of a breath - the rim glow.")]
        float rimIntensityPeak = 6f;
        [SerializeField, Tooltip("HDR multiplier on the bomb colour at the trough of a breath.")]
        float rimIntensityTrough = 1.5f;
        [SerializeField, Tooltip("Colour the rim flashes toward at the peak of a breath - the spark.")]
        Color sparkColor = new(1f, 0.92f, 0.7f, 1f);
        [SerializeField, Tooltip("How far each peak leans from the bomb colour toward the spark colour.")]
        [Range(0f, 1f)] float sparkMix = 0.45f;
        [SerializeField, Tooltip("Core darkness: the bomb colour scaled by this. Low keeps the bomb a dark body with a bright rim.")]
        [Range(0f, 1f)] float coreDarkness = 0.12f;

        [Header("Lit prisms (LIT, Docs/LIT.md)")]
        [SerializeField, Tooltip("Radius of the lit volume as a multiple of the bomb's diameter. A prism is lit when its ORIGIN is inside, so this must reach past the bomb's own skin or a bomb grazing a long prism lights nothing.")]
        float litRadiusFactor = 1.5f;
        [SerializeField, Tooltip("Seconds of the bomb's own path kept lit behind it, so a prism it just passed through stays lit a beat instead of blinking off as the bomb leaves it.")]
        float litWakeSeconds = 0.3f;

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int HotColorId = Shader.PropertyToID("_HotColor");
        static readonly int HeatId = Shader.PropertyToID("_Heat");
        static readonly int HeatIntensityId = Shader.PropertyToID("_HeatIntensity");
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int TrailColorId = Shader.PropertyToID("_Color");
        static readonly int TrailIntensityId = Shader.PropertyToID("_Intensity");
        static readonly int TrailSpeedId = Shader.PropertyToID("_Speed01");

        /// <summary>The spikes tumble about this (local) axis - off every symmetry axis of the
        /// crown, so the turn never looks like a wheel.</summary>
        static readonly Vector3 SpinAxis = new Vector3(0.35f, 1f, 0.2f).normalized;

        MaterialPropertyBlock _coreProps;
        MaterialPropertyBlock _ringProps;
        MaterialPropertyBlock _trailProps;
        Color _bombColor = Color.white;
        Domains _domain = Domains.Blue;
        float _frozenAt;
        float _phase;
        float _ringPhase;
        bool _frozen;
        bool _live;

        // The lit wake: a short ring buffer of where the bomb has been.
        const int WakeSamples = 16;
        readonly Vector3[] _wakePos = new Vector3[WakeSamples];
        readonly float[] _wakeTime = new float[WakeSamples];
        int _wakeHead;
        int _wakeCount;

        void Awake()
        {
            if (spikesFilter) spikesFilter.sharedMesh = SpikeMesh;
            // The facets read only on a low-poly body: the blast look's icosphere, the same 0.5 radius as
            // the built-in sphere it replaces. The shell is the blast's own shock-front mesh.
            if (core && core.TryGetComponent(out MeshFilter coreFilter)) coreFilter.sharedMesh = BlastLookMeshes.Core;
            if (ring && ring.TryGetComponent(out MeshFilter ringFilter)) ringFilter.sharedMesh = BlastLookMeshes.Shell;
        }

        /// <summary>
        /// Called by the executor right after the bomb leaves the muzzle: tint it with its
        /// trigger's danger shade, remember whose it is (the LIT tint), retract the spikes,
        /// clear the comet a pooled shell carried from its last flight.
        /// </summary>
        public void Arm(Color bombColor, Domains domain)
        {
            _bombColor = bombColor;
            _domain = domain;
            _phase = 0f;
            _ringPhase = 0f;
            _frozen = false;
            _live = true;
            _wakeHead = 0;
            _wakeCount = 0;

            if (spikes)
            {
                spikes.localScale = Vector3.one * spikesFlightScale;
                spikes.localRotation = Quaternion.identity;
            }

            if (trail)
            {
                trail.Clear();
                trail.widthMultiplier = Mathf.Max(0.05f, transform.lossyScale.x * trailWidthFactor);
                // The gradient's ALPHA is the comet shader's along-trail coordinate (1 head ->
                // 0 tail), not an opacity; its colour comes from the property block.
                trail.startColor = Color.white;
                trail.endColor = new Color(1f, 1f, 1f, 0f);
                _trailProps ??= new MaterialPropertyBlock();
                trail.GetPropertyBlock(_trailProps);
                _trailProps.SetColor(TrailColorId, FullValue(bombColor));
                _trailProps.SetFloat(TrailIntensityId, trailIntensity);
                _trailProps.SetFloat(TrailSpeedId, trailBraidSpread);
                trail.SetPropertyBlock(_trailProps);
                trail.emitting = true;
            }
            Apply(1f, 0f);
        }

        /// <summary>The bomb stopped - the trigger's second pull, or another vessel's hull - and
        /// ARMS: the comet ends, the spikes snap out, the facets flash hot, the shell turns inward.</summary>
        public void Freeze()
        {
            if (_frozen) return;
            _frozen = true;
            _frozenAt = Time.time;
            _ringPhase = 0f;
            if (trail) trail.emitting = false;
        }

        void OnDisable()
        {
            // Starts the light's fade (LIT rule 4); the bank collects it on its own regardless.
            if (_live) PrismLit.ClearLight(GetInstanceID());
            _live = false;
            _frozen = false;
            if (trail)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }

        void Update()
        {
            if (!_live) return;
            float dt = Time.deltaTime;

            float hz = _frozen ? armedPulseHz : pulseHz;

            // The arming beat: spikes snap out with an overshoot, the facets flash hot and settle.
            float deploy01 = _frozen ? Mathf.Clamp01((Time.time - _frozenAt) / Mathf.Max(0.01f, spikesDeploySeconds)) : 0f;
            float flash = _frozen ? armFlash * (1f - deploy01) * (1f - deploy01) : 0f;

            if (spikes)
            {
                float spikeScale = _frozen
                    ? Mathf.LerpUnclamped(spikesFlightScale, spikesArmedScale, EaseOutBack(deploy01))
                    : spikesFlightScale;
                spikes.localScale = Vector3.one * spikeScale;
                spikes.Rotate(SpinAxis, (_frozen ? armedSpinDegreesPerSecond : spinDegreesPerSecond) * dt, Space.Self);
            }

            _phase += hz * dt * 2f * Mathf.PI;
            float breath = 0.5f + 0.5f * Mathf.Sin(_phase);   // 0 trough .. 1 peak
            Apply(breath, flash);

            _ringPhase = Mathf.Repeat(_ringPhase + (_frozen ? armedRingHz : ringPingHz) * dt, 1f);
            ApplyRing(_ringPhase);

            PublishLit();
        }

        /// <summary>
        /// Light the prisms the bomb is passing through (Docs/LIT.md). Published from Update
        /// because <c>PrismLit.Flush</c> packs the bank in LateUpdate. The volume is a cylinder
        /// from just ahead of the bomb back over the path it covered in the last
        /// <see cref="litWakeSeconds"/>, and a sphere once that path is shorter than the bomb's
        /// own reach (at rest, or frozen).
        /// </summary>
        void PublishLit()
        {
            var pos = transform.position;
            float now = Time.time;
            _wakePos[_wakeHead] = pos;
            _wakeTime[_wakeHead] = now;
            _wakeHead = (_wakeHead + 1) % WakeSamples;
            if (_wakeCount < WakeSamples) _wakeCount++;

            // The oldest sample still inside the wake window.
            var tail = pos;
            for (int k = 1; k <= _wakeCount; k++)
            {
                int i = (_wakeHead - k + WakeSamples) % WakeSamples;
                if (now - _wakeTime[i] > litWakeSeconds) break;
                tail = _wakePos[i];
            }

            float radius = transform.lossyScale.x * litRadiusFactor;
            if (radius <= 0f) return;

            var back = tail - pos;
            float wake = back.magnitude;
            LitVolume volume;
            if (wake <= radius * 0.25f)
                volume = LitVolume.Sphere(pos, radius);
            else
            {
                var axis = back / wake;
                // Origin one radius AHEAD of the bomb, so the flat cap does not cut the prism the
                // bomb is entering in half; reach covers that radius, the wake and a radius past it.
                volume = LitVolume.Cylinder(pos - axis * radius, axis, wake + 2f * radius, radius, false);
            }
            PrismLit.PublishLight(GetInstanceID(), volume, 1f, _domain);
        }

        /// <summary>One shell beat, <paramref name="t01"/> through it. Flying: a ping born at the
        /// bomb, racing outward (ease-out) and fading. Armed: a collapse from wide onto the bomb
        /// (ease-in), brightening and heating as it closes - the space pulled in.</summary>
        void ApplyRing(float t01)
        {
            float scale, bright;
            if (_frozen)
            {
                scale = Mathf.Lerp(armedRingMaxScale, ringMinScale, t01 * t01);
                bright = t01;
            }
            else
            {
                float outward = 1f - (1f - t01) * (1f - t01);
                scale = Mathf.Lerp(ringMinScale, ringMaxScale, outward);
                bright = (1f - t01) * (1f - t01);
            }

            if (ring) ring.localScale = Vector3.one * scale;
            if (ringRenderer)
            {
                _ringProps ??= new MaterialPropertyBlock();
                ringRenderer.GetPropertyBlock(_ringProps);
                _ringProps.SetColor(BrightColorId, FullValue(_bombColor));
                _ringProps.SetColor(HotColorId, SparkColor());
                _ringProps.SetFloat(HeatId, _frozen ? bright * bright : 0f);
                _ringProps.SetFloat(OpacityId, bright);
                _ringProps.SetFloat(IntensityId, ringIntensity);
                ringRenderer.SetPropertyBlock(_ringProps);
            }
        }

        /// <summary>The colour at full HSV value - brightness belongs to the white-hot parts,
        /// never the hue (Docs/PALETTE.md §4.3).</summary>
        static Color FullValue(Color c)
        {
            Color.RGBToHSV(c, out float h, out float sat, out _);
            var full = Color.HSVToRGB(h, sat, 1f);
            full.a = 1f;
            return full;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ── The spike crown mesh ─────────────────────────────────────────────

        static Mesh s_spikeMesh;

        /// <summary>Twelve cones on the icosahedron's vertex axes, built once and shared by
        /// every bomb. Object space matches Unity's sphere (radius 0.5): each cone's base ring
        /// sits at <see cref="SpikeBaseRadius"/>, inside the sphere, and its tip at
        /// <see cref="SpikeTipRadius"/>. Normals are RADIAL - see the class doc.</summary>
        internal static Mesh SpikeMesh => s_spikeMesh ? s_spikeMesh : (s_spikeMesh = BuildSpikeMesh());

        internal const float SpikeBaseRadius = 0.42f;
        internal const float SpikeTipRadius = 1f;
        internal const float SpikeBaseHalfWidth = 0.13f;
        internal const int SpikeSides = 6;

        /// <summary>The icosahedron's twelve vertex directions - evenly spread, so the crown has
        /// no seam and no favoured side.</summary>
        internal static Vector3[] SpikeAxes()
        {
            const float phi = 1.618034f;
            var axes = new Vector3[12];
            int i = 0;
            foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -phi, phi })
            {
                axes[i++] = new Vector3(0f, a, b).normalized;
                axes[i++] = new Vector3(a, b, 0f).normalized;
                axes[i++] = new Vector3(b, 0f, a).normalized;
            }
            return axes;
        }

        static Mesh BuildSpikeMesh()
        {
            var axes = SpikeAxes();
            int perSpike = SpikeSides + 1;
            var vertices = new Vector3[axes.Length * perSpike];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[axes.Length * SpikeSides * 3];
            int v = 0, t = 0;
            foreach (var axis in axes)
            {
                var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var w = Vector3.Cross(axis, u);
                int tip = v;
                vertices[v] = axis * SpikeTipRadius;
                normals[v++] = axis;
                for (int k = 0; k < SpikeSides; k++)
                {
                    float a = k * (2f * Mathf.PI / SpikeSides);
                    var p = axis * SpikeBaseRadius + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * SpikeBaseHalfWidth;
                    vertices[v] = p;
                    normals[v++] = p.normalized;
                }
                for (int k = 0; k < SpikeSides; k++)
                {
                    triangles[t++] = tip;
                    triangles[t++] = tip + 1 + (k + 1) % SpikeSides;
                    triangles[t++] = tip + 1 + k;
                }
            }
            var mesh = new Mesh { name = "GrizzlyBombSpikes", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The spark as the worn colour set paints it (ColorTrace).</summary>
        Color SparkColor()
        {
            var spark = ColorTrace.Retrace(ColorTrace.World, sparkColor);
            spark.a = 1f;
            return spark;
        }

        /// <summary>The facets for one breath: <paramref name="breath"/> 0 trough to 1 peak, plus the
        /// arming <paramref name="flash"/>. A square-on facet is the bomb's colour (brighter, and leaning to
        /// the spark, at a peak), an edge-on facet its dark body, and the breath is heat on both.</summary>
        void Apply(float breath, float flash)
        {
            var spark = SparkColor();
            var rim = Color.Lerp(_bombColor, spark, sparkMix * breath)
                      * Mathf.Lerp(rimIntensityTrough, rimIntensityPeak, breath);
            var dark = _bombColor * coreDarkness;
            rim.a = 1f;
            dark.a = 1f;
            float heat = Mathf.Clamp01((_frozen ? armedHeatPeak : flightHeatPeak) * breath + flash);

            _coreProps ??= new MaterialPropertyBlock();
            if (core) Paint(core, rim, dark, spark, heat);
            if (spikesRenderer) Paint(spikesRenderer, rim, dark, spark, heat);
        }

        void Paint(Renderer target, Color rim, Color dark, Color hot, float heat)
        {
            target.GetPropertyBlock(_coreProps);
            _coreProps.SetColor(BrightColorId, rim);
            _coreProps.SetColor(DarkColorId, dark);
            _coreProps.SetColor(HotColorId, hot);
            _coreProps.SetFloat(HeatId, heat);
            _coreProps.SetFloat(HeatIntensityId, heatIntensity);
            target.SetPropertyBlock(_coreProps);
        }
    }
}
