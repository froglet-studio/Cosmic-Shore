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
    }
}
