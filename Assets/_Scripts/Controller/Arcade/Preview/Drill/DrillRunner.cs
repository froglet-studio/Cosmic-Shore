using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Where a microgame run's coaching is.</summary>
    public enum DrillPhase
    {
        Idle = 0,
        /// <summary>Section 1: the forced flight lesson (D2).</summary>
        Lesson = 1,
        /// <summary>Section 2: the always-open tip playlist.</summary>
        Mentor = 2,
        /// <summary>Every tip said; the closing line stays up.</summary>
        Done = 3,
    }

    /// <summary>What a run needs from the preview session that starts it.</summary>
    public struct DrillContext
    {
        public IVessel Vessel;
        public GameModes Mode;
        public ScoringMetric Metric;
        /// <summary>The card's player-facing name ({mode}).</summary>
        public string ModeName;
        /// <summary>The hull's player-facing name ({vessel}).</summary>
        public string VesselName;
        /// <summary>The preview's rings, or null on a card with no race.</summary>
        public ModePreviewGateCourse GateCourse;
        /// <summary>True on a party guest: a guest cannot be held in a window while the host
        /// launches, so its Lesson is skippable from the start (TRAINING_PLAN §4.5).</summary>
        public bool PartyGuest;
    }

    /// <summary>
    /// Runs one microgame visit's coaching: the Lesson, then the Mentor
    /// (Docs/ModePreview/TRAINING_PLAN.md §4.3-§4.5).
    ///
    /// <para><b>A plain MonoBehaviour beside <see cref="ModePreviewRunner"/>, with the same
    /// lifetime</b>: the session starts it at tap-in and stops it on every exit route. It never
    /// writes <c>GameDataSO</c> and never replicates - the preview is local by design.</para>
    ///
    /// <para><b>It decides; it does not draw.</b> Everything a view needs is a property here,
    /// and <see cref="OnChanged"/> fires whenever any of it moves, so a view needs no Update and
    /// the authoring tool can drive the same runner with no UI at all.</para>
    ///
    /// <para><b>Cost:</b> one live Lesson condition, or one pending Mentor moment, at a time;
    /// counters fed by events; nothing allocated per frame.</para>
    /// </summary>
    public sealed class DrillRunner : MonoBehaviour, IDrillSignals
    {
        /// <summary>Anything a view shows changed.</summary>
        public event Action OnChanged;

        /// <summary>The Lesson finished: true = completed, false = skipped.</summary>
        public event Action<bool> OnLessonEnded;

        /// <summary>Any runner's Lesson finished (true = completed, false = skipped). For listeners
        /// that cannot reach the runner - the first-login quest, which only knows a Lesson should
        /// happen somewhere.</summary>
        public static event Action<bool> AnyLessonEnded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyLessonEnded = null;

        public DrillPhase Phase { get; private set; } = DrillPhase.Idle;

        /// <summary>Section header, authored in the library's Strings.</summary>
        public string Title { get; private set; } = string.Empty;

        /// <summary>The line on screen now (prompt, hint, tip or closing line). Empty = nothing.</summary>
        public string Line { get; private set; } = string.Empty;

        /// <summary>True while the Lesson's hint is shown instead of its prompt.</summary>
        public bool ShowingHint { get; private set; }

        /// <summary>The element the current line is about, for the control chip and the row pulse.</summary>
        public Element LineElement { get; private set; } = Element.None;

        public DrillCue Cue { get; private set; } = DrillCue.None;

        public int StepIndex { get; private set; }
        public int StepCount => _steps.Count;

        /// <summary>The Skip button may be shown.</summary>
        public bool SkipAvailable { get; private set; }

        /// <summary>The Mentor's 'next' affordance may be shown.</summary>
        public bool NextAvailable => Phase == DrillPhase.Mentor;

        /// <summary>True while the Lesson may not be left: the window holds its release and the
        /// card's Play button stays disabled (D2). False the moment the Mentor starts.</summary>
        public bool HoldsExit => Phase == DrillPhase.Lesson && !_skippable;

        /// <summary>The library this run reads, for the view's chrome text.</summary>
        public DrillLibrarySO Library => _library;

        /// <summary>The hull's facts, for the view's control chip.</summary>
        public IDrillHullFacts Facts => _facts;

        /// <summary>True when the player is reading keyboard + mouse labels.</summary>
        public bool Keyboard => _keyboard;

        // ── State ────────────────────────────────────────────────────────

        DrillLibrarySO _library;
        DrillContext _ctx;
        DrillHullFacts _facts;
        ElementalAbilityMapSO _map;
        ControlGlyphSetSO _glyphs;
        R_VesselActionHandler _handler;
        InputDeviceFamily _family;
        bool _keyboard;
        bool _canDrift;
        bool _skippable;

        List<ComposedLessonStep> _steps = new();
        DrillConditionState _conditionState;
        DrillCondition _liveCondition;
        float _stepElapsed;
        float _lessonElapsed;

        List<ComposedTip> _tips = new();
        int _tipIndex;
        enum TipStage { Waiting, Showing, Gap }
        TipStage _tipStage;
        float _tipElapsed;

        readonly Dictionary<InputEvents, int> _presses = new();
        readonly Dictionary<Element, int> _abilityUses = new();

        static bool _warnedNoLibrary;

        // ── IDrillSignals ────────────────────────────────────────────────

        public float Speed => Status != null ? Status.Speed : 0f;
        public float Throttle => Status?.InputStatus != null ? Mathf.Clamp01(Status.InputStatus.XDiff) : 0f;

        public float Steer
        {
            get
            {
                var input = Status?.InputStatus;
                if (input == null) return 0f;
                return Mathf.Clamp01(Mathf.Max(input.EasedLeftJoystickPosition.magnitude,
                                               input.EasedRightJoystickPosition.magnitude));
            }
        }

        public bool IsDrifting => Status != null && Status.IsDrifting;

        /// <summary>No per-vessel skim channel is reachable from here (the skim event is a
        /// scene-wired SOAP asset shared by every vessel), so a Skims step is skipped rather than
        /// left waiting on a counter that never moves.</summary>
        public int SkimCount => 0;

        public int GatesThreaded => _ctx.GateCourse ? _ctx.GateCourse.Threaded : 0;
        public int LapsCompleted => _ctx.GateCourse ? _ctx.GateCourse.LapsCompleted : 0;

        public bool TryGetNextGate(out float seconds, out float angleDegrees)
        {
            seconds = angleDegrees = 0f;
            var status = Status;
            if (status == null || !_ctx.GateCourse || !_ctx.GateCourse.TryGetNextGate(out var gate)) return false;
            var t = _ctx.Vessel.Transform;
            if (!t) return false;

            Vector3 toGate = gate - t.position;
            float distance = toGate.magnitude;
            // Course, not the nose: "ahead" is where the ship is GOING (they differ in a drift).
            Vector3 heading = status.Course.sqrMagnitude > 1e-6f ? status.Course : t.forward;
            angleDegrees = distance > 1e-3f ? Vector3.Angle(heading, toGate) : 0f;
            seconds = distance / Mathf.Max(status.Speed, MinSpeedForSeconds);
            return true;
        }

        /// <summary>Below this speed a ship is not racing; the gate reads as far away rather than
        /// as infinitely far (and never as a divide by zero).</summary>
        const float MinSpeedForSeconds = 1f;
        public int PressCount(InputEvents input) => _presses.TryGetValue(input, out var n) ? n : 0;
        public int AbilityActivationCount(Element element) => _abilityUses.TryGetValue(element, out var n) ? n : 0;

        IVesselStatus Status => _ctx.Vessel is UnityEngine.Object o && !o ? null : _ctx.Vessel?.VesselStatus;

        // ── Lifecycle ────────────────────────────────────────────────────

        /// <summary>
        /// Start a run. False - and nothing started - when the library is missing or the vessel is
        /// not ready; the preview flies exactly as it did before the drill existed.
        /// </summary>
        public bool Begin(DrillContext context)
        {
            Stop();

            _library = DrillLibrarySO.Load();
            if (!_library)
            {
                if (!_warnedNoLibrary)
                    CSDebug.LogWarning($"[Drill] No Resources/{DrillLibrarySO.ResourcePath} - previews run without coaching.");
                _warnedNoLibrary = true;
                return false;
            }

            _ctx = context;
            var status = Status;
            if (status == null) return false;

            _map = ElementalAbilityMapSO.LoadFor(status.VesselType);
            _glyphs = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            _handler = status.ActionHandler;
            _canDrift = _handler && _handler.TryGetInputForAction<DriftActionSO>(out _);
            if (_handler) _handler.OnInputEventStarted += HandleInputStarted;

            _family = ResolveFamily();
            RebuildFacts();

            var scheme = _facts.Scheme;
            _skippable = context.PartyGuest || DrillProgressStore.IsLessonSkippable(scheme);
            _steps = DrillComposer.ComposeLesson(_library, _facts);
            _resumeKey = (context.Mode, status.VesselType);

            // Re-entering this card with this hull picks up where the last visit left off (§4.4).
            if (DrillResume.TryGet(_resumeKey, out var mark) && mark.Phase != DrillPhase.Idle)
            {
                if (mark.Phase == DrillPhase.Lesson && _steps.Count > 0)
                    EnterLesson(Mathf.Clamp(mark.StepIndex, 0, _steps.Count - 1));
                else if (mark.Phase == DrillPhase.Done)
                    FinishMentor();
                else
                    EnterMentor(mark.NextTipId);
                return true;
            }

            // A Lesson with no steps left after composition ends exactly as one whose every step
            // is skipped at run time (BeginStep): completed, which is what raises AnyLessonEnded.
            // Going straight to the Mentor here raised nothing, so a first-login guide waiting on
            // that event held its dim over the menu forever.
            if (_steps.Count > 0) EnterLesson(0);
            else CompleteLesson();
            return true;
        }

        /// <summary>End the run without recording anything. Idempotent.</summary>
        public void Stop()
        {
            RememberWhereWeAre();
            if (_handler) _handler.OnInputEventStarted -= HandleInputStarted;
            _handler = null;
            _liveCondition = null;
            _presses.Clear();
            _abilityUses.Clear();
            bool wasRunning = Phase != DrillPhase.Idle;
            Phase = DrillPhase.Idle;
            Line = Title = string.Empty;
            SkipAvailable = ShowingHint = false;
            LineElement = Element.None;
            Cue = DrillCue.None;
            if (wasRunning) OnChanged?.Invoke();
        }

        void OnDisable() => Stop();

        (GameModes, VesselClassType) _resumeKey;

        /// <summary>Record where this visit stopped, so the next one on this card and hull resumes
        /// there. Nothing is recorded for a run that never started.</summary>
        void RememberWhereWeAre()
        {
            if (Phase == DrillPhase.Idle) return;
            var mark = new DrillResume.Mark { Phase = Phase, StepIndex = StepIndex };
            if (Phase == DrillPhase.Mentor)
            {
                // A tip still waiting for its moment was never said: resume ON it. One already
                // said resumes at the one after it.
                int next = _tipStage == TipStage.Waiting ? _tipIndex : _tipIndex + 1;
                if (next >= _tips.Count) mark.Phase = DrillPhase.Done;
                else mark.NextTipId = _tips[next].Id;
            }
            DrillResume.Set(_resumeKey, mark);
        }

        /// <summary>The Skip button. Refused unless skipping is currently offered.</summary>
        public void Skip()
        {
            if (Phase != DrillPhase.Lesson || !SkipAvailable) return;
            OnLessonEnded?.Invoke(false);
            AnyLessonEnded?.Invoke(false);
            EnterMentor(null);
        }

        /// <summary>The Mentor's 'next': say the next tip now.</summary>
        public void NextTip()
        {
            if (Phase != DrillPhase.Mentor) return;
            if (_tipStage == TipStage.Showing) _tipIndex++;
            BeginTip();
        }

        void Update()
        {
            if (Phase == DrillPhase.Idle) return;
            if (Status == null) { Stop(); return; }

            // The device decides the words a prompt uses ("RT" or "RShift"), so a switch mid-run
            // re-says the same line in the new device's words.
            var family = ResolveFamily();
            if (family != _family)
            {
                _family = family;
                RebuildFacts();
                RefreshLine();
            }

            float dt = Time.unscaledDeltaTime;   // the preview runs beside a menu free to touch timeScale
            if (Phase == DrillPhase.Lesson) TickLesson(dt);
            else if (Phase == DrillPhase.Mentor) TickMentor(dt);
        }

        // ── The Lesson ───────────────────────────────────────────────────

        void EnterLesson(int stepIndex)
        {
            Phase = DrillPhase.Lesson;
            Title = _library.Strings.LessonTitle ?? string.Empty;
            _lessonElapsed = 0f;
            StepIndex = stepIndex;
            BeginStep();
        }

        void BeginStep()
        {
            while (StepIndex < _steps.Count && !CanRun(_steps[StepIndex].Source.Condition))
                StepIndex++;

            if (StepIndex >= _steps.Count)
            {
                CompleteLesson();
                return;
            }

            var step = _steps[StepIndex];
            _liveCondition = step.Source.Condition;
            _conditionState = default;
            _liveCondition?.Begin(this, ref _conditionState);
            _stepElapsed = 0f;
            ShowingHint = false;
            LineElement = step.Source.Element;
            Cue = step.Source.Cue;
            RefreshLine();
        }

        /// <summary>A Skims step has no signal to wait on here (see <see cref="SkimCount"/>);
        /// running it would hold a forced Lesson forever.</summary>
        static bool CanRun(DrillCondition condition)
        {
            if (condition is not SkimsCondition) return true;
            CSDebug.LogWarning("[Drill] A Skims step is in a Lesson, but the preview has no skim signal - step skipped.");
            return false;
        }

        void TickLesson(float dt)
        {
            _lessonElapsed += dt;
            _stepElapsed += dt;

            bool skip = _skippable && _lessonElapsed >= _library.LessonSkipDelaySeconds;
            bool changed = skip != SkipAvailable;
            SkipAvailable = skip;

            var step = _steps[StepIndex];
            if (!ShowingHint && !string.IsNullOrEmpty(step.Hint) && _stepElapsed >= step.HintAfterSeconds)
            {
                ShowingHint = true;
                RefreshLine();
                changed = false;   // RefreshLine raised it
            }

            if (_liveCondition == null || _liveCondition.Tick(this, dt, ref _conditionState))
            {
                StepIndex++;
                BeginStep();
                return;
            }

            if (changed) OnChanged?.Invoke();
        }

        void CompleteLesson()
        {
            DrillProgressStore.RecordLessonCompleted(_facts.Scheme);
            OnLessonEnded?.Invoke(true);
            AnyLessonEnded?.Invoke(true);
            EnterMentor(null);
        }

        // ── The Mentor ───────────────────────────────────────────────────

        /// <param name="resumeAtTipId">Start at this tip if the playlist still has it; null or a
        /// tip no longer in it starts at the top.</param>
        void EnterMentor(string resumeAtTipId)
        {
            _liveCondition = null;
            SkipAvailable = ShowingHint = false;
            Phase = DrillPhase.Mentor;
            Title = _library.Strings.MentorTitle ?? string.Empty;
            _tips = DrillComposer.ComposeMentor(_library, _facts, _ctx.Mode, _ctx.Metric,
                                                DrillProgressStore.SeenTipIds());
            _tipIndex = 0;
            if (!string.IsNullOrEmpty(resumeAtTipId))
                for (int i = 0; i < _tips.Count; i++)
                    if (_tips[i].Id == resumeAtTipId) { _tipIndex = i; break; }
            BeginTip();
        }

        void BeginTip()
        {
            if (_tipIndex >= _tips.Count)
            {
                FinishMentor();
                return;
            }

            var tip = _tips[_tipIndex];
            _tipElapsed = 0f;
            _liveCondition = tip.Moment;
            _conditionState = default;
            if (_liveCondition != null)
            {
                _liveCondition.Begin(this, ref _conditionState);
                _tipStage = TipStage.Waiting;
                Line = string.Empty;
                LineElement = Element.None;
                Cue = DrillCue.None;
                OnChanged?.Invoke();
                return;
            }
            ShowTip();
        }

        void ShowTip()
        {
            var tip = _tips[_tipIndex];
            _tipStage = TipStage.Showing;
            _tipElapsed = 0f;
            _liveCondition = null;
            LineElement = tip.Element;
            Cue = DrillCue.None;
            Line = tip.Prompt;
            DrillProgressStore.MarkTipSeen(tip.Id);
            OnChanged?.Invoke();
        }

        void TickMentor(float dt)
        {
            _tipElapsed += dt;
            switch (_tipStage)
            {
                case TipStage.Waiting:
                    // A moment changes WHEN a tip is said, never WHICH: one that never comes lets
                    // the tip go anyway (D10).
                    if (_liveCondition.Tick(this, dt, ref _conditionState) ||
                        _tipElapsed >= _library.MomentTimeoutSeconds)
                        ShowTip();
                    break;

                case TipStage.Showing:
                    if (_tipElapsed >= _tips[_tipIndex].DwellSeconds)
                    {
                        _tipStage = TipStage.Gap;
                        _tipElapsed = 0f;
                        Line = string.Empty;
                        LineElement = Element.None;
                        OnChanged?.Invoke();
                    }
                    break;

                case TipStage.Gap:
                    if (_tipElapsed >= _library.MentorGapSeconds)
                    {
                        _tipIndex++;
                        BeginTip();
                    }
                    break;
            }
        }

        void FinishMentor()
        {
            Phase = DrillPhase.Done;
            Title = _library.Strings.MentorTitle ?? string.Empty;
            _liveCondition = null;
            LineElement = Element.None;
            Line = DrillTokens.Resolve(_library.Strings.MentorClosingLine, _facts, out var outcome);
            if (outcome == DrillTokenOutcome.MissingFact) Line = string.Empty;
            OnChanged?.Invoke();
        }

        // ── Words ────────────────────────────────────────────────────────

        void RefreshLine()
        {
            if (Phase == DrillPhase.Lesson && StepIndex < _steps.Count)
            {
                var source = _steps[StepIndex].Source;
                string text = ShowingHint ? source.HintPrompt : source.Prompt;
                string resolved = DrillTokens.Resolve(text, _facts, out var outcome);

                // The new device may lack a label the old one had: keep the composed line rather
                // than going blank in the middle of a step.
                if (outcome == DrillTokenOutcome.MissingFact || string.IsNullOrEmpty(resolved))
                    resolved = ShowingHint ? _steps[StepIndex].Hint : _steps[StepIndex].Prompt;
                Line = resolved;
            }
            OnChanged?.Invoke();
        }

        void RebuildFacts()
        {
            var status = Status;
            _keyboard = _family == InputDeviceFamily.KeyboardMouse;
            var scheme = status != null && status.IsSingleStickControls ? FlightScheme.OneThumb : FlightScheme.TwoThumb;
            _facts = new DrillHullFacts(status != null ? status.VesselType : VesselClassType.Any, scheme, _canDrift,
                                        _map, _library, _glyphs, _keyboard, _ctx.VesselName, _ctx.ModeName);
        }

        InputDeviceFamily ResolveFamily()
        {
            var controller = Status?.InputController;
            var family = controller ? controller.ActiveDeviceFamily : InputDeviceFamily.None;
            return family == InputDeviceFamily.None ? InputDeviceActuation.DetectInitial() : family;
        }

        void HandleInputStarted(InputEvents input)
        {
            _presses[input] = PressCount(input) + 1;
            if (!_map) return;
            foreach (var entry in _map.Entries)
                if (entry != null && entry.Input == input && entry.Input != InputEvents.FullSpeedStraightAction)
                    _abilityUses[entry.Element] = AbilityActivationCount(entry.Element) + 1;
        }
    }
}
