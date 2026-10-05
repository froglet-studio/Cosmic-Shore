// Asserted tests of the SHIPPED SnapTrapCore against the research (Tools/Ecology/flora/snaptrap.py and its
// searched best, results/search_snaptrap_best.json). Docs/THREAT_FLORA.md §6 lists what each one proves.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using CosmicShore.Gameplay;

/// <summary>
/// The research species around the shipped core: snaptrap.py's arena side (roots feed on arena mass, rim teeth
/// burn on contact, plain prisms are rammable, the snap is a hit), with prism positions read from the core's
/// CURRENT geometry (the research's per-step _pose) - the game reads the keyframed pose instead.
/// </summary>
public sealed class SnapTrapSpecies
{
    public readonly SnapTrapCore Core;
    readonly FloraArena _ar;
    readonly Dictionary<(int, string), float> _burnCd = new Dictionary<(int, string), float>();
    readonly Dictionary<(int, string), float> _hot = new Dictionary<(int, string), float>();
    readonly Dictionary<(int, string), float> _seen = new Dictionary<(int, string), float>();
    public readonly List<float> Leads = new List<float>();
    public double CutVolume;
    public int Burns, SnapHits;
    ThreatVessel[] _vessels = new ThreatVessel[4];
    public bool MouthEats;
    const int Cap = 512;

    public SnapTrapSpecies(FloraArena ar, SnapTrapParams p, ulong seed, int nTraps = 40, int clumps = 9, float clumpR = 57.0011f)
    {
        _ar = ar;
        Core = new SnapTrapCore(p, seed, 128);
        var centres = new Vector3[clumps];
        for (int c = 0; c < clumps; c++) centres[c] = ar.Grove(60f);
        for (int i = 0; i < nTraps; i++)
        {
            Vector3 pos = centres[i % clumps] + ar.Rng.GaussianVector() * clumpR;
            int t = Core.AddTrap(pos, ar.Rng.OnUnitSphere(), false);
            Core.CompleteGrowth(t);
        }
    }

    public double MassTotal => Core.Reserve + Core.LaidVolume();

    void SlotPos(int i, int s, out Vector3 pos)
    {
        bool tooth = SnapTrapCore.SlotTooth[s];
        Core.SlotPose(Core.Heart[i], Core.Axis[i], Core.Theta[i], s, SnapTrapCore.SlotR[s], out pos, out _, out _);
    }

