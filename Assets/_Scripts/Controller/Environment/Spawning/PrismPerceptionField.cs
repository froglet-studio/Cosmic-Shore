using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// PROOF OF CONCEPT — "prism constructs, perception" (branch: prism-constructs-perception).
    ///
    /// Builds a static prism construct that tricks the eye into perceiving MORE than three domains,
    /// the same way a print halftone fools it into a full gamut from three inks. It composes the
    /// existing fundamentals only — it lays ordinary <see cref="Prism"/>s through the canonical
    /// <see cref="PrismTrailBuilder"/> path, one <see cref="Domains"/> per prism, and lets the domain's
    /// own HDR emission + URP Bloom do the additive (partitive) colour mixing. No new colour channel,
    /// no decay, no timer: the field is a fixed stock of conserved mass that blooms in and then just
    /// exists (continuity + mass-conservation laws).
    ///
    /// The trick that makes it work: a prism's <c>_BrightColor</c> emission is NOT its team colour —
    /// Jade glows azure-cyan, Ruby violet, Gold amber (a near-CMY triad, values in
    /// OriginalColorSetSO.InsideBlockColor). So area-ratio dithering of the three domains reaches a
    /// wide interior of blues, teals, purples and ambers that no single prism owns.
    ///
    /// Two modes:
    ///   • PartitiveVolume — fill a shape with a blue-noise dither of the three domains whose ratio
    ///     hits a chosen target colour (barycentric weights, solved + gamut-clamped in LINEAR light).
    ///   • SplatSurface    — a sparse trefoil-knot point cloud that reads as a continuous glowing
    ///     surface (connect-the-dots → gaussian-splat), colour-swept along its length.
    ///
    /// COLLIDER BUDGET: every prism carries a trigger BoxCollider, so this is bounded by the same
    /// per-cell collider budget as any trail. Keep <see cref="count"/> modest (≤ a few thousand),
    /// prefer LayBatched (default) so the spawn never spikes a single frame, and treat large fields
    /// as you would any dense prismscape. Requires a ThemeManager + a URP Bloom volume in the scene
    /// (ChangeTeam indexes the theme material sets; the additive glow is what makes the mix read).
    /// </summary>
    public class PrismPerceptionField : MonoBehaviour
    {
        public enum FieldMode { PartitiveVolume, SplatSurface }
        public enum FieldShape { Sphere, Box, Disc }

        [Header("Prism")]
        [Tooltip("Assign _Prefabs/Trails/SpawnablePrism.prefab (the environment prism).")]
        [SerializeField] Prism prism;
        [Tooltip("Uniform edge length of each prism. Smaller = finer halftone = fuses at closer range.")]
        [SerializeField] float prismScale = 3f;

        [Header("Construct")]
        [SerializeField] FieldMode mode = FieldMode.PartitiveVolume;
        [SerializeField] FieldShape shape = FieldShape.Sphere;
        [Tooltip("How many prisms to lay. Watch the collider budget — this is a hard gate.")]
        [SerializeField, Range(64, 4000)] int count = 1400;
        [Tooltip("Radius / half-extent of the construct, in world units.")]
        [SerializeField] float radius = 40f;
        [Tooltip("Deterministic layout seed.")]
        [SerializeField] int seed = 1;

        [Header("Partitive target")]
        [Tooltip("The colour the dithered field should FUSE to at distance. Only its chromaticity is " +
                 "used; it is solved to domain area-ratios and gamut-clamped to the azure/violet/amber wedge.")]
        [SerializeField] Color targetColor = new Color(0.33f, 0.40f, 0.96f, 1f); // periwinkle: none of the three

        [Header("Build")]
        [SerializeField] bool buildOnStart = true;
        [Tooltip("Prisms laid per frame (batched) so the spawn never spikes a frame.")]
        [SerializeField] int perFrame = 60;

        // Real prism emissive primaries (OriginalColorSetSO.InsideBlockColor, linear HDR).
        // These, not the team colours, are what the blocks GLOW — a near-CMY triad.
        static readonly Vector3 P_JADE = new Vector3(0f, 0.588f, 1.135f); // azure-cyan  ≈ C
        static readonly Vector3 P_RUBY = new Vector3(0.549f, 0f, 1.498f); // violet      ≈ M
        static readonly Vector3 P_GOLD = new Vector3(1.498f, 0.668f, 0.089f); // amber    ≈ Y

        CancellationTokenSource _cts;

        void Start()
        {
            if (buildOnStart) Build();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        [ContextMenu("Build")]
        public void Build()
        {
            if (prism == null)
            {
                Debug.LogError($"{nameof(PrismPerceptionField)}: assign the SpawnablePrism prefab.", this);
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var elems = mode == FieldMode.SplatSurface
                ? BuildSplatSurface()
                : BuildPartitiveVolume();

            // Lay through the canonical builder: Instantiate → ChangeTeam → pose → Initialize → bloom-in.
            var trail = new Trail();
            PrismTrailBuilder
                .LayBatched(prism, elems, transform, trail, name, Mathf.Max(1, perFrame), _cts.Token)
                .Forget();
        }

        // ── PartitiveVolume ────────────────────────────────────────────────────
        // Fill a shape with domains dithered to the target colour's barycentric weights.
        List<PrismLay> BuildPartitiveVolume()
        {
            var rng = new System.Random(seed);
            Vector3 w = SolveDomainWeights(SrgbToLinear(targetColor)); // area fractions over {J,R,G}, gamut-clamped, sum=1
            float wJ = w.x, wJR = w.x + w.y;

            var quat = Quaternion.identity;
            var s = Vector3.one * prismScale;
            var list = new List<PrismLay>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = SamplePoint(shape, radius, rng);
                // stochastic (blue-noise-ish) assignment to the solved ratio
                double u = rng.NextDouble();
                Domains dom = u < wJ ? Domains.Jade : u < wJR ? Domains.Ruby : Domains.Gold;
                // face the prism outward-ish so its flat card catches the eye from many angles
                Quaternion rot = pos.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(pos.normalized, Vector3.up) : quat;
                list.Add(new PrismLay(new SpawnPoint(pos, rot, s), dom));
            }
            return list;
        }

        // ── SplatSurface ───────────────────────────────────────────────────────
        // A trefoil knot as a sparse cloud that fuses into a continuous glowing surface,
        // colour-swept Jade → Gold → Ruby along the parameter (connect-the-dots → splat).
        List<PrismLay> BuildSplatSurface()
        {
            var rng = new System.Random(seed);
            var s = Vector3.one * prismScale;
            var list = new List<PrismLay>(count);
            float tube = radius * 0.13f;
            for (int i = 0; i < count; i++)
            {
                double tt = rng.NextDouble();
                float t = (float)(tt * Mathf.PI * 2f);
                var p = new Vector3(
                    Mathf.Sin(t) + 2f * Mathf.Sin(2f * t),
                    Mathf.Cos(t) - 2f * Mathf.Cos(2f * t),
                    -Mathf.Sin(3f * t)) * (radius * 0.28f);
                // jitter within a tube so the surface has body
                p += Random3(rng) * tube;
                // colour sweep along the knot
                Domains dom = tt < 0.4 ? Domains.Jade : tt < 0.72 ? Domains.Gold : Domains.Ruby;
                Quaternion rot = p.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(p.normalized, Vector3.up) : Quaternion.identity;
                list.Add(new PrismLay(new SpawnPoint(p, rot, s), dom));
            }
            return list;
        }

        // ── shape sampling ─────────────────────────────────────────────────────
        static Vector3 SamplePoint(FieldShape shape, float r, System.Random rng)
        {
            switch (shape)
            {
                case FieldShape.Box:
                    return new Vector3(Rand(rng), Rand(rng), Rand(rng)) * r;
                case FieldShape.Disc:
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float rr = Mathf.Sqrt((float)rng.NextDouble()) * r;
                    return new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, ((float)rng.NextDouble() - 0.5f) * r * 0.12f);
                }
                default: // solid sphere (uniform)
                {
                    Vector3 v = Random3(rng);
                    while (v.sqrMagnitude > 1f) v = Random3(rng);
                    return v * r;
                }
            }
        }

        static float Rand(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
        static Vector3 Random3(System.Random rng) => new Vector3(Rand(rng), Rand(rng), Rand(rng));

        // ── colour → domain weights ────────────────────────────────────────────
        // Solve target ≈ wJ·Jade + wR·Ruby + wG·Gold with w ≥ 0, Σw = 1 (barycentric area fractions).
        // Coarse simplex search minimising linear-RGB error — inherently gamut-CLAMPS an out-of-wedge
        // target to the nearest reachable mix (never returns negative area fractions to the dither).
        static Vector3 SolveDomainWeights(Vector3 targetLinear)
        {
            const int N = 24;
            float best = float.MaxValue;
            Vector3 bestW = new Vector3(1f, 1f, 1f) / 3f;
            for (int a = 0; a <= N; a++)
            for (int b = 0; b <= N - a; b++)
            {
                int c = N - a - b;
                float wj = a / (float)N, wr = b / (float)N, wg = c / (float)N;
                Vector3 mix = wj * P_JADE + wr * P_RUBY + wg * P_GOLD;
                float e = (mix - targetLinear).sqrMagnitude;
                if (e < best) { best = e; bestW = new Vector3(wj, wr, wg); }
            }
            return bestW;
        }

        static Vector3 SrgbToLinear(Color c) => new Vector3(
            Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b));
    }
}
