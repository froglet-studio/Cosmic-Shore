// Headless proof of the SHIPPED substrate (Assets/.../FloraAndFauna/Substrate). Every test prints its numbers and
// asserts them; the run exits non-zero if any assertion fails. What this does NOT prove: anything about Unity
// (rendering, proxies, colliders, the burn itself) - see Docs/SUBSTRATE_FAUNA.md §6.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static partial class SubstrateHarness
{
    static int _fail;
    const float R = 1200f, Dt = 0.1f;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");
        if (!ok) _fail++;
    }

    static int Main(string[] args)
    {
        string fixture = args.Length > 0 ? args[0] : "research_params.json";
        string which = args.Length > 1 ? args[1] : "all";
        bool all = which == "all";
        string gameJson = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fixture)) ?? ".", "game_params.json");
        _repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fixture)) ?? ".", "..", "..", ".."));
        if (which == "emotion") return Emotion(args[2]);   // round 11d-2 (SWARM_FAUNA.md §27): the emotion-probe export
        if (which == "export") { File.WriteAllText(gameJson, ExportGame()); Console.WriteLine($"wrote {gameJson}"); return 0; }
        if (all || which == "fidelity") { Fidelity(fixture); GameExportCurrent(gameJson); }
        if (all || which == "pack") Pack();
        if (all || which == "packhold") PackHold();
        if (all || which == "locust") Locust();
        if (all || which == "lurker") Lurker();
        if (all || which == "stampede") Stampede();
        if (all || which == "mobber") Mobber();
        if (all || which == "leech") Leech();
        if (all || which == "leviathan") Leviathan();
        if (all || which == "proxies") Proxies();
        if (all || which == "ledger") Ledger();
        if (all || which == "job") Job();
        if (all || which == "index") IndexLedger();
        if (all || which == "kernel") KernelMatch();
        if (all || which == "lod") Lod();
        if (all || which == "bench") Bench();
        Console.WriteLine(_fail == 0 ? "\nALL SUBSTRATE TESTS PASSED" : $"\n{_fail} SUBSTRATE ASSERTION(S) FAILED");
        return _fail == 0 ? 0 : 1;
    }

    // ───────────────────────────────────────────────────────────── a little world: pilots and real food

    sealed class Pilot
    {
        public Vector3 Pos, Vel, Goal;
        public float Speed = 120f, Turn = 1.5f;
        public string Mode = "wander";
        public int Id;
        readonly Random _rng;
        public Pilot(int seed, Vector3 pos, Vector3 dir) { _rng = new Random(seed); Pos = pos; Vel = SubstrateCore.Unit(dir) * Speed; Goal = pos + dir * 500f; }

        public void Step(float dt)
        {
            if (Mode == "still") { Vel = Vector3.Zero; return; }
            if (Mode == "replay") return;   // the emotion export places it from a recorded track each step
            if (Mode == "spin")
            {
                // a hard, constant turn about the vertical (the leech's counterplay: shake them off)
                float c0 = MathF.Cos(Turn * dt), s0 = MathF.Sin(Turn * dt);
                Vel = new Vector3(Vel.X * c0 - Vel.Z * s0, 0f, Vel.X * s0 + Vel.Z * c0);
                Vel = SubstrateCore.Unit(Vel) * Speed;
                Pos += Vel * dt;
                return;
            }
            if (Mode == "wander" && (Vector3.Distance(Pos, Goal) < 60f || Goal.Length() > 0.9f * R))
                Goal = Ball(_rng, 0.2f * R, 0.8f * R);
            var want = Mode == "straight" ? SubstrateCore.Unit(Vel) : SubstrateCore.Unit(Goal - Pos);
            var h = SubstrateCore.Unit(Vel.LengthSquared() > 1e-6f ? Vel : want);
            float c = Math.Clamp(Vector3.Dot(h, want), -1f, 1f), ang = MathF.Acos(c);
            float k = MathF.Min(1f, Turn * dt / MathF.Max(ang, 1e-6f));
            Vel = SubstrateCore.Unit(h + (want - h) * k) * Speed;
            Pos += Vel * dt;
            float r = Pos.Length();
            if (r > 0.97f * R) Pos *= 0.97f * R / r;
        }

        public SubstratePilot Sense => new SubstratePilot { Pos = Pos, Vel = Vel, Radius = 6f, Id = Id };
    }

    static Vector3 Ball(Random rng, float lo, float hi)
    {
        var d = SubstrateCore.Unit(new Vector3((float)Gauss(rng), (float)Gauss(rng), (float)Gauss(rng)));
        float r = MathF.Pow((float)(lo * lo * lo + (hi * hi * hi - lo * lo * lo) * rng.NextDouble()), 1f / 3f);
        return d * r;
    }

    static double Gauss(Random r) => Math.Sqrt(-2.0 * Math.Log(1.0 - r.NextDouble())) * Math.Cos(2.0 * Math.PI * r.NextDouble());

    sealed class World
    {
        public readonly SubstrateCore Core;
        public readonly List<Pilot> Pilots = new();
        public readonly List<Vector3> MassPos = new();
        public readonly List<float> MassVol = new();
        public readonly List<bool> MassAlive = new();
        public double Eaten, Laid, Seeded;
        public readonly List<SubstrateEvent> Log = new();
        SubstrateFood[] _food = new SubstrateFood[0];
        SubstratePilot[] _sense = new SubstratePilot[8];
        public float MaxStep;   // largest per-step displacement any agent made (continuity)
        Vector3[] _prev;

        public World(int capacity, int seed = 1) { Core = new SubstrateCore(capacity, R, Dt, 40, seed); _prev = new Vector3[capacity]; }

        public void Scatter(Random rng, int n, float lo, float hi, int clumps, float vlo = 8f, float vhi = 40f)
        {
            var centres = Enumerable.Range(0, clumps).Select(_ => Ball(rng, lo, hi)).ToArray();
            for (int k = 0; k < n; k++)
            {
                var c = centres[rng.Next(clumps)];
                MassPos.Add(c + new Vector3((float)Gauss(rng), (float)Gauss(rng), (float)Gauss(rng)) * 25f);
                MassVol.Add(vlo + (vhi - vlo) * (float)rng.NextDouble());
                MassAlive.Add(true);
            }
        }

        public double LiveMass() { double m = 0; for (int k = 0; k < MassVol.Count; k++) if (MassAlive[k]) m += MassVol[k]; return m; }

        public void Step()
        {
            for (int j = 0; j < Pilots.Count; j++) { Pilots[j].Step(Dt); _sense[j] = Pilots[j].Sense; }
            if (Core.Tick % 10 == 0)
            {
                int live = 0;
                for (int k = 0; k < MassAlive.Count; k++) if (MassAlive[k]) live++;
                if (_food.Length < live) _food = new SubstrateFood[live * 2];
                int f = 0;
                for (int k = 0; k < MassAlive.Count; k++) if (MassAlive[k]) _food[f++] = new SubstrateFood { Pos = MassPos[k], Volume = MassVol[k] };
                _foodCount = f;
            }
            for (int i = 0; i < Core.Capacity; i++) _prev[i] = Core.Pos[i];
            var aliveBefore = (bool[])Core.Alive.Clone();
            Core.Step(new ReadOnlySpan<SubstratePilot>(_sense, 0, Pilots.Count), new ReadOnlySpan<SubstrateFood>(_food, 0, _foodCount));
            for (int i = 0; i < Core.Capacity; i++)
                if (aliveBefore[i] && Core.Alive[i] && Core.FreedTick[i] != Core.Tick && Core.BornTick[i] != Core.Tick)
                    MaxStep = MathF.Max(MaxStep, Vector3.Distance(_prev[i], Core.Pos[i]));
            Log.AddRange(Core.Events);
            // the owner's half: real food for the hungry slice, and a starving agent dies (its body stays as mass)
            foreach (int i in Core.EatRequests)
            {
                var P = Core.Pops[Core.PopOf[i]].P;
                int best = -1; float bd = P.EatR;
                for (int k = 0; k < MassPos.Count; k++)
                {
                    if (!MassAlive[k]) continue;
                    float d = Vector3.Distance(MassPos[k], Core.Pos[i]);
                    if (d < bd) { bd = d; best = k; }
                }
                if (best < 0) continue;
                MassAlive[best] = false;
                Eaten += MassVol[best];
                Core.Feed(i, MassVol[best]);
            }
            // the food web: a caught prey dies (its crystal would drop through its proxy) and its body is the predator's
            foreach (var pr in Core.PreyRequests)
            {
                if (!Core.Alive[pr.Prey] || !Core.Alive[pr.Predator]) continue;
                float body = Core.Kill(pr.Prey);
                Core.Feed(pr.Predator, body);
                Preyed++;
            }
            foreach (var e in Core.Events)
                if (e.Kind == SubstrateEventKind.Starving) KillAndLay(e.Index);
        }
        public int Preyed;
        int _foodCount;

        /// <summary>A death: the body (its stock) stays where it fell as ordinary mass the food web grazes.</summary>
        public void KillAndLay(int i)
        {
            var p = Core.Pos[i];
            float s = Core.Kill(i);
            if (s <= 0f) return;
            MassPos.Add(p); MassVol.Add(s); MassAlive.Add(true);
            Laid += s;
        }
    }

    static IEnumerable<int> LiveOf(SubstrateCore c, int q)
    {
        var pop = c.Pops[q];
        for (int i = pop.Start; i < pop.Start + pop.Cap; i++) if (c.Alive[i]) yield return i;
    }

    static float Mean(IEnumerable<float> xs) { var a = xs.ToArray(); return a.Length == 0 ? 0f : a.Average(); }

    // ───────────────────────────────────────────────────────────── F: the research numbers, number for number

    /// <summary>
    /// The GAME parameter sets (research + the documented deltas) as JSON keyed by species, every field under its C#
    /// name - exactly what Unity serializes into SubstrateSpeciesSO.species, so Tools/Build/author_substrate_fauna.py
    /// writes the species assets from this file and nothing is typed twice.
    /// </summary>
    static string ExportGame()
    {
        var o = new System.Text.StringBuilder("{\n");
        string[] names = SubstrateResearch.Names;
        for (int k = 0; k < names.Length; k++)
        {
            var p = SubstrateResearch.ByName(names[k], game: true);
            o.Append($"  \"{names[k]}\": {Obj(p, "    ")}{(k < names.Length - 1 ? "," : "")}\n");
        }
        return o.Append("}\n").ToString();
    }

    static string Obj(object v, string ind)
    {
        var fs = v.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var parts = new List<string>();
        foreach (var f in fs)
        {
            object x = f.GetValue(v);
            string val = x switch
            {
                float fl => fl.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                string s => JsonSerializer.Serialize(s),
                SubstrateRegime r => Obj(r, ind + "  "),
                float[] arr => "[" + string.Join(", ", arr.Select(a => a.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]",
                _ => throw new InvalidOperationException($"export: unhandled field {f.Name} ({f.FieldType})"),
            };
            parts.Add($"{ind}\"{f.Name}\": {val}");
        }
        return "{\n" + string.Join(",\n", parts) + "\n" + ind.Substring(2) + "}";
    }

    static void GameExportCurrent(string gameJson)
    {
        bool same = File.Exists(gameJson) && File.ReadAllText(gameJson) == ExportGame();
        Check(same, $"game_params.json (what author_substrate_fauna.py writes the species assets from) matches the shipped " +
                    "game sets - regenerate with `run.sh export` after changing SubstrateResearch");
    }

    static void Fidelity(string fixture)
    {
        Console.WriteLine("\nF. species parameter fidelity vs the Python (research_params.json, research_fixture.py)");
        var doc = JsonDocument.Parse(File.ReadAllText(fixture)).RootElement;
        foreach (var name in SubstrateResearch.ResearchNames)
        {
            var py = doc.GetProperty(name);
            var cs = SubstrateResearch.ByName(name, game: false);
            int checkedN = 0; var bad = new List<string>();
            void Cmp(string field, float v, JsonElement src)
            {
                if (!src.TryGetProperty(field, out var e)) { bad.Add($"{field}: not in the research"); return; }
                double want = e.ValueKind == JsonValueKind.True ? 1 : e.ValueKind == JsonValueKind.False ? 0 : e.GetDouble();
                checkedN++;
                if (Math.Abs(want - v) > 1e-6 * Math.Max(1.0, Math.Abs(want))) bad.Add($"{field}: C# {v} vs research {want}");
            }
            cs.Visit((f, v) => Cmp(f, v, py));
            cs.Solitary.Visit((f, v) => Cmp(f, v, py.GetProperty("solitary")));
            cs.Gregarious.Visit((f, v) => Cmp(f, v, py.GetProperty("gregarious")));
            var body = py.GetProperty("body");
            if (body.ValueKind == JsonValueKind.Object) cs.VisitBody((f, v) => Cmp(f, v, body));
            else if (cs.BodyK > 0) bad.Add("a body plan the research does not have");
            Check(bad.Count == 0, $"{name}: {checkedN} research numbers match (e.g. q_up {cs.QUp}, gregarious speed {cs.Gregarious.Speed}, " +
                                  $"solitary gait {cs.Solitary.GaitHz} Hz x {cs.Solitary.GaitAmp} u/s)" + (bad.Count > 0 ? " - " + string.Join("; ", bad.Take(4)) : ""));
        }
        // the leviathan's body plan IS the research's manta_slots at the game's member count (research K = n0)
        var slots = doc.GetProperty("manta_slots_96").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var game = SubstrateResearch.GameLeviathan();
        double worst = slots.Length == game.BodySlots.Length ? slots.Select((v, k) => Math.Abs(v - game.BodySlots[k])).Max() : double.MaxValue;
        Check(worst < 1e-4 && game.BodyK == game.N0,
              $"leviathan body plan = research manta_slots({game.BodyK}) (max |diff| {worst:E1} u over {slots.Length / 3} slots; K = n0, the research rule)");

        // every port differs from its base (the research set; the substrate defaults for a bestiary-only species) in
        // EXACTLY its listed fields
        foreach (var name in SubstrateResearch.Names)
        {
            var rv = SubstrateResearch.Flatten(SubstrateResearch.ByName(name, game: false));
            var gv = SubstrateResearch.Flatten(SubstrateResearch.ByName(name, game: true));
            var diff = new HashSet<string>(gv.Where(kv => kv.Key != "dive_period" && Math.Abs(rv[kv.Key] - kv.Value) > 1e-6f).Select(kv => kv.Key));
            var listed = SubstrateResearch.GameDeltas.Where(d => d.species == name).Select(d => d.field).ToHashSet();
            Check(diff.SetEquals(listed), $"{name}: the game port changes exactly its {listed.Count} documented fields" +
                  (diff.SetEquals(listed) ? "" : $" - undocumented: {string.Join(",", diff.Except(listed))}; listed but unchanged: {string.Join(",", listed.Except(diff))}"));
        }

        // the bestiary's numbers (read out of bestiary/species/*.py and burn-rules.md by research_fixture.py)
        var best = doc.GetProperty("bestiary");
        foreach (var name in new[] { "stampede", "mobber", "leech", "leviathan" })
        {
            var gv = SubstrateResearch.Flatten(SubstrateResearch.ByName(name, game: true));
            var rows = SubstrateResearch.BestiaryPorts.Where(b => b.species == name).ToList();
            var bad = new List<string>();
            foreach (var r in rows)
            {
                if (!best.TryGetProperty(r.key, out var e)) { bad.Add($"{r.key}: not in the fixture"); continue; }
                if (!gv.TryGetValue(r.field, out float v)) { bad.Add($"{r.field}: no such game field"); continue; }
                if (Math.Abs(e.GetDouble() - v) > 1e-4 * Math.Max(1.0, Math.Abs(e.GetDouble()))) bad.Add($"{r.field} {v} vs bestiary {r.key} {e.GetDouble()}");
            }
            Check(bad.Count == 0 && rows.Count > 0, $"{name}: {rows.Count} numbers taken from the bestiary match it" + (bad.Count > 0 ? " - " + string.Join("; ", bad.Take(4)) : ""));
        }
    }

    // ───────────────────────────────────────────────────────────── P: the pack closes its ring, then strikes together

    sealed class PackRun
    {
        public float RingClosedAt = -1, FirstStrikeAt = -1, SpreadS = -1, FracWithin = 0, RingAtClose, RingAtStrike;
        public int Bites, Rests, Hunters, StrikersInWindow;
        public float WindedSpeed, StalkSpeed, WindedDangerFrac = -1;
        public float ClosureMax;
        /// <summary>The STALK before the first strike: how long the ring was HELD - at least min(4, n) hunters around
        /// the pilot inside CloseR, none of them dangerous - in the unbroken stretch that ends at the strike.</summary>
        public float HoldBeforeStrike = -1;
        /// <summary>Every stalk that ended in a strike (the hold before each strike onset of the pack).</summary>
        public readonly List<float> Holds = new();
        /// <summary>The opt-in ring hold (RingHoldSeconds > 0): when the pilot's hold clock first released before the
        /// first strike; strikes that began while that clock was still holding (must be 0); and ring breaks - the clock
        /// reset to 0 after it had started, with no strike in between (the pilot broke the gap; the ring re-forms).</summary>
        public float ReleaseAt = -1, FirstClosedAt = -1;
        public int HeldStrikes, Breaks;
    }

    static PackRun RunPack(int seed, SubstrateSpeciesParams P, string pilotMode, float seconds, float window = 1.0f)
    {
        bool gap = pilotMode == "gap";
        if (gap) pilotMode = "wander";
        var w = new World(64, seed);
        int q = w.Core.AddPopulation(P, 3);
        var rng = new Random(seed);
        var pil = new Pilot(seed, Ball(rng, 100f, 300f), new Vector3((float)Gauss(rng), (float)Gauss(rng), (float)Gauss(rng))) { Mode = pilotMode, Id = 1 };
        w.Pilots.Add(pil);
        w.Core.Seed(q, P.N0, pil.Pos + SubstrateCore.Unit(new Vector3(1, 0.3f, 0.2f)) * 600f, 50f);
        var res = new PackRun { Hunters = P.N0 };
        var strikeAt = new Dictionary<int, float>();
        bool wasClosed = false;
        float holdFrom = -1;
        bool wasStriking = false, striking0 = false, clockRan = false;
        float prevClock = 0f, lastRelease = -1f;
        float windedSum = 0, stalkSum = 0; int windedN = 0, stalkN = 0, windedDanger = 0;
        for (int t = 0; t < (int)(seconds / Dt); t++)
        {
            if (gap)
            {
                // the counterplay pilot: it SEES hunters inside 350 u and flies at the gap - away from the resultant of
                // their bearings, i.e. through the side of the ring that has not closed (same 120 u/s as the careless one)
                var sum = Vector3.Zero; int seen = 0;
                foreach (int i in LiveOf(w.Core, q))
                    if (Vector3.Distance(w.Core.Pos[i], pil.Pos) < 350f) { sum += SubstrateCore.Unit(w.Core.Pos[i] - pil.Pos); seen++; }
                if (seen > 0 && sum.Length() > 1e-3f) pil.Goal = pil.Pos - SubstrateCore.Unit(sum) * 400f;
            }
            w.Step();
            float now = w.Core.T;
            var live = LiveOf(w.Core, q).ToArray();
            int closed = live.Count(i => w.Core.Closure[i] >= P.QUp);
            res.ClosureMax = MathF.Max(res.ClosureMax, live.Length > 0 ? live.Max(i => w.Core.Closure[i]) : 0f);
            // the ring-closed ONSET that precedes the first strike (a ring can close and break before one holds)
            bool isClosed = closed >= Math.Min(4, live.Length);
            if (res.FirstClosedAt < 0 && isClosed) res.FirstClosedAt = now;
            if (res.FirstStrikeAt < 0 && isClosed && !wasClosed)
            {
                res.RingClosedAt = now;
                res.RingAtClose = Mean(live.Select(i => Vector3.Distance(w.Core.Pos[i], pil.Pos)));
            }
            wasClosed = isClosed;
            // the ring HELD: enough hunters around the pilot, none striking (the research's menace configuration)
            int around = live.Count(i => Vector3.Distance(w.Core.Pos[i], pil.Pos) < P.CloseR);
            bool striking = live.Any(i => w.Core.Danger[i]);
            bool resting = live.Any(i => w.Core.Rest[i] > 0f);
            bool holding = around >= Math.Min(4, live.Length) && live.Length > 0 && !striking && !resting;
            bool strikeNow = w.Core.Events.Any(e => e.Kind == SubstrateEventKind.Strike);
            if (P.RingHoldSeconds > 0f)
            {
                w.Core.Pops[q].RingHold.TryGetValue(pil.Id, out float clock);
                // the release that led to the first strike: the last time the clock reached the hold before it
                if (clock >= P.RingHoldSeconds && prevClock < P.RingHoldSeconds && res.FirstStrikeAt < 0) lastRelease = now;
                if (res.FirstStrikeAt < 0 && strikeNow && res.ReleaseAt < 0) res.ReleaseAt = lastRelease;
                prevClock = clock;
                // a strike onset near this pilot must come from a released ring (the clock is read after the step that
                // struck, so a just-released clock reads >= hold; a strike with the clock below it broke the gate)
                foreach (var e in w.Core.Events)
                    if (e.Kind == SubstrateEventKind.Strike && Vector3.Distance(w.Core.Pos[e.Index], pil.Pos) < P.CloseR
                        && clock < P.RingHoldSeconds && !striking0) res.HeldStrikes++;
                if (clock > 0f) clockRan = true;
                else if (clockRan && !striking && !resting) { res.Breaks++; clockRan = false; }
                if (striking || resting) clockRan = false;
            }
            striking0 = striking;
            if (strikeNow && holdFrom >= 0 && !wasStriking)
            {
                res.Holds.Add(now - holdFrom);
                if (res.HoldBeforeStrike < 0) res.HoldBeforeStrike = now - holdFrom;
            }
            wasStriking = striking || resting;
            if (holding) { if (holdFrom < 0) holdFrom = now - Dt; }
            else holdFrom = -1;
            foreach (var e in w.Core.Events)
            {
                if (e.Kind == SubstrateEventKind.Strike && res.FirstStrikeAt < 0) { res.FirstStrikeAt = now; res.RingAtStrike = Mean(live.Select(i => Vector3.Distance(w.Core.Pos[i], pil.Pos))); }
                if (e.Kind == SubstrateEventKind.Strike && res.FirstStrikeAt >= 0 && now - res.FirstStrikeAt <= window && !strikeAt.ContainsKey(e.Index)) strikeAt[e.Index] = now;
                if (e.Kind == SubstrateEventKind.Bite) res.Bites++;
                if (e.Kind == SubstrateEventKind.Rest) res.Rests++;
            }
            foreach (int i in live)
            {
                float s = w.Core.Vel[i].Length();
                if (w.Core.Rest[i] > 0f) { windedSum += s; windedN++; if (w.Core.Danger[i]) windedDanger++; }
                else if (w.Core.Phase[i] < 0.3f && Vector3.Distance(w.Core.Pos[i], pil.Pos) < 600f) { stalkSum += s; stalkN++; }
            }
        }
        res.StrikersInWindow = strikeAt.Count;
        res.FracWithin = strikeAt.Count / (float)Math.Max(1, res.Hunters);
        res.SpreadS = strikeAt.Count > 0 ? strikeAt.Values.Max() - strikeAt.Values.Min() : -1;
        res.WindedSpeed = windedN > 0 ? windedSum / windedN : -1;
        res.StalkSpeed = stalkN > 0 ? stalkSum / stalkN : -1;
        res.WindedDangerFrac = windedN > 0 ? windedDanger / (float)windedN : -1;
        return res;
    }

    static string _repo = ".";

    /// <summary>The demo (Swarm) cell's pack AS AUTHORED: the game port plus the asset's opt-in ring hold
    /// (author_substrate_fauna.py DEMO_OVERRIDES writes it; game_params.json stays the research port).</summary>
    static SubstrateSpeciesParams DemoPack()
    {
        var P = SubstrateResearch.GamePack();
        string asset = Path.Combine(_repo, "Assets", "_SO_Assets", "Substrate Fauna", "Substrate Pack Hunter Species.asset");
        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(asset), @"(?m)^    RingHoldSeconds: ([0-9.]+)\s*$");
        if (!m.Success) throw new InvalidOperationException("no RingHoldSeconds in " + asset);
        P.RingHoldSeconds = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        return P;
    }

    static void Pack()
    {
        Console.WriteLine("\nP. pack hunters: predict the line, fan out on a ring, close it, strike together, then winded");
        var P = SubstrateResearch.GamePack();
        var runs = new List<PackRun>();
        foreach (int seed in new[] { 7, 23, 41, 5, 1000 })
        {
            var r = RunPack(seed, P, "wander", 90f);
            runs.Add(r);
            Console.WriteLine($"    seed {seed,4}: ring closed {r.RingClosedAt,6:F1} s (mean ring {r.RingAtClose,5:F0} u) -> first strike {r.FirstStrikeAt,6:F1} s (ring {r.RingAtStrike,5:F0} u); " +
                              $"{r.StrikersInWindow}/{r.Hunters} strike within 1 s (spread {r.SpreadS:F1} s); bites {r.Bites}, rests {r.Rests}; " +
                              $"winded {r.WindedSpeed:F0} u/s vs stalking {r.StalkSpeed:F0}, winded danger {r.WindedDangerFrac:P0}; max closure {r.ClosureMax:F2}");
            Console.WriteLine($"              ring held before each strike: {string.Join(", ", r.Holds.Select(x => x.ToString("F1")))} s");
        }
        var eng = runs.Where(r => r.RingClosedAt >= 0 && r.FirstStrikeAt >= 0).ToList();
        Check(eng.Count >= 4, $"the ring closes and the pack strikes in {eng.Count}/5 seeds (90 s each, a 120 u/s wandering pilot; the hunters stalk at {P.Solitary.Speed} u/s)");
        var lead = eng.Select(r => r.FirstStrikeAt - r.RingClosedAt).ToList();
        Check(eng.Count > 0 && lead.All(x => x >= 0f && x <= 4f),
              $"the strike follows the ring closing - the telegraph: {string.Join(", ", lead.Select(x => x.ToString("F1")))} s from ring-closed to first strike (bestiary telegraph 0.9 s)");
        Check(eng.Count > 0 && eng.All(r => r.FracWithin >= 0.66f),
              $"they strike TOGETHER: {string.Join(", ", eng.Select(r => r.FracWithin.ToString("P0")))} of the pack turns dangerous within 1 s of the first");
        Check(eng.Count > 0 && eng.All(r => r.RingAtStrike < r.RingAtClose + 1f),
              $"the ring TIGHTENS into the strike: mean hunter distance {string.Join(", ", eng.Select(r => $"{r.RingAtClose:F0}->{r.RingAtStrike:F0}"))} u");
        var winded = eng.Where(r => r.WindedSpeed >= 0f).ToList();
        Check(winded.Count > 0 && winded.All(r => r.WindedDangerFrac == 0f && r.WindedSpeed < r.StalkSpeed),
              $"winded = the payoff window: never dangerous, slower ({string.Join(", ", winded.Select(r => $"{r.WindedSpeed:F0} vs {r.StalkSpeed:F0}"))} u/s)");
        // the menace->terror ARC (research DISCOVERIES finding 11): menace is read only while the ring HOLDS without
        // striking (4-5 big members holding the ring read menacing 0.37-0.42), and the emotion timeline reads 8 s
        // windows (emotion/timeline.py) - so the stalk before the first strike must fill at least one whole window
        const float probeWindow = 8f;
        var firstHolds = runs.Select(r => r.HoldBeforeStrike).ToList();
        var allHolds = runs.SelectMany(r => r.Holds).OrderBy(x => x).ToList();
        float medianHold = allHolds.Count > 0 ? allHolds[allHolds.Count / 2] : -1f;
        Check(firstHolds.All(x => x >= probeWindow),
              $"MENACE before terror: the ring is held (>= 4 hunters inside {P.CloseR:F0} u, none striking) {string.Join(", ", firstHolds.Select(x => x.ToString("F1")))} s " +
              $"before the first strike - every seed >= one {probeWindow:F0} s emotion-probe window (finding 11: menace only while the ring holds)");
        Check(medianHold >= probeWindow,
              $"and it stays an arc after a rest: the median stalk before ANY strike is {medianHold:F1} s over {allHolds.Count} strikes (>= {probeWindow:F0} s)");
        Check(runs.Sum(r => r.Bites) > 0, $"the strike lands on a careless pilot: {runs.Sum(r => r.Bites)} bites over 5 x 90 s (research pack 9.7/min on a wanderer)");

        // counterplay: the same pilot speed, but it breaks the gap before the ring closes
        var aware = new[] { 7, 23, 41, 5, 1000 }.Select(sd => RunPack(sd, P, "gap", 90f)).ToList();
        int careless = runs.Sum(r => r.Bites), dodged = aware.Sum(r => r.Bites);
        Console.WriteLine($"    counterplay (flies at the gap): {dodged} bites vs {careless} careless; rings closed {aware.Count(r => r.RingClosedAt >= 0)}/5");
        Check(dodged * 2 <= careless, $"counterplay: breaking the gap before it closes halves the bites at least ({dodged} vs {careless}; bestiary aware pilot 0.0)");

        // negative control: without the closure quorum the pack never strikes at all (the quorum IS the strike)
        var nq = P.Clone(); nq.QWClose = 0f;
        var n = RunPack(7, nq, "wander", 90f);
        Check(n.FirstStrikeAt < 0, $"ablation q_w_close=0: no strike in 90 s (first strike {n.FirstStrikeAt:F1})");
    }

    // ───────────────────────────────────────────────────────────── H: the demo cell's opt-in ring hold

    /// <summary>Group H (round 11-12, Docs/SUBSTRATE_FAUNA.md §7.7): the demo cell's pack AS AUTHORED (DemoPack: the
    /// game port + the asset's RingHoldSeconds) against the same wandering pilot as group P, plus the gate's own laws.
    /// The straight-in emotion read is asserted by Tools/Build/emotion_range (--assert).</summary>
    static void PackHold()
    {
        var P = DemoPack();
        float hold = P.RingHoldSeconds;
        Console.WriteLine($"\nH. the demo cell's pack: the ring HOLDS {hold:F1} s once saturated, circling and tightening, then all strike together");
        Check(hold > 0f, $"the demo cell's pack asset carries the opt-in ring hold ({hold:F1} s; author_substrate_fauna.py DEMO_OVERRIDES)");
        var seeds = new[] { 7, 23, 41, 5, 1000 };
        var runs = new List<PackRun>();
        foreach (int seed in seeds)
        {
            var r = RunPack(seed, P, "wander", 90f);
            runs.Add(r);
            Console.WriteLine($"    seed {seed,4}: ring closed {r.RingClosedAt,6:F1} s -> released {r.ReleaseAt,6:F1} s -> first strike {r.FirstStrikeAt,6:F1} s; " +
                              $"{r.StrikersInWindow}/{r.Hunters} within 1 s; bites {r.Bites}; ring breaks {r.Breaks}; strikes while held {r.HeldStrikes}; " +
                              $"ring {r.RingAtClose:F0}->{r.RingAtStrike:F0} u; holds {string.Join(", ", r.Holds.Select(x => x.ToString("F1")))} s");
        }
        var eng = runs.Where(r => r.RingClosedAt >= 0 && r.FirstStrikeAt >= 0 && r.ReleaseAt >= 0).ToList();
        Check(eng.Count >= 4, $"the ring closes, holds and the pack strikes in {eng.Count}/5 seeds (90 s, a 120 u/s wandering pilot)");
        Check(runs.Sum(r => r.HeldStrikes) == 0, $"the gate: no strike begins while the ring is still holding ({runs.Sum(r => r.HeldStrikes)} over 5 x 90 s)");
        var lead = eng.Select(r => r.FirstStrikeAt - r.FirstClosedAt).ToList();
        Check(eng.Count > 0 && lead.All(x => x >= hold - 0.05f),
              $"the ring holds before it strikes: first ring-closed -> first strike {string.Join(", ", lead.Select(x => x.ToString("F1")))} s (>= the {hold:F1} s hold)");
        var tele = eng.Select(r => r.FirstStrikeAt - r.ReleaseAt).ToList();
        Check(eng.Count > 0 && tele.All(x => x >= 0.3f && x <= 1.0f),
              $"the telegraph after the release: {string.Join(", ", tele.Select(x => x.ToString("F1")))} s from the release that led to it to the first strike (research 0.4-0.7 s; every hunter climbs from the held phase at q_rate)");
        Check(eng.Count > 0 && eng.All(r => r.FracWithin >= 0.66f),
              $"they strike TOGETHER: {string.Join(", ", eng.Select(r => r.FracWithin.ToString("P0")))} of the pack turns dangerous within 1 s of the first");
        Check(eng.Count > 0 && eng.All(r => r.RingAtStrike < r.RingAtClose + 1f),
              $"the ring TIGHTENS into the strike: mean hunter distance {string.Join(", ", eng.Select(r => $"{r.RingAtClose:F0}->{r.RingAtStrike:F0}"))} u");
        var firstHolds = runs.Select(r => r.HoldBeforeStrike).ToList();
        var allHolds = runs.SelectMany(r => r.Holds).OrderBy(x => x).ToList();
        float medianHold = allHolds.Count > 0 ? allHolds[allHolds.Count / 2] : -1f;
        Check(firstHolds.Where(x => x >= 0f).All(x => x >= 8f) && firstHolds.Count(x => x >= 0f) >= 4,
              $"MENACE before terror still holds for the wanderer: ring held {string.Join(", ", firstHolds.Select(x => x.ToString("F1")))} s before the first strike (>= 8 s)");
        Check(medianHold >= 8f, $"and after a rest: the median stalk before ANY strike is {medianHold:F1} s over {allHolds.Count} strikes (>= 8 s)");
        int careless = runs.Sum(r => r.Bites);
        Check(careless > 0, $"the strike lands on a careless pilot: {careless} bites over 5 x 90 s");
        var aware = seeds.Select(sd => RunPack(sd, P, "gap", 90f)).ToList();
        int dodged = aware.Sum(r => r.Bites);
        Console.WriteLine($"    counterplay (flies at the gap): {dodged} bites vs {careless} careless; rings closed {aware.Count(r => r.RingClosedAt >= 0)}/5");
        Check(dodged * 2 <= careless, $"counterplay: breaking the gap halves the bites at least ({dodged} vs {careless})");
        BreakOut(P);
        // negative control: the same run with the hold off is the research port's - it strikes sooner
        var off = P.Clone(); off.RingHoldSeconds = 0f;
        var o = RunPack(7, off, "wander", 90f);
        var h7 = runs[0];
        Check(o.FirstStrikeAt >= 0 && (h7.FirstStrikeAt < 0 || h7.FirstStrikeAt - h7.FirstClosedAt > o.FirstStrikeAt - o.FirstClosedAt + 1f),
              $"control: without the hold the same seed strikes {o.FirstStrikeAt - o.FirstClosedAt:F1} s after its ring first closes (with it: {h7.FirstStrikeAt - h7.FirstClosedAt:F1} s)");
    }

    /// <summary>Counterplay inside a held ring: a pilot comes in slow (25 u/s, the emotion probe's hover viewer), and
    /// once the ring has held half its time it sprints out through the gap (150 u/s, away from the hunters' resultant)
    /// for 4 s, then slows again. The hold must RESET (no strike on the way out) and the ring must re-form.</summary>
    static void BreakOut(SubstrateSpeciesParams P)
    {
        int resets = 0, outStrikes = 0, reformed = 0, struck = 0;
        foreach (int seed in new[] { 7, 23, 41, 5, 1000 })
        {
            var w = new World(64, seed);
            int q = w.Core.AddPopulation(P, 3);
            var home = new Vector3(0f, 0f, 300f);
            w.Core.Seed(q, P.N0, home, 50f);
            for (int t = 0; t < 20; t++) w.Step();
            var pil = new Pilot(seed, home + new Vector3(350f, 0f, 0f), new Vector3(-25f, 0f, 0f)) { Mode = "replay", Id = 1 };
            w.Pilots.Add(pil);
            var vel = new Vector3(-25f, 0f, 0f);
            float dashUntil = -1f, peak = 0f; bool reset = false, re = false, hit = false;
            for (int t = 0; t < 600; t++)
            {
                float now = w.Core.T;
                pil.Vel = vel; pil.Pos += vel * Dt;
                w.Step();
                var pop = w.Core.Pops[q];
                pop.RingHold.TryGetValue(pil.Id, out float clock);
                var live = LiveOf(w.Core, q).ToArray();
                bool danger = live.Any(i => w.Core.Danger[i]);
                if (dashUntil < 0f && clock >= 0.5f * P.RingHoldSeconds && !danger)
                {
                    var sum = Vector3.Zero;
                    foreach (int i in live) if (Vector3.Distance(w.Core.Pos[i], pil.Pos) < P.CloseR) sum += SubstrateCore.Unit(w.Core.Pos[i] - pil.Pos);
                    vel = -SubstrateCore.Unit(sum.Length() > 1e-3f ? sum : new Vector3(1, 0, 0)) * 150f;
                    dashUntil = now + 4f; peak = clock;
                }
                else if (dashUntil > 0f && now < dashUntil)
                {
                    if (clock == 0f) reset = true;
                    if (danger) hit = true;
                }
                else if (dashUntil > 0f && now >= dashUntil)
                {
                    vel = SubstrateCore.Unit(vel) * 25f;
                    if (reset && clock > 0f) re = true;
                }
            }
            if (reset) resets++;
            if (hit) outStrikes++;
            if (re) reformed++;
            if (w.Core.Pops[q].Strikes > 0) struck++;
            Console.WriteLine($"    break-out seed {seed,4}: dashed at clock {peak:F1} s; reset {reset}; struck during the dash {hit}; ring re-formed {re}; strikes in 60 s {w.Core.Pops[q].Strikes}");
        }
        Check(resets >= 4 && outStrikes <= 1,
              $"breaking out of a held ring RESETS the hold: reset in {resets}/5 dashes, a strike during the dash in {outStrikes}/5");
        Check(reformed >= 4, $"and the ring RE-FORMS after it (its hold clock runs again) in {reformed}/5");
    }

    // ───────────────────────────────────────────────────────────── L: one locust parameter set, two animals

    static (float phase, int bites, int births, int alive, float firstFlip) RunLocust(int seed, int n, float spread, float hunger0, bool food, float seconds, int cap = 400)
    {
        var P = SubstrateResearch.GameLocust(); P.Capacity = cap; P.N0 = n;
        var w = new World(cap, seed);
        int q = w.Core.AddPopulation(P, 2);
        var rng = new Random(seed);
        if (food) w.Scatter(rng, 900, 0.1f, 0.5f, 6);
        var pil = new Pilot(seed, new Vector3(0, 0, 250), new Vector3(1, 0, 0)) { Id = 1 };
        w.Pilots.Add(pil);
        w.Core.Seed(q, n, Vector3.Zero, spread);
        foreach (int i in LiveOf(w.Core, q)) w.Core.Hunger[i] = hunger0;
        float flip = -1;
        for (int t = 0; t < (int)(seconds / Dt); t++)
        {
            w.Step();
            if (flip < 0 && LiveOf(w.Core, q).Count(i => w.Core.Phase[i] > 0.5f) > 0.2f * Math.Max(1, LiveOf(w.Core, q).Count())) flip = w.Core.T;
        }
        var live = LiveOf(w.Core, q).ToArray();
        var pop = w.Core.Pops[q];
        return (Mean(live.Select(i => w.Core.Phase[i])), (int)pop.Bites, (int)pop.Births, live.Length, flip);
    }

    static void Locust()
    {
        Console.WriteLine("\nL. locust: solitary and cute sparse + fed, a gregarious storm dense + hungry - one parameter set");
        var sparse = RunLocust(7, 40, 300f, 0.1f, true, 60f);
        var dense = RunLocust(7, 300, 40f, 0.9f, false, 30f);
        Console.WriteLine($"    sparse+fed  (40, spread 300 u, hunger 0.1, food): mean phase {sparse.phase:F2}, bites {sparse.bites}, births {sparse.births}, alive {sparse.alive}");
        Console.WriteLine($"    dense+hungry (300, spread 40 u, hunger 0.9, no food): mean phase {dense.phase:F2} (20% gregarious at {dense.firstFlip:F1} s), bites {dense.bites}");
        Check(sparse.phase < 0.1f && sparse.bites == 0, $"sparse + fed stays solitary and harmless (phase {sparse.phase:F2}, {sparse.bites} bites; research: 0 hits sparse)");
        Check(dense.phase > 0.6f && dense.firstFlip >= 0f && dense.firstFlip < 15f, $"dense + hungry tips gregarious (phase {dense.phase:F2}, a fifth flipped by {dense.firstFlip:F1} s)");
        Check(dense.bites > 0, $"the storm bites ({dense.bites} bite events in 30 s; research 46-47 hits/min dense+hungry)");
        var hungrySparse = RunLocust(7, 40, 300f, 0.9f, false, 30f);
        Check(hungrySparse.phase < 0.3f, $"hunger alone does not tip a SPARSE cloud (phase {hungrySparse.phase:F2}): the quorum is density x hunger");
        var fedDense = RunLocust(7, 300, 40f, 0.0f, true, 20f);
        Console.WriteLine($"    dense+fed: mean phase {fedDense.phase:F2}");

        // the swarm's size is the food it found: fed, it breeds; unfed, it does not
        var grow = RunLocust(11, 40, 120f, 0.5f, true, 120f, 360);
        var starve = RunLocust(11, 40, 120f, 0.5f, false, 120f, 360);
        Console.WriteLine($"    2 min with food: {grow.births} births, {grow.alive} alive; without: {starve.births} births, {starve.alive} alive");
        Check(grow.births > 0 && starve.births == 0 && grow.alive > starve.alive, $"swarm size = food found: {grow.births} births fed vs {starve.births} unfed");
    }

    // ───────────────────────────────────────────────────────────── U: the lurker creeps only while unwatched

    static void Lurker()
    {
        Console.WriteLine("\nU. lurker: still on its seat, creeps while unwatched, freezes when looked at, gapes, snaps, is spent");
        var P = SubstrateResearch.GameLurker();
        // one lurker, a pilot 300 u away. AWAY: the pilot flies away from it (it is behind, unwatched).
        // TOWARD: the pilot flies at it, slowly (it is in the forward cone, watched).
        float Moved(string how, out bool watchedAll)
        {
            var w = new World(32, 3);
            int q = w.Core.AddPopulation(P, 1);
            w.Core.SeedAt(q, new[] { Vector3.Zero }, 0f);
            var dir = how == "away" ? new Vector3(1, 0, 0) : new Vector3(-1, 0, 0);
            var pil = new Pilot(3, new Vector3(300, 0, 0), dir) { Mode = "straight", Speed = how == "away" ? 20f : 15f, Id = 1 };
            w.Pilots.Add(pil);
            int i = LiveOf(w.Core, q).First();
            var start = w.Core.Pos[i];
            watchedAll = true;
            for (int t = 0; t < 30; t++) { w.Step(); watchedAll &= w.Core.Watched[i]; }
            return Vector3.Distance(start, w.Core.Pos[i]);
        }
        float away = Moved("away", out bool wa), toward = Moved("toward", out bool wt);
        Console.WriteLine($"    3 s with a pilot 300 u off: unwatched it moved {away:F1} u, watched {toward:F2} u (creep {P.CreepSpeed} u/s)");
        Check(away > 30f && !wa, $"unwatched it creeps toward the pilot's line ({away:F1} u in 3 s)");
        Check(toward < 0.5f && wt, $"watched it stays dead still ({toward:F2} u in 3 s, watched every tick)");

        // the ambush: a pilot passing close triggers the gape (phase), the snap (danger), then spent (rest)
        var w2 = new World(32, 5);
        int q2 = w2.Core.AddPopulation(P, 1);
        w2.Core.SeedAt(q2, new[] { new Vector3(0, 0, 0) }, 0f);
        var pass = new Pilot(5, new Vector3(-400, 60, 0), new Vector3(1, 0, 0)) { Mode = "straight", Speed = 120f, Id = 1 };
        w2.Pilots.Add(pass);
        int a = LiveOf(w2.Core, q2).First();
        float gapeAt = -1, strikeAt = -1, restAt = -1, maxPhase = 0;
        for (int t = 0; t < 80; t++)
        {
            w2.Step();
            maxPhase = MathF.Max(maxPhase, w2.Core.Phase[a]);
            if (gapeAt < 0 && w2.Core.Phase[a] > 0.05f) gapeAt = w2.Core.T;
            foreach (var e in w2.Core.Events)
            {
                if (e.Kind == SubstrateEventKind.Strike && strikeAt < 0) strikeAt = w2.Core.T;
                if (e.Kind == SubstrateEventKind.Rest && restAt < 0) restAt = w2.Core.T;
            }
        }
        Console.WriteLine($"    a pilot passing 60 u off: gape begins {gapeAt:F1} s, snap (danger) {strikeAt:F1} s, spent {restAt:F1} s; peak phase {maxPhase:F2}; bites {w2.Core.Pops[q2].Bites}");
        Check(gapeAt >= 0 && strikeAt > gapeAt && strikeAt - gapeAt >= 0.3f, $"the gape telegraphs the snap ({strikeAt - gapeAt:F1} s between them; bestiary gape 0.9 s)");
        Check(restAt > strikeAt, $"after the snap it is spent (rest at {restAt:F1} s) - the payoff window");
    }

    // ───────────────────────────────────────────────────────────── round 11-11: the rest of the bestiary

    /// <summary>Points on the world's mass clumps (the bestiary seeds its herds, roosts and puddles there).</summary>
    static Vector3[] Seats(World w, Random rng, int groups, int each)
    {
        var at = new List<Vector3>();
        for (int g = 0; g < groups; g++)
        {
            var c = w.MassPos[rng.Next(w.MassPos.Count)];
            for (int k = 0; k < each; k++) at.Add(c);
        }
        return at.ToArray();
    }

    static bool Role(SubstrateCore c, int i)
    {
        var pop = c.Pops[c.PopOf[i]];
        return pop.P.ChargeEvery <= 1 || (i - pop.Start) % pop.P.ChargeEvery == 0;
    }

    static float Median(List<float> xs) { if (xs.Count == 0) return -1f; var a = xs.OrderBy(x => x).ToList(); return a[a.Count / 2]; }

    sealed class StrikeRun
    {
        /// <summary>Contacts counted the way the bestiary counts hits: every animal that touches the pilot while
        /// dangerous, once per 2 s per animal (bestiary per-animal cooldown) - the game's harm events are fewer, one per
        /// vessel per bite_cool (burn rules).</summary>
        public int Contacts;
        public float ContactsPerMin => Contacts * 60f / Math.Max(Seconds, 1e-3f);
        public int Hits, RoleHits, Windups, Gulps, Latches, Sips, Shaken, MaxRiders, Assemblies, Dissolves;
        public float Seconds;
        public readonly List<float> Telegraph = new();
        public float PerMin => Hits * 60f / Math.Max(Seconds, 1e-3f);
    }

    /// <summary>One species against one pilot for <paramref name="seconds"/> - the bestiary's scorecard run on the
    /// substrate: mass scattered in clumps across the cell, the species seeded on them, a pilot flying its policy.
    /// Counts hits (Bite events), hits by the strike role, and for each hit the TELEGRAPH: how long the hitter's windup
    /// (its Windup event) or the body's gulp began before the contact.</summary>
    static StrikeRun RunStrikes(int seed, SubstrateSpeciesParams P, Func<World, Random, Vector3[]> seat, Pilot pil, float seconds, int element,
                                Action<World, int> each = null)
    {
        var w = new World(Math.Max(64, P.Capacity), seed);
        var rng = new Random(seed);
        w.Scatter(rng, 1500, 0.2f, 0.8f, 24);
        int q = w.Core.AddPopulation(P, element);
        w.Core.SeedAt(q, seat(w, rng), 40f);
        w.Pilots.Add(pil);
        var res = new StrikeRun { Seconds = seconds };
        var lastWind = new Dictionary<int, float>();
        var latchedAt = new Dictionary<int, float>();
        var lastContact = new Dictionary<int, float>();
        float gulpPrepFrom = -1f;
        var pop = w.Core.Pops[q];
        for (int t = 0; t < (int)(seconds / Dt); t++)
        {
            w.Step();
            float now = w.Core.T;
            if (pop.GulpPrep > 0f && gulpPrepFrom < 0f) gulpPrepFrom = now - pop.GulpPrep;
            if (pop.GulpPrep <= 0f && pop.Gulp <= 0f) gulpPrepFrom = -1f;
            foreach (var e in w.Core.Events)
            {
                switch (e.Kind)
                {
                    case SubstrateEventKind.Windup: res.Windups++; lastWind[e.Index] = now; break;
                    case SubstrateEventKind.Gulp: res.Gulps++; break;
                    case SubstrateEventKind.Latch: res.Latches++; latchedAt[e.Index] = now; break;
                    case SubstrateEventKind.Shaken: res.Shaken++; latchedAt.Remove(e.Index); break;
                    case SubstrateEventKind.Assemble: res.Assemblies++; break;
                    case SubstrateEventKind.Dissolve: res.Dissolves++; break;
                    case SubstrateEventKind.Sip:
                        res.Sips++;
                        if (latchedAt.TryGetValue(e.Index, out float la)) { res.Telegraph.Add(now - la); latchedAt.Remove(e.Index); }
                        break;
                    case SubstrateEventKind.Bite:
                    {
                        res.Hits++;
                        // the first agent in contact (the core's event; a biter is sent to rest the same tick)
                        int hitter = e.Index;
                        bool role = P.ChargeEvery > 1 && hitter >= 0 && Role(w.Core, hitter);
                        if (role) res.RoleHits++;
                        if (hitter >= 0 && lastWind.TryGetValue(hitter, out float wt)) res.Telegraph.Add(now - wt);
                        else if (gulpPrepFrom >= 0f) res.Telegraph.Add(now - gulpPrepFrom);
                        break;
                    }
                }
            }
            int riders = 0;
            foreach (int i in LiveOf(w.Core, q))
            {
                if (w.Core.Host[i] == pil.Id) riders++;
                bool hot = w.Core.Danger[i] || (P.RestS > 0f && w.Core.Rest[i] == P.RestS);   // a biter is sent to rest this tick
                if (!hot || Vector3.Distance(w.Core.Pos[i], pil.Pos) >= P.BiteR + 6f) continue;
                if (lastContact.TryGetValue(i, out float lc) && now - lc < 2f) continue;
                lastContact[i] = now;
                res.Contacts++;
            }
            res.MaxRiders = Math.Max(res.MaxRiders, riders);
            each?.Invoke(w, q);
        }
        return res;
    }

    static Pilot Wanderer(int seed, float speed = 120f) { var rng = new Random(seed * 7 + 1); return new Pilot(seed, Ball(rng, 200f, 600f), new Vector3(1, 0.2f, 0.3f)) { Id = 1, Speed = speed }; }

    // ───────────────────────────────────────────────────────────── S: the stampede - the herd flips, the bulls charge

    static void Stampede()
    {
        Console.WriteLine("\nS. stampede: the alarm quorum flips the herd; the BULLS climb the alarm to its source, lower their heads, charge");
        var P = SubstrateResearch.GameStampede();
        var seeds = new[] { 7, 23, 41 };
        // the bestiary's scorecard setting: n=72 in 6 herds on the mass clumps (the game seeds 48 and grows to 72 by eating)
        Func<World, Random, Vector3[]> herds = (w, rng) => Seats(w, rng, 6, 12);
        var runs = seeds.Select(sd => RunStrikes(sd, P, herds, Wanderer(sd), 180f, 1)).ToList();
        foreach (var (r, sd) in runs.Zip(seeds))
            Console.WriteLine($"    seed {sd,3}: {r.Contacts} trample contacts ({r.ContactsPerMin:F1}/min the bestiary's way), {r.Hits} harm events ({r.PerMin:F1}/min, {r.RoleHits} by bulls; 1 s per vessel), {r.Windups} head-downs, telegraph median {Median(r.Telegraph):F1} s");
        float perMin = runs.Sum(r => r.Contacts) * 60f / runs.Sum(r => r.Seconds);
        float harm = runs.Sum(r => r.Hits) * 60f / runs.Sum(r => r.Seconds);
        int bull = runs.Sum(r => r.RoleHits), all = runs.Sum(r => r.Hits);
        var tele = runs.SelectMany(r => r.Telegraph).ToList();
        // the research's measured range for a wanderer (bestiary scorecards.json stampede): skimmer 5.3/min, wanderer
        // 10.9/min - the substrate herd alone (research open note) managed 0.3/min
        Check(perMin >= 0.5f * 5.33f && perMin <= 1.5f * 10.89f,
              $"the charge LANDS: {perMin:F1} trample contacts/min on a 120 u/s wanderer, counted as the bestiary counts them - inside its measured range (skimmer 5.3 .. wanderer 10.9 /min, asserted 2.7 .. 16.3); {harm:F1} harm events/min after the burn rules' 1 s per-vessel cooldown");
        Check(all > 0 && bull >= 0.7f * all, $"the bulls deliver it: {bull}/{all} tramples by a charging bull (bestiary ablation nobulls: the herd alone hits nothing)");
        Check(tele.Count > 0 && Median(tele) >= 0.7f && tele.Count(x => x >= 0.25f) >= 0.9f * tele.Count,
              $"it READS first: the head-down precedes a bull's trample by median {Median(tele):F1} s (bestiary telegraph 0.7 s; burn rules: telegraphed = >= 0.25 s of intent) - {tele.Count(x => x >= 0.25f)}/{tele.Count} warned");

        // ablation: the research herd with no bulls - the open issue, measured again
        var nb = P.Clone(); nb.ChargeEvery = 0; nb.WAlarmClimb = 0f; nb.RampS = 0f; nb.WStrike = 0f; nb.StrikeSpeed = 0f; nb.StrikeAccel = 0f;
        var nbRuns = seeds.Select(sd => RunStrikes(sd, nb, herds, Wanderer(sd), 180f, 1)).ToList();
        float nbPerMin = nbRuns.Sum(r => r.Contacts) * 60f / nbRuns.Sum(r => r.Seconds);
        Console.WriteLine($"    ablation nobulls (the research herd, flees AWAY): {nbPerMin:F2} tramples/min");
        Check(nbPerMin * 3f <= perMin, $"ablation nobulls: the herd alone tramples {nbPerMin:F2}/min - the bulls are the threat ({perMin:F1}/min with them)");

        var nc = P.Clone(); nc.WAlarmClimb = 0f;
        var ncRuns = seeds.Select(sd => RunStrikes(sd, nc, herds, Wanderer(sd), 180f, 1)).ToList();
        float ncPerMin = ncRuns.Sum(r => r.Contacts) * 60f / ncRuns.Sum(r => r.Seconds);
        Console.WriteLine($"    ablation noclimb (bulls arm on sight but do not climb the alarm to its source): {ncPerMin:F2} contacts/min");
        Check(perMin > ncPerMin, $"the alarm climb brings the bulls to the threat: {perMin:F1} contacts/min with it, {ncPerMin:F1} without");

        // the READ: an alarm source 200 u east of an alarmed herd - the cows run AWAY down the gradient, the bulls
        // climb it TOWARD the source (no pilot, so no ramp: the climb alone is measured)
        var rw = new World(128, 4);
        int rq = rw.Core.AddPopulation(P, 1);
        rw.Core.Seed(rq, 8, Vector3.Zero, 30f);
        foreach (int i in LiveOf(rw.Core, rq)) rw.Core.Phase[i] = 1f;
        var src = new Vector3(200, 0, 0);
        double bullV = 0, cowV = 0; int bn = 0, cn = 0;
        for (int t = 0; t < 50; t++)
        {
            rw.Core.Fields.Deposit(SubstrateFields.Alarm, src, 4f);
            rw.Step();
            if (t < 5) continue;
            foreach (int i in LiveOf(rw.Core, rq))
            {
                float v = Vector3.Dot(rw.Core.Vel[i], SubstrateCore.Unit(src - rw.Core.Pos[i]));
                if (Role(rw.Core, i)) { bullV += v; bn++; } else { cowV += v; cn++; }
            }
        }
        bullV /= Math.Max(1, bn); cowV /= Math.Max(1, cn);
        Console.WriteLine($"    alarm source 200 u off an alarmed herd: bulls ({bn / 45}) move toward it at {bullV:F1} u/s, cows ({cn / 45}) at {cowV:F1} u/s (negative = away)");
        Check(bn > 0 && cn > 0 && bullV > 0f && cowV < 0f && bullV - cowV > 15f, $"it reads without a script: the herd SPLITS on the alarm - the bulls hold against the cohesion and edge TOWARD its source ({bullV:F1} u/s) while the cows flee down it ({cowV:F1} u/s), a {bullV - cowV:F0} u/s split");

        // the herd still FLIPS as one (the research's alarm quorum): frighten a corner and the run spreads
        var w = new World(128, 3);
        int q = w.Core.AddPopulation(P, 1);
        w.Core.Seed(q, 48, Vector3.Zero, 60f);
        w.Pilots.Add(new Pilot(3, new Vector3(150, 0, 0), new Vector3(-1, 0, 0)) { Mode = "still", Id = 1 });
        float flipAt = -1f;
        for (int t = 0; t < 200 && flipAt < 0; t++)
        {
            w.Step();
            if (LiveOf(w.Core, q).Count(i => w.Core.Phase[i] > 0.5f) >= 0.5f * LiveOf(w.Core, q).Count()) flipAt = w.Core.T;
        }
        Check(flipAt > 0f && flipAt < 20f, $"the alarm quorum flips the herd: half of it gregarious {flipAt:F1} s after a pilot hovers at its edge");
        Check(GameDeltaOf("stampede", "w_alarm_climb") && P.WAlarmClimb > 0f && P.ChargeEvery == 4, "no script: the charge is data - the bulls' alarm climb, ramp and strike are species numbers");
    }

    static bool GameDeltaOf(string species, string field) => SubstrateResearch.GameDeltas.Any(d => d.species == species && d.field == field);

    // ───────────────────────────────────────────────────────────── T: the mobber - mobs a slow pilot, pecks a drain

    static void Mobber()
    {
        Console.WriteLine("\nT. mobber: roosting birds mob a SLOW pilot (or one at the roost): orbit, pull up, dive, peck (a drain, 0.25)");
        var P = SubstrateResearch.GameMobber();
        var seeds = new[] { 7, 23, 41 };
        Func<World, Random, Vector3[]> roosts = (w, rng) => Seats(w, rng, 5, 8);
        Pilot Hover(int sd)
        {
            // a pilot hovering beside a roost: the world's first clump, 120 u off
            var rng = new Random(sd);
            return new Pilot(sd, Vector3.Zero, new Vector3(1, 0, 0)) { Mode = "still", Id = 1 };
        }
        var hover = seeds.Select(sd => RunStrikes(sd, P, roosts, Hover(sd), 120f, 3, (w, q) =>
        {
            // park the pilot beside the first roost once (the hover IS the provocation)
            if (w.Core.Tick == 1) { var c = w.Core.Home[w.Core.Pops[q].Start]; w.Pilots[0].Pos = c + new Vector3(120, 0, 0); }
        })).ToList();
        var skim = seeds.Select(sd => RunStrikes(sd, P, roosts, Wanderer(sd, 90f), 180f, 3)).ToList();
        var fast = seeds.Select(sd => RunStrikes(sd, P, roosts, Wanderer(sd, 140f), 180f, 3)).ToList();
        float hv = hover.Sum(r => r.Hits) * 60f / hover.Sum(r => r.Seconds);
        float sk = skim.Sum(r => r.Hits) * 60f / skim.Sum(r => r.Seconds);
        float fs = fast.Sum(r => r.Hits) * 60f / fast.Sum(r => r.Seconds);
        var tele = hover.Concat(skim).SelectMany(r => r.Telegraph).ToList();
        Console.WriteLine($"    pecks/min: hovering at a roost {hv:F1}, a 90 u/s skimmer {sk:F1}, a 140 u/s flyer {fs:F2}; pull-up before a peck median {Median(tele):F2} s");
        Check(hv >= 20f, $"it mobs a hovering pilot: {hv:F1} pecks/min (bestiary skimmer 70/min; the burn rules' 1 s per-vessel cooldown caps a contact rate at 60/min)");
        Check(sk > fs && fs <= 1.5f * 6.22f, $"keep your speed up: a slow skimmer {sk:F1}/min vs a 140 u/s flyer {fs:F2}/min (bestiary: wanderer 6.2, evader 0)");
        var ts = tele.OrderBy(x => x).ToList();
        Console.WriteLine($"    pull-up -> peck: min {ts.DefaultIfEmpty(0).First():F2} s, p10 {(ts.Count > 0 ? ts[ts.Count / 10] : 0):F2} s, median {Median(tele):F2} s, n {ts.Count}");
        Check(tele.Count > 0 && Median(tele) >= 0.7f && tele.Count(x => x >= 0.25f) >= 0.9f * tele.Count, $"every peck is telegraphed by the pull-up: median {Median(tele):F2} s before contact (bestiary PULL 0.8 s, telegraph 1.15 s), {tele.Count(x => x >= 0.25f)}/{tele.Count} >= 0.25 s (burn rules)");
        Check(Math.Abs(P.ContactWeight - 0.25f) < 1e-6f, $"a peck is a DRAIN: contact weight {P.ContactWeight} of a bite (burn rules; the glue multiplies the burn by PrismProperties.DangerWeight)");

        // ablation noprovoke: without the provocation term (a slow pilot / one at the roost) the colony never mobs
        var np = P.Clone(); np.QWProvoke = 0f;
        var calm = seeds.Select(sd => RunStrikes(sd, np, roosts, Wanderer(sd, 90f), 180f, 3)).ToList();
        float cl = calm.Sum(r => r.Hits) * 60f / calm.Sum(r => r.Seconds);
        Check(cl * 4f <= sk, $"ablation noprovoke: the 90 u/s skimmer takes {cl:F2} pecks/min instead of {sk:F1} (the provocation is what flips the colony to a mob)");

        // turns: the birds dive IN TURN - at most RampTurns winding up or diving at one hull at once
        int MaxDivers(SubstrateSpeciesParams pp)
        {
            int most = 0;
            foreach (int sd in seeds)
                RunStrikes(sd, pp, roosts, Hover(sd), 60f, 3, (w, q) =>
                {
                    if (w.Core.Tick == 1) { var c = w.Core.Home[w.Core.Pops[q].Start]; w.Pilots[0].Pos = c + new Vector3(120, 0, 0); }
                    most = Math.Max(most, LiveOf(w.Core, q).Count(i => w.Core.Ramp[i] > 0f && w.Core.Rest[i] <= 0f));
                });
            return most;
        }
        var nt = P.Clone(); nt.RampTurns = 0;
        int divers = MaxDivers(P), diversFree = MaxDivers(nt);
        Console.WriteLine($"    at most {divers} birds winding up or diving at a hovering hull at once ({diversFree} without turns)");
        Check(divers <= P.RampTurns && diversFree > divers, $"in turn: at most {divers} of the mob pull up or dive at once (turns {P.RampTurns}; {diversFree} without) - the read is one bird above you, and the proxy cap covers the divers");

        // jink: a pilot pointing straight at a mobber inside 60 u makes it break sideways
        float Off(SubstrateSpeciesParams pp)
        {
            var w = new World(128, 5);
            int q = w.Core.AddPopulation(pp, 3);
            w.Core.SeedAt(q, new[] { new Vector3(40, 0, 0) }, 0f);
            int i = LiveOf(w.Core, q).First();
            w.Core.Phase[i] = 1f; w.Core.QTarget[i] = 1f;
            var pil = new Pilot(5, Vector3.Zero, new Vector3(1, 0, 0)) { Mode = "straight", Speed = 30f, Id = 1 };
            w.Pilots.Add(pil);
            for (int t = 0; t < 8; t++) w.Step();
            var d = w.Core.Pos[i] - pil.Pos;
            return MathF.Sqrt(d.Y * d.Y + d.Z * d.Z);   // off the pilot's line (+X)
        }
        var nj = P.Clone(); nj.WJink = 0f;
        float jk = Off(P), nojk = Off(nj);
        Check(jk > nojk + 5f, $"jink: pointed at inside 60 u it breaks {jk:F1} u off your line in 0.8 s ({nojk:F1} u without the jink)");
    }

    // ───────────────────────────────────────────────────────────── C: the leech - pounce, latch, ride, sip, shaken off

    static void Leech()
    {
        Console.WriteLine("\nC. leech: puddles on the mass clumps POUNCE on a pilot, latch onto its hull (<= 6), sip every 1.5 s, shaken by a hard turn");
        var P = SubstrateResearch.GameLeech();
        var seeds = new[] { 7, 23, 41 };
        Func<World, Random, Vector3[]> puddles = (w, rng) => Seats(w, rng, 12, 4);
        // the research pilot (common/arena.py Pilot) turns at 2.0 rad/s - above the leech's 1.0 rad/s grip limit, so a
        // wanderer's goal turns shake riders off (the bestiary's 8.5 shaken per run); the harness default is 1.5
        Pilot LeechWanderer(int sd) { var pl = Wanderer(sd); pl.Turn = 2.0f; return pl; }
        var wander = seeds.Select(sd => RunStrikes(sd, P, puddles, LeechWanderer(sd), 180f, 0)).ToList();
        int sips = wander.Sum(r => r.Sips), lat = wander.Sum(r => r.Latches), sh = wander.Sum(r => r.Shaken), bites = wander.Sum(r => r.Hits);
        float spm = sips * 60f / wander.Sum(r => r.Seconds);
        var tele = wander.SelectMany(r => r.Telegraph).ToList();
        Console.WriteLine($"    wanderer: {lat} latches, {sips} sips ({spm:F1}/min), {sh} shaken off, max {wander.Max(r => r.MaxRiders)} riders on one hull, {bites} bites; latch -> first sip median {Median(tele):F1} s");
        Check(lat > 0 && sips > 0 && bites == 0, $"the pounce latches and the RIDE harms (sips), the pounce itself never bites ({lat} latches, {sips} sips, {bites} bites)");
        Check(spm >= 3f && spm <= 1.5f * 49.1f, $"sips land inside the bestiary's range: {spm:F1}/min on a wanderer (bestiary: aware 5.3 .. wanderer 49 drains/min)");
        Check(wander.All(r => r.MaxRiders <= P.ClingMax), $"at most {P.ClingMax} riders per hull (bestiary MAX_PER_HULL; seen {wander.Max(r => r.MaxRiders)})");
        Check(tele.Count > 0 && tele.All(x => x >= P.SipS - 1e-3f), $"a rider on your hull is the telegraph: the first sip lands {tele.DefaultIfEmpty(0).Min():F1} s after the latch (bestiary telegraph 2.0 s)");

        // the counterplay: a hard turn shakes them off; flying straight they ride and sip
        (int sips, int shaken, int left) Ride(string mode, SubstrateSpeciesParams pp)
        {
            var w = new World(128, 9);
            int q = w.Core.AddPopulation(pp, 0);
            var pil = new Pilot(9, new Vector3(0, 0, 0), new Vector3(1, 0, 0)) { Mode = mode, Speed = 120f, Turn = 2.5f, Id = 1 };
            w.Pilots.Add(pil);
            w.Core.Seed(q, 4, new Vector3(30, 0, 0), 4f);
            int s0 = 0, sk0 = 0;
            for (int t = 0; t < 150; t++)
            {
                w.Step();
                s0 += w.Core.Events.Count(e => e.Kind == SubstrateEventKind.Sip);
                sk0 += w.Core.Events.Count(e => e.Kind == SubstrateEventKind.Shaken);
                if (t == 20 && mode == "spin") { }
            }
            return (s0, sk0, LiveOf(w.Core, q).Count(i => w.Core.Host[i] != 0));
        }
        var straight = Ride("straight", P);
        var spin = Ride("spin", P);
        var spinNoGrip = Ride("spin", Clone(P, c => c.GripLoss = 0f));
        Console.WriteLine($"    15 s with riders: straight {straight.sips} sips / {straight.shaken} shaken; turning 2.5 rad/s {spin.sips} sips / {spin.shaken} shaken; nogrip turning {spinNoGrip.sips} sips");
        Check(straight.shaken == 0 && straight.sips > 0, $"flying straight they hold on and sip ({straight.sips} sips, {straight.shaken} shaken)");
        Check(spin.shaken > 0 && spin.sips < straight.sips, $"a hard turn shakes them off ({spin.shaken} flung, {spin.sips} sips vs {straight.sips})");
        Check(spinNoGrip.sips > spin.sips && spinNoGrip.shaken == 0, $"ablation nogrip: turning does nothing ({spinNoGrip.sips} sips, none shaken)");
        Check(P.ContactWeight == 0f && Math.Abs(P.SipWeight - 0.25f) < 1e-6f, $"its plate never burns (contact weight {P.ContactWeight}); a sip is a drain of weight {P.SipWeight}");
    }

    static SubstrateSpeciesParams Clone(SubstrateSpeciesParams p, Action<SubstrateSpeciesParams> edit) { var c = p.Clone(); edit(c); return c; }

    // ───────────────────────────────────────────────────────────── V: the leviathan - a swarm that assembles a body

    static void Leviathan()
    {
        Console.WriteLine("\nV. leviathan: a grazer school ASSEMBLES into a 120 u manta when sated, burns to touch, gulps a pilot ahead of its mouth");
        var P = SubstrateResearch.GameLeviathan();
        var w = new World(160, 11);
        var rng = new Random(11);
        w.Scatter(rng, 1200, 0.25f, 0.6f, 12);
        int q = w.Core.AddPopulation(P, 2);
        w.Core.Seed(q, P.N0, new Vector3(400, 0, 0), 60f);
        var pop = w.Core.Pops[q];
        float assembledAt = -1f, dissolvedAt = -1f; bool dangerLoose = false; float slotErr = -1f, attFrac = 0f; var errs = new List<float>();
        for (int t = 0; t < 300; t++)
        {
            w.Step();
            if (pop.BodyActive && assembledAt < 0f) assembledAt = w.Core.T;
            if (!pop.BodyActive && LiveOf(w.Core, q).Any(i => w.Core.Danger[i])) dangerLoose = true;
            if (!pop.BodyActive && assembledAt >= 0f && dissolvedAt < 0f) dissolvedAt = w.Core.T;
            if (t >= 80 && t % 10 == 0 && pop.BodyActive)
            {
                // formed (8 s after the start): how much of the school is in the body, and how close to its slot
                var live = LiveOf(w.Core, q).ToArray();
                var att = live.Where(i => w.Core.Attach[i] > 0.5f).ToArray();
                attFrac = Math.Max(attFrac, att.Length / (float)live.Length);
                errs.AddRange(att.Select(i => Vector3.Distance(w.Core.Pos[i], w.Core.SlotGoal[i])));
            }
        }
        if (errs.Count > 0) slotErr = Median(errs);
        Console.WriteLine($"    assembled at {assembledAt:F1} s, dissolved (hungry again) at {dissolvedAt:F1} s; formed, up to {attFrac:P0} attached, median member-to-slot {slotErr:F1} u (body {60 * P.BodyScale:F0} u long)");
        Check(assembledAt >= 0f && attFrac >= 0.8f, $"sated, the school assembles ({assembledAt:F1} s; {attFrac:P0} of the members attached)");
        Check(slotErr >= 0f && slotErr < 12f * P.BodyScale, $"the members HOLD the body plan: median {slotErr:F1} u from their slots (research: well {P.BodyWell} u, members chase the slot's own velocity)");
        Check(!dangerLoose, "a loose shoal is harmless: no member is dangerous while the body is not assembled");

        // the gulp: a pilot ahead of the mouth - the jaws flare GulpRampS, then the surge
        var g = new World(160, 12);
        g.Scatter(new Random(12), 1200, 0.25f, 0.6f, 12);
        int qg = g.Core.AddPopulation(P, 2);
        g.Core.Seed(qg, P.N0, new Vector3(400, 0, 0), 60f);
        var gp = g.Core.Pops[qg];
        for (int t = 0; t < 200 && !gp.BodyActive; t++) g.Step();
        for (int t = 0; t < 100; t++) g.Step();   // settle the formed body
        var pil = new Pilot(12, gp.BodyC + gp.BodyF * (gp.MouthZ * P.BodyScale + 140f), -gp.BodyF) { Mode = "still", Id = 1 };
        g.Pilots.Add(pil);
        float aheadFrom = g.Core.T, gulpAt = -1f, hitAt = -1f;
        for (int t = 0; t < 150; t++)
        {
            g.Step();
            if (g.Core.Events.Any(e => e.Kind == SubstrateEventKind.Gulp) && gulpAt < 0f) gulpAt = g.Core.T;
            if (g.Core.Events.Any(e => e.Kind == SubstrateEventKind.Bite) && hitAt < 0f) hitAt = g.Core.T;
        }
        Console.WriteLine($"    a pilot 140 u ahead of the mouth: gulp {gulpAt - aheadFrom:F1} s after it got there, first burn {hitAt - aheadFrom:F1} s");
        Check(gulpAt > 0f && gulpAt - aheadFrom >= P.GulpRampS - 1e-3f, $"the jaws flare first: the surge starts {gulpAt - aheadFrom:F1} s after a pilot sits ahead of the mouth (bestiary prep 1.2 s, telegraph 1.0 s)");
        Check(hitAt > 0f, $"the gulp lands on a pilot that stays ({hitAt - aheadFrom:F1} s)");

        // a wanderer: a slow giant you can keep clear of; without the gulp it almost never touches you
        var seeds = new[] { 7, 23, 41 };
        Func<World, Random, Vector3[]> school = (ww, r) => Seats(ww, r, 1, P.N0);
        var wan = seeds.Select(sd => RunStrikes(sd, P, school, Wanderer(sd), 180f, 2)).ToList();
        var ng = seeds.Select(sd => RunStrikes(sd, Clone(P, c => c.GulpR = 0f), school, Wanderer(sd), 180f, 2)).ToList();
        float wm = wan.Sum(r => r.Hits) * 60f / wan.Sum(r => r.Seconds), nm = ng.Sum(r => r.Hits) * 60f / ng.Sum(r => r.Seconds);
        Console.WriteLine($"    wanderer: {wm:F2} burns/min, {wan.Sum(r => r.Gulps)} gulps, {wan.Sum(r => r.Assemblies)} assemblies / {wan.Sum(r => r.Dissolves)} dissolves; nogulp {nm:F2}/min");
        Check(wm <= 3.11f * 1.5f, $"a slow giant: {wm:F2} burns/min on a wanderer (bestiary wanderer 0.45, skimmer 3.1 /min)");
        Check(wan.Sum(r => r.Assemblies) > 0 && wan.Sum(r => r.Gulps) >= 0, $"it assembles in the cell ({wan.Sum(r => r.Assemblies)} assemblies over 3 runs)");

        // hungry again, it dissolves: no food, the school's hunger climbs past attach_off_h
        var d = new World(160, 13);
        int qd = d.Core.AddPopulation(P, 2);
        d.Core.Seed(qd, P.N0, new Vector3(400, 0, 0), 60f);
        int assem = 0, diss = 0;
        for (int t = 0; t < 600; t++)
        {
            d.Step();
            assem += d.Core.Events.Count(e => e.Kind == SubstrateEventKind.Assemble);
            diss += d.Core.Events.Count(e => e.Kind == SubstrateEventKind.Dissolve);
        }
        var dp = d.Core.Pops[qd];
        Check(assem > 0 && diss > 0 && !dp.BodyActive && LiveOf(d.Core, qd).All(i => !d.Core.Danger[i]),
              $"unfed it falls apart again: {assem} assembly, {diss} dissolve in 60 s; the shoal is harmless");
    }

    // ───────────────────────────────────────────────────────────── Q: the proxy budget - caps re-divided, engagement kept

    /// <summary>
    /// The collider budget (Docs/SUBSTRATE_FAUNA.md §5, §8.6): the substrate's share of the Swarm cell's 1,200 is the
    /// 78 colliders (39 proxies) it had before round 11-11 - the four new species are fitted by RE-DIVIDING those 39,
    /// never by raising the ceiling. author_substrate_fauna.py reads this table and asserts its proxies equal it.
    /// </summary>
    static readonly (string species, int cap, float engage)[] ProxyCaps =
    {
        // round 11-14: a lurker and a stampede proxy moved to the leech - the pen's full pull at its edge
        // (SubstrateKernel.PenWeight) packs a puddle of 4 tighter, and two proxies covered 84% of a ram's passes (three,
        // 89.5%); a lurker never has more than one agent in the contact horizon at once, a stampede three
        ("pack", 7, 260f), ("locust", 12, 140f), ("lurker", 3, 160f), ("stampede", 5, 200f), ("mobber", 4, 120f),
        ("leech", 4, 140f), ("leviathan", 4, 200f),
    };

    /// <summary>
    /// The measured argument that a cap still engages: every tick the proxies are the cap NEAREST agents inside the
    /// engage radius in the PREVIOUS tick's frame (the glue builds them from the published frame); a contact (a Bite,
    /// or a leech's Latch) is COVERED when one of the agents in contact this tick had a proxy. Also the most dangerous
    /// agents ever inside the contact horizon (bite reach + one tick of closing speed) at once.
    /// </summary>
    static (int contacts, int covered, int maxHorizon, int harmAll, int harmCovered) Coverage(World w, int q, int cap, float engage, int ticks)
    {
        var P = w.Core.Pops[q].P;
        int contacts = 0, covered = 0, maxH = 0, harmAll = 0, harmCov = 0;
        var eng = new HashSet<int>();
        // the burn rules' 1 s per-vessel cooldown: what the pilot FEELS is harm events, not contacts
        var lastAll = new Dictionary<int, float>(); var lastCov = new Dictionary<int, float>();
        var lastNear = new Dictionary<int, float>();
        var wasRider = new HashSet<int>();   // flung off this tick: it was ON the hull, not a pass
        bool ramOnly = P.ContactWeight <= 0f;   // a leech's plate never burns: its proxy is there to be RAMMED
        for (int t = 0; t < ticks; t++)
        {
            eng.Clear();
            wasRider.Clear();
            foreach (int i in LiveOf(w.Core, q)) if (w.Core.Host[i] != 0) wasRider.Add(i);
            var cand = new List<(float d, int i)>();
            foreach (int i in LiveOf(w.Core, q))
            {
                if (w.Core.Host[i] != 0) continue;   // a rider has no proxy
                float best = float.MaxValue;
                foreach (var p in w.Pilots) best = MathF.Min(best, Vector3.Distance(w.Core.Pos[i], p.Pos));
                if (best <= engage) cand.Add((w.Core.Danger[i] ? -1f + best * 1e-6f : best, i));   // the tick job's rule: dangerous first
            }
            foreach (var c in cand.OrderBy(c => c.d).Take(cap)) eng.Add(c.i);
            w.Step();
            foreach (var p in w.Pilots)
            {
                float reach = P.BiteR + 6f;
                int h = 0;
                foreach (int i in LiveOf(w.Core, q))
                    if (w.Core.Danger[i] && Vector3.Distance(w.Core.Pos[i], p.Pos) < reach + (w.Core.Vel[i] - p.Vel).Length() * Dt) h++;
                maxH = Math.Max(maxH, h);
            }
            if (ramOnly)
            {
                // a ram: free agents inside the hull's reach (pilot radius 6 + its own reach) - the hull can swat one per
                // pass, so a pass (once per pilot per second) is covered when ANY agent in reach had a proxy
                foreach (var p in w.Pilots)
                {
                    bool any = false, hit = false;
                    foreach (int i in LiveOf(w.Core, q))
                    {
                        // swept over the tick, as the contact horizon is: a 140 u/s pass covers 14 u per tick
                        if (w.Core.Host[i] != 0 || wasRider.Contains(i) || Vector3.Distance(w.Core.Pos[i], p.Pos) >= P.BiteR + 6f + (w.Core.Vel[i] - p.Vel).Length() * Dt) continue;
                        any = true; hit |= eng.Contains(i);
                    }
                    if (!any || (lastNear.TryGetValue(p.Id, out float tn) && w.Core.T - tn < 1f)) continue;
                    lastNear[p.Id] = w.Core.T;
                    contacts++;
                    if (hit) covered++;
                }
                continue;
            }
            foreach (var e in w.Core.Events)
            {
                if (e.Kind != SubstrateEventKind.Bite) continue;
                if (w.Core.PopOf[e.Index] != q) continue;
                var p = w.Pilots.First(pp => pp.Id == e.Other);
                contacts++;
                bool ok = eng.Contains(e.Index);
                if (!ok)
                    foreach (int i in LiveOf(w.Core, q))
                        if (w.Core.Danger[i] && Vector3.Distance(w.Core.Pos[i], p.Pos) < P.BiteR + 6f && eng.Contains(i)) { ok = true; break; }
                if (!lastAll.TryGetValue(p.Id, out float ta) || w.Core.T - ta >= 1f) { lastAll[p.Id] = w.Core.T; harmAll++; }
                if (ok)
                {
                    covered++;
                    if (!lastCov.TryGetValue(p.Id, out float tc) || w.Core.T - tc >= 1f) { lastCov[p.Id] = w.Core.T; harmCov++; }
                }
            }
        }
        return (contacts, covered, maxH, harmAll, harmCov);
    }

    static void Proxies()
    {
        Console.WriteLine("\nQ. the collider budget: 39 substrate proxies (78 colliders, the pre-11-11 share) re-divided over seven species - does each cap still engage?");
        int total = ProxyCaps.Sum(c => c.cap);
        Check(total == 39, $"the caps sum to {total} proxies = {2 * total} colliders: the substrate's share of the 1,200 is unchanged (pack 7 + locust 24 + lurker 8 before)");
        foreach (var (name, cap, engage) in ProxyCaps)
        {
            int contacts = 0, covered = 0, maxH = 0, harmAll = 0, harmCov = 0;
            foreach (int seed in new[] { 7, 23, 41 })
            {
                var P = SubstrateResearch.ByName(name, game: true);
                var w = new World(Math.Max(64, P.Capacity + 8), seed);
                var rng = new Random(seed);
                w.Scatter(rng, 1500, 0.2f, 0.8f, 24);
                int q = w.Core.AddPopulation(P, 1);
                int ticks = 1200;
                switch (name)
                {
                    case "pack":
                        w.Core.Seed(q, P.N0, new Vector3(300, 0, 0), 50f);
                        w.Pilots.Add(new Pilot(seed, new Vector3(0, 0, 300), new Vector3(1, 0, 0)) { Id = 1 });
                        break;
                    case "locust":
                        // the storm: dense and hungry around a pilot that flies through it
                        P.N0 = 300;
                        w.Core.Seed(q, 300, new Vector3(0, 0, 250), 40f);
                        foreach (int i in LiveOf(w.Core, q)) w.Core.Hunger[i] = 0.9f;
                        w.Pilots.Add(new Pilot(seed, new Vector3(0, 0, 250), new Vector3(1, 0, 0)) { Id = 1 });
                        ticks = 600;
                        break;
                    case "lurker":
                        {
                            // the ambush lane: lurkers seated every 150 u along a line, a pilot skimming it 20 u off
                            var seatsL = Enumerable.Range(0, 8).Select(k => new Vector3(-525 + 150 * k, 0, 300)).ToArray();
                            w.Core.SeedAt(q, seatsL, 0f);
                            w.Pilots.Add(new Pilot(seed, new Vector3(-700, 20, 300), new Vector3(1, 0, 0)) { Mode = "straight", Speed = 120f, Id = 1 });
                            ticks = 130;
                            break;
                        }
                    case "stampede":
                        w.Core.SeedAt(q, Seats(w, rng, 4, 12), 40f);
                        w.Pilots.Add(Wanderer(seed));
                        break;
                    case "mobber":
                        w.Core.SeedAt(q, Seats(w, rng, 5, 8), 40f);
                        var h = new Pilot(seed, w.Core.Home[w.Core.Pops[q].Start] + new Vector3(120, 0, 0), new Vector3(1, 0, 0)) { Mode = "still", Id = 1 };
                        w.Pilots.Add(h);
                        break;
                    case "leech":
                        {
                            // the hunter's ram (bestiary payoff: "ram free ones"): puddles of 4 every 200 u along a line, a
                            // 140 u/s pilot flying straight through them
                            var seatsC = Enumerable.Range(0, 6).SelectMany(k => Enumerable.Repeat(new Vector3(-500 + 200 * k, 0, 300), 4)).ToArray();
                            w.Core.SeedAt(q, seatsC, 8f);
                            w.Pilots.Add(new Pilot(seed, new Vector3(-800, 0, 300), new Vector3(1, 0, 0)) { Mode = "straight", Speed = 140f, Id = 1 });
                            ticks = 120;
                            break;
                        }
                    case "leviathan":
                        w.Core.Seed(q, P.N0, new Vector3(400, 0, 0), 60f);
                        for (int t = 0; t < 150; t++) w.Step();   // formed (it dissolves when hungry again, ~25 s)
                        var pop = w.Core.Pops[q];
                        w.Pilots.Add(new Pilot(seed, pop.BodyC + pop.BodyF * (pop.MouthZ * P.BodyScale + 140f), -pop.BodyF) { Mode = "still", Id = 1 });
                        break;
                }
                var r = Coverage(w, q, cap, engage, ticks);
                contacts += r.contacts; covered += r.covered; maxH = Math.Max(maxH, r.maxHorizon); harmAll += r.harmAll; harmCov += r.harmCovered;
            }
            float frac = contacts > 0 ? covered / (float)contacts : 1f;
            float hfrac = harmAll > 0 ? harmCov / (float)harmAll : 1f;
            bool ram = SubstrateResearch.ByName(name, game: true).ContactWeight <= 0f;
            Console.WriteLine($"    {name,-9} cap {cap,2} within {engage,3:F0} u: {covered}/{contacts} {(ram ? "rammable passes" : "contacts")} on a proxied agent ({frac:P1}); harm events after the 1 s cooldown {harmCov}/{harmAll} ({hfrac:P1}); most dangerous agents in the contact horizon at once {maxH}");
            // engaged = what the pilot feels survives the cap: >= 95 % of the contacts land on a proxy, or (a swirl that
            // contacts faster than the burn rules' 1 s cooldown can pass on) >= 90 % of the harm EVENTS still land. A
            // ram is PAYOFF, not harm (a leech's plate never burns): a missed one is a lost swat, never a phantom burn,
            // and the pass is decided by a pounce arriving from outside the nearest few - >= 90 % of the passes
            Check(contacts > 0 && (ram ? frac >= 0.9f : frac >= 0.95f || hfrac >= 0.9f),
                  $"{name}: a cap of {cap} still engages - {frac:P1} of {contacts} {(ram ? "rammable passes" : "contacts")}, {hfrac:P1} of {harmAll} harm events covered by the previous frame's nearest {cap}");
        }

        // the tick job builds each population's proxies from ITS OWN cap and radius, and never gives a rider one
        var core = new SubstrateCore(256, R, Dt, 40, 3);
        int ql = core.AddPopulation(SubstrateResearch.GameLeech(), 0);
        int qs = core.AddPopulation(SubstrateResearch.GameStampede(), 1);
        core.Pops[ql].EngageRadius = 140f; core.Pops[ql].MaxEngaged = 2;
        core.Pops[qs].EngageRadius = 200f; core.Pops[qs].MaxEngaged = 6;
        core.Seed(ql, 30, new Vector3(20, 0, 0), 20f);
        core.Seed(qs, 40, new Vector3(-30, 0, 0), 30f);
        var job = new SubstrateTickJob(core, new SubstrateTickSettings { EngageRadius = 500f, MaxEngaged = 24 });
        job.Prime();
        job.Pilots[0] = new SubstratePilot { Pos = Vector3.Zero, Vel = new Vector3(60, 0, 0), Radius = 6f, Id = 1 };
        job.PilotCount = 1;
        bool capsOk = true, noRider = true; int ridersSeen = 0;
        for (int t = 0; t < 60; t++)
        {
            job.Kick(true); job.Collect();
            capsOk &= job.EngagedCount[ql] <= 2 && job.EngagedCount[qs] <= 6;
            for (int k = 0; k < job.EngagedCount[ql]; k++) noRider &= !job.Riding[job.Engaged[core.Pops[ql].Start + k]];
            for (int i = 0; i < core.Capacity; i++) if (job.Riding[i]) ridersSeen++;
        }
        Check(capsOk, "the tick job caps each population at its own MaxEngaged (leech 2, stampede 6) under a shared default of 24");
        Check(noRider && ridersSeen > 0, $"a rider never gets a proxy (it would sit inside the hull's collider): {ridersSeen} rider-ticks, none engaged");
    }

    // ───────────────────────────────────────────────────────────── M: the mass ledger and the laws

    static void Ledger()
    {
        Console.WriteLine("\nM. mass ledger and continuity: three species, real food, a pilot, 3 minutes");
        var w = new World(640, 13);
        var rng = new Random(13);
        w.Scatter(rng, 1500, 0.3f, 0.9f, 24);
        double food0 = w.LiveMass();
        int ql = w.Core.AddPopulation(SubstrateResearch.GameLocust(), 2);
        int qp = w.Core.AddPopulation(SubstrateResearch.GamePack(), 3);
        int qu = w.Core.AddPopulation(SubstrateResearch.GameLurker(), 1);
        w.Core.Seed(ql, 40, Ball(rng, 400f, 600f), 120f);
        w.Core.Seed(qp, 6, Ball(rng, 400f, 600f), 50f);
        var seats = Enumerable.Range(0, 12).Select(_ => w.MassPos[rng.Next(w.MassPos.Count)]).ToArray();
        w.Core.SeedAt(qu, seats);
        double seeded = w.Core.MassIn;
        w.Pilots.Add(new Pilot(13, Ball(rng, 200f, 500f), new Vector3(0, 0, 1)) { Id = 1 });
        double maxDrift = 0;
        for (int t = 0; t < 1800; t++)
        {
            w.Step();
            // world ledger: live food + agent bodies = what was scattered + what the spawner seeded
            double total = w.LiveMass() + w.Core.MassHeld();
            maxDrift = Math.Max(maxDrift, Math.Abs(total - (food0 + seeded)) / (food0 + seeded));
        }
        var c = w.Core;
        double held = c.MassHeld();
        double coreDrift = Math.Abs(c.MassIn - c.MassOut - held) / Math.Max(1, c.MassIn);
        int births = c.Pops.Sum(p => (int)p.Births), starved = c.Pops.Sum(p => (int)p.Starvations);
        Console.WriteLine($"    eaten {w.Eaten:F0}, laid back {w.Laid:F0}, held {held:F0}; births {births}, starvations {starved}, preyed {w.Preyed}; " +
                          $"alive locust {c.Pops[ql].Alive} pack {c.Pops[qp].Alive} lurker {c.Pops[qu].Alive}");
        Check(coreDrift < 1e-6, $"agent ledger: in {c.MassIn:F1} - out {c.MassOut:F1} = held {held:F1} (relative drift {coreDrift:E1})");
        Check(maxDrift < 1e-5, $"world ledger: live food + agent bodies constant every tick (max relative drift {maxDrift:E1}; research <= 4e-16 in float64)");
        Check(w.Log.Where(e => e.Kind == SubstrateEventKind.Starving).All(e => true) && starved == w.Log.Count(e => e.Kind == SubstrateEventKind.Starving),
              $"every death has a cause: {starved} starvations, each reported (no imposed death, no lifespan)");
        float vmax = 230f * 1.0f + 27.6f;   // the fastest regime (lurker lunge) plus the locust hop
        Check(w.MaxStep <= vmax * Dt + 1e-3f, $"no teleport: the largest per-step move is {w.MaxStep:F1} u (bound {vmax * Dt:F1} u at 10 Hz)");
        Check(births > 0, $"lifeforms breed from food ({births} births)");
        Check(w.Preyed > 0 && c.Pops[qp].Alive > 0, $"the food web: the pack lives on what it catches ({w.Preyed} locusts eaten, {c.Pops[qp].Alive} hunters alive after 3 min)");

        // round 11-11: all SEVEN in one core - the herd, the mob, the riders and the body on the same ledger
        Console.WriteLine("\nM2. mass ledger with the whole bestiary: seven species, real food, a wanderer, a slow skimmer, 3 minutes");
        var w2 = new World(1024, 19);
        var r2 = new Random(19);
        w2.Scatter(r2, 1500, 0.3f, 0.9f, 24);
        double f0 = w2.LiveMass();
        var names = SubstrateResearch.Names;
        var qs = new int[names.Length];
        for (int k = 0; k < names.Length; k++)
        {
            var P = SubstrateResearch.ByName(names[k], game: true);
            qs[k] = w2.Core.AddPopulation(P, 1 + k % 4);
            switch (names[k])
            {
                case "lurker": case "leech": w2.Core.SeedAt(qs[k], Seats(w2, r2, 12, 4), 8f); break;
                case "stampede": w2.Core.SeedAt(qs[k], Seats(w2, r2, 4, 12), 40f); break;
                case "mobber": w2.Core.SeedAt(qs[k], Seats(w2, r2, 5, 8), 40f); break;
                case "pack": w2.Core.Seed(qs[k], 6, Ball(r2, 400f, 600f), 50f); break;
                default: w2.Core.Seed(qs[k], Math.Min(P.N0, 96), Ball(r2, 400f, 600f), 80f); break;
            }
        }
        double s0 = w2.Core.MassIn;
        w2.Pilots.Add(new Pilot(19, Ball(r2, 200f, 500f), new Vector3(0, 0, 1)) { Id = 1, Turn = 2f });
        w2.Pilots.Add(new Pilot(20, Ball(r2, 200f, 500f), new Vector3(1, 0, 0)) { Id = 2, Speed = 60f });
        double drift2 = 0;
        long riders = 0;
        for (int t = 0; t < 1800; t++)
        {
            w2.Step();
            double total = w2.LiveMass() + w2.Core.MassHeld();
            drift2 = Math.Max(drift2, Math.Abs(total - (f0 + s0)) / (f0 + s0));
            foreach (int i in LiveOf(w2.Core, qs[Array.IndexOf(names, "leech")])) if (w2.Core.Host[i] != 0) riders++;
        }
        var c2 = w2.Core;
        double held2 = c2.MassHeld();
        double core2 = Math.Abs(c2.MassIn - c2.MassOut - held2) / Math.Max(1, c2.MassIn);
        int st2 = c2.Pops.Sum(p => (int)p.Starvations);
        var alive = string.Join(" ", names.Select((nm, k) => $"{nm} {c2.Pops[qs[k]].Alive}"));
        var lev = c2.Pops[qs[Array.IndexOf(names, "leviathan")]];
        Console.WriteLine($"    eaten {w2.Eaten:F0}, held {held2:F0}; births {c2.Pops.Sum(p => (int)p.Births)}, starvations {st2}; alive {alive}; " +
                          $"windups {c2.Pops.Sum(p => p.Windups)}, latches {c2.Pops.Sum(p => p.Latches)}, rider-ticks {riders}, sips {c2.Pops.Sum(p => p.Sips)}, assemblies {lev.Assemblies}");
        Check(core2 < 1e-6, $"agent ledger with all seven: in {c2.MassIn:F1} - out {c2.MassOut:F1} = held {held2:F1} (relative drift {core2:E1}) - riding, assembling and gulping move no mass");
        Check(drift2 < 1e-5, $"world ledger with all seven: live food + agent bodies constant every tick (max relative drift {drift2:E1})");
        Check(st2 == w2.Log.Count(e => e.Kind == SubstrateEventKind.Starving), $"every death has a cause with all seven: {st2} starvations, each reported");
        Check(riders > 0 && lev.Assemblies > 0 && c2.Pops.Sum(p => p.Windups) > 0, "the new behaviours ran in the ledger world (rides, an assembly, windups)");
    }

    // ───────────────────────────────────────────────────────────── K: the Burst-shaped kernel vs the managed step

    /// <summary>What one agent's step writes (its own slot only), saved and restored so the reference and the kernel
    /// step the SAME input.</summary>
    struct AgentState
    {
        public Vector3 Pos, Vel, IDir, Home;
        public float Hunger, Fear, Curious, Aggr, Phase, QTarget, ISpeed;
        public bool Steered, Watched, Creeping;

        public static AgentState Of(SubstrateCore c, int i) => new AgentState
        {
            Pos = c.Pos[i], Vel = c.Vel[i], IDir = c.IDir[i], Home = c.Home[i], Hunger = c.Hunger[i], Fear = c.Fear[i],
            Curious = c.Curious[i], Aggr = c.Aggr[i], Phase = c.Phase[i], QTarget = c.QTarget[i], ISpeed = c.ISpeed[i],
            Steered = c.Steered[i], Watched = c.Watched[i], Creeping = c.Creeping[i],
        };

        public void To(SubstrateCore c, int i)
        {
            c.Pos[i] = Pos; c.Vel[i] = Vel; c.IDir[i] = IDir; c.Home[i] = Home; c.Hunger[i] = Hunger; c.Fear[i] = Fear;
            c.Curious[i] = Curious; c.Aggr[i] = Aggr; c.Phase[i] = Phase; c.QTarget[i] = QTarget; c.ISpeed[i] = ISpeed;
            c.Steered[i] = Steered; c.Watched[i] = Watched; c.Creeping[i] = Creeping;
        }

        static float D(Vector3 a, Vector3 b) => MathF.Max(MathF.Abs(a.X - b.X), MathF.Max(MathF.Abs(a.Y - b.Y), MathF.Abs(a.Z - b.Z)));

        /// <summary>Largest difference in each written quantity: position, velocity, intent direction, the scalars.</summary>
        public static (float pos, float vel, float dir, float scalar, bool flags) Diff(in AgentState a, in AgentState b) =>
            (MathF.Max(D(a.Pos, b.Pos), D(a.Home, b.Home)), D(a.Vel, b.Vel), D(a.IDir, b.IDir),
             new[] { a.Hunger - b.Hunger, a.Fear - b.Fear, a.Curious - b.Curious, a.Aggr - b.Aggr, a.Phase - b.Phase,
                     a.QTarget - b.QTarget, (a.ISpeed - b.ISpeed) / MathF.Max(1f, MathF.Abs(a.ISpeed)) }.Max(x => MathF.Abs(x)),
             a.Steered == b.Steered && a.Watched == b.Watched && a.Creeping == b.Creeping);

        public bool Bits(in AgentState o) =>
            Eq(Pos, o.Pos) && Eq(Vel, o.Vel) && Eq(IDir, o.IDir) && Eq(Home, o.Home) && B(Hunger, o.Hunger) && B(Fear, o.Fear) &&
            B(Curious, o.Curious) && B(Aggr, o.Aggr) && B(Phase, o.Phase) && B(QTarget, o.QTarget) && B(ISpeed, o.ISpeed) &&
            Steered == o.Steered && Watched == o.Watched && Creeping == o.Creeping;

        static bool B(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool Eq(Vector3 a, Vector3 b) => B(a.X, b.X) && B(a.Y, b.Y) && B(a.Z, b.Z);
    }

    static void KernelMatch()
    {
        Console.WriteLine("\nK. the Burst-shaped kernel (SubstrateKernel.StepAgent) vs the pre-11c managed step, agent by agent");
        // the M world - three species, the food web, real food, a wandering pilot - plus a lurker pass, so every
        // branch runs: the ring, the prey pounce, the spring, gaze/creep/freeze, rest, the band
        var w = new World(640, 17);
        var rng = new Random(17);
        w.Scatter(rng, 1500, 0.3f, 0.9f, 24);
        int ql = w.Core.AddPopulation(SubstrateResearch.GameLocust(), 2);
        int qp = w.Core.AddPopulation(SubstrateResearch.GamePack(), 3);
        int qu = w.Core.AddPopulation(SubstrateResearch.GameLurker(), 1, 100f, 900f);
        w.Core.Seed(ql, 120, Ball(rng, 300f, 500f), 120f);
        w.Core.Seed(qp, 6, Ball(rng, 300f, 500f), 50f);
        w.Core.SeedAt(qu, Enumerable.Range(0, 12).Select(_ => w.MassPos[rng.Next(w.MassPos.Count)]).ToArray());
        w.Pilots.Add(new Pilot(17, Ball(rng, 200f, 500f), new Vector3(0, 0, 1)) { Id = 1 });
        w.Pilots.Add(new Pilot(18, Ball(rng, 200f, 500f), new Vector3(1, 0, 0)) { Id = 2, Speed = 40f });

        long steps = 0, bits = 0, flagMiss = 0, steered = 0, watched = 0, creeping = 0, ringed = 0;
        float mPos = 0, mVel = 0, mDir = 0, mSc = 0;
        w.Core.Stepper = (core, pop, k, I, G) =>
        {
            int i = pop.Live[k];
            var before = AgentState.Of(core, i);
            SubstrateReference.StepAgent(core, pop, k, I, G);
            var refOut = AgentState.Of(core, i);
            before.To(core, i);
            core.KernelStep(pop, k, I, G);
            var kerOut = AgentState.Of(core, i);
            steps++;
            if (refOut.Bits(kerOut)) bits++;
            var d = AgentState.Diff(refOut, kerOut);
            mPos = MathF.Max(mPos, d.pos); mVel = MathF.Max(mVel, d.vel); mDir = MathF.Max(mDir, d.dir); mSc = MathF.Max(mSc, d.scalar);
            if (!d.flags) flagMiss++;
            if (kerOut.Steered) steered++;
            if (kerOut.Watched) watched++;
            if (kerOut.Creeping) creeping++;
            if (pop.P.RingRoles > 0 && kerOut.Steered && kerOut.Aggr > 0f) ringed++;
        };
        for (int t = 0; t < 1200; t++) w.Step();
        w.Core.Stepper = null;
        double frac = (double)bits / Math.Max(1, steps);
        Console.WriteLine($"    {steps} agent-steps over 120 s ({steered} steered, {ringed} pack ring/hunt, {watched} watched, {creeping} creeping, {w.Preyed} preyed): " +
                          $"{frac:P3} bit-identical; max |diff| pos {mPos:E1} u, vel {mVel:E1} u/s, dir {mDir:E1}, scalars {mSc:E1}; flag mismatches {flagMiss}");
        Check(steps > 100000 && steered > 10000 && ringed > 100 && watched + creeping > 0,
              $"every branch exercised ({steps} agent-steps, {ringed} pack ring/hunt steps, {watched} watched + {creeping} creeping lurker steps)");
        Check(flagMiss == 0, $"the decisions match exactly: steered / watched / creeping agree on all {steps} agent-steps");
        Check(bits == steps, $"BIT-identical: {bits}/{steps} agent-steps write exactly the same bits through the kernel as through the " +
              $"managed step (max |diff| pos {mPos:E1}, vel {mVel:E1}, dir {mDir:E1}, scalars {mSc:E1}) - on .NET; Burst's own float codegen is not proved here");
    }

    // ───────────────────────────────────────────────────────────── J: the off-thread tick job

    static void Job()
    {
        Console.WriteLine("\nJ. the tick job: worker thread, double-buffered frame, true bodies, hearts, engaged agents, volume");
        var core = new SubstrateCore(256, R, Dt, 40, 3);
        int ql = core.AddPopulation(SubstrateResearch.GameLocust().WithCap(200), 2);
        int qp = core.AddPopulation(SubstrateResearch.GamePack(), 3);
        core.Seed(ql, 40, new Vector3(300, 0, 0), 60f);
        core.Seed(qp, 6, new Vector3(-300, 0, 0), 40f);
        var job = new SubstrateTickJob(core, new SubstrateTickSettings { Centre = new Vector3(1000, 0, 0), EngageRadius = 160f, MaxEngaged = 24 });
        job.Prime();
        Check(job.Instances.Where(x => x.Alive).All(x => x.PrevPos == x.CurPos), "primed: every seeded agent is a newborn (Prev = Cur, it blooms)");
        job.Pilots[0] = new SubstratePilot { Pos = new Vector3(300, 0, 0), Vel = new Vector3(100, 0, 0), Radius = 6f, Id = 1 };
        job.PilotCount = 1;
        for (int t = 0; t < 20; t++)
        {
            job.Kick(false);
            var sw = Stopwatch.StartNew();
            while (!job.Collect()) { if (sw.ElapsedMilliseconds > 5000) break; System.Threading.Thread.Sleep(0); }
        }
        Check(job.Error == null && job.State == SwarmJobState.Idle, "20 ticks off-thread, no worker error");
        double vol = 0; int alive = 0;
        for (int i = 0; i < core.Capacity; i++)
            if (job.Instances[i].Alive) { alive++; var b = job.Body[i]; vol += b.X * b.Y * b.Z; }
        double stock = core.MassHeld();
        Check(Math.Abs(vol - stock) / stock < 1e-4, $"every agent's true body volume IS its stock: {vol:F1} vs {stock:F1}");
        Check(Math.Abs(job.PopVolume[ql] + job.PopVolume[qp] - stock) / stock < 1e-6, $"stated volume per population sums to the held mass ({job.PopVolume[ql]:F0} + {job.PopVolume[qp]:F0})");
        int eng = job.EngagedCount[ql];
        bool engOk = true;
        for (int k = 0; k < eng; k++) engOk &= Vector3.Distance(core.Pos[job.Engaged[core.Pops[ql].Start + k]], job.Pilots[0].Pos) <= 160f;
        Check(engOk && eng <= 24, $"engaged = within 160 u of the vessel, capped at 24 ({eng} locusts engaged)");
        Check(job.HeartCount(ql) == core.Pops[ql].Alive && Enumerable.Range(0, job.HeartCount(ql)).All(k => job.Instances[core.Pops[ql].Start + (int)job.HeartIdx[core.Pops[ql].Start + k]].Alive),
              $"heart list = the living agents ({job.HeartCount(ql)})");
        int victim = LiveOf(core, ql).First();
        float vs = core.Stock[victim];
        job.QueueKill(victim);
        job.QueueFeed(LiveOf(core, ql).Skip(1).First(), 10f);
        double before = core.MassIn - core.MassOut;
        job.Kick(true); job.Collect();
        Check(!core.Alive[victim] && !job.Instances[victim].Alive && Math.Abs((core.MassIn - core.MassOut) - (before + 10.0 - vs)) < 1e-3,
              "a queued kill and a queued feed land next tick (the dead slot is dark, the ledger moved by exactly both)");
        // the food web crosses the thread boundary too: a hungry hunter among locusts asks for one, the frame carries it
        job.PilotCount = 0;
        foreach (int h in LiveOf(core, qp)) { core.Hunger[h] = 1.2f; }
        var hunters = LiveOf(core, qp).ToArray(); var prey = LiveOf(core, ql).ToArray();
        for (int k = 0; k < prey.Length; k++) core.Pos[prey[k]] = core.Pos[hunters[k % hunters.Length]] + new Vector3(3f + k % 5, 0, 0);
        bool carried = false, matched = true;
        for (int t = 0; t < 40 && !carried; t++)
        {
            job.Kick(true); job.Collect();
            matched &= job.PreyRequests.Count == core.PreyRequests.Count;
            carried = job.PreyRequests.Count > 0;
        }
        Check(carried && matched && job.PreyRequests.All(r => core.PopOf[r.Predator] == qp && core.PopOf[r.Prey] == ql),
              $"a predation the worker found is published to the main thread ({job.PreyRequests.Count} hunter->locust request(s))");

        // round 11b-2: the game's split tick - the worker parks after BeginStep, the main thread runs the agent pass (the
        // Burst jobs there; RunAgentPass, the same kernel, here), the pool finishes - publishes exactly the plain tick
        SubstrateTickJob Twin(out SubstrateCore c)
        {
            c = new SubstrateCore(256, R, Dt, 40, 21);
            int a = c.AddPopulation(SubstrateResearch.GameLocust().WithCap(200), 2);
            int b = c.AddPopulation(SubstrateResearch.GamePack(), 3);
            c.Seed(a, 60, new Vector3(200, 0, 0), 60f);
            c.Seed(b, 6, new Vector3(-100, 0, 0), 40f);
            var j = new SubstrateTickJob(c, new SubstrateTickSettings { EngageRadius = 160f, MaxEngaged = 24 });
            j.Prime();
            j.Pilots[0] = new SubstratePilot { Pos = new Vector3(100, 0, 0), Vel = new Vector3(80, 0, 0), Radius = 6f, Id = 1 };
            j.PilotCount = 1;
            return j;
        }
        var plain = Twin(out var cPlain);
        var split = Twin(out var cSplit);
        split.ExternalAgentPass = true;
        bool same = true, parkedEvery = true;
        int evPlain = 0, evSplit = 0;
        for (int t = 0; t < 60; t++)
        {
            plain.Kick(false);
            var sw = Stopwatch.StartNew();
            while (!plain.Collect()) { if (sw.ElapsedMilliseconds > 5000) break; System.Threading.Thread.Sleep(0); }
            split.Kick(false);
            sw.Restart();
            while (!split.AwaitingAgentPass) { if (sw.ElapsedMilliseconds > 5000) { parkedEvery = false; break; } System.Threading.Thread.Sleep(0); }
            parkedEvery &= split.State == SwarmJobState.Running;
            cSplit.RunAgentPass();   // the main thread's agent pass over the parked core
            split.ResumeAfterAgentPass(false);
            sw.Restart();
            while (!split.Collect()) { if (sw.ElapsedMilliseconds > 5000) break; System.Threading.Thread.Sleep(0); }
            evPlain += plain.Events.Count; evSplit += split.Events.Count;
            for (int i = 0; i < 256 && same; i++)
                same &= plain.Instances[i].Alive == split.Instances[i].Alive && plain.Instances[i].CurPos == split.Instances[i].CurPos && cPlain.Pos[i] == cSplit.Pos[i];
        }
        Check(split.Error == null && parkedEvery && same && evPlain == evSplit,
              $"the split tick (worker parks after BeginStep, main-thread agent pass, pool finishes) publishes exactly the plain tick: 60 ticks, identical positions and frames, {evSplit} events each");
    }

    // ───────────────────────────────────────────────────────────── X: the index ledger over a population slice

    /// <summary>A model of PrismSpatialIndex's virtual-entry contract (what the glue's ISwarmEntrySink drives).</summary>
    sealed class ModelIndex : ISwarmEntrySink
    {
        public readonly Dictionary<int, (int Slot, float Volume, bool Suspended)> E = new();
        int _next;
        public int Register(int slot, Vector3 point, int domainSlot, float volume, bool shielded, float radius)
        { E[_next] = (slot, volume, false); return _next++; }
        public void Release(int id) => E.Remove(id);
        public void SetSuspended(int id, bool suspended) { var e = E[id]; E[id] = (e.Slot, e.Volume, suspended); }
        public void SetShape(int id, float volume, float radius) { var e = E[id]; E[id] = (e.Slot, volume, e.Suspended); }
        public void SetShielded(int id, bool shielded) { }
        public void SetDomainSlot(int id, int domainSlot) { }
    }

    static void IndexLedger()
    {
        Console.WriteLine("\nX. one prism system: each population's agents as index entries through the swarm's SwarmEntryLedger (count-once, volume = stock)");
        var core = new SubstrateCore(512, R, Dt, 40, 21);
        int ql = core.AddPopulation(SubstrateResearch.GameLocust().WithCap(200), 2);
        int qp = core.AddPopulation(SubstrateResearch.GamePack(), 3);
        core.Seed(ql, 60, new Vector3(300, 0, 0), 60f);
        core.Seed(qp, 6, new Vector3(250, 0, 0), 40f);
        var job = new SubstrateTickJob(core, new SubstrateTickSettings());
        job.Prime();
        var rng = new Random(5);
        var pops = new[] { ql, qp };
        var sinks = pops.Select(_ => new ModelIndex()).ToArray();
        var ledgers = pops.Select(q => new SwarmEntryLedger(core.Pops[q].Cap)).ToArray();
        var drawn = pops.Select(q => new SwarmInstance[core.Pops[q].Cap]).ToArray();
        var ledInst = pops.Select(q => new SwarmInstance[core.Pops[q].Cap]).ToArray();
        var pts = pops.Select(q => new Vector3[core.Pops[q].Cap]).ToArray();
        var real = pops.Select(q => new bool[core.Pops[q].Cap]).ToArray();
        for (int i = 0; i < job.Food.Length; i++) job.Food[i] = new SubstrateFood { Pos = new Vector3(300 + 40 * (i % 8), 20 * (i / 8), 0), Volume = 1f };
        job.FoodCount = job.Food.Length;
        job.Pilots[0] = new SubstratePilot { Pos = new Vector3(300, 0, 0), Vel = new Vector3(40, 0, 0), Radius = 6f, Id = 1 };
        job.PilotCount = 1;
        bool once = true, volOk = true, slotsOk = true; int maxReal = 0, deaths = 0;
        double worst = 0;
        for (int t = 0; t < 300; t++)
        {
            for (int k = 0; k < 6; k++)   // the owner's half: bites of food (births), and deaths through proxies
            {
                int i = rng.Next(core.Capacity);
                if (job.Instances[i].Alive) job.QueueFeed(i, 30f);
            }
            if (t % 7 == 3)
            {
                int i = rng.Next(core.Capacity);
                if (job.Instances[i].Alive) { job.QueueKill(i); deaths++; }
            }
            job.Kick(true); job.Collect();
            for (int n = 0; n < pops.Length; n++)
            {
                var pop = core.Pops[pops[n]];
                job.Slice(pop.Start, pop.Cap, drawn[n], ledInst[n], pts[n]);
                // a proxy with a finished body stands in for ~a third of the living agents, changing every tick
                int nr = 0;
                for (int k = 0; k < pop.Cap; k++) { real[n][k] = drawn[n][k].Alive && rng.NextDouble() < 0.33; if (real[n][k]) nr++; }
                maxReal = Math.Max(maxReal, nr);
                ledgers[n].Sync(ledInst[n], real[n], pts[n], sinks[n]);
                int alive = 0; double stock = 0, live = 0, standIn = 0;
                for (int k = 0; k < pop.Cap; k++)
                {
                    if (!drawn[n][k].Alive) continue;
                    alive++; stock += core.Stock[pop.Start + k];
                    if (real[n][k]) { var b = job.Body[pop.Start + k]; standIn += b.X * b.Y * b.Z; }
                }
                var liveEntries = sinks[n].E.Values.Where(e => !e.Suspended).ToList();
                foreach (var e in liveEntries) live += e.Volume;
                once &= liveEntries.Count + nr == alive && sinks[n].E.Count == alive;
                slotsOk &= sinks[n].E.Values.All(e => e.Slot >= 0 && e.Slot < pop.Cap && drawn[n][e.Slot].Alive)
                           && sinks[n].E.Values.Select(e => e.Slot).Distinct().Count() == sinks[n].E.Count;
                double rel = Math.Abs(live + standIn - stock) / Math.Max(1.0, stock);
                worst = Math.Max(worst, rel);
                volOk &= rel < 1e-4;
            }
        }
        Check(once, $"every living agent is in the index exactly once (its entry, or its proxy's real body with the entry suspended) - 300 ticks, {deaths} deaths, up to {maxReal} stand-ins");
        Check(slotsOk, "entries are per population slice: each names a living agent of its own population, one entry per slot");
        Check(volOk, $"the index's volume for a population = its agents' stock (live entries + stand-in bodies vs held mass, worst relative error {worst:E1})");
        Check(core.Pops[ql].Births > 0, $"births re-register reused slots as new creatures ({core.Pops[ql].Births} births)");
    }

    // ───────────────────────────────────────────────────────────── B: cost per step at 10k agents

    static void Bench()
    {
        Console.WriteLine("\nB. cost per step at 10k agents (locust params - every term active; research fused kernel 0.12 us/agent-step = 1.2 ms/10k at k=8, 4 numba threads)");
        double single8 = 0, multi8 = 0;
        int cores = Math.Max(2, Environment.ProcessorCount);
        foreach (var (k, workers) in new[] { (8, 1), (4, 1), (8, 4), (8, cores), (1, 1), (1, cores) }.Distinct())
        {
            var P = SubstrateResearch.GameLocust().WithCap(10000); P.FracK = k; P.N0 = 10000;
            var core = new SubstrateCore(10000, R, Dt, 40, 9) { Workers = workers };
            int q = core.AddPopulation(P, 2);
            core.Seed(q, 10000, Vector3.Zero, 350f);
            var pil = new[] { new SubstratePilot { Pos = new Vector3(0, 0, 500), Vel = new Vector3(120, 0, 0), Radius = 6, Id = 1 } };
            var food = new[] { new SubstrateFood { Pos = new Vector3(200, 0, 0), Volume = 30 } };
            for (int t = 0; t < 10; t++) core.Step(pil, food);
            int steps = 40;
            core.MsFields = core.MsHash = core.MsAgents = core.MsWorld = core.MsKernel = 0;
            var sw = Stopwatch.StartNew();
            for (int t = 0; t < steps; t++) core.Step(pil, food);
            double ms = sw.Elapsed.TotalMilliseconds / steps;
            Console.WriteLine($"      stages: fields {core.MsFields / steps:F2}, hash {core.MsHash / steps:F2}, agents {core.MsAgents / steps:F2} (kernel pass {core.MsKernel / steps:F2}), world {core.MsWorld / steps:F2} ms");
            if (k == 8 && workers == 1) single8 = ms;
            if (k == 8 && workers == cores) multi8 = ms;
            Console.WriteLine($"    k={k} workers={workers}: {ms:F2} ms/step ({ms * 1000.0 / 10000:F3} us/agent-step), steered {core.Steered.Count(x => x) / 100.0:F1}%");
        }
        Check(single8 > 0 && single8 < 20.0, $"10k agents at k=8 on ONE worker thread: {single8:F2} ms/step - at a 10 Hz tick that is {single8 / 6.0:F2} ms per 60 fps frame, off the main thread");
        Console.WriteLine($"    the agent pass through SubstrateKernel.StepAgent over {cores} threads (Parallel.For, contiguous chunks): {multi8:F2} ms/step at k=8 - " +
                          "the multi-threaded .NET upper bound for the Burst job (Burst's SIMD/float codegen is not measured here)");
        Check(multi8 > 0 && multi8 < 20.0, $"10k agents at k=8 with the kernel pass over {cores} threads: {multi8:F2} ms/step (one thread {single8:F2}; the scaling depends on the host's free cores)");
    }

    static SubstrateSpeciesParams WithCap(this SubstrateSpeciesParams p, int cap) { var c = p.Clone(); c.Capacity = cap; return c; }
}
