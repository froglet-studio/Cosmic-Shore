using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.Tools.DensityPartitionBenchmark
{
    /// <summary>
    /// One prism in the synthetic input tape. The benchmark never spawns real Prism
    /// MonoBehaviours - Edit Mode can't drive the pool, the SOAP events, the AOE
    /// registry singleton, or PrismProperties initialization. Instead, each candidate
    /// algorithm receives a list of these structs and returns a Vector3 answer; the
    /// ground-truth scan operates on the same list, so the comparison is apples to
    /// apples on the algorithm itself, isolated from MonoBehaviour scaffolding.
    /// </summary>
    public struct BenchmarkPrism
    {
        public Vector3 Position;
        public Domains Domain;
        public float Volume;
    }

    /// <summary>Distribution shape for a scenario.</summary>
    public enum ScenarioKind
    {
        UniformRandom = 0,
        SingleCluster = 1,
        MultiCluster = 2,
        Gradient = 3,

        /// <summary>
        /// K clusters at deterministic positions, each cluster dominated by one
        /// domain (cluster i → {Jade, Ruby, Gold}[i % 3] with `segregatedPurity`
        /// fraction of prisms in that domain, the rest split among the others).
        ///
        /// Models a "territorial" game state where each team has carved out a
        /// region. This is the scenario where the §2.3.1 staleness bug should
        /// actually move the answer: phantom-Blue prisms at the Jade-dominant
        /// cluster's position pollute anti-Jade's view, potentially flipping
        /// the algorithm's pick from "Ruby/Gold cluster" to "polluted Jade
        /// cluster."
        /// </summary>
        DomainSegregatedClusters = 4,
    }

    /// <summary>
    /// Whether a scenario actually has a peak to find. Peaked scenarios are where
    /// peak-finding accuracy is meaningful - the summary headline ranks algorithms
    /// only on these. Diffuse scenarios (uniform random, density gradient) have no
    /// well-defined peak and serve as a diagnostic floor: every algorithm "fails"
    /// them, and that failure is not a regression. Keep them in the report for
    /// diagnostic value but exclude them from the headline median.
    /// </summary>
    public enum ScenarioShape
    {
        Peaked = 0,
        Diffuse = 1,
    }

    /// <summary>
    /// Deterministic scenario definition. Serializable so the runner inspector can
    /// edit a list. Build() expands into a list of BenchmarkPrism structs and is
    /// repeatable for a given seed - every algorithm in the report sees the same
    /// input tape.
    /// </summary>
    [Serializable]
    public class BenchmarkScenario
    {
        [Tooltip("Display name for this scenario in the report.")]
        public string label = "UniformRandom_2000";

        [Tooltip("Distribution shape.")]
        public ScenarioKind kind = ScenarioKind.UniformRandom;

        [Tooltip("Whether this scenario has a real peak to find. Peaked: " +
                 "algorithm accuracy is meaningful. Diffuse: no peak exists, " +
                 "every algorithm produces noisy answers and the result is a " +
                 "diagnostic floor only.")]
        public ScenarioShape shape = ScenarioShape.Peaked;

        [Tooltip("Total prism count produced by Build().")]
        [Min(10)] public int prismCount = 2000;

        [Tooltip("Deterministic RNG seed. Same seed → same input tape.")]
        public int seed = 42;

        [Tooltip("World-space half-extent for UniformRandom and the bounding box for other shapes.")]
        [Min(50f)] public float worldHalfExtent = 500f;

        [Header("SingleCluster / MultiCluster")]
        [Tooltip("Center for SingleCluster.")]
        public Vector3 clusterCenter = new Vector3(150f, 80f, -120f);

        [Tooltip("Gaussian sigma for SingleCluster / MultiCluster.")]
        [Min(10f)] public float clusterRadius = 100f;

        [Tooltip("MultiCluster: number of Gaussian clusters.")]
        [Range(2, 8)] public int clusterCount = 3;

        [Header("Gradient")]
        [Tooltip("Axis along which density increases.")]
        public Vector3 gradientAxis = Vector3.right;

        [Header("Domain mix (renormalized to sum 1)")]
        [Range(0f, 1f)] public float jadeFraction = 0.4f;
        [Range(0f, 1f)] public float rubyFraction = 0.4f;
        [Range(0f, 1f)] public float goldFraction = 0.2f;

        [Header("DomainSegregatedClusters")]
        [Tooltip("Fraction of each cluster's prisms that go to its dominant domain. " +
                 "The rest are split among the other two domains. 1.0 = fully pure, " +
                 "0.33 = same as MultiCluster (uniform mix).")]
        [Range(0.34f, 1f)] public float segregatedPurity = 0.9f;

        [Header("Volume distribution")]
        [Tooltip("Minimum prism volume. Volume is sampled uniformly in [min, max] per prism.")]
        [Min(0.1f)] public float volumeMin = 1f;
        [Tooltip("Maximum prism volume. Set min==max for uniform 1.0 (the current production grid's effective weighting).")]
        [Min(0.1f)] public float volumeMax = 1f;

        [Header("Staleness injection (models the §2.3.1 dynamic Add/Remove bug)")]
        [Tooltip("Fraction of prisms to *duplicate* as Blue-tagged phantoms at the " +
                 "same position. Models Cell.AddBlock running while block.Domain is " +
                 "still Blue (the pool default) so the prism is added to all three " +
                 "per-domain countGrids, then RemoveBlock running after ChangeTeam " +
                 "fails to remove from the prism's eventual domain's grid - a phantom " +
                 "is left at the prism's position in its own anti-domain bucket. " +
                 "Ground truth never sees the phantoms.")]
        [Range(0f, 1f)] public float staleFraction = 0f;

        /// <summary>
        /// Build the synthetic prism list. Always returns the same list for the same
        /// (seed, kind, prismCount, distribution params) - used as the canonical
        /// input tape for every algorithm in this scenario.
        /// </summary>
        public List<BenchmarkPrism> Build()
        {
            var prisms = new List<BenchmarkPrism>(prismCount);
            var rng = new System.Random(seed);

            float jf = Mathf.Max(0f, jadeFraction);
            float rf = Mathf.Max(0f, rubyFraction);
            float gf = Mathf.Max(0f, goldFraction);
            float sum = jf + rf + gf;
            if (sum <= 0f) { jf = rf = gf = 1f; sum = 3f; }
            float jadeT = jf / sum;
            float rubyT = (jf + rf) / sum;

            float vmin = Mathf.Max(0.1f, volumeMin);
            float vmax = Mathf.Max(vmin, volumeMax);

            Vector3[] multiCenters = null;
            Domains[] dominantDomainPerCluster = null;
            if (kind == ScenarioKind.MultiCluster || kind == ScenarioKind.DomainSegregatedClusters)
            {
                int k = Mathf.Clamp(clusterCount, 2, 8);
                multiCenters = new Vector3[k];
                dominantDomainPerCluster = new Domains[k];
                Domains[] domainCycle = { Domains.Jade, Domains.Ruby, Domains.Gold };
                for (int i = 0; i < k; i++)
                {
                    multiCenters[i] = new Vector3(
                        (float)(rng.NextDouble() * 2 - 1) * worldHalfExtent * 0.6f,
                        (float)(rng.NextDouble() * 2 - 1) * worldHalfExtent * 0.6f,
                        (float)(rng.NextDouble() * 2 - 1) * worldHalfExtent * 0.6f);
                    dominantDomainPerCluster[i] = domainCycle[i % 3];
                }
            }

            Vector3 axis = gradientAxis.sqrMagnitude > 0.0001f ? gradientAxis.normalized : Vector3.right;

            for (int i = 0; i < prismCount; i++)
            {
                int chosenCluster;
                Vector3 pos = SamplePosition(rng, kind, axis, multiCenters, out chosenCluster);

                Domains d;
                if (kind == ScenarioKind.DomainSegregatedClusters && chosenCluster >= 0)
                {
                    d = SampleDomainForSegregatedCluster(rng,
                        dominantDomainPerCluster[chosenCluster],
                        Mathf.Clamp(segregatedPurity, 0.34f, 1f));
                }
                else
                {
                    float t = (float)rng.NextDouble();
                    d = t < jadeT ? Domains.Jade : t < rubyT ? Domains.Ruby : Domains.Gold;
                }

                float v = Mathf.Lerp(vmin, vmax, (float)rng.NextDouble());

                prisms.Add(new BenchmarkPrism { Position = pos, Domain = d, Volume = v });
            }

            return prisms;
        }

        /// <summary>
        /// Pick a domain for a prism in a segregated cluster: `purity` chance of
        /// the dominant domain, remainder split evenly between the other two.
        /// </summary>
        static Domains SampleDomainForSegregatedCluster(System.Random rng, Domains dominant, float purity)
        {
            float r = (float)rng.NextDouble();
            if (r < purity) return dominant;
            // Remaining (1 - purity) split evenly between the two non-dominant domains.
            float t = (r - purity) / (1f - purity);
            switch (dominant)
            {
                case Domains.Jade: return t < 0.5f ? Domains.Ruby : Domains.Gold;
                case Domains.Ruby: return t < 0.5f ? Domains.Jade : Domains.Gold;
                case Domains.Gold: return t < 0.5f ? Domains.Jade : Domains.Ruby;
                default: return Domains.Jade;
            }
        }

        Vector3 SamplePosition(System.Random rng, ScenarioKind shape, Vector3 axis, Vector3[] centers, out int chosenCluster)
        {
            chosenCluster = -1;
            switch (shape)
            {
                case ScenarioKind.SingleCluster:
                    chosenCluster = 0;
                    return clusterCenter + GaussianVector(rng) * clusterRadius;

                case ScenarioKind.MultiCluster:
                case ScenarioKind.DomainSegregatedClusters:
                {
                    int k = centers != null ? centers.Length : 3;
                    int pick = rng.Next(k);
                    chosenCluster = pick;
                    return centers[pick] + GaussianVector(rng) * clusterRadius;
                }

                case ScenarioKind.Gradient:
                {
                    // Density linear in the projection of position onto axis.
                    // Inverse-CDF sampling: u² gives a triangular density.
                    float u = (float)rng.NextDouble();
                    float along = Mathf.Sqrt(u) * worldHalfExtent;
                    // Orthogonal axes: pick any two perpendicular to axis.
                    Vector3 ortho1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                    Vector3 ortho2 = Vector3.Cross(axis, ortho1).normalized;
                    float o1 = ((float)rng.NextDouble() * 2 - 1) * worldHalfExtent;
                    float o2 = ((float)rng.NextDouble() * 2 - 1) * worldHalfExtent;
                    return axis * along + ortho1 * o1 + ortho2 * o2;
                }

                case ScenarioKind.UniformRandom:
                default:
                    return new Vector3(
                        ((float)rng.NextDouble() * 2 - 1) * worldHalfExtent,
                        ((float)rng.NextDouble() * 2 - 1) * worldHalfExtent,
                        ((float)rng.NextDouble() * 2 - 1) * worldHalfExtent);
            }
        }

        static Vector3 GaussianVector(System.Random rng)
        {
            // Box-Muller (paired samples, second is discarded - we generate three
            // pairs across x/y/z for vector samples; per-axis bias is fine because
            // each call is independent and the report aggregates across hundreds).
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double mag = Math.Sqrt(-2.0 * Math.Log(u1));
            double phi = 2.0 * Math.PI * u2;
            float x = (float)(mag * Math.Cos(phi));
            float y = (float)(mag * Math.Sin(phi));
            double u3 = 1.0 - rng.NextDouble();
            double u4 = 1.0 - rng.NextDouble();
            float z = (float)(Math.Sqrt(-2.0 * Math.Log(u3)) * Math.Cos(2.0 * Math.PI * u4));
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Apply staleness to the input tape sent to an algorithm under test.
        ///
        /// Models the §2.3.1 dynamic-cycle failure: HealthBlockTracker.Add runs
        /// `cell.AddBlock(hp)` while `hp.Domain` is still Blue (the pool default),
        /// so Cell.AddBlock's `if (t != block.Domain)` loop increments ALL THREE
        /// per-domain countGrids. Later, `hp.ChangeTeam(domain)` sets the real
        /// domain. If the prism dies after that, RemoveBlock's same-shaped loop
        /// only decrements two of the three buckets - the bucket matching the
        /// prism's eventual domain is left with a phantom entry forever.
        ///
        /// Implementation: append `staleFraction × N` phantom prisms at the same
        /// positions as randomly-chosen real prisms, tagged Blue so they pass
        /// every anti-D filter (Blue ∉ {Jade, Ruby, Gold}). Each phantom adds one
        /// "phantom enemy" near a real prism - the cumulative effect of the bug
        /// over a long-running ecosystem.
        ///
        /// Ground truth never sees the phantoms - it scores the clean truth tape.
        /// </summary>
        public List<BenchmarkPrism> ApplyStaleness(List<BenchmarkPrism> truth)
        {
            if (staleFraction <= 0f) return truth;
            int phantomCount = Mathf.RoundToInt(truth.Count * staleFraction);
            if (phantomCount <= 0) return truth;

            var corrupted = new List<BenchmarkPrism>(truth.Count + phantomCount);
            corrupted.AddRange(truth);

            var rng = new System.Random(seed ^ 0x50_48_4E_54); // 'PHNT'
            for (int i = 0; i < phantomCount; i++)
            {
                int srcIdx = rng.Next(truth.Count);
                var src = truth[srcIdx];
                corrupted.Add(new BenchmarkPrism
                {
                    Position = src.Position,
                    Domain = Domains.Blue, // phantoms pass every anti-D filter
                    Volume = src.Volume,
                });
            }
            return corrupted;
        }
    }
}
