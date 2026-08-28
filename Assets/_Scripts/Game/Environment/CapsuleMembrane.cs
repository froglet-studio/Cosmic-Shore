using System.Collections.Generic;
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

        [Tooltip("Wall-clock length of one full wobble loop, in seconds. Bake input.")]
        [Min(1f)]
        [SerializeField] float loopDuration = 12f;

        [Tooltip("Baked keyframes per second. Higher = smoother but larger asset. The runtime " +
                 "interpolates between keyframes, so low values are plenty for the slow wobble. Bake input.")]
        [Range(1, 30)]
        [SerializeField] int bakeFps = 5;

        Matrix4x4[] matrices;
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

        // ---- Fallback per-frame computation state (valid when playbackMode is false) ----
        Vector3[] baseDirections;
        Vector3[] baseOffsets;
        Vector3[] perpendiculars;
        // Noise sampling coordinates (one per capsule, derived from jittered position)
        Vector3[] noiseCoords;

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

            playbackMode = animationPreset != null
                           && animationPreset.IsValid
                           && animationPreset.Signature.Matches(CurrentSignature);

            if (playbackMode)
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
                      "expensive per-frame fallback. Bake one via the inspector's 'Bake Animation Preset' button."
                    : $"{nameof(CapsuleMembrane)} '{name}': baked preset is stale or invalid - running the " +
                      "expensive per-frame fallback. Re-bake via the inspector's 'Bake Animation Preset' button.",
                    this);

                BuildBaseData();
                matrices = new Matrix4x4[baseDirections.Length];
            }

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
                Graphics.RenderMeshInstanced(renderParams, meshToRender, 0, matrices);
        }

        void UpdateMatrices()
        {
            if (playbackMode) UpdateMatricesFromPreset();
            else UpdateMatricesRuntime();
        }

        /// <summary>
        /// Cheap path: interpolate the two nearest baked keyframes and build the
        /// per-capsule transform matrices. No Perlin noise, no quaternion construction.
        /// </summary>
        void UpdateMatricesFromPreset()
        {
            int count = presetCount;
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

            for (int i = 0; i < count; i++)
            {
                Quaternion rot = Quaternion.Lerp(presetRotations[baseA + i], presetRotations[baseB + i], blend);
                matrices[i] = Matrix4x4.TRS(center + presetOffsets[i], rot, capsuleScale);
            }
        }

        /// <summary>
        /// Fallback path: the original per-frame Perlin/quaternion/matrix computation,
        /// with the dead radial-noise samples and per-frame constant work removed.
        /// Only used when no valid baked preset is assigned.
        /// </summary>
        void UpdateMatricesRuntime()
        {
            Vector3 center = transform.position;
            float time = Time.time * pulseSpeed;
            int count = baseDirections.Length;

            for (int i = 0; i < count; i++)
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
            var vertices = GenerateIcosphereVertices(subdivisions);
            int count = vertices.Count;
            baseDirections = new Vector3[count];
            baseOffsets = new Vector3[count];
            perpendiculars = new Vector3[count];
            noiseCoords = new Vector3[count];
            var rng = new System.Random(seed);

            for (int i = 0; i < count; i++)
            {
                Vector3 radialDir = vertices[i];

                Vector3 tangent1 = Vector3.Cross(radialDir, Vector3.up).sqrMagnitude > 0.001f
                    ? Vector3.Cross(radialDir, Vector3.up).normalized
                    : Vector3.Cross(radialDir, Vector3.right).normalized;
                Vector3 tangent2 = Vector3.Cross(radialDir, tangent1).normalized;

                float jitterU = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterV = ((float)rng.NextDouble() * 2f - 1f) * placementJitter;
                float jitterR = ((float)rng.NextDouble() * 2f - 1f) * radialJitter;

                Vector3 jitteredDir = (radialDir + tangent1 * jitterU + tangent2 * jitterV).normalized;

                baseDirections[i] = jitteredDir;
                // Capsule's static local position (radius folded in) and the look-up vector,
                // both constant per capsule - precomputed so the hot loop stays branch-free.
                baseOffsets[i] = jitteredDir * (radius * (1f + jitterR));
                perpendiculars[i] = Vector3.Cross(jitteredDir, Vector3.up).normalized;

                // Pre-scale the noise coordinate so we only add the time/phase term later.
                noiseCoords[i] = jitteredDir * noiseFrequency;
            }
        }

        /// <summary>
        /// Computes the seamless looping wobble animation and writes it into
        /// <paramref name="target"/>. This is the offline bake step driven by the
        /// inspector's "Bake Animation Preset" button - never called at runtime.
        /// </summary>
        public void BakeAnimationInto(CapsuleMembraneAnimationSO target, System.Action<float> onProgress = null)
        {
            if (target == null) return;

            BuildBaseData();
            int count = baseDirections.Length;
            int frameCount = Mathf.Max(2, Mathf.RoundToInt(loopDuration * bakeFps));

            // The original animation drives the Perlin sample point along an open line
            // (coord + time * k), so it never repeats. To bake a seamless loop we move
            // the sample point around a closed circle instead. Each circle's radius is
            // sized so the point traverses the noise field at the same speed as the
            // original linear drift - this preserves the wobble's tempo and character
            // while making frame 0 and frame N identical.
            float twoPi = Mathf.PI * 2f;
            float radX = pulseSpeed * loopDuration * Mathf.Sqrt(0.3f * 0.3f + 0.5f * 0.5f) / twoPi;
            float radY = pulseSpeed * loopDuration * Mathf.Sqrt(0.8f * 0.8f + 0.2f * 0.2f) / twoPi;
            float radZ = pulseSpeed * loopDuration * Mathf.Sqrt(0.6f * 0.6f + 0.4f * 0.4f) / twoPi;

            var offsets = (Vector3[])baseOffsets.Clone();
            var rotations = new Quaternion[frameCount * count];

            for (int f = 0; f < frameCount; f++)
            {
                float theta = twoPi * f / frameCount;
                float c = Mathf.Cos(theta);
                float s = Mathf.Sin(theta);
                int frameBase = f * count;

                for (int i = 0; i < count; i++)
                {
                    Vector3 nc = noiseCoords[i];
                    Vector3 noiseVec = new(
                        Mathf.PerlinNoise(nc.y + radX * c, nc.z + radX * s) * 2f - 1f,
                        Mathf.PerlinNoise(nc.z + radY * c, nc.x + radY * s) * 2f - 1f,
                        Mathf.PerlinNoise(nc.x + radZ * c, nc.y + radZ * s) * 2f - 1f);

                    Quaternion eulerNoise = Quaternion.Euler(noiseAmplitude * noiseVec);
                    rotations[frameBase + i] = Quaternion.LookRotation(
                        eulerNoise * baseDirections[i], eulerNoise * perpendiculars[i]);
                }

                onProgress?.Invoke((f + 1) / (float)frameCount);
            }

            target.SetBakedData(offsets, rotations, count, frameCount, loopDuration, CurrentSignature);
        }

        static Mesh GetBuiltinCapsuleMesh()
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);
            return mesh;
        }

        // ---- Icosphere generation (matches SpawnableSpherene algorithm) ----

        static List<Vector3> GenerateIcosphereVertices(int subdivisionLevel)
        {
            float phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            float a = 1f;
            float b = 1f / phi;

            var vertices = new List<Vector3>
            {
                new( 0,  b, -a),
                new( b,  a,  0),
                new(-b,  a,  0),
                new( 0,  b,  a),
                new( 0, -b,  a),
                new(-a,  0,  b),
                new( 0, -b, -a),
                new( a,  0, -b),
                new( a,  0,  b),
                new(-a,  0, -b),
                new( b, -a,  0),
                new(-b, -a,  0),
            };

            for (int i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;

            var triangles = new List<int>
            {
                2, 1, 0,   1, 2, 3,   5, 2, 3,   3, 8, 1,   3, 4, 8,
                3, 5, 4,   0, 1, 7,   7, 1, 8,   6, 7, 8,   6, 8,10,
                8, 4,10,   4,11,10,   4, 5,11,  11, 5, 9,   9, 5, 2,
                9, 2, 0,   6, 0, 7,   9, 0, 6,  11, 9, 6,  10,11, 6,
            };

            var midpointCache = new Dictionary<(int, int), int>();

            for (int level = 0; level < subdivisionLevel; level++)
            {
                var newTriangles = new List<int>();
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                    int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                    int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.AddRange(new[] { v0, m01, m20 });
                    newTriangles.AddRange(new[] { v1, m12, m01 });
                    newTriangles.AddRange(new[] { v2, m20, m12 });
                    newTriangles.AddRange(new[] { m01, m12, m20 });
                }

                triangles = newTriangles;
            }

            return vertices;
        }

        static int GetMidpoint(int a, int b, List<Vector3> vertices, Dictionary<(int, int), int> cache)
        {
            var key = a < b ? (a, b) : (b, a);
            if (cache.TryGetValue(key, out int index))
                return index;

            Vector3 midpoint = ((vertices[a] + vertices[b]) * 0.5f).normalized;
            index = vertices.Count;
            vertices.Add(midpoint);
            cache[key] = index;
            return index;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
