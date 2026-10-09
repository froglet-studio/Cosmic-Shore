// Headless proof of Tandava's SEVERING (Assets/_Scripts/Controller/Arcade/TANDAVA.md §3.11): a cut clean through the body
// parts it, and the piece crawls off as THE SEVERED - a second creature on its own sort core and its own director, run
// beside the body in the same closed cell, eating the same plants. The glue's half (TandavaController's sever, rejoin and
// succession) is modelled here exactly as the controller runs it: find the piece the moment the cut lands (before the
// wound buds shut), move its members across (Release / Graft: nothing dies, nothing is born), split the stomach by members,
// graft a homebound piece back on when it arrives, and hand a piece the body if the body is cut away to nothing.
//
//   TANDAVA_ONLY=sever bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans dir>   # T20-T26 only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CosmicShore.Gameplay;

static partial class TandavaHarness
{
    /// <summary>A severed piece: its own core and director (TandavaController keeps the same pair per Severed swarm).</summary>
    sealed class Piece
    {
        public SwarmSortCore C;
        public TandavaDirectorCore D;
        public int Lost, BiteCursor;
        public float SinceBite = float.PositiveInfinity, SinceFed, LastShed = -1e9f, Eaten, Born;
    }

    static readonly TandavaSever _sever = new();
    /// <summary>A slicing pass's swath (sim units): the sever link and a member's spacing - what it takes to leave a gap.</summary>
    const float SliceWidth = 9f;
    static readonly List<int> _piece = new();

    static TandavaForm SeveredForm(Bake b) => new()
    {
        Name = "the Severed", Role = TandavaFormRole.Severed,
        PlanIndex = b.Ix["severed"], FeedPlanIndex = b.Ix["severed_feed"], PlanCount = b.Plans[b.Ix["severed"]].N,
        Bank = DirectorSettings().RejoinBank, MealVolume = 0.5f * MealVolume,
        Mouth = b.Mouth["severed"], FeedMouth = b.Mouth["severed_feed"],
    };

    static float StomachTotal(SwarmSortCore c) => c.Stomach[0] + c.Stomach[1] + c.Stomach[2] + c.Stomach[3];

    /// <summary>TandavaController.TrySever: the moment a cut lands, look for a piece; part it off if there is one.</summary>
    static void TrySever(Sim s)
    {
        var c = s.C; var st = State(s);
        if (!s.D.MaySever(st)) return;
        int n = _sever.FindPiece(c.Pos, c.Active, c.Cap, DirectorSettings().SeverLink / UnitScale, s.D.SeverMinMembers, s.D.SeverMaxMembers, _piece);
        if (n == 0) return;
        int aliveBefore = Active(c);
        Vector3 pc = Vector3.Zero, all = Vector3.Zero;
        foreach (int i in _piece) pc += c.Pos[i];
        pc /= n;
        int rest = 0;
        for (int i = 0; i < c.Cap; i++) if (c.Active[i] && !_piece.Contains(i)) { all += c.Pos[i]; rest++; }
        var bc = rest > 0 ? all / rest : c.Anchor;
        var away = pc - bc;
        away = away.LengthSquared() > 1e-4f ? Vector3.Normalize(away) : -c.BX;
        var pcore = new SwarmSortCore(s.B.Plans, Params(s.B.Plans), 7919 + s.Severs);
        pcore.Seed(s.B.Ix["severed"], 0, pc, away);
        foreach (int i in _piece) { pcore.Graft(c.Pos[i], c.Vel[i], c.Facing[i], c.EffectiveElement(i)); c.Release(i); }
        float share = n / (float)Math.Max(1, aliveBefore);
        for (int e = 0; e < 4; e++) { float take = c.Stomach[e] * share; pcore.Stomach[e] = take; c.Stomach[e] -= take; }
        var pd = new TandavaDirectorCore(new[] { SeveredForm(s.B) }, DirectorSettings(), 104729 + s.Severs);
        s.Pieces.Add(new Piece { C = pcore, D = pd, Born = s.Now });
        s.D.OnSevered(n);
        s.Severs++;
        s.LastSever = (aliveBefore, n, Active(c), StomachTotal(c) + StomachTotal(pcore));
        s.SeverLog.Add((s.Now, $"severed {n} of {aliveBefore} as the {s.D.Form.Name}"));
    }

