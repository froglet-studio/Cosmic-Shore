// Drives the SHIPPED DrillRunner through scripted visits: a forced first Lesson, a skippable
// later one, hints, a party guest, and the Mentor's pacing.
using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

static class RunnerDriver
{
    sealed class Input : IInputStatus
    {
        public float XDiff { get; set; }
        public Vector2 EasedLeftJoystickPosition { get; set; }
        public Vector2 EasedRightJoystickPosition { get; set; }
    }

    sealed class Status : IVesselStatus
    {
        public float Speed { get; set; }
        public bool IsDrifting { get; set; }
        public bool IsSingleStickControls { get; set; }
        public VesselClassType VesselType { get; set; } = VesselClassType.Squirrel;
        public R_VesselActionHandler ActionHandler { get; set; } = new();
        public InputController InputController { get; set; } = new();
        public IInputStatus InputStatus => In;
        public Vector3 Course { get; set; } = Vector3.forward;
        public Input In = new();
    }

    sealed class Vessel : MonoBehaviour, IVessel
    {
        public Status S = new();
        public Transform T = new();
        public IVesselStatus VesselStatus => S;
        public Transform Transform => T;
    }

    static Action<bool, string> _check;
    static readonly MethodInfo Update = typeof(DrillRunner).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);

    static void Tick(DrillRunner r, float seconds, float dt = 0.1f)
    {
        Time.unscaledDeltaTime = dt;
        for (float t = 0; t < seconds - 1e-4f; t += dt) Update.Invoke(r, null);
    }

    static void Set(object o, string f, object v) =>
        o.GetType().GetField(f, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).SetValue(o, v);

    static DrillLibrarySO BuildLibrary()
    {
        var two = new LessonTemplateSO();
        Set(two, "steps", new List<LessonStep>
        {
            new() { Id = "steer", Prompt = "Steer with {glyph:Steer}.", HintPrompt = "Push the sticks.",
                    HintAfterSeconds = 2, Condition = new SteerHeldCondition { MinDeflection = 0.5f, Seconds = 1 } },
            new() { Id = "time", Prompt = "Press {glyph:Time}: {ability:Time}.", Element = Element.Time,
                    Applicability = DrillApplicability.AbilityHasInput, Condition = new AbilityActivatedCondition { Element = Element.Time } },
            new() { Id = "skim", Prompt = "Skim.", Condition = new SkimsCondition { Count = 1 } },
            new() { Id = "drift", Prompt = "Drift.", Applicability = DrillApplicability.HullDrifts,
                    Condition = new DriftHeldCondition { Seconds = 0.5f } },
        });
        var lib = new DrillLibrarySO();
        Set(lib, "twoThumbLesson", two);
        Set(lib, "abilityTipTemplate", "{ability:E} on {glyph:E}");
        var tips = new TipListSO();
        Set(tips, "tips", new List<MentorTip>
        {
            new() { Id = "fast", Prompt = "Go fast.", Tier = MentorTier.WinFaster,
                    Moment = new SpeedAtLeastCondition { Speed = 100 } },
        });
        Set(lib, "advancedTips", tips);
        lib.Strings.LessonTitle = "LEARN";
        lib.Strings.MentorTitle = "TIPS";
        lib.Strings.MentorClosingLine = "Done in the {vessel}.";
        lib.Strings.PadControlLabels.Add(new DrillLibrarySO.PadControlLabel
            { Binding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding.PadRightTrigger, Label = "RT" });
        lib.Strings.PadControlLabels.Add(new DrillLibrarySO.PadControlLabel
            { Binding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding.PadButtonSouth, Label = "A" });
        lib.Strings.FlightControlLabels.Add(new DrillLibrarySO.FlightControlLabel
            { Control = DrillControl.Steer, Scheme = FlightScheme.TwoThumb, Keyboard = false, Label = "the sticks" });
        lib.Strings.FlightControlLabels.Add(new DrillLibrarySO.FlightControlLabel
            { Control = DrillControl.Steer, Scheme = FlightScheme.TwoThumb, Keyboard = true, Label = "WASD" });
        return lib;
    }

    static ElementalAbilityMapSO BuildMap()
    {
        var map = new ElementalAbilityMapSO();
        Set(map, "entries", new List<ElementalAbilityEntry>
        {
            new() { Element = Element.Time, AbilityLabel = "Skim Boost", AbilityDescription = "x", Input = InputEvents.RightStickAction },
            new() { Element = Element.Mass, AbilityLabel = "Boost Ring", AbilityDescription = "y", Input = InputEvents.Button1Action },
        });
        return map;
    }

    static string Last(List<string> log) => log.Count > 0 ? log[log.Count - 1] : "";

    static void GhostDemo(DrillRunner r, Vessel v, DrillContext ctx, DrillLibrarySO lib, ElementalAbilityMapSO map)
    {
        var check = _check;

        // The decision, pure.
        var tap = DrillGhostDemo.PlanFor(new AbilityActivatedCondition { Element = Element.Time }, map, true, InputEvents.LeftStickAction);
        check(tap.Kind == GhostDemoPress.Tap && tap.Input == InputEvents.RightStickAction,
              $"ghost: an ability step taps that element's button ({tap.Kind} {tap.Input})");
        var hold = DrillGhostDemo.PlanFor(new DriftHeldCondition(), map, true, InputEvents.LeftStickAction);
        check(hold.Kind == GhostDemoPress.Hold && hold.Input == InputEvents.LeftStickAction, "ghost: a drift step holds the drift control");
        check(!DrillGhostDemo.PlanFor(new DriftHeldCondition(), map, false, InputEvents.LeftStickAction).HasPress,
              "ghost: a hull with no drift gets no drift press");
        check(!DrillGhostDemo.PlanFor(new AbilityActivatedCondition { Element = Element.Charge }, map, true, default).HasPress,
              "ghost: an ability the hull lacks gets no press");
        check(!DrillGhostDemo.PlanFor(new SteerHeldCondition(), map, true, default).HasPress &&
              !DrillGhostDemo.PlanFor(null, map, true, default).HasPress,
              "ghost: steering (and no condition) is shown by flying alone");
        check(DrillGhostDemo.PlanFor(new InputPressedCondition { Input = InputEvents.Button2Action }, map, true, default).Input == InputEvents.Button2Action,
              "ghost: a button step taps that button");
        check(!DrillGhostDemo.IsPressed(tap, 0.4f, 3f, 0.5f, 0.4f) && DrillGhostDemo.IsPressed(tap, 0.6f, 3f, 0.5f, 0.4f) &&
              !DrillGhostDemo.IsPressed(tap, 0.95f, 3f, 0.5f, 0.4f), "ghost: a tap waits its lead-in, then lets go");
        check(DrillGhostDemo.IsPressed(hold, 2.4f, 3f, 0.5f, 0.4f) && !DrillGhostDemo.IsPressed(hold, 2.6f, 3f, 0.5f, 0.4f),
              "ghost: a hold lets go a lead-in before the hand-over");

        // The runner, scripted. Mark the two button steps as demos.
        var two = lib.LessonFor(FlightScheme.TwoThumb);
        var steps = (List<LessonStep>)typeof(LessonTemplateSO).GetField("steps", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(two);
        foreach (var st in steps) if (st.Id == "time" || st.Id == "drift") st.Cue = DrillCue.GhostDemo;
        lib.Strings.GhostDemoTitle = "WATCH";

        DrillProgressStore.ResetLocal();
        DrillResume.Clear();
        v.S.Speed = 0;
        v.S.IsDrifting = false;
        var log = v.S.ActionHandler.Log;
        log.Clear();
        var demos = new List<bool>();
        void OnDemo(bool d) => demos.Add(d);
        r.OnGhostDemoChanged += OnDemo;

        r.Begin(ctx);
        check(!r.GhostDemoActive && demos.Count == 0, "ghost: a step without the cue is not demonstrated");
        v.S.In.EasedLeftJoystickPosition = new Vector2(0.9f, 0);
        for (int guard = 0; !r.GhostDemoActive && guard < 50; guard++) Tick(r, 0.1f);   // stop on the demo's first frame
        v.S.In.EasedLeftJoystickPosition = new Vector2(0, 0);
        check(r.GhostDemoElapsed == 0f, $"ghost: measured from the demo's start ({r.GhostDemoElapsed})");
        check(r.StepIndex == 1 && r.GhostDemoActive && demos.Count == 1 && demos[0],
              $"ghost: the Time step opens with a demo, asking for the autopilot (step {r.StepIndex}, {demos.Count})");
        check(r.Title == "WATCH" && r.Line == "Press RT: Skim Boost.", $"ghost: the header changes, the prompt is said ({r.Title} / {r.Line})");
        Tick(r, 0.4f);
        check(log.Count == 0, "ghost: nothing pressed during the lead-in");
        Tick(r, 0.2f);
        check(Last(log) == "+RightStickAction", $"ghost: the demo taps the Time button ({Last(log)})");
        check(r.StepIndex == 1, "ghost: the autopilot's press does not complete the player's step");
        Tick(r, 0.5f);
        check(Last(log) == "-RightStickAction", "ghost: the tap is let go");
        Tick(r, 2.0f);
        check(!r.GhostDemoActive && demos.Count == 2 && !demos[1] && r.Title == "LEARN" && r.StepIndex == 1,
              $"ghost: after 3 s the stick goes back and the step waits for the player ({r.Title} {r.StepIndex})");
        Tick(r, 0.5f);
        check(r.StepIndex == 1 && !r.GhostDemoActive,
              $"ghost: frames after the hand-over, the autopilot's press still has not completed the step ({r.StepIndex})");
        check(!r.ShowingHint, "ghost: the hint clock starts at the hand-over, not with the demo");
        v.S.ActionHandler.Raise(InputEvents.RightStickAction);
        Tick(r, 0.1f);
        check(r.StepIndex == 3 && r.GhostDemoActive && demos.Count == 3,
              $"ghost: the player's press completes it; the drift step opens with its own demo (step {r.StepIndex})");
        Tick(r, 1.0f);
        check(Last(log) == "+LeftStickAction", $"ghost: the drift demo holds the drift control ({Last(log)})");
        Tick(r, 1.6f);
        check(Last(log) == "-LeftStickAction" && r.GhostDemoActive, "ghost: the hold is let go before the hand-over");
        Tick(r, 0.5f);
        check(!r.GhostDemoActive && demos.Count == 4 && r.Phase == DrillPhase.Lesson, "ghost: drift handed over");
        v.S.IsDrifting = true;
        Tick(r, 0.6f);
        v.S.IsDrifting = false;
        check(r.Phase == DrillPhase.Mentor, $"ghost: the player's drift completes the Lesson ({r.Phase})");
        r.Stop();

        // Stop mid-demo lets go of the control and does NOT announce: whoever stopped the run owns the stick.
        DrillResume.Set((GameModes.Switchback, VesselClassType.Squirrel), new DrillResume.Mark { Phase = DrillPhase.Lesson, StepIndex = 3 });
        demos.Clear();
        r.Begin(ctx);
        check(r.GhostDemoActive && demos.Count == 1, "ghost: resuming onto a demo step demonstrates it");
        Tick(r, 1.0f);
        r.Stop();
        check(!r.GhostDemoActive && Last(log) == "-LeftStickAction" && demos.Count == 1,
              $"ghost: Stop releases the held control without handing the stick back ({Last(log)}, {demos.Count})");
        DrillResume.Clear();

        // Skip mid-demo hands the stick back. A longer demo so Skip (3 s) appears inside it.
        Set(lib, "ghostDemoSeconds", 5f);
        DrillProgressStore.RecordLessonCompleted(FlightScheme.TwoThumb);
        DrillResume.Set((GameModes.Switchback, VesselClassType.Squirrel), new DrillResume.Mark { Phase = DrillPhase.Lesson, StepIndex = 3 });
        demos.Clear();
        r.Begin(ctx);
        Tick(r, 3.05f);
        check(r.GhostDemoActive && r.SkipAvailable, "ghost: Skip appears during a long demo on a later visit");
        r.Skip();
        check(!r.GhostDemoActive && demos.Count == 2 && !demos[1] && r.Phase == DrillPhase.Mentor && Last(log) == "-LeftStickAction",
              "ghost: Skip mid-demo lets go and hands the stick back");
        r.Stop();
        DrillResume.Clear();

        // Ghost demos off: the cue is ignored.
        Set(lib, "ghostDemoSeconds", 0f);
        DrillProgressStore.ResetLocal();
        demos.Clear();
        DrillResume.Set((GameModes.Switchback, VesselClassType.Squirrel), new DrillResume.Mark { Phase = DrillPhase.Lesson, StepIndex = 1 });
        r.Begin(ctx);
        check(!r.GhostDemoActive && demos.Count == 0 && r.Title == "LEARN", "ghost: 0 seconds turns demos off");
        r.Stop();
        DrillResume.Clear();

        Set(lib, "ghostDemoSeconds", 3f);
        foreach (var st in steps) st.Cue = DrillCue.None;
        r.OnGhostDemoChanged -= OnDemo;
        DrillProgressStore.ResetLocal();
    }

    public static void Run(Action<bool, string> check)
    {
        _check = check;
        var lib = BuildLibrary();
        var map = BuildMap();
        Resources.Loader = path => path == DrillLibrarySO.ResourcePath ? lib
                                 : path.StartsWith(ElementalAbilityMapSO.ResourceFolder) ? map
                                 : null;
        ProgressionBackendGate.CloudEnabled = false;
        DrillProgressStore.ResetLocal();
        DrillResume.Clear();

        // ── 1. First visit: forced. ──
        var v = new Vessel();
        var r = new DrillRunner();
        int changes = 0;
        r.OnChanged += () => changes++;
        bool? ended = null;
        r.OnLessonEnded += c => ended = c;
        var ctx = new DrillContext { Vessel = v, Mode = GameModes.Switchback, Metric = ScoringMetric.SwitchesThreaded,
                                     ModeName = "Switchback", VesselName = "Squirrel" };
        check(r.Begin(ctx), "runner: begins");
        check(r.Phase == DrillPhase.Lesson && r.Title == "LEARN" && r.StepCount == 4,
              $"runner: forced lesson of 4 (the skim step is composed, then skipped live) - got {r.Phase} {r.StepCount}");
        check(r.Line == "Steer with the sticks.", $"runner: first line ({r.Line})");
        check(r.HoldsExit, "runner: the first lesson holds the exit");
        check(v.S.ActionHandler.Subscribers == 1, "runner: subscribed once to the action handler");

        Tick(r, 2.5f);
        check(r.ShowingHint && r.Line == "Push the sticks.", $"runner: hint after its delay ({r.Line})");
        Tick(r, 1.0f);   // 3.5 s into the lesson: past the skip delay
        check(!r.SkipAvailable, "runner: no Skip on a forced lesson, even after the delay");

        v.S.InputController.Family = InputDeviceFamily.KeyboardMouse;
        Tick(r, 0.1f);
        check(r.Line == "Push the sticks.", "runner: a device switch keeps a token-free hint as written");

        v.S.In.EasedLeftJoystickPosition = new Vector2(0.9f, 0);
        Tick(r, 0.5f);
        v.S.In.EasedLeftJoystickPosition = new Vector2(0, 0);
        Tick(r, 0.2f);
        check(r.StepIndex == 0, "runner: a released steer did not complete the step");
        v.S.In.EasedLeftJoystickPosition = new Vector2(0.9f, 0);
        Tick(r, 1.1f);
        check(r.StepIndex == 1 && r.LineElement == Element.Time, $"runner: steer step completes (step {r.StepIndex})");
        check(r.Line == "Press RT: Skim Boost.",
              $"runner: a keyboard label that does not exist keeps the composed line ({r.Line})");
        v.S.InputController.Family = InputDeviceFamily.Gamepad;

        v.S.ActionHandler.Raise(InputEvents.Button1Action);
        Tick(r, 0.1f);
        check(r.StepIndex == 1, "runner: the wrong button does not complete the Time step");
        v.S.ActionHandler.Raise(InputEvents.RightStickAction);
        Tick(r, 0.1f);
        check(r.StepIndex == 3 && r.Line == "Drift.", $"runner: Skims step skipped, now drift (step {r.StepIndex}, {r.Line})");

        v.S.IsDrifting = true;
        Tick(r, 0.6f);
        check(ended == true && r.Phase == DrillPhase.Mentor && !r.HoldsExit, $"runner: lesson completes into the Mentor ({r.Phase})");
        check(DrillProgressStore.CompletedAnyLesson && DrillProgressStore.CompletedTwoThumbLesson,
              "runner: completing a two-thumb lesson sets both keys");

        // Mentor: derived Mass tip first (ShipBasics), then "fast" waits for its moment.
        check(r.Title == "TIPS" && r.Line.StartsWith("Boost Ring on"), $"runner: first tip ({r.Line})");
        Tick(r, 6.05f);
        check(r.Line == "", "runner: a tip leaves after its dwell");
        Tick(r, 8.05f);
        check(r.Line == "", "runner: the next tip waits for its moment");
        v.S.Speed = 150;
        Tick(r, 0.1f);
        check(r.Line == "Go fast.", $"runner: the moment arrives and the tip is said ({r.Line})");
        r.NextTip();
        check(r.Phase == DrillPhase.Done && r.Line == "Done in the Squirrel.", $"runner: closing line ({r.Phase} {r.Line})");
        check(DrillProgressStore.SeenTipIds().Contains("fast"), "runner: shown tips are marked seen");

        r.Stop();
        check(v.S.ActionHandler.Subscribers == 0 && r.Phase == DrillPhase.Idle, "runner: Stop unsubscribes and idles");

        // ── 1b. Resume: the closed run above ended at Done, so re-entering says the closing line. ──
        r.Begin(ctx);
        check(r.Phase == DrillPhase.Done && r.Line == "Done in the Squirrel.",
              $"runner: re-entering a finished card resumes at its closing line ({r.Phase})");
        r.Stop();
        DrillResume.Clear();

        // ── 2. Later visit: skippable after the delay; skipping records nothing new. ──
        DrillProgressStore.ResetLocal();
        DrillProgressStore.RecordLessonCompleted(FlightScheme.TwoThumb);
        v.S.In.EasedLeftJoystickPosition = new Vector2(0, 0);
        v.S.Speed = 0;
        ended = null;
        r.Begin(ctx);
        check(!r.HoldsExit && !r.SkipAvailable, "runner: a later lesson does not hold the exit and starts without Skip");
        Tick(r, 3.05f);
        check(r.SkipAvailable, "runner: Skip appears after 3 s");
        r.Skip();
        check(ended == false && r.Phase == DrillPhase.Mentor, "runner: Skip goes to the Mentor");
        string firstTip = r.Line;
        r.NextTip();
        check(r.Line == "", $"runner: next goes to the moment-gated tip, which waits ({r.Line})");
        r.Stop();
        // Forget the seen list, so a run from the top would say the Mass tip first again: only
        // the resume mark can put it on the waiting tip.
        DrillProgressStore.ResetLocal();
        r.Begin(ctx);
        check(r.Phase == DrillPhase.Mentor && r.Line == "",
              $"runner: tapping back in resumes ON the tip still waiting, not the top ({r.Phase} '{r.Line}' after '{firstTip}')");
        r.Stop();
        DrillResume.Clear();

        // ── 2b. A Lesson left part way resumes at its step. ──
        DrillProgressStore.ResetLocal();
        DrillProgressStore.RecordLessonCompleted(FlightScheme.TwoThumb);
        r.Begin(ctx);
        v.S.In.EasedLeftJoystickPosition = new Vector2(0.9f, 0);
        Tick(r, 1.2f);
        v.S.In.EasedLeftJoystickPosition = new Vector2(0, 0);
        check(r.StepIndex == 1, $"runner: steer done (step {r.StepIndex})");
        r.Stop();
        r.Begin(ctx);
        check(r.Phase == DrillPhase.Lesson && r.StepIndex == 1,
              $"runner: re-entering mid-Lesson resumes at its step ({r.Phase} {r.StepIndex})");
        r.Stop();
        DrillResume.Clear();

        // ── 2c. Another card is another visit. ──
        var other = ctx;
        other.Mode = GameModes.Headlong;
        DrillResume.Set((GameModes.Switchback, VesselClassType.Squirrel), new DrillResume.Mark { Phase = DrillPhase.Done });
        r.Begin(other);
        check(r.Phase == DrillPhase.Lesson && r.StepIndex == 0, "runner: a resume mark is per card");
        r.Stop();
        DrillResume.Clear();

        // ── 2d. The gate signal: seconds at speed, angle off the course. ──
        var gc = new ModePreviewGateCourse { HasGate = true, Gate = new Vector3(0, 0, 300) };
        var raced = ctx;
        raced.GateCourse = gc;
        DrillProgressStore.RecordLessonCompleted(FlightScheme.TwoThumb);
        r.Begin(raced);
        v.S.Speed = 100;
        check(r.TryGetNextGate(out float secs, out float ang) && Math.Abs(secs - 3f) < 1e-3f && ang < 1e-3f,
              $"runner: a gate 300 ahead at 100 u/s is 3 s dead ahead ({secs}, {ang})");
        v.S.Course = new Vector3(1, 0, 0);
        r.TryGetNextGate(out _, out ang);
        check(Math.Abs(ang - 90f) < 1e-2f, $"runner: angle is measured off the COURSE ({ang})");
        v.S.Speed = 0;
        r.TryGetNextGate(out secs, out _);
        check(secs > 0f && !float.IsInfinity(secs), $"runner: a stopped ship reads a finite time ({secs})");
        gc.HasGate = false;
        check(!r.TryGetNextGate(out _, out _), "runner: no lit ring, no gate");
        v.S.Course = Vector3.forward;
        r.Stop();
        DrillResume.Clear();

        // ── 3. Party guest: never forced, even on a fresh account. ──
        DrillProgressStore.ResetLocal();
        var guest = ctx;
        guest.PartyGuest = true;
        r.Begin(guest);
        check(!r.HoldsExit, "runner: a party guest is never held");
        r.Stop();

        // ── 3b. Ghost demo (TRAINING_PLAN §10.7): the autopilot shows a step, then hands over. ──
        GhostDemo(r, v, ctx, lib, map);

        // ── 4. A vessel destroyed mid-run stops the runner. ──
        r.Begin(ctx);
        v.Destroyed = true;
        Tick(r, 0.1f);
        check(r.Phase == DrillPhase.Idle, "runner: a destroyed vessel ends the run");

        // ── 5. No library: refuses quietly. ──
        Resources.Loader = _ => null;
        check(!new DrillRunner().Begin(ctx), "runner: no library, no run");
    }
}
