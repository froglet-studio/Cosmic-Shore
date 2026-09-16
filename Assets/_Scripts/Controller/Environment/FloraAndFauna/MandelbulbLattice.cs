using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The <b>Mandelbulb's own membership test</b>, addressed on an integer lattice - everything
    /// <see cref="MandelbulbFlora"/> needs to know about the shape it grows on, and nothing about
    /// plants, cells or prisms.
    ///
    /// <para><b>Why the AMBIENT cubic lattice rather than an intrinsic tile.</b> Docs/ECOSYSTEM.md
    /// §34 establishes the rule for the lattice flora: a species grows on its surface's OWN tile,
    /// never on a fitted grid, because a marching walk across a curved surface accumulates drift,
    /// fronts arriving from different directions disagree, and the occupancy key degrades to a
    /// quantized float. That rule is about surfaces that HAVE an exact tiling - a triply periodic
    /// minimal surface does, and the gyroid and Schwarz P species use theirs. <b>A fractal
    /// boundary has none</b>: the Mandelbulb is not periodic, not quasiperiodic, has no repeat
    /// unit and is not a smooth manifold, so there is no intrinsic tile to find and inventing one
    /// would be exactly the fitted grid §34 forbids.
    ///
    /// So this species addresses in the AMBIENT integer lattice instead - the same
    /// <c>Vector3Int</c> bookkeeping <c>SchwarzPTileData</c> uses, one level out. That keeps the
    /// property §34 actually cares about: <b>sameness is an integer address</b>. A site either is
    /// or is not <see cref="IsShellSite"/>, decided by a pure function of three integers, so
    /// occupancy is exact, there is no tolerance to drift, two growth fronts meeting from
    /// opposite sides agree by construction, and nothing has to be baked - membership is the
    /// closed form, evaluated on demand.</para>
    ///
    /// <para><b>Doubles, deliberately.</b> Membership is a BOOLEAN over a discrete set, so a site
    /// whose centre sits a hair either side of the boundary can flip between float and double
    /// arithmetic. The offline model (<c>Tools/Build/measure_mandelbulb_flora.py</c>) is the
    /// authority for this species' prism counts and volume ladder, and
    /// <c>verify_mandelbulb_flora_tables.py</c> proves this file against it site for site - which
    /// is only a meaningful proof if both sides compute the same numbers. The cost is nothing:
    /// a plant evaluates a few thousand sites over its whole life, cached.</para>
    ///
    /// <para><b>The cache is static and that is safe here</b>, which is worth stating because a
    /// static registry surviving a cell teardown is a live hazard in this system (the ecology
    /// skill's "a static registry/claim book/frontier that coordinates a population survives every
    /// cell teardown"). This is not a claim book and holds no world state: it memoises a pure
    /// mathematical function of (site, power, pitch, iterations, bailout). A stale entry cannot
    /// exist, because the answer never depended on anything that could go stale. It is bounded
    /// and cleared wholesale rather than evicted, since rebuilding an entry costs microseconds.</para>
    /// </summary>
    public sealed class MandelbulbLattice
    {
        /// <summary>The six FACE neighbours - the exposure test (a site is on the shell iff one
        /// of these is outside). Face rather than 26 on purpose: a site touching the exterior only
        /// at a corner is not a face of the surface, and counting it thickens the shell.</summary>
        public static readonly Vector3Int[] Face6 =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        /// <summary>The 26 neighbours - the GROWTH adjacency, and the exposed-face census the
        /// surface normal is built from. Growth uses 26 because a voxelised fractal shell is
        /// 26-connected but not always 6-connected: a 6-connected walk strands patches the plant
        /// can see but never reach. Measured, not assumed - it is a negative control in
        /// <c>Tools/Build/verify_mandelbulb_flora_tables.py --self-test</c>, where reducing this
        /// to <see cref="Face6"/> reaches only <b>414 of the 589</b> sites of the shipped
        /// Charge form.</summary>
        public static readonly Vector3Int[] Neighbour26 = BuildNeighbour26();

        static Vector3Int[] BuildNeighbour26()
        {
            var list = new List<Vector3Int>(26);
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                        if (x != 0 || y != 0 || z != 0)
                            list.Add(new Vector3Int(x, y, z));
            return list.ToArray();
        }

        public readonly int Power;
        public readonly double Pitch;
        public readonly int Iterations;
        public readonly double Bailout;

        readonly Dictionary<Vector3Int, bool> _inside = new();

        MandelbulbLattice(int power, double pitch, int iterations, double bailout)
        {
            Power = power;
            Pitch = pitch;
            Iterations = iterations;
            Bailout = bailout;
        }

        // ── Shared instances ──────────────────────────────────────────────────

        readonly struct ShapeKey : IEquatable<ShapeKey>
        {
            readonly int _power, _iterations;
            readonly double _pitch, _bailout;
            public ShapeKey(int power, double pitch, int iterations, double bailout)
            { _power = power; _pitch = pitch; _iterations = iterations; _bailout = bailout; }
            public bool Equals(ShapeKey o) => _power == o._power && _iterations == o._iterations
                                              && _pitch.Equals(o._pitch) && _bailout.Equals(o._bailout);
            public override bool Equals(object o) => o is ShapeKey k && Equals(k);
            public override int GetHashCode() =>
                (_power * 397) ^ (_iterations * 17) ^ _pitch.GetHashCode() ^ _bailout.GetHashCode();
        }

        static readonly Dictionary<ShapeKey, MandelbulbLattice> Shapes = new();

        /// <summary>Total cached membership answers across every live shape, past which the cache
        /// is dropped wholesale. Generous: one plant's whole life is a few thousand sites, and an
        /// entry costs microseconds to rebuild.</summary>
        const int MaxCachedSites = 400_000;

        /// <summary>
        /// The shared lattice for a shape. Every plant of the same element shares one, because
        /// the lattice lives in the plant's LOCAL space and is therefore identical for all of them
        /// - the plant's own transform is what puts its bulb somewhere in the world.
        /// </summary>
        public static MandelbulbLattice For(int power, float pitch, int iterations, float bailout)
        {
            var key = new ShapeKey(power, pitch, iterations, bailout);
            if (Shapes.TryGetValue(key, out var found)) return found;

            int cached = 0;
            foreach (var shape in Shapes.Values) cached += shape._inside.Count;
            if (cached > MaxCachedSites) Shapes.Clear();

            var made = new MandelbulbLattice(power, pitch, iterations, bailout);
            Shapes[key] = made;
            return made;
        }

        // ── Membership ────────────────────────────────────────────────────────

        /// <summary>
        /// True when this site's centre is INSIDE the Mandelbulb set: the orbit of
        /// <c>v -> v^power + c</c> from <c>v = 0</c>, with <c>c</c> the site's own position, has
        /// not escaped the bailout radius within <see cref="Iterations"/> steps. The triplex power
        /// is the standard White/Nylander form - <c>(r, theta, phi)^n = (r^n, n*theta, n*phi)</c>.
        ///
        /// <para>Nothing here describes a bulb. The shape is what this iteration happens to
        /// leave behind, which is the same claim the gyroid colony makes about its own local
        /// continuation rule (Docs/ECOSYSTEM.md §32.7).</para>
        /// </summary>
        public bool Inside(Vector3Int site)
        {
            if (_inside.TryGetValue(site, out bool known)) return known;

            double px = site.x * Pitch, py = site.y * Pitch, pz = site.z * Pitch;
            double x = 0.0, y = 0.0, z = 0.0;
            bool inside = true;

            for (int i = 0; i < Iterations; i++)
            {
                double r = Math.Sqrt(x * x + y * y + z * z);
                if (r > Bailout) { inside = false; break; }
                if (r < 1e-12) { x = px; y = py; z = pz; continue; }

                double theta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, z / r)));
                double phi = Math.Atan2(y, x);
                double rn = Math.Pow(r, Power);
                double st = Math.Sin(Power * theta);

                x = rn * st * Math.Cos(Power * phi) + px;
                y = rn * st * Math.Sin(Power * phi) + py;
                z = rn * Math.Cos(Power * theta) + pz;
            }

            _inside[site] = inside;
            return inside;
        }

        /// <summary>
        /// True when this site is part of the SURFACE a plant grows over: inside the set, with at
        /// least one FACE neighbour outside it.
        ///
        /// <para><b>Purely local - no flood fill, and that is a measurement rather than a
        /// simplification.</b> The strictly correct "visible surface" excludes the walls of sealed
        /// internal cavities, which needs a global flood from outside and is therefore something a
        /// growing plant could not evaluate for one site. Measured over the shipped shape, the
        /// filter removes <b>0 of 589 sites at the shipped pitch</b>, and 6 of 1602 at a pitch
        /// nearly twice as fine - the Mandelbulb has essentially no sealed voids at prism scale -
        /// so growth uses the local test, and any cavity wall that does survive is an interior
        /// prism nobody can see. The measurement is re-run by
        /// <c>Tools/Build/measure_mandelbulb_flora.py --cavities</c>; if a future power or pitch
        /// makes it large, this is the decision to revisit.</para>
        /// </summary>
        public bool IsShellSite(Vector3Int site)
        {
            if (!Inside(site)) return false;
            foreach (var d in Face6)
                if (!Inside(site + d)) return true;
            return false;
        }

        /// <summary>
        /// The outward surface normal at a shell site, as the sum of unit directions to its EMPTY
        /// neighbours over the 26-neighbourhood.
        ///
        /// <para><b>Why not the distance estimator.</b> §34's rule for the smooth lattice species
        /// is "never bake a rotation - derive orientation from the closed-form gradient". That
        /// rule inverts here, and it was measured rather than assumed: the Mandelbulb's analytic
        /// distance estimator (<c>0.5 * log(r) * r / dr</c>) has a gradient that is <b>noisy at
        /// voxel scale on a fractal boundary</b> - neighbouring sites get wildly different
        /// directions, and prisms oriented by it render as confetti. The exposed-face census is
        /// derived from the same exact integer occupancy the address is, so it is stable, cheap,
        /// consistent between neighbours, and it is the honest normal for what is actually being
        /// drawn: a face of a voxel shell. Both renders are in the measure script.</para>
        ///
        /// <para>Falls back to the radial direction at a site with no empty neighbour at all -
        /// unreachable for a shell site by definition, kept so the function is total.</para>
        /// </summary>
        public Vector3 Normal(Vector3Int site)
        {
            Vector3 sum = Vector3.zero;
            foreach (var d in Neighbour26)
            {
                if (Inside(site + d)) continue;
                sum += ((Vector3)d).normalized;
            }

            if (sum.sqrMagnitude > 1e-9f) return sum.normalized;
            Vector3 radial = site;
            return radial.sqrMagnitude > 1e-9f ? radial.normalized : Vector3.up;
        }

        /// <summary>
        /// The shell site a plant starts from: march out from the origin along <c>-z</c> to the
        /// last site still inside the set. That site is inside and its own <c>-z</c> neighbour is
        /// not, so it IS a shell site by construction - no search, no failure case.
        ///
        /// <para>The origin is always inside (<c>c = 0</c> leaves the orbit at zero forever), so
        /// the march always has somewhere to start. <c>-z</c> because the plant orients its local
        /// <c>+z</c> along its growth axis, which puts the seed at the plant's footing and grows
        /// the bulb up over itself.</para>
        /// </summary>
        public Vector3Int SeedSite(int maxSiteRadius)
        {
            var last = Vector3Int.zero;
            for (int s = 1; s <= maxSiteRadius; s++)
            {
                var probe = new Vector3Int(0, 0, -s);
                if (!Inside(probe)) break;
                last = probe;
            }
            return last;
        }

        // ── Plating ───────────────────────────────────────────────────────────

        /// <summary>
        /// How the surface is cut into prisms. Every field is a shape rule, not a size: the
        /// world size of a plate is <see cref="MandelbulbFlora"/>'s business, and everything
        /// here is measured in LATTICE CELLS so the rule is scale-free.
        /// </summary>
        public readonly struct PlatingRules : IEquatable<PlatingRules>
        {
            /// <summary>A cell is plated iff <c>1 - |n . r|</c> reaches this - i.e. its surface
            /// faces SIDEWAYS relative to the radial. Those are the terrace risers and the crease
            /// walls, which is the whole of what a Mandelbulb's form actually is; plating the
            /// treads as well closes the plant into a skin and a skin reads as a ball.</summary>
            public readonly float RiserBias;
            /// <summary>Merge admits a neighbouring cell whose normal is within this cosine of
            /// the patch's running mean. It is what makes a flat riser ONE long plate and a
            /// twisting seam a scatter of chips.</summary>
            public readonly float CoplanarCos;
            /// <summary>Largest RMS deviation from the patch's own fitted plane, in cells. One
            /// plate cannot represent a patch that is not flat, whatever its normals say.</summary>
            public readonly float PlanarTau;
            /// <summary>The largest patch one plate may stand for. The only ceiling on plate size
            /// - everything below it is decided by the surface.</summary>
            public readonly int MaxCells;
            /// <summary>Plate size as a fraction of its patch's own measured span. Below 1 by
            /// design: the gaps it opens are what you see the inner structure through.</summary>
            public readonly float Pad;
            /// <summary>Plate thickness in CELLS, absolute rather than a fraction of the plate.
            /// A proportional thickness makes a long plate a slab, and a slab's volume lands on
            /// the cell's Frenzy ladder as the cube of its length.</summary>
            public readonly float Thickness;
            /// <summary>A candidate whose separating-axis scale against an already-laid plate
            /// falls below this is essentially inside it and is not laid at all - hidden mass
            /// buys nothing, and this is also the bound on how deeply any two plates interleave.
            /// Zero disables the drop.</summary>
            public readonly float ContainDrop;
            /// <summary>Bound on the walk, in cells.</summary>
            public readonly int MaxSiteRadius;

            public PlatingRules(float riserBias, float coplanarCos, float planarTau, int maxCells,
                                float pad, float thickness, float containDrop, int maxSiteRadius)
            {
                RiserBias = riserBias; CoplanarCos = coplanarCos; PlanarTau = planarTau;
                MaxCells = maxCells; Pad = pad; Thickness = thickness;
                ContainDrop = containDrop; MaxSiteRadius = maxSiteRadius;
            }

            public bool Equals(PlatingRules o) =>
                RiserBias.Equals(o.RiserBias) && CoplanarCos.Equals(o.CoplanarCos) &&
                PlanarTau.Equals(o.PlanarTau) && MaxCells == o.MaxCells && Pad.Equals(o.Pad) &&
                Thickness.Equals(o.Thickness) && ContainDrop.Equals(o.ContainDrop) &&
                MaxSiteRadius == o.MaxSiteRadius;
            public override bool Equals(object o) => o is PlatingRules r && Equals(r);
            public override int GetHashCode() =>
                RiserBias.GetHashCode() ^ (CoplanarCos.GetHashCode() * 3) ^
                (PlanarTau.GetHashCode() * 7) ^ (MaxCells * 31) ^ (Pad.GetHashCode() * 11) ^
                (Thickness.GetHashCode() * 13) ^ (ContainDrop.GetHashCode() * 17) ^
                (MaxSiteRadius * 61);
        }

        /// <summary>One prism: the patch of surface cells it stands for, reduced to an oriented
        /// box. Everything is in LATTICE CELLS and in the plant's local frame. The basis is
        /// handed over as three vectors rather than a rotation because half the frames on a
        /// surface are reflections and a baked quaternion carried through one is silently wrong
        /// (Docs/ECOSYSTEM.md §34) - the caller composes the rotation it needs.</summary>
        public readonly struct Plate
        {
            /// <summary>The patch's first cell in growth order - the plate's identity, and an
            /// exact integer one, which is what lets a verifier compare two platings cell for
            /// cell instead of position for position.</summary>
            public readonly Vector3Int Seed;
            public readonly Vector3 Centre;
            public readonly Vector3 Right, Up, Forward;
            public readonly Vector3 Size;
            public readonly int Cells;

            public Plate(Vector3Int seed, Vector3 centre, Vector3 right, Vector3 up,
                         Vector3 forward, Vector3 size, int cells)
            {
                Seed = seed; Centre = centre; Right = right; Up = up; Forward = forward;
                Size = size; Cells = cells;
            }
        }

        readonly Dictionary<PlatingRules, Plate[]> _plating = new();

        /// <summary>
        /// The plant, as a list of prisms in GROWTH ORDER. Pure, deterministic and shared by
        /// every plant of this element - the plating lives in the plant's local frame, so one
        /// answer serves all of them.
        ///
        /// <para>Built in one pass on first demand rather than incrementally, because merging is
        /// not a local decision: which plate a cell belongs to depends on the whole patch, so
        /// there is no prefix of the answer that can be computed without the walk. The pass is
        /// the walk plus a linear sweep; it is paid once per (element, rules) per session.</para>
        /// </summary>
        public IReadOnlyList<Plate> Plating(in PlatingRules rules)
        {
            if (_plating.TryGetValue(rules, out var made)) return made;
            made = BuildPlating(rules);
            _plating[rules] = made;
            return made;
        }

        Plate[] BuildPlating(in PlatingRules rules)
        {
            // 1. the reachable shell, in growth order - the same walk MandelbulbFlora spreads with
            int bound = rules.MaxSiteRadius;
            var seed = SeedSite(bound);
            var seen = new HashSet<Vector3Int> { seed };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(seed);
            var order = new List<Vector3Int>();
            while (queue.Count > 0)
            {
                var site = queue.Dequeue();
                order.Add(site);
                foreach (var d in Neighbour26)
                {
                    var next = site + d;
                    if (Math.Abs(next.x) > bound || Math.Abs(next.y) > bound ||
                        Math.Abs(next.z) > bound) continue;
                    if (!seen.Add(next)) continue;
                    if (IsShellSite(next)) queue.Enqueue(next);
                }
            }

            // 2. selection: the risers, and only the risers
            var normal = new Dictionary<Vector3Int, Vector3>(order.Count);
            var selected = new HashSet<Vector3Int>();
            foreach (var site in order)
            {
                var n = Normal(site);
                normal[site] = n;
                double rx = site.x, ry = site.y, rz = site.z;
                double rm = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                if (rm < 1e-9) rm = 1.0;
                double radial = (rx * n.x + ry * n.y + rz * n.z) / rm;
                if (1.0 - Math.Abs(radial) >= rules.RiserBias) selected.Add(site);
            }

            // 3. merge into coplanar patches, one plate each, in growth order
            var claimed = new HashSet<Vector3Int>();
            var plates = new List<Plate>();
            var bucket = new Dictionary<Vector3Int, List<int>>();
            double bucketSize = Math.Max(4.0, rules.MaxCells);
            var patch = new List<Vector3Int>();
            var frontier = new Queue<Vector3Int>();

            foreach (var start in order)
            {
                if (!selected.Contains(start) || claimed.Contains(start)) continue;

                patch.Clear();
                frontier.Clear();
                patch.Add(start);
                claimed.Add(start);
                frontier.Enqueue(start);
                var startNormal = normal[start];
                double mx = startNormal.x, my = startNormal.y, mz = startNormal.z;

                while (frontier.Count > 0 && patch.Count < rules.MaxCells)
                {
                    var cur = frontier.Dequeue();
                    foreach (var d in Neighbour26)
                    {
                        var next = cur + d;
                        if (!selected.Contains(next) || claimed.Contains(next)) continue;
                        var nn = normal[next];
                        double mm = Math.Sqrt(mx * mx + my * my + mz * mz);
                        if (mm < 1e-12) mm = 1.0;
                        if ((mx * nn.x + my * nn.y + mz * nn.z) / mm < rules.CoplanarCos) continue;

                        patch.Add(next);
                        if (patch.Count >= 4 && PatchDeviation(patch) > rules.PlanarTau)
                        {
                            patch.RemoveAt(patch.Count - 1);
                            continue;
                        }
                        claimed.Add(next);
                        frontier.Enqueue(next);
                        mx += nn.x; my += nn.y; mz += nn.z;
                        if (patch.Count >= rules.MaxCells) break;
                    }
                }

                var plate = MakePlate(start, patch, startNormal, rules);

                if (rules.ContainDrop > 0f && IsHidden(plate, plates, bucket, bucketSize, rules.ContainDrop))
                    continue;   // the cells stay claimed: a hidden plate is a decision, not a retry

                int index = plates.Count;
                plates.Add(plate);
                var key = BucketKey(plate.Centre, bucketSize);
                if (!bucket.TryGetValue(key, out var list)) bucket[key] = list = new List<int>();
                list.Add(index);
            }

            // The membership memo has done its job and is by far the largest thing this class
            // holds - a build touches ~500k sites, well past MaxCachedSites, so keeping it would
            // make the NEXT element's For() clear every shape including this one's finished
            // plating. Nothing reads it again: a plant grows from the plate list.
            _inside.Clear();
            return plates.ToArray();
        }

        static Vector3Int BucketKey(Vector3 centre, double size) => new(
            (int)Math.Floor(centre.x / size), (int)Math.Floor(centre.y / size),
            (int)Math.Floor(centre.z / size));

        /// <summary>RMS distance of a patch's cells from their own best-fit plane, in cells.</summary>
        static double PatchDeviation(List<Vector3Int> patch)
        {
            Fit(patch, out _, out var values, out _);
            return Math.Sqrt(Math.Max(0.0, values[2]));
        }

        /// <summary>
        /// The patch's principal frame: the centroid, the covariance eigenvalues in descending
        /// order, and their eigenvectors. The smallest eigenvector is the surface normal and the
        /// two larger ones are the plate's in-plane axes, so a plate's ASPECT is a measurement of
        /// the patch rather than an authored number - an elongated riser becomes a strut and a
        /// broad one a plate, with nothing to tune.
        /// </summary>
        static void Fit(List<Vector3Int> patch, out double[] centroid, out double[] values,
                        out double[][] vectors)
        {
            int n = patch.Count;
            centroid = new double[3];
            foreach (var p in patch)
            {
                centroid[0] += p.x; centroid[1] += p.y; centroid[2] += p.z;
            }
            for (int i = 0; i < 3; i++) centroid[i] /= n;

            var m = new double[3][];
            for (int i = 0; i < 3; i++) m[i] = new double[3];
            foreach (var p in patch)
            {
                double vx = p.x - centroid[0], vy = p.y - centroid[1], vz = p.z - centroid[2];
                m[0][0] += vx * vx; m[0][1] += vx * vy; m[0][2] += vx * vz;
                m[1][0] += vy * vx; m[1][1] += vy * vy; m[1][2] += vy * vz;
                m[2][0] += vz * vx; m[2][1] += vz * vy; m[2][2] += vz * vz;
            }
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++) m[i][j] /= n;

            Jacobi3(m, out values, out vectors);
        }

        /// <summary>Symmetric 3x3 eigen-decomposition by cyclic Jacobi rotations, descending.
        /// Written out rather than pulled from a library because this file has to compile and RUN
        /// outside Unity - that is how it is proved (Tools/Build/mandelbulb_lattice_harness).</summary>
        static void Jacobi3(double[][] a, out double[] values, out double[][] vectors)
        {
            var A = new double[3][];
            for (int i = 0; i < 3; i++) { A[i] = new double[3]; Array.Copy(a[i], A[i], 3); }
            var V = new double[3][];
            for (int i = 0; i < 3; i++) { V[i] = new double[3]; V[i][i] = 1.0; }

            for (int sweep = 0; sweep < 60; sweep++)
            {
                double off = Math.Abs(A[0][1]) + Math.Abs(A[0][2]) + Math.Abs(A[1][2]);
                if (off < 1e-14) break;
                for (int pair = 0; pair < 3; pair++)
                {
                    int p = pair == 2 ? 1 : 0;
                    int q = pair == 0 ? 1 : 2;
                    if (Math.Abs(A[p][q]) < 1e-18) continue;
                    double theta = (A[q][q] - A[p][p]) / (2.0 * A[p][q]);
                    double t = (theta >= 0 ? 1.0 : -1.0) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                    double c = 1.0 / Math.Sqrt(t * t + 1.0);
                    double s = t * c;
                    for (int k = 0; k < 3; k++)
                    {
                        double akp = A[k][p], akq = A[k][q];
                        A[k][p] = c * akp - s * akq;
                        A[k][q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double apk = A[p][k], aqk = A[q][k];
                        A[p][k] = c * apk - s * aqk;
                        A[q][k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        double vkp = V[k][p], vkq = V[k][q];
                        V[k][p] = c * vkp - s * vkq;
                        V[k][q] = s * vkp + c * vkq;
                    }
                }
            }

            var raw = new[] { A[0][0], A[1][1], A[2][2] };
            // STABLE descending insertion sort. Stability matters: on a degenerate patch two
            // eigenvalues tie exactly, and an unstable order would hand back a different pair of
            // in-plane axes than the offline model does for the same patch.
            var index = new[] { 0, 1, 2 };
            for (int i = 1; i < 3; i++)
            {
                int key = index[i];
                int j = i - 1;
                while (j >= 0 && raw[index[j]] < raw[key]) { index[j + 1] = index[j]; j--; }
                index[j + 1] = key;
            }

            values = new double[3];
            vectors = new double[3][];
            for (int i = 0; i < 3; i++)
            {
                values[i] = raw[index[i]];
                vectors[i] = new[] { V[0][index[i]], V[1][index[i]], V[2][index[i]] };
            }
        }

        /// <summary>
        /// Smallest MINOR in-plane variance, in cells squared, at which a patch is treated as
        /// genuinely two-dimensional. Below it the patch is a point or a LINE, its covariance has
        /// rank under 2, and the two smallest eigenvectors are interchangeable - so the frame a
        /// principal-axis fit hands back is arbitrary and can flip on a rounding difference
        /// between two machines. A two-cell patch is the common case and there are always some.
        /// Below the bar the normal comes from the cell's own exposed-face census (which is always
        /// well defined) and the long axis from the patch's one real principal direction, which
        /// makes a row of cells an honest strut instead of a coin toss. 0.02 sits far under the
        /// 0.25 a two-cell-wide strip measures and far over the exact zero a line measures.
        /// </summary>
        const double PlanarRankTolerance = 0.02;

        static Plate MakePlate(Vector3Int seedCell, List<Vector3Int> patch, Vector3 seedNormal,
                               in PlatingRules rules)
        {
            double[] c;
            double[] e0, e1, e2;

            Fit(patch, out c, out var values, out var vecs);

            if (patch.Count >= 3 && values[1] > PlanarRankTolerance)
            {
                e0 = vecs[0]; e1 = vecs[1]; e2 = vecs[2];
                if (PointsInward(e2, seedNormal, c))
                    for (int i = 0; i < 3; i++) { e2[i] = -e2[i]; e0[i] = -e0[i]; }
            }
            else
            {
                e2 = new double[] { seedNormal.x, seedNormal.y, seedNormal.z };
                Normalize(e2);
                // The patch's one real direction, squared off against the normal. For a single
                // cell there is none and any tangent will do.
                double[] axis = values[0] > PlanarRankTolerance
                    ? vecs[0]
                    : (Math.Abs(e2[2]) > 0.95 ? new[] { 1.0, 0.0, 0.0 } : new[] { 0.0, 0.0, 1.0 });
                double along = Dot(axis, e2);
                e0 = new[] { axis[0] - along * e2[0], axis[1] - along * e2[1], axis[2] - along * e2[2] };
                if (Dot(e0, e0) < 1e-12) e0 = Cross(e2, new[] { 0.0, 1.0, 0.0 });
                if (Dot(e0, e0) < 1e-12) e0 = Cross(e2, new[] { 1.0, 0.0, 0.0 });
                Normalize(e0);
                e1 = Cross(e2, e0);
            }

            double h0 = 0.0, h1 = 0.0;
            foreach (var p in patch)
            {
                double vx = p.x - c[0], vy = p.y - c[1], vz = p.z - c[2];
                h0 = Math.Max(h0, Math.Abs(vx * e0[0] + vy * e0[1] + vz * e0[2]));
                h1 = Math.Max(h1, Math.Abs(vx * e1[0] + vy * e1[1] + vz * e1[2]));
            }
            // +1 cell: a patch is a set of unit CELLS, so its extent is the spread of their
            // centres plus the cell itself.
            double l0 = (2.0 * h0 + 1.0) * rules.Pad;
            double l1 = (2.0 * h1 + 1.0) * rules.Pad;

            return new Plate(seedCell,
                new Vector3((float)c[0], (float)c[1], (float)c[2]),
                new Vector3((float)e0[0], (float)e0[1], (float)e0[2]),
                new Vector3((float)e1[0], (float)e1[1], (float)e1[2]),
                new Vector3((float)e2[0], (float)e2[1], (float)e2[2]),
                new Vector3((float)l0, (float)l1, rules.Thickness),
                patch.Count);
        }

        /// <summary>
        /// Below this, a dot product is not evidence about which way a plate faces. A patch's
        /// fitted normal can come out very nearly PERPENDICULAR to the cell's own census normal
        /// (three cells arranged edge-on to the surface do exactly that), and then "flip it if the
        /// dot is negative" is a coin toss decided by float noise - it flips between two machines,
        /// and between this file and the offline model, for no reason anyone can see. Measured: 8
        /// of 1,848 Space plates landed there.
        /// </summary>
        const double OrientationTolerance = 1e-3;

        /// <summary>
        /// Whether a fitted normal points INTO the plant, deterministically. The cell's census
        /// normal answers it whenever it is a real answer; otherwise the RADIAL does, which is
        /// always meaningful for a set defined about the origin and is a pure function of the
        /// patch's own integer cells; and if the patch somehow straddles the origin, a sign
        /// convention on the vector's first significant component answers it rather than leaving
        /// float noise to.
        /// </summary>
        static bool PointsInward(double[] normal, Vector3 census, double[] centroid)
        {
            double d = normal[0] * census.x + normal[1] * census.y + normal[2] * census.z;
            if (Math.Abs(d) > OrientationTolerance) return d < 0.0;

            d = normal[0] * centroid[0] + normal[1] * centroid[1] + normal[2] * centroid[2];
            if (Math.Abs(d) > OrientationTolerance) return d < 0.0;

            for (int i = 0; i < 3; i++)
                if (Math.Abs(normal[i]) > OrientationTolerance) return normal[i] < 0.0;
            return false;
        }

        static double[] Cross(double[] a, double[] b) => new[]
        {
            a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0],
        };

        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];

        static void Normalize(double[] v)
        {
            double m = Math.Sqrt(Dot(v, v));
            if (m > 1e-12) { v[0] /= m; v[1] /= m; v[2] /= m; }
        }

        static bool IsHidden(in Plate candidate, List<Plate> laid,
                             Dictionary<Vector3Int, List<int>> bucket, double bucketSize, float drop)
        {
            var key = BucketKey(candidate.Centre, bucketSize);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!bucket.TryGetValue(new Vector3Int(key.x + dx, key.y + dy, key.z + dz),
                                                out var list)) continue;
                        foreach (int i in list)
                            if (TouchingScale(candidate, laid[i]) < drop) return true;
                    }
            return false;
        }

        /// <summary>
        /// The uniform scale at which two plates first touch, exactly: both boxes are centrally
        /// symmetric, so scaling by <c>s</c> scales every projection radius by <c>s</c> while the
        /// centre offset is fixed, and the separating-axis theorem collapses to
        /// <c>s* = max over the 15 candidate axes of |d.u| / (rA(u) + rB(u))</c>. Below it some
        /// axis separates them; above it none does. <c>s* &gt;= 1</c> means they are clear as
        /// they stand. The same closed form <c>Tools/Build/fit_shield_clearance.py</c> uses.
        /// </summary>
        public static float TouchingScale(in Plate a, in Plate b)
        {
            Span<Vector3> axes = stackalloc Vector3[15];
            int n = 0;
            axes[n++] = a.Right; axes[n++] = a.Up; axes[n++] = a.Forward;
            axes[n++] = b.Right; axes[n++] = b.Up; axes[n++] = b.Forward;
            for (int i = 0; i < 3; i++)
                for (int j = 3; j < 6; j++)
                {
                    var u = CrossV(axes[i], axes[j]);
                    if (u.sqrMagnitude > 1e-12f) axes[n++] = u.normalized;
                }

            var d = new Vector3(b.Centre.x - a.Centre.x, b.Centre.y - a.Centre.y,
                                b.Centre.z - a.Centre.z);
            float best = 0f;
            for (int i = 0; i < n; i++)
            {
                var u = axes[i];
                float ra = Support(a, u), rb = Support(b, u);
                if (ra + rb <= 1e-12f) continue;
                float s = Math.Abs(DotV(d, u)) / (ra + rb);
                if (s > best) best = s;
            }
            return best;
        }

        static float Support(in Plate p, Vector3 u) =>
            0.5f * (p.Size.x * Math.Abs(DotV(p.Right, u)) + p.Size.y * Math.Abs(DotV(p.Up, u)) +
                    p.Size.z * Math.Abs(DotV(p.Forward, u)));

        static Vector3 CrossV(Vector3 a, Vector3 b) => new(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        static float DotV(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
    }
}