    public void Step(float dt)
    {
        int nv = _ar.Pilots.Count;
        if (_vessels.Length < nv) _vessels = new ThreatVessel[nv];
        for (int k = 0; k < nv; k++)
            _vessels[k] = new ThreatVessel { Position = _ar.Pilots[k].Pos, Previous = _ar.Pilots[k].Prev, Radius = 6f };
        Core.Step(dt, _vessels, nv);
        var ev = Core.Events;
        for (int e = 0; e < ev.Count; e++)
        {
            var x = ev[e];
            switch (x.Kind)
            {
                case SnapTrapEventKind.AbsorbRequest:
                {
                    int j = _ar.NearestMass(Core.Heart[x.Trap], Core.P.Root);
                    if (j >= 0) Core.Deposit(x.Trap, _ar.Consume(j));
                    break;
                }
                case SnapTrapEventKind.MouthAbsorbRequest when MouthEats:
                {
                    int j = _ar.NearestMass(x.Position, Core.P.MouthLength * 0.6f);
                    if (j >= 0) Core.Deposit(x.Trap, _ar.Consume(j));
                    break;
                }
                case SnapTrapEventKind.BudRequest:
                    if (Core.LiveCount < Cap) Core.AddTrap(x.Position, x.Axis, true);
                    break;
                case SnapTrapEventKind.Snap:
                {
                    var p = _ar.Pilots[x.Vessel];
                    Credit(x.Trap, p);
                    _ar.Hit(p, "snap");
                    SnapHits++;
                    break;
                }
            }
        }
        // rim teeth burn on contact; plain prisms are rammed (snaptrap.py step / _ram)
        foreach (var p in _ar.Pilots)
        {
            for (int i = 0; i < Core.Count; i++)
            {
                if (!Core.Used[i] || !Core.Alive[i]) continue;
                if ((Core.Heart[i] - p.Pos).Length() > 140f) continue;
                for (int s = 0; s < SnapTrapCore.SlotCount; s++)
                {
                    if (!Core.Occupied[i * SnapTrapCore.SlotCount + s]) continue;
                    SlotPos(i, s, out Vector3 pos);
                    bool tooth = SnapTrapCore.SlotTooth[s];
                    float d = ThreatFloraMath.SegmentPointDistance(p.Prev, p.Pos, pos);
                    if (tooth)
                    {
                        if (d >= 3.5f + 6f) continue;
                        var key = (i * SnapTrapCore.SlotCount + s, p.Name);
                        if (_burnCd.TryGetValue(key, out float until) && until > _ar.T) continue;
                        _burnCd[key] = _ar.T + 1f;
                        if (Core.Caught[i]) continue;
                        Credit(i, p);
                        _ar.Hit(p, "burn");
                        Burns++;
                    }
                    else if (d < 4f + p.Radius)
                    {
                        CutVolume += Core.SlotVolume(s);
                        Core.SlotLost(i, s);
                    }
                }
            }
        }
        // telegraph bookkeeping (per trap, per pilot)
        foreach (var p in _ar.Pilots)
            for (int i = 0; i < Core.Count; i++)
            {
                if (!Core.Used[i]) continue;
                var key = (i, p.Name);
                float dist = (Core.Heart[i] - p.Pos).Length();
                if (dist < FloraArena.View && Core.Alive[i])
                {
                    if (!_seen.ContainsKey(key)) _seen[key] = _ar.T;
                    if (Core.Intent[i] >= 0.5f && !_hot.ContainsKey(key)) _hot[key] = _ar.T;
                }
                if (dist >= FloraArena.View) _seen.Remove(key);
                if (Core.Intent[i] < 0.2f) _hot.Remove(key);
            }
    }

    void Credit(int i, Pilot p)
    {
        var key = (i, p.Name);
        float t0 = _hot.TryGetValue(key, out float h) ? h : _seen.TryGetValue(key, out float s) ? s : _ar.T;
        Leads.Add(MathF.Min(5f, _ar.T - t0));
    }

    /// <summary>snaptrap.py threat_elements: the mouth of every trap that could fire, and every tooth.</summary>
    public void ThreatElements(List<Vector3> pos, List<float> rad)
    {
        pos.Clear(); rad.Clear();
        float L = Core.P.MouthLength * Core.P.GeometryScale;
        for (int i = 0; i < Core.Count; i++)
        {
            if (!Core.Used[i] || !Core.Alive[i] || Core.Grow[i] < 1f) continue;
            if (Core.State[i] != (int)SnapTrapState.Shut)
            {
                pos.Add(Core.Heart[i] + Core.Axis[i] * (Core.P.MouthOffset + L * 0.5f));
                rad.Add(L * 0.6f);
            }
        }
        for (int i = 0; i < Core.Count; i++)
        {
            if (!Core.Used[i]) continue;
            for (int s = 0; s < SnapTrapCore.SlotCount; s++)
            {
                if (!SnapTrapCore.SlotTooth[s] || !Core.Occupied[i * SnapTrapCore.SlotCount + s] || !Core.Alive[i]) continue;
                SlotPos(i, s, out Vector3 tp);
                pos.Add(tp); rad.Add(3.5f);
            }
        }
    }
}

public static class SnapTrapTests
{
    static SnapTrapParams Best() => new SnapTrapParams();   // the defaults ARE the research's searched best

