using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>A navigator mote as the AI sees it on the board: where its shadow is, how it is drifting, its element
    /// (0 Charge, 1 Mass, 2 Space, 3 Time) and whether it is near enough the plane to take.</summary>
    public struct KiteMote
    {
        public float X, Z, VX, VZ;
        public int Element;
        public bool Touchable;
    }

    /// <summary>A hostile prism or a rival hull on the board. <see cref="Id"/> must be stable across frames.</summary>
    public struct KiteTarget
    {
        public int Id;
        public float X, Z, VX, VZ;
    }

    /// <summary>
    /// The navigator's AI squad mate (the lab's <c>navAI</c> / <c>planLoop</c>, ported line for line): it moves an AIM point
    /// the way a thumb does, at a finite speed with aim noise, and presses it. It never writes the kite; the kite reads the
    /// aim exactly as it reads a human's (the input-only rule, <c>/vessel-ai</c>). Each think it charges a ram when the kite
    /// holds power and a rival is in reach, else plans a loop round the fattest one-element cluster, else pops the nearest
    /// hostile prism. Difficulty changes the thumb's speed, reaction, aim noise and judgement, never the kite.
    /// </summary>
    public sealed class KiteNavigatorAI
    {
        public enum Skill { Easy = 0, Medium = 1, Hard = 2 }

        struct Profile { public float Thumb, Think, Noise, Purity, ReachAware, Lead; }

        // board radii per second, seconds, board radii, probabilities, lead fraction (the lab's NSKILL)
        static readonly Profile[] Profiles =
        {
            new() { Thumb = 1.2f, Think = 0.7f,  Noise = 0.04f,  Purity = 0.4f, ReachAware = 0.5f,  Lead = 0f },
            new() { Thumb = 2.0f, Think = 0.4f,  Noise = 0.02f,  Purity = 0.8f, ReachAware = 0.85f, Lead = 0.6f },
            new() { Thumb = 3.0f, Think = 0.2f,  Noise = 0.008f, Purity = 1f,   ReachAware = 1f,    Lead = 1f },
        };

        enum PlanKind { None, Loop, Ram, Pop }

        public KiteVec Aim;
        public bool Down;
        public Skill Level = Skill.Hard;

        readonly Random _rng;
        readonly List<KiteVec> _pts = new();
        readonly List<int> _cand = new();
        PlanKind _plan;
        int _ptIndex, _targetId;
        float _until, _thinkT, _jx, _jz, _t;

        public KiteNavigatorAI(int seed) { _rng = new Random(seed); }

        Profile P => Profiles[(int)Level];

        public void Reset(KiteVec at)
        {
            Aim = at; Down = false; _plan = PlanKind.None; _thinkT = 0f; _t = 0f;
        }

        float Gauss()
        {
            double u = Math.Max(1e-9, _rng.NextDouble()), v = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u)) * Math.Cos(2.0 * Math.PI * v));
        }

        void Jitter() { _jx = Gauss() * P.Noise; _jz = Gauss() * P.Noise; }

        // the thumb travels toward its (noisy) goal; the distance returned is to where it is AIMING (L-STU-82)
        float MoveAim(in KiteSettings s, float gx, float gz, float dt)
        {
            float tx = gx + _jx * s.BoardRadius, tz = gz + _jz * s.BoardRadius;
            float dx = tx - Aim.X, dz = tz - Aim.Z, d = MathF.Sqrt(dx * dx + dz * dz), step = P.Thumb * s.BoardRadius * dt;
            if (d <= step) Aim = new KiteVec(tx, tz);
            else Aim = new KiteVec(Aim.X + dx / d * step, Aim.Z + dz / d * step);
            float rx = tx - Aim.X, rz = tz - Aim.Z;
            return MathF.Sqrt(rx * rx + rz * rz);
        }

        /// <summary>
        /// One think-and-move. <paramref name="motes"/>, <paramref name="prisms"/> (hostile, touchable, on the board) and
        /// <paramref name="rivals"/> (touchable, on the board, off their ram cooldown) are this frame's board.
        /// <paramref name="idle"/> leaves the thumb up (the scorecard's idle navigator).
        /// </summary>
        public void Step(in KiteSettings s, float growth, KiteNavigatorCore kite, float charge,
                         IReadOnlyList<KiteMote> motes, IReadOnlyList<KiteTarget> prisms, IReadOnlyList<KiteTarget> rivals,
                         bool anyRival, float dt, bool idle = false)
        {
            _t += dt;
            if (idle) { Down = false; return; }
            _thinkT -= dt;
            if (_plan != PlanKind.None && (_t > _until || (_plan == PlanKind.Loop && _ptIndex >= _pts.Count))) _plan = PlanKind.None;

            if (_thinkT <= 0f)
            {
                _thinkT = P.Think * (0.8f + 0.4f * (float)_rng.NextDouble());
                int rv = -1; float rd = float.MaxValue;
                if (charge >= 1f)
                    for (int i = 0; i < rivals.Count; i++)
                    {
                        float dx = rivals[i].X - kite.Position.X, dz = rivals[i].Z - kite.Position.Z, d = dx * dx + dz * dz;
                        if (d < rd) { rd = d; rv = i; }
                    }
                if (rv >= 0) { _plan = PlanKind.Ram; _targetId = rivals[rv].Id; _until = _t + 2f; Jitter(); }
                else if (_plan != PlanKind.Loop)
                {
                    if (PlanLoop(s, growth, kite, motes, anyRival)) Jitter();
                    else
                    {
                        int bp = -1; float bd = float.MaxValue;
                        for (int i = 0; i < prisms.Count; i++)
                        {
                            float dx = prisms[i].X - kite.Position.X, dz = prisms[i].Z - kite.Position.Z, d = dx * dx + dz * dz;
                            if (d < bd) { bd = d; bp = i; }
                        }
                        if (bp >= 0) { _plan = PlanKind.Pop; _targetId = prisms[bp].Id; _until = _t + 1.5f; }
                        else _plan = PlanKind.None;
                    }
                }
            }

            switch (_plan)
            {
                case PlanKind.None:
                    MoveAim(s, kite.Position.X, kite.Position.Z, dt);
                    Down = false;
                    return;
                case PlanKind.Loop:
                {
                    var q = _pts[Math.Min(_ptIndex, _pts.Count - 1)];
                    if (MoveAim(s, q.X, q.Z, dt) < 0.04f * s.BoardRadius) _ptIndex++;
                    break;
                }
                case PlanKind.Ram:
                {
                    int i = IndexOf(rivals, _targetId);
                    if (i < 0 || charge < 1f) { _plan = PlanKind.None; break; }
                    var r = rivals[i];
                    float lead = Dist(r.X, r.Z, kite.Position) / s.KiteSpeed * P.Lead;
                    MoveAim(s, r.X + r.VX * lead, r.Z + r.VZ * lead, dt);
                    break;
                }
                case PlanKind.Pop:
                {
                    int i = IndexOf(prisms, _targetId);
                    if (i < 0) { _plan = PlanKind.None; break; }
                    var p = prisms[i];
                    float lead = Dist(p.X, p.Z, kite.Position) / s.KiteSpeed * P.Lead;
                    MoveAim(s, p.X + p.VX * lead, p.Z + p.VZ * lead, dt);
                    break;
                }
            }
            Down = true;
        }

        static float Dist(float x, float z, KiteVec k) { float dx = x - k.X, dz = z - k.Z; return MathF.Sqrt(dx * dx + dz * dz); }

        static int IndexOf(IReadOnlyList<KiteTarget> list, int id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return i;
            return -1;
        }

        // the lab's planLoop: the fattest one-element cluster near the kite, circled with a margin, aimed where it will be
        bool PlanLoop(in KiteSettings s, float growth, KiteNavigatorCore kite, IReadOnlyList<KiteMote> motes, bool anyRival)
        {
            float R = s.BoardRadius, rc = MathF.Min(0.16f * R, s.TailLength * R / 7.5f);
            _cand.Clear();
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                if (!KiteNavigatorCore.InDisc(s, m.X, m.Z)) continue;
                if ((float)_rng.NextDouble() < P.ReachAware && !m.Touchable) continue;
                _cand.Add(i);
            }
            int best = -1; float bv = 0f, bcx = 0f, bcz = 0f;
            Span<int> counts = stackalloc int[4];
            foreach (int ai in _cand)
            {
                var a = motes[ai];
                counts.Clear();
                float cx = 0f, cz = 0f; int k = 0;
                foreach (int bi in _cand)
                {
                    var b = motes[bi];
                    float dx = a.X - b.X, dz = a.Z - b.Z;
                    if (dx * dx + dz * dz < rc * rc) { counts[b.Element]++; cx += b.X; cz += b.Z; k++; }
                }
                int kinds = (counts[0] > 0 ? 1 : 0) + (counts[1] > 0 ? 1 : 0) + (counts[2] > 0 ? 1 : 0) + (counts[3] > 0 ? 1 : 0);
                if (kinds > 1 && (float)_rng.NextDouble() < P.Purity) continue;      // a careful navigator loops one element only
                float v = MathF.Pow(growth, counts[a.Element] - 1) * (kinds > 1 ? 0.3f : 1f);
                if (a.Element == 0 && !anyRival) v *= 0.2f;                          // Charge with nobody to ram
                cx /= k; cz /= k;
                v /= 1f + Dist(cx, cz, kite.Position) / R;                            // nearer is better
                if (v > bv) { bv = v; best = ai; bcx = cx; bcz = cz; }
            }
            if (best < 0) return false;

            var bm = motes[best];
            float rr = rc + 10f, lapT = (2f * MathF.PI * rr * 1.25f) / s.KiteSpeed + 0.4f;
            float c0x = bcx + bm.VX * lapT * 0.5f * P.Lead, c0z = bcz + bm.VZ * lapT * 0.5f * P.Lead;
            float a0 = MathF.Atan2(kite.Position.Z - c0z, kite.Position.X - c0x), dir = _rng.NextDouble() < 0.5 ? 1f : -1f;
            _pts.Clear();
            for (int i = 0; i <= 30; i++)
            {
                float ang = a0 + dir * i / 24f * 2f * MathF.PI;
                _pts.Add(KiteNavigatorCore.OnBoard(s, c0x + MathF.Cos(ang) * rr, c0z + MathF.Sin(ang) * rr));
            }
            _plan = PlanKind.Loop; _ptIndex = 0; _until = _t + lapT + 2f;
            return true;
        }
    }
}
