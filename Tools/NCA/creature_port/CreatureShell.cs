// The creature's vessel-reaction SHELL (Tools/NCA/creature_model.py, CreatureRule._shell), ported to plain C#
// (System.Numerics, no Unity) in the shape a Burst job takes: struct-of-arrays per swarm, no allocation per
// step, a uniform hash grid for the startle relay (no O(n^2)). It is BODY-AGNOSTIC: call Step() after the body
// (learned rule, field slots, anything) has moved the tadpoles; it displaces them with an elastic offset that
// springs back when calm, and reports per-tadpole flags (startle, danger, mob) for the renderer.
//
// Parity: Tools/NCA/creature_port_check.py dumps (input, state, output) cases from the Python shell and this
// program replays them (tell jitter off, the one random term).
using System;
using System.Collections.Generic;
using System.Numerics;

public struct Vessel { public Vector3 C, V; public float R; }

public sealed class ShellCfg
{
    public float Sense = 3.0f, Lookahead = 10f, Relay = 0.75f, RelayR = 4.5f, Decay = 0.9f;
    public float[] Flee = { 0.5f, 0.3f, 2.0f, 2.0f };
    public float FleeSwirl = 0.6f, KRet = 0.10f, MaxOff = 30f;
    public int JetPeriod = 6; public float JetGain = 3.0f, Bell = 0.35f, BodyJet = 0.6f;
    public float Inflate = 0.55f, DangerAt = 0.35f, Tuck = 0.35f, Shield = 0.25f, Dart = 0.8f;
    public float[] Mob = { 0, 0, 0, 1 }; public float MobSpeed = 1.0f, MobR = 1.4f;
    public float[] Escort = { 0, 0.8f, 0, 0.45f }; public float EscortMin = 1.0f, EscortMax = 2.4f, EscortR = 4.0f;
    public bool Tell = true; public int TellSteps = 36; public float TellJitter = 0.7f, TellSwirl = 0.25f, PreGap = 0.08f;
    public float Membrane = 80f; public int MinBody = 32;
}

public sealed class CreatureShell
{
    public readonly ShellCfg C; public readonly int Cap;
    // per tadpole state
    public readonly Vector3[] Off; public readonly float[] Startle;
    // per tadpole flags (renderer)
    public readonly bool[] Danger, Mobbing; public float TellAmp;
    // per swarm state
    public float Threat, Swell, TellLeft; public int Maj = -1, T;
    public Vector3 Drift;              // translation applied this step (escort) - a body with a frame moves it too
    // scratch
    readonly Vector3[] react; readonly float[] st2; readonly int[] cellHead, next; readonly Random rng = new Random(1234);
    const int GridBits = 12; const int GridSize = 1 << GridBits;

    public CreatureShell(int cap, ShellCfg cfg = null)
    {
        Cap = cap; C = cfg ?? new ShellCfg();
        Off = new Vector3[cap]; Startle = new float[cap]; Danger = new bool[cap]; Mobbing = new bool[cap];
        react = new Vector3[cap]; st2 = new float[cap]; cellHead = new int[GridSize]; next = new int[cap];
    }

    static float Clamp01(float x) => x < 0 ? 0 : (x > 1 ? 1 : x);
    static Vector3 SafeNorm(Vector3 v, float min) { float n = v.Length(); return v / MathF.Max(n, min); }