    public static void Run()
    {
        Console.WriteLine("== snap trap (SnapTrapCore vs research snaptrap.py) ==");
        StateMachine();
        Relax();
        HalfLobes();
        ReserveOnlyRebud();
        GlowCoversStrike();
        Arena();
        RouteBias();
        Cost();
        RimReach();
    }

    /// <summary>S9: the game's traps fit the rim. Rooted TrapRootDepthMin..Max inside the grove's outer radius,
    /// planted facing the cell centre and turned at most HelioConeDegrees from it, every prism of the trap - every
    /// slot at the shut, resting and primed gapes, plus half its leaf's diagonal - stays outside the outer swarm band
    /// and inside the membrane. Swept over the cone's edge and centre, eight azimuths, both root depths.</summary>
    static void RimReach()
    {
        var p = ThreatGroveDefaults.SnapTrap().ForElement(ThreatGroveDefaults.SnapTrapElement);
        var c = new SnapTrapCore(p, 1);
        float leafHalf = 0.5f * MathF.Max(ThreatGroveDefaults.LobeLeaf.Length(),
            MathF.Max(ThreatGroveDefaults.ToothLeaf.Length(), ThreatGroveDefaults.StalkLeaf.Length()));
        float minR = float.MaxValue, maxR = 0f;
        foreach (float depth in new[] { ThreatGroveDefaults.TrapRootDepthMin, ThreatGroveDefaults.TrapRootDepthMax })
        {
            var root = new Vector3(0f, 0f, ThreatGroveDefaults.GroveOuterRadius - depth);   // cell centre at the origin
            var inward = -Vector3.UnitZ;
            foreach (float tilt in new[] { 0f, 0.5f * p.HelioConeDegrees, p.HelioConeDegrees })
                for (int az = 0; az < 8; az++)
                {
                    float t = tilt * MathF.PI / 180f, phi = az * MathF.PI / 4f;
                    var axis = Vector3.Normalize(MathF.Cos(t) * inward + MathF.Sin(t) * new Vector3(MathF.Cos(phi), MathF.Sin(phi), 0f));
                    foreach (float deg in new[] { p.ShutGape, p.Gape, p.Gape + p.DGape })
                        for (int s = 0; s < SnapTrapCore.SlotCount; s++)
                        {
                            c.SlotPose(root, axis, deg * MathF.PI / 180f, s, SnapTrapCore.SlotR[s], out var pos, out _, out _);
                            minR = MathF.Min(minR, pos.Length() - leafHalf);
                            maxR = MathF.Max(maxR, pos.Length() + leafHalf);
                        }
                }
        }
        Console.WriteLine($"   rim: a trap's prisms span radius {minR:F1}..{maxR:F1} u over every heading in its {p.HelioConeDegrees:F0} deg cone " +
                          $"(outer band ends {ThreatGroveDefaults.OuterSwarmBandEdge:F0}, membrane {ThreatGroveDefaults.MembraneRadius:F0})");
        Program.Check(minR >= ThreatGroveDefaults.OuterSwarmBandEdge, $"S9 a rim trap's jaws stay off the outer swarm band ({minR - ThreatGroveDefaults.OuterSwarmBandEdge:F1} u clear)");
        Program.Check(maxR <= ThreatGroveDefaults.MembraneRadius, $"S9 and its back stays inside the membrane ({ThreatGroveDefaults.MembraneRadius - maxR:F1} u clear)");

        // and the cone holds in the core: a trap whose traffic sits straight behind it turns only to the cone's edge
        var q = Best(); q.HelioConeDegrees = 60f;
        var cc = new SnapTrapCore(q, 3);
        int i = cc.AddTrap(Vector3.Zero, -Vector3.UnitZ, false);
        cc.CompleteGrowth(i);
        var behind = new Vector3(0f, 10f, 100f);   // inside Sense, straight behind the jaws
        for (int k = 0; k < 900; k++) cc.Step(0.1f, One(behind, behind - new Vector3(0f, 0f, 4f)), 1);
        float ang = MathF.Acos(Math.Clamp(Vector3.Dot(cc.Axis[i], -Vector3.UnitZ), -1f, 1f)) * 180f / MathF.PI;
        Console.WriteLine($"   cone: traffic behind the trap for 90 s turned it {ang:F1} deg (cone {q.HelioConeDegrees:F0})");
        Program.Check(ang <= q.HelioConeDegrees + 0.5f && ang >= q.HelioConeDegrees - 5f, $"S9 heliotropism stops at the cone's edge ({ang:F1} deg)");
    }

