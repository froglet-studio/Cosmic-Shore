using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    /// <summary>
    /// Renders an icospheric arrangement of capsules as the cell membrane.
    /// All capsules are drawn in a single instanced draw call via Graphics.RenderMeshInstanced.
    ///
    /// The membrane's organic wobble is precomputed offline into a
    /// <see cref="CapsuleMembraneAnimationSO"/> preset (via the "Bake Animation Preset"
    /// button on this component's inspector). At runtime the membrane only interpolates
    /// the baked rotations - no Perlin noise, quaternion or matrix construction per
    /// capsule per frame. If no valid preset is assigned it falls back to the original
    /// per-frame computation (and logs a warning) so the membrane still renders.
    ///
    /// <see cref="MembraneAnimationSource.GenerateFromSettings"/> needs no preset at all: the same loop is
    /// generated from the bake inputs on demand and shared (<see cref="CapsuleMembraneLoop"/>,
    /// Docs/GENOME_MEMBRANE_LOOP.md), and <see cref="ApplyWobble"/> gives a membrane its own wobble.
    ///
    /// Setup: Attach to a GameObject, assign any material (SpindleMaterial works directly),
    /// then click "Bake Animation Preset" on the inspector.
    /// </summary>
    public class CapsuleMembrane : MonoBehaviour
    {
        [Header("Icosphere Layout")]
        [Tooltip("Subdivision level. 0=12, 1=42, 2=162, 3=642 capsules.")]
        [Range(0, 4)]
        [SerializeField] int subdivisions = 2;

        [Tooltip("Radius of the membrane sphere.")]
        [SerializeField] float radius = 500f;

        public float Radius => radius;

        [Header("Capsule Shape")]
        [Tooltip("Mesh to instance. Leave null to use Unity's built-in capsule.")]
        [SerializeField] Mesh capsuleMesh;

        [Tooltip("Scale of each capsule in local space (X=width, Y=height/length, Z=depth).")]
        [SerializeField] Vector3 capsuleScale = new(12f, 30f, 12f);

        [Header("Placement Noise")]
        [Tooltip("How much each capsule's position is jittered tangentially on the sphere surface.")]
        [SerializeField] float placementJitter = 0.15f;

        [Tooltip("How much each capsule's radial distance varies from the base radius.")]
        [SerializeField] float radialJitter = 0.05f;

        [Tooltip("Seed for deterministic placement noise.")]
        [SerializeField] int seed = 42;

        [Header("Radial Pulse")]
        [Tooltip("Spatial frequency of the Perlin noise driving the radial pulse.")]
        [SerializeField] float noiseFrequency = 0.6f;

        [Tooltip("Maximum radial displacement in world units.")]
        [SerializeField] float noiseAmplitude = 40f;

        [Tooltip("How fast the pulse pattern drifts over time.")]
        [SerializeField] float pulseSpeed = 0.4f;

        [Header("Rendering")]
        [SerializeField] Material membraneMaterial;

        [Tooltip("Rendering layer mask for the instanced draw.")]
        [SerializeField] uint renderingLayerMask = 1;

        [Tooltip("Matrix rebuild rate for the wobble, in Hz. The baked loop is slow (seconds per " +
                 "cycle), so rebuilding every capsule's TRS matrix at frame rate is wasted work — " +
                 "20Hz is visually identical at membrane scale. The instanced draw still submits " +
                 "every frame with the cached matrices. 0 = rebuild every frame. A membrane that " +
                 "moves rebuilds immediately regardless of cadence.")]
        [Range(0, 60)]
        [SerializeField] int matrixUpdateHz = 20;

        [Header("Baked Animation")]
        [Tooltip("Baked animation preset. When assigned and up to date, the membrane plays " +
                 "it back cheaply instead of computing the wobble every frame. Bake/re-bake " +
                 "with the button below the default fields.")]
        [SerializeField] CapsuleMembraneAnimationSO animationPreset;

        [Tooltip("Where the wobble loop comes from. BakedPreset plays the assigned asset (3.4 MB for the shipped " +
                 "membrane). GenerateFromSettings computes the same loop from the ten bake inputs above, one keyframe " +
                 "at a time as playback first needs it, shared by every membrane with the same settings - no asset, " +
                 "no bake, no load spike. Both give the same rotations to float precision (CapsuleMembraneLoopTests).")]
        [SerializeField] MembraneAnimationSource animationSource = MembraneAnimationSource.BakedPreset;

        [Tooltip("Wall-clock length of one full wobble loop, in seconds. Bake input.")]
        [Min(1f)]
        [SerializeField] float loopDuration = 12f;

        [Tooltip("Baked keyframes per second. Higher = smoother but larger asset. The runtime " +
                 "interpolates between keyframes, so low values are plenty for the slow wobble. Bake input.")]
        [Range(1, 30)]
        [SerializeField] int bakeFps = 5;

        Matrix4x4[] matrices;

        /// <summary>
        /// Capsules actually drawn: all of them, unless the device tier or quality preset caps the
        /// subdivision level (<c>GraphicsCuts.MembraneMaxSubdivisions</c>). The icosphere generator only ever
        /// APPENDS midpoint vertices, and the jitter is drawn per vertex in index order, so the
        /// first 10*4^s+2 capsules are exactly the layout a membrane authored at level s would have,
        /// and the baked animation indexes them the same way. Drawing a prefix is therefore a
        /// lower-subdivision membrane with no re-bake, at the same radius.
        /// </summary>
        int _drawCount;

        RenderParams renderParams;
        Mesh meshToRender;
        bool playbackMode;
        float _nextMatrixUpdateAt;
        Vector3 _lastCenter;

        // ---- Playback state (valid when playbackMode is true) ----
        Quaternion[] presetRotations;   // frame-major: [frame * presetCount + capsule]
        Vector3[] presetOffsets;
        int presetCount;
        int presetFrameCount;
        float presetLoopDuration;

        // ---- Generated playback state (valid when _loop is not null) ----
        CapsuleMembraneLoop _loop;

        // ---- Fallback per-frame computation state (valid when playbackMode is false) ----
        Vector3[] baseDirections;
        Vector3[] baseOffsets;
        Vector3[] perpendiculars;
        // Noise sampling coordinates (one per capsule, derived from jittered position)
        Vector3[] noiseCoords;

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DullColorId = Shader.PropertyToID("_DullColor");
        MaterialPropertyBlock _colourBlock;

        /// <summary>The membrane's own colours as its material authors them (SpindleGraph's <c>_BrightColor</c> /
        /// <c>_DullColor</c>) - what a mode's tint eases away from and back to. False without a material.</summary>
        public bool TryGetMaterialColours(out Color bright, out Color dull)
        {
            bright = dull = Color.white;
            if (!membraneMaterial) return false;
            if (membraneMaterial.HasProperty(BrightColorId)) bright = membraneMaterial.GetColor(BrightColorId);
            if (membraneMaterial.HasProperty(DullColorId)) dull = membraneMaterial.GetColor(DullColorId);
            return true;
        }

        /// <summary>
        /// Recolour THIS membrane's draw (a property block on its instanced draw) - never the material, which is
        /// <c>SpindleMaterial</c>, shared with every spindle, worm segment and two fauna prefabs. A mode-side visual
        /// (<see cref="CosmicShore.Gameplay.CellVisualTint"/>); the membrane's boundary and gameplay are untouched.
        /// </summary>
        public void SetColourOverride(Color bright, Color dull)
        {
            _colourBlock ??= new MaterialPropertyBlock();
            _colourBlock.SetColor(BrightColorId, bright);
            _colourBlock.SetColor(DullColorId, dull);
            renderParams.matProps = _colourBlock;
        }

        /// <summary>Back to the material's own colours.</summary>
        public void ClearColourOverride() => renderParams.matProps = null;

        /// <summary>The baked preset currently assigned, if any. Used by the editor bake tooling.</summary>
        public CapsuleMembraneAnimationSO AnimationPreset => animationPreset;

        /// <summary>Bake-input fingerprint of the membrane's current settings.</summary>
        public CapsuleMembraneAnimationSO.BakeSignature CurrentSignature => new()
        {
            subdivisions = subdivisions,
            radius = radius,
            seed = seed,
            placementJitter = placementJitter,
            radialJitter = radialJitter,
            noiseFrequency = noiseFrequency,
            noiseAmplitude = noiseAmplitude,
            pulseSpeed = pulseSpeed,
            loopDuration = loopDuration,
            bakeFps = bakeFps,
        };

        void Awake()
        {
            meshToRender = capsuleMesh != null ? capsuleMesh : GetBuiltinCapsuleMesh();

            _loop = animationSource == MembraneAnimationSource.GenerateFromSettings
                ? CapsuleMembraneLoop.Get(CurrentSignature)
                : null;

            playbackMode = _loop == null
                           && animationPreset != null
                           && animationPreset.IsValid
                           && animationPreset.Signature.Matches(CurrentSignature);

            if (_loop != null)
            {
                matrices = new Matrix4x4[_loop.CapsuleCount];
            }
            else if (playbackMode)
            {
                presetOffsets = animationPreset.CapsuleOffsets;
                presetRotations = animationPreset.Rotations;
                presetCount = animationPreset.CapsuleCount;
                presetFrameCount = animationPreset.FrameCount;
                presetLoopDuration = animationPreset.LoopDuration;
                matrices = new Matrix4x4[presetCount];
            }
            else
            {
                CSDebug.LogWarning(animationPreset == null
                    ? $"{nameof(CapsuleMembrane)} '{name}': no baked animation preset assigned - running the " +
                      "expensive per-frame fallback. Bake one via the inspector's 'Bake Animation Preset' button, " +
                      "or set Animation Source to GenerateFromSettings."
                    : $"{nameof(CapsuleMembrane)} '{name}': baked preset is stale or invalid - running the " +
                      "expensive per-frame fallback. Re-bake via the inspector's 'Bake Animation Preset' button.",
                    this);

                BuildBaseData();
                matrices = new Matrix4x4[baseDirections.Length];
            }

            _drawCount = TierCappedCount(matrices.Length);

            renderParams = new RenderParams(membraneMaterial)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * (radius * 2.5f)),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                renderingLayerMask = renderingLayerMask,
            };

            _lastCenter = transform.position;
            UpdateMatrices();
        }

        // Split markers: matrix math (CPU, scales with capsule count) vs the instanced
        // submit (copies the matrix array to the render thread — where a spike means
        // render-thread sync, not this component's math).
        static readonly ProfilerMarker s_updateMatrices = new("CapsuleMembrane.UpdateMatrices");
        static readonly ProfilerMarker s_submit = new("CapsuleMembrane.RenderMeshInstanced");

        void Update()
        {
            if (meshToRender == null || membraneMaterial == null) return;

            // The wobble loop is seconds long — rebuild the per-capsule matrices at
            // matrixUpdateHz (or when the membrane moves) and submit the cached
            // array every frame. The submit is the only per-frame requirement:
            // RenderMeshInstanced is an immediate-mode draw.
            Vector3 center = transform.position;
            bool moved = (center - _lastCenter).sqrMagnitude > 0.25f;
            if (matrixUpdateHz <= 0 || moved || Time.time >= _nextMatrixUpdateAt)
            {
                _nextMatrixUpdateAt = matrixUpdateHz > 0 ? Time.time + 1f / matrixUpdateHz : 0f;
                _lastCenter = center;
                using (s_updateMatrices.Auto())
                    UpdateMatrices();
                renderParams.worldBounds = new Bounds(center, Vector3.one * (radius * 2.5f));
            }
            using (s_submit.Auto())
                Graphics.RenderMeshInstanced(renderParams, meshToRender, 0, matrices, _drawCount);
        }

        /// <summary>Vertices of an icosphere at <paramref name="level"/> subdivisions: 12, 42, 162, 642, 2562.</summary>
        public static int IcosphereVertexCount(int level) => 10 * (1 << (2 * level)) + 2;

        /// <summary>
        /// <paramref name="total"/> capsules, or the prefix the device tier and quality preset allow
        /// (<see cref="GraphicsCuts.MembraneMaxSubdivisions"/>). A cap at or above this membrane's own
        /// level changes nothing.
        /// </summary>
        int TierCappedCount(int total)
        {
            int cap = GraphicsCuts.MembraneMaxSubdivisions;
            if (cap < 0 || cap >= subdivisions) return total;
            return Mathf.Min(total, IcosphereVertexCount(cap));
        }

        void UpdateMatrices()
        {
            if (_loop != null) UpdateMatricesFromLoop();
            else if (playbackMode) UpdateMatricesFromPreset();
            else UpdateMatricesRuntime();
        }

        /// <summary>
        /// Cheap path: interpolate the two nearest baked keyframes and build the
        /// per-capsule transform matrices. No Perlin noise, no quaternion construction.
        /// </summary>
        void UpdateMatricesFromPreset()
        {
            int count = presetCount;   // the bake's stride: frame-major, every capsule per frame
            int frames = presetFrameCount;
            float dur = presetLoopDuration;

            float fpos = dur > 0f ? Mathf.Repeat(Time.time, dur) / dur * frames : 0f;
            int f0 = (int)fpos;
            if (f0 >= frames) f0 = 0;
            int f1 = f0 + 1;
            if (f1 >= frames) f1 = 0;
            float blend = fpos - Mathf.Floor(fpos);

            Vector3 center = transform.position;
            int baseA = f0 * count;
            int baseB = f1 * count;

            for (int i = 0; i < _drawCount; i++)
            {
                Quaternion rot = Quaternion.Lerp(presetRotations[baseA + i], presetRotations[baseB + i], blend);
                matrices[i] = Matrix4x4.TRS(center + presetOffsets[i], rot, capsuleScale);
            }
        }

        /// <summary>
        /// Generated path: the same interpolation as <see cref="UpdateMatricesFromPreset"/>, reading the two
        /// keyframes from the shared <see cref="CapsuleMembraneLoop"/>, which generates each keyframe the
        /// first time any membrane needs it.
        /// </summary>
        void UpdateMatricesFromLoop()
        {
            int frames = _loop.FrameCount;
            float dur = _loop.LoopDuration;

            float fpos = dur > 0f ? Mathf.Repeat(Time.time, dur) / dur * frames : 0f;
            int f0 = (int)fpos;
            if (f0 >= frames) f0 = 0;
            int f1 = f0 + 1;
            if (f1 >= frames) f1 = 0;
            float blend = fpos - Mathf.Floor(fpos);

            Quaternion[] a = _loop.Frame(f0);
            Quaternion[] b = _loop.Frame(f1);

            // Spread the keyframe after next over the updates before it is needed: a keyframe lasts
            // 1/bakeFps s, i.e. matrixUpdateHz/bakeFps updates (4 at the defaults), so a third of it per
            // update completes it with room to spare. A no-op once that keyframe exists.
            int f2 = f1 + 1 >= frames ? 0 : f1 + 1;
            _loop.Warm(f2, (_loop.CapsuleCount + 2) / 3);
            Vector3[] offsets = _loop.Offsets;
            Vector3 center = transform.position;

            for (int i = 0; i < _drawCount; i++)
                matrices[i] = Matrix4x4.TRS(center + offsets[i], Quaternion.Lerp(a[i], b[i], blend), capsuleScale);
        }

        /// <summary>
        /// Give this membrane a different wobble (a per-cell "membrane genome": seed, noise frequency, noise
        /// amplitude, pulse speed) and switch it to the generated loop. Needs no bake and no asset; membranes
        /// with equal settings still share one loop. The layout keeps its subdivision level, so the capsule
        /// count and the device-tier cap are unchanged. Gameplay never reads the wobble.
        /// </summary>
        public void ApplyWobble(int wobbleSeed, float wobbleNoiseFrequency, float wobbleNoiseAmplitude, float wobblePulseSpeed)
        {
            seed = wobbleSeed;
            noiseFrequency = wobbleNoiseFrequency;
            noiseAmplitude = wobbleNoiseAmplitude;
            pulseSpeed = wobblePulseSpeed;
            animationSource = MembraneAnimationSource.GenerateFromSettings;
            if (matrices == null) return;   // before Awake: Awake picks the settings up

            _loop = CapsuleMembraneLoop.Get(CurrentSignature);
            playbackMode = false;
            if (matrices.Length != _loop.CapsuleCount)
            {
                matrices = new Matrix4x4[_loop.CapsuleCount];
                _drawCount = TierCappedCount(matrices.Length);
            }
            UpdateMatrices();
        }

        /// <summary>Where the wobble comes from right now (diagnostics, tests).</summary>
        public MembraneAnimationSource AnimationSource => animationSource;

        /// <summary>
        /// Fallback path: the original per-frame Perlin/quaternion/matrix computation,
        /// with the dead radial-noise samples and per-frame constant work removed.
        /// Only used when no valid baked preset is assigned.
        /// </summary>
        void UpdateMatricesRuntime()
        {
            Vector3 center = transform.position;
            float time = Time.time * pulseSpeed;

            for (int i = 0; i < _drawCount; i++)
            {
                Vector3 nc = noiseCoords[i];
                // Sample Perlin noise at the capsule's sphere-surface coordinate + animated
                // time offset. Three samples give a pseudo-3D euler wobble per capsule.
                Vector3 noiseVec = new(
                    Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f,
                    Mathf.PerlinNoise(nc.z + time * 0.8f, nc.x + time * 0.2f) * 2f - 1f,
                    Mathf.PerlinNoise(nc.x + time * 0.6f, nc.y + time * 0.4f) * 2f - 1f);

                Quaternion eulerNoise = Quaternion.Euler(noiseAmplitude * noiseVec);
                Quaternion rot = Quaternion.LookRotation(
                    eulerNoise * baseDirections[i], eulerNoise * perpendiculars[i]);
                matrices[i] = Matrix4x4.TRS(center + baseOffsets[i], rot, capsuleScale);
            }
        }

        void BuildBaseData()
        {
            CapsuleMembraneLoop.BuildBase(CurrentSignature,
                out baseDirections, out baseOffsets, out perpendiculars, out noiseCoords);
        }

        /// <summary>
        /// Computes the seamless looping wobble animation and writes it into
        /// <paramref name="target"/>. This is the offline bake step driven by the
        /// inspector's "Bake Animation Preset" button - never called at runtime.
        /// </summary>
        public void BakeAnimationInto(CapsuleMembraneAnimationSO target, System.Action<float> onProgress = null)
        {
            if (target == null) return;

            // The bake IS the generated loop written out: one code path, so a baked asset and a membrane
            // set to GenerateFromSettings can never disagree. A private instance (not the shared cache)
            // so baking in the editor leaves no loop alive behind it.
            var loop = CapsuleMembraneLoop.Create(CurrentSignature);
            var rotations = new Quaternion[loop.FrameCount * loop.CapsuleCount];
            for (int f = 0; f < loop.FrameCount; f++)
            {
                System.Array.Copy(loop.Frame(f), 0, rotations, f * loop.CapsuleCount, loop.CapsuleCount);
                onProgress?.Invoke((f + 1) / (float)loop.FrameCount);
            }

            target.SetBakedData((Vector3[])loop.Offsets.Clone(), rotations,
                loop.CapsuleCount, loop.FrameCount, loop.LoopDuration, CurrentSignature);
        }

        static Mesh GetBuiltinCapsuleMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return mesh;
        }

        // The icosphere generator lives in CapsuleMembraneLoop; DeviceTierTests reflects on this name to pin
        // that a lower level is a prefix of a higher one.
        static List<Vector3> GenerateIcosphereVertices(int subdivisionLevel) =>
            CapsuleMembraneLoop.GenerateIcosphereVertices(subdivisionLevel);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }

    /// <summary>Where a <see cref="CapsuleMembrane"/>'s wobble loop comes from.</summary>
    public enum MembraneAnimationSource
    {
        /// <summary>Play the assigned baked <see cref="CapsuleMembraneAnimationSO"/>.</summary>
        BakedPreset = 0,
        /// <summary>Generate the same loop from the bake inputs, on demand (<see cref="CapsuleMembraneLoop"/>).</summary>
        GenerateFromSettings = 1,
    }
}