    /// pos/elem/active/hatched: the swarm after the body's step. Modifies pos in place.
    public void Step(Vector3[] pos, int[] elem, bool[] active, bool[] hatched, IReadOnlyList<Vessel> vessels, bool react_ = true)
    {
        int n = Cap; var c = C;
        for (int i = 0; i < n; i++) if (!active[i]) { Off[i] = Vector3.Zero; Startle[i] = 0; }
        // body summary
        float cnt = 0; Vector3 cen = Vector3.Zero; var counts = new float[4];
        for (int i = 0; i < n; i++) if (active[i] && hatched[i]) { cnt++; cen += pos[i]; counts[elem[i]]++; }
        cnt = MathF.Max(cnt, 1); cen /= cnt;
        int maj = 0; for (int e = 1; e < 4; e++) if (counts[e] > counts[maj]) maj = e;
        for (int i = 0; i < n; i++) { react[i] = Vector3.Zero; Startle[i] *= c.Decay; Danger[i] = false; Mobbing[i] = false; }
        Drift = Vector3.Zero;
        bool anyShip = vessels != null && vessels.Count > 0 && react_;
        float t = T;
        bool jetOn = (T % c.JetPeriod) < 2;
        Vector3 up = Vector3.UnitY;
        if (anyShip)
        {
            float thr = Threat;
            foreach (var vs in vessels)
            {
                float sense = c.Sense * vs.R, spd = vs.V.Length();
                Vector3 pvn = vs.V / MathF.Max(spd, 1e-6f);
                Vector3 toShip = vs.C - cen, tsn = SafeNorm(toShip, 1e-3f);
                for (int i = 0; i < n; i++)
                {
                    if (!active[i]) continue;
                    Vector3 rel = pos[i] - cen, relS = pos[i] - vs.C;
                    int e = elem[i];
                    if (maj == 1)       // whale: minorities tuck, the Mass hull closes toward the ship (eggs included, as the reference)
                    {
                        Vector3 tuck = e != 1 ? -c.Tuck * 0.1f * rel : Vector3.Zero;
                        Vector3 shield = e == 1 ? c.Shield * 0.05f * tsn * MathF.Max(0, Vector3.Dot(rel, tsn)) : Vector3.Zero;
                        react[i] += thr * (tuck + shield);
                    }
                    if (!hatched[i]) continue;
                    float dd = MathF.Max(relS.Length(), 1e-3f);
                    float along = Vector3.Dot(relS, pvn);
                    Vector3 lat = relS - along * pvn; float dl = MathF.Max(lat.Length(), 1e-3f); Vector3 latn = lat / dl;
                    float ahead = Clamp01(1 - along / (c.Lookahead * spd + vs.R)) * (along > -vs.R ? 1 : 0);
                    float w = MathF.Max(Clamp01(1 - dl / sense) * ahead, Clamp01(1 - dd / sense));
                    Startle[i] = MathF.Max(Startle[i], Clamp01(1.4f * w));
                    Vector3 radial = relS / dd;
                    if (spd < 1e-3f) latn = radial;
                    Vector3 swirl = Vector3.Cross(pvn, latn);
                    float gain = c.Flee[e];
                    if (e == 2) gain *= c.JetGain * (jetOn ? 1 : 0) + 0.15f;
                    Vector3 flee = w * gain * (0.75f * latn + 0.25f * radial + c.FleeSwirl * 0.5f * swirl);
                    if (e == 3)
                    {
                        float zig = MathF.Sin(t * 1.3f + i * 2.1f);
                        flee += w * c.Dart * zig * Vector3.Cross(latn, radial);
                    }
                    if (spd < c.MobSpeed)
                    {
                        float mk = c.Mob[e];
                        float wMob = Clamp01(1 - dd / (2.5f * sense)) * mk;
                        Vector3 tang = SafeNorm(Vector3.Cross(up, radial), 1e-3f);
                        Vector3 orbit = 0.4f * (c.MobR * vs.R - dd) * radial + 1.5f * tang;
                        react[i] += wMob * orbit;
                        flee *= (1 - mk);
                        if (wMob > 0.05f) Mobbing[i] = true;
                    }
                    react[i] += flee;
                }
                if (spd >= c.EscortMin && spd <= c.EscortMax)
                {
                    float r2 = 0; for (int i = 0; i < n; i++) if (active[i] && hatched[i]) r2 += (pos[i] - cen).LengthSquared();
                    float rms = MathF.Sqrt(r2 / cnt);
                    float headingIn = MathF.Max(0, -Vector3.Dot(tsn, pvn));
                    float eg = c.Escort[maj] * Clamp01(1 - toShip.Length() / (c.EscortR * rms)) * (headingIn < 0.3f ? 1 : 0);
                    if (eg != 0)
                    {
                        for (int i = 0; i < n; i++) if (active[i] && hatched[i]) pos[i] += eg * vs.V;
                        Drift += eg * vs.V;
                    }
                }
                if (maj == 2)
                {
                    Vector3 bodyjet = -tsn * c.BodyJet * (jetOn ? 1 : 0);
                    for (int i = 0; i < n; i++)
                    {
                        if (!active[i]) continue;
                        // as the Python reference: the bell term uses rel from the pre-escort centroid
                        Vector3 rel = pos[i] - (hatched[i] ? Drift : Vector3.Zero) - cen;
                        react[i] += thr * (bodyjet - c.Bell * 0.1f * rel * (jetOn ? 1 : 0));
                    }
                }
            }
        }
        // startle wave over a hash grid (cell = RelayR)
        bool anyStartle = false; for (int i = 0; i < n; i++) if (Startle[i] > 0.02f) { anyStartle = true; break; }
        if (anyStartle)
        {
            Array.Fill(cellHead, -1);
            float inv = 1f / c.RelayR;
            for (int i = 0; i < n; i++)
            {
                if (!(active[i] && hatched[i])) continue;
                int h = Hash(pos[i], inv); next[i] = cellHead[h]; cellHead[h] = i;
            }
            float r2 = c.RelayR * c.RelayR;
            for (int i = 0; i < n; i++)
            {
                st2[i] = 0;
                if (!(active[i] && hatched[i])) continue;
                float best = 0;
                int cx = (int)MathF.Floor(pos[i].X * inv), cy = (int)MathF.Floor(pos[i].Y * inv), cz = (int)MathF.Floor(pos[i].Z * inv);
                for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                    for (int j = cellHead[HashC(cx + dx, cy + dy, cz + dz)]; j >= 0; j = next[j])
                        if (Vector3.DistanceSquared(pos[i], pos[j]) < r2 && Startle[j] > best) best = Startle[j];
                st2[i] = MathF.Max(Startle[i], best * c.Relay);
            }
            for (int i = 0; i < n; i++) Startle[i] = (active[i] && hatched[i]) ? st2[i] : 0;
        }
        float sm = 0; for (int i = 0; i < n; i++) if (active[i] && hatched[i]) sm += Startle[i];
        sm /= cnt;
        Threat = 0.85f * Threat + 0.15f * MathF.Min(3 * sm, 1);
        float swellT = maj == 0 ? c.Inflate * Threat : 0;
        float dsw = swellT - Swell; Swell = swellT;
        Vector3 cen2 = cen;                                           // pufferfish swell about the pre-escort centroid
        for (int i = 0; i < n; i++) if (active[i] && hatched[i]) react[i] += dsw * (pos[i] - Drift - cen2);
        TellAmp = 0;
        if (c.Tell)
        {
            bool changed = Maj >= 0 && maj != Maj && cnt >= c.MinBody;
            TellLeft = changed ? c.TellSteps : MathF.Max(TellLeft - 1, 0);
            Maj = maj;
            var sh = new float[4]; for (int e = 0; e < 4; e++) sh[e] = counts[e] / cnt;
            Array.Sort(sh); float gap = sh[3] - sh[2];
            float pre = Clamp01((c.PreGap - gap) / c.PreGap) * (cnt >= c.MinBody ? 1 : 0);
            float amp = MathF.Max(TellLeft / c.TellSteps, pre);
            if (amp > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!(active[i] && hatched[i])) continue;
                    Vector3 rel = pos[i] - Drift - cen;
                    Vector3 jit = c.TellJitter > 0 ? new Vector3(Gauss(), Gauss(), Gauss()) * c.TellJitter : Vector3.Zero;
                    react[i] += amp * (jit + c.TellSwirl * 0.1f * Vector3.Cross(up, rel));
                }
                TellAmp = amp;
            }
        }
        // elastic offset: off <- off * (1 - k_ret * calm) + reaction, clamped; pos += off_new - off_old
        for (int i = 0; i < n; i++)
        {
            if (!active[i]) { Off[i] = Vector3.Zero; continue; }
            float calm = 1 - Clamp01(Startle[i]);
            Vector3 nw = Off[i] * (1 - c.KRet * calm) + react[i];
            float nn = nw.Length(); if (nn > c.MaxOff) nw *= c.MaxOff / nn;
            pos[i] += nw - Off[i];
            float rad = MathF.Max(pos[i].Length(), 1e-6f);
            if (rad > c.Membrane) pos[i] -= 0.5f * (rad - c.Membrane) * pos[i] / rad;
            Off[i] = nw;
            Danger[i] = elem[i] == 0 && Startle[i] > c.DangerAt && hatched[i];
        }
        T++;
    }

    float Gauss() { double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble(); return (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2)); }
    static int HashC(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) & (GridSize - 1));
    static int Hash(Vector3 p, float inv) => HashC((int)MathF.Floor(p.X * inv), (int)MathF.Floor(p.Y * inv), (int)MathF.Floor(p.Z * inv));
}