    static ThreatVessel[] One(Vector3 p, Vector3 prev) => new[] { new ThreatVessel { Position = p, Previous = prev, Radius = 6f } };

    /// <summary>S1: OPEN -> PRIMING -> ARMED -> CLOSING -> SHUT -> OPEN with the research's timings, and one
    /// keyframe per state change (plus heliotropic re-poses) - never per frame.</summary>
    static void StateMachine()
    {
        var p = Best();
        var c = new SnapTrapCore(p, 1);
        int t = c.AddTrap(Vector3.Zero, Vector3.UnitZ, false);
        c.CompleteGrowth(t);
        const float dt = 0.05f;
        Vector3 m = c.MouthCentre(t);
        Vector3 beside = m + new Vector3(100f, 0f, 0f);
        float tPriming = -1, tArmed = -1, tClosing = -1, tShut = -1, tOpen = -1;
        int poses = 0, snaps = 0, glowOn = 0, glowOff = 0, ticks = 0, lastState = 0;
        var states = new List<int> { 0 };
        float time = 0f;
        Vector3 vessel = beside, prev = beside;
        for (int k = 0; k < 600; k++)
        {
            // script: wait beside the trap until it arms, then dive into the mouth and sit there; leave once shut
            if (tArmed >= 0 && tClosing < 0) { prev = vessel; vessel = c.MouthCentre(t) + c.Axis[t] * 20f; }
            else if (tShut >= 0) { prev = vessel; vessel = m + new Vector3(600f, 0f, 0f); }
            else prev = vessel;
            c.Step(dt, One(vessel, prev), 1);
            time += dt; ticks++;
            foreach (var e in c.Events)
            {
                if (e.Kind == SnapTrapEventKind.Pose) poses++;
                if (e.Kind == SnapTrapEventKind.Snap) snaps++;
                if (e.Kind == SnapTrapEventKind.Glow) { if (e.Glow == 1) glowOn++; else glowOff++; }
            }
            int st = c.State[t];
            if (st != lastState)
            {
                states.Add(st);
                if (st == 1 && tPriming < 0) tPriming = time;
                if (st == 2 && tArmed < 0) tArmed = time;
                if (st == 3 && tClosing < 0) tClosing = time;
                if (st == 4 && tShut < 0) tShut = time;
                if (st == 0 && tShut >= 0 && tOpen < 0) tOpen = time;
                lastState = st;
            }
            if (tOpen >= 0) break;
        }
        Program.Check(string.Join(",", states) == "0,1,2,3,4,0", $"S1 state sequence {string.Join(",", states)} == OPEN,PRIMING,ARMED,CLOSING,SHUT,OPEN");
        Program.Near("S1 PRIMING -> ARMED takes t_prime", tArmed - tPriming, p.TPrime, dt + 1e-3f);
        Program.Near("S1 CLOSING -> SHUT takes t_close", tShut - tClosing, p.TClose, dt + 1e-3f);
        Program.Near("S1 SHUT (caught) -> OPEN takes t_digest + reopen", tOpen - tShut, p.TDigest + p.ReopenSeconds, 2 * dt + 1e-3f);
        Program.Check(snaps == 1, $"S1 the vessel sitting in the jaws is snapped exactly once (snaps {snaps})");
        Program.Check(glowOn == 1 && glowOff == 1, $"S1 the lobes glow once and fade once (on {glowOn}, off {glowOff})");
        Program.Check(c.Fired[t] == 1, "S1 fired once");
        // transitions that move prisms: PRIMING, CLOSING, SHUT, reopen = 4; the rest are heliotropic re-poses
        // (one per HelioReposeDegrees of turn at TurnDegrees / s) - a keyframe count, not a frame count
        int helioBound = (int)MathF.Ceiling(p.TurnDegrees * time / p.HelioReposeDegrees) + 1;
        Program.Check(poses >= 4 && poses <= 4 + helioBound,
            $"S1 {poses} pose keyframes over {ticks} ticks ({time:F1} s): 4 state changes + <= {helioBound} heliotropic re-poses, never per frame");
        Program.Check(Math.Abs(c.Audit()) < 1e-6, $"S1 ledger closes ({c.Audit():E2})");
    }

