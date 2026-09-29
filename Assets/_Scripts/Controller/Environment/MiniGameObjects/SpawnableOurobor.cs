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
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableOurobor), 1);
        protected override int LayCapacity => 40000;

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

            for (int b = 0; b < BandSpecs.Length; b++)
            {
                ref readonly BandSpec spec = ref BandSpecs[b];
                BuildRollingGround(spec, _bands[b]);
                BuildCityscape(spec, _bands[b], b);
                BuildCornice(spec, _bands[b]);
                BuildSpineRoad(spec, _bands[b]);
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
        //  The rolling country
        // =====================================================================

        /// <summary>The pleasant rolling landscape, transplanted from a ground plane onto a ribbon:
        /// scattered plates laid flat on the surface, lifted by low-frequency noise into swells and
        /// hollows, with noise-cut ponds and gold field patches. Locally it is a countryside; only
        /// the band's own curvature says otherwise.</summary>
        void BuildRollingGround(in BandSpec spec, in Band band)
        {
            int nu = Mathf.Max(24, (int)(2f * Mathf.PI * band.Radius / 11f));
            int nv = Mathf.Max(6, (int)(2f * band.HalfWidth / 13f));

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

                    float field = N01(flat.x * 0.010f, flat.y * 0.010f, flat.z * 0.010f, 6);
                    Domains dom = field > 0.62f ? spec.Field : field < 0.31f ? Domains.Blue : spec.Ground;

                    Emit(flat + n * swell,
                        SpawnPoint.LookRotation(n, band.AlongSurface(u, v)),
                        Jit(new Vector3(4.6f, 5.2f, 1f), 0.3f), dom);
                }
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
        void BuildCityscape(in BandSpec spec, in Band band, int b)
        {
            const int clusters = 22;
            for (int c = 0; c < clusters; c++)
            {
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
                        Emit(seat + off + grow * (h * 1.9f),
                            SpawnPoint.LookRotation(grow, along),
                            Jit(new Vector3(3f, 3f, 1.7f), 0.08f), spec.Stone);
                    Emit(seat + off + grow * (height * 1.9f),
                        SpawnPoint.LookRotation(grow, along),
                        new Vector3(3.2f, 3.2f, 0.8f), spec.Crown);
                }

                // Every third district gets a spire above the roofline under a shielded crown -
                // the landmark you navigate a featureless country by.
                if (c % 3 != 0) continue;
                for (int h = 0; h < storeys + 10; h++)
                    Emit(seat + grow * (h * 1.9f), SpawnPoint.LookRotation(grow, along),
                        new Vector3(2.2f, 2.2f, 1.7f), spec.Stone);
                Emit(seat + grow * ((storeys + 10) * 1.9f + 2f), Quaternion.identity,
                    new Vector3(2.4f, 2.4f, 2.4f), spec.Crown, PrismKind.Shielded);
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
        void BuildCornice(in BandSpec spec, in Band band)
        {
            int steps = Mathf.Max(64, (int)(4f * Mathf.PI * band.Radius / 6.5f));
            Vector3 prev = band.At(0f, band.HalfWidth);
            for (int i = 1; i <= steps; i++)
            {
                float u = 4f * Mathf.PI * i / steps;
                var p = band.At(u, band.HalfWidth);
                Emit(p, SpawnPoint.LookRotation(p - prev, band.Normal(u, band.HalfWidth)),
                    new Vector3(2.2f, 1.2f, 3.6f), i % 5 == 0 ? spec.Crown : spec.Stone);
                prev = p;
            }

            Emit(band.At(Mathf.PI, band.HalfWidth), Quaternion.identity,
                new Vector3(3.4f, 3.4f, 3.4f), spec.Crown, PrismKind.SuperShielded);
        }

        /// <summary>A road down the centreline - the one line in the cell that reads as a route,
        /// and the fastest way to feel the surface roll out from under your sense of up.</summary>
        void BuildSpineRoad(in BandSpec spec, in Band band)
        {
            int steps = Mathf.Max(48, (int)(2f * Mathf.PI * band.Radius / 6.5f));
            Vector3 prev = band.At(0f, 0f);
            for (int i = 1; i <= steps; i++)
            {
                float u = 2f * Mathf.PI * i / steps;
                var n = band.Normal(u, 0f);
                var flat = band.At(u, 0f);
                float swell = RollAmp * (N01(flat.x * 0.007f, flat.y * 0.007f, flat.z * 0.007f, 4) - 0.5f);
                var p = flat + n * swell;
                Emit(p, SpawnPoint.LookRotation(p - prev, n),
                    new Vector3(3.4f, 1.1f, 3.8f), i % 7 == 0 ? spec.Field : spec.Ground);
                prev = p;
            }
        }

        // =====================================================================
        //  Between the bands
        // =====================================================================

        /// <summary>Motes drifting through the voids the three bands leave. Sparse, and deliberately
        /// unaligned to anything - in a cell with no up there is no horizon for them to settle
        /// toward.</summary>
        void BuildDrift()
        {
            int n = Scaled(4400);
            float inner = NucleusR + 30f;
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 11) < 0.28f) continue;
                float y = 1f - 2f * (i + 0.5f) / n;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float a = i * GoldenAngle;
                var dir = new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
                Emit(dir * (inner + 560f * Hash01(i * 7 + _noiseSeed)),
                    Quaternion.Euler(Hash01(i * 5) * 360f, Hash01(i * 13) * 360f, Hash01(i * 3) * 360f),
                    new Vector3(1.9f, 0.9f, 2.6f), i % 5 == 0 ? Domains.Gold : Domains.Jade);
            }
        }
    }
}
