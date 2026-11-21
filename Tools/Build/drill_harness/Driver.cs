using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using HB = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

static class Driver
{
    static int _fail, _pass;

    static void Check(bool ok, string what)
    {
        if (ok) _pass++;
        else { _fail++; Console.WriteLine("FAIL: " + what); }
    }

    static void Set(object o, string field, object value)
    {
        var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (f == null) throw new Exception($"{o.GetType().Name}.{field} not found");
        f.SetValue(o, value);
    }

    static LessonStep Step(string id, string prompt, DrillApplicability a = DrillApplicability.Always,
                           Element e = Element.None, DrillCondition c = null, string hint = null) =>
        new() { Id = id, Prompt = prompt, Applicability = a, Element = e, Condition = c, HintPrompt = hint };

    static MentorTip Tip(string id, string prompt, MentorTier tier, int priority) =>
        new() { Id = id, Prompt = prompt, Tier = tier, Priority = priority };

    static TipListSO Tips(params MentorTip[] tips)
    {
        var so = new TipListSO();
        Set(so, "tips", new List<MentorTip>(tips));
        return so;
    }

    sealed class Facts : IDrillHullFacts
    {
        public VesselClassType Vessel { get; set; } = VesselClassType.Squirrel;
        public FlightScheme Scheme { get; set; } = FlightScheme.TwoThumb;
        public bool CanDrift { get; set; } = true;
        public string VesselName { get; set; } = "Squirrel";
        public string ModeName { get; set; } = "Switchback";
        public Dictionary<Element, (string label, string desc, string control)> Abilities = new();
        public bool AbilityHasInput(Element e) => Abilities.TryGetValue(e, out var a) && a.control != null;
        public bool TryAbility(Element e, out string l, out string d)
        {
            l = d = null;
            if (!Abilities.TryGetValue(e, out var a)) return false;
            l = a.label; d = a.desc;
            return !string.IsNullOrEmpty(l);
        }
        public bool TryAbilityControlLabel(Element e, out string l)
        {
            l = Abilities.TryGetValue(e, out var a) ? a.control : null;
            return !string.IsNullOrEmpty(l);
        }
        public bool TryControlLabel(DrillControl c, out string l)
        {
            l = c switch { DrillControl.Steer => "LS", DrillControl.Throttle => "RS", DrillControl.Drift => "LT+RT", _ => null };
            return l != null;
        }
    }

    sealed class Signals : IDrillSignals
    {
        public float Speed { get; set; }
        public float Throttle { get; set; }
        public float Steer { get; set; }
        public bool IsDrifting { get; set; }
        public int SkimCount { get; set; }
        public int GatesThreaded { get; set; }
        public int LapsCompleted { get; set; }
        public bool HasGate;
        public float GateSeconds, GateAngle;
        public bool TryGetNextGate(out float seconds, out float angle)
        {
            seconds = GateSeconds; angle = GateAngle; return HasGate;
        }
        public Dictionary<InputEvents, int> Presses = new();
        public Dictionary<Element, int> Abilities = new();
        public int PressCount(InputEvents i) => Presses.TryGetValue(i, out var n) ? n : 0;
        public int AbilityActivationCount(Element e) => Abilities.TryGetValue(e, out var n) ? n : 0;
    }

    static void GameOfTheWeek()
    {
        var utc = DateTimeKind.Utc;
        // 2024-01-01 is a Monday (the epoch): Sunday the 7th is still week 0, Monday the 8th is week 1.
        Check(CosmicShore.ScriptableObjects.GameOfTheWeekSO.WeekIndex(new DateTime(2024, 1, 7, 23, 59, 59, utc), 7) == 0,
              "gotw: Sunday belongs to the week that began on Monday");
        Check(CosmicShore.ScriptableObjects.GameOfTheWeekSO.WeekIndex(new DateTime(2024, 1, 8, 0, 0, 0, utc), 7) == 1,
              "gotw: the week turns at Monday 00:00 UTC");
        Check(CosmicShore.ScriptableObjects.GameOfTheWeekSO.WeekIndex(new DateTime(2024, 2, 19, 12, 0, 0, utc), 7) == 0,
              "gotw: the rotation wraps after its last week");
        Check(CosmicShore.ScriptableObjects.GameOfTheWeekSO.WeekIndex(new DateTime(2023, 12, 31, 12, 0, 0, utc), 7) == 6,
              "gotw: a date before the epoch wraps backwards, not to index 0");
        var gotw = new CosmicShore.ScriptableObjects.GameOfTheWeekSO();
        Check(gotw.For(new DateTime(2026, 10, 2, 0, 0, 0, utc)) == GameModes.SkimRace,
              "gotw: an empty rotation falls back");
    }