    /// <summary>S2: a primed trap whose vessel leaves eases back to OPEN and its glow fades (an un-fired telegraph).</summary>
    static void Relax()
    {
        var p = Best();
        var c = new SnapTrapCore(p, 2);
        int t = c.AddTrap(Vector3.Zero, Vector3.UnitZ, false);
        c.CompleteGrowth(t);
        Vector3 near = c.MouthCentre(t) + new Vector3(100f, 0f, 0f), far = near + new Vector3(500f, 0f, 0f);
        int on = 0, off = 0;
        for (int k = 0; k < 8; k++) { c.Step(0.05f, One(near, near), 1); foreach (var e in c.Events) if (e.Kind == SnapTrapEventKind.Glow && e.Glow == 1) on++; }
        Program.Check(c.State[t] == (int)SnapTrapState.Priming, "S2 primed while a vessel is near");
        for (int k = 0; k < 40; k++) { c.Step(0.05f, One(far, far), 1); foreach (var e in c.Events) if (e.Kind == SnapTrapEventKind.Glow && e.Glow == 0) off++; }
        Program.Check(c.State[t] == (int)SnapTrapState.Open && on == 1 && off == 1 && c.Fired[t] == 0,
            $"S2 the vessel left: back to OPEN, glow on {on} / off {off}, never fired");
    }

    /// <summary>S3: a trap with fewer than half its lobe + tooth slots cannot fire (exactly half still can).</summary>
    static void HalfLobes()
    {
        foreach (int removed in new[] { 12, 13 })
        {
            var p = Best();
            var c = new SnapTrapCore(p, 3);
            int t = c.AddTrap(Vector3.Zero, Vector3.UnitZ, false);
            c.CompleteGrowth(t);
            // break `removed` of the 24 lobe + tooth slots (the far lobe's eight plates first, then teeth)
            int k = 0;
            for (int s = SnapTrapCore.StalkSlots; s < SnapTrapCore.SlotCount && k < removed; s++, k++) c.SlotLost(t, s);
            Vector3 beside = c.MouthCentre(t) + new Vector3(100f, 0f, 0f), mouth = c.MouthCentre(t) + Vector3.UnitZ * 20f;
            for (int i = 0; i < 20; i++) c.Step(0.05f, One(beside, beside), 1);
            Program.Check(c.State[t] == (int)SnapTrapState.Armed, $"S3 ({removed} broken) armed");
            for (int i = 0; i < 4; i++) c.Step(0.05f, One(mouth, beside), 1);
            bool fired = c.Fired[t] > 0;
            Program.Check(fired == (removed <= 12),
                $"S3 {24 - removed}/24 lobe+tooth slots intact -> {(fired ? "fires" : "cannot fire")} (the half rule)");
        }
    }

