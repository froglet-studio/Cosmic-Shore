// Headless proof of Tandava's WOUND MEMORY (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.12): the creature remembers how
// it was hurt - struck at the table, punished in a lunge, run down while it roamed, cut in two - and adapts, so the same
// kill does not work twice. T11 (TandavaHarness.cs) is the headline: the denial that shatters a creature that cannot learn
// no longer does once it can, and pilots who change their play still win. These are the other three wounds.
//
//   TANDAVA_ONLY=wound bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>   # T27-T29 only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class TandavaHarness
{
    static string Memories(TandavaDirectorCore d) =>
        string.Join(", ", Enum.GetValues(typeof(TandavaWound)).Cast<TandavaWound>().Select(w => $"{w} {d.Scar(w):F2}/{d.Memory(w):F2}"));

    /// <summary>Hold it in its first form (an unreachable bank): a wound test measures the memory, not the ladder.</summary>
    static void Serpent(Sim s) { s.Forms[0].Bank = 1e9f; }

    static void WoundTests(Bake b)
    {
        // ── T27: the scarred lunger - every lunge costs it members; a creature that learns stops paying
        Console.WriteLine("T27 the scarred lunger");
        {
            (int early, int late, int total, bool learned) Lunges(bool learns)
            {
                var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 83, ds => { if (!learns) ds.ScarRef = 0f; });
                Serpent(s);
                s.C.Stomach[1] = 0.12f * StomachCapacity;   // under the bank: it stays a serpent, and has the stomach to regrow
                var pilot = new Pilot();
                s.Pilots.Add(pilot);
                int early = 0, late = 0, punished = 0; bool was = false;
                RunFor(s, 180f, x =>
                {
                    // a pilot who sits off its flank, close enough to be lunged at, and punishes every lunge
                    pilot.At = x.C.Anchor * UnitScale + x.C.BZ * 150f; pilot.Vel = Vector3.Zero;
                    bool lunging = x.D.Lunging || x.D.Snapping;
                    if (lunging && !was) { if (x.Now < 90f) early++; else late++; punished = 0; }
                    was = lunging;
                    if (lunging && punished < 10) { int n = Cut(x.C, 1, -x.C.BX); x.Lost += n; punished += n; }   // at its head
                });
                if (learns) Console.WriteLine($"      memory: {Memories(s.D)}; lost {s.Lost}, {s.D.Form.Name}");
                return (early, late, early + late, s.Learned.Any(l => l.wound == TandavaWound.Lunging));
            }
            var fresh = Lunges(false); var scarred = Lunges(true);
            Console.WriteLine($"    cannot learn: {fresh.early} lunges in the first 90 s, {fresh.late} in the next; " +
                              $"learns: {scarred.early} then {scarred.late}{(scarred.learned ? " (it learned)" : "")}");
            Check(scarred.learned, "punished at every lunge, it learned what its lunges cost");
            Check(scarred.late < scarred.early, $"it lunges less once it has learned ({scarred.early} -> {scarred.late})");
            Check(scarred.late < fresh.late, $"and less than a creature that cannot learn ({scarred.late} vs {fresh.late})");
        }

        // ── T28: severed twice - the second piece turns for home sooner, and gets there sooner
        Console.WriteLine("T28 severed twice");
        {
            var s = MakeSim(b, new[] { 1, 0, 0, 0 }, 73);
            Serpent(s);
            s.C.Stomach[1] = 0.12f * StomachCapacity;
            RunFor(s, 6f);
            var clocks = new List<float>(); var trips = new List<float>();
            for (int round = 0; round < 2 && s.D.Outcome == TandavaOutcome.Running; round++)
            {
                // wait out the cooldown and the bolt, then slice it - again every 2 s until a slice parts it
                RunFor(s, 12f);
                for (int e = 0; e < 4; e++) s.C.Stomach[e] = e == 1 ? 0.12f * StomachCapacity : 0f;   // the same share each time
                int severs = s.Severs;
                for (int tries = 0; tries < 20 && s.Severs == severs; tries++)
                {
                    Tick(s, x => x.Lost += Slice(x.C, 0.3f, SliceWidth));
                    if (s.Severs == severs) RunFor(s, 2f);
                }
                if (s.Severs == severs || s.Pieces.Count == 0) break;
                clocks.Add(s.Pieces[0].D.RejoinRemaining);
                float t0 = s.Now; int rejoins = s.Rejoins;
                while (s.Rejoins == rejoins && s.Pieces.Count > 0 && s.D.Outcome == TandavaOutcome.Running && s.Now - t0 < 150f) Tick(s);
                if (s.Rejoins > rejoins) trips.Add(s.Now - t0);
            }
            Console.WriteLine($"      memory: {Memories(s.D)}");
            Console.WriteLine($"    {SeverStory(s)}; time apart allowed {string.Join(" -> ", clocks.Select(c => $"{c:F0} s"))}, " +
                              $"home after {string.Join(" -> ", trips.Select(t => $"{t:F0} s"))}; learned: " +
                              string.Join(", ", s.Learned.Select(l => $"{l.wound} at {l.t:F0} s")));
            Check(clocks.Count == 2 && clocks[1] < 0.75f * clocks[0], "the second piece's clock home is shorter: it remembers the first");
            Check(trips.Count == 2 && trips[1] < trips[0], "and the second piece was home sooner");
            Check(s.Learned.Any(l => l.wound == TandavaWound.Severed), "cut in two twice, it learned (the narrator says so)");
        }

        // ── T29: run down - a creature nibbled while it roams senses pilots from farther, and runs sooner
        Console.WriteLine("T29 run down");
        {
            (float threat, float sense, bool learned) Probe(bool chased)
            {
                var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 89);
                Serpent(s);
                s.C.Stomach[1] = 0.12f * StomachCapacity;
                var pilot = new Pilot();
                s.Pilots.Add(pilot);
                // 20 s of a pilot on its tail, nibbling while it roams (or just riding along, harmless)
                RunFor(s, 20f, x =>
                {
                    // on its tail, just out of lunging reach
                    pilot.At = x.C.Anchor * UnitScale - x.C.BX * 1.05f * DirectorSettings().LungeRadius; pilot.Vel = x.C.BX * 30f;
                    if (chased && x.D.Phase == TandavaPhase.Roam && !x.D.Lunging && !x.D.Snapping && (int)MathF.Round(x.Now * TickHz) % 2 == 0)
                    { int n = Cut(x.C, 1, x.C.BX); x.Lost += n; }
                });
                // gone, long enough for every threat to fall away
                s.Pilots.Clear();
                RunFor(s, 25f);
                // a pilot parks just outside how far a fresh creature senses
                float probe = 1.15f * DirectorSettings().SenseRadius;
                s.Pilots.Add(pilot);
                float peak = 0f;
                RunFor(s, 3f, x =>
                {
                    pilot.At = x.C.Anchor * UnitScale + x.C.BZ * probe; pilot.Vel = Vector3.Zero;
                    peak = MathF.Max(peak, x.D.Threat);
                });
                if (chased) Console.WriteLine($"      memory: {Memories(s.D)}; lost {s.Lost}");
                return (peak, s.D.SenseRadiusNow, s.Learned.Any(l => l.wound == TandavaWound.Chased));
            }
            var fresh = Probe(false); var scarred = Probe(true);
            Console.WriteLine($"    a pilot {1.15f * DirectorSettings().SenseRadius:F0} u off: the unscarred creature's threat {fresh.threat:F2} (senses {fresh.sense:F0} u), " +
                              $"the run-down one's {scarred.threat:F2} (senses {scarred.sense:F0} u){(scarred.learned ? ", learned" : "")}");
            Check(scarred.learned, "nibbled while it roamed, it learned being run down");
            Check(fresh.threat < 0.01f && scarred.threat > 0.03f, "it feels a pilot the unscarred creature cannot");
        }
    }
}
