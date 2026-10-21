using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Ourobor" - the one-sided-country cell: THREE ultrawide Möbius bands, interlocked on the
    /// three coordinate planes around the nucleus, each carrying rolling countryside on its
    /// surface and a cityscape hanging off BOTH faces.
    ///
    /// It exists to keep the two best things about the pre-tetrahedral Caldera - the pleasant
    /// rolling ground plane and the fun cityscape at its base - while paying none of the
    /// gravitational debt that got that build reworked (Docs/ECOSYSTEM.md §18.1). Both survive as
    /// *local* feels: each band is ~290 units across, so at flight scale the ground under you is
    /// as flat and rolling as a landscape, and the towers around you stand as straight as a
    /// skyline. Only when you keep going does the surface curve out from under the idea of a
    /// single up - and because the band carries an ODD number of half twists it is genuinely
    /// one-sided, so following the countryside far enough returns you to your own starting patch
    /// standing upside down on the other face. The stalagmites you flew between on the way out
    /// are the stalactites you fly between on the way back. They were never different towers.
    ///
    /// Families: the rolling ground (noise-displaced plates with ponds and field patches), the
    /// cityscape (basalt-column tower bundles on two rings whose gaps fit the vessel, grown along
    /// ±normal so half hang and half stand, with a skyline of heights and shielded crowns), the
    /// CORNICE - the band's single boundary curve, which needs u to run 0→4π to close, and is the
    /// cell's proof of its own one-sidedness - a centreline road, and drifting motes between the
    /// bands. Jade country + gold fields and crowns + blue city stone. No danger anywhere: this
    /// is the pastoral pole, and its risk is disorientation, not damage.
    ///
    /// Nothing is laid inside <see cref="NucleusR"/> - see Docs/ECOSYSTEM.md §13: the nucleus
    /// interior is the territorial claim, and an authored environment sitting in it hands node
    /// control to whatever colour it favours before a player has flown a metre.
    ///
    /// Every prism is laid FLUSH (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10): where the families meet - road and towers on the rolling ground, edge plates over
    /// the cornice, two bands' countrysides at an interchange, a mote inside a tower - one side
    /// gives up the least size that parts them, worked out before the lay (<see cref="PlanFlush"/>).
    /// Same prisms, same places, same draws.
    /// </summary>
    public class SpawnableOurobor : CellEnvironmentSpawnableBase
    {
        // ── Shell geometry (every radius is distance from the cell centre) ──
        //
        // NucleusR is Nucleus.prefab's world radius (localScale 400 x the Node mesh's ~0.98 unit
        // radius) - the figure Cell.RefreshNucleusControlRadius derives from the renderer bounds.
        // A band's deepest possible prism is Radius - HalfWidth - RollAmp - TowerDepth, so every
        // BandSpec below is authored to keep that above NucleusR.
        const float NucleusR = 392f;
        const float RollAmp = 30f;       // amplitude of the rolling-country displacement
        const float TowerDepth = 38f;    // deepest a stalactite hangs (20 segments x 1.9)

        protected override int DefaultSeed => 79;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableOurobor), 2);
        protected override int LayCapacity => 40000;

        /// <summary>The Ourobor lays every prism flush (PlanFlush), and where two families meet a
        /// few prisms are flush at less than the prism prefab's 0.5 floor on one axis (a plate
        /// thinned under the road, a mote slimmed beside a tower). The prefab would grow those back
        /// into their neighbours, so this world admits its authored sizes (every other size it lays
        /// is inside the window already, so nothing else changes).</summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        readonly struct BandSpec
        {
            public readonly float Radius;      // centreline circle radius
            public readonly float HalfWidth;   // ULTRAWIDE: half the ribbon's span
            public readonly int HalfTwists;    // MUST be odd - that is what makes the band one-sided
            public readonly float TiltDeg;     // nudge off the coordinate plane (nothing is exact here)
            public readonly float PhaseDeg;    // where the twist starts, so the three don't rhyme
            public readonly Domains Ground, Field, Stone, Crown;

            public BandSpec(float radius, float halfWidth, int halfTwists, float tiltDeg, float phaseDeg,
                Domains ground, Domains field, Domains stone, Domains crown)
            {
                Radius = radius; HalfWidth = halfWidth; HalfTwists = halfTwists;
                TiltDeg = tiltDeg; PhaseDeg = phaseDeg;
                Ground = ground; Field = field; Stone = stone; Crown = crown;
            }
        }

        static readonly BandSpec[] BandSpecs =
        {
            // The homeland: the widest, calmest band, one half twist - the long way round is the
            // clearest demonstration that there is only one side.
            new(620f, 145f, 1, 7f, 0f, Domains.Jade, Domains.Gold, Domains.Blue, Domains.Gold),
            // The wringer: three half twists over the same loop, so the horizon rolls three times
            // as fast and the towers scissor past each other.
            new(700f, 160f, 3, -11f, 40f, Domains.Jade, Domains.Blue, Domains.Gold, Domains.Ruby),
            // The narrow: five half twists on the tightest ribbon - nearly a corkscrew, and the
            // one band you can see the far side of from the near side.
            new(780f, 130f, 5, 15f, 95f, Domains.Gold, Domains.Jade, Domains.Blue, Domains.Ruby),
        };

        /// <summary>A band's working frame. <c>E1</c>/<c>E2</c> span its loop plane and <c>E3</c> is
        /// that plane's normal; the width direction rotates out of E1/E2 into E3 as it goes round,
        /// which IS the Möbius twist.</summary>
        readonly struct Band
        {
            public readonly Vector3 E1, E2, E3;
            public readonly float Radius, HalfWidth, TwistRate, Phase;

            public Band(Vector3 e1, Vector3 e2, Vector3 e3, in BandSpec spec)
            {
                E1 = e1; E2 = e2; E3 = e3;
                Radius = spec.Radius; HalfWidth = spec.HalfWidth;
                TwistRate = spec.HalfTwists * 0.5f;
                Phase = spec.PhaseDeg * Mathf.Deg2Rad;
            }

            /// <summary>Outward direction in the loop plane at <paramref name="u"/>.</summary>
            public Vector3 Radial(float u) => E1 * Mathf.Cos(u) + E2 * Mathf.Sin(u);

            /// <summary>Direction of travel around the loop.</summary>
            public Vector3 Along(float u) => E2 * Mathf.Cos(u) - E1 * Mathf.Sin(u);

            /// <summary>The ribbon's width direction - rotated <c>TwistRate * u</c> out of the loop
            /// plane. With an ODD half-twist count it has flipped sign after a full lap, which is
            /// exactly why the band has one side and one edge.</summary>
            public Vector3 Width(float u)
            {
                float h = Phase + TwistRate * u;
                return Radial(u) * Mathf.Cos(h) + E3 * Mathf.Sin(h);
            }

            /// <summary>The width direction's partner in the rotating frame (needed for the exact
            /// surface normal).</summary>
            public Vector3 Rise(float u)
            {
                float h = Phase + TwistRate * u;
                return Radial(u) * -Mathf.Sin(h) + E3 * Mathf.Cos(h);
            }

            public Vector3 At(float u, float v) => Radial(u) * Radius + Width(u) * v;

            /// <summary>Exact ∂P/∂u: the loop tangent stretched by the width term, plus the twist's
            /// own contribution.</summary>
            public Vector3 AlongSurface(float u, float v)
            {
                float h = Phase + TwistRate * u;
                return Along(u) * (Radius + v * Mathf.Cos(h)) + Rise(u) * (v * TwistRate);
            }

            /// <summary>Unit surface normal - the local "up" that only exists locally.</summary>
            public Vector3 Normal(float u, float v) =>
                Vector3.Cross(AlongSurface(u, v), Width(u)).normalized;
        }

        Band[] _bands;

        protected override void BuildEnvironment()
        {
            BuildBands();
            PlanFlush();

            for (int b = 0; b < BandSpecs.Length; b++)
            {
                ref readonly BandSpec spec = ref BandSpecs[b];
                BuildRollingGround(spec, _bands[b], b);
                BuildCityscape(spec, _bands[b], b);
                BuildCornice(spec, _bands[b], b);
                BuildSpineRoad(spec, _bands[b], b);
            }

            BuildDrift();
        }

        // =====================================================================
        //  Frames
        // =====================================================================

        void BuildBands()
        {
            // One band per coordinate plane, so the three interlock like the great circles of a
            // sphere. They are NOT kept apart: where two bands pass they cross, and a crossing is
            // a multi-level interchange with country and city on every deck - which is the whole
            // point of a cell with no up.
            Vector3[][] planes =
            {
                new[] { Vector3.right, Vector3.up, Vector3.forward },
                new[] { Vector3.up, Vector3.forward, Vector3.right },
                new[] { Vector3.forward, Vector3.right, Vector3.up },
            };

            _bands = new Band[BandSpecs.Length];
            for (int b = 0; b < BandSpecs.Length; b++)
            {
                ref readonly BandSpec spec = ref BandSpecs[b];

                // Authoring guard, not a runtime fix: a band whose deepest reach dips into the
                // node-control zone hands DominantDomain to its own palette before anyone flies
                // (§13), and that failure is invisible in-editor. Fail loud instead.
                float deepest = spec.Radius - spec.HalfWidth - RollAmp - TowerDepth;
                if (deepest < NucleusR)
                    Debug.LogError($"[Ourobor] Band {b} reaches r={deepest:F0}, inside the nucleus " +
                                   $"control radius {NucleusR}. Raise its Radius or narrow its HalfWidth.");
                if (spec.HalfTwists % 2 == 0)
                    Debug.LogError($"[Ourobor] Band {b} has {spec.HalfTwists} half twists - an EVEN " +
                                   "count makes an ordinary two-sided annulus, not a Möbius band.");

                // Tilt the plane off true so the three are siblings, not a diagram.
                var tilt = Quaternion.AngleAxis(spec.TiltDeg, planes[b][0]);
                _bands[b] = new Band(tilt * planes[b][0], tilt * planes[b][1], tilt * planes[b][2], spec);
            }
        }

        // =====================================================================
        //  Flush (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10)
        // =====================================================================
        //
        // The families are sparse on their own - plates on an 11 x 13 grid, road and cornice
        // links on a 6.5 step, tower storeys on a 1.9 step - so each is flush by itself. What
        // clipped is where they meet: the spine road and the towers stand on the same rolling
        // ground (the ground's fine swell lifts a plate into the road or through a tower's lower
        // storeys), the edge plates reach over the cornice, two bands' countrysides cross at an
        // interchange, and a few drifting motes landed inside the structure. Every prism and its
        // size is worked out before the first is laid, and at each meeting ONE family yields:
        //
        //   road, towers, cornice  -> the ground (and towers -> the road, cornice -> keystone)
        //   a later band's ground  -> an earlier band's ground
        //   drifting motes         -> everything
        //
        // The yielding prism gives up the least it can (OneSided: the one half-extent that
        // reaches furthest along the face axis that best separates them). Where its centre sits
        // inside the other, so it cannot step aside alone, the pair parts by SplitShare - both
        // give up the same fraction - and the other family takes its half from a short list of
        // such sitters. Each prism only ever looks at the handful of others its own grid indices
        // say can reach it (ToBand inverts a band's parameterisation), so this is a few dozen
        // dot products per prism, not a search; nothing is resolved after the lay. The jitter's
        // draws are taken while planning, in exactly the order the lay used to take them (each
        // band's ground, then its towers), so every prism keeps the size jitter it always had.

        /// <summary>A planned prism: its pose and its size before the flush pass.</summary>
        readonly struct Lay
        {
            public readonly Vector3 At;
            public readonly Quaternion Rot;
            public readonly Vector3 Env;
            public Lay(Vector3 at, Quaternion rot, Vector3 env) { At = at; Rot = rot; Env = env; }
            public Box Box => new(At, Rot, Env);
        }

        /// <summary>An oriented box (centre, axes, half extents) for the flush arithmetic.</summary>
        readonly struct Box
        {
            public readonly Vector3 C, X, Y, Z, H;
            public Box(Vector3 c, Quaternion rot, Vector3 size)
            {
                C = c; X = rot * Vector3.right; Y = rot * Vector3.up; Z = rot * Vector3.forward; H = 0.5f * size;
            }
            public Vector3 Axis(int k) => k == 0 ? X : k == 1 ? Y : Z;
            public float Half(int k) => k == 0 ? H.x : k == 1 ? H.y : H.z;
            public float Extent(Vector3 a) =>
                H.x * Mathf.Abs(Vector3.Dot(X, a)) + H.y * Mathf.Abs(Vector3.Dot(Y, a)) + H.z * Mathf.Abs(Vector3.Dot(Z, a));
            public float Reach => H.magnitude;
        }

        /// <summary>A hair under exact contact, so float noise never turns a touching pair into a clip.</summary>
        const float Flush = 0.995f;

        static Vector3 Min3(Vector3 a, Vector3 b) => new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));

        static Vector3 SepAxis(in Box a, in Box b, int l) =>
            l < 3 ? a.Axis(l) : l < 6 ? b.Axis(l - 3) : Vector3.Cross(a.Axis((l - 6) / 3), b.Axis((l - 6) % 3));

        /// <summary>The 15-axis separating-axis test: true when the two boxes do not interpenetrate.</summary>
        static bool Apart(in Box a, in Box b)
        {
            var d = b.C - a.C;
            if (d.sqrMagnitude >= (a.Reach + b.Reach) * (a.Reach + b.Reach)) return true;
            for (int l = 0; l < 15; l++)
            {
                var axis = SepAxis(a, b, l);
                if (axis.sqrMagnitude < 1e-10f) continue;
                if (Mathf.Abs(Vector3.Dot(d, axis)) >= a.Extent(axis) + b.Extent(axis)) return true;
            }
            return false;
        }

        /// <summary>
        /// <paramref name="self"/> yields to <paramref name="other"/> alone: on the face axis that
        /// leaves it the most, it gives up only the one half-extent that reaches furthest along it.
        /// Returns the factor per axis of <paramref name="self"/>; false when no single axis can do
        /// it (its centre sits inside the other).
        /// </summary>
        static bool OneSided(in Box self, in Box other, out Vector3 share)
        {
            share = Vector3.one;
            if (Apart(self, other)) return true;
            var d = other.C - self.C;
            float best = -1f;
            int bestK = 0;
            for (int l = 0; l < 6; l++)
            {
                var axis = SepAxis(self, other, l);
                float cs = -1f, ss = 0f;
                int k = 0;
                for (int a = 0; a < 3; a++)
                {
                    float c = self.Half(a) * Mathf.Abs(Vector3.Dot(self.Axis(a), axis));
                    ss += c;
                    if (c > cs) { cs = c; k = a; }
                }
                float budget = Mathf.Abs(Vector3.Dot(d, axis)) - (ss - cs) - other.Extent(axis);
                if (budget <= 0f || cs <= 0f) continue;
                if (budget / cs > best) { best = budget / cs; bestK = k; }
            }
            if (best <= 0f) return false;
            share[bestK] = Mathf.Min(1f, best) * Flush;
            return true;
        }

        /// <summary>
        /// Two prisms that cannot step aside alone part by sharing the cost: on the face axis that
        /// separates them best, each gives up the same fraction of its one half-extent that reaches
        /// furthest along it. Symmetric (the pair is taken in a fixed order), so each side computes
        /// its own half of the same split; per axis, so a prism crossing several keeps the
        /// smallest factor per axis. Where no single axis can do it both shrink uniformly to the
        /// pair's flush scale.
        /// </summary>
        static Vector3 SplitShare(in Box self, in Box other)
        {
            if (Apart(self, other)) return Vector3.one;
            bool swap = other.C.x < self.C.x || (other.C.x == self.C.x &&
                (other.C.y < self.C.y || (other.C.y == self.C.y && other.C.z < self.C.z)));
            Box a = swap ? other : self, b = swap ? self : other;
            var d = b.C - a.C;
            float best = -1f;
            int bestA = 0, bestB = 0;
            for (int l = 0; l < 6; l++)
            {
                var axis = SepAxis(a, b, l);
                float ca = -1f, cb = -1f, sa = 0f, sb = 0f;
                int ia = 0, ib = 0;
                for (int k = 0; k < 3; k++)
                {
                    float c1 = a.Half(k) * Mathf.Abs(Vector3.Dot(a.Axis(k), axis));
                    float c2 = b.Half(k) * Mathf.Abs(Vector3.Dot(b.Axis(k), axis));
                    sa += c1; sb += c2;
                    if (c1 > ca) { ca = c1; ia = k; }
                    if (c2 > cb) { cb = c2; ib = k; }
                }
                float budget = Mathf.Abs(Vector3.Dot(d, axis)) - (sa - ca) - (sb - cb);
                if (budget <= 0f || ca + cb <= 0f) continue;
                if (budget / (ca + cb) > best) { best = budget / (ca + cb); bestA = ia; bestB = ib; }
            }
            var share = Vector3.one;
            if (best > 0f)
            {
                share[swap ? bestB : bestA] = Mathf.Min(1f, best) * Flush;
                return share;
            }
            float u = 0f;
            for (int l = 0; l < 15; l++)
            {
                var axis = SepAxis(a, b, l);
                float reach = a.Extent(axis) + b.Extent(axis);
                if (reach > 1e-6f) u = Mathf.Max(u, Mathf.Abs(Vector3.Dot(d, axis)) / reach);
            }
            u = Mathf.Min(1f, u) * Flush;
            return new Vector3(u, u, u);
        }

        /// <summary>Yield <paramref name="self"/> to <paramref name="other"/>: alone where it can,
        /// else by a split, recording itself among <paramref name="sitters"/> so the other side takes
        /// its half.</summary>
        static void Yield(in Box self, in Box other, ref Vector3 share, ref List<Box> sitters)
        {
            if (OneSided(self, other, out var alone)) { share = Min3(share, alone); return; }
            share = Min3(share, SplitShare(self, other));
            (sitters ??= new List<Box>()).Add(self);
        }

        /// <summary>The factors for <paramref name="self"/> from every sitter it crosses.</summary>
        static Vector3 FromSitters(in Box self, List<Box> sitters)
        {
            var share = Vector3.one;
            if (sitters == null) return share;
            for (int i = 0; i < sitters.Count; i++) share = Min3(share, SplitShare(self, sitters[i]));
            return share;
        }

        /// <summary>Where <paramref name="p"/> sits over band <paramref name="band"/>: its loop angle
        /// u in [0, 2 pi), its width coordinate v, and how far it is off the flat ribbon. Exact for
        /// u (the width direction has no along-loop part); the ribbon's swell is along the normal,
        /// which has no width part, so v holds too.</summary>
        static void ToBand(in Band band, Vector3 p, out float u, out float v, out float off)
        {
            u = Mathf.Atan2(Vector3.Dot(p, band.E2), Vector3.Dot(p, band.E1));
            if (u < 0f) u += 2f * Mathf.PI;
            v = Vector3.Dot(p - band.Radial(u) * band.Radius, band.Width(u));
            off = (p - band.At(u, v)).magnitude;
        }

        /// <summary>How far a ground plate's centre can lie off the flat ribbon (both swells) plus
        /// its own reach - the band-distance beyond which no plate can touch a point.</summary>
        const float GroundReach = 0.5f * RollAmp + 4.5f + 4.6f;

        Lay[][] _ground;              // per band, iu * nv + iv; Env == 0 where the cull leaves a pond
        Vector3[][] _groundShare;
        int[] _nu, _nv;
        Lay[][] _road, _corn;         // per band, index i (1-based, as laid)
        Vector3[][] _roadShare, _cornShare;
        Lay[] _key;
        Vector3[] _keyShare;
        List<Lay>[] _city;
        List<Vector3>[] _cityShare;
        List<(Vector3 seat, int start, int count)>[] _clusters;
        Lay[] _drift;
        Vector3[] _driftShare;

        void PlanFlush()
        {
            int nb = BandSpecs.Length;
            _ground = new Lay[nb][]; _groundShare = new Vector3[nb][]; _nu = new int[nb]; _nv = new int[nb];
            _road = new Lay[nb][]; _roadShare = new Vector3[nb][];
            _corn = new Lay[nb][]; _cornShare = new Vector3[nb][];
            _key = new Lay[nb]; _keyShare = new Vector3[nb];
            _city = new List<Lay>[nb]; _cityShare = new List<Vector3>[nb]; _clusters = new List<(Vector3, int, int)>[nb];
            var sitGround = new List<Box>[nb];
            var sitRoad = new List<Box>[nb];
            var sitCorn = new List<Box>[nb];
            var sitKey = new List<Box>[nb];
            var sitCity = new List<Box>[nb];

            for (int b = 0; b < nb; b++)   // the order the lay takes its jitter draws: ground, then towers
            {
                PlanGround(b);
                PlanCityscape(b);
                PlanCornice(b);
                PlanSpineRoad(b);
            }
            PlanDrift();

            // ── the yielders ──
            for (int b = 0; b < nb; b++)
            {
                // A later band's ground yields to an earlier band's where they cross.
                for (int g = 0; g < _ground[b].Length; g++)
                {
                    if (_ground[b][g].Env == Vector3.zero) continue;
                    var box = _ground[b][g].Box;
                    var share = Vector3.one;
                    for (int o = 0; o < b; o++) YieldToGround(o, box, ref share, ref sitGround[o]);
                    _groundShare[b][g] = share;
                }
                for (int i = 1; i < _road[b].Length; i++)
                {
                    var box = _road[b][i].Box;
                    var share = Vector3.one;
                    for (int o = 0; o < nb; o++) YieldToGround(o, box, ref share, ref sitGround[o]);
                    _roadShare[b][i] = share;
                }
                for (int i = 1; i < _corn[b].Length; i++)
                {
                    var box = _corn[b][i].Box;
                    var share = Vector3.one;
                    for (int o = 0; o < nb; o++) YieldToGround(o, box, ref share, ref sitGround[o]);
                    Yield(box, _key[b].Box, ref share, ref sitKey[b]);
                    _cornShare[b][i] = share;
                }
                for (int i = 0; i < _city[b].Count; i++)
                {
                    var box = _city[b][i].Box;
                    var share = Vector3.one;
                    for (int o = 0; o < nb; o++) YieldToGround(o, box, ref share, ref sitGround[o]);
                    YieldToRoad(b, box, ref share, ref sitRoad[b]);
                    _cityShare[b].Add(share);
                }
            }
            for (int i = 0; i < _drift.Length; i++)
            {
                if (_drift[i].Env == Vector3.zero) continue;
                var box = _drift[i].Box;
                var share = Vector3.one;
                for (int o = 0; o < nb; o++)
                {
                    YieldToGround(o, box, ref share, ref sitGround[o]);
                    YieldToRoad(o, box, ref share, ref sitRoad[o]);
                    YieldToCornice(o, box, ref share, ref sitCorn[o], ref sitKey[o]);
                    YieldToCity(o, box, ref share, ref sitCity[o]);
                }
                _driftShare[i] = share;
            }

            // ── the families that were yielded to take their half from the sitters ──
            for (int b = 0; b < nb; b++)
            {
                for (int g = 0; g < _ground[b].Length; g++)
                    if (_ground[b][g].Env != Vector3.zero)
                        _groundShare[b][g] = Min3(_groundShare[b][g], FromSitters(_ground[b][g].Box, sitGround[b]));
                for (int i = 1; i < _road[b].Length; i++)
                    _roadShare[b][i] = Min3(_roadShare[b][i], FromSitters(_road[b][i].Box, sitRoad[b]));
                for (int i = 1; i < _corn[b].Length; i++)
                    _cornShare[b][i] = Min3(_cornShare[b][i], FromSitters(_corn[b][i].Box, sitCorn[b]));
                _keyShare[b] = FromSitters(_key[b].Box, sitKey[b]);
                for (int i = 0; i < _city[b].Count; i++)
                    _cityShare[b][i] = Min3(_cityShare[b][i], FromSitters(_city[b][i].Box, sitCity[b]));
            }
        }

        void YieldToGround(int o, in Box box, ref Vector3 share, ref List<Box> sitters)
        {
            ToBand(_bands[o], box.C, out float u, out float v, out float off);
            if (off > GroundReach + box.Reach) return;
            int nu = _nu[o], nv = _nv[o];
            float hw = _bands[o].HalfWidth;
            int iu0 = Mathf.RoundToInt(u / (2f * Mathf.PI) * nu);
            for (int du = -2; du <= 2; du++)
            {
                // Across the seam (u = 0 is u = 2 pi) the width coordinate changes sign: that is
                // the half twist.
                int raw = iu0 + du, iu = (raw % nu + nu) % nu;
                float vv = raw == iu ? v : -v;
                int iv0 = Mathf.RoundToInt((vv + hw) / (2f * hw) * nv - 0.5f);
                for (int iv = Mathf.Max(0, iv0 - 2); iv <= Mathf.Min(nv - 1, iv0 + 2); iv++)
                {
                    ref readonly var g = ref _ground[o][iu * nv + iv];
                    if (g.Env == Vector3.zero || g.At == box.C) continue;
                    Yield(box, g.Box, ref share, ref sitters);
                }
            }
        }

        void YieldToRoad(int o, in Box box, ref Vector3 share, ref List<Box> sitters)
        {
            ToBand(_bands[o], box.C, out float u, out float v, out float off);
            if (Mathf.Abs(v) > 6f + box.Reach || off > 0.5f * RollAmp + 3f + box.Reach) return;
            int steps = _road[o].Length - 1;
            int i0 = Mathf.RoundToInt(u / (2f * Mathf.PI) * steps);
            for (int di = -2; di <= 2; di++)
            {
                int i = ((i0 + di - 1) % steps + steps) % steps + 1;
                Yield(box, _road[o][i].Box, ref share, ref sitters);
            }
        }

        void YieldToCornice(int o, in Box box, ref Vector3 share, ref List<Box> sitCorn, ref List<Box> sitKey)
        {
            Yield(box, _key[o].Box, ref share, ref sitKey);
            ToBand(_bands[o], box.C, out float u, out float v, out float off);
            float hw = _bands[o].HalfWidth;
            if (Mathf.Abs(v) < hw - 4f - box.Reach || off > 4f + box.Reach) return;
            int steps = _corn[o].Length - 1;
            // v = +hw at u is the first lap; v = -hw at u is the second (u + 2 pi).
            float lap = v > 0f ? u : u + 2f * Mathf.PI;
            int i0 = Mathf.RoundToInt(lap / (4f * Mathf.PI) * steps);
            for (int di = -2; di <= 2; di++)
            {
                int i = ((i0 + di - 1) % steps + steps) % steps + 1;
                Yield(box, _corn[o][i].Box, ref share, ref sitCorn);
            }
        }

        void YieldToCity(int o, in Box box, ref Vector3 share, ref List<Box> sitters)
        {
            foreach (var (seat, start, count) in _clusters[o])
            {
                if ((box.C - seat).sqrMagnitude > 80f * 80f) continue;
                for (int i = start; i < start + count; i++)
                    Yield(box, _city[o][i].Box, ref share, ref sitters);
            }
        }

        // =====================================================================
        //  The rolling country
        // =====================================================================

        /// <summary>The pleasant rolling landscape, transplanted from a ground plane onto a ribbon:
        /// scattered plates laid flat on the surface, lifted by low-frequency noise into swells and
        /// hollows, with noise-cut ponds and gold field patches. Locally it is a countryside; only
        /// the band's own curvature says otherwise.</summary>
        void PlanGround(int b)
        {
            var band = _bands[b];
            int nu = Mathf.Max(24, (int)(2f * Mathf.PI * band.Radius / 11f));
            int nv = Mathf.Max(6, (int)(2f * band.HalfWidth / 13f));
            _nu[b] = nu; _nv[b] = nv;
            _ground[b] = new Lay[nu * nv];
            _groundShare[b] = new Vector3[nu * nv];
            for (int iu = 0; iu < nu; iu++)
            {
                float u = 2f * Mathf.PI * iu / nu;
                for (int iv = 0; iv < nv; iv++)
                {
                    // Rows are offset against each other so the sampling never reads as a grid.
                    float v = Mathf.Lerp(-band.HalfWidth, band.HalfWidth, (iv + 0.5f) / nv)
                              + 4.5f * Mathf.Sin(iu * 0.7f + iv);
                    var flat = band.At(u, v);

                    // Ponds and broken ground - the same cull that gave the old floor its scatter.
                    if (N01(flat.x * 0.013f, flat.y * 0.013f, flat.z * 0.013f, 3) < 0.30f) continue;

                    var n = band.Normal(u, v);
                    float swell = RollAmp * (N01(flat.x * 0.007f, flat.y * 0.007f, flat.z * 0.007f, 4) - 0.5f)
                                  + 9f * (N01(flat.x * 0.031f, flat.y * 0.031f, flat.z * 0.031f, 5) - 0.5f);
                    _ground[b][iu * nv + iv] = new Lay(flat + n * swell,
                        SpawnPoint.LookRotation(n, band.AlongSurface(u, v)), Jit(new Vector3(4.6f, 5.2f, 1f), 0.3f));
                }
            }
        }

        void BuildRollingGround(in BandSpec spec, in Band band, int b)
        {
            int nu = _nu[b], nv = _nv[b];
            for (int iu = 0; iu < nu; iu++)
                for (int iv = 0; iv < nv; iv++)
                {
                    ref readonly var g = ref _ground[b][iu * nv + iv];
                    if (g.Env == Vector3.zero) continue;
                    float u = 2f * Mathf.PI * iu / nu;
                    float v = Mathf.Lerp(-band.HalfWidth, band.HalfWidth, (iv + 0.5f) / nv) + 4.5f * Mathf.Sin(iu * 0.7f + iv);
                    var flat = band.At(u, v);
                    float field = N01(flat.x * 0.010f, flat.y * 0.010f, flat.z * 0.010f, 6);
                    Domains dom = field > 0.62f ? spec.Field : field < 0.31f ? Domains.Blue : spec.Ground;
                    Emit(g.At, g.Rot, Vector3.Scale(g.Env, _groundShare[b][iu * nv + iv]), dom);
                }
        }

        // =====================================================================
        //  The cityscape - on both faces
        // =====================================================================

        /// <summary>Tower bundles seated across the country and grown along ±normal. The sign is
        /// noise, so roughly half stand off the surface and half hang under it - but on a one-sided
        /// band "under" is only ever a statement about where you are standing, and the field of
        /// stalagmites you climbed out through is the field of stalactites you come back down
        /// through. Bundle geometry is the Giant's-Causeway idiom Caldera uses: solid pipes on two
        /// rings whose gaps fit the vessel, so a city is flyable terrain, not a wall.</summary>
        void PlanCityscape(int b)
        {
            var band = _bands[b];
            _city[b] = new List<Lay>();
            _cityShare[b] = new List<Vector3>();
            _clusters[b] = new List<(Vector3, int, int)>();
            const int clusters = 22;
            for (int c = 0; c < clusters; c++)
            {
                int start = _city[b].Count;
                float u = (c * GoldenAngle) % (2f * Mathf.PI);
                float v = band.HalfWidth * (2f * Hash01(c * 37 + b * 211 + _noiseSeed) - 1f) * 0.82f;
                var seat = band.At(u, v);
                var n = band.Normal(u, v);
                var along = band.AlongSurface(u, v).normalized;
                float swell = RollAmp * (N01(seat.x * 0.007f, seat.y * 0.007f, seat.z * 0.007f, 4) - 0.5f);
                seat += n * swell;

                // Which face this district grows from. Noise, not alternation - a real skyline
                // clumps, and the clumping is what makes the flip legible when you cross one.
                float side = N01(seat.x * 0.009f, seat.y * 0.009f, seat.z * 0.009f, 7) < 0.5f ? -1f : 1f;
                var grow = n * side;
                var lateral = Vector3.Cross(grow, along).normalized;
                var rot = SpawnPoint.LookRotation(grow, along);

                // A skyline needs a spread of heights, not a mean.
                float tall = Hash01(c * 13 + b * 71);
                int storeys = 6 + (int)(14f * tall * tall);
                int cols = 7 + (int)(6f * Hash01(c * 7 + b * 29));

                for (int co = 0; co < cols; co++)
                {
                    float ca = 2f * Mathf.PI * co / cols;
                    float ring = 9.5f * (1 + co % 2);
                    var off = lateral * (ring * Mathf.Cos(ca)) + along * (ring * Mathf.Sin(ca));
                    int height = Mathf.Max(3, storeys - (int)(4f * Hash01(c * 101 + co + b)));
                    for (int h = 0; h < height; h++)
                        _city[b].Add(new Lay(seat + off + grow * (h * 1.9f), rot, Jit(new Vector3(3f, 3f, 1.7f), 0.08f)));
                    _city[b].Add(new Lay(seat + off + grow * (height * 1.9f), rot, new Vector3(3.2f, 3.2f, 0.8f)));
                }

                // Every third district gets a spire above the roofline under a shielded crown -
                // the landmark you navigate a featureless country by.
                if (c % 3 == 0)
                {
                    for (int h = 0; h < storeys + 10; h++)
                        _city[b].Add(new Lay(seat + grow * (h * 1.9f), rot, new Vector3(2.2f, 2.2f, 1.7f)));
                    _city[b].Add(new Lay(seat + grow * ((storeys + 10) * 1.9f + 2f), Quaternion.identity,
                        new Vector3(2.4f, 2.4f, 2.4f)));
                }
                _clusters[b].Add((seat, start, _city[b].Count - start));
            }
        }

        void BuildCityscape(in BandSpec spec, in Band band, int b)
        {
            int k = 0;
            const int clusters = 22;
            for (int c = 0; c < clusters; c++)
            {
                float tall = Hash01(c * 13 + b * 71);
                int storeys = 6 + (int)(14f * tall * tall);
                int cols = 7 + (int)(6f * Hash01(c * 7 + b * 29));
                for (int co = 0; co < cols; co++)
                {
                    int height = Mathf.Max(3, storeys - (int)(4f * Hash01(c * 101 + co + b)));
                    for (int h = 0; h < height; h++, k++)
                        Emit(_city[b][k].At, _city[b][k].Rot, Vector3.Scale(_city[b][k].Env, _cityShare[b][k]), spec.Stone);
                    Emit(_city[b][k].At, _city[b][k].Rot, Vector3.Scale(_city[b][k].Env, _cityShare[b][k]), spec.Crown);
                    k++;
                }
                if (c % 3 != 0) continue;
                for (int h = 0; h < storeys + 10; h++, k++)
                    Emit(_city[b][k].At, _city[b][k].Rot, Vector3.Scale(_city[b][k].Env, _cityShare[b][k]), spec.Stone);
                Emit(_city[b][k].At, _city[b][k].Rot, Vector3.Scale(_city[b][k].Env, _cityShare[b][k]),
                    spec.Crown, PrismKind.Shielded);
                k++;
            }
        }

        // =====================================================================
        //  The cornice - the cell's proof of its own one-sidedness
        // =====================================================================

        /// <summary>The band's boundary. A Möbius band has ONE edge, so this traces v = +HalfWidth
        /// with u running 0→4π: after the first lap the rail arrives back at the start on what was
        /// the far edge and has to go round again to close. Fly it and you have flown both "edges"
        /// of the country without ever crossing one. The keystone at the far end of the first lap
        /// is super-shielded - the one fixed point in a cell with no up.</summary>
        void PlanCornice(int b)
        {
            var band = _bands[b];
            int steps = Mathf.Max(64, (int)(4f * Mathf.PI * band.Radius / 6.5f));
            _corn[b] = new Lay[steps + 1];
            _cornShare[b] = new Vector3[steps + 1];
            Vector3 prev = band.At(0f, band.HalfWidth);
            for (int i = 1; i <= steps; i++)
            {
                float u = 4f * Mathf.PI * i / steps;
                var p = band.At(u, band.HalfWidth);
                _corn[b][i] = new Lay(p, SpawnPoint.LookRotation(p - prev, band.Normal(u, band.HalfWidth)),
                    new Vector3(2.2f, 1.2f, 3.6f));
                prev = p;
            }
            _key[b] = new Lay(band.At(Mathf.PI, band.HalfWidth), Quaternion.identity, new Vector3(3.4f, 3.4f, 3.4f));
        }

        void BuildCornice(in BandSpec spec, in Band band, int b)
        {
            for (int i = 1; i < _corn[b].Length; i++)
                Emit(_corn[b][i].At, _corn[b][i].Rot, Vector3.Scale(_corn[b][i].Env, _cornShare[b][i]),
                    i % 5 == 0 ? spec.Crown : spec.Stone);

            Emit(_key[b].At, _key[b].Rot, Vector3.Scale(_key[b].Env, _keyShare[b]), spec.Crown, PrismKind.SuperShielded);
        }

        /// <summary>A road down the centreline - the one line in the cell that reads as a route,
        /// and the fastest way to feel the surface roll out from under your sense of up.</summary>
        void PlanSpineRoad(int b)
        {
            var band = _bands[b];
            int steps = Mathf.Max(48, (int)(2f * Mathf.PI * band.Radius / 6.5f));
            _road[b] = new Lay[steps + 1];
            _roadShare[b] = new Vector3[steps + 1];
            Vector3 prev = band.At(0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float u = 2f * Mathf.PI * i / steps;
                var n = band.Normal(u, 0f);
                var flat = band.At(u, 0f);
                float swell = RollAmp * (N01(flat.x * 0.007f, flat.y * 0.007f, flat.z * 0.007f, 4) - 0.5f);
                var p = flat + n * swell;
                _road[b][i] = new Lay(p, SpawnPoint.LookRotation(p - prev, n), new Vector3(3.4f, 1.1f, 3.8f));
                prev = p;
            }
        }

        void BuildSpineRoad(in BandSpec spec, in Band band, int b)
        {
            for (int i = 1; i < _road[b].Length; i++)
                Emit(_road[b][i].At, _road[b][i].Rot, Vector3.Scale(_road[b][i].Env, _roadShare[b][i]),
                    i % 7 == 0 ? spec.Field : spec.Ground);
        }

        // =====================================================================
        //  Between the bands
        // =====================================================================

        /// <summary>Motes drifting through the voids the three bands leave. Sparse, and deliberately
        /// unaligned to anything - in a cell with no up there is no horizon for them to settle
        /// toward.</summary>
        void PlanDrift()
        {
            int n = Scaled(4400);
            _drift = new Lay[n];
            _driftShare = new Vector3[n];
            float inner = NucleusR + 30f;
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 11) < 0.28f) continue;
                float y = 1f - 2f * (i + 0.5f) / n;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = i * GoldenAngle;
                var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                _drift[i] = new Lay(dir * (inner + 560f * Hash01(i * 7 + _noiseSeed)),
                    Quaternion.Euler(Hash01(i * 5) * 360f, Hash01(i * 13) * 360f, Hash01(i * 3) * 360f),
                    new Vector3(1.9f, 0.9f, 2.6f));
            }
        }

        void BuildDrift()
        {
            for (int i = 0; i < _drift.Length; i++)
            {
                if (_drift[i].Env == Vector3.zero) continue;
                Emit(_drift[i].At, _drift[i].Rot, Vector3.Scale(_drift[i].Env, _driftShare[i]),
                    i % 5 == 0 ? Domains.Gold : Domains.Jade);
            }
        }
    }
}
