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
