using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The look of a Grizzly TRIGGER BOMB in flight (GRIZZLY_TRIGGER_BOMBS.md) - on the root of
    /// GrizzlyBomb.prefab, driven by <see cref="GrizzlyTriggerBombExecutor"/>.
    ///
    /// <para><b>A bomb reads as a bomb by its PULSE.</b> The core is a small dark body with a hot
    /// fresnel rim; an additive glow billboard around it breathes at a rate that climbs as the
    /// throw slows (slow at the muzzle, quick once it hangs at rest). It has no fuse - only the
    /// trigger detonates it - so a bomb at rest keeps breathing at the rest rate for as long as
    /// it is left there. Freezing it (the second pull) ARMS it: the streak stops, the halo flares
    /// wide and strobes at the armed rate - "this one is about to go" - until the release blows
    /// it. Every bomb is DANGER-coloured, LT's and RT's turned a little apart on the hue wheel so
    /// the two read as two (<see cref="GrizzlyTriggerBombConfigSO.BombColor"/>).</para>
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
        [SerializeField, Tooltip("An additive glow quad around the core that breathes as the throw slows and flares when armed. Turned to face the camera every frame.")]
        Transform halo;
        [SerializeField, Tooltip("The halo's renderer (an additive particle glow material, tinted through _BaseColor).")]
        Renderer haloRenderer;
        [SerializeField, Tooltip("A short streak behind the bomb while it flies. Stops emitting when the bomb is frozen.")]
        TrailRenderer trail;
        [SerializeField, Tooltip("Streak width as a fraction of the bomb's diameter. A trail's width is in WORLD units, so it is re-sized per shot from the bomb's own scale.")]
        float trailWidthFactor = 0.7f;

        [Header("Pulse")]
        [SerializeField, Tooltip("Halo breaths per second as the bomb leaves the muzzle.")]
        float pulseHzAtLaunch = 2.5f;
        [FormerlySerializedAs("pulseHzAtFuseEnd")]
        [SerializeField, Tooltip("Halo breaths per second once the throw has run out and the bomb hangs at rest, waiting on the trigger.")]
        float pulseHzAtRest = 11f;
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
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _coreProps;
        MaterialPropertyBlock _haloProps;
        Color _bombColor = Color.white;
        Domains _domain = Domains.Blue;
        float _armedAt;
        float _throwSeconds = 1f;
        float _phase;
        bool _frozen;
        bool _live;
        Camera _camera;

        // The lit wake: a short ring buffer of where the bomb has been.
        const int WakeSamples = 16;
        readonly Vector3[] _wakePos = new Vector3[WakeSamples];
        readonly float[] _wakeTime = new float[WakeSamples];
        int _wakeHead;
        int _wakeCount;

        /// <summary>
        /// Called by the executor right after the bomb leaves the muzzle: tint it with its
        /// trigger's danger shade, remember whose it is (the LIT tint), start the throw clock,
        /// clear the streak a pooled shell carried from its last flight.
        /// </summary>
        public void Arm(Color bombColor, Domains domain, float throwSeconds)
        {
            _bombColor = bombColor;
            _domain = domain;
            _throwSeconds = Mathf.Max(0.05f, throwSeconds);
            _armedAt = Time.time;
            _phase = 0f;
            _frozen = false;
            _live = true;
            _camera = Camera.main;
            _wakeHead = 0;
            _wakeCount = 0;

            if (trail)
            {
                trail.Clear();
                trail.widthMultiplier = Mathf.Max(0.05f, transform.lossyScale.x * trailWidthFactor);
                trail.emitting = true;
                var c = bombColor;
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

            float hz;
            float size;
            if (_frozen)
            {
                hz = armedPulseHz;
                size = armedHaloScale;
            }
            else
            {
                // Quickens as the throw slows (quadratically, so the change is felt near the
                // end), then holds the rest rate for as long as the bomb is left hanging.
                float spent = Mathf.Clamp01((Time.time - _armedAt) / _throwSeconds);
                hz = Mathf.Lerp(pulseHzAtLaunch, pulseHzAtRest, spent * spent);
                size = haloScale;
            }

            _phase += hz * Time.deltaTime * 2f * Mathf.PI;
            float breath = 0.5f + 0.5f * Mathf.Sin(_phase);   // 0 trough .. 1 peak
            Apply(breath, size * (1f + pulseAmplitude * breath));

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

        void LateUpdate()
        {
            if (!_live || !halo) return;
            if (!_camera) _camera = Camera.main;
            if (_camera) halo.rotation = _camera.transform.rotation;   // billboard
        }

        void Apply(float breath, float haloSize)
        {
            var rim = Color.Lerp(_bombColor, sparkColor, sparkMix * breath)
                      * Mathf.Lerp(rimIntensityTrough, rimIntensityPeak, breath);
            var dark = _bombColor * coreDarkness;
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