    /// <summary>One tick of every severed piece, in SwarmFauna's order (feed, metabolism, director, plan, levers, step),
    /// then the glue's rejoin and succession.</summary>
    static void TickPieces(Sim s)
    {
        const float dt = 1f / TickHz;
        for (int q = s.Pieces.Count - 1; q >= 0; q--)
        {
            var p = s.Pieces[q];
            var c = p.C;
            bool bit = false;
            if (StomachTotal(c) < StomachCapacity)
                for (int bq = 0, tries = 0; bq < BitersPerStep && tries < c.Cap; tries++)
                {
                    p.BiteCursor = (p.BiteCursor + 1) % c.Cap;
                    int i = p.BiteCursor;
                    if (!c.Active[i]) continue;
                    bq++;
                    var at = c.Pos[i] * UnitScale;
                    foreach (var pl in s.Plants)
                    {
                        if (!pl.Alive || pl.Store < pl.Leaf) continue;
                        if (Vector3.DistanceSquared(at, pl.At) > (PlantRadius + BiteRadius) * (PlantRadius + BiteRadius)) continue;
                        pl.Store -= pl.Leaf; pl.LastBite = s.Now;
                        c.Stomach[pl.Element] += pl.Leaf; p.Eaten += pl.Leaf; bit = true;
                        break;
                    }
                }
            p.SinceBite = bit ? 0f : p.SinceBite + dt;
            float fill = StomachTotal(c) / StomachCapacity;
            p.SinceFed = bit || fill >= 1f ? 0f : p.SinceFed + dt;
            bool starving = p.SinceFed >= StarvationSeconds && fill < ForageBelow;
            if (starving && s.Now - p.LastShed >= ShedIntervalSeconds)
            {
                p.LastShed = s.Now;
                int v = c.StarvationVictim();
                if (v >= 0) { c.Kill(v); p.Lost++; }
            }
            var st = new TandavaSwarmState
            {
                Alive = Active(c), Lost = p.Lost, Anchor = c.Anchor * UnitScale, Forward = c.BX, Up = c.BY, Side = c.BZ,
                Stomach0 = c.Stomach[0], Stomach1 = c.Stomach[1], Stomach2 = c.Stomach[2], Stomach3 = c.Stomach[3],
                StomachFill = fill, SinceBite = p.SinceBite, EatenTotal = p.Eaten, Starving = starving,
            };
            var d = p.D;
            d.Home = s.C.Anchor * UnitScale;
            d.Tick(dt, st, s.Food, s.Sensed);
            foreach (var e in d.Events)
                if (e.Kind == TandavaEventKind.HomeBound) s.SeverLog.Add((s.Now, $"the Severed turned for home with {e.A} members"));
            d.Events.Clear();
            if (d.Outcome != TandavaOutcome.Running)
            {
                s.SeverLog.Add((s.Now, $"the Severed ended: {d.Outcome}"));
                s.Pieces.RemoveAt(q); s.PiecesLost++;
                continue;
            }
            if (Active(s.C) == 0)   // the body is gone and this piece lives: it takes the body
            {
                Succeed(s, p); s.Pieces.RemoveAt(q);
                continue;
            }
            if (d.HomeBound && Vector3.Distance(c.Anchor * UnitScale, s.C.Anchor * UnitScale) <= DirectorSettings().RejoinReach)
            {
                Rejoin(s, p); s.Pieces.RemoveAt(q);
                continue;
            }
            if (c.PlanIx != d.WantPlan) c.RequestPose(d.WantPlan);
            c.SetLevers(d.CruiseScale, d.TurnScale, d.HoldLaying);
            c.SwimTarget = d.Goal / UnitScale;
            c.Step(ReadOnlySpan<SwarmPredator>.Empty);
            c.Events.Clear();
        }
    }

