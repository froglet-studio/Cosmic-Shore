using CosmicShore.Gameplay;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns prisms along the Schwarz P minimal surface - the simplest triply periodic
    /// minimal surface (TPMS).
    ///
    /// The surface is approximated by the zero-level-set of:
    ///   f(x,y,z) = cos(x) + cos(y) + cos(z) = 0
    ///
    /// This creates a network of interconnected tunnels running in all three coordinate
    /// directions, with cubic symmetry. Each tunnel opens into six neighbors, forming
    /// a labyrinth that rewards exploration. The structure tiles infinitely - we sample
    /// a finite number of periods.
    ///
    /// Schwarz discovered this surface in 1865; it appears naturally in block copolymers,
    /// butterfly wing scales, and lipid membranes.
    /// </summary>
    public class SpawnableSchwarzPSurface : SpawnableBase
    {
        [Header("Block Settings")]
        [SerializeField] Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(2f, 2f, 2f);

        /// <summary>Laying slice per frame before the load gate takes over (it raises the slice
        /// while the connecting screen is covered — see PrismTrailBuilder.EffectiveLayBudget).</summary>
        const float LayBudgetMsPerFrame = 100f;

        [Header("Surface Sampling")]
        [Tooltip("Number of full periods of the surface in each direction.")]
        [SerializeField] int periods = 3;

        [Tooltip("Sample points per period per axis. Higher = denser, smoother surface.")]
        [SerializeField] int samplesPerPeriod = 10;

        [Tooltip("How close to zero the implicit function must be to place a block. " +
                 "Lower = thinner shell, higher = thicker walls.")]
        [SerializeField] float surfaceThreshold = 0.4f;

        [Tooltip("World-space size of one period. Controls overall scale.")]
        [SerializeField] float periodScale = 30f;

        [Header("Visual")]
        [Tooltip("Alternate domains based on which side of the surface a block sits near.")]
        [SerializeField] bool colorBySide = true;
        [SerializeField] Domains positiveDomain = Domains.Blue;
        [SerializeField] Domains negativeDomain = Domains.Jade;

        struct SurfaceNode
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Domains BlockDomain;
        }

        // Cache nodes for per-block domain assignment in SpawnLeafObjects
        private List<SurfaceNode> _cachedNodes;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var nodes = ComputeSurfacePositions();
            var points = new SpawnPoint[nodes.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                points[i] = new SpawnPoint(node.Position, node.Rotation, blockScale);
            }

            // Store node data for SpawnLeafObjects to use for per-block domain coloring
            _cachedNodes = nodes;

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        List<SurfaceNode> ComputeSurfacePositions()
        {
            var result = new List<SurfaceNode>();

            int totalSamples = periods * samplesPerPeriod;
            float halfExtent = periods * Mathf.PI; // f uses raw radians; one period = 2π

            for (int ix = 0; ix < totalSamples; ix++)
            {
                for (int iy = 0; iy < totalSamples; iy++)
                {
                    for (int iz = 0; iz < totalSamples; iz++)
                    {
                        // Map grid index to [0, periods*2π)
                        float u = 2f * Mathf.PI * ix / samplesPerPeriod;
                        float v = 2f * Mathf.PI * iy / samplesPerPeriod;
                        float w = 2f * Mathf.PI * iz / samplesPerPeriod;

                        float f = Mathf.Cos(u) + Mathf.Cos(v) + Mathf.Cos(w);

                        if (Mathf.Abs(f) > surfaceThreshold) continue;

                        // Map to world coordinates centered at origin
                        float worldScale = periodScale / (2f * Mathf.PI);
                        Vector3 position = new Vector3(
                            (u - halfExtent) * worldScale,
                            (v - halfExtent) * worldScale,
                            (w - halfExtent) * worldScale
                        );

                        // Gradient of f gives surface normal - use it for block orientation
                        Vector3 gradient = new Vector3(
                            -Mathf.Sin(u),
                            -Mathf.Sin(v),
                            -Mathf.Sin(w)
                        );

                        Vector3 lookTarget = gradient.sqrMagnitude > 0.001f
                            ? position + gradient.normalized
                            : position + Vector3.forward;

                        // CreateBlock used flip=true (default), so forward = position - lookTarget
                        var rotation = SpawnPoint.LookRotation(lookTarget, position, Vector3.up);

                        Domains blockDomain = colorBySide
                            ? (f >= 0 ? positiveDomain : negativeDomain)
                            : domain;

                        result.Add(new SurfaceNode
                        {
                            Position = position,
                            Rotation = rotation,
                            BlockDomain = blockDomain
                        });
                    }
                }
            }

            return result;
        }

        protected override void SpawnLeafObjects(SpawnTrailData[] trailData, GameObject container)
        {
            if (prism == null || _cachedNodes == null) return;

            // A minimal surface is a 2D prismscape - a SHELL, not a ribbon. The Trail here is
            // the general lay container, and the layer declares the shape it laid so topology
            // consumers (the Urchin's ride routing) roll ACROSS it rather than sliding along
            // the lay order.
            var trail = new Trail { Dimension = PrismscapeDimension.Surface };
            var nodes = _cachedNodes;

            // Per-node domain, so this routes through the PrismLay overload of the shared
            // builder rather than the single-domain one. Building the list is cheap; the cost
            // is the cloning, which the builder batches across worker threads.
            var elems = new PrismLay[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                elems[i] = new PrismLay(
                    new SpawnPoint(node.Position, node.Rotation, blockScale), node.BlockDomain);
            }

            // Streamed + batched at play time (this surface is Joust intensity 3 — the heaviest
            // structure in the game; laying it in one frame froze the load for ~15s). The
            // arena-ready gate holds the connecting screen until every prism is revealed and
            // grown, so streaming can never leak into play. Edit-mode spawns stay synchronous.
            if (Application.isPlaying)
                PrismTrailBuilder.LayBudgetedAsync(prism, elems, container.transform, trail,
                    $"{container.name}::SURFACE", LayBudgetMsPerFrame).Forget();
            else
                PrismTrailBuilder.LaySync(prism, elems, container.transform, trail, $"{container.name}::SURFACE");

            trails.Add(trail);
        }

        protected override int GetParameterHash()
        {
            return System.HashCode.Combine(periods, samplesPerPeriod, surfaceThreshold, periodScale,
                System.HashCode.Combine(colorBySide, positiveDomain, negativeDomain, blockScale, seed));
        }
    }
}