    static int Main()
    {
        Tokens();
        Conditions();
        Lesson();
        Mentor();
        Progress();
        HullFacts();
        GameOfTheWeek();
        RunnerDriver.Run(Check);
        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    static void Tokens()
    {
        var f = new Facts();
        f.Abilities[Element.Time] = ("Skim Boost", "Go fast near prisms.", "RT");

        Check(DrillTokens.Resolve("Hold {glyph:Time} to {ability:Time}.", f, out var o) == "Hold RT to Skim Boost." &&
              o == DrillTokenOutcome.Complete, "tokens: complete");
        Check(DrillTokens.Resolve("{vessel} in {mode}", f, out o) == "Squirrel in Switchback", "tokens: vessel/mode");
        Check(DrillTokens.Resolve("{glyph:Steer} to steer", f, out o) == "LS to steer", "tokens: control glyph");

        DrillTokens.Resolve("Use {ability:Charge}", f, out o);
        Check(o == DrillTokenOutcome.MissingFact, "tokens: missing ability is a missing fact");

        CSDebug.Warnings.Clear();
        string typo = DrillTokens.Resolve("Use {abilty:Time} now", f, out o);
        Check(typo == "Use {abilty:Time} now" && o == DrillTokenOutcome.UnknownToken, "tokens: typo renders as itself");
        DrillTokens.Resolve("Again {abilty:Time}", f, out _);
        Check(CSDebug.Warnings.Count == 1, $"tokens: typo logged once (got {CSDebug.Warnings.Count})");

        DrillTokens.Resolve("{ability:4}", f, out o);
        Check(o == DrillTokenOutcome.UnknownToken, "tokens: numeric element is a typo");
        DrillTokens.Resolve("{ability:Omni}", f, out o);
        Check(o == DrillTokenOutcome.UnknownToken, "tokens: Omni is not an ability element");
        DrillTokens.Resolve("{glyph:Boost}", f, out o);
        Check(o == DrillTokenOutcome.UnknownToken, "tokens: unknown control is a typo");

        DrillTokens.Resolve("{ability:Charge} {abilty:X}", f, out o);
        Check(o == DrillTokenOutcome.MissingFact, "tokens: missing fact outranks unknown token");

        Check(DrillTokens.Resolve(null, f, out o) == "" && o == DrillTokenOutcome.Complete, "tokens: empty template");
        Check(DrillTokens.Resolve("a { b", f, out o) == "a { b", "tokens: unclosed brace is text");
        Check(DrillTokens.IsKnownToken("glyph:Drift") && !DrillTokens.IsKnownToken("glyph:drift"), "tokens: case-sensitive");
    }

    static void Conditions()
    {
        var s = new Signals();
        var st = default(DrillConditionState);

        var steer = new SteerHeldCondition { MinDeflection = 0.5f, Seconds = 1f };
        steer.Begin(s, ref st);
        s.Steer = 0.9f;
        bool done = steer.Tick(s, 0.6f, ref st);
        s.Steer = 0.1f;
        done |= steer.Tick(s, 0.1f, ref st);
        s.Steer = 0.9f;
        done |= steer.Tick(s, 0.6f, ref st);
        Check(!done, "conditions: a release restarts a hold");
        Check(steer.Tick(s, 0.5f, ref st), "conditions: a continuous hold completes");

        var ability = new AbilityActivatedCondition { Element = Element.Time, Count = 2 };
        s.Abilities[Element.Time] = 5;
        ability.Begin(s, ref st);
        Check(!ability.Tick(s, 0.1f, ref st), "conditions: presses before Begin do not count");
        s.Abilities[Element.Time] = 7;
        Check(ability.Tick(s, 0.1f, ref st), "conditions: counts presses after Begin");

        var timer = new TimerCondition { Seconds = 1f };
        timer.Begin(s, ref st);
        Check(!timer.Tick(s, 0.5f, ref st) && timer.Tick(s, 0.5f, ref st), "conditions: timer");

        var gates = new GatesThreadedCondition { Count = 1 };
        s.GatesThreaded = 3;
        gates.Begin(s, ref st);
        s.GatesThreaded = 4;
        Check(gates.Tick(s, 0f, ref st), "conditions: gates");

        var ahead = new GateAheadCondition { MaxAngle = 30f, MinSeconds = 2f, MaxSeconds = 5f };
        ahead.Begin(s, ref st);
        Check(!ahead.Tick(s, 0f, ref st), "conditions: no course, no gate ahead");
        s.HasGate = true; s.GateAngle = 10f; s.GateSeconds = 3f;
        Check(ahead.Tick(s, 0f, ref st), "conditions: a gate inside the window is ahead");
        s.GateAngle = 40f;
        Check(!ahead.Tick(s, 0f, ref st), "conditions: too far off the line is not ahead");
        s.GateAngle = 10f; s.GateSeconds = 1f;
        Check(!ahead.Tick(s, 0f, ref st), "conditions: closer than MinSeconds is not 'a straight'");
        s.GateSeconds = 6f;
        Check(!ahead.Tick(s, 0f, ref st), "conditions: further than MaxSeconds is not yet");

        var laps = new LapsCompletedCondition { Count = 1 };
        s.LapsCompleted = 2;
        laps.Begin(s, ref st);
        Check(!laps.Tick(s, 0f, ref st), "conditions: laps before Begin do not count");
        s.LapsCompleted = 3;
        Check(laps.Tick(s, 0f, ref st), "conditions: a lap after Begin counts");
    }

    static DrillLibrarySO Library(out LessonTemplateSO twoThumb)
    {
        twoThumb = new LessonTemplateSO();
        Set(twoThumb, "steps", new List<LessonStep>
        {
            Step("steer", "Use {glyph:Steer} to steer.", c: new SteerHeldCondition()),
            Step("throttle", "Push {glyph:Throttle} to speed up.", c: new ThrottleHeldCondition(), hint: "Hold {glyph:Throttle}."),
            Step("time", "Hold {glyph:Time} - {ability:Time}.", DrillApplicability.AbilityHasInput, Element.Time,
                 new AbilityActivatedCondition(), "Try {abilityDescription:Space}"),
            Step("drift", "Hold {glyph:Drift} to drift.", DrillApplicability.HullDrifts, c: new DriftHeldCondition()),
            Step("empty", "", c: new TimerCondition()),
        });
        var oneThumb = new LessonTemplateSO();
        Set(oneThumb, "scheme", FlightScheme.OneThumb);
        Set(oneThumb, "steps", new List<LessonStep> { Step("aim", "Aim with {glyph:Steer}.") });

        var lib = new DrillLibrarySO();
        Set(lib, "twoThumbLesson", twoThumb);
        Set(lib, "oneThumbLesson", oneThumb);
        Set(lib, "abilityTipTemplate", "{ability:E}: {abilityDescription:E} ({glyph:E})");
        return lib;
    }

    static Facts FullSquirrel()
    {
        var f = new Facts();
        f.Abilities[Element.Time] = ("Skim", "Boost near prisms.", "RT");
        f.Abilities[Element.Charge] = ("Joust", "Steal on overtake.", null);   // passive
        f.Abilities[Element.Mass] = ("Boost Ring", "Lay a ring.", "A");
        f.Abilities[Element.Space] = ("Steal", "", "B");                      // no description
        return f;
    }

    static void Lesson()
    {
        var lib = Library(out _);
        var f = FullSquirrel();

        var dropped = new List<string>();
        var steps = DrillComposer.ComposeLesson(lib, f, dropped);
        Check(steps.Count == 4, $"lesson: full two-thumb hull gets 4 steps (got {steps.Count})");
        Check(steps[2].Prompt == "Hold RT - Skim.", "lesson: Time step resolved");
        Check(steps[2].Hint == "", "lesson: a hint naming a missing fact is dropped, the step is kept");
        Check(steps[1].Hint == "Hold RS." && steps[1].HintAfterSeconds == lib.DefaultHintAfterSeconds, "lesson: hint + default delay");
        Check(dropped.Count == 1 && dropped[0].StartsWith("empty"), "lesson: empty prompt dropped and reported");

        f.CanDrift = false;
        f.Abilities[Element.Time] = ("Skim", "x", null);
        steps = DrillComposer.ComposeLesson(lib, f);
        Check(steps.Count == 2, $"lesson: no drift + passive Time drops two steps (got {steps.Count})");

        f.Scheme = FlightScheme.OneThumb;
        steps = DrillComposer.ComposeLesson(lib, f);
        Check(steps.Count == 1 && steps[0].Prompt == "Aim with LS.", "lesson: scheme picks the template");

        f = FullSquirrel();
        var ov = new DrillLibrarySO.HullOverride { Vessel = VesselClassType.Squirrel };
        ov.SuppressedStepIds.Add("throttle");
        Set(lib, "hullOverrides", new List<DrillLibrarySO.HullOverride> { ov });
        steps = DrillComposer.ComposeLesson(lib, f);
        Check(steps.Count == 3 && steps[1].Source.Id == "time", "lesson: per-hull suppress");

        ov.ReplaceLesson = true;
        ov.ReplacementSteps.Add(Step("custom", "Just fly the {vessel}."));
        steps = DrillComposer.ComposeLesson(lib, f);
        Check(steps.Count == 1 && steps[0].Prompt == "Just fly the Squirrel.", "lesson: per-hull replacement");
    }

    static void Mentor()
    {
        var lib = Library(out _);
        var f = FullSquirrel();

        var raceTips = Tips(
            Tip("race-faster", "Cut the corners.", MentorTier.WinFaster, 0),
            Tip("race-gates", "Thread the gates in order.", MentorTier.WinTheMode, 0),
            Tip("race-charge", "{ability:Nope}", MentorTier.WinTheMode, 1),
            Tip("race-skip", "{ability:Charge} {abilityDescription:Charge}", MentorTier.WinTheMode, 2));
        var hullTips = Tips(Tip("sq-basic", "Squirrels love trails.", MentorTier.ShipBasics, 5),
                            Tip("race-gates", "dup id ignored", MentorTier.ShipBasics, 0));
        Set(lib, "metricTips", new List<DrillLibrarySO.MetricTips>
            { new() { Metric = ScoringMetric.SwitchesThreaded, Tips = raceTips } });
        Set(lib, "hullOverrides", new List<DrillLibrarySO.HullOverride>
            { new() { Vessel = VesselClassType.Squirrel, Tips = hullTips } });
        Set(lib, "advancedTips", Tips(Tip("adv", "Climb the board.", MentorTier.WinFaster, 99)));

        var list = DrillComposer.ComposeMentor(lib, f, GameModes.Switchback, ScoringMetric.SwitchesThreaded);
        var ids = new List<string>();
        foreach (var t in list) ids.Add(t.Id);
        string got = string.Join(",", ids);

        // ShipBasics: race-gates is claimed by the HULL list first (dedupe by id keeps the first
        // copy, priority 0), then sq-basic(5), then derived Mass (1002); Charge's ability is passive so its
        // {glyph:E} is a missing fact, Space has no description. WinTheMode: race-charge is a typo (kept, rendered), race-skip needs
        // Charge's description (kept - "Steal on overtake." exists). WinFaster: race-faster, adv.
        string want = "race-gates,sq-basic,ability:Squirrel:Mass,race-charge,race-skip,race-faster,adv";
        Check(got == want, $"mentor: order\n  got  {got}\n  want {want}");

        var seen = new HashSet<string> { "sq-basic", "race-faster" };
        list = DrillComposer.ComposeMentor(lib, f, GameModes.Switchback, ScoringMetric.SwitchesThreaded, seen);
        Check(list[list.Count - 2].Id == "sq-basic" && list[list.Count - 1].Id == "race-faster" &&
              list[0].Id == "race-gates", "mentor: seen tips move to the end in order");

        Set(lib, "modeTips", new List<DrillLibrarySO.ModeTips>
            { new() { Mode = GameModes.Switchback, Tips = Tips(Tip("sb", "Dolphin drift.", MentorTier.WinTheMode, 0)), ReplacesMetricTips = true } });
        list = DrillComposer.ComposeMentor(lib, f, GameModes.Switchback, ScoringMetric.SwitchesThreaded);
        bool hasRace = false, hasSb = false;
        foreach (var t in list) { hasRace |= t.Id == "race-faster"; hasSb |= t.Id == "sb"; }
        Check(hasSb && !hasRace, "mentor: mode tips can replace the metric family's");

        var silence = new DrillLibrarySO.HullOverride { Vessel = VesselClassType.Squirrel };
        silence.SilencedAbilityTips.Add(Element.Mass);
        Set(lib, "hullOverrides", new List<DrillLibrarySO.HullOverride> { silence });
        list = DrillComposer.ComposeMentor(lib, f, GameModes.Switchback, ScoringMetric.SwitchesThreaded);
        foreach (var t in list) Check(t.Origin != DrillTipOrigin.DerivedAbility, "mentor: silenced derived tip");
    }

    static void Progress()
    {
        Check(!DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb, false, false), "skip: first lesson ever, one-thumb");
        Check(!DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb, false, false), "skip: first lesson ever, two-thumb");
        Check(DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb, true, false), "skip: one-thumb after any");
        Check(!DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb, true, false), "skip: first two-thumb stays forced");
        Check(DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb, true, true), "skip: two-thumb after two-thumb");

        ProgressionBackendGate.CloudEnabled = false;
        DrillProgressStore.ResetLocal();
        Check(!DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb), "store: fresh account forced");
        DrillProgressStore.RecordLessonCompleted(FlightScheme.OneThumb);
        Check(DrillProgressStore.IsLessonSkippable(FlightScheme.OneThumb) &&
              !DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb), "store: one-thumb lesson sets only the any key");

        // The cloud's answer merges in: finishing a two-thumb lesson on another device counts here.
        UGSDataService.Instance = new UGSDataService();
        ProgressionBackendGate.CloudEnabled = true;
        UGSDataService.Instance.DrillRepo.Data.CompletedTwoThumbLesson = true;
        Check(DrillProgressStore.IsLessonSkippable(FlightScheme.TwoThumb), "store: cloud key merges with the mirror");

        DrillProgressStore.MarkTipSeen("a");
        DrillProgressStore.MarkTipSeen("a");
        DrillProgressStore.MarkTipSeen("bad|id");
        UGSDataService.Instance.DrillRepo.Data.SeenTipIds.Add("b");
        var seen = DrillProgressStore.SeenTipIds();
        Check(seen.Count == 2 && seen.Contains("a") && seen.Contains("b"), "store: seen tips union, separator rejected");
        Check(UGSDataService.Instance.DrillRepo.Dirty == 1, "store: marking seen twice dirties once");

        Check(DrillProgressStore.ReportLap(GameModes.Redline, VesselClassType.Manta, 40f), "store: first lap is a best");
        Check(!DrillProgressStore.ReportLap(GameModes.Redline, VesselClassType.Manta, 41f), "store: slower lap is not");
        UGSDataService.Instance.DrillRepo.Data.BestLaps[0].Seconds = 30f;
        Check(DrillProgressStore.TryGetBestLap(GameModes.Redline, VesselClassType.Manta, out float best) && best == 30f,
              "store: best lap takes the min of cloud and mirror");
        ProgressionBackendGate.CloudEnabled = false;
        UGSDataService.Instance = null;
    }

    static void HullFacts()
    {
        var map = new ElementalAbilityMapSO();
        Set(map, "entries", new List<ElementalAbilityEntry>
        {
            new() { Element = Element.Time, AbilityLabel = "Afterburner", AbilityDescription = "Fly faster.", Input = InputEvents.RightStickAction },
            new() { Element = Element.Charge, AbilityLabel = "Joust", Input = InputEvents.FullSpeedStraightAction },
            new() { Element = Element.Mass, AbilityLabel = "", Input = InputEvents.Button1Action },
        });

        var glyphs = new ControlGlyphSetSO();
        Set(glyphs, "glyphs", new List<ControlGlyphSetSO.Glyph>
            { new() { binding = HB.PadRightTrigger, keyboardLabel = "RShift" } });

        var lib = new DrillLibrarySO();
        lib.Strings.PadControlLabels.Add(new DrillLibrarySO.PadControlLabel { Binding = HB.PadRightTrigger, Label = "RT" });
        lib.Strings.FlightControlLabels.Add(new DrillLibrarySO.FlightControlLabel
            { Control = DrillControl.Steer, Scheme = FlightScheme.OneThumb, Keyboard = true, Label = "the mouse" });

        var pad = new DrillHullFacts(VesselClassType.Sparrow, FlightScheme.OneThumb, false, map, lib, glyphs, false, "Sparrow", "Dog Fight");
        var kb = new DrillHullFacts(VesselClassType.Sparrow, FlightScheme.OneThumb, false, map, lib, glyphs, true, "Sparrow", "Dog Fight");

        Check(pad.TryAbilityControlLabel(Element.Time, out var l) && l == "RT", "facts: pad label from the library");
        Check(kb.TryAbilityControlLabel(Element.Time, out l) && l == "RShift", "facts: keyboard label from the glyph set");
        Check(!pad.AbilityHasInput(Element.Charge), "facts: the FullSpeedStraightAction sentinel is passive");
        Check(!pad.AbilityHasInput(Element.Mass), "facts: an unlabelled slot is an open design slot");
        Check(!pad.AbilityHasInput(Element.Space), "facts: an absent entry");
        Check(kb.TryControlLabel(DrillControl.Steer, out l) && l == "the mouse" &&
              !pad.TryControlLabel(DrillControl.Steer, out _), "facts: flight labels per device");
    }
}