    /// <summary>TandavaController.Rejoin: a homebound piece grafts back on - up to the body's form, the rest paid back into
    /// the stomach as the eggs they were (nothing dies), and its stomach poured in.</summary>
    static void Rejoin(Sim s, Piece p)
    {
        var c = s.C; var pc = p.C;
        int home = 0, refunded = 0, room = s.D.Form.PlanCount - Active(c);
        for (int i = 0; i < pc.Cap; i++)
        {
            if (!pc.Active[i]) continue;
            int e = pc.EffectiveElement(i);
            if (home < room && c.Graft(pc.Pos[i], pc.Vel[i], pc.Facing[i], e) >= 0) home++;
            else { c.Stomach[e] += EggVolume[e]; refunded++; }
            pc.Release(i);
        }
        for (int e = 0; e < 4; e++) c.Stomach[e] += pc.Stomach[e];
        float over = StomachTotal(c) - StomachCapacity;   // a full stomach holds no more (SwarmFauna.Feed's cap)
        if (over > 0f) for (int e = 0; e < 4; e++) c.Stomach[e] -= over * c.Stomach[e] / StomachTotal(c);
        s.D.OnRejoined(home);
        s.Rejoins++; s.PieceMembersHome += home;
        s.SeverLog.Add((s.Now, $"the Severed rejoined: {home} members home, {refunded} paid back as eggs"));
    }

    /// <summary>TandavaController.Succeed: the body is cut away to nothing while its piece lives - the piece IS the body
    /// now, and it regrows into the form the director remembers.</summary>
    static void Succeed(Sim s, Piece p)
    {
        int n = Active(p.C);
        s.C = p.C;
        s.LastForm = -1;   // a new body: the form's plan is a RequestPlan, not a pose
        s.BiteCursor = 0;
        s.D.OnSuccession(n);
        s.Successions++;
        s.SeverLog.Add((s.Now, $"the body was cut away; the Severed ({n}) took the {s.D.Form.Name}'s form"));
    }

