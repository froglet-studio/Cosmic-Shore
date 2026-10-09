// Headless proof of Tandava's WHALE-JELLY CHIMERA (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.13): before the
// Many-Headed Serpent rises into the Dance it passes through a body the hybrid NCA grew - a whale spliced onto a jelly,
// torn between the two. It drifts to where it will rise, turns from one shape to the other every few seconds (it cannot
// heal mid-turn), and rises when its time is up. It can still be cut while it is torn.
//
//   TANDAVA_ONLY=chimera bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>   # T30-T31 only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class TandavaHarness
{
    static void ChimeraTests(Bake b)
    {
        var ds = DirectorSettings();

        // ── T30: torn between a whale and a jelly - it enters before the rise, turns, holds its healing mid-turn, rises
        Console.WriteLine("T30 the chimera");
        foreach (var (picks, seed) in new[] { (new[] { 0, 0, 0, 0 }, 31), (new[] { 1, 2, 1, 2 }, 37) })
        {
            var s = MakeSim(b, picks, seed);
            int manyAt = -1;
            RunFor(s, RunCap, x => { if (manyAt < 0 && x.D.FormIx == 1) manyAt = (int)x.Now; });
            var d = s.D;
            string tag = string.Join("", picks);
            float span = s.RiseAt - s.ChimeraAt;
            string shapes = string.Join(", ", s.ChimeraPlans.Select(i => Keys[i]));
            Console.WriteLine($"    picks {tag}: {d.Outcome} at {d.Clock:F0} s - {Timeline(s)}; chimera at {s.ChimeraAt:F0} s for {span:F0} s, " +
                              $"{s.Turns.Count} turns ({string.Join(" ", s.Turns.Select(t => $"{t.t:F0}{(t.jelly ? "J" : "W")}"))}), wore {shapes}, " +
                              $"fewest members {s.ChimeraMinAlive}, laid {s.LaidWhileTurning} mid-turn");
            Check(s.ChimeraAt > 0f && s.Commits.Count >= 1 && s.ChimeraAt >= s.Commits[0].t,
                  $"picks {tag}: it became the chimera as the Many-Headed Serpent ({s.ChimeraAt:F0} s)");
            Check(s.RiseAt > s.ChimeraAt && MathF.Abs(span - ds.ChimeraSeconds) < 1f,
                  $"and rose into the Dance {ds.ChimeraSeconds:F0} s later ({span:F1} s)");
            Check(s.ChimeraPlans.SetEquals(new[] { b.Ix["chimera_whale"], b.Ix["chimera_jelly"] }),
                  $"it wore both shapes and nothing else while torn ({shapes})");
            int expect = (int)(ds.ChimeraSeconds / ds.ChimeraFlipSeconds);
            Check(s.Turns.Count >= expect - 1 && s.Turns.Zip(s.Turns.Skip(1)).All(p => p.First.jelly != p.Second.jelly),
                  $"it turned {s.Turns.Count} times, whale and jelly in turn (every {ds.ChimeraFlipSeconds:F0} s)");
            Check(s.LaidWhileTurning == 0, "it laid no egg mid-turn (it cannot heal while it changes)");
            Check(s.ChimeraMinAlive >= 0.9f * s.Forms[1].PlanCount,
                  $"turning cost it no members ({s.ChimeraMinAlive} of {s.Forms[1].PlanCount}): a turn is a pose, not a molt");
            Check(Vector3.Distance(s.RiseAnchor, d.DancePoint) <= ds.DanceReach + 20f,
                  $"it rose where it drifted to ({Vector3.Distance(s.RiseAnchor, d.DancePoint):F0} u from the dance ground)");
            Check(d.Outcome == TandavaOutcome.Completed && d.Clock < 200f, $"unopposed it still completes in under 200 s ({d.Clock:F0} s)");
        }

        // ── T31: cut while torn - the chimera can be severed like any body, and its piece is the Severed
        Console.WriteLine("T31 cut while torn");
        {
            var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 31);
            while (!s.D.InChimera && s.D.Outcome == TandavaOutcome.Running && s.Now < RunCap) Tick(s);
            float t0 = s.Now;
            RunFor(s, 2f);
            int severs = s.Severs;
            for (int tries = 0; tries < 8 && s.Severs == severs && s.D.InChimera; tries++)
            {
                // through its middle: the whale end trails a thin jelly fringe, and a cut there takes too few to live
                Tick(s, x => x.Lost += Slice(x.C, 0.42f, SliceWidth));
                if (s.Severs == severs) RunFor(s, 1f);
            }
            bool cut = s.Severs > severs, piece = s.Pieces.Count > 0 && s.Pieces[0].D.Form.Role == TandavaFormRole.Severed;
            bool stillTorn = s.D.InChimera;
            Console.WriteLine($"    the chimera at {t0:F0} s: {(cut ? $"cut at {s.SeverLog[0].t:F0} s" : "never parted")}; " +
                              $"{SeverStory(s)}; still torn after the cut: {stillTorn}");
            Check(cut && piece, "a slice through the torn body parts a piece, and the piece is the Severed");
            Check(stillTorn || s.RiseAt > 0f, "the body it left keeps on: still torn, or risen");
        }
    }
}
