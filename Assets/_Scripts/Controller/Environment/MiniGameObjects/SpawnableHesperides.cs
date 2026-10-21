using System;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Hesperides" - the GARDEN cell (~12k authored prisms). The freestyle seven are worlds you
    /// fly through; this one is a world that <b>grows</b>. It authors only the garden's
    /// architecture - a five-terrace bowl of planting beds inside a walled rim, radial pergola
    /// arcades, trellis towers, an aqueduct ring feeding cascades into a central pool, a ring of
    /// hanging baskets, a vine-dome of meridian ribs, a super-shielded orchard gate, shielded
    /// fruit lanterns, and two bramble arcs of true danger prisms - and then <b>sows planting
    /// sites</b> (<see cref="FloraPlantingSite"/>) all over it: along every terrace, at every
    /// pergola foot and trellis foot, inside every hanging basket (normal pointing DOWN, so what
    /// roots there trails), and around the pool rim. Each site is TAGGED with its ground kind
    /// (<see cref="FloraSiteKind"/>) so species plant where they belong - reeds at the water,
    /// climbers at the column feet, bells in the baskets.
    ///
    /// The Cell's ordinary flora spawner plants into those sites through the ordinary spawn path,
    /// so the garden's canopy is made of LIVING flora - grazeable, joustable, starvable,
    /// crystal-dropping food-web citizens - not laid scenery. Authored bones (~12k) plus a
    /// mature planting (~21k) is the ~34k of <see cref="SpawnableYggdra"/>, but reached by growth
    /// instead of by lay: the cell arrives sparse and fills, and it stays full only while the food
    /// web lets it. Nothing here is on a clock; nothing decays. A garden the fauna strip back is
    /// a correct outcome, and the beds are still prepared ground when the pressure lifts.
    ///
    /// <b>Every prism is laid FLUSH</b> (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10): a chain's prisms are as long as the room their neighbours leave, a jittered size is
    /// clamped only on the axis that touches, and where two structures cross the later one yields
    /// on the one axis that keeps the most of it - all from geometry recomputed from indices, so the
    /// prism count, the draw stream and every planting site are the authored ones (see "Flush
    /// fitting" at the bottom of the file).
    /// </summary>
    public class SpawnableHesperides : CellEnvironmentSpawnableBase
    {
        const float RimR = 420f;          // outer wall radius
        const float Floor = -150f;        // pool surface
        const float Rim = 120f;           // wall crown height
        const int Terraces = 5;

        protected override int DefaultSeed => 137;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableHesperides), 2);
        protected override int LayCapacity => 18000;

        /// <summary>Terrace i's ring radius - the bowl steps outward and upward from the pool.</summary>
        static float TerraceR(int i) => 120f + i * 62f;

        /// <summary>Terrace i's bed height - the bowl rises toward the rim.</summary>
        static float TerraceY(int i) => Floor + 22f + i * 34f;

        protected override void BuildEnvironment()
        {
            BuildCaches();
            Terracing();
            OuterWall();
            Pergolas();
            TrellisTowers();
            Aqueduct();
            Pool();
            HangingBaskets();
            VineDome();
            OrchardGate();
            Brambles();
            Pollen();
            _arch = _terrace = _upright = _rung = _fall = _bramble = _gate = null;
            _lantern = null;
        }

        // ── Fitted geometry, computed once ───────────────────────────────────
        //
        // Every structure another family yields to has its pose and fitted (bound) size worked out
        // ONCE per generation, from its indices, into these arrays - so a yielding prism reads its
        // neighbours by index instead of re-deriving them, and the whole fit costs a few box tests
        // per prism. They live only while BuildEnvironment runs.

        readonly struct Box
        {
            public readonly Vector3 P;
            public readonly Quaternion R;
            public readonly Pose A;     // R's axes, worked out once
            public readonly Vector3 S;
            public Box(Vector3 p, Quaternion r, Vector3 s) { P = p; R = r; A = new Pose(r); S = s; }
        }

        static readonly Pose Identity = new(Quaternion.identity);

        Box[] _arch, _terrace, _upright, _rung, _fall, _bramble, _gate;
        Vector3[] _lantern;
        int[] _terraceAt;

        void BuildCaches()
        {
            _arch = new Box[Arcades * Bays * ArchPrisms];
            for (int p = 0; p < Arcades; p++)
                for (int bay = 0; bay < Bays; bay++)
                    for (int i = 0; i < ArchPrisms; i++)
                    {
                        var g = ArchSize * JitMax;
                        g.z = ComputeArchLength(p, bay, i);
                        _arch[(p * Bays + bay) * ArchPrisms + i] = new Box(ArchPos(p, bay, i, out var r), r, g);
                    }
            _lantern = new Vector3[Arcades * Bays];
            for (int p = 0; p < Arcades; p++)
                for (int bay = 0; bay < Bays; bay += 2)
                    _lantern[p * Bays + bay] = ComputeLanternFit(p, bay);

            _terraceAt = new int[Terraces + 1];
            for (int t = 0; t < Terraces; t++) _terraceAt[t + 1] = _terraceAt[t] + TerraceCount(t) * 4;
            _terrace = new Box[_terraceAt[Terraces]];
            for (int t = 0; t < Terraces; t++)
                for (int i = 0; i < TerraceCount(t); i++)
                    for (int c = -1; c <= 2; c++)
                    {
                        var pos = ComputeTerracePos(t, i, c, out var rot);
                        _terrace[_terraceAt[t] + i * 4 + c + 1] = new Box(pos, rot, (c == 2 ? KerbSize : BedSize) * JitMax);
                    }

            _towerFoot = new Vector3[Towers]; _towerA = new float[Towers]; _towerH = new float[Towers];
            for (int k = 0; k < Towers; k++) ComputeTower(k, out _towerFoot[k], out _towerA[k], out _towerH[k]);
            _upright = new Box[Towers * 4 * UprightPrisms];
            _rung = new Box[Towers * Rungs * 8];
            for (int k = 0; k < Towers; k++)
            {
                Tower(k, out var foot, out float a, out float height);
                for (int u = 0; u < 4; u++)
                    for (int i = 0; i < UprightPrisms; i++)
                        _upright[(k * 4 + u) * UprightPrisms + i] =
                            new Box(ComputeUprightPos(foot, a, height, u, i), Quaternion.identity, UprightBound(height));
                for (int rung = 0; rung < Rungs; rung++)
                    for (int i = 0; i < 8; i++)
                        _rung[(k * Rungs + rung) * 8 + i] = new Box(ComputeRungPos(foot, height, rung, i, out var rr), rr, RungSize * JitMax);
            }

            _fall = new Box[Falls * FallPrisms];
            for (int f = 0; f < Falls; f++)
                for (int i = 0; i < FallPrisms; i++)
                    _fall[f * FallPrisms + i] = new Box(ComputeFallPos(f, i, out var fr), fr, FallSize * JitMax);

            // Brambles: where each thorn ends up first, then the room its neighbours leave it.
            _bramble = new Box[2 * BramblePrisms];
            for (int arc = 0; arc < 2; arc++)
                for (int i = 0; i < BramblePrisms; i++)
                    _bramble[arc * BramblePrisms + i] = new Box(ComputeBramblePos(arc, i, out var br), br, BrambleSize * JitMax);
            for (int arc = 0; arc < 2; arc++)
                for (int i = 0; i < BramblePrisms; i++)
                {
                    var b = _bramble[arc * BramblePrisms + i];
                    float len = b.S.z;
                    if (i > 0) { var o = _bramble[arc * BramblePrisms + i - 1]; len = Mathf.Min(len, ChainRoom(o.P, o.R, o.S, b.P, b.R, b.S)); }
                    if (i < BramblePrisms - 1) { var o = _bramble[arc * BramblePrisms + i + 1]; len = Mathf.Min(len, ChainRoom(b.P, b.R, b.S, o.P, o.R, o.S)); }
                    _bramble[arc * BramblePrisms + i] = new Box(b.P, b.R, new Vector3(b.S.x, b.S.y, len));
                }

            _gate = new Box[GateArch];
            for (int i = 0; i < GateArch; i++)
                _gate[i] = new Box(GateArchPos(i, out var gr), gr, ComputeGateArchFit(i));
        }

        // ── The beds ──────────────────────────────────────────────────────────

        static readonly Vector3 BedSize = new(6.4f, 1.1f, 6.2f);
        static readonly Vector3 KerbSize = new(3.2f, 5.5f, 6.4f);
        const float CourseStep = 13f;     // radial spacing of a terrace's three bed courses
        const float KerbOut = 24f;        // the retaining kerb's radial offset from the terrace ring

        int TerraceCount(int t) => Scaled(Mathf.RoundToInt(TerraceR(t) * 0.5f));

        /// <summary>Bed course <paramref name="c"/> (-1..1) or, for c = 2, the kerb, of terrace
        /// <paramref name="t"/>'s <paramref name="i"/>th position.</summary>
        Vector3 TerracePos(int t, int i, int c, out Quaternion rot)
        {
            var b = _terrace[_terraceAt[t] + i * 4 + c + 1];
            rot = b.R;
            return b.P;
        }

        Vector3 ComputeTerracePos(int t, int i, int c, out Quaternion rot)
        {
            float r = TerraceR(t), y = TerraceY(t);
            float a = 2f * Mathf.PI * i / TerraceCount(t);
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
            Vector3 pos;
            if (c == 2) pos = new Vector3(radial.x * (r + KerbOut), y + 5f, radial.z * (r + KerbOut));
            else
            {
                float rr = r + c * CourseStep;
                float bump = 2.2f * (N01(rr * 0.02f, y * 0.02f, a, 3) - 0.5f);
                pos = new Vector3(radial.x * rr, y + bump, radial.z * rr);
            }
            // Where a pergola column's foot stands so near a bed's centre that no cut of the bed
            // clears it, the bed slides level (along the band or across it) by the least that does.
            var bound = (c == 2 ? KerbSize : BedSize) * JitMax;
            ClearOfPergolas(ref bound, ref pos, rot, escape: true, keep: Vector3.up);
            return pos;
        }

        /// <summary>
        /// Five concentric terraces: a five-course soil band with a retaining kerb on its outer
        /// edge. This is where most of the planting happens, so the sites are sown here first -
        /// the Cell shuffles them before dealing, so the first seeding batch spreads over the
        /// whole bowl rather than filling one terrace solid.
        /// </summary>
        void Terracing()
        {
            for (int t = 0; t < Terraces; t++)
            {
                float r = TerraceR(t);
                float y = TerraceY(t);
                int count = TerraceCount(t);

                for (int i = 0; i < count; i++)
                {
                    float a = 2f * Mathf.PI * i / count;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));

                    // Three radial courses of bed, chained tangentially so the terrace reads as a
                    // continuous band rather than dotted tiles. Deliberately THIN slabs: the bed
                    // is the stage, and volume is the spine - authored mass that reads as ground
                    // should not eat the headroom the planting is supposed to fill. A pergola
                    // column stands THROUGH the band where an arcade climbs it, so the bed it
                    // stands in yields its footprint to the column.
                    for (int c = -1; c <= 1; c++)
                    {
                        var pos = TerracePos(t, i, c, out var rot);
                        var size = Jit(BedSize);
                        ClearOfPergolas(ref size, ref pos, rot);
                        Emit(pos, rot, size, Domains.Jade);
                    }

                    // Retaining kerb on the outer lip - the readable step between terraces.
                    var kerb = TerracePos(t, i, 2, out var kerbRot);
                    var kerbSize = Jit(KerbSize);
                    ClearOfPergolas(ref kerbSize, ref kerb, kerbRot);
                    Emit(kerb, kerbRot, kerbSize, Domains.Gold);

                    // Sow every other bed position, alternating the inner and outer course so
                    // plantings stagger across the band instead of forming one line.
                    if (i % 2 == 0)
                    {
                        float rr = r + ((i / 2) % 2 == 0 ? -12f : 12f);
                        Sow(new Vector3(radial.x * rr, y + 3f, radial.z * rr), Vector3.up,
                            FloraSiteKind.Bed);
                    }
                }
            }
        }

        /// <summary>Fit a box against the terrace beds and kerbs around it (the nearest terrace
        /// ring, the three positions either side of it in angle).</summary>
        void ClearOfTerraces(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            float reach = 0.5f * size.magnitude;
            float pr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            float pa = Mathf.Atan2(pos.z, pos.x);
            for (int t = 0; t < Terraces; t++)
            {
                float r = TerraceR(t);
                if (pr < r - CourseStep - 5f - reach || pr > r + KerbOut + 3f + reach) continue;
                if (Mathf.Abs(pos.y - (TerraceY(t) + 2.5f)) > 5f + reach) continue;
                int count = TerraceCount(t);
                int i0 = Mathf.RoundToInt(pa / (2f * Mathf.PI) * count);
                for (int di = -1; di <= 1; di++)
                {
                    int i = ((i0 + di) % count + count) % count;
                    for (int c = -1; c <= 2; c++)
                    {
                        var op = TerracePos(t, i, c, out var orot);
                        Meet(ref size, ref pos, rot, escape, keep, along, op, orot, (c == 2 ? KerbSize : BedSize) * JitMax);
                    }
                }
            }
            return;
        }

        /// <summary>The walled rim - the garden is enclosed, which is what makes it a garden. Where
        /// the orchard gate stands IN the wall's line, the bricks it passes through yield to it.</summary>
        void OuterWall()
        {
            int count = Scaled(200);
            for (int i = 0; i < count; i++)
            {
                float a = 2f * Mathf.PI * i / count;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                float baseY = TerraceY(Terraces - 1) + 8f;

                for (int c = 0; c < 8; c++)
                {
                    // Crenellated crown: the top two courses drop out on a slow beat, so the wall
                    // has gaps to fly through instead of being a sealed drum.
                    if (c >= 6 && (i % 9) < 3) continue;
                    float y = baseY + c * (Rim / 8f);
                    var pos = new Vector3(radial.x * RimR, y, radial.z * RimR);
                    var rot = SpawnPoint.LookRotation(tangent, Vector3.up);
                    var size = Jit(new Vector3(2.6f, 4.2f, 6.6f));
                    ClearOfGate(ref size, ref pos, rot);
                    Emit(pos, rot, size, Domains.Jade);
                }
            }
        }

        // ── The built garden ──────────────────────────────────────────────────

        const int Arcades = 12;
        const int Bays = 8;
        const int ArchPrisms = 14;
        const float ColumnPitch = 7.5f;   // a column's courses, centre to centre
        const float ColumnSide = 17f;     // each column's offset either side of the walkway
        const float ArchRise = 13f;       // the arch's vertical semi-axis
        const float CrownUp = 45f;        // the arch crown over the column feet
        static readonly Vector3 ColumnSize = new(2.8f, 7.2f, 2.8f);
        static readonly Vector3 ArchSize = new(2.4f, 2.4f, 6.2f);
        static readonly Vector3 LanternSize = new(4.4f, 4.4f, 4.4f);

        /// <summary>A column course is clamped to its pitch on the one axis the courses touch on
        /// (the jitter keeps its girth): the tallest a course can be and stay flush.</summary>
        static readonly Vector3 ColumnBound = new(ColumnSize.x * JitMax,
            Mathf.Min(ColumnSize.y * JitMax, ColumnPitch * Flush), ColumnSize.z * JitMax);

        /// <summary>Arcade <paramref name="p"/>'s arch tangential semi-axis. The arch used to
        /// spring from the column CENTRELINES (17), so its feet were laid inside the columns' top
        /// courses - a placement no size can clear. It springs from the columns' inner FACES
        /// instead: 17 less the arch's widest half-girth and the column's, measured along the
        /// arcade (a column is square to the WORLD, so on a diagonal arcade its footprint reaches
        /// further along the walkway). That moves each arch prism inward by at most ~3.5 at the
        /// feet and by nothing at the crown.</summary>
        static float ArchHalfSpan(int p)
        {
            Arcade(p, out _, out var tangent);
            float column = ColumnSize.x * (Mathf.Abs(tangent.x) + Mathf.Abs(tangent.z));
            return (ColumnSide - 0.5f * JitMax * (column + ArchSize.x)) * Flush;
        }

        static readonly Vector3[] ArcadeRadial = new Vector3[Arcades], ArcadeTangent = new Vector3[Arcades];

        static SpawnableHesperides()
        {
            for (int p = 0; p < Arcades; p++)
            {
                float a = 2f * Mathf.PI * p / Arcades + 0.13f;
                ArcadeRadial[p] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ArcadeTangent[p] = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            }
        }

        static void Arcade(int p, out Vector3 radial, out Vector3 tangent)
        {
            radial = ArcadeRadial[p];
            tangent = ArcadeTangent[p];
        }

        static float BayR(int bay) => 150f + bay / 7f * 240f;
        static float BayY(int bay) => Floor + 30f + bay / 7f * (TerraceY(Terraces - 1) - Floor - 10f);

        static Vector3 ColumnPos(int p, int bay, int s, int c)
        {
            Arcade(p, out var radial, out var tangent);
            return radial * BayR(bay) + Vector3.up * (BayY(bay) + c * ColumnPitch) + tangent * (s * ColumnSide);
        }

        static Vector3 ArchPos(int p, int bay, int i, out Quaternion rot)
        {
            Arcade(p, out var radial, out var tangent);
            float ang = Mathf.PI * i / (ArchPrisms - 1f);
            Vector3 crown = radial * BayR(bay) + Vector3.up * (BayY(bay) + CrownUp);
            Vector3 along = tangent * -Mathf.Sin(ang) + Vector3.up * Mathf.Cos(ang);
            rot = SpawnPoint.LookRotation(along, radial);
            return crown + tangent * (Mathf.Cos(ang) * ArchHalfSpan(p)) + Vector3.up * (Mathf.Sin(ang) * ArchRise - ArchRise);
        }

        static float ComputeArchLength(int p, int bay, int i)
        {
            var g = ArchSize * JitMax;
            float len = g.z;
            var pos = ArchPos(p, bay, i, out var rot);
            if (i > 0) len = Mathf.Min(len, ChainRoom(ArchPos(p, bay, i - 1, out var r0), r0, g, pos, rot, g));
            if (i < ArchPrisms - 1) len = Mathf.Min(len, ChainRoom(pos, rot, g, ArchPos(p, bay, i + 1, out var r1), r1, g));
            return len;
        }

        static Vector3 LanternPos(int p, int bay) =>
            ColumnPos(p, bay, 0, 0) + Vector3.up * (CrownUp + 2f);

        /// <summary>The lantern hangs at the crown, inside the arch's top prisms: it is as tall as
        /// the gap over them allows (its footprint is kept).</summary>
        Vector3 LanternFit(int p, int bay) => _lantern[p * Bays + bay];

        Vector3 ComputeLanternFit(int p, int bay)
        {
            var size = LanternSize;
            var pos = LanternPos(p, bay);
            for (int i = 0; i < ArchPrisms; i++)
            {
                var b = _arch[(p * Bays + bay) * ArchPrisms + i];
                size = Clear(size, pos, Quaternion.identity, b.P, b.R, b.S);
            }
            return size;
        }

        /// <summary>
        /// Twelve radial pergola arcades climbing the terraces - paired columns carrying a run of
        /// arches you fly under. Every column foot is prepared ground: this is where the climbers
        /// go, and a pergola with a vine on it is the whole point of a pergola.
        /// </summary>
        void Pergolas()
        {
            for (int p = 0; p < Arcades; p++)
            {
                for (int bay = 0; bay < Bays; bay++)
                {
                    // Two columns straddling the walkway.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        for (int c = 0; c < 6; c++)
                        {
                            // The courses touch top to bottom: the jitter keeps its girth but a
                            // course is never taller than its pitch.
                            var size = Jit(ColumnSize);
                            size.y = Mathf.Min(size.y, ColumnBound.y);
                            Emit(ColumnPos(p, bay, s, c), Quaternion.identity, size, Domains.Gold);
                        }

                        // Prepared ground at the column foot, growing straight up the column.
                        Sow(ColumnPos(p, bay, s, 0) + Vector3.up * 2f, Vector3.up, FloraSiteKind.Climb);
                    }

                    // The arch over the bay (fly under it), each prism as long as the curve
                    // leaves it room for.
                    for (int i = 0; i < ArchPrisms; i++)
                    {
                        var b = _arch[(p * Bays + bay) * ArchPrisms + i];
                        var size = Jit(ArchSize);
                        size.z = Mathf.Min(size.z, b.S.z);
                        Emit(b.P, b.R, size, Domains.Gold);
                    }

                    // A shielded fruit lantern hangs from every other arch.
                    if (bay % 2 == 0)
                        Emit(LanternPos(p, bay), Quaternion.identity, LanternFit(p, bay),
                            Domains.Ruby, PrismKind.Shielded);
                }
            }
        }

        /// <summary>Fit a box against the pergola arcade nearest it in bearing - the columns, arch
        /// and lantern of the two bays nearest it in radius.</summary>
        void ClearOfPergolas(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            float reach = 0.5f * size.magnitude;
            float a = Mathf.Atan2(pos.z, pos.x) - 0.13f;
            int p = ((Mathf.RoundToInt(a / (2f * Mathf.PI) * Arcades) % Arcades) + Arcades) % Arcades;
            Arcade(p, out var radial, out var tangent);
            float outward = Vector3.Dot(pos, radial), across = Vector3.Dot(pos, tangent);
            if (Mathf.Abs(across) > ColumnSide + ColumnBound.x + reach) return;
            int b0 = Mathf.RoundToInt((outward - 150f) / (240f / 7f));
            for (int bay = b0 - 1; bay <= b0 + 1; bay++)
            {
                if (bay < 0 || bay >= Bays || Mathf.Abs(outward - BayR(bay)) > LanternSize.x + reach) continue;
                float y0 = BayY(bay);
                if (pos.y < y0 - ColumnBound.y - reach || pos.y > y0 + CrownUp + LanternSize.y + reach) continue;
                if (Mathf.Abs(Mathf.Abs(across) - ColumnSide) <= ColumnBound.x + reach)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        // A move steps clear of the WHOLE column (its six courses as one box), or it
                        // would only trade one course for the next.
                        if (escape)
                            Meet(ref size, ref pos, rot, true, keep, along, ColumnPos(p, bay, s, 0) + Vector3.up * (2.5f * ColumnPitch),
                                Identity, new Vector3(ColumnBound.x, 5f * ColumnPitch + ColumnBound.y, ColumnBound.z));
                        else
                        {
                            int c0 = Mathf.RoundToInt((pos.y - y0) / ColumnPitch);
                            for (int c = Mathf.Max(0, c0 - 1); c <= Mathf.Min(5, c0 + 1); c++)
                                Meet(ref size, ref pos, rot, false, keep, along, ColumnPos(p, bay, s, c), Identity, ColumnBound);
                        }
                    }
                if (pos.y < y0 + CrownUp - 2f * ArchRise - reach) continue;
                for (int i = 0; i < ArchPrisms; i++)
                {
                    var b = _arch[(p * Bays + bay) * ArchPrisms + i];
                    Meet(ref size, ref pos, rot, escape, keep, along, b.P, b.A, b.S);
                }
                if (bay % 2 == 0)
                    Meet(ref size, ref pos, rot, escape, keep, along, LanternPos(p, bay), Identity, LanternFit(p, bay));
            }
        }

        const int Towers = 9;
        const int UprightPrisms = 22;
        const int Rungs = 12;
        const float TowerHalf = 9f;
        static readonly Vector3 UprightSize = new(1.8f, 5.4f, 1.8f);
        static readonly Vector3 RungSize = new(1.5f, 1.5f, 5.4f);

        /// <summary>The rung ring's radius. It was 1.05 x the upright radius, so every rung ring
        /// ran THROUGH the uprights (a rung centre sat inside an upright on every tower - the
        /// rungs twist with the uprights, so the overlap held all the way up). The ring now clears
        /// the uprights' widest corner by the rung's half-girth: a rung is a chord whose inner face
        /// lies wholly outside that circle.</summary>
        const float RungR = TowerHalf + 0.5f * JitMax * (1.8f * 1.41421356f + 1.5f) / Flush;

        Vector3[] _towerFoot;
        float[] _towerA, _towerH;

        void Tower(int k, out Vector3 foot, out float a, out float height)
        {
            foot = _towerFoot[k]; a = _towerA[k]; height = _towerH[k];
        }

        void ComputeTower(int k, out Vector3 foot, out float a, out float height)
        {
            a = k * GoldenAngle;
            int terrace = 1 + k % (Terraces - 1);
            float r = TerraceR(terrace) - 6f;
            height = 96f + 28f * Hash01(k * 31 + _noiseSeed);
            foot = new Vector3(Mathf.Cos(a) * r, TerraceY(terrace) + 4f, Mathf.Sin(a) * r);
        }

        Vector3 ComputeUprightPos(Vector3 foot, float a, float height, int u, int i)
        {
            float ua = a + Mathf.PI * 0.25f + u * Mathf.PI * 0.5f;
            Vector3 offset = new Vector3(Mathf.Cos(ua), 0f, Mathf.Sin(ua)) * TowerHalf;
            // Uprights twist slightly as they climb - a woven trellis, not a cage.
            float t = i / (UprightPrisms - 1f);
            var pos = foot + Quaternion.AngleAxis(t * 26f, Vector3.up) * offset + Vector3.up * (t * height);
            // A course that an arcade or the aqueduct passes too close to cut clear steps level
            // by the least that clears (its course-to-course contact is vertical, so it keeps).
            var bound = UprightBound(height);
            ClearOfBuilt(ref bound, ref pos, Quaternion.identity, escape: true);
            return pos;
        }

        /// <summary>An upright course is never taller than the climb to the next one.</summary>
        static Vector3 UprightBound(float height) => new(UprightSize.x * JitMax,
            Mathf.Min(UprightSize.y * JitMax, height / (UprightPrisms - 1f) * Flush), UprightSize.z * JitMax);

        Vector3 ComputeRungPos(Vector3 foot, float height, int rung, int i, out Quaternion rot)
        {
            float t = (rung + 0.5f) / Rungs;
            float ra = 2f * Mathf.PI * i / 8f + t * 0.45f;
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(ra), 0f, Mathf.Cos(ra)), Vector3.up);
            var pos = foot + new Vector3(Mathf.Cos(ra), 0f, Mathf.Sin(ra)) * RungR + Vector3.up * (t * height);
            // A rung laid through a pergola column or the aqueduct steps OUT along its own radial
            // by the least that clears (a cut cannot clear a rung whose centre is inside the
            // column, and stepping in would put it through the uprights).
            var bound = RungSize * JitMax;
            ClearOfBuilt(ref bound, ref pos, rot, escape: true, along: new Vector3(Mathf.Cos(ra), 0f, Mathf.Sin(ra)));
            return pos;
        }

        /// <summary>
        /// Nine lattice towers on golden-angle bearings - four uprights laced with rungs. Sown at
        /// the foot and again at half height, so a climber can start partway up an old tower. A
        /// tower that stands in an arcade or reaches the aqueduct yields to them where they cross.
        /// </summary>
        void TrellisTowers()
        {
            for (int k = 0; k < Towers; k++)
            {
                Tower(k, out var foot, out float a, out float height);
                float half = TowerHalf;
                var bound = UprightBound(height);

                for (int u = 0; u < 4; u++)
                {
                    for (int i = 0; i < UprightPrisms; i++)
                    {
                        var pos = _upright[(k * 4 + u) * UprightPrisms + i].P;
                        var size = Jit(UprightSize);
                        size.y = Mathf.Min(size.y, bound.y);
                        ClearOfBuilt(ref size, ref pos, Quaternion.identity);
                        Emit(pos, Quaternion.identity, size, Domains.Gold);
                    }
                }

                for (int rung = 0; rung < Rungs; rung++)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        var rb = _rung[(k * Rungs + rung) * 8 + i];
                        var pos = rb.P;
                        var rot = rb.R;
                        var size = Jit(RungSize);
                        ClearOfBuilt(ref size, ref pos, rot);
                        Emit(pos, rot, size, Domains.Jade);
                    }
                }

                Sow(foot + Vector3.up * 3f, Vector3.up, FloraSiteKind.Climb);
                Sow(foot + Vector3.up * (height * 0.5f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * half,
                    new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)), FloraSiteKind.Climb);
                Sow(foot + Vector3.up * (height * 0.85f), Vector3.up, FloraSiteKind.Ledge);
            }
        }

        /// <summary>The pergolas and the aqueduct ring - what a trellis tower yields to.</summary>
        void ClearOfBuilt(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 along = default)
        {
            ClearOfPergolas(ref size, ref pos, rot, escape, default, along);
            ClearOfAqueductRing(ref size, ref pos, rot, escape, default, along);
        }

        /// <summary>Fit a box against the trellis towers near it (every upright and rung course
        /// within reach of it).</summary>
        void ClearOfTrellis(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            float reach = 0.5f * size.magnitude;
            for (int k = 0; k < Towers; k++)
            {
                Tower(k, out var foot, out float a, out float height);
                var d = pos - foot;
                if (d.x * d.x + d.z * d.z > Sq(RungR + RungSize.z + reach)) continue;
                if (d.y < -UprightSize.y - reach || d.y > height + UprightSize.y + reach) continue;
                var bound = UprightBound(height);
                int i0 = Mathf.RoundToInt(d.y / height * (UprightPrisms - 1f));
                for (int u = 0; u < 4; u++)
                    for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(UprightPrisms - 1, i0 + 2); i++)
                        Meet(ref size, ref pos, rot, escape, keep, along, _upright[(k * 4 + u) * UprightPrisms + i].P, Identity, bound);
                int r0 = Mathf.RoundToInt(d.y / height * Rungs - 0.5f);
                for (int rung = Mathf.Max(0, r0 - 1); rung <= Mathf.Min(Rungs - 1, r0 + 1); rung++)
                    for (int i = 0; i < 8; i++)
                    {
                        var rb = _rung[(k * Rungs + rung) * 8 + i];
                        var rp = rb.P;
                        var rr = rb.R;
                        Meet(ref size, ref pos, rot, escape, keep, along, rp, rr, RungSize * JitMax);
                    }
            }
            return;
        }

        const int Falls = 6;
        const int FallPrisms = 60;
        const float AqueductR = 372f;
        static readonly Vector3 RingSize = new(2.6f, 3f, 6.6f);
        static readonly Vector3 FallSize = new(2.2f, 2.2f, 6.8f);

        /// <summary>How far inside the aqueduct ring each fall starts. A fall's head was laid ON the
        /// ring (inside a channel prism - two of the six exactly on a prism centre), which no size
        /// can clear; it now spills from the ring's inner face (the ring's radial half-girth plus
        /// the head's radial reach), and the offset tapers to nothing at the pool.</summary>
        const float FallInset = 0.5f * JitMax * (2.6f + 0.514f * 6.8f + 0.857f * 2.2f) / Flush;

        float AqueductY => TerraceY(Terraces - 1) + 62f;
        int AqueductCount => Scaled(220);

        Vector3 RingPos(int i, int c, out Quaternion rot)
        {
            float a = 2f * Mathf.PI * i / AqueductCount;
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
            return new Vector3(Mathf.Cos(a) * AqueductR, AqueductY + c * 6f, Mathf.Sin(a) * AqueductR);
        }

        Vector3 FallPos(int f, int i, out Quaternion rot)
        {
            var b = _fall[f * FallPrisms + i];
            rot = b.R;
            return b.P;
        }

        Vector3 ComputeFallPos(int f, int i, out Quaternion rot)
        {
            float a = 2f * Mathf.PI * f / Falls + 0.4f;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            // The fall drifts inward as it descends and braids on the noise - water, not a plumb
            // line.
            float t = i / (FallPrisms - 1f);
            float r = AqueductR - FallInset - t * (250f - FallInset);
            float y = AqueductY;
            float fy = y - t * (y - Floor - 4f);
            float braid = 7f * (N01(t * 6f, f * 3f, 0f, 11) - 0.5f);
            rot = SpawnPoint.LookRotation(Vector3.down + radial * 0.6f, Vector3.up);
            var pos = radial * r + Vector3.up * fy + new Vector3(-radial.z, 0f, radial.x) * braid;
            // Where the fall drops through a terrace too near a bed's centre to cut clear, it
            // steps aside by the least that clears.
            var bound = FallSize * JitMax;
            ClearOfTerraces(ref bound, ref pos, rot, escape: true);
            ClearOfAqueductRing(ref bound, ref pos, rot, escape: true);
            return pos;
        }

        /// <summary>The aqueduct ring above the top terrace, with six cascades falling inward. A
        /// fall passes down through the terraces and yields to them (the bramble hedge it also
        /// passes yields to the fall).</summary>
        void Aqueduct()
        {
            int count = AqueductCount;
            for (int i = 0; i < count; i++)
                for (int c = 0; c < 2; c++)
                    Emit(RingPos(i, c, out var rot), rot, Jit(RingSize), Domains.Ruby);

            for (int f = 0; f < Falls; f++)
            {
                for (int i = 0; i < FallPrisms; i++)
                {
                    var pos = FallPos(f, i, out var rot);
                    var size = Jit(FallSize);
                    ClearOfTerraces(ref size, ref pos, rot);
                    ClearOfAqueductRing(ref size, ref pos, rot);
                    Emit(pos, rot, size, Domains.Ruby);
                }
            }
        }

        /// <summary>Fit a box against the aqueduct ring's channel prisms near it.</summary>
        void ClearOfAqueductRing(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            float reach = 0.5f * size.magnitude;
            float pr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            if (Mathf.Abs(pr - AqueductR) > 3f + reach || pos.y < AqueductY - 3f - reach || pos.y > AqueductY + 9f + reach)
                return;
            int count = AqueductCount;
            int i0 = Mathf.RoundToInt(Mathf.Atan2(pos.z, pos.x) / (2f * Mathf.PI) * count);
            for (int di = -1; di <= 1; di++)
                for (int c = 0; c < 2; c++)
                {
                    var op = RingPos(((i0 + di) % count + count) % count, c, out var orot);
                    Meet(ref size, ref pos, rot, escape, keep, along, op, orot, RingSize * JitMax);
                }
            return;
        }

        /// <summary>Fit a box against the falls near it.</summary>
        void ClearOfFalls(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            float reach = 0.5f * size.magnitude;
            float pr = Mathf.Sqrt(pos.x * pos.x + pos.z * pos.z);
            float ty = (AqueductY - pos.y) / (AqueductY - Floor - 4f);
            if (ty < -0.1f || ty > 1.1f) return;
            int i0 = Mathf.RoundToInt(ty * (FallPrisms - 1f));
            for (int f = 0; f < Falls; f++)
            {
                for (int i = Mathf.Max(0, i0 - 2); i <= Mathf.Min(FallPrisms - 1, i0 + 2); i++)
                {
                    var op = FallPos(f, i, out var orot);
                    if ((op - pos).sqrMagnitude > Sq(reach + 6f)) continue;
                    Meet(ref size, ref pos, rot, escape, keep, along, op, orot, FallSize * JitMax);
                }
            }
            return;
        }

        /// <summary>The central pool the cascades feed - a flat disc of still water at the floor.</summary>
        void Pool()
        {
            int count = Scaled(500);
            // A pool plate's footprint is capped at this fraction of the phyllotaxis's mean site
            // spacing - the largest at which the disc's worst-placed neighbours (where the spiral
            // changes parastichy, around plates 70 and 200) still only touch. Its thin axis keeps
            // the jitter.
            float maxFootprint = 0.655f * Mathf.Sqrt(Mathf.PI * 112f * 112f / count);
            for (int i = 0; i < count; i++)
            {
                // Phyllotaxis disc: even coverage with no ring banding.
                float t = (i + 0.5f) / count;
                float r = 112f * Mathf.Sqrt(t);
                float a = i * GoldenAngle;
                Vector3 pos = new Vector3(Mathf.Cos(a) * r, Floor + 1.5f * (N01(r * 0.05f, a, 0f, 7) - 0.5f),
                    Mathf.Sin(a) * r);
                var size = Jit(new Vector3(6.5f, 0.9f, 6.5f));
                size.x = Mathf.Min(size.x, maxFootprint);
                size.z = Mathf.Min(size.z, maxFootprint);
                Emit(pos, SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up),
                    size, Domains.Ruby);
            }

            // Rim planting - reeds at the water's edge.
            for (int i = 0; i < 24; i++)
            {
                float a = 2f * Mathf.PI * i / 24f;
                Sow(new Vector3(Mathf.Cos(a) * 118f, Floor + 3f, Mathf.Sin(a) * 118f), Vector3.up,
                    FloraSiteKind.Water);
            }
        }

        /// <summary>
        /// A ring of baskets suspended from the vine dome. Their planting normal points DOWN, so
        /// what roots in them grows toward the floor - the trailing half of the garden. This is
        /// the whole reason a site carries a normal at all.
        /// </summary>
        void HangingBaskets()
        {
            const int baskets = 14;
            const int hoop = 20;
            const float hoopR = 15f;
            var hoopSize = new Vector3(1.8f, 2.6f, 5.4f);
            var chainSize = new Vector3(1.2f, 4f, 1.2f);
            // The hoop is a regular 20-gon of tangent prisms: each one is as long as the polygon's
            // side at its INNER face (the widest girth's), so neighbours meet at the bisector.
            float hoopLen = 2f * (hoopR - 0.5f * hoopSize.x * JitMax) * Mathf.Tan(Mathf.PI / hoop) * Flush;
            // A hanger's links are as tall as the climb between them.
            float linkLen = 40f / 9f * Flush;
            // A hanger's foot sat ON the hoop line (one of the three exactly on a hoop prism) and
            // the three hangers' top links on ONE point: the foot steps in to the hoop's inner face,
            // and each top link steps back along its own hanger until the three clear each other.
            float footIn = 0.5f * JitMax * (hoopSize.x + chainSize.x * 1.41421356f) / Flush;
            float topBack = JitMax * chainSize.x / (1.5f * Flush);

            float y = TerraceY(Terraces - 1) + 118f;
            for (int b = 0; b < baskets; b++)
            {
                float a = 2f * Mathf.PI * b / baskets + 0.22f;
                float r = 190f + 70f * Hash01(b * 53 + _noiseSeed);
                Vector3 centre = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);

                for (int i = 0; i < hoop; i++)
                {
                    float ha = 2f * Mathf.PI * i / hoop;
                    var tangent = new Vector3(-Mathf.Sin(ha), 0f, Mathf.Cos(ha));
                    var size = Jit(hoopSize);
                    size.z = Mathf.Min(size.z, hoopLen);
                    Emit(centre + new Vector3(Mathf.Cos(ha), 0f, Mathf.Sin(ha)) * hoopR,
                        SpawnPoint.LookRotation(tangent, Vector3.up), size, Domains.Gold);
                }

                for (int s = 0; s < 3; s++)
                {
                    float ha = 2f * Mathf.PI * s / 3f;
                    var dir = new Vector3(Mathf.Cos(ha), 0f, Mathf.Sin(ha));
                    Vector3 anchor = centre + dir * hoopR;
                    for (int i = 0; i < 10; i++)
                    {
                        Vector3 pos = Vector3.Lerp(anchor, centre + Vector3.up * 40f, i / 9f);
                        if (i == 0) pos -= dir * footIn;
                        else if (i == 9) pos += dir * topBack;
                        var size = Jit(chainSize);
                        size.y = Mathf.Min(size.y, linkLen);
                        Emit(pos, Quaternion.identity, size, Domains.Gold);
                    }
                }

                Sow(centre + Vector3.down * 3f, Vector3.down, FloraSiteKind.Basket);
            }
        }

        /// <summary>Meridian ribs arcing from the wall crown to a crown ring - the frame a mature
        /// garden's climbers eventually roof over.</summary>
        void VineDome()
        {
            const int ribs = 10;
            const int ribPrisms = 60;
            const int crown = 40;
            const float crownR = 34f;
            var ribSize = new Vector3(2f, 2f, 7f);
            var crownSize = new Vector3(2.4f, 2.4f, 6f);
            float baseY = TerraceY(Terraces - 1) + Rim;
            float apexY = baseY + 150f;

            // The ten ribs met in ONE point at the apex (every rib's last prism on the same
            // centre): a rib's last prism stops where the ten, as spokes of the widest girth, clear
            // each other - and the prism before it shares the rest of the gap.
            float apexStop = 0.5f * JitMax * ribSize.x / Mathf.Sin(Mathf.PI / ribs) / Flush;
            float lastR = RimR * Mathf.Cos((ribPrisms - 2f) / (ribPrisms - 1f) * Mathf.PI * 0.5f);
            float tipR = 0.5f * (apexStop + lastR);

            var g = ribSize * JitMax;
            for (int k = 0; k < ribs; k++)
            {
                float a = 2f * Mathf.PI * k / ribs;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 RibPos(int i, out Quaternion rot)
                {
                    float t = i / (ribPrisms - 1f);
                    float r = i == ribPrisms - 1 ? tipR : RimR * Mathf.Cos(t * Mathf.PI * 0.5f);
                    float y = baseY + (apexY - baseY) * Mathf.Sin(t * Mathf.PI * 0.5f);
                    Vector3 along = radial * -Mathf.Sin(t * Mathf.PI * 0.5f) +
                                    Vector3.up * Mathf.Cos(t * Mathf.PI * 0.5f);
                    // At the apex the rib runs level, straight at the axis - a square girth, so
                    // the roll is free; it is set by world up instead of the parallel radial.
                    rot = SpawnPoint.LookRotation(along, i == ribPrisms - 1 ? Vector3.up : radial);
                    return radial * r + Vector3.up * y;
                }

                for (int i = 0; i < ribPrisms; i++)
                {
                    var pos = RibPos(i, out var rot);
                    var size = Jit(ribSize);
                    if (i > 0) size.z = Mathf.Min(size.z, ChainRoom(RibPos(i - 1, out var r0), r0, g, pos, rot, g));
                    if (i < ribPrisms - 1) size.z = Mathf.Min(size.z, ChainRoom(pos, rot, g, RibPos(i + 1, out var r1), r1, g));
                    if (i == ribPrisms - 1) size.z = Mathf.Min(size.z, 2f * (tipR - apexStop) * Flush);
                    Emit(pos, rot, size, Domains.Jade);
                }
            }

            // The crown ring sat at the apex height, where every fourth prism had a rib running
            // through its centre: it rests ON the ribs instead (lifted by the two half-girths), a
            // 40-gon of prisms as long as its side at the inner face.
            float crownLift = 0.5f * JitMax * (ribSize.y + crownSize.y) / Flush + 0.4f;
            float crownLen = 2f * (crownR - 0.5f * crownSize.x * JitMax) * Mathf.Tan(Mathf.PI / crown) * Flush;
            for (int i = 0; i < crown; i++)
            {
                float a = 2f * Mathf.PI * i / crown;
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                var size = Jit(crownSize);
                size.z = Mathf.Min(size.z, crownLen);
                Emit(new Vector3(Mathf.Cos(a) * crownR, apexY + crownLift, Mathf.Sin(a) * crownR),
                    SpawnPoint.LookRotation(tangent, Vector3.up), size, Domains.Gold);
            }
        }

        const int GateArch = 56;
        static readonly Vector3 GateArchSize = new(2.4f, 2.4f, 7.4f);
        static readonly Vector3 GatePostSize = new(3.2f, 6.6f, 3.2f);
        static float GateY => TerraceY(Terraces - 1) + 10f;

        static Vector3 GateArchPos(int i, out Quaternion rot)
        {
            float ang = Mathf.PI * i / (GateArch - 1f);
            rot = SpawnPoint.LookRotation(new Vector3(0f, Mathf.Cos(ang), -Mathf.Sin(ang)), Vector3.right);
            return new Vector3(RimR, GateY + Mathf.Sin(ang) * 78f, Mathf.Cos(ang) * 62f);
        }

        static Vector3 ComputeGateArchFit(int i)
        {
            var size = GateArchSize;
            var pos = GateArchPos(i, out var rot);
            if (i > 0) size.z = Mathf.Min(size.z, ChainRoom(GateArchPos(i - 1, out var r0), r0, GateArchSize, pos, rot, GateArchSize));
            if (i < GateArch - 1) size.z = Mathf.Min(size.z, ChainRoom(pos, rot, GateArchSize, GateArchPos(i + 1, out var r1), r1, GateArchSize));
            return size;
        }

        static Vector3 GatePostPos(int p, int c) =>
            new(RimR + (p % 2 == 0 ? -14f : 14f), GateY + c * 7f, (p < 2 ? -1f : 1f) * 62f);

        /// <summary>
        /// The orchard gate - the garden's permanent bones. Super-shielded, so no force in the
        /// food web can take it: the gate is still standing whatever the fauna do to the planting.
        /// Kept small (96 prisms) because super-shielded mass is removable only by an energised
        /// blade, so it is permanent furniture - not because of colliders (a shield swaps the mesh,
        /// never the collider). It stands in the wall's line; the wall yields to it.
        /// </summary>
        void OrchardGate()
        {
            for (int i = 0; i < GateArch; i++)
                Emit(_gate[i].P, _gate[i].R, _gate[i].S, Domains.Gold, PrismKind.SuperShielded);

            for (int p = 0; p < 4; p++)
                for (int c = 0; c < 10; c++)
                    Emit(GatePostPos(p, c), Quaternion.identity, GatePostSize, Domains.Gold, PrismKind.SuperShielded);
        }

        /// <summary>Fit a box against the orchard gate (only a wall brick near its bearing asks).</summary>
        void ClearOfGate(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape = false, Vector3 keep = default, Vector3 along = default)
        {
            if (pos.x < RimR - 30f || Mathf.Abs(pos.z) > 80f) return;
            for (int i = 0; i < GateArch; i++)
                Meet(ref size, ref pos, rot, escape, keep, along, _gate[i].P, _gate[i].A, _gate[i].S);
            for (int p = 0; p < 4; p++)
                for (int c = 0; c < 10; c++)
                    Meet(ref size, ref pos, rot, escape, keep, along, GatePostPos(p, c), Identity, GatePostSize);
            return;
        }

        const int BramblePrisms = 160;
        static readonly Vector3 BrambleSize = new(1.6f, 1.6f, 5.2f);

        Vector3 BramblePos(int arc, int i, out Quaternion rot)
        {
            var b = _bramble[arc * BramblePrisms + i];
            rot = b.R;
            return b.P;
        }

        Vector3 ComputeBramblePos(int arc, int i, out Quaternion rot)
        {
            float a0 = arc * Mathf.PI * 0.8f + 0.6f;
            float r = TerraceR(arc) + 30f;
            float y = TerraceY(arc) + 10f;
            float t = i / (BramblePrisms - 1f);
            float a = a0 + t * 1.5f;
            // A tangled hedge: the thorn wanders in radius and height on the noise.
            float rr = r + 16f * (N01(t * 9f, arc * 5f, 0f, 21) - 0.5f);
            float yy = y + 18f * N01(t * 7f, arc * 2f, 1f, 22);
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0.3f, Mathf.Cos(a)), Vector3.up);
            var pos = new Vector3(Mathf.Cos(a) * rr, yy, Mathf.Sin(a) * rr);
            // A thorn the hedge wanders into a column or a fall with steps out by the least that
            // clears (its chain room is measured from where it ends up).
            var bound = BrambleSize * JitMax;
            ClearOfPergolas(ref bound, ref pos, rot, escape: true);
            ClearOfFalls(ref bound, ref pos, rot, escape: true);
            return pos;
        }

        /// <summary>The longest bramble thorn <paramref name="i"/> can be: the room its neighbours
        /// along the hedge leave it (measured where they end up).</summary>
        float BrambleLength(int arc, int i) => _bramble[arc * BramblePrisms + i].S.z;

        /// <summary>
        /// Two bramble arcs of TRUE danger prisms on the lowest terraces. A garden has thorns;
        /// danger prisms are not domain-safe, so these bite whoever brushes them - and per the
        /// diet rules they are ordinary food for a herbivore that finds them, unlike the gate.
        /// Each thorn is as long as the hedge leaves it room for (the thorns step ~2 along the
        /// hedge, so they are short), and a thorn that strays into a pergola column or a fall
        /// yields to it.
        /// </summary>
        void Brambles()
        {
            for (int arc = 0; arc < 2; arc++)
            {
                for (int i = 0; i < BramblePrisms; i++)
                {
                    var pos = BramblePos(arc, i, out var rot);
                    var size = Jit(BrambleSize);
                    size.z = Mathf.Min(size.z, BrambleLength(arc, i));
                    ClearOfPergolas(ref size, ref pos, rot);
                    ClearOfFalls(ref size, ref pos, rot);
                    Emit(pos, rot, size, Domains.Ruby, PrismKind.Danger);
                }
            }
        }

        /// <summary>Pollen: sparse motes drifting through the bowl volume - the air is not empty. A
        /// mote that drifts into the built garden is fitted to it.</summary>
        void Pollen()
        {
            int count = Scaled(900);
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float r = RimR * 0.94f * Mathf.Pow(t, 0.42f);
                float a = i * GoldenAngle;
                float y = Floor + 20f + RangeF(0f, TerraceY(Terraces - 1) + Rim - Floor);
                // Nudged along the curl field so the motes read as drifting currents, not a
                // uniform speckle.
                Vector3 pos = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                pos += Curl(pos * 0.004f, 1f, 31) * 22f;
                var rot = Quaternion.Euler(RangeF(0f, 360f), RangeF(0f, 360f), RangeF(0f, 360f));
                var size = Jit(new Vector3(1.3f, 1.3f, 1.3f), 0.35f);
                Pose ax = rot;
                // A mote laid inside a structure steps out of it by the least that clears; the
                // rest are cut to touch.
                for (int pass = 0; pass < 2; pass++)
                {
                    bool escape = pass == 0;
                    ClearOfTerraces(ref size, ref pos, ax, escape);
                    ClearOfPergolas(ref size, ref pos, ax, escape);
                    ClearOfTrellis(ref size, ref pos, ax, escape);
                    ClearOfAqueductRing(ref size, ref pos, ax, escape);
                    ClearOfFalls(ref size, ref pos, ax, escape);
                }
                Emit(pos, rot, size, i % 3 == 0 ? Domains.Gold : Domains.Jade);
            }
        }

        // ── Flush fitting (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md §49.10) ──
        //
        // Every size above is FITTED at authoring time from geometry the generator already has: a
        // chain's prism is as long as the room its neighbours leave it, a jittered prism is
        // clamped only on the axis that touches, and where two structures cross the later one
        // yields on the single axis that keeps the most of it. Nothing searches the lay list -
        // every neighbour is recomputed from its own index - so the cost is a bounded handful of
        // box tests per prism, the prism count, positions and draw stream are the authored ones,
        // and the only prisms MOVED are those whose centre was laid inside another prism, each by
        // the smallest offset that clears: the arch feet, the rung ring, the fall heads, the
        // basket hangers' ends, the dome apex and crown (all of them, by construction), and the
        // odd bed, rung, thorn or pollen mote that a column, an arch or the aqueduct passes
        // through too near its centre to cut clear.

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

        /// <summary>The most <see cref="CellEnvironmentSpawnableBase.Jit"/>'s default amount (0.2)
        /// can grow a size: the bound a neighbour's not-yet-drawn jitter is fitted against.</summary>
        const float JitMax = 1.2f;

        static float Sq(float x) => x * x;

        static float ChainRoom(Vector3 pa, in Pose ra, Vector3 sa, Vector3 pb, in Pose rb, Vector3 sb) =>
            Room(pa, ra, sa, pb, rb, sb, 2);

        /// <summary>One prism meeting one fixed obstacle: CUT shrinks it to touch (the emit), and
        /// ESCAPE moves it by the least that clears when no cut can (the pose - so every
        /// family that looks the prism up sees where it really is).</summary>
        static void Meet(ref Vector3 size, ref Vector3 pos, in Pose rot, bool escape, Vector3 keep, Vector3 along,
            Vector3 op, in Pose orot, Vector3 os)
        {
            if (!escape) { size = Clear(size, pos, rot, op, orot, os); return; }
            Clear(size, pos, rot, op, orot, os, out bool fits);
            if (fits) return;
            pos += along != Vector3.zero ? EscapeAlong(size, pos, rot, op, orot, os, along)
                                         : Escape(size, pos, rot, op, orot, os, keep);
        }

        /// <summary>The smallest move ALONG <paramref name="u"/> (a unit direction, forward only)
        /// that takes box A clear of box B at its full size.</summary>
        static Vector3 EscapeAlong(Vector3 size, Vector3 p, in Pose a, Vector3 op, in Pose b, Vector3 os, Vector3 u)
        {
            Vector3 d = op - p, h = 0.5f * size, oh = 0.5f * os;
            Span<Vector3> axes = stackalloc Vector3[15];
            int n = Axes(a, b, axes);
            float least = float.MaxValue;
            for (int s = 0; s < n; s++)
            {
                var l = axes[s];
                float span = Reach(l, a, h) + Reach(l, b, oh);
                float dl = Vector3.Dot(d, l), ul = Vector3.Dot(u, l);
                if (Mathf.Abs(dl) >= span) return Vector3.zero;       // already clear
                if (Mathf.Abs(ul) < 1e-4f) continue;
                // Separated on this axis once |dl - t ul| reaches span.
                float t1 = (dl - span) / ul, t2 = (dl + span) / ul;
                if (t1 >= 0f && t1 < least) least = t1;
                if (t2 >= 0f && t2 < least) least = t2;
            }
            return least == float.MaxValue ? Vector3.zero : u * (least / Flush + 1e-3f);
        }
    }
}
