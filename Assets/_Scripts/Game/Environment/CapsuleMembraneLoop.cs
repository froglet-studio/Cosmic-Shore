using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// The <see cref="CapsuleMembrane"/> wobble loop, generated from its ten-number
    /// <see cref="CapsuleMembraneAnimationSO.BakeSignature"/> instead of read from a baked asset.
    ///
    /// The baked preset (<c>CapsuleMembraneAnimation.asset</c>) is 3.4 MB on disk for what is a
    /// pure function of about 40 bytes of settings: 2,562 capsules x 60 keyframes of rotations.
    /// This class runs that same function (it is the code the bake button runs, so the two agree to
    /// float precision) one KEYFRAME at a time, only when playback first needs that keyframe, and
    /// keeps the result. Playback needs two keyframes at once and advances five per second. The first
    /// two cost one fallback matrix update each (2,562 capsules) when the first membrane wakes; after
    /// that playback warms the next keyframe a slice per matrix update (<see cref="Warm"/>), and after
    /// one loop (12 s) every keyframe exists and generation stops for good.
    ///
    /// One instance per distinct signature, shared process-wide: the 92 cell configs that use the
    /// same membrane share one loop, exactly as they share one baked asset today. A membrane with a
    /// different seed or noise (a per-cell "membrane genome") gets its own loop for free, with no
    /// bake and no asset - which a baked pipeline cannot offer.
    ///
    /// Came out of the Amoebius genome lab's Unity mining pass (Docs/GENOME_MEMBRANE_LOOP.md):
    /// procedural compression of an asset that was already procedural.
    /// </summary>
    public sealed class CapsuleMembraneLoop
    {
        static readonly Dictionary<CapsuleMembraneAnimationSO.BakeSignature, CapsuleMembraneLoop> s_cache = new();

        /// <summary>The shared loop for <paramref name="signature"/>, created on first request.</summary>
        public static CapsuleMembraneLoop Get(in CapsuleMembraneAnimationSO.BakeSignature signature)
        {
            if (!s_cache.TryGetValue(signature, out var loop))
            {
                loop = new CapsuleMembraneLoop(signature);
                s_cache[signature] = loop;
            }
            return loop;
        }

        /// <summary>A private, uncached loop (the editor bake uses this so baking leaves nothing alive).</summary>
        public static CapsuleMembraneLoop Create(in CapsuleMembraneAnimationSO.BakeSignature signature) =>
            new(signature);

        /// <summary>Distinct loops alive (diagnostics and tests).</summary>
        public static int CachedLoopCount => s_cache.Count;

        /// <summary>Drops every cached loop (tests, domain reload).</summary>
        public static void ClearCache() => s_cache.Clear();

        public CapsuleMembraneAnimationSO.BakeSignature Signature { get; }
        public int CapsuleCount { get; }
        public int FrameCount { get; }
        public float LoopDuration { get; }

        /// <summary>Static local-space capsule positions (radius folded in), as the bake stores them.</summary>
        public Vector3[] Offsets { get; }

        /// <summary>Keyframes fully generated so far (0..FrameCount).</summary>
        public int GeneratedFrameCount { get; private set; }

        readonly Vector3[] _directions;
        readonly Vector3[] _perpendiculars;
        readonly Vector3[] _noiseCoords;
        readonly Quaternion[][] _frames;
        readonly int[] _filled;   // capsules generated so far per keyframe (CapsuleCount = complete)
        readonly float _radX, _radY, _radZ;

        CapsuleMembraneLoop(in CapsuleMembraneAnimationSO.BakeSignature sig)
        {
            Signature = sig;
            BuildBase(sig, out _directions, out var offsets, out _perpendiculars, out _noiseCoords);
            Offsets = offsets;
            CapsuleCount = _directions.Length;
            FrameCount = FrameCountFor(sig);
            LoopDuration = sig.loopDuration;
            _frames = new Quaternion[FrameCount][];
            _filled = new int[FrameCount];
            LoopRadii(sig, out _radX, out _radY, out _radZ);
        }

        /// <summary>Keyframes in one loop: <c>round(loopDuration * bakeFps)</c>, at least 2 (the bake's rule).</summary>
        public static int FrameCountFor(in CapsuleMembraneAnimationSO.BakeSignature sig) =>
            Mathf.Max(2, Mathf.RoundToInt(sig.loopDuration * sig.bakeFps));

        /// <summary>Keyframe <paramref name="frame"/>'s per-capsule rotations, completed on first use.</summary>
        public Quaternion[] Frame(int frame)
        {
            Fill(frame, CapsuleCount);
            return _frames[frame];
        }

        /// <summary>
        /// Generate up to <paramref name="capsuleBudget"/> more capsules of keyframe <paramref name="frame"/>
        /// ahead of need, so a keyframe is spread over several updates instead of landing in one. Playback warms
        /// the keyframe after the two it is blending, which is complete before it is reached.
        /// </summary>
        public void Warm(int frame, int capsuleBudget)
        {
            if (_filled[frame] >= CapsuleCount || capsuleBudget <= 0) return;
            Fill(frame, Mathf.Min(CapsuleCount, _filled[frame] + capsuleBudget));
        }

        /// <summary>True once keyframe <paramref name="frame"/> is fully generated.</summary>
        public bool IsComplete(int frame) => _filled[frame] >= CapsuleCount;

        void Fill(int frame, int upTo)
        {
            int from = _filled[frame];
            if (from >= upTo) return;

            var rotations = _frames[frame] ??= new Quaternion[CapsuleCount];
            FillRange(frame, rotations, from, upTo);
            _filled[frame] = upTo;
            if (upTo >= CapsuleCount) GeneratedFrameCount++;
        }

        /// <summary>
        /// The bake's per-keyframe step. The sample point of each capsule's three Perlin channels
        /// moves around a closed circle (not the open line the per-frame fallback uses) so keyframe
        /// N wraps to keyframe 0 seamlessly; each circle's radius keeps the fallback's drift speed.
        /// </summary>
        void FillRange(int frame, Quaternion[] rotations, int from, int upTo)
        {
            float theta = Mathf.PI * 2f * frame / FrameCount;
            float c = Mathf.Cos(theta);
            float s = Mathf.Sin(theta);
            float amplitude = Signature.noiseAmplitude;

            for (int i = from; i < upTo; i++)
            {
                Vector3 nc = _noiseCoords[i];
                Vector3 noiseVec = new(
                    Mathf.PerlinNoise(nc.y + _radX * c, nc.z + _radX * s) * 2f - 1f,
                    Mathf.PerlinNoise(nc.z + _radY * c, nc.x + _radY * s) * 2f - 1f,
                    Mathf.PerlinNoise(nc.x + _radZ * c, nc.y + _radZ * s) * 2f - 1f);

                Quaternion eulerNoise = Quaternion.Euler(amplitude * noiseVec);
                rotations[i] = Quaternion.LookRotation(eulerNoise * _directions[i], eulerNoise * _perpendiculars[i]);
            }
        }

        /// <summary>Every keyframe, frame-major (<c>[frame * CapsuleCount + capsule]</c>), as the bake stores them.</summary>
        public Quaternion[] AllFramesFrameMajor()
        {
            var all = new Quaternion[FrameCount * CapsuleCount];
            for (int f = 0; f < FrameCount; f++)
                System.Array.Copy(Frame(f), 0, all, f * CapsuleCount, CapsuleCount);
            return all;
        }

        static void LoopRadii(in CapsuleMembraneAnimationSO.BakeSignature sig, out float radX, out float radY, out float radZ)
        {
            // Same expression order as the original bake, so the generated loop is bit-identical to it.
            float twoPi = Mathf.PI * 2f;
            radX = sig.pulseSpeed * sig.loopDuration * Mathf.Sqrt(0.3f * 0.3f + 0.5f * 0.5f) / twoPi;
            radY = sig.pulseSpeed * sig.loopDuration * Mathf.Sqrt(0.8f * 0.8f + 0.2f * 0.2f) / twoPi;
            radZ = sig.pulseSpeed * sig.loopDuration * Mathf.Sqrt(0.6f * 0.6f + 0.4f * 0.4f) / twoPi;
        }

        /// <summary>
        /// Per-capsule static layout: jittered direction, offset (radius folded in), look-up vector and
        /// the pre-scaled noise coordinate. Deterministic in the signature's seed; the jitter is drawn
        /// per vertex in index order, so a prefix of the capsules is the layout of a lower subdivision.
        /// </summary>
        public static void BuildBase(in CapsuleMembraneAnimationSO.BakeSignature sig,
            out Vector3[] directions, out Vector3[] offsets, out Vector3[] perpendiculars, out Vector3[] noiseCoords)
        {
            var vertices = GenerateIcosphereVertices(sig.subdivisions);
            int count = vertices.Count;
            directions = new Vector3[count];
            offsets = new Vector3[count];
            perpendiculars = new Vector3[count];
            noiseCoords = new Vector3[count];
            var rng = new System.Random(sig.seed);

            for (int i = 0; i < count; i++)
            {
                Vector3 radialDir = vertices[i];

                Vector3 tangent1 = Vector3.Cross(radialDir, Vector3.up).sqrMagnitude > 0.001f
                    ? Vector3.Cross(radialDir, Vector3.up).normalized
                    : Vector3.Cross(radialDir, Vector3.right).normalized;
                Vector3 tangent2 = Vector3.Cross(radialDir, tangent1).normalized;

                float jitterU = ((float)rng.NextDouble() * 2f - 1f) * sig.placementJitter;
                float jitterV = ((float)rng.NextDouble() * 2f - 1f) * sig.placementJitter;
                float jitterR = ((float)rng.NextDouble() * 2f - 1f) * sig.radialJitter;

                Vector3 jitteredDir = (radialDir + tangent1 * jitterU + tangent2 * jitterV).normalized;

                directions[i] = jitteredDir;
                offsets[i] = jitteredDir * (sig.radius * (1f + jitterR));
                perpendiculars[i] = Vector3.Cross(jitteredDir, Vector3.up).normalized;
                noiseCoords[i] = jitteredDir * sig.noiseFrequency;
            }
        }

        // ---- Icosphere generation (matches SpawnableSpherene algorithm) ----

        /// <summary>Unit icosphere vertices at <paramref name="subdivisionLevel"/>: 12, 42, 162, 642, 2562.</summary>
        public static List<Vector3> GenerateIcosphereVertices(int subdivisionLevel)
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
                var newTriangles = new List<int>(triangles.Count * 4);
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int m01 = GetMidpoint(v0, v1, vertices, midpointCache);
                    int m12 = GetMidpoint(v1, v2, vertices, midpointCache);
                    int m20 = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.Add(v0); newTriangles.Add(m01); newTriangles.Add(m20);
                    newTriangles.Add(v1); newTriangles.Add(m12); newTriangles.Add(m01);
                    newTriangles.Add(v2); newTriangles.Add(m20); newTriangles.Add(m12);
                    newTriangles.Add(m01); newTriangles.Add(m12); newTriangles.Add(m20);
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
    }
}
