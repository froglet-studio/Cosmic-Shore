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
            public Vector3 Scale;
            public Domains BlockDomain;
        }

        // Cache nodes for per-block domain assignment in SpawnLeafObjects
        private List<SurfaceNode> _cachedNodes;

        /// <summary>Blocks are sized to this fraction of their exact flush size, so float noise
        /// never turns two faces that touch into a clip.</summary>
        const float FlushMargin = 0.995f;

        /// <summary>The smallest axis the prism prefab lays (its PrismScaleAnimator window grows
        /// anything thinner back to this), so a flush block is never sized under it.</summary>
        const float MinPrismAxis = 0.5f;

        protected override SpawnTrailData[] GenerateTrailData()
        {
            var nodes = ComputeSurfacePositions();
            var points = new SpawnPoint[nodes.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                points[i] = new SpawnPoint(node.Position, node.Rotation, node.Scale);
            }

            // Store node data for SpawnLeafObjects to use for per-block domain coloring
            _cachedNodes = nodes;

            return new[] { new SpawnTrailData(points, false, domain) };
        }

        // Per grid index along an axis: the sample's angle, its cosine (f's term) and sine (the
        // gradient's), computed once per generation instead of three times per grid point.
        float[] _angle, _cos, _sin;
        int _samples;

        List<SurfaceNode> ComputeSurfacePositions()
        {
            var result = new List<SurfaceNode>();

            int totalSamples = periods * samplesPerPeriod;
            _samples = totalSamples;
            _angle = new float[totalSamples];
            _cos = new float[totalSamples];
            _sin = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
            {
                // Map grid index to [0, periods*2π)
                _angle[i] = 2f * Mathf.PI * i / samplesPerPeriod;
                _cos[i] = Mathf.Cos(_angle[i]);
                _sin[i] = Mathf.Sin(_angle[i]);
            }
            float spacing = periodScale / samplesPerPeriod;

            for (int ix = 0; ix < totalSamples; ix++)
            {
                for (int iy = 0; iy < totalSamples; iy++)
                {
                    for (int iz = 0; iz < totalSamples; iz++)
                    {
                        float f = _cos[ix] + _cos[iy] + _cos[iz];

                        if (Mathf.Abs(f) > surfaceThreshold) continue;

                        var (position, rotation) = NodePose(ix, iy, iz);

                        Domains blockDomain = colorBySide
                            ? (f >= 0 ? positiveDomain : negativeDomain)
                            : domain;

                        result.Add(new SurfaceNode
                        {
                            Position = position,
                            Rotation = rotation,
                            Scale = FlushScale(ix, iy, iz, rotation, spacing),
                            BlockDomain = blockDomain
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>A grid point's block pose: centred on the sample, its forward along the
        /// surface normal (the gradient of f).</summary>
        (Vector3 position, Quaternion rotation) NodePose(int ix, int iy, int iz)
        {
            float halfExtent = periods * Mathf.PI; // f uses raw radians; one period = 2π

            // Map to world coordinates centered at origin
            float worldScale = periodScale / (2f * Mathf.PI);
            Vector3 position = new Vector3(
                (_angle[ix] - halfExtent) * worldScale,
                (_angle[iy] - halfExtent) * worldScale,
                (_angle[iz] - halfExtent) * worldScale
            );

            // Gradient of f gives surface normal - use it for block orientation
            Vector3 gradient = new Vector3(-_sin[ix], -_sin[iy], -_sin[iz]);

            Vector3 lookTarget = gradient.sqrMagnitude > 0.001f
                ? position + gradient.normalized
                : position + Vector3.forward;

            // CreateBlock used flip=true (default), so forward = position - lookTarget
            return (position, SpawnPoint.LookRotation(lookTarget, position, Vector3.up));
        }

        /// <summary>
        /// The block's scale, at most blockScale, at which it is FLUSH with the blocks of its six
        /// grid neighbours (Docs/ECOSYSTEM.md §49.10). Blocks sit on a cubic grid of the given
        /// spacing, each turned to the surface normal; where the shell is thick enough to hold two
        /// grid points along a normal, a block long on that normal ran through the next one -
        /// the shipped SpawnableSchwarzP (12 long on its normal, spacing 10) clipped in 288 pairs
        /// and the SchwarzP cubes of SpawnableLinkedRings (2 on a side, spacing 3) in 864, every
        /// one of them face neighbours. Only a face neighbour can reach: one an edge or a corner
        /// away is at least spacing x sqrt 2 off, further than two blocks reach while a block's
        /// diagonal is under that. For each face neighbour in the shell that is not already parted
        /// on the grid axis, both blocks shrink, by the common factor that makes them flush (closed
        /// form, SharedFlushFactor), the one axis that reaches furthest along the grid axis - for
        /// the long blocks their length on the normal. The pair is computed alike from either side,
        /// so the two agree without either knowing the other's result. The block's centre and
        /// rotation never move.
        /// </summary>
        Vector3 FlushScale(int ix, int iy, int iz, Quaternion rotation, float spacing)
        {
            var frame = new Frame(rotation);
            Vector3 half = 0.5f * new Vector3(Mathf.Abs(blockScale.x), Mathf.Abs(blockScale.y), Mathf.Abs(blockScale.z));
            Vector3 factor = Vector3.one;
            for (int n = 0; n < 6; n++)
            {
                int axis = n >> 1, step = (n & 1) == 0 ? 1 : -1;
                int jx = ix + (axis == 0 ? step : 0), jy = iy + (axis == 1 ? step : 0), jz = iz + (axis == 2 ? step : 0);
                if (jx < 0 || jy < 0 || jz < 0 || jx >= _samples || jy >= _samples || jz >= _samples) continue;
                if (Mathf.Abs(_cos[jx] + _cos[jy] + _cos[jz]) > surfaceThreshold) continue;   // no block there

                // Parted on the grid axis already (each reaches less than half way): nothing to do.
                if (frame.Reach(axis, half) * 2f <= spacing) continue;
                var (_, otherRotation) = NodePose(jx, jy, jz);
                var other = new Frame(otherRotation);
                if (frame.Reach(axis, half) + other.Reach(axis, half) <= spacing) continue;

                Vector3 d = Vector3.zero;
                d[axis] = step * spacing;
                int mine = frame.Furthest(axis, half), theirs = other.Furthest(axis, half);
                float k = SharedFlushFactor(d, frame, other, half, mine, theirs);
                factor[mine] = Mathf.Min(factor[mine], k);
            }

            var scale = blockScale;
            for (int i = 0; i < 3; i++)
                if (factor[i] < 1f)
                    scale[i] = Mathf.Sign(blockScale[i]) * Mathf.Max(Mathf.Abs(blockScale[i]) * factor[i], Mathf.Min(MinPrismAxis, Mathf.Abs(blockScale[i])));
            return scale;
        }

        /// <summary>A block's three axes.</summary>
        readonly struct Frame
        {
            readonly Vector3 _x, _y, _z;
            public Frame(Quaternion r) { _x = r * Vector3.right; _y = r * Vector3.up; _z = r * Vector3.forward; }
            public Vector3 this[int i] => i == 0 ? _x : i == 1 ? _y : _z;

            /// <summary>How far the block reaches along a world axis.</summary>
            public float Reach(int worldAxis, Vector3 half) =>
                Mathf.Abs(_x[worldAxis]) * half.x + Mathf.Abs(_y[worldAxis]) * half.y + Mathf.Abs(_z[worldAxis]) * half.z;

            /// <summary>The block's own axis that reaches furthest along a world axis.</summary>
            public int Furthest(int worldAxis, Vector3 half)
            {
                float rx = Mathf.Abs(_x[worldAxis]) * half.x, ry = Mathf.Abs(_y[worldAxis]) * half.y, rz = Mathf.Abs(_z[worldAxis]) * half.z;
                return rx >= ry && rx >= rz ? 0 : ry >= rz ? 1 : 2;
            }
        }

        /// <summary>
        /// The largest common factor k in (0, 1] on axis ia of box A and axis ib of box B (both of
        /// half extents half) at which the two are flush - closed form, no search. On each of the
        /// 15 separating axes L the clearance is linear in k, gap_L - k * reach_L, so L separates
        /// for k up to gap_L / reach_L, and the pair is flush for the best of the 15.
        /// </summary>
        static float SharedFlushFactor(Vector3 d, in Frame a, in Frame b, Vector3 half, int ia, int ib)
        {
            float best = 0f;
            for (int i = 0; i < 15; i++)
            {
                Vector3 l = i < 3 ? a[i] : i < 6 ? b[i - 3] : Vector3.Cross(a[(i - 6) / 3], b[(i - 6) % 3]);
                if (l.sqrMagnitude < 1e-10f) continue;
                float gap = Mathf.Abs(Vector3.Dot(d, l)), reach = 0f;
                for (int j = 0; j < 3; j++)
                {
                    float ra = half[j] * Mathf.Abs(Vector3.Dot(a[j], l)), rb = half[j] * Mathf.Abs(Vector3.Dot(b[j], l));
                    if (j == ia) reach += ra; else gap -= ra;
                    if (j == ib) reach += rb; else gap -= rb;
                }
                if (gap < 0f) continue;
                if (reach <= gap) return 1f;
                best = Mathf.Max(best, gap / reach);
            }
            return Mathf.Min(1f, best * FlushMargin);
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
                    new SpawnPoint(node.Position, node.Rotation, node.Scale), node.BlockDomain);
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