    /// <summary>A pilot's slicing pass: kill every live member within <paramref name="width"/> (sim units) of the plane
    /// across the body at <paramref name="fromTail"/> of its length from the tail. Returns how many died.</summary>
    static int Slice(SwarmSortCore c, float fromTail, float width)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i]) { float a = Vector3.Dot(c.Pos[i] - c.Anchor, c.BX); lo = MathF.Min(lo, a); hi = MathF.Max(hi, a); }
        if (lo > hi) return 0;
        float x0 = lo + fromTail * (hi - lo);
        int n = 0;
        for (int i = 0; i < c.Cap; i++)
            if (c.Active[i] && MathF.Abs(Vector3.Dot(c.Pos[i] - c.Anchor, c.BX) - x0) < 0.5f * width) { c.Kill(i); n++; }
        return n;
    }

    /// <summary>The live members' pieces at the sever link: (pieces of 2+, the biggest, the second biggest).</summary>
    static (int pieces, int big, int second) Pieces(SwarmSortCore c)
    {
        float link = DirectorSettings().SeverLink / UnitScale;
        // FindPiece with min 1 / max cap reports the second piece; the body is everything else
        int second = _sever.FindPiece(c.Pos, c.Active, c.Cap, link, 2, c.Cap, _piece);
        return (second > 0 ? 2 : 1, Active(c) - second, second);
    }

    static string SeverStory(Sim s) => string.Join("; ", s.SeverLog.Select(x => $"{x.t:F0} s {x.what}"));

    static void SeverTests(Bake b)
    {
        if (Environment.GetEnvironmentVariable("TANDAVA_DIAG") == "knots")
        {
            foreach (var key in Keys)
            {
                var c = MakeCore(b, b.Ix[key], Vector3.Zero, 3, fed: true);
                Step(c, 60);
                var row = new List<string>();
                foreach (float link in new[] { 3.5f, 4.5f, 5.5f, 6.5f, 8f })
                {
                    int sec = _sever.FindPiece(c.Pos, c.Active, c.Cap, link, 2, c.Cap, _piece);
                    row.Add($"{link}:{sec}");
                }
                // nearest-neighbour spacing
                var nn = new List<float>();
                for (int i = 0; i < c.Cap; i++) { if (!c.Active[i]) continue; float m = float.MaxValue; for (int j = 0; j < c.Cap; j++) if (j != i && c.Active[j]) m = MathF.Min(m, Vector3.Distance(c.Pos[i], c.Pos[j])); nn.Add(m); }
                nn.Sort();
                Console.WriteLine($"    {key,-22} n {Active(c),3}  nn med {nn[nn.Count / 2]:F2} p95 {nn[(int)(nn.Count * 0.95)]:F2} max {nn[^1]:F2}  second@link {string.Join(" ", row)}");
            }
            return;
        }
        // ── T20: the finder - a whole body is ONE piece in every pose it wears; a slice parts it
        Console.WriteLine("T20 the sever finder");
        {
            int falseParts = 0, worstSecond = 0; string worstKey = "";
            foreach (var key in Keys)
            {
                if (key.StartsWith("dancer")) continue;   // the dance never severs (its attendant packs stand apart by design)
                if (new[] { "_rear", "_strike", "_gape", "_snap" }.Any(key.EndsWith)) continue;   // nor a lunge (MaySever)
                var c = MakeCore(b, b.Ix[key], Vector3.Zero, 3, fed: true);
                Step(c, 60);
                var (_, _, second) = Pieces(c);
                var d = new TandavaDirectorCore(new[] { new TandavaForm { PlanCount = b.Plans[b.Ix[key]].N } }, DirectorSettings(), 1);
                if (second >= d.SeverMinMembers) { falseParts++; }
                if (second > worstSecond) { worstSecond = second; worstKey = key; }
            }
            Console.WriteLine($"    the biggest loose knot on any whole body: {worstSecond} members ({worstKey})");
            Check(falseParts == 0, $"no whole body, in any pose it wears, reads as two pieces ({falseParts} did)");
            foreach (var key in new[] { "great_serpent_1", "many_headed_7", "antlion_2" })
            {
                var c = MakeCore(b, b.Ix[key], Vector3.Zero, 5, fed: true);
                Step(c, 60);
                int before = Active(c);
                int killed = Slice(c, 0.3f, SliceWidth);
                var (_, big, second) = Pieces(c);
                Console.WriteLine($"    {key}: a slice 30% from the tail killed {killed}; pieces {big} + {second}");
                var d = new TandavaDirectorCore(new[] { new TandavaForm { PlanCount = before } }, DirectorSettings(), 1);
                Check(second >= d.SeverMinMembers && second <= d.SeverMaxMembers, $"{key}: the slice parted off the tail ({second} of {before}; a piece is {d.SeverMinMembers}-{d.SeverMaxMembers})");
            }
        }

        // ── T21: the Severed crawls off - the members and the stomach are conserved, and it is a creature of its own
        Console.WriteLine("T21 the Severed");
        {
            var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 71);
            s.C.Stomach[1] = 0.12f * StomachCapacity;   // under the Great Serpent's bank: it stays a serpent
            RunFor(s, 6f);
            int before = Active(s.C); float stomach = StomachTotal(s.C);
            int killed = 0; float stomachAtCut = 0f;
            Tick(s, x => { killed = Slice(x.C, 0.32f, SliceWidth); x.Lost += killed; stomachAtCut = StomachTotal(x.C); });
            Check(s.Severs == 1 && s.Pieces.Count == 1, $"a slice through the Great Serpent parted it ({SeverStory(s)})");
            if (s.Pieces.Count == 1)
            {
                var p = s.Pieces[0];
                int pieceNow = Active(p.C);
                var (was, moved, left, split) = s.LastSever;
                Check(moved + left == was, $"every member is accounted for: {left} stay + {moved} crawl off = {was} after the cut (nothing dies, nothing is born)");
                Check(MathF.Abs(split - stomachAtCut) < 1e-2f * MathF.Max(1f, stomachAtCut), $"the stomach is conserved across the split ({stomachAtCut:F0} -> {split:F0}, shared by members)");
                Check(s.D.Mood == TandavaMood.Fleeing, "the body bolts - cut in two is the worst wound there is");
                var b0 = s.C.Anchor; var p0 = p.C.Anchor;
                RunFor(s, 5f);
                float gap = Vector3.Distance(s.C.Anchor, p.C.Anchor) * UnitScale, gap0 = Vector3.Distance(b0, p0) * UnitScale;
                Check(gap > gap0 + 40f, $"the piece crawled off on its own: {gap0:F0} u -> {gap:F0} u from the body in 5 s");
                Check(p.C.PlanIx == b.Ix["severed"] || p.C.PlanIx == b.Ix["severed_feed"], $"it wears the Severed's own body ({Keys[p.C.PlanIx]})");
                Check(Active(p.C) > pieceNow, $"it regrows out of its share of the stomach: {pieceNow} -> {Active(p.C)}");
            }
        }

        // ── T22: home - unopposed, the piece regrows, eats, turns for home and rejoins; the body is whole again
        Console.WriteLine("T22 home");
        {
            var s = MakeSim(b, new[] { 1, 0, 0, 0 }, 73);
            s.C.Stomach[1] = 0.12f * StomachCapacity;
            RunFor(s, 6f);
            Tick(s, x => x.Lost += Slice(x.C, 0.3f, SliceWidth));
            float t0 = s.Now;
            while (s.Rejoins == 0 && s.Pieces.Count > 0 && s.D.Outcome == TandavaOutcome.Running && s.Now - t0 < 150f)
            {
                Tick(s);
                if (Environment.GetEnvironmentVariable("TANDAVA_DIAG") == "home" && (int)MathF.Round(s.Now * TickHz) % 50 == 0 && s.Pieces.Count > 0)
                {
                    var p = s.Pieces[0];
                    Console.WriteLine($"      {s.Now:F0} s piece {Active(p.C)} stomach {StomachTotal(p.C):F0} eaten {p.Eaten:F0} phase {p.D.Phase} mood {p.D.Mood} food {p.D.TargetFood} progress {p.D.Progress(new TandavaSwarmState { Alive = Active(p.C), Stomach1 = StomachTotal(p.C) }):F2}" +
                                      $" | body {Active(s.C)} stomach {StomachTotal(s.C):F0} {s.D.Phase} {s.D.Mood}");
                }
            }
            Console.WriteLine($"    {SeverStory(s)}");
            Check(s.Severs == 1 && s.Rejoins == 1, $"the Severed came home and rejoined {s.Now - t0:F0} s after the cut ({s.PieceMembersHome} members)");
            Check(s.Now - t0 <= DirectorSettings().RejoinAfterSeconds + 25f, "within its clock and the swim home");
            Check(s.D.Outcome == TandavaOutcome.Running, "the creature is one again, in its form");
        }

        // ── T23: the hydra - sever a FED creature and both halves regrow (it becomes MORE); a starved one stays cut
        Console.WriteLine("T23 the hydra");
        {
            int Grown(bool fed)
            {
                var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 79);
                s.C.Stomach[1] = fed ? 0.13f * StomachCapacity : 0f;
                RunFor(s, 6f);
                if (!fed) for (int e = 0; e < 4; e++) s.C.Stomach[e] = 0f;
                s.Plants.Clear();   // nothing to eat after the cut: what they regrow is what the stomach held
                int before = Active(s.C), cut = 0;
                Tick(s, x => { cut = Slice(x.C, 0.3f, SliceWidth); x.Lost += cut; });
                RunFor(s, 12f);
                int after = Active(s.C) + s.Pieces.Sum(p => Active(p.C));
                Console.WriteLine($"    {(fed ? "fed" : "starved")}: {before} -> cut {cut} -> body {Active(s.C)} + piece {s.Pieces.Sum(p => Active(p.C))} = {after} after 12 s ({s.Severs} sever)");
                return after - before;
            }
            int fedGain = Grown(true), starvedGain = Grown(false);
            Check(fedGain > 0, $"severed fed, the two halves together outgrow the body that was cut (+{fedGain})");
            Check(starvedGain < 0, $"severed starved, it stays cut ({starvedGain})");
        }

        // ── T24: succession - cut the body away to nothing while the piece lives, and the piece takes the form
        Console.WriteLine("T24 succession");
        {
            var s = MakeSim(b, new[] { 0, 0, 0, 0 }, 83);
            s.C.Stomach[1] = 0.12f * StomachCapacity;   // under the Great Serpent's bank: it stays a serpent
            RunFor(s, 6f);
            Tick(s, x => x.Lost += Slice(x.C, 0.3f, SliceWidth));
            int form = s.D.FormIx;
            Tick(s, x => { int n = Active(x.C); for (int i = 0; i < x.C.Cap; i++) if (x.C.Active[i]) x.C.Kill(i); x.Lost += n; });
            RunFor(s, 1f);
            Console.WriteLine($"    {SeverStory(s)}");
            Check(s.Successions == 1 && s.D.Outcome == TandavaOutcome.Running && s.D.FormIx == form,
                  $"the body is gone and the creature lives on in its piece, as the {s.D.Form.Name} ({s.D.Outcome})");
            int heir = Active(s.C);
            Console.WriteLine($"    the heir: {heir} members, stomach {StomachTotal(s.C):F0}, phase {s.D.Phase} mood {s.D.Mood}, laying held {s.C.LayingHeld}");
            RunFor(s, 30f);
            Check(s.C.PlanIx == s.D.Form.PlanIndex || s.C.PlanIx == s.D.WantPlan, $"the heir wears the form's own body ({Keys[s.C.PlanIx]})");
            Check(Active(s.C) > heir, $"and eats its way back into it: {heir} -> {Active(s.C)} in 30 s");
        }

        // ── T25: the shatter counts the whole animal
        Console.WriteLine("T25 the shatter counts both bodies");
        {
            var forms = BuildForms(b, new[] { 0, 0, 0, 0 });
            int n = forms[0].PlanCount;
            TandavaOutcome After(int alive, int severed)
            {
                var d = new TandavaDirectorCore(forms, DirectorSettings(), 1);
                var st = new TandavaSwarmState { Alive = n, Anchor = Vector3.Zero, Forward = Vector3.UnitX, Up = Vector3.UnitY, Side = Vector3.UnitZ };
                d.Tick(0.1f, st, null, null);
                st.Alive = alive; st.Severed = severed;
                d.Tick(0.1f, st, null, null);
                return d.Outcome;
            }
            Check(After((int)(0.3f * n), 0) == TandavaOutcome.Shattered, "body at 30%, no piece: shattered");
            Check(After((int)(0.3f * n), (int)(0.2f * n)) == TandavaOutcome.Running, "body at 30% and a piece of 20%: the animal is half itself - it fights on");
            Check(After(0, (int)(0.2f * n)) == TandavaOutcome.Running, "body gone, a piece alive: not dead (the succession)");
            Check(After((int)(0.2f * n), (int)(0.1f * n)) == TandavaOutcome.Shattered, "body and piece together under 35%: shattered");
        }

        // ── T26: no false severs - an unopposed run, and the T11 denial run's tail cuts, never part the body
        Console.WriteLine("T26 cuts from an end never sever");
        {
            var s = MakeSim(b, new[] { 0, 1, 2, 0 }, 53);
            RunFor(s, 160f);
            Check(s.Severs == 0, $"unopposed, {s.D.Clock:F0} s to the {s.D.Form.Name}: never parted ({s.Severs})");
            // the tail nibbled while it ROAMS (stretched out, so "from the end" is the tail): a wound, never a sever. Coiled at a
            // meal the same cut is a chord across its turns and CAN part it - that is geometry, not a false sever (T11 runs it)
            var t = MakeSim(b, new[] { 0, 1, 2, 0 }, 53);
            t.Plants.Clear();   // nothing to eat: it roams the whole time
            RunFor(t, 40f, x => { if ((int)MathF.Round(x.Now * TickHz) % 10 == 0 && x.D.Phase == TandavaPhase.Roam) x.Lost += Cut(x.C, 6, x.C.BX, i => x.C.EffectiveElement(i) == 0); });
            Check(t.Severs == 0, $"40 s of its tail nibbled 6 at a time while it roams parts nothing ({t.Severs} severs; {Active(t.C)} left)");
        }
    }
}
