// Round 11b-2 (Docs/SUBSTRATE_FAUNA.md §7): ONE agent's step as a static pure function over struct-of-arrays - the
// research's fused kernel (kernels_nb.fused_step) plus the bestiary primitives - written so Burst compiles it: scalar
// float maths, MathF, Span reads and writes, nothing else. No System.Numerics method or operator, no System.Math, no
// allocation, no managed type (Tools/Build/substrate_harness/check_burst_substrate.py is the gate).
//
// The SAME function runs in three places: SubstrateCore's managed agent pass (the harness, Parallel.For over workers),
// the game's [BurstCompile] SubstrateAgentJob (an IJobParallelFor per population, chained in population order), and
// the harness's bit-match test against the pre-11c managed step it replaced (harness group K).
//
// Pure C#: compiled and run by Tools/Build/substrate_harness.
using System;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>One population's numbers as the kernel reads them: blittable (the species' scalars, its two regimes, its
    /// slot block, its moment table's shape, its prey block). Built once per tick by <see cref="SubstrateCore"/>.</summary>
    public struct SubstrateKernelPop
    {
        public SubstrateRegime Rs, Rg;
        public float Metabolism, Sense, FearGain, FearDecay, CuriosityRate, AggrBase, AttnR, AttnUrg;
        public float QUp, QDown, QRate, QWidth, QContagion, QWDens, QWProx, QWAlarm, QWClose, QHunger, DensNorm, NbrR;
        public float WPrey, PreySense, Momentum, IntentBlend, RestSpeed, WRestRetreat;
        public float WCreep, CreepMin, CreepR, CreepSpeed, CreepLeadS, GazeCos, Freeze;
        public float BandInner, BandOuter;
        /// <summary>The opt-in ring hold (SubstrateSpeciesParams.RingHoldSeconds; 0 = off, the research step bit for bit).</summary>
        public float RingHoldS;
        /// <summary>Round 11-11: the population's SECTOR pen (unit axis; SectorCos = cos of the half-angle); HasSector 0 = none.</summary>
        public float SectorX, SectorY, SectorZ, SectorCos;
        /// <summary>Round 11-11 primitives (SubstrateSpeciesParams; 0 = off): ramp + strike, the alarm climb, provocation,
        /// jink, the hunt lead's clip, the body plan's well.</summary>
        public float RampS, RampSpeed, WStrike, StrikeSpeed, StrikeAccel, StrikeTurn, HuntLeadMax, WAlarmClimb;
        public float QWProvoke, SlowBelow, RoostR, WJink, JinkR, JinkCos, BodyWell;
        public int FracK, RingRoles, NDirs, Start, PreyStart, PreyCap, LiveCount, ChargeEvery;
        public byte SpacingSpring, HasPrey, HasSector, RestHoldsPhase, Cling, HasBody, RampOnSight;
        public long M, TabMask;
    }

    /// <summary>The tick's world numbers the kernel reads (blittable).</summary>
    public struct SubstrateKernelWorld
    {
        public float Dt, R;
        public long Tick;
        public int NPil;
    }

    /// <summary>
    /// The agents as struct-of-arrays, as spans: over the core's managed arrays in the harness and over NativeArrays
    /// (<c>AsSpan()</c>) in the Burst job. Indexed by core slot except <see cref="Live"/>/<see cref="Key"/> (by the
    /// population's live list position) and the population's own moment table / directions.
    /// </summary>
    public ref struct SubstrateAgentSoA
    {
        // written: only slot i by agent i
        public Span<Vector3> Pos, Vel, IDir, Home;
        public Span<float> Hunger, Fear, Curious, Aggr, Phase, QTarget, ISpeed;
        public Span<bool> Steered, Watched, Creeping;
        // read
        public ReadOnlySpan<Vector3> WSeed;
        public ReadOnlySpan<float> Closure, Rest;
        /// <summary>The ring hold as each agent sees it (SubstrateCore.RingGate): in (0, 1) the ring is holding.</summary>
        public ReadOnlySpan<float> RingGate;
        public ReadOnlySpan<bool> Alive, Starving;
        public ReadOnlySpan<long> ClaimedTick;
        public ReadOnlySpan<SubstratePilot> Pilots;
        public ReadOnlySpan<int> Live;
        public ReadOnlySpan<long> Key, Tab;
        public ReadOnlySpan<double> Agg;
        public ReadOnlySpan<Vector3> Dirs;
        // the fields, gathered per agent at the step's start (SubstrateCore.BeginStep): no G^3 grid crosses to a job
        public ReadOnlySpan<float> FThreat, FAlarm;
        public ReadOnlySpan<Vector3> GFood, GScent, GAlarm, GThreat;
        // round 11-11 (read; written by the world pass): the ramp clock, the attachment, the host a rider clings to, and
        // each member's body-plan slot (its world position and velocity, gathered in BeginStep)
        public ReadOnlySpan<float> Ramp, Attach;
        public ReadOnlySpan<int> Host;
        public ReadOnlySpan<Vector3> SlotGoal, SlotVel;
    }

    public static class SubstrateKernel
    {
        /// <summary>Largest direction set the kernel's callers allocate scratch for (stackalloc in the job).</summary>
        public const int MaxDirs = 32;

        /// <summary>The ring hold's look (Docs/SUBSTRATE_FAUNA.md §7.7): while it holds, a hunter's top speed is this
        /// fraction of its stalk speed (or 0.9 x a faster pilot's, so the ring keeps station), its ring slot orbits the pilot at this many rad/s, and the ring radius tightens by
        /// up to this fraction as the hold runs out. A slow, steady, tightening circle - never frozen, never darting.</summary>
        public const float HoldSpeed = 0.55f, HoldOrbit = 0.3f, HoldTighten = 0.45f;

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static float Len(float x, float y, float z) => MathF.Sqrt(x * x + y * y + z * z);

        static long Hash(long k) => unchecked(k * (long)0x9E3779B97F4A7C15) & 0x7FFFFFFFFFFFFFFF;

        /// <summary>Interest: every direction within 90 degrees of v gains cos * w (research _paint).</summary>
        public static void Paint(Span<float> I, ReadOnlySpan<Vector3> dirs, int nd, float vx, float vy, float vz, float w)
        {
            float n = Len(vx, vy, vz);
            if (n <= 1e-9f || w == 0f) return;
            vx /= n; vy /= n; vz /= n;
            for (int d = 0; d < nd; d++)
            {
                float c = vx * dirs[d].X + vy * dirs[d].Y + vz * dirs[d].Z;
                if (c > 0f) I[d] += c * w;
            }
        }

        /// <summary>Danger: cos^2 * w (research _paint_d).</summary>
        public static void PaintD(Span<float> G, ReadOnlySpan<Vector3> dirs, int nd, float vx, float vy, float vz, float w)
        {
            float n = Len(vx, vy, vz);
            if (n <= 1e-9f || w == 0f) return;
            vx /= n; vy /= n; vz /= n;
            for (int d = 0; d < nd; d++)
            {
                float c = vx * dirs[d].X + vy * dirs[d].Y + vz * dirs[d].Z;
                if (c > 0f) G[d] += c * c * w;
            }
        }

        static int NearestPilot(in SubstrateAgentSoA s, int npil, float px, float py, float pz, out float dist)
        {
            dist = 1e9f;
            int best = -1;
            for (int j = 0; j < npil; j++)
            {
                float d = Len(px - s.Pilots[j].Pos.X, py - s.Pilots[j].Pos.Y, pz - s.Pilots[j].Pos.Z);
                if (d < dist) { dist = d; best = j; }
            }
            return best;
        }

        static int NearestPrey(in SubstrateAgentSoA s, int start, int cap, long tick, float px, float py, float pz, float radius)
        {
            float dist = radius;
            int best = -1;
            for (int j = start; j < start + cap; j++)
            {
                if (!s.Alive[j] || s.Starving[j] || s.ClaimedTick[j] == tick) continue;
                float d = Len(px - s.Pos[j].X, py - s.Pos[j].Y, pz - s.Pos[j].Z);
                if (d < dist) { dist = d; best = j; }
            }
            return best;
        }

        /// <summary>
        /// ONE agent's step - live-list position <paramref name="q"/> of population <paramref name="k"/>: drives for
        /// everyone; if in the re-steer slice (or engaged - the attention LOD) the 27-cell moment read, the quorum target
        /// and the context map; then the bounded integrate. Plus the bestiary primitives: closure feeds the quorum, the
        /// posture clock's rest, the gaze sensor, the prey pounce. Writes only slot <c>Live[q]</c>.
        /// <paramref name="I"/> and <paramref name="G"/> are scratch of at least <c>k.NDirs</c>.
        /// </summary>
        public static void StepAgent(in SubstrateAgentSoA s, in SubstrateKernelPop k, in SubstrateKernelWorld w, int q,
                                     Span<float> I, Span<float> G)
        {
            float dt = w.Dt;
            int i = s.Live[q];
            float px = s.Pos[i].X, py = s.Pos[i].Y, pz = s.Pos[i].Z;
            float ph = s.Phase[i];
            int nd = k.NDirs;

            // ── drives ──
            float hu = s.Hunger[i] + k.Metabolism * dt;   // above 1 = the stomach is empty and the reserve is burning
            s.Hunger[i] = hu;
            float h = MathF.Min(1f, hu);
            int pj = NearestPilot(s, w.NPil, px, py, pz, out float pd);
            float prox = Clamp(1f - pd / k.Sense, 0f, 1f);
            float threat = s.FThreat[i], alarm = s.FAlarm[i];
            float fe = s.Fear[i];
            fe += dt * (k.FearGain * (prox * prox + 0.5f * MathF.Min(threat, 2f) + MathF.Min(alarm, 2f))) - dt * k.FearDecay * fe;
            fe = Clamp(fe, 0f, 1f); s.Fear[i] = fe;
            float calm = (1f - fe) * (1f - h);
            float cu = s.Curious[i] + dt * k.CuriosityRate * (calm * (1f - ph) - s.Curious[i]); s.Curious[i] = cu;
            float capw = MathF.Min(1f, (k.Rs.WHunt + k.Rs.WRing) + ((k.Rg.WHunt + k.Rg.WRing) - (k.Rs.WHunt + k.Rs.WRing)) * ph);
            float ag = Clamp(MathF.Max(h * 1.4f - 0.3f, k.AggrBase), 0f, 1f) * capw; s.Aggr[i] = ag;
            bool resting = s.Rest[i] > 0f;
            if (k.Cling != 0 && s.Host[i] != 0)
            {
                // a RIDER is the world pass's: it sits on its host's hull (no steering, no integrate, no phase)
                s.Steered[i] = false; s.Watched[i] = false; s.Creeping[i] = false;
                return;
            }
            // the strike role (a bull), its ramp (head-down) and its strike (the charge, the dive)
            bool role = k.ChargeEvery <= 1 || ((i - k.Start) % k.ChargeEvery) == 0;
            bool ramping = k.RampS > 0f && role && s.Ramp[i] > 0f && s.Ramp[i] < k.RampS && !resting;
            bool striking = k.RampS > 0f && role && s.Ramp[i] >= k.RampS && !resting;

            // ── re-steer the 1/k slice, plus the attention LOD ──
            int fk = k.FracK > 1 ? k.FracK : 1;
            bool st = ((i + w.Tick) % fk == 0) || pd < k.AttnR || MathF.Max(fe, ag) > k.AttnUrg || ramping || striking;
            s.Steered[i] = st;
            bool freeze = false;
            if (st)
            {
                // 27-cell moment read
                double c0 = 0, c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0, c6 = 0, c7 = 0;
                long k0 = s.Key[q], M = k.M, mask = k.TabMask;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            long kk = k0 + (dx * M + dy) * M + dz;
                            long j = Hash(kk) & mask;
                            while (s.Tab[(int)j] != -1L)
                            {
                                if (s.Tab[(int)j] == kk)
                                {
                                    int o = (int)j * 8;
                                    c0 += s.Agg[o]; c1 += s.Agg[o + 1]; c2 += s.Agg[o + 2]; c3 += s.Agg[o + 3];
                                    c4 += s.Agg[o + 4]; c5 += s.Agg[o + 5]; c6 += s.Agg[o + 6]; c7 += s.Agg[o + 7];
                                    break;
                                }
                                j = (j + 1) & mask;
                            }
                        }
                float cnt = (float)(c0 - 1.0);
                float inv = 1f / MathF.Max(cnt, 1f);
                float cenx = (float)(c1 - px) * inv, ceny = (float)(c2 - py) * inv, cenz = (float)(c3 - pz) * inv;
                float alix = (float)(c4 - s.Vel[i].X) * inv, aliy = (float)(c5 - s.Vel[i].Y) * inv, aliz = (float)(c6 - s.Vel[i].Z) * inv;
                float mph = cnt > 0f ? (float)(c7 - ph) * inv : ph;

                // quorum target - a resting agent's target is the solitary end
                if (k.QUp < 9f)
                {
                    float sig = k.QWDens * cnt / k.DensNorm + k.QWProx * prox + k.QWAlarm * MathF.Min(alarm, 2f) + k.QWClose * s.Closure[i];
                    if (k.QWProvoke > 0f && pj >= 0 && pd < k.Sense)
                    {
                        // PROVOCATION (bestiary mobber): a slow pilot, or one at the roost
                        float pspd = Len(s.Pilots[pj].Vel.X, s.Pilots[pj].Vel.Y, s.Pilots[pj].Vel.Z);
                        float rd = Len(s.Pilots[pj].Pos.X - s.Home[i].X, s.Pilots[pj].Pos.Y - s.Home[i].Y, s.Pilots[pj].Pos.Z - s.Home[i].Z);
                        if (pspd < k.SlowBelow || rd < k.RoostR) sig += k.QWProvoke;
                    }
                    float sq = sig * MathF.Pow(h, k.QHunger);
                    float th = s.QTarget[i] > 0.5f ? k.QDown : k.QUp;
                    float tg = 1f / (1f + MathF.Exp(-(sq - th) / k.QWidth));
                    float qc = k.QContagion;
                    if (cnt > 0f) tg = (1f - qc) * tg + qc * MathF.Max(tg, mph);
                    s.QTarget[i] = resting && k.RestHoldsPhase == 0 ? 0f : tg;
                }

                // the regime at this phase (SubstrateRegime.Lerp, field by field)
                float wFood = Lerp(k.Rs.WFood, k.Rg.WFood, ph), wCoh = Lerp(k.Rs.WCoh, k.Rg.WCoh, ph);
                float wSep = Lerp(k.Rs.WSep, k.Rg.WSep, ph), wAlign = Lerp(k.Rs.WAlign, k.Rg.WAlign, ph);
                float crowd = Lerp(k.Rs.Crowd, k.Rg.Crowd, ph), wWander = Lerp(k.Rs.WWander, k.Rg.WWander, ph);
                float comfort = Lerp(k.Rs.Comfort, k.Rg.Comfort, ph), wCurious = Lerp(k.Rs.WCurious, k.Rg.WCurious, ph);
                float wHunt = Lerp(k.Rs.WHunt, k.Rg.WHunt, ph), wRing = Lerp(k.Rs.WRing, k.Rg.WRing, ph);
                float ringR = Lerp(k.Rs.RingR, k.Rg.RingR, ph), wFlee = Lerp(k.Rs.WFlee, k.Rg.WFlee, ph);
                float wHome = Lerp(k.Rs.WHome, k.Rg.WHome, ph), wAlarm = Lerp(k.Rs.WAlarm, k.Rg.WAlarm, ph);
                float wThreat = Lerp(k.Rs.WThreat, k.Rg.WThreat, ph), speed0 = Lerp(k.Rs.Speed, k.Rg.Speed, ph);
                float burst = Lerp(k.Rs.Burst, k.Rg.Burst, ph);
                // the opt-in ring hold: the ring is saturated and holding (the core's gate is in (0, 1))
                float hg = k.RingHoldS > 0f ? s.RingGate[i] : 0f;
                bool holding = hg > 0f && hg < 1f && !resting && s.Closure[i] >= k.QUp;
                if (holding) ringR *= 1f - HoldTighten * hg;
                float att = k.HasBody != 0 ? s.Attach[i] : 0f;
                if (k.WAlarmClimb > 0f && role)
                {
                    // the BULL: the herd's panic leads it UP the alarm gradient, to what spooked it - it does not flee
                    float bm = 1f - (k.RampOnSight != 0 ? MathF.Max(ph, fe) : ph);
                    wFlee *= bm; wAlarm *= bm; wThreat *= bm;
                }

                for (int d = 0; d < nd; d++) { I[d] = 0f; G[d] = 0f; }
                Paint(I, s.Dirs, nd, s.GFood[i].X, s.GFood[i].Y, s.GFood[i].Z, wFood * h);
                if (k.WPrey > 0f && k.HasPrey != 0)
                {
                    // food web: follow the prey's scent; within PreySense make straight for the nearest one
                    Paint(I, s.Dirs, nd, s.GScent[i].X, s.GScent[i].Y, s.GScent[i].Z, k.WPrey * h);
                    int prey = NearestPrey(s, k.PreyStart, k.PreyCap, w.Tick, px, py, pz, k.PreySense);
                    if (prey >= 0) Paint(I, s.Dirs, nd, s.Pos[prey].X - px, s.Pos[prey].Y - py, s.Pos[prey].Z - pz, 2f * k.WPrey * h);
                }
                if (cnt > 0f)
                {
                    if (k.SpacingSpring != 0)
                    {
                        // ONE signed spring along the neighbour-centroid axis: toward when sparse, away when crowded
                        float sg = Clamp(1f - cnt / k.DensNorm / MathF.Max(crowd, 1e-3f), -1.5f, 1f);
                        float wsp = sg > 0f ? wCoh : wSep;
                        float wspr = MathF.Abs(sg) * wsp;
                        if (k.HasBody != 0) wspr *= 1f + att;   // research: an assembling member holds its school
                        Paint(I, s.Dirs, nd, (cenx - px) * sg, (ceny - py) * sg, (cenz - pz) * sg, wspr);
                    }
                    else Paint(I, s.Dirs, nd, cenx - px, ceny - py, cenz - pz, k.HasBody != 0 ? wCoh * (1f + att) : wCoh);
                    Paint(I, s.Dirs, nd, alix, aliy, aliz, wAlign);
                }
                float tt = w.Tick * 0.05f;
                float wsx = s.WSeed[i].X, wsy = s.WSeed[i].Y, wsz = s.WSeed[i].Z;
                Paint(I, s.Dirs, nd,
                      MathF.Sin(wsx + tt) + 0.6f * MathF.Sin(1.7f * wsz + tt * 2.1f),
                      MathF.Sin(wsy + tt * 1.3f) + 0.6f * MathF.Sin(1.7f * wsy + tt * 2.1f),
                      MathF.Sin(wsz + tt * 0.7f) + 0.6f * MathF.Sin(1.7f * wsx + tt * 2.1f), wWander);
                bool creeping = false;
                bool watched = false;
                if (pj >= 0)
                {
                    float ppx = s.Pilots[pj].Pos.X, ppy = s.Pilots[pj].Pos.Y, ppz = s.Pilots[pj].Pos.Z;
                    float pvx = s.Pilots[pj].Vel.X, pvy = s.Pilots[pj].Vel.Y, pvz = s.Pilots[pj].Vel.Z;
                    float tpx = ppx - px, tpy = ppy - py, tpz = ppz - pz;
                    float near = pd < k.Sense * 1.5f ? 1f : 0f;
                    float sp = Clamp((pd - comfort) / MathF.Max(comfort, 1f), -1f, 1f);
                    Paint(I, s.Dirs, nd, tpx * sp, tpy * sp, tpz * sp, wCurious * cu * MathF.Abs(sp) * near);
                    float ld = Clamp(pd / 150f, 0f, k.HuntLeadMax);
                    Paint(I, s.Dirs, nd, tpx + pvx * ld, tpy + pvy * ld, tpz + pvz * ld, wHunt * ag * near);
                    // the ramp turns it to face your lead point, the strike drives at it (a bull's charge, a dive)
                    if (k.WStrike > 0f && (ramping || striking))
                        Paint(I, s.Dirs, nd, tpx + pvx * ld, tpy + pvy * ld, tpz + pvz * ld, k.WStrike);
                    if (k.WJink > 0f && pd < k.JinkR && pvx * pvx + pvy * pvy + pvz * pvz > 1f)
                    {
                        // JINK: the pilot points straight at it - break sideways off the pilot's line
                        float vn = Len(pvx, pvy, pvz);
                        float ux = pvx / vn, uy = pvy / vn, uz = pvz / vn;
                        float on = MathF.Max(pd, 1e-6f);
                        float ox = -tpx / on, oy = -tpy / on, oz = -tpz / on;
                        float cj = ux * ox + uy * oy + uz * oz;
                        if (cj > k.JinkCos)
                        {
                            float sx = ox - ux * cj, sy = oy - uy * cj, sz = oz - uz * cj;
                            if (Len(sx, sy, sz) < 1e-3f) { sx = -uz; sy = 0f; sz = ux; }   // dead on the line: cross(u, up)
                            Paint(I, s.Dirs, nd, sx, sy, sz, k.WJink);
                        }
                    }
                    if (k.RingRoles > 0)
                    {
                        // ring slots around the pilot in the plane normal to its velocity, slightly AHEAD (a cut-off)
                        float fx = pvx + 1e-9f, fy = pvy, fz = pvz;
                        float fn = Len(fx, fy, fz);
                        if (fn > 1e-9f) { fx /= fn; fy /= fn; fz /= fn; } else { fx = 0f; fy = 0f; fz = 0f; }
                        float ax = -fz + 1e-6f, ay = 1e-6f, az = fx + 1e-6f;   // cross(f, up)
                        float an = Len(ax, ay, az);
                        if (an > 1e-9f) { ax /= an; ay /= an; az /= an; } else { ax = 0f; ay = 0f; az = 0f; }
                        float bx = fy * az - fz * ay, by = fz * ax - fx * az, bz = fx * ay - fy * ax;
                        float ang = 2f * MathF.PI * ((i - k.Start) % k.RingRoles) / k.RingRoles;
                        if (holding) ang += HoldOrbit * w.Tick * w.Dt;   // the held ring circles the pilot
                        float ca = MathF.Cos(ang), sa = MathF.Sin(ang);
                        float slx = (ppx + fx * 40f) + ringR * (ca * ax + sa * bx);
                        float sly = (ppy + fy * 40f) + ringR * (ca * ay + sa * by);
                        float slz = (ppz + fz * 40f) + ringR * (ca * az + sa * bz);
                        Paint(I, s.Dirs, nd, slx - px, sly - py, slz - pz, wRing * ag * near);
                    }
                    PaintD(G, s.Dirs, nd, -tpx, -tpy, -tpz, wFlee * fe * prox);
                    if (resting && k.WRestRetreat > 0f) Paint(I, s.Dirs, nd, -tpx, -tpy, -tpz, k.WRestRetreat);   // winded: fall back
                    // GAZE: a calm agent within creep range slides toward where the pilot will be - only while it is
                    // OUTSIDE the pilot's forward cone; inside it, it freezes (bestiary lurker)
                    if (k.WCreep > 0f && !resting && ph < 0.2f && pd > k.CreepMin && pd < k.CreepR)
                    {
                        bool looked = false;
                        if (pvx * pvx + pvy * pvy + pvz * pvz > 1f)
                        {
                            float vn = Len(pvx, pvy, pvz);
                            float ux = pvx / vn, uy = pvy / vn, uz = pvz / vn;
                            float ox = px - ppx, oy = py - ppy, oz = pz - ppz;
                            float on = Len(ox, oy, oz);
                            if (on > 1e-9f) { ox /= on; oy /= on; oz /= on; } else { ox = 0f; oy = 0f; oz = 0f; }
                            looked = ux * ox + uy * oy + uz * oz > k.GazeCos;
                        }
                        watched = looked;
                        if (!looked)
                        {
                            Paint(I, s.Dirs, nd, ppx + pvx * k.CreepLeadS - px, ppy + pvy * k.CreepLeadS - py,
                                  ppz + pvz * k.CreepLeadS - pz, k.WCreep);
                            creeping = true;
                            s.Home[i].X = px; s.Home[i].Y = py; s.Home[i].Z = pz;   // it leaves its seat
                        }
                        else freeze = k.Freeze > 0f;
                    }
                }
                s.Watched[i] = watched;
                s.Creeping[i] = creeping;
                Paint(I, s.Dirs, nd, s.Home[i].X - px, s.Home[i].Y - py, s.Home[i].Z - pz, wHome * (1f - h) * (creeping ? 0f : 1f));
                if (k.WAlarmClimb > 0f && role)
                    Paint(I, s.Dirs, nd, s.GAlarm[i].X, s.GAlarm[i].Y, s.GAlarm[i].Z, k.WAlarmClimb * (k.RampOnSight != 0 ? MathF.Max(ph, fe) : ph));
                float mute = 1f;
                if (k.HasBody != 0 && att > 0f)
                {
                    // research: an assembled member hands its own drives to the body - every term but body/inward muted
                    mute = 1f - 0.9f * att;
                    for (int d = 0; d < nd; d++) { I[d] *= mute; G[d] *= mute; }
                }
                float r = Len(px, py, pz);
                float R = w.R;
                Paint(I, s.Dirs, nd, -px, -py, -pz, Clamp((r - 0.8f * R) / (0.15f * R), 0f, 1f) * 3f);
                PaintD(G, s.Dirs, nd, px, py, pz, Clamp((r - 0.85f * R) / (0.1f * R), 0f, 1f) * 3f);
                if (k.BandOuter > 0f)
                {
                    // the species' pen (FaunaConfigurationSO band): steered back in, never walled
                    float soft = 0.1f * MathF.Max(k.BandOuter - k.BandInner, 50f);
                    Paint(I, s.Dirs, nd, -px, -py, -pz, Clamp((r - k.BandOuter) / soft, 0f, 1f) * 2f);
                    Paint(I, s.Dirs, nd, px, py, pz, Clamp((k.BandInner - r) / soft, 0f, 1f) * 2f);
                }
                if (k.HasSector != 0 && r > 1e-3f)
                {
                    // the SECTOR pen (round 11-11): outside the cone, steered back toward the axis at this radius
                    float cs = (px * k.SectorX + py * k.SectorY + pz * k.SectorZ) / r;
                    float wsec = Clamp((k.SectorCos - cs) / 0.1f, 0f, 1f) * 2f;
                    if (wsec > 0f) Paint(I, s.Dirs, nd, k.SectorX * r - px, k.SectorY * r - py, k.SectorZ * r - pz, wsec);
                }
                if (cnt > 0f && k.SpacingSpring == 0)
                {
                    float sc = cnt / k.DensNorm / k.NbrR;
                    float svx = (px - cenx) * sc, svy = (py - ceny) * sc, svz = (pz - cenz) * sc;
                    PaintD(G, s.Dirs, nd, svx, svy, svz, wSep * MathF.Min(Len(svx, svy, svz), 2f) * (1f - att));
                }
                PaintD(G, s.Dirs, nd, -s.GAlarm[i].X, -s.GAlarm[i].Y, -s.GAlarm[i].Z, wAlarm * (0.3f + fe) * mute);
                PaintD(G, s.Dirs, nd, -s.GThreat[i].X, -s.GThreat[i].Y, -s.GThreat[i].Z, wThreat * (0.3f + fe) * mute);
                float vstarN = 0f;
                if (k.HasBody != 0 && att > 0f)
                {
                    // the BODY term (research core.py): the slot's own velocity plus a closing speed along the displacement
                    // (flat-bottom well, bounded by the turn radius so members do not orbit their slot)
                    float dx = s.SlotGoal[i].X - px, dy = s.SlotGoal[i].Y - py, dz = s.SlotGoal[i].Z - pz;
                    float dn = Len(dx, dy, dz);
                    float turnB = Lerp(k.Rs.Turn, k.Rg.Turn, ph);
                    float close = MathF.Min(MathF.Min(MathF.Max(dn - k.BodyWell, 0f) * 2f, 0.5f * turnB * dn), 150f);
                    float idn = 1f / MathF.Max(dn, 1e-6f);
                    float vsx = s.SlotVel[i].X + dx * idn * close, vsy = s.SlotVel[i].Y + dy * idn * close, vsz = s.SlotVel[i].Z + dz * idn * close;
                    vstarN = Len(vsx, vsy, vsz);
                    Paint(I, s.Dirs, nd, vsx, vsy, vsz, 4f * att);
                }

                // context choice: momentum, soft danger mask, soft-argmax around the best direction
                float curx = s.IDir[i].X, cury = s.IDir[i].Y, curz = s.IDir[i].Z;
                float mx = -1e30f;
                for (int d = 0; d < nd; d++)
                {
                    float c = curx * s.Dirs[d].X + cury * s.Dirs[d].Y + curz * s.Dirs[d].Z;
                    if (c > 0f) I[d] += k.Momentum * c;
                    float e = I[d] * (1f - Clamp(G[d], 0f, 1f)) - 0.25f * MathF.Max(G[d] - 1f, 0f);
                    I[d] = e;
                    if (e > mx) mx = e;
                }
                float vx = 0f, vy = 0f, vz = 0f;
                if (mx > 1e-6f)
                    for (int d = 0; d < nd; d++)
                    {
                        float wd = I[d] - 0.75f * mx;
                        if (wd > 0f) { float ww = wd * wd; vx += ww * s.Dirs[d].X; vy += ww * s.Dirs[d].Y; vz += ww * s.Dirs[d].Z; }
                    }
                float nn = Len(vx, vy, vz);
                if (nn > 1e-9f)
                {
                    float bl = k.IntentBlend;
                    float ox = bl * curx + (1f - bl) * (vx / nn);
                    float oy = bl * cury + (1f - bl) * (vy / nn);
                    float oz = bl * curz + (1f - bl) * (vz / nn);
                    float on = MathF.Max(Len(ox, oy, oz), 1e-9f);
                    s.IDir[i].X = ox / on; s.IDir[i].Y = oy / on; s.IDir[i].Z = oz / on;
                }
                float urg = MathF.Max(wFlee > 0f ? fe : 0f, ag);
                float speed = speed0 * (1f + (burst - 1f) * urg);
                if (creeping) speed = k.CreepSpeed;
                if (resting) speed *= k.RestSpeed;
                if (ramping) speed *= k.RampSpeed;
                if (striking && k.StrikeSpeed > 0f) speed = k.StrikeSpeed;
                if (k.HasBody != 0 && att > 0f) speed = Lerp(speed, vstarN, att);   // assembled: catch the slot, then the body's pace
                if (holding && pj >= 0)
                {
                    // slow against a slow pilot, never slower than one that moves (the ring keeps station around it)
                    float hps = Len(s.Pilots[pj].Vel.X, s.Pilots[pj].Vel.Y, s.Pilots[pj].Vel.Z);
                    speed = MathF.Min(speed, MathF.Max(HoldSpeed * k.Rs.Speed, 0.9f * hps));
                }
                s.ISpeed[i] = speed;
            }

            // ── integrate (bounded turn and acceleration: smooth by construction) ──
            if (freeze)
            {
                s.Vel[i].X = 0f; s.Vel[i].Y = 0f; s.Vel[i].Z = 0f;   // looked at: dead still
            }
            else
            {
                float turn = k.Rs.Turn + (k.Rg.Turn - k.Rs.Turn) * ph;
                float acl = k.Rs.Accel + (k.Rg.Accel - k.Rs.Accel) * ph;
                if (striking && k.StrikeAccel > 0f) acl = k.StrikeAccel;
                if (striking && k.StrikeTurn > 0f) turn = k.StrikeTurn;
                float wx = s.Vel[i].X, wy = s.Vel[i].Y, wz = s.Vel[i].Z;
                float vs = Len(wx, wy, wz);
                float hx, hy, hz;
                if (vs > 1e-6f) { hx = wx / vs; hy = wy / vs; hz = wz / vs; }
                else { hx = s.IDir[i].X; hy = s.IDir[i].Y; hz = s.IDir[i].Z; }
                float tx = s.IDir[i].X, ty = s.IDir[i].Y, tz = s.IDir[i].Z;
                float c = Clamp(hx * tx + hy * ty + hz * tz, -1f, 1f);
                float ang = MathF.Acos(c);
                float kk = MathF.Min(1f, turn * dt / MathF.Max(ang, 1e-6f));
                float nx = hx + (tx - hx) * kk, ny = hy + (ty - hy) * kk, nz = hz + (tz - hz) * kk;
                float nl = MathF.Max(Len(nx, ny, nz), 1e-9f);
                nx /= nl; ny /= nl; nz /= nl;
                float ns = vs + Clamp(s.ISpeed[i] - vs, -acl * dt, acl * dt);
                float vnx = nx * ns, vny = ny * ns, vnz = nz * ns;
                s.Vel[i].X = vnx; s.Vel[i].Y = vny; s.Vel[i].Z = vnz;
                float qx = px + vnx * dt, qy = py + vny * dt, qz = pz + vnz * dt;
                // the membrane: never past 0.98 R
                float qr = Len(qx, qy, qz);
                if (qr > 0.98f * w.R)
                {
                    float f = 0.98f * w.R / qr;
                    qx *= f; qy *= f; qz *= f;
                }
                s.Pos[i].X = qx; s.Pos[i].Y = qy; s.Pos[i].Z = qz;
            }
            s.Phase[i] = ph + dt * k.QRate * (s.QTarget[i] - ph);
        }
    }
}
