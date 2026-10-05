// Round 11c (Docs/SUBSTRATE_FAUNA.md §7): the PRE-11c managed agent step, kept verbatim (System.Numerics, the core's
// arrays, the field grids read in place) as the REFERENCE the Burst-shaped SubstrateKernel.StepAgent is bit-matched
// against (group K), and as check_burst_substrate.py's negative control: it must FAIL that gate.
using System;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    static class SubstrateReference
    {
        public static void StepAgent(SubstrateCore core, SubstratePopulation pop, int q, Span<float> I, Span<float> G)
        {
            var Pos = core.Pos; var Vel = core.Vel; var IDir = core.IDir; var Home = core.Home; var WSeed = core.WSeed;
            var Hunger = core.Hunger; var Fear = core.Fear; var Curious = core.Curious; var Aggr = core.Aggr; var Phase = core.Phase;
            var QTarget = core.QTarget; var ISpeed = core.ISpeed; var Rest = core.Rest; var Closure = core.Closure;
            var Steered = core.Steered; var Watched = core.Watched; var Creeping = core.Creeping;
            var P = pop.P;
            ref readonly var Rs = ref P.Solitary;
            ref readonly var Rg = ref P.Gregarious;
            float dt = core.Dt;
            int i = pop.Live[q];
            var p = Pos[i];
            float ph = Phase[i];
            int fc = core.Fields.Cell(p);

            // ── drives ──
            float hu = Hunger[i] + P.Metabolism * dt;   // above 1 = the stomach is empty and the reserve is burning
            Hunger[i] = hu;
            float h = MathF.Min(1f, hu);
            int pj = core.NearestPilot(p, out float pd);
            float prox = Math.Clamp(1f - pd / P.Sense, 0f, 1f);
            float threat = core.Fields.Sample(SubstrateFields.Threat, fc), alarm = core.Fields.Sample(SubstrateFields.Alarm, fc);
            float fe = Fear[i];
            fe += dt * (P.FearGain * (prox * prox + 0.5f * MathF.Min(threat, 2f) + MathF.Min(alarm, 2f))) - dt * P.FearDecay * fe;
            fe = Math.Clamp(fe, 0f, 1f); Fear[i] = fe;
            float calm = (1f - fe) * (1f - h);
            float cu = Curious[i] + dt * P.CuriosityRate * (calm * (1f - ph) - Curious[i]); Curious[i] = cu;
            float capw = MathF.Min(1f, (Rs.WHunt + Rs.WRing) + ((Rg.WHunt + Rg.WRing) - (Rs.WHunt + Rs.WRing)) * ph);
            float ag = Math.Clamp(MathF.Max(h * 1.4f - 0.3f, P.AggrBase), 0f, 1f) * capw; Aggr[i] = ag;
            bool resting = Rest[i] > 0f;

            // ── re-steer the 1/k slice, plus the attention LOD (finding 4) ──
            int k = Math.Max(1, P.FracK);
            bool st = ((i + core.Tick) % k == 0) || pd < P.AttnR || MathF.Max(fe, ag) > P.AttnUrg;
            Steered[i] = st;
            bool freeze = false;
            if (st)
            {
                // 27-cell moment read
                double c0 = 0, c1 = 0, c2 = 0, c3 = 0, c4 = 0, c5 = 0, c6 = 0, c7 = 0;
                long k0 = pop.Key[q], M = pop.M, mask = pop.Tab.Length - 1;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            long kk = k0 + (dx * M + dy) * M + dz;
                            long j = SubstrateCore.Hash(kk) & mask;
                            while (pop.Tab[j] != -1L)
                            {
                                if (pop.Tab[j] == kk)
                                {
                                    int o = (int)j * 8; var a = pop.Agg;
                                    c0 += a[o]; c1 += a[o + 1]; c2 += a[o + 2]; c3 += a[o + 3];
                                    c4 += a[o + 4]; c5 += a[o + 5]; c6 += a[o + 6]; c7 += a[o + 7];
                                    break;
                                }
                                j = (j + 1) & mask;
                            }
                        }
                float cnt = (float)(c0 - 1.0);
                float inv = 1f / MathF.Max(cnt, 1f);
                var cen = new Vector3((float)(c1 - p.X) * inv, (float)(c2 - p.Y) * inv, (float)(c3 - p.Z) * inv);
                var ali = new Vector3((float)(c4 - Vel[i].X) * inv, (float)(c5 - Vel[i].Y) * inv, (float)(c6 - Vel[i].Z) * inv);
                float mph = cnt > 0f ? (float)(c7 - ph) * inv : ph;

                // quorum target (one signal for every species) - a resting agent's target is the solitary end
                if (P.QUp < 9f)
                {
                    float sig = P.QWDens * cnt / P.DensNorm + P.QWProx * prox + P.QWAlarm * MathF.Min(alarm, 2f) + P.QWClose * Closure[i];
                    float s = sig * MathF.Pow(h, P.QHunger);
                    float th = QTarget[i] > 0.5f ? P.QDown : P.QUp;
                    float tg = 1f / (1f + MathF.Exp(-(s - th) / P.QWidth));
                    float c = P.QContagion;
                    if (cnt > 0f) tg = (1f - c) * tg + c * MathF.Max(tg, mph);
                    QTarget[i] = resting ? 0f : tg;
                }

                var W = SubstrateRegime.Lerp(Rs, Rg, ph);
                var dirs = pop.Dirs;
                I.Clear(); G.Clear();
                Paint(I, dirs, core.Fields.Grad(SubstrateFields.Food, fc), W.WFood * h);
                if (P.WPrey > 0f && pop.PreyPop >= 0)
                {
                    // food web: follow the prey's scent; within PreySense make straight for the nearest one
                    Paint(I, dirs, core.Fields.Grad(SubstrateFields.Scent, fc), P.WPrey * h);
                    int prey = core.NearestPrey(core.Pops[pop.PreyPop], p, P.PreySense, out _);
                    if (prey >= 0) Paint(I, dirs, Pos[prey] - p, 2f * P.WPrey * h);
                }
                if (cnt > 0f)
                {
                    if (P.SpacingSpring)
                    {
                        // ONE signed spring along the neighbour-centroid axis (finding 2): toward when sparse, away when
                        // crowded, crossing zero at the regime's target crowding
                        float sg = Math.Clamp(1f - cnt / P.DensNorm / MathF.Max(W.Crowd, 1e-3f), -1.5f, 1f);
                        float wsp = sg > 0f ? W.WCoh : W.WSep;
                        Paint(I, dirs, (cen - p) * sg, MathF.Abs(sg) * wsp);
                    }
                    else Paint(I, dirs, cen - p, W.WCoh);
                    Paint(I, dirs, ali, W.WAlign);
                }
                float tt = core.Tick * 0.05f;
                var ws = WSeed[i];
                var wv = new Vector3(
                    MathF.Sin(ws.X + tt) + 0.6f * MathF.Sin(1.7f * ws.Z + tt * 2.1f),
                    MathF.Sin(ws.Y + tt * 1.3f) + 0.6f * MathF.Sin(1.7f * ws.Y + tt * 2.1f),
                    MathF.Sin(ws.Z + tt * 0.7f) + 0.6f * MathF.Sin(1.7f * ws.X + tt * 2.1f));
                Paint(I, dirs, wv, W.WWander);
                bool creeping = false;
                if (pj >= 0)
                {
                    var pil = core._pilots[pj];
                    var tp = pil.Pos - p;
                    float near = pd < P.Sense * 1.5f ? 1f : 0f;
                    float sp = Math.Clamp((pd - W.Comfort) / MathF.Max(W.Comfort, 1f), -1f, 1f);
                    Paint(I, dirs, tp * sp, W.WCurious * cu * MathF.Abs(sp) * near);
                    float ld = Math.Clamp(pd / 150f, 0f, 2f);
                    Paint(I, dirs, tp + pil.Vel * ld, W.WHunt * ag * near);
                    if (P.RingRoles > 0)
                    {
                        // ring slots around the pilot in the plane normal to its velocity, slightly AHEAD (a cut-off)
                        var f = SubstrateCore.Unit(pil.Vel + new Vector3(1e-9f, 0f, 0f));
                        var a = SubstrateCore.Unit(new Vector3(-f.Z + 1e-6f, 1e-6f, f.X + 1e-6f));   // cross(f, up)
                        var b = Vector3.Cross(f, a);
                        float an = 2f * MathF.PI * ((i - pop.Start) % P.RingRoles) / P.RingRoles;
                        var slot = pil.Pos + f * 40f + W.RingR * (MathF.Cos(an) * a + MathF.Sin(an) * b);
                        Paint(I, dirs, slot - p, W.WRing * ag * near);
                    }
                    PaintD(G, dirs, -tp, W.WFlee * fe * prox);
                    if (resting && P.WRestRetreat > 0f) Paint(I, dirs, -tp, P.WRestRetreat);   // winded: fall back, widen
                    // GAZE: a calm agent within creep range slides toward where the pilot will be - only while it is
                    // OUTSIDE the pilot's forward cone; inside it, it freezes (bestiary lurker)
                    if (P.WCreep > 0f && !resting && ph < 0.2f && pd > P.CreepMin && pd < P.CreepR)
                    {
                        bool looked = pil.Vel.LengthSquared() > 1f && Vector3.Dot(SubstrateCore.Unit(pil.Vel), SubstrateCore.Unit(p - pil.Pos)) > P.GazeCos;
                        Watched[i] = looked;
                        if (!looked)
                        {
                            Paint(I, dirs, pil.Pos + pil.Vel * P.CreepLeadS - p, P.WCreep);
                            creeping = true;
                            Home[i] = p;   // it leaves its seat: home is wherever it is now
                        }
                        else freeze = P.Freeze > 0f;
                    }
                    else Watched[i] = false;
                }
                else Watched[i] = false;
                Creeping[i] = creeping;
                Paint(I, dirs, Home[i] - p, W.WHome * (1f - h) * (creeping ? 0f : 1f));
                float r = p.Length();
                Paint(I, dirs, -p, Math.Clamp((r - 0.8f * core.R) / (0.15f * core.R), 0f, 1f) * 3f);
                PaintD(G, dirs, p, Math.Clamp((r - 0.85f * core.R) / (0.1f * core.R), 0f, 1f) * 3f);
                if (pop.BandOuter > 0f)
                {
                    // the species' pen (FaunaConfigurationSO band): steered back in, never walled
                    float soft = 0.1f * MathF.Max(pop.BandOuter - pop.BandInner, 50f);
                    Paint(I, dirs, -p, Math.Clamp((r - pop.BandOuter) / soft, 0f, 1f) * 2f);
                    Paint(I, dirs, p, Math.Clamp((pop.BandInner - r) / soft, 0f, 1f) * 2f);
                }
                if (cnt > 0f && !P.SpacingSpring)
                {
                    float sc = cnt / P.DensNorm / P.NbrR;
                    var sv = (p - cen) * sc;
                    PaintD(G, dirs, sv, W.WSep * MathF.Min(sv.Length(), 2f));
                }
                PaintD(G, dirs, -core.Fields.Grad(SubstrateFields.Alarm, fc), W.WAlarm * (0.3f + fe));
                PaintD(G, dirs, -core.Fields.Grad(SubstrateFields.Threat, fc), W.WThreat * (0.3f + fe));

                // context choice: momentum, soft danger mask, soft-argmax around the best direction
                var cur = IDir[i];
                float mx = -1e30f;
                for (int d = 0; d < dirs.Length; d++)
                {
                    float c = Vector3.Dot(cur, dirs[d]);
                    if (c > 0f) I[d] += P.Momentum * c;
                    float e = I[d] * (1f - Math.Clamp(G[d], 0f, 1f)) - 0.25f * MathF.Max(G[d] - 1f, 0f);
                    I[d] = e;
                    if (e > mx) mx = e;
                }
                var v = Vector3.Zero;
                if (mx > 1e-6f)
                    for (int d = 0; d < dirs.Length; d++)
                    {
                        float w = I[d] - 0.75f * mx;
                        if (w > 0f) v += w * w * dirs[d];
                    }
                float nn = v.Length();
                if (nn > 1e-9f)
                {
                    float bl = P.IntentBlend;
                    var o = bl * cur + (1f - bl) * (v / nn);
                    IDir[i] = o / MathF.Max(o.Length(), 1e-9f);
                }
                float urg = MathF.Max(W.WFlee > 0f ? fe : 0f, ag);
                float speed = W.Speed * (1f + (W.Burst - 1f) * urg);
                if (creeping) speed = P.CreepSpeed;
                if (resting) speed *= P.RestSpeed;
                ISpeed[i] = speed;
            }

            // ── integrate (bounded turn and acceleration: smooth by construction) ──
            if (freeze)
            {
                Vel[i] = Vector3.Zero;   // looked at: dead still
            }
            else
            {
                float turn = Rs.Turn + (Rg.Turn - Rs.Turn) * ph;
                float acl = Rs.Accel + (Rg.Accel - Rs.Accel) * ph;
                var vel = Vel[i];
                float vs = vel.Length();
                var hd = vs > 1e-6f ? vel / vs : IDir[i];
                var t = IDir[i];
                float c = Math.Clamp(Vector3.Dot(hd, t), -1f, 1f);
                float ang = MathF.Acos(c);
                float kk = MathF.Min(1f, turn * dt / MathF.Max(ang, 1e-6f));
                var nd = hd + (t - hd) * kk;
                nd /= MathF.Max(nd.Length(), 1e-9f);
                float ns = vs + Math.Clamp(ISpeed[i] - vs, -acl * dt, acl * dt);
                Vel[i] = nd * ns;
                Pos[i] = p + Vel[i] * dt;
                ClampMembrane(core, i);
            }
            Phase[i] = ph + dt * P.QRate * (QTarget[i] - ph);
        }

        static void ClampMembrane(SubstrateCore core, int i)
        {
            float r = core.Pos[i].Length();
            if (r > 0.98f * core.R) core.Pos[i] *= 0.98f * core.R / r;
        }

        static void Paint(Span<float> I, Vector3[] dirs, Vector3 v, float w)
        {
            float n = v.Length();
            if (n <= 1e-9f || w == 0f) return;
            v /= n;
            for (int d = 0; d < dirs.Length; d++)
            {
                float c = Vector3.Dot(v, dirs[d]);
                if (c > 0f) I[d] += c * w;
            }
        }

        static void PaintD(Span<float> G, Vector3[] dirs, Vector3 v, float w)
        {
            float n = v.Length();
            if (n <= 1e-9f || w == 0f) return;
            v /= n;
            for (int d = 0; d < dirs.Length; d++)
            {
                float c = Vector3.Dot(v, dirs[d]);
                if (c > 0f) G[d] += c * c * w;
            }
        }
    }
}