    /// <summary>S4: a trap re-lays lost slots ONLY from its own reserve, buds only by handing a daughter a whole
    /// body, and the ledger closes to rounding through all of it.</summary>
    static void ReserveOnlyRebud()
    {
        var p = Best();
        var c = new SnapTrapCore(p, 4, 8);
        int t = c.AddTrap(Vector3.Zero, Vector3.UnitZ, false);
        c.CompleteGrowth(t);
        Program.Check(c.LaidSlots(t) == 27 && Math.Abs(c.Reserve) < 1e-4, "S4 planted with exactly its own body: 27 slots laid, reserve 0");
        for (int s = 10; s < 15; s++) c.SlotLost(t, s);
        var none = new ThreatVessel[0];
        for (int i = 0; i < 400; i++) c.Step(0.05f, none, 0);   // 20 s of absorb ticks with nothing to eat
        Program.Check(c.LaidSlots(t) == 22, $"S4 five slots broken and nothing eaten: stays at {c.LaidSlots(t)} (22) - no slot from nothing");
        float two = c.SlotVolume(10) + c.SlotVolume(11);
        c.Deposit(t, two);
        Program.Check(c.LaidSlots(t) == 24 && Math.Abs(c.Reserve) < 1e-4,
            $"S4 fed exactly two slots' volume: re-lays two ({c.LaidSlots(t)}), reserve {c.Reserve:F3}");
        c.Deposit(t, c.SlotVolume(12) + c.SlotVolume(13) + c.SlotVolume(14) + c.BodyVolume + 1f);
        Program.Check(c.LaidSlots(t) == 27, "S4 fed the rest: whole again");
        int buds = 0;
        for (int i = 0; i < 200 && buds == 0; i++)
        {
            c.Step(0.05f, none, 0);
            for (int k = 0; k < c.Events.Count; k++)
            {
                var e = c.Events[k];
                if (e.Kind == SnapTrapEventKind.BudRequest) { c.AddTrap(e.Position, e.Axis, true); buds++; }
            }
        }
        double planted = c.Planted;
        for (int i = 0; i < 200; i++)
        {
            c.Step(0.05f, none, 0);
            foreach (var e in c.Events) if (e.Kind == SnapTrapEventKind.BudRequest) buds++;
        }
        Program.Check(buds == 1 && c.LiveCount == 2 && c.LaidSlots(1) == 27 && Math.Abs(c.Reserve - 1.0) < 1e-3,
            $"S4 a body's worth in the rhizome buds ONE daughter ({buds}), who blooms to 27 slots from it; 1 left ({c.Reserve:F3})");
        Program.Check(c.Planted == planted, $"S4 budding planted no new mass ({c.Planted})");
        c.Kill(0);
        c.AddTrap(new Vector3(200f, 0f, 0f), Vector3.UnitX, false);
        Program.Check(Math.Abs(c.Audit()) < 1e-6,
            $"S4 a dead trap leaves its body as skeleton, a seeded trap brings its own; ledger {c.Audit():E2}");
    }

    /// <summary>S5: the telegraph covers the strike: the research rule (glow radius >= L tan(gape + dgape)) and,
    /// in game, the glowing lobes and teeth at full gape reach past every corner of the strike volume.</summary>
    static void GlowCoversStrike()
    {
        foreach (int element in new[] { 0, 2, 3, 4 })
        {
            var p = Best().ForElement(element);
            var c = new SnapTrapCore(p, 5);
            int t = c.AddTrap(Vector3.Zero, Vector3.UnitZ, false);
            float wide = ThreatFloraMath.Deg2Rad(p.Gape + p.DGape);
            float lip = p.MouthLength * p.GeometryScale * MathF.Tan(wide);
            Vector3 m = c.MouthCentre(t);
            float reach = 0f;
            for (int s = SnapTrapCore.StalkSlots; s < SnapTrapCore.SlotCount; s++)
            {
                c.SlotPose(c.Heart[t], c.Axis[t], wide, s, SnapTrapCore.SlotR[s], out Vector3 pos, out _, out _);
                reach = MathF.Max(reach, (pos - m).Length() + (SnapTrapCore.SlotTooth[s] ? 3.5f : 4f));
            }
            Program.Check(c.GlowRadius >= lip - 1e-4f && reach >= c.StrikeExtent,
                $"S5 element {element}: glow radius {c.GlowRadius:F1} >= lip {lip:F1}; glowing body reaches {reach:F1} >= strike {c.StrikeExtent:F1}");
        }
    }

