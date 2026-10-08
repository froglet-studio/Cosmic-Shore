using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The look of a Grizzly TRIGGER BOMB in flight (GRIZZLY_TRIGGER_BOMBS.md) - on the root of
    /// GrizzlyBomb.prefab, driven by <see cref="GrizzlyTriggerBombExecutor"/>.
    ///
    /// <para><b>A bomb reads as a bomb by its FUSE.</b> The core is a small dark body with a hot
    /// fresnel rim; an additive glow billboard around it breathes at a rate that climbs as the fuse burns (slow at the muzzle, frantic at
    /// the end), so a pilot can read how long a bomb has left without a HUD. Freezing it (the
    /// second pull) ARMS it: the streak stops, the halo flares wide and strobes at the armed rate
    /// - "this one is about to go" - until the release blows it. Everything is tinted the firing
    /// pilot's DOMAIN so a rival's bomb and your own are told apart at a glance.</para>
    ///
    /// <para><b>Small before, big after</b> is the design owner's ask: the bomb itself is a few
    /// units across (its scale is set per shot by the executor from the squeeze) and the blast
    /// it becomes is tens of times that. The halo is the only part allowed to swell, and only to
    /// say "armed".</para>
    ///
    /// <para>All colour rides a <see cref="MaterialPropertyBlock"/> - the core's shared fresnel
    /// material (the shader's HDR <c>_BrightColor</c> rim and <c>_DarkColor</c> body) and the
    /// halo's shared additive glow (<c>_BaseColor</c>) - never <c>renderer.material</c>. The halo
    /// is a camera-facing QUAD, not a sphere: the glow texture is a soft radial sprite, and the
    /// core's fresnel shader writes depth, so a shell around the core would have hidden it. The streak's colour is the TrailRenderer's own per-renderer
    /// gradient, which mints no material either. Pooled: <see cref="Arm"/> resets every field, and
    /// the visual goes idle on disable.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GrizzlyBombVisual : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField, Tooltip("The bomb's hot core - the root sphere.")]
        Renderer core;
        [SerializeField, Tooltip("An additive glow quad around the core that breathes with the fuse and flares when armed. Turned to face the camera every frame.")]
        Transform halo;
        [SerializeField, Tooltip("The halo's renderer (an additive particle glow material, tinted through _BaseColor).")]
        Renderer haloRenderer;
        [SerializeField, Tooltip("A short streak behind the bomb while it flies. Stops emitting when the bomb is frozen.")]
        TrailRenderer trail;
        [SerializeField, Tooltip("Streak width as a fraction of the bomb's diameter. A trail's width is in WORLD units, so it is re-sized per shot from the bomb's own scale.")]
        float trailWidthFactor = 0.7f;

        [Header("Fuse pulse")]
        [SerializeField, Tooltip("Halo breaths per second as the bomb leaves the muzzle.")]
        float pulseHzAtLaunch = 2.5f;
        [SerializeField, Tooltip("Halo breaths per second at the end of the fuse - the 'about to go' rate.")]
        float pulseHzAtFuseEnd = 11f;
        [SerializeField, Tooltip("Halo size relative to the core while flying (the glow sprite fades to nothing well inside its quad, so this reads smaller than it sounds).")]
        float haloScale = 3f;
        [SerializeField, Tooltip("How far the halo swells on each breath, as a fraction of its size.")]
        [Range(0f, 1f)] float pulseAmplitude = 0.22f;

        [Header("Armed (frozen)")]
        [SerializeField, Tooltip("Halo size relative to the core once the bomb is frozen - it flares to say 'armed'.")]
        float armedHaloScale = 5f;
        [SerializeField, Tooltip("Strobe rate of a frozen bomb, breaths per second.")]
        float armedPulseHz = 14f;

        [Header("Colour")]
        [SerializeField, Tooltip("HDR multiplier on the domain colour at the peak of a breath - the rim glow.")]
        float rimIntensityPeak = 6f;
        [SerializeField, Tooltip("HDR multiplier on the domain colour at the trough of a breath.")]
        float rimIntensityTrough = 1.5f;
        [SerializeField, Tooltip("Colour the rim flashes toward at the peak of a breath - the fuse spark.")]
        Color sparkColor = new(1f, 0.92f, 0.7f, 1f);
        [SerializeField, Tooltip("How far each peak leans from the domain colour toward the spark colour.")]
        [Range(0f, 1f)] float sparkMix = 0.45f;
        [SerializeField, Tooltip("Core darkness: the domain colour scaled by this. Low keeps the bomb a dark body with a bright rim.")]
        [Range(0f, 1f)] float coreDarkness = 0.12f;

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _coreProps;
        MaterialPropertyBlock _haloProps;
        Color _domainColor = Color.white;
        float _armedAt;
        float _fuseSeconds = 1f;
        float _phase;
        bool _frozen;
        bool _live;
        Camera _camera;

        /// <summary>
        /// Called by the executor right after the bomb leaves the muzzle: tint it, start the fuse
        /// clock, clear the streak a pooled shell carried from its last flight.
        /// </summary>
        public void Arm(Color domainColor, float fuseSeconds)
        {
            _domainColor = domainColor;
            _fuseSeconds = Mathf.Max(0.05f, fuseSeconds);
            _armedAt = Time.time;
            _phase = 0f;
            _frozen = false;
            _live = true;
            _camera = Camera.main;

            if (trail)
            {
                trail.Clear();
                trail.widthMultiplier = Mathf.Max(0.05f, transform.lossyScale.x * trailWidthFactor);
                trail.emitting = true;
                var c = domainColor;
                trail.startColor = new Color(c.r, c.g, c.b, 0.9f);
                trail.endColor = new Color(c.r, c.g, c.b, 0f);
            }
            Apply(1f, haloScale);
        }

        /// <summary>The second pull: the bomb stops and ARMS - the streak ends, the halo flares.</summary>
        public void Freeze()
        {
            _frozen = true;
            if (trail) trail.emitting = false;
        }

        void OnDisable()
        {
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

            float hz;
            float size;
            if (_frozen)
            {
                hz = armedPulseHz;
                size = armedHaloScale;
            }
            else
            {
                // The fuse burns quadratically faster, so the last second is the frantic one.
                float burnt = Mathf.Clamp01((Time.time - _armedAt) / _fuseSeconds);
                hz = Mathf.Lerp(pulseHzAtLaunch, pulseHzAtFuseEnd, burnt * burnt);
                size = haloScale;
            }

            _phase += hz * Time.deltaTime * 2f * Mathf.PI;
            float breath = 0.5f + 0.5f * Mathf.Sin(_phase);   // 0 trough .. 1 peak
            Apply(breath, size * (1f + pulseAmplitude * breath));
        }

        void LateUpdate()
        {
            if (!_live || !halo) return;
            if (!_camera) _camera = Camera.main;
            if (_camera) halo.rotation = _camera.transform.rotation;   // billboard
        }

        void Apply(float breath, float haloSize)
        {
            var rim = Color.Lerp(_domainColor, sparkColor, sparkMix * breath)
                      * Mathf.Lerp(rimIntensityTrough, rimIntensityPeak, breath);
            var dark = _domainColor * coreDarkness;
            rim.a = 1f;
            dark.a = 1f;

            if (core)
            {
                _coreProps ??= new MaterialPropertyBlock();
                core.GetPropertyBlock(_coreProps);
                _coreProps.SetColor(BrightColorId, rim);
                _coreProps.SetColor(DarkColorId, dark);
                core.SetPropertyBlock(_coreProps);
            }

            if (halo) halo.localScale = Vector3.one * haloSize;

            if (haloRenderer)
            {
                _haloProps ??= new MaterialPropertyBlock();
                haloRenderer.GetPropertyBlock(_haloProps);
                // Additive: the colour IS the light. The rim's hue, a little softer.
                var glow = rim * 0.35f;
                glow.a = 1f;
                _haloProps.SetColor(BaseColorId, glow);
                haloRenderer.SetPropertyBlock(_haloProps);
            }
        }
    }
}
