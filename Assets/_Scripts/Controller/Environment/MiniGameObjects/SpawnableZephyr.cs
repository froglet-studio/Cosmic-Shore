using System;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Zephyr" - the painted-sky cell environment (~33k prisms): pure weather, nothing
    /// built. Braided wind-rivers circle at three altitudes; two counter-chiral cyclones
    /// climb and sink; lenticular cloud banks drift (one thunderhead hides danger lightning
    /// bolts over a rain veil); a radiating sun disc and moon disc swirl Van-Gogh-style; a
    /// rolling swell sea anchors the horizon below. Starry-Night blue + gold, jade wisps.
    /// The flow cell - every family is a line to ride, and the only mass is cloud.
    ///
    /// <b>The lines are laid FLUSH</b> (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10): every chain prism - a wind-river filament, a cyclone arm, a lightning bolt - is as
    /// long as the room its neighbours on the line leave it, and where a line or a cloud meets a
    /// cyclone arm or the swell it yields on the one axis that keeps the most of it. The prism
    /// count, the positions and the draw stream are the authored ones (see "Flush fitting" at the
    /// bottom of the file, and its note on what is NOT flush yet: the scattered families).
    /// </summary>
    public class SpawnableZephyr : CellEnvironmentSpawnableBase
    {
        const float Sea = -170f;

        protected override int DefaultSeed => 41;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableZephyr), 4);

        protected override void BuildEnvironment()
        {
            BuildArms();
            BuildWindRivers();
            BuildCyclones();
            BuildClouds();
            BuildDiscs();
            BuildSwellSea();
            _arm = null;
        }

        /// <summary>Every cyclone arm prism's pose and fitted size, worked out once per
        /// generation: the arms are laid from it, and a line or a cloud that meets an arm reads
        /// it by index instead of re-deriving it.</summary>
        Box[] _arm;

        readonly struct Box
        {
            public readonly Vector3 P;
            public readonly Quaternion R;
            public readonly Pose A;
            public readonly Vector3 S;
            public Box(Vector3 p, Quaternion r, Vector3 s) { P = p; R = r; A = new Pose(r); S = s; }
        }

        void BuildArms()
        {
            _arm = new Box[Cyclones.Length * CycloneArms * ArmPrisms];
            for (int c = 0; c < Cyclones.Length; c++)
                for (int arm = 0; arm < CycloneArms; arm++)
                    for (int i = 0; i < ArmPrisms; i++)
                        _arm[(c * CycloneArms + arm) * ArmPrisms + i] = new Box(ArmPos(c, arm, i), ArmRot(c, arm, i), ArmFit(c, arm, i));
        }

        static readonly Vector3 RiverSize = new(1.8f, 0.8f, 4.8f);
        const float RiverStep = 5.4f;

        /// <summary>The streamline direction a wind river steps along from <paramref name="p"/>.</summary>
        Vector3 RiverFlow(Vector3 p, float alt, int layer)
        {
            var v = Curl(p, 0.0052f, layer * 7);
            var orbit = new Vector3(-p.z, 0f, p.x).normalized;
            var toCentre = new Vector3(-p.x, 0f, -p.z).normalized;
            var d = (v * 0.9f + orbit * 0.75f + toCentre * 0.22f).normalized;
            d = new Vector3(d.x, d.y * 0.35f, d.z).normalized; // keep layers flat-ish
            if ((p.y + d.y * RiverStep < alt - 45f && d.y < 0f) || (p.y + d.y * RiverStep > alt + 45f && d.y > 0f))
                d = new Vector3(d.x, -d.y, d.z);
            return d;
        }

        /// <summary>Braid: four filaments coiling in the STREAMLINE frame (a true helix around the
        /// tangent at every heading - a world-frame offset collapses the coil whenever the flow
        /// runs along +/-x).</summary>
        static Vector3 Filament(Vector3 p, in Pose flow, int i, int f)
        {
            float fa = i * 0.35f + f * 1.5708f;
            return p + flow.X * (Mathf.Cos(fa) * 3.2f) + flow.Y * (Mathf.Sin(fa) * 2.2f);
        }

        static bool InSky(Vector3 p) => Mathf.Sqrt(p.x * p.x + p.z * p.z) <= 470f;

        void BuildWindRivers()
        {
            var layers = new (float alt, Domains dom1, Domains dom2)[]
            {
                (-60f, Domains.Blue, Domains.Jade),
                (40f, Domains.Blue, Domains.Gold),
                (150f, Domains.Gold, Domains.Blue),
            };
            int rivers = Scaled(10);
            Span<float> room = stackalloc float[4];
            for (int layer = 0; layer < layers.Length; layer++)
            {
                var (alt, dom1, dom2) = layers[layer];
                for (int r = 0; r < rivers; r++)
                {
                    int sidx = layer * rivers + r;
                    float a = sidx * GoldenAngle * 2.9f;
                    var p = new Vector3(430f * Mathf.Cos(a), alt + 30f * Hash01(sidx * 29 + _noiseSeed) - 15f, 430f * Mathf.Sin(a));
                    // The streamline is integrated one step AHEAD, so every filament prism is as
                    // long as the room the next one on its own filament leaves it (a bend, or the
                    // helix carrying the filament across the flow, shortens it; a straight run
                    // keeps the authored 4.8 against the 5.4 step).
                    var d = RiverFlow(p, alt, layer);
                    for (int f = 0; f < 4; f++) room[f] = RiverSize.z;
                    for (int i = 0; i < 280; i++)
                    {
                        p += d * RiverStep;
                        if (!InSky(p)) break;
                        var flowRot = SpawnPoint.LookRotation(d, Vector3.up);
                        var dn = RiverFlow(p, alt, layer);
                        var pn = p + dn * RiverStep;
                        bool more = i < 279 && InSky(pn);
                        var nextRot = SpawnPoint.LookRotation(dn, Vector3.up);
                        Pose flow = flowRot, nextFlow = nextRot;
                        for (int f = 0; f < 4; f++)
                        {
                            var q = Filament(p, flow, i, f);
                            float next = more
                                ? ChainRoom(q, flow, RiverSize, Filament(pn, nextFlow, i + 1, f), nextFlow, RiverSize)
                                : RiverSize.z;
                            var size = RiverSize;
                            size.z = Mathf.Min(size.z, Mathf.Min(room[f], next));
                            room[f] = next;
                            ClearOfCyclones(ref size, ref q, flow);
                            Emit(q, flowRot, size, (i / 9 + f) % 3 != 0 ? dom1 : dom2);
                        }
                        d = dn;
                    }
                }
            }
        }

        static readonly (Vector3 cx, float chir, float yTip, float yMouth)[] Cyclones =
        {
            // Anchored: one waterspout whose tip skims the swell and whose mouth opens at cloud
            // base (the missing sea-to-sky climb line), one funnel hung inverted from the cloud
            // deck, sinking. Quadratic lerp hugs the anchor before climbing.
            (new Vector3(190f, 0f, -140f), 1f, Sea + 18f, 120f),
            (new Vector3(-200f, 0f, 150f), -1f, 170f, -40f),
        };
        const int CycloneArms = 11;
        const int ArmPrisms = 210;
        static readonly Vector3 ArmSize = new(1.4f, 0.8f, 4.4f);

        static Vector3 ArmPos(int c, int arm, int i)
        {
            var (cx, chir, yTip, yMouth) = Cyclones[c];
            float t = i / (ArmPrisms - 1f);
            float r = 8f + 150f * t;
            float a = 2f * Mathf.PI * arm / CycloneArms + chir * t * 3.4f * Mathf.PI;
            return new Vector3(cx.x + r * Mathf.Cos(a), Mathf.Lerp(yTip, yMouth, t * t), cx.z + r * Mathf.Sin(a));
        }

        static Quaternion ArmRot(int c, int arm, int i)
        {
            if (i > 0) return SpawnPoint.LookRotation(ArmPos(c, arm, i) - ArmPos(c, arm, i - 1), Vector3.up);
            float a = 2f * Mathf.PI * arm / CycloneArms;
            return SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
        }

        /// <summary>An arm prism is as long as the room its neighbours on the arm leave it: near
        /// the tip the arm steps under 1 (it winds tight and climbs slowly), at the mouth ~8.</summary>
        static Vector3 ArmFit(int c, int arm, int i)
        {
            var size = ArmSize;
            var p = ArmPos(c, arm, i);
            var rot = ArmRot(c, arm, i);
            // The tip prism lies ACROSS the arm (it faces along the spiral, the rest along the
            // climb), so a length budget does not describe it: it is cut to touch the next one.
            if (i == 0) return Clear(size, p, rot, ArmPos(c, arm, 1), ArmRot(c, arm, 1), ArmFit(c, arm, 1));
            size.z = Mathf.Min(size.z, ChainRoom(ArmPos(c, arm, i - 1), ArmRot(c, arm, i - 1), ArmSize, p, rot, ArmSize));
            if (i < ArmPrisms - 1) size.z = Mathf.Min(size.z, ChainRoom(p, rot, ArmSize, ArmPos(c, arm, i + 1), ArmRot(c, arm, i + 1), ArmSize));
            size.z = Mathf.Max(size.z, MinAxis);   // what the lay will grow it to
            return size;
        }

        void BuildCyclones()
        {
            for (int c = 0; c < Cyclones.Length; c++)
                for (int arm = 0; arm < CycloneArms; arm++)
                    for (int i = 0; i < ArmPrisms; i++)
                    {
                        ref readonly Box b = ref _arm[(c * CycloneArms + arm) * ArmPrisms + i];
                        Emit(b.P, b.R, b.S, arm % 2 != 0 ? Domains.Blue : Domains.Gold);
                    }
        }

        /// <summary>Fit a box against the cyclone arms near it: the arm prisms at the heights and
        /// radii it could touch, on the two arms either side of its bearing.</summary>
        void ClearOfCyclones(ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            float reach = 0.5f * (size.magnitude + ArmSize.magnitude);
            for (int c = 0; c < Cyclones.Length; c++)
            {
                var (cx, chir, yTip, yMouth) = Cyclones[c];
                float dx = pos.x - cx.x, dz = pos.z - cx.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist > 158f + reach || pos.y < Mathf.Min(yTip, yMouth) - reach || pos.y > Mathf.Max(yTip, yMouth) + reach)
                    continue;
                // Every arm sits at radius 8 + 150 t and height lerp(tip, mouth, t^2): a box can only
                // meet arm prisms whose t satisfies both, on the arms nearest its bearing.
                float tLo = Mathf.Max(0f, (dist - reach - 8f) / 150f), tHi = Mathf.Min(1f, (dist + reach - 8f) / 150f);
                float y0 = (pos.y - reach - yTip) / (yMouth - yTip), y1 = (pos.y + reach - yTip) / (yMouth - yTip);
                float sLo = Mathf.Sqrt(Mathf.Clamp01(Mathf.Min(y0, y1))), sHi = Mathf.Sqrt(Mathf.Clamp01(Mathf.Max(y0, y1)));
                tLo = Mathf.Max(tLo, sLo); tHi = Mathf.Min(tHi, sHi);
                if (tLo > tHi) continue;
                float theta = Mathf.Atan2(dz, dx);
                int iLo = Mathf.FloorToInt(tLo * (ArmPrisms - 1f)), iHi = Mathf.CeilToInt(tHi * (ArmPrisms - 1f));
                for (int i = iLo; i <= iHi; i++)
                {
                    float t = i / (ArmPrisms - 1f);
                    int arm0 = Mathf.RoundToInt((theta - chir * t * 3.4f * Mathf.PI) / (2f * Mathf.PI) * CycloneArms);
                    for (int da = -1; da <= 1; da++)
                    {
                        int arm = ((arm0 + da) % CycloneArms + CycloneArms) % CycloneArms;
                        ref readonly Box b = ref _arm[(c * CycloneArms + arm) * ArmPrisms + i];
                        if ((b.P - pos).sqrMagnitude > reach * reach) continue;
                        Meet(ref size, ref pos, rot, b.P, b.A, b.S);
                    }
                }
            }
        }

        void BuildClouds()
        {
            int banks = 11;
            int m = Scaled(1150);
            for (int k = 0; k < banks; k++)
            {
                float a = k * GoldenAngle * 2.3f;
                float rr = 120f + 230f * Hash01(k * 67 + _noiseSeed);
                float cy = 60f + 130f * Hash01(k * 31);
                var cx = new Vector3(rr * Mathf.Cos(a), cy, rr * Mathf.Sin(a));
                float ex = 46f + 22f * Hash01(k * 7), ey = 13f + 6f * Hash01(k * 11), ez = 34f + 18f * Hash01(k * 13);
                for (int i = 0; i < m; i++)
                {
                    float u = Hash01(k * 997 + i * 7), v = Hash01(k * 991 + i * 11), w = Hash01(k * 983 + i * 13);
                    var p = new Vector3((u * 2f - 1f) * ex, (v * 2f - 1f) * ey, (w * 2f - 1f) * ez);
                    if (p.x / ex * (p.x / ex) + p.y / ey * (p.y / ey) + p.z / ez * (p.z / ez) > 1f) continue;
                    var rot = SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                    var pos = cx + p;
                    var size = Jit(new Vector3(4.4f, 1.6f, 3.2f), 0.3f);
                    ClearOfCyclones(ref size, ref pos, rot);
                    Emit(pos, rot, size, k == 3 && Hash01(i * 3) < 0.06f ? Domains.Ruby : Domains.Blue);
                }

                // The thunderhead: lightning bolts and a rain veil beneath.
                if (k == 3)
                {
                    for (int bolt = 0; bolt < 3; bolt++)
                    {
                        // Chained, hard-kinked, and striking the sea - unmistakably lightning
                        // against the strictly-vertical rain around it (same prism volume).
                        float startY = cx.y - ey - 4f;
                        float drop = (startY - (Sea + 12f)) / 26f;
                        var p = cx + new Vector3(20f * bolt - 20f, -ey - 4f, 10f * bolt - 10f);
                        for (int i = 0; i < 26; i++)
                        {
                            float kink = i % 3 == 0 ? 2.2f : 1f;
                            var step = new Vector3(
                                8f * (Hash01(bolt * 97 + i * 3) - 0.5f) * kink, -drop,
                                8f * (Hash01(bolt * 89 + i * 7) - 0.5f) * kink);
                            p += step;
                            // A bolt link that strikes the swell yields to it.
                            var rot = SpawnPoint.LookRotation(step, Vector3.up);
                            var size = new Vector3(1f, 1f, 3.2f);
                            var at = p;
                            ClearOfSwell(ref size, ref at, rot);
                            Emit(at, rot, size, Domains.Ruby, PrismKind.Danger);
                        }
                    }
                    for (int rv = 0; rv < RainColumns; rv++)
                        for (int i = 0; i < RainDrops; i++)
                            Emit(RainPos(k, rv, i, cx, ex, ey, ez), Quaternion.identity, RainSize, Domains.Blue);
                }
            }
        }

        const int RainColumns = 100;
        const int RainDrops = 9;
        static readonly Vector3 RainSize = new(0.7f, 2.8f, 0.7f);

        static Vector3 RainPos(int k, int rv, int i, Vector3 cx, float ex, float ey, float ez)
        {
            float rx = Hash01(k * 555 + rv * 3) * 2f - 1f, rz = Hash01(k * 551 + rv * 7) * 2f - 1f;
            return cx + new Vector3(rx * ex * 0.8f, -ey - 2f - i * 4.2f, rz * ez * 0.8f);
        }


        void BuildDiscs()
        {
            var defs = new (Vector3 dc, Domains dom, float rad)[]
            {
                (new Vector3(240f, 210f, 240f), Domains.Gold, 52f),
                (new Vector3(-260f, 190f, -230f), Domains.Blue, 40f),
            };
            for (int disc = 0; disc < defs.Length; disc++)
            {
                var (dc, dom, rad) = defs[disc];
                int body = (int)(rad * rad / 7f);
                for (int i = 0; i < body; i++)
                {
                    float u = Hash01(disc * 771 + i * 3);
                    float a = i * GoldenAngle;
                    float rr = rad * Mathf.Sqrt(u);
                    Emit(dc + new Vector3(rr * Mathf.Cos(a), 0.12f * rr * Mathf.Sin(a * 3f), rr * Mathf.Sin(a)),
                        SpawnPoint.LookRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))),
                        new Vector3(2.6f, 0.6f, 1.8f), dom);
                }
                Emit(dc + new Vector3(0f, 3f, 0f), Quaternion.identity, new Vector3(2.6f, 2.6f, 2.6f), dom, PrismKind.Shielded);
                for (int ray = 0; ray < 16; ray++)
                {
                    float a = 2f * Mathf.PI * ray / 16f;
                    for (int i = 0; i < 12; i++)
                    {
                        float rr = rad + 6f + i * 4.4f;
                        float aa = a + i * 0.14f; // swirl
                        var outward = new Vector3(Mathf.Cos(aa), 0f, Mathf.Sin(aa));
                        var tangent = new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa));
                        // Long axis chains along the actual spiral stroke (radial step 4.4 +
                        // tangential step rr*0.14) - a curling brushstroke, not herringbone.
                        var chainDir = (outward * 4.4f + tangent * (rr * 0.14f)).normalized;
                        Emit(dc + new Vector3(rr * outward.x, 3f * Mathf.Sin(i * 0.9f + ray), rr * outward.z),
                            SpawnPoint.LookRotation(chainDir, Vector3.up), new Vector3(1.2f, 0.7f, 3.8f), dom);
                    }
                }
            }
        }

        const int SwellRows = 30;
        static readonly Vector3 SwellSize = new(3.6f, 5.8f, 0.9f);

        static float SwellR(int row) => 60f + row * 13.5f;
        static int SwellCount(int row) => (int)(2f * Mathf.PI * SwellR(row) / 7.4f);

        Vector3 SwellPos(int row, int i, out Quaternion rot, out bool laid)
        {
            float R = SwellR(row);
            int n = SwellCount(row);
            float a = 2f * Mathf.PI * i / n;
            float y = Sea + 11f * Mathf.Sin(a * 4f + row * 0.9f) + 5f * N01(Mathf.Cos(a) * 4f, row * 0.5f, Mathf.Sin(a) * 4f, 17);
            laid = N01(a * 3.1f, row * 0.7f, 8.5f, 18) >= 0.30f;
            rot = SpawnPoint.LookRotation(Vector3.up, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
            return new Vector3(R * Mathf.Cos(a), y, R * Mathf.Sin(a));
        }

        void BuildSwellSea()
        {
            for (int row = 0; row < SwellRows; row++)
            {
                int n = SwellCount(row);
                for (int i = 0; i < n; i++)
                {
                    var pos = SwellPos(row, i, out var rot, out bool laid);
                    if (!laid) continue;
                    Emit(pos, rot, Jit(SwellSize, 0.25f), row % 3 != 0 ? Domains.Blue : Domains.Jade);
                }
            }
        }

        /// <summary>Fit a box against the swell plates near it (the rows and positions it could
        /// reach), at the jitter's largest.</summary>
        void ClearOfSwell(ref Vector3 size, ref Vector3 pos, in Pose rot)
        {
            if (pos.y > Sea + 16f + 5f + size.magnitude) return;
            float pr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            float pa = Mathf.Atan2(pos.z, pos.x);
            int r0 = Mathf.RoundToInt((pr - 60f) / 13.5f);
            for (int row = Mathf.Max(0, r0 - 1); row <= Mathf.Min(SwellRows - 1, r0 + 1); row++)
            {
                int n = SwellCount(row);
                int i0 = Mathf.RoundToInt(pa / (2f * Mathf.PI) * n);
                for (int di = -2; di <= 2; di++)
                {
                    var sp = SwellPos(row, ((i0 + di) % n + n) % n, out var srot, out bool laid);
                    if (laid) Meet(ref size, ref pos, rot, sp, srot, SwellSize * 1.25f);
                }
            }
            return;
        }

        // ── Flush fitting (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10) ──
        //
        // Every chain size above is FITTED at authoring time from geometry the generator already
        // has, and a box that meets a cyclone arm, the swell or the rain veil yields on the one
        // axis that keeps the most of it. Nothing searches the lay list - each neighbour is
        // recomputed from its own index - so the prism count, positions and draw stream are the
        // authored ones.
        //
        // NOT flush yet, and not fixable by a size: the SCATTERED families - the cloud banks (a
        // hash scatter, ~600 plates to an ellipsoid with no spacing law), the sun and moon disc
        // bodies (golden-angle bearings at hashed radii) and the rain columns (hashed positions) -
        // put pairs of prisms arbitrarily close together, so no per-prism size short of a few
        // percent of the authored one clears them; and two wind rivers of one layer converge onto
        // the same streamlines (the flow is not divergence-free once it is flattened and pulled
        // to the centre). Each needs either a re-placement of its positions or a search of its
        // neighbours, which is a decision for the owner, not a fit.

        /// <summary>A fitted size is this fraction of the exact contact size, so float noise never
        /// reads a touching pair as a clip.</summary>
        const float Flush = 0.995f;

        /// <summary>The prism prefab's per-axis scale floor: a fitted size under it is grown back
        /// to it by the lay, so a fit that needs less is not a fit.</summary>
        const float MinAxis = 0.5f;

        /// <summary>A rotation's three axes: what every box test reads, so they are worked out once
        /// per prism rather than once per test.</summary>
        readonly struct Pose
        {
            public readonly Vector3 X, Y, Z;
            public Pose(Quaternion r) { X = r * Vector3.right; Y = r * Vector3.up; Z = r * Vector3.forward; }
            public static implicit operator Pose(Quaternion r) => new(r);
            public Vector3 this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;
        }

        /// <summary>
        /// The budget two neighbouring prisms share along their local <paramref name="axis"/>
        /// (2 = z, a chain's length; 1 = y, a ring's tangential run): the largest
        /// (La + Lb) / 2 at which they still only touch, given each one's other two sizes.
        /// Either prism's axis may separate them, so it is the better of the two - each less the
        /// other prism's girth projected onto it, which is what a bend costs.
        /// </summary>
        static float Room(Vector3 pa, in Pose ra, Vector3 sa, Vector3 pb, in Pose rb, Vector3 sb, int axis = 2)
        {
            Vector3 d = pb - pa, za = ra[axis], zb = rb[axis];
            float onA = Mathf.Abs(Vector3.Dot(d, za)), onB = Mathf.Abs(Vector3.Dot(d, zb));
            for (int m = 0; m < 3; m++)
            {
                if (m == axis) continue;
                onA -= 0.5f * Comp(sb, m) * Mathf.Abs(Vector3.Dot(rb[m], za));
                onB -= 0.5f * Comp(sa, m) * Mathf.Abs(Vector3.Dot(ra[m], zb));
            }
            return Mathf.Max(onA, onB) * Flush;
        }

        static float Comp(Vector3 v, int axis) => axis == 0 ? v.x : axis == 1 ? v.y : v.z;

        static float Reach(Vector3 l, in Pose a, Vector3 h) =>
            Mathf.Abs(Vector3.Dot(a.X, l)) * h.x + Mathf.Abs(Vector3.Dot(a.Y, l)) * h.y + Mathf.Abs(Vector3.Dot(a.Z, l)) * h.z;

        /// <summary>The 15 separating axes of two boxes (the 6 faces, then the edge pairs that are
        /// not degenerate); returns how many.</summary>
        static int Axes(in Pose a, in Pose b, Span<Vector3> axes)
        {
            axes[0] = a.X; axes[1] = a.Y; axes[2] = a.Z; axes[3] = b.X; axes[4] = b.Y; axes[5] = b.Z;
            int n = 6;
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var c = Vector3.Cross(a[i], b[j]);
                    float m = c.magnitude;
                    if (m > 1e-4f) axes[n++] = c / m;
                }
            return n;
        }

        /// <summary>
        /// Shrink box (<paramref name="size"/>, <paramref name="p"/>, <paramref name="a"/>) until
        /// it only touches the fixed box (<paramref name="os"/>, <paramref name="op"/>,
        /// <paramref name="b"/>): on the ONE axis, or uniformly, whichever keeps the most volume -
        /// or, when no one axis reaches, one axis to the floor and a second cut (separating-axis
        /// test, exact on the 15 axes). Already clear: unchanged. <paramref name="fits"/> is false
        /// when nothing at all fits (a centre laid inside the other box): that prism must move.
        /// </summary>
        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os) =>
            Clear(size, p, a, op, b, os, out _, true);

        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, out bool fits) =>
            Clear(size, p, a, op, b, os, out fits, true);

        static Vector3 Clear(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, out bool fits, bool pairs, int locked = -1)
        {
            fits = true;
            Vector3 d = op - p;
            float reach = 0.5f * (size.magnitude + os.magnitude);
            if (d.sqrMagnitude >= reach * reach) return size;
            Vector3 h = 0.5f * size, oh = 0.5f * os;
            // Cheap first: one of the six face axes separates most pairs that get this far.
            if (Apart(d, a.X, a, h, b, oh) || Apart(d, a.Y, a, h, b, oh) || Apart(d, a.Z, a, h, b, oh) ||
                Apart(d, b.X, a, h, b, oh) || Apart(d, b.Y, a, h, b, oh) || Apart(d, b.Z, a, h, b, oh))
                return size;

            Span<Vector3> axes = stackalloc Vector3[15];
            int n = Axes(a, b, axes);
            float bestRatio = 0f, uniform = 0f;
            Span<float> best = stackalloc float[3] { 0f, 0f, 0f };
            for (int s = 0; s < n; s++)
            {
                var l = axes[s];
                float gap = Mathf.Abs(Vector3.Dot(d, l)) - Reach(l, b, oh);
                float own = Reach(l, a, h);
                if (gap >= own) return size;                       // already only touching
                if (gap <= 0f) continue;
                uniform = Mathf.Max(uniform, gap / own);
                for (int k = 0; k < 3; k++)
                {
                    float c = Mathf.Abs(Vector3.Dot(a[k], l));
                    if (c < 1e-4f) continue;
                    best[k] = Mathf.Max(best[k], (gap - (own - Comp(h, k) * c)) / c);
                }
            }

            // Pick the fit that keeps the most: one axis cut to its bound, or a uniform shrink.
            int pick = -1;
            for (int k = 0; k < 3; k++)
            {
                if (k == locked || 2f * best[k] * Flush < MinAxis) continue;
                float ratio = best[k] / Comp(h, k);
                if (ratio > bestRatio) { bestRatio = ratio; pick = k; }
            }
            float u = uniform * Flush;
            if (locked < 0 && u * u * u > bestRatio && Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * u >= MinAxis)
                return size * u;
            if (pick < 0)
            {
                // No one axis reaches: take one axis to the floor and cut a second, keeping the
                // pair that leaves the most.
                fits = false;
                if (!pairs) return size;
                Vector3 keep = size;
                float kept = 0f;
                for (int k = 0; k < 3; k++)
                {
                    if (k == locked) continue;
                    var floor = size;
                    if (k == 0) floor.x = MinAxis; else if (k == 1) floor.y = MinAxis; else floor.z = MinAxis;
                    var cut2 = Clear(floor, p, a, op, b, os, out bool ok, false, locked);
                    float vol = cut2.x * cut2.y * cut2.z;
                    if (ok && vol > kept) { kept = vol; keep = cut2; fits = true; }
                }
                return keep;
            }
            float cut = 2f * best[pick] * Flush;
            if (pick == 0) size.x = cut; else if (pick == 1) size.y = cut; else size.z = cut;
            return size;
        }

        static bool Apart(Vector3 d, Vector3 l, in Pose a, Vector3 h, in Pose b, Vector3 oh) =>
            Mathf.Abs(Vector3.Dot(d, l)) >= Reach(l, a, h) + Reach(l, b, oh);

        /// <summary>
        /// The smallest move that takes box A clear of box B at its full size: the
        /// separating-axis overlap along the axis where it is least, skipping any axis that runs
        /// within ~45 degrees of <paramref name="keep"/> (zero: none skipped).
        /// </summary>
        static Vector3 Escape(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, Vector3 keep)
        {
            Vector3 d = op - p, h = 0.5f * size, oh = 0.5f * os;
            Span<Vector3> axes = stackalloc Vector3[15];
            int n = Axes(a, b, axes);
            float least = float.MaxValue;
            Vector3 move = Vector3.zero;
            for (int s = 0; s < n; s++)
            {
                var l = axes[s];
                float overlap = Reach(l, a, h) + Reach(l, b, oh) - Mathf.Abs(Vector3.Dot(d, l));
                if (overlap <= 0f) return Vector3.zero;               // already clear
                if (keep != Vector3.zero && Mathf.Abs(Vector3.Dot(l, keep)) > 0.7f) continue;
                if (overlap < least)
                {
                    least = overlap;
                    move = l * (-(Vector3.Dot(d, l) >= 0f ? 1f : -1f) * (overlap / Flush + 1e-3f));
                }
            }
            return move;
        }

        static float ChainRoom(Vector3 pa, in Pose ra, Vector3 sa, Vector3 pb, in Pose rb, Vector3 sb) =>
            Room(pa, ra, sa, pb, rb, sb, 2);

        /// <summary>One prism meeting one fixed obstacle: cut to touch it, or - when its centre lies
        /// so deep in the obstacle that no cut clears - moved by the least that does.</summary>
        static void Meet(ref Vector3 size, ref Vector3 pos, in Pose rot, Vector3 op, in Pose orot, Vector3 os)
        {
            var cut = Clear(size, pos, rot, op, orot, os, out bool fits);
            if (fits) size = cut;
            else pos += Escape(size, pos, rot, op, orot, os, Vector3.zero);
        }
    }
}