    static void Arena()
    {
        // the research scorecard's WANDER runs (3 seeds x 2 min) - the threat a blind pilot meets
        float hits = 0, minutes = 0;
        int prismsEnd = 0;
        double worst = 0;
        var leads = new List<float>();
        foreach (ulong seed in new ulong[] { 7, 23, 41 })
        {
            var ar = new FloraArena(seed);
            ar.GroveMass(1600, 20);
            var sp = new SnapTrapSpecies(ar, Best(), seed * 31 + 1);
            var pl = ar.AddPilot("wander", 120f, "wanderer");
            double m0 = ar.LiveVolume() + sp.MassTotal + sp.CutVolume;
            for (int k = 0; k < 1200; k++) { sp.Step(0.1f); ar.Step(0.1f); }
            double m1 = ar.LiveVolume() + sp.MassTotal + sp.CutVolume - ar.Created;
            worst = Math.Max(worst, Math.Abs((m1 - m0) / m0));
            worst = Math.Max(worst, Math.Abs(sp.Core.Audit()) / m0);
            hits += pl.Hits.Count; minutes += 2f;
            prismsEnd += CountLaid(sp.Core);
            leads.AddRange(sp.Leads);
        }
        float hpm = hits / minutes;
        prismsEnd /= 3;
        Console.WriteLine($"   wander: {hpm:F2} hits/min (research anchor 4.17), prisms at 2 min {prismsEnd} (research 1733), " +
                          $"telegraph p10 {FloraArena.Percentile(leads, 10):F2} s (research 0.86, wander+reader), mass drift {worst:E2}");
        Program.Check(hpm >= 2f && hpm <= 8.5f, $"S6 a blind pilot meets the clump: {hpm:F2} hits/min within [2, 8.5] of the research's 4.17");
        Program.Check(prismsEnd >= 1200 && prismsEnd <= 2500, $"S6 the colony grows on trails to {prismsEnd} prisms in 2 min (research 1733; R_hard budget 2500)");
        Program.Check(worst < 1e-6, $"S6 mass audit closes to float32 rounding over 3 seeds x 2 min ({worst:E2})");
    }

    static int CountLaid(SnapTrapCore c)
    {
        int n = 0;
        for (int i = 0; i < c.Count; i++) if (c.Used[i]) n += c.LaidSlots(i);
        return n;
    }

