// The ARMS RACE (Docs/SUBSTRATE_FAUNA.md §11): the lab's co-evolved predator and prey swarms, ported from
// Tools/NCA/arms_sim.py on the research branch (cece/gifted-curie-x2cpd0), with the policies of run a9 - the selfish
// herd - at generation 1500 (Tools/NCA/results/arms/NOTE.md, "most FUN pair"):
//   - the SHOAL: tadpole grazers that school. Nothing in their training rewarded grouping; individual-level selection
//     (four competing tribes per pond) made them form aligned schools of 10-12 (local polarisation 0.53 -> 0.86).
//   - the HARRIERS: hunters that sprint on stamina, pin prey on the pond's wall and, against schools, run in packs
//     (pack share 0.26 -> 0.38). Every catch is a burst - the telegraph - and a catch is followed by 1 s of handling.
//
// The two MLPs are run exactly as the lab runs them (SubstrateArmsSim: the same body-frame inputs, the same steering,
// turn limits, membrane, swept catch and confusion rule), inside the lab's 200 u POND - a sphere in the cell whose wall is
// the lab's membrane, because the policies learned to use it (prey avoid it, predators pin on it). Everything else is
// the substrate's: the agents are ordinary substrate agents of two populations (stock = body, proxies, the danger tier
// on screen, index entries, eating, breeding, starving, the crystal on death). What the game changes, and why
// (Docs/SUBSTRATE_FAUNA.md §11.2):
//   - FOOD: the lab grazes a 12^3 food grid; the game's food is real flora. The pond's grid is the flora hearts inside
//     it, splatted with the lab's patch shape (sigma 30 u, capped at 1), so the prey read food the way they learned to;
//     a hungry prey eats a real leaf (the substrate's EatRequests) and its body grows by exactly that volume;
//   - SATIETY: the lab's satiety gate (no burst above 1.25 x birth mass; the open economy collapses without it) is a
//     hunger gate here - a harrier bursts only while at least SatedHunger hungry - because a substrate body does not
//     burn down (its stock is conserved mass), its hunger does;
//   - a CATCH is the substrate's food web: the prey dies through its own proxy (crystal and all) and its stock becomes
//     the harrier's body (SubstratePredation). Until its owner has eaten it, a caught prey is out of the pond's sight;
//   - VESSELS are what the lab's player test made them: the prey perceive a vessel as a predator, the harriers as prey,
//     and its hull (+ VesselPad) is solid to both;
//   - the BURST TELL is the pack's fair-burn rule (#1003): a bursting harrier stretches into a streak at once (phase 1:
//     the gregarious regime's aspect), and turns DANGEROUS only after StrikeWindupS (0.4 s) of continuous burst;
//   - births and starvation are the substrate's (BirthStock, StarveS) - every life form feeds and breeds before it dies.
//
// Pure C# (System.Numerics, no UnityEngine): compiled and RUN by Tools/Build/substrate_harness (group arms), which
// matches it against the lab's own observations, outputs and steps (arms_fixture.json) and its behaviour numbers.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The arms-race block of a species (<see cref="SubstrateSpeciesParams.Arms"/>). Role 0 = not an arms species. The
    /// pond physics below the role are the lab's <c>arms_sim.Cfg</c> verbatim (the harness asserts them against
    /// arms_fixture.json); both species of a pond carry the same numbers, and the pond runs on its prey species' copy.
    /// </summary>
    [Serializable]
    public sealed class SubstrateArmsParams
    {
        /// <summary>0 = none, 1 = the prey (the shoal), 2 = the predator (the harriers).</summary>
        public int Role;

        // ── the lab's world (arms_sim.Cfg) ──
        /// <summary>The pond's radius: its wall is the lab's membrane.</summary>
        public float PondR = 200f;
        /// <summary>Prey: top speed (sustained), acceleration, turn rate (rad/s), perception, same-kind spacing, body radius.</summary>
        public float PreyV = 60f, PreyAcc = 300f, PreyTurn = 10f, PreyR = 50f, PreyR0 = 5f, PreySize = 2.5f;
        /// <summary>Predator: cruise, BURST, acceleration, turn rate (rad/s), perception, spacing, body radius.</summary>
        public float PredV = 45f, PredBurst = 100f, PredAcc = 150f, PredTurn = 3f, PredR = 80f, PredR0 = 14f, PredSize = 5f;
        /// <summary>Burst stamina: drained per second of burst (3 s from full), refilled per second not bursting (8 s).</summary>
        public float StamDrain = 1f / 3f, StamRegen = 0.125f;
        /// <summary>A catch: contact radius, the handling time after it (slowed to half, no catch), the confusion effect
        /// (catch probability 1 / (1 + Confusion x crowd), crowd = other prey within ConfR of the target).</summary>
        public float CatchR = 7.5f, Handle = 1f, Confusion = 0.3f, ConfR = 10f;
        /// <summary>The food grid (G^3 over the pond's bounding cube) and the lab's patch width (u).</summary>
        public int G = 12;
        public float FoodSigma = 30f;

        // ── game only (Docs/SUBSTRATE_FAUNA.md §11.2) ──
        /// <summary>A harrier may burst only while at least this hungry (the lab's satiety gate, by hunger: one catch feeds
        /// it ~30 s of not bursting).</summary>
        public float SatedHunger = 0.3f;
        /// <summary>Food-point volume that reads as a full food patch (1) at its centre. The cell senses one unit per living
        /// plant heart (SubstrateCellHost.SenseFood), so each plant in the pond is one of the lab's patches.</summary>
        public float FoodFull = 1f;
        /// <summary>A vessel's hull is solid out to its radius + this (the lab's player test pads it by 4 u).</summary>
        public float VesselPad = 4f;
        /// <summary>A caught prey its owner has not eaten after this long (s) rejoins the pond (the kill did not land).</summary>
        public float EatenTimeout = 3f;

        public SubstrateArmsParams Clone() => (SubstrateArmsParams)MemberwiseClone();

        /// <summary>(lab name, value) for every lab number - the fidelity gate's list.</summary>
        public void Visit(Action<string, float> f)
        {
            f("R_cell", PondR); f("prey_v", PreyV); f("prey_acc", PreyAcc); f("prey_turn", PreyTurn); f("prey_R", PreyR);
            f("prey_r0", PreyR0); f("prey_size", PreySize); f("pred_v", PredV); f("pred_burst", PredBurst);
            f("pred_acc", PredAcc); f("pred_turn", PredTurn); f("pred_R", PredR); f("pred_r0", PredR0);
            f("pred_size", PredSize); f("stam_drain", StamDrain); f("stam_regen", StamRegen); f("catch_r", CatchR);
            f("handle", Handle); f("confusion", Confusion); f("conf_r", ConfR); f("G", G);
        }
    }

    /// <summary>
    /// One pond of the lab's world: the prey and predator arrays (positions relative to the pond's centre), their body
    /// frames, the lab's observation, the two MLPs and one physics step - <c>arms_sim.observe</c>, <c>mlp</c> and
    /// <c>apply</c> line for line. Slots are fixed (an agent keeps its slot); A* marks the living ones.
    /// </summary>
    public sealed class SubstrateArmsSim
    {
        public const int PreyIn = 31, PreyOut = 4, PredIn = 36, PredOut = 5, Hid = 32, MaxGhosts = 8;

        public readonly SubstrateArmsParams C;
        public readonly int CapQ, CapP;
        public float Dt = 0.1f;

        // prey (q) and predators (p): position (pond-relative), velocity, body frame (forward, up), alive, signal
        public readonly Vector3[] Pq, Vq, Fq, Uq, Pp, Vp, Fp, Up;
        public readonly bool[] Aq, Ap, Burst, MayBurst;
        public readonly float[] Sq, Sp, Stam, Hand;
        /// <summary>The food grid, G^3, x-major ([i, j, k] = (i G + j) G + k). Null = no food anywhere.</summary>
        public float[] Food;
        /// <summary>Vessels: position, velocity (pond-relative), obstacle radius. Seen by the prey as predators, by the
        /// predators as prey (the lab's player test), and solid to both.</summary>
        public readonly Vector3[] GPos = new Vector3[MaxGhosts], GVel = new Vector3[MaxGhosts];
        public readonly float[] GRad = new float[MaxGhosts];
        public int NG;

        /// <summary>This step's observations and MLP outputs (row per slot).</summary>
        public readonly float[] Xq, Xp, Oq, Op;
        /// <summary>This step's catch attempts (predator slot, prey slot, crowd) and catches (predator slot, prey slot).</summary>
        public readonly List<(int p, int q, int crowd)> Attempts = new();
        public readonly List<(int p, int q)> Catches = new();
        /// <summary>Each predator's perceived nearest prey this step (a prey slot, CapQ + g for vessel g, -1 none).</summary>
        public readonly int[] Target;

        readonly Vector3[] _Rq, _Rp, _P0q, _P0p;
        readonly float[] _dqq, _dpp, _crowd, _h1 = new float[Hid], _h2 = new float[Hid];

        public SubstrateArmsSim(SubstrateArmsParams c, int capQ, int capP)
        {
            C = c; CapQ = Math.Max(0, capQ); CapP = Math.Max(0, capP);
            Pq = new Vector3[CapQ]; Vq = new Vector3[CapQ]; Fq = new Vector3[CapQ]; Uq = new Vector3[CapQ]; Aq = new bool[CapQ]; Sq = new float[CapQ];
            Pp = new Vector3[CapP]; Vp = new Vector3[CapP]; Fp = new Vector3[CapP]; Up = new Vector3[CapP]; Ap = new bool[CapP]; Sp = new float[CapP];
            Stam = new float[CapP]; Hand = new float[CapP]; Burst = new bool[CapP]; MayBurst = new bool[CapP]; Target = new int[CapP];
            Xq = new float[CapQ * PreyIn]; Xp = new float[CapP * PredIn]; Oq = new float[CapQ * PreyOut]; Op = new float[CapP * PredOut];
            _Rq = new Vector3[CapQ]; _Rp = new Vector3[CapP]; _P0q = new Vector3[CapQ]; _P0p = new Vector3[CapP];
            _dqq = new float[CapQ * CapQ]; _dpp = new float[CapP * CapP]; _crowd = new float[CapQ];
            for (int k = 0; k < CapP; k++) MayBurst[k] = true;
        }

        // ───────────────────────────────────────────────────────────── helpers (arms_sim._norm, _frame, _to_local)

        static float Norm(Vector3 v) => MathF.Sqrt(v.LengthSquared() + 1e-6f);

        static readonly Vector3 Alt = new Vector3(0.31f, 0.83f, 0.47f);

        /// <summary>Re-orthonormalise a body frame (forward, right, up) - parallel transport, no world up.</summary>
        static void Frame(ref Vector3 fwd, ref Vector3 up, out Vector3 right)
        {
            var f = fwd / Norm(fwd);
            var r = Vector3.Cross(f, up);
            float rn = Norm(r);
            if (rn < 1e-3f) { r = Vector3.Cross(f, Alt); rn = Norm(r); }
            r /= rn;
            fwd = f; up = Vector3.Cross(r, f); right = r;
        }

        static Vector3 Local(Vector3 v, Vector3 f, Vector3 r, Vector3 u) => new Vector3(Vector3.Dot(v, f), Vector3.Dot(v, r), Vector3.Dot(v, u));

        static float Dist(Vector3 a, Vector3 b) => MathF.Sqrt(MathF.Max(Vector3.DistanceSquared(a, b), 1e-6f));

        int CellIndex(float x)
        {
            int i = (int)((x + C.PondR) / (2f * C.PondR) * C.G);
            return Math.Clamp(i, 0, C.G - 1);
        }

        /// <summary>The food at a position and its central-difference gradient (arms_sim._sample_food).</summary>
        void SampleFood(Vector3 p, out float val, out Vector3 grad)
        {
            val = 0f; grad = Vector3.Zero;
            if (Food == null) return;
            int G = C.G, i = CellIndex(p.X), j = CellIndex(p.Y), k = CellIndex(p.Z);
            float F(int a, int b, int c) => Food[(a * G + b) * G + c];
            val = F(i, j, k);
            grad = new Vector3(F(Math.Min(i + 1, G - 1), j, k) - F(Math.Max(i - 1, 0), j, k),
                               F(i, Math.Min(j + 1, G - 1), k) - F(i, Math.Max(j - 1, 0), k),
                               F(i, j, Math.Min(k + 1, G - 1)) - F(i, j, Math.Max(k - 1, 0)));
        }

        // ───────────────────────────────────────────────────────────── observe (arms_sim.observe)

        public void Observe()
        {
            float RC = C.PondR;
            for (int i = 0; i < CapQ; i++) Frame(ref Fq[i], ref Uq[i], out _Rq[i]);
            for (int i = 0; i < CapP; i++) Frame(ref Fp[i], ref Up[i], out _Rp[i]);
            // same-kind distance matrices (pre-move): reused by the spacing spring and the confusion crowd
            for (int i = 0; i < CapQ; i++) for (int j = 0; j < CapQ; j++) _dqq[i * CapQ + j] = Dist(Pq[i], Pq[j]);
            for (int i = 0; i < CapP; i++) for (int j = 0; j < CapP; j++) _dpp[i * CapP + j] = Dist(Pp[i], Pp[j]);
            // crowding at each prey: living prey within ConfR, itself included (arms_sim crowd_all)
            for (int i = 0; i < CapQ; i++)
            {
                int c = 0;
                for (int j = 0; j < CapQ; j++) if (Aq[j] && _dqq[i * CapQ + j] < C.ConfR) c++;
                _crowd[i] = c;
            }

            // ── prey: self, prey within PreyR, predators (and vessels) within PreyR ──
            for (int i = 0; i < CapQ; i++)
            {
                var P = Pq[i]; var V = Vq[i]; var f = Fq[i]; var r = _Rq[i]; var u = Uq[i];
                float R = C.PreyR;
                int o = i * PreyIn;
                SelfInputs(Xq, ref o, P, V, f, r, u, R, C.PreyV, RC);
                // same kind
                Same(Pq, Vq, Sq, Aq, _dqq, CapQ, i, R, C.PreyV, 10f, f, r, u, Xq, ref o);
                // other kind: predators + vessels
                int cnt = 0, jn = -1; float dmin = float.PositiveInfinity; Vector3 sum = Vector3.Zero;
                for (int j = 0; j < CapP + NG; j++)
                {
                    bool ghost = j >= CapP;
                    if (!ghost && !Ap[j]) continue;
                    var pj = ghost ? GPos[j - CapP] : Pp[j];
                    float d = Dist(P, pj);
                    if (d >= R) continue;
                    cnt++; sum += pj;
                    if (d < dmin) { dmin = d; jn = j; }
                }
                bool has = jn >= 0;
                var off = cnt > 0 ? sum / cnt - P : Vector3.Zero;
                var near = has ? (jn >= CapP ? GPos[jn - CapP] : Pp[jn]) - P : Vector3.Zero;
                var vj = has ? (jn >= CapP ? GVel[jn - CapP] : Vp[jn]) : Vector3.Zero;
                var relv = has ? Local(vj - V, f, r, u) / C.PredBurst : Vector3.Zero;
                Xq[o++] = cnt / 3f;
                Put(Xq, ref o, Local(near, f, r, u) / R);
                Put(Xq, ref o, relv);
                Xq[o++] = has ? 1f - dmin / R : 0f;
                Put(Xq, ref o, Local(off, f, r, u) / R);
            }

            // ── predators: self (+ stamina, handling), predators within PredR, prey (and vessels) within PredR ──
            for (int i = 0; i < CapP; i++)
            {
                var P = Pp[i]; var V = Vp[i]; var f = Fp[i]; var r = _Rp[i]; var u = Up[i];
                float R = C.PredR;
                int o = i * PredIn;
                // sp, stam, hand/handle, then prox, mem, food (the self block with two extra inputs after speed)
                float rad = Norm(P);
                float prox = Math.Clamp(1f - (RC - rad) / R, 0f, 1f);
                var mem = Local(P / rad, f, r, u) * prox;
                SampleFood(P, out float fv, out var fg);
                Xp[o++] = MathF.Sqrt(V.LengthSquared()) / C.PredBurst;
                Xp[o++] = Stam[i];
                Xp[o++] = Hand[i] / C.Handle;
                Xp[o++] = prox; Put(Xp, ref o, mem); Xp[o++] = fv; Put(Xp, ref o, Local(fg, f, r, u));
                Same(Pp, Vp, Sp, Ap, _dpp, CapP, i, R, C.PredBurst, 3f, f, r, u, Xp, ref o);
                // other kind: prey + vessels
                int cnt = 0, jn = -1; float dmin = float.PositiveInfinity; Vector3 sum = Vector3.Zero, vsum = Vector3.Zero;
                for (int j = 0; j < CapQ + NG; j++)
                {
                    bool ghost = j >= CapQ;
                    if (!ghost && !Aq[j]) continue;
                    var pj = ghost ? GPos[j - CapQ] : Pq[j];
                    float d = Dist(P, pj);
                    if (d >= R) continue;
                    cnt++; sum += pj; vsum += ghost ? GVel[j - CapQ] : Vq[j];
                    if (d < dmin) { dmin = d; jn = j; }
                }
                bool has = jn >= 0;
                Target[i] = jn;
                var off = cnt > 0 ? sum / cnt - P : Vector3.Zero;
                var mvel = vsum / MathF.Max(cnt, 1);
                var near = has ? (jn >= CapQ ? GPos[jn - CapQ] : Pq[jn]) - P : Vector3.Zero;
                var vj = has ? (jn >= CapQ ? GVel[jn - CapQ] : Vq[jn]) : Vector3.Zero;
                float crowd = has ? MathF.Max(0f, (jn >= CapQ ? 1f : _crowd[jn]) - 1f) : 0f;
                Xp[o++] = cnt / 20f;
                Put(Xp, ref o, Local(off, f, r, u) / R);
                Put(Xp, ref o, Local(mvel, f, r, u) / C.PreyV);
                Put(Xp, ref o, Local(near, f, r, u) / R);
                Put(Xp, ref o, has ? Local(vj - V, f, r, u) / C.PreyV : Vector3.Zero);
                Xp[o++] = crowd / 5f;
            }
        }

        /// <summary>speed, membrane proximity + outward direction, food + gradient (the prey's self block).</summary>
        void SelfInputs(float[] X, ref int o, Vector3 P, Vector3 V, Vector3 f, Vector3 r, Vector3 u, float R, float vmax, float RC)
        {
            float rad = Norm(P);
            float prox = Math.Clamp(1f - (RC - rad) / R, 0f, 1f);
            var mem = Local(P / rad, f, r, u) * prox;
            SampleFood(P, out float fv, out var fg);
            X[o++] = MathF.Sqrt(V.LengthSquared()) / vmax;
            X[o++] = prox; Put(X, ref o, mem); X[o++] = fv; Put(X, ref o, Local(fg, f, r, u));
        }

        /// <summary>Own kind within R (self excluded): count, mean offset, mean velocity, nearest offset, mean signal
        /// (arms_sim._agg with same=True).</summary>
        static void Same(Vector3[] Pa, Vector3[] Va, float[] Sa, bool[] Aa, float[] d, int n, int i, float R, float vscale, float cntScale,
                         Vector3 f, Vector3 r, Vector3 u, float[] X, ref int o)
        {
            int cnt = 0, jn = -1; float dmin = float.PositiveInfinity, sig = 0f;
            Vector3 sum = Vector3.Zero, vsum = Vector3.Zero;
            for (int j = 0; j < n; j++)
            {
                if (j == i || !Aa[j]) continue;
                float dj = d[i * n + j];
                if (dj >= R) continue;
                cnt++; sum += Pa[j]; vsum += Va[j]; sig += Sa[j];
                if (dj < dmin) { dmin = dj; jn = j; }
            }
            float inv = 1f / MathF.Max(cnt, 1);
            var P = Pa[i];
            X[o++] = cnt / cntScale;
            Put(X, ref o, Local(cnt > 0 ? sum * inv - P : Vector3.Zero, f, r, u) / R);
            Put(X, ref o, Local(vsum * inv, f, r, u) / vscale);
            Put(X, ref o, Local(jn >= 0 ? Pa[jn] - P : Vector3.Zero, f, r, u) / R);
            X[o++] = sig * inv;
        }

        static void Put(float[] X, ref int o, Vector3 v) { X[o++] = v.X; X[o++] = v.Y; X[o++] = v.Z; }

        // ───────────────────────────────────────────────────────────── the policies (arms_sim.mlp)

        public void Decide(float[] prey, float[] pred)
        {
            for (int i = 0; i < CapQ; i++) if (Aq[i]) Mlp(prey, Xq, i * PreyIn, PreyIn, Oq, i * PreyOut, PreyOut);
            for (int i = 0; i < CapP; i++) if (Ap[i]) Mlp(pred, Xp, i * PredIn, PredIn, Op, i * PredOut, PredOut);
        }

        /// <summary>tanh(tanh(tanh(x W1 + b1) W2 + b2) W3 + b3); theta = W1 [in x 32] row-major, b1, W2, b2, W3, b3.</summary>
        void Mlp(float[] th, float[] x, int xo, int din, float[] y, int yo, int dout)
        {
            int w1 = 0, b1 = din * Hid, w2 = b1 + Hid, b2 = w2 + Hid * Hid, w3 = b2 + Hid, b3 = w3 + Hid * dout;
            for (int c = 0; c < Hid; c++)
            {
                float s = 0f;
                for (int a = 0; a < din; a++) s += x[xo + a] * th[w1 + a * Hid + c];
                _h1[c] = MathF.Tanh(s + th[b1 + c]);
            }
            for (int c = 0; c < Hid; c++)
            {
                float s = 0f;
                for (int a = 0; a < Hid; a++) s += _h1[a] * th[w2 + a * Hid + c];
                _h2[c] = MathF.Tanh(s + th[b2 + c]);
            }
            for (int c = 0; c < dout; c++)
            {
                float s = 0f;
                for (int a = 0; a < Hid; a++) s += _h2[a] * th[w3 + a * dout + c];
                y[yo + c] = MathF.Tanh(s + th[b3 + c]);
            }
        }

        // ───────────────────────────────────────────────────────────── the step (arms_sim.apply)

        /// <summary>One physics step of Dt: burst gate and stamina, steering, the spacing spring, vessels as obstacles, the
        /// pond's wall, then the swept catch (a catch draws <paramref name="draw"/> against the confusion probability).</summary>
        public void Apply(Func<double> draw)
        {
            float dt = Dt;
            Attempts.Clear(); Catches.Clear();
            // predators: burst gate on stamina (and satiety), handling slows them
            var vmaxP = new float[CapP];
            for (int i = 0; i < CapP; i++)
            {
                bool want = Op[i * PredOut + 4] > 0f;
                Burst[i] = Ap[i] && want && Stam[i] > 0.02f && Hand[i] <= 0f && MayBurst[i];
                Stam[i] = Math.Clamp(Stam[i] + (Burst[i] ? -C.StamDrain : C.StamRegen) * dt, 0f, 1f);
                vmaxP[i] = (Burst[i] ? C.PredBurst : C.PredV) * (Hand[i] > 0f ? 0.5f : 1f);
                Hand[i] = MathF.Max(Hand[i] - dt, 0f);
            }
            // steering (acceleration in the body frame of this step's observation)
            for (int i = 0; i < CapQ; i++)
            {
                _P0q[i] = Pq[i];
                if (!Aq[i]) { Vq[i] = Vector3.Zero; continue; }
                int o = i * PreyOut;
                var a = Oq[o] * Fq[i] + Oq[o + 1] * _Rq[i] + Oq[o + 2] * Uq[i];
                Steer(ref Vq[i], ref Fq[i], a, dt, C.PreyAcc, C.PreyV, C.PreyTurn);
                Sq[i] = Oq[o + 3];
            }
            for (int i = 0; i < CapP; i++)
            {
                _P0p[i] = Pp[i];
                if (!Ap[i]) { Vp[i] = Vector3.Zero; continue; }
                int o = i * PredOut;
                var a = Op[o] * Fp[i] + Op[o + 1] * _Rp[i] + Op[o + 2] * Up[i];
                Steer(ref Vp[i], ref Fp[i], a, dt, C.PredAcc, vmaxP[i], C.PredTurn);
                Sp[i] = Op[o + 3];
            }
            Move(Pq, Vq, Aq, _P0q, _dqq, CapQ, C.PreyR0, C.PreySize, dt);
            Move(Pp, Vp, Ap, _P0p, _dpp, CapP, C.PredR0, C.PredSize, dt);
            Catch(draw);
        }

        /// <summary>Bounded acceleration, then the turn-rate limit (a slerp of the heading), then the speed cap.</summary>
        static void Steer(ref Vector3 V, ref Vector3 fwd, Vector3 a, float dt, float acc, float vmax, float turn)
        {
            var vn = V + a * (acc * dt);
            float s0 = V.Length(), s1 = vn.Length();
            var f0 = s0 > 1e-3f ? V / MathF.Max(s0, 1e-6f) : fwd;
            var f1 = s1 > 1e-3f ? vn / MathF.Max(s1, 1e-6f) : f0;
            float c = Math.Clamp(Vector3.Dot(f0, f1), -1f, 1f);
            float ang = MathF.Acos(c);
            float lim = turn * dt;
            float t = ang > lim ? lim / MathF.Max(ang, 1e-6f) : 1f;
            float sa = MathF.Sin(ang);
            bool ok = sa > 1e-4f;
            float w0 = ok ? MathF.Sin((1f - t) * ang) / MathF.Max(sa, 1e-6f) : 1f - t;
            float w1 = ok ? MathF.Sin(t * ang) / MathF.Max(sa, 1e-6f) : t;
            var fd = w0 * f0 + w1 * f1;
            fd /= Norm(fd);
            V = fd * MathF.Min(s1, vmax);
            fwd = fd;
        }

        /// <summary>Integrate, the soft same-kind spacing (from the pre-move distances), vessels as solid obstacles, and the
        /// pond's wall (pushed back inside, outward velocity removed).</summary>
        void Move(Vector3[] P, Vector3[] V, bool[] A, Vector3[] P0, float[] d, int n, float r0, float size, float dt)
        {
            const float k = 0.5f;
            for (int i = 0; i < n; i++)
            {
                var push = Vector3.Zero;
                if (A[i])
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || !A[j]) continue;
                        float dij = d[i * n + j];
                        if (dij >= r0) continue;
                        push += (r0 - dij) / (r0 * dij) * (k * r0) * (P0[i] - P0[j]);
                    }
                P[i] = P0[i] + V[i] * dt + push;
            }
            for (int i = 0; i < n; i++)
            {
                for (int g = 0; g < NG; g++)
                {
                    var dv = P[i] - GPos[g];
                    float dd = Norm(dv);
                    float pu = MathF.Max(0f, GRad[g] - dd);
                    if (pu > 0f) P[i] += dv / dd * pu;
                }
                float rad = Norm(P[i]), lim = C.PondR - size;
                if (rad > lim)
                {
                    var nrm = P[i] / rad;
                    P[i] = nrm * lim;
                    float vout = Vector3.Dot(V[i], nrm);
                    if (vout > 0f) V[i] -= vout * nrm;
                }
            }
        }

        /// <summary>Swept catches: per predator in order, the nearest living prey whose closest approach over the step came
        /// inside CatchR; caught with probability 1 / (1 + Confusion x crowd). A catch kills the prey in the pond and starts
        /// the predator's handling.</summary>
        void Catch(Func<double> draw)
        {
            for (int p = 0; p < CapP; p++)
            {
                if (!Ap[p] || Hand[p] > 0f) continue;
                int best = -1; float bd = float.PositiveInfinity;
                for (int q = 0; q < CapQ; q++)
                {
                    if (!Aq[q]) continue;
                    var rel0 = _P0q[q] - _P0p[p];
                    var dr = (Pq[q] - Pp[p]) - rel0;
                    float tt = Math.Clamp(-Vector3.Dot(rel0, dr) / MathF.Max(dr.LengthSquared(), 1e-9f), 0f, 1f);
                    float dm = (rel0 + tt * dr).Length();
                    if (dm < C.CatchR && dm < bd) { bd = dm; best = q; }
                }
                if (best < 0) continue;
                int crowd = -1;
                for (int q = 0; q < CapQ; q++) if (Aq[q] && (Pq[q] - Pq[best]).Length() < C.ConfR) crowd++;
                Attempts.Add((p, best, crowd));
                if (draw() < 1.0 / (1.0 + C.Confusion * Math.Max(crowd, 0)))
                {
                    Aq[best] = false;
                    Hand[p] = C.Handle;
                    Catches.Add((p, best));
                }
            }
        }

        // ───────────────────────────────────────────────────────────── the lab's food signal

        /// <summary>The lab's grazing (Michaelis-Menten per prey, shared out when a cell is crowded) and regrowth toward
        /// <paramref name="template"/> (arms_sim.apply's non-eco food). The game runs it on the grid the policies SEE, so the
        /// shoal moves on from a grazed patch as it learned to; what it eats is real flora (SubstrateArms.Hungry).</summary>
        public void LabGraze(float[] template, float grazeRate = 0.08f, float half = 0.3f, float regrow = 0.05f)
        {
            int G = C.G, n3 = G * G * G;
            var demand = new float[n3];
            var cell = new int[CapQ];
            var want = new float[CapQ];
            for (int i = 0; i < CapQ; i++)
            {
                cell[i] = (CellIndex(Pq[i].X) * G + CellIndex(Pq[i].Y)) * G + CellIndex(Pq[i].Z);
                float fv = Food[cell[i]];
                want[i] = Aq[i] ? grazeRate * Dt * fv / (fv + half) : 0f;
                demand[cell[i]] += want[i];
            }
            for (int i = 0; i < CapQ; i++)
            {
                int c = cell[i];
                float scale = demand[c] > Food[c] ? Food[c] / MathF.Max(demand[c], 1e-9f) : 1f;
                Food[c] -= want[i] * scale;
            }
            for (int c = 0; c < n3; c++) Food[c] += MathF.Max(0f, template[c] - Food[c]) * regrow * Dt;
        }
    }

    /// <summary>One arms population's state (<see cref="SubstratePopulation.Arms"/>): the per-slot body frame, signal,
    /// stamina and handling the lab carries between steps, and - on the prey's state - the pond.</summary>
    public sealed class SubstrateArmsState
    {
        public readonly Vector3[] Fwd, Up;
        public readonly float[] Sig, Stam, Hand;
        /// <summary>Seconds of continuous burst (the telegraph clock), and the tick each slot's agent was born (a changed
        /// BornTick is a new agent: its lab state starts fresh).</summary>
        public readonly float[] BurstHeld;
        public readonly long[] Born;
        /// <summary>Caught and waiting for its owner to eat it: the time of the catch (s), or -1.</summary>
        public readonly float[] EatenAt;
        /// <summary>The pond (prey state only), and its centre in sim space.</summary>
        public SubstrateArmsSim Sim;
        public Vector3 Centre;
        /// <summary>The pond's food capacity from the living plants (the lab's template); Sim.Food is what the shoal sees,
        /// grazed down under it and regrowing toward this, as in the lab.</summary>
        public float[] Template;
        public bool FoodReady;
        public readonly Random Rng;
        public long Catches, Attempts, BurstStarts, Steps;
        readonly int _cap;

        public SubstrateArmsState(int cap, int seed)
        {
            _cap = cap;
            Fwd = new Vector3[cap]; Up = new Vector3[cap]; Sig = new float[cap]; Stam = new float[cap]; Hand = new float[cap];
            BurstHeld = new float[cap]; Born = new long[cap]; EatenAt = new float[cap];
            for (int k = 0; k < cap; k++) { Born[k] = -1; EatenAt[k] = -1f; }
            Rng = new Random(seed);
        }
    }

    /// <summary>The arms-race director: steps every pond in place of the agent kernel (SubstrateCore.BeginStep).</summary>
    public static class SubstrateArms
    {
        /// <summary>The pond's centre in sim space: its population's sector axis at the middle of its band (the pond's
        /// planting pen sits there, so its flora is inside it - author_substrate_fauna.py).</summary>
        public static Vector3 PondCentre(SubstratePopulation pop)
        {
            var ax = pop.HasSector ? pop.SectorAxis : Vector3.UnitY;
            float r = pop.BandOuter > 0f ? 0.5f * (pop.BandInner + pop.BandOuter) : 0f;
            return ax * r;
        }

        /// <summary>One substrate tick for every pond: the prey population with the predator population that hunts it.</summary>
        internal static void Step(SubstrateCore c, ReadOnlySpan<SubstrateFood> food)
        {
            for (int q = 0; q < c.Pops.Count; q++)
            {
                var prey = c.Pops[q];
                if (!Running(prey) || prey.P.Arms.Role != 1) continue;
                SubstratePopulation pred = null;
                for (int o = 0; o < c.Pops.Count; o++)
                {
                    var p = c.Pops[o];
                    if (Running(p) && p.P.Arms.Role == 2 && p.PreyPop == q) { pred = p; break; }
                }
                Pond(c, prey, pred, food);
            }
            // a predator population whose prey is gone (or frozen far away) hunts an empty pond (it scavenges flora)
            for (int q = 0; q < c.Pops.Count; q++)
            {
                var pred = c.Pops[q];
                if (!Running(pred) || pred.P.Arms.Role != 2) continue;
                if (pred.PreyPop >= 0 && Running(c.Pops[pred.PreyPop]) && c.Pops[pred.PreyPop].P.Arms.Role == 1) continue;
                Pond(c, null, pred, food);
            }
        }

        /// <summary>Active, an arms population, and not frozen by the ecology LOD (a frozen block is in no pass: only its
        /// metabolism runs, SubstrateCore.FrozenPopulation).</summary>
        static bool Running(SubstratePopulation p) => p.Active && !p.Frozen && p.Arms != null;

        static void Pond(SubstrateCore c, SubstratePopulation prey, SubstratePopulation pred, ReadOnlySpan<SubstrateFood> food)
        {
            var host = prey ?? pred;
            var S = host.Arms;
            var K = host.P.Arms;
            int capQ = prey?.Cap ?? 0, capP = pred?.Cap ?? 0;
            if (S.Sim == null || S.Sim.CapQ != capQ || S.Sim.CapP != capP)
            {
                S.Sim = new SubstrateArmsSim(K, capQ, capP) { Dt = c.Dt };
                S.Sim.Food = new float[K.G * K.G * K.G];
                S.Template = new float[K.G * K.G * K.G];
                S.FoodReady = false;
            }
            var sim = S.Sim;
            var C0 = S.Centre = PondCentre(host);
            float now = c.T;
            int liveQ = 0;

            // ── into the pond ──
            if (prey != null) liveQ = Load(c, prey, sim.Pq, sim.Vq, sim.Fq, sim.Uq, sim.Aq, sim.Sq, null, null, C0, now, true);
            if (pred != null)
            {
                Load(c, pred, sim.Pp, sim.Vp, sim.Fp, sim.Up, sim.Ap, sim.Sp, sim.Stam, sim.Hand, C0, now, false);
                var P = pred.P;
                for (int k = 0; k < capP; k++) sim.MayBurst[k] = c.Hunger[pred.Start + k] >= K.SatedHunger;
            }
            sim.NG = 0;
            float reach = K.PondR + MathF.Max(K.PreyR, K.PredR);
            foreach (var pil in c.TickPilots)
            {
                if (sim.NG >= SubstrateArmsSim.MaxGhosts) break;
                var rel = pil.Pos - C0;
                if (rel.Length() > reach) continue;
                sim.GPos[sim.NG] = rel; sim.GVel[sim.NG] = pil.Vel; sim.GRad[sim.NG] = pil.Radius + K.VesselPad;
                sim.NG++;
            }
            // the food the policies see: the lab's grid - plants are its patches, grazing draws a cell down and it regrows
            // toward the plants at the lab's rate (what the shoal EATS is real flora, through EatRequests below)
            BuildFood(S.Template, K, food, C0);
            if (!S.FoodReady) { Array.Copy(S.Template, sim.Food, sim.Food.Length); S.FoodReady = true; }
            else for (int k = 0; k < sim.Food.Length; k++) sim.Food[k] = MathF.Min(sim.Food[k], S.Template[k]);

            // ── the lab's step ──
            sim.Observe();
            sim.Decide(SubstrateArmsPolicy.Prey, SubstrateArmsPolicy.Predator);
            var rng = S.Rng;
            sim.Apply(() => rng.NextDouble());
            sim.LabGraze(S.Template);
            S.Steps++;

            // ── back to the substrate ──
            if (prey != null) Store(c, prey, sim.Pq, sim.Vq, sim.Fq, sim.Uq, sim.Aq, sim.Sq, null, null, null, C0);
            if (pred != null) Store(c, pred, sim.Pp, sim.Vp, sim.Fp, sim.Up, sim.Ap, sim.Sp, sim.Stam, sim.Hand, sim.Burst, C0);
            if (pred != null && prey != null)
            {
                var PS = pred.Arms;
                PS.Attempts += sim.Attempts.Count;
                foreach (var (p, q) in sim.Catches)
                {
                    int hunter = pred.Start + p, victim = prey.Start + q;
                    c.ClaimedTick[victim] = c.Tick;
                    prey.Arms.EatenAt[q] = now;
                    c.Vel[victim] = Vector3.Zero;
                    c.PreyRequests.Add(new SubstratePredation { Predator = hunter, Prey = victim });
                    PS.Catches++;
                }
            }
            // food: a hungry prey asks for a real leaf; a harrier grazes flora only when its pond has no prey left
            if (prey != null) Hungry(c, prey);
            if (pred != null && liveQ == 0) Hungry(c, pred);
        }

        /// <summary>Copies a population into the pond's arrays. A new agent (its BornTick changed) starts with the lab's
        /// fresh state: forward = its heading, full stamina, no handling, silent; a newborn split from its parent is
        /// nudged ~1 u apart and sent the other way (arms_sim's birth). Returns the living count.</summary>
        static int Load(SubstrateCore c, SubstratePopulation pop, Vector3[] P, Vector3[] V, Vector3[] F, Vector3[] U, bool[] A,
                        float[] Sg, float[] Stam, float[] Hand, Vector3 C0, float now, bool prey)
        {
            var S = pop.Arms;
            var K = pop.P.Arms;
            int live = 0;
            for (int k = 0; k < pop.Cap; k++)
            {
                int i = pop.Start + k;
                bool alive = c.Alive[i] && !c.Starving[i];
                if (alive && S.Born[k] != c.BornTick[i])
                {
                    bool child = S.Born[k] >= 0 || c.BornTick[i] > 0;
                    S.Born[k] = c.BornTick[i];
                    S.EatenAt[k] = -1f; S.Sig[k] = 0f; S.Stam[k] = 1f; S.Hand[k] = 0f; S.BurstHeld[k] = 0f;
                    var v = c.Vel[i];
                    if (child && c.Tick > 1)
                    {
                        c.Vel[i] = v = -v;
                        c.Pos[i] += new Vector3(Gauss(S.Rng), Gauss(S.Rng), Gauss(S.Rng));
                    }
                    var d = v.LengthSquared() > 1e-6f ? Vector3.Normalize(v) : SubstrateCore.Unit(new Vector3(Gauss(S.Rng), Gauss(S.Rng), Gauss(S.Rng)));
                    S.Fwd[k] = d;
                    S.Up[k] = new Vector3(Gauss(S.Rng), Gauss(S.Rng), Gauss(S.Rng));
                }
                if (alive && S.EatenAt[k] >= 0f)
                {
                    // caught: out of the pond's sight until its owner eats it (or the kill never lands)
                    if (now - S.EatenAt[k] < K.EatenTimeout) alive = false;
                    else S.EatenAt[k] = -1f;
                }
                A[k] = alive;
                P[k] = c.Pos[i] - C0; V[k] = c.Vel[i]; F[k] = S.Fwd[k]; U[k] = S.Up[k]; Sg[k] = alive ? S.Sig[k] : 0f;
                if (Stam != null) { Stam[k] = S.Stam[k]; Hand[k] = S.Hand[k]; }
                if (alive) live++;
            }
            return live;
        }

        static void Store(SubstrateCore c, SubstratePopulation pop, Vector3[] P, Vector3[] V, Vector3[] F, Vector3[] U, bool[] A,
                          float[] Sg, float[] Stam, float[] Hand, bool[] Burst, Vector3 C0)
        {
            var S = pop.Arms;
            var Pp = pop.P;
            float dt = c.Dt;
            for (int k = 0; k < pop.Cap; k++)
            {
                int i = pop.Start + k;
                if (!c.Alive[i] || c.Starving[i]) continue;
                // the kernel's metabolism (every live agent, every tick)
                c.Hunger[i] += Pp.Metabolism * dt;
                c.Steered[i] = true;
                if (!A[k]) continue;   // caught and waiting to be eaten: it holds still
                c.Pos[i] = P[k] + C0; c.Vel[i] = V[k];
                S.Fwd[k] = F[k]; S.Up[k] = U[k]; S.Sig[k] = Sg[k];
                c.IDir[i] = F[k]; c.ISpeed[i] = V[k].Length();
                if (Stam != null)
                {
                    S.Stam[k] = Stam[k]; S.Hand[k] = Hand[k];
                    bool b = Burst[k];
                    if (b && S.BurstHeld[k] <= 0f) S.BurstStarts++;
                    S.BurstHeld[k] = b ? S.BurstHeld[k] + dt : 0f;
                    // the burst is the hunt's whole telegraph: phase 1 stretches the body into a streak (the gregarious
                    // aspect) at once, and the world pass's bite wind-up (StrikeWindupS) makes it dangerous only after
                    // that long - the pack's fair-burn rule, unchanged
                    c.Phase[i] = b ? 1f : 0f;
                    c.QTarget[i] = c.Phase[i];
                    c.Aggr[i] = 1f;
                }
                else
                {
                    c.Phase[i] = 0f; c.QTarget[i] = 0f; c.Aggr[i] = 0f;
                }
            }
        }

        static void Hungry(SubstrateCore c, SubstratePopulation pop)
        {
            var P = pop.P;
            for (int k = 0; k < pop.Cap; k++)
            {
                int i = pop.Start + k;
                if (!c.Alive[i] || c.Starving[i] || pop.Arms.EatenAt[k] >= 0f) continue;
                if (MathF.Min(1f, c.Hunger[i]) > P.EatHunger) c.EatRequests.Add(i);
            }
        }

        /// <summary>The pond's food grid from the real flora: each food point inside reach adds its volume / FoodFull in
        /// the lab's patch shape (a Gaussian of FoodSigma), capped at 1, and nothing outside 0.95 of the pond (the lab's
        /// template mask).</summary>
        static void BuildFood(float[] F, SubstrateArmsParams K, ReadOnlySpan<SubstrateFood> food, Vector3 C0)
        {
            int G = K.G;
            float R = K.PondR, h = 2f * R / G, s2 = 2f * K.FoodSigma * K.FoodSigma, cut = R * 1.7320508f + 3f * K.FoodSigma;
            Array.Clear(F, 0, F.Length);
            for (int j = 0; j < food.Length; j++)
            {
                if (food[j].Volume <= 0f) continue;
                var p = food[j].Pos - C0;
                if (p.Length() > cut) continue;
                float w = food[j].Volume / K.FoodFull;
                for (int a = 0; a < G; a++)
                {
                    float x = (a + 0.5f) * h - R - p.X;
                    for (int b = 0; b < G; b++)
                    {
                        float y = (b + 0.5f) * h - R - p.Y;
                        for (int e = 0; e < G; e++)
                        {
                            float z = (e + 0.5f) * h - R - p.Z;
                            F[(a * G + b) * G + e] += w * MathF.Exp(-(x * x + y * y + z * z) / s2);
                        }
                    }
                }
            }
            for (int a = 0; a < G; a++)
                for (int b = 0; b < G; b++)
                    for (int e = 0; e < G; e++)
                    {
                        var cc = new Vector3((a + 0.5f) * h - R, (b + 0.5f) * h - R, (e + 0.5f) * h - R);
                        int ix = (a * G + b) * G + e;
                        F[ix] = cc.Length() < 0.95f * R ? MathF.Min(1f, F[ix]) : 0f;
                    }
        }

        static float Gauss(Random r) => (float)(Math.Sqrt(-2.0 * Math.Log(1.0 - r.NextDouble())) * Math.Cos(2.0 * Math.PI * r.NextDouble()));
    }
}