    /// <summary>S7: heliotropism -> route bias. A COURIER flying one 3-waypoint loop: threat per unit length near
    /// the route over threat near random lanes, late half / early half (harness.py `adapt`, research 1.61-1.83).
    /// The control (no turning, no bud bias) must grow less.</summary>
    static void RouteBias()
    {
        float Adapt(SnapTrapParams p, ulong[] seeds, bool ecology)
        {
            float sum = 0;
            var per = new List<string>();
            foreach (ulong seed in seeds)
            {
                var ar = new FloraArena(seed);
                // ecology = the research's scorecard run (food, trails, budding); otherwise the clump is fixed (no
                // food, no trails, so nothing grows) and the only thing that can move a mouth is heliotropism
                if (ecology) ar.GroveMass(1600, 20); else ar.Trails = false;
                var sp = new SnapTrapSpecies(ar, p, seed * 31 + 1);
                ar.AddPilot("courier", 120f, "courier");
                var lanes = FloraArena.Lanes();
                float laneLen = 0; foreach (var l in lanes) laneLen += (l[1] - l[0]).Length();
                var route = ar.CourierRoute;
                float routeLen = 0; for (int i = 0; i < 3; i++) routeLen += (route[(i + 1) % 3] - route[i]).Length();
                var rb = new List<float>();
                var tp = new List<Vector3>(); var tr = new List<float>();
                for (int k = 0; k < 1200; k++)
                {
                    sp.Step(0.1f); ar.Step(0.1f);
                    if ((k + 1) % 10 != 0) continue;
                    sp.ThreatElements(tp, tr);
                    float onRoute = 0, onLanes = 0;
                    for (int e = 0; e < tp.Count; e++)
                    {
                        for (int i = 0; i < 3; i++)
                            if (ThreatFloraMath.SegmentPointDistance(route[i], route[(i + 1) % 3], tp[e]) < tr[e] + 60f) onRoute++;
                        foreach (var l in lanes)
                            if (ThreatFloraMath.SegmentPointDistance(l[0], l[1], tp[e]) < tr[e] + 60f) onLanes++;
                    }
                    rb.Add((onRoute / routeLen + 1e-4f) / (onLanes / laneLen + 1e-4f));
                }
                int h = rb.Count / 2;
                float early = 0, late = 0;
                for (int i = 0; i < h; i++) early += rb[i];
                for (int i = h; i < rb.Count; i++) late += rb[i];
                float a = (late / (rb.Count - h)) / MathF.Max(early / h, 1e-3f);
                per.Add(a.ToString("F2"));
                sum += a;
            }
            Console.WriteLine($"     per seed: {string.Join(" ", per)}");
            return sum / seeds.Length;
        }
        var seeds = new ulong[] { 7, 23, 41, 1, 2, 3, 4, 5, 6, 8, 9, 10 };
        var ctl = Best(); ctl.TurnDegrees = 0f; ctl.BudBias = 0f;
        float helio = Adapt(Best(), seeds, false), still = Adapt(ctl, seeds, false);
        Console.WriteLine($"   heliotropism alone (a fixed clump, no growth): route bias grows {helio:F2}x; turning off: {still:F2}x");
        Program.Check(helio >= 1.15f, $"S7 the clump turns its mouths onto a courier's loop: route bias grows {helio:F2}x (>= 1.15, 12 seeds)");
        Program.Check(MathF.Abs(still - 1f) < 0.05f, $"S7 control: a clump that cannot turn stays at {still:F2}x (1.00 +- 0.05)");
        float eco = Adapt(Best(), seeds, true), ecoCtl = Adapt(ctl, seeds, true);
        Console.WriteLine($"   the research's scorecard run (food, trails, budding; 12 seeds): {eco:F2}x with heliotropism, {ecoCtl:F2}x without " +
                          "(research 1.61-1.83 over 3 seeds, never controlled; per-seed spread above - the growth term dominates)");
        Program.Check(eco >= 1.0f, $"S7 with the whole ecology running the clump still drifts onto the route ({eco:F2}x >= 1.0)");
    }

    static void Cost()
    {
        // the research's cost row: 60 traps x 27 prisms
        var c = new SnapTrapCore(Best(), 9, 64);
        var rng = new ThreatRng(9);
        for (int i = 0; i < 60; i++) c.CompleteGrowth(c.AddTrap(rng.OnUnitSphere() * 300f, rng.OnUnitSphere(), false));
        var vs = new[] { new ThreatVessel { Position = new Vector3(0, 0, 0), Previous = new Vector3(0, 0, -5) } };
        for (int k = 0; k < 200; k++) c.Step(0.05f, vs, 1);
        var sw = Stopwatch.StartNew();
        const int N = 4000;
        for (int k = 0; k < N; k++) { vs[0].Previous = vs[0].Position; vs[0].Position += new Vector3(1, 0.5f, 0.2f); c.Step(0.05f, vs, 1); }
        double ms = sw.Elapsed.TotalMilliseconds / N;
        Console.WriteLine($"   cost: {ms:F4} ms per step for 60 traps x 27 slots (research Burst estimate 0.002 ms)");
        Program.Check(ms < 0.5, $"S8 60 traps step in {ms:F4} ms (< 0.5 ms, CoreCLR, one thread)");
    }
}
