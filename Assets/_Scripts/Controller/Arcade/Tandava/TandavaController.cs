using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md) - an ARENA co-op hunt in a CLOSED cell. One creature, a
    /// tadpole swarm, hatches whole as the GREAT SERPENT and lives inside the membrane with the cell's dispersed flora.
    /// It goes where it likes to eat, its speed and its manner set by how threatened it feels (the pilots near it, how
    /// fast they close, how fast it is losing members). At a plant it settles into its FEED pose - its plates go out to
    /// orbit its mouth as DANGER guards - and it stops regrowing: the one time it is both dangerous to approach and unable
    /// to heal. Hurt badly enough at the table, it bolts. Banked, it takes its next form: the MANY-HEADED SERPENT, which
    /// banked in turn RISES where it stands into the LORD OF THE DANCE - a halo of twelve rings (<see cref="TandavaHaloRing"/>)
    /// lights round it, its attendant packs patrol it, a drum runs and the cell glows gold - and when the drum stops it is
    /// the ANTLION, whose last feast completes the cycle. A form, once taken, is remembered: cut limbs regrow from its
    /// stomach. Every match draws one of three VARIANTS of each form. The mode has no fire: its effect is gold prism
    /// debris (<see cref="TandavaGoldBurst"/>). Every pilot flies one domain; they win by shattering its body below a third
    /// of its form, breaking the halo, wiping it out, starving it, or holding it off until the clock runs out.
    ///
    /// This controller is the swarm's <see cref="ISwarmDirector"/> on EVERY peer (fauna are client-local, the Brood Rush
    /// precedent). The SERVER runs the director (<see cref="TandavaDirectorCore"/>, pure C#, proven in
    /// Tools/Build/swarm_core_harness mode `tandava`) on its own swarm and replicates what it decides - the variants, the
    /// form and the plan the body wears, the phase and mood (every peer derives the same speed and turn from them), the
    /// goal, the anchor, the HUD's numbers, the halo's placement and its broken / guarded masks. A CLIENT's swarm takes
    /// the replicated plan, levers and goal and is nudged toward the server's anchor, so every peer hunts the same animal
    /// in the same place. It is also the top-left goal stack's <see cref="IGoalSource"/> on every peer. Scoring is the server's.
    /// </summary>
    public class TandavaController : MultiplayerDomainGamesController, ISwarmDirector, IGoalSource
    {
        [Header("Tandava")]
        [Tooltip("Drag TandavaSettings.asset - the forms and their variants, the director's dials, the halo, the gold " +
                 "burst, the dance's palette, the narration.")]
        [SerializeField] TandavaSettingsSO settings;
        [Tooltip("Drag TandavaScoringRule.asset - the outcome-based rule (winner, scores, results).")]
        [SerializeField] TandavaScoringRuleSO rule;

        /// <summary>The live Tandava match, if any (the turn monitor and the HUD arrow read it).</summary>
        public static TandavaController Current { get; private set; }

        readonly NetworkVariable<int> _variants = new(0);
        readonly NetworkVariable<int> _form = new(0);
        readonly NetworkVariable<int> _plan = new(-1);
        readonly NetworkVariable<int> _phase = new((int)TandavaPhase.Roam);
        readonly NetworkVariable<int> _mood = new((int)TandavaMood.Calm);
        readonly NetworkVariable<int> _outcome = new((int)TandavaOutcome.Running);
        readonly NetworkVariable<bool> _racing = new(false);
        readonly NetworkVariable<Vector3> _goal = new();
        readonly NetworkVariable<Vector3> _anchor = new();
        readonly NetworkVariable<float> _progress = new(0f);
        readonly NetworkVariable<float> _body = new(1f);
        readonly NetworkVariable<float> _timeLeft = new(-1f);
        /// <summary>Rising: the seconds before the halo lights. Dancing: the drum's seconds left. Else 0.</summary>
        readonly NetworkVariable<float> _drum = new(0f);
        readonly NetworkVariable<Vector3> _haloCentre = new();
        readonly NetworkVariable<Vector3> _haloUp = new();
        readonly NetworkVariable<Vector3> _haloSide = new();
        readonly NetworkVariable<int> _haloOut = new(0);
        readonly NetworkVariable<int> _haloGuarded = new(0);
        readonly NetworkVariable<int> _haloToBreak = new(EndConditionOverridesSO.DefaultTandavaHaloRingsToBreak);

        TandavaDirectorCore _core;     // server only
        int[] _picks;                  // server: the variants it drew
        SwarmFauna _swarm;
        float _lastTick = -1f, _nextFoodCheck, _nextGuardCheck, _turnStartedAt = -1f, _flashUntil;
        int _endedForm = -1, _revision, _appliedForm = -1, _meals, _lunges;
        bool _finalResultsSent, _cellPrepared, _warnedNoSwarm, _tinted;
        Vector3 _flashAt;
        readonly List<TandavaFood> _food = new();
        readonly List<TandavaPilot> _pilots = new();
        readonly Dictionary<IPlayer, Vector3> _pilotLast = new();
        readonly List<Vector3> _burstFrom = new();
        // the halo, as this peer draws it
        TandavaHaloRing[] _halo;
        GameObject _haloRoot;
        readonly Dictionary<IPlayer, Vector3> _lastPilotPos = new();

        protected override bool UseGolfRules => false;
        // flora, the swarm and the cell's trail mass do not reset in place (the Brood Rush / SkimRace precedent)
        protected override bool UseSceneReloadForReplay => true;
        // end-game runs through OnTurnEndedCustom -> SyncFinalScores_ClientRpc
        protected override bool HasEndGame => false;

        public TandavaOutcome Outcome => (TandavaOutcome)_outcome.Value;
        public int FormIndex => _form.Value;
        public TandavaPhase Phase => (TandavaPhase)_phase.Value;
        public TandavaMood Mood => (TandavaMood)_mood.Value;
        public bool InDance => Phase == TandavaPhase.Dance && Outcome == TandavaOutcome.Running;
        /// <summary>How close the swarm is to its next form (the final form: to completing the cycle), 0..1.</summary>
        public float EvolveProgress => _progress.Value;
        public int HaloBroken => CountBits(_haloOut.Value);
        public int HaloToBreak => _haloToBreak.Value;
        /// <summary>The swarm the pilots hunt (this peer's), for the HUD arrow.</summary>
        public Transform SwarmTransform => _swarm ? _swarm.transform : null;
        /// <summary>The name the HUD gives the form the swarm wears now - the variant this match drew.</summary>
        public string FormName => VariantName(_form.Value);

        // ───────────────────────────────────────────────────────────── lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Current = this;
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
            rule.ResetOutcome();

            _form.OnValueChanged += OnFormChanged;
            _phase.OnValueChanged += OnPhaseChanged;
            _outcome.OnValueChanged += OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;

            if (IsServer) DrawVariants();
            if (settings && settings.SwarmConfig) SwarmFauna.SetDirector(settings.SwarmConfig, this);
            else CSDebug.LogError("[Tandava] TandavaController has no settings / swarm config - the swarm will not be directed.");
            GoalStack.Source = this;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) DisarmChasers();
            _form.OnValueChanged -= OnFormChanged;
            _phase.OnValueChanged -= OnPhaseChanged;
            _outcome.OnValueChanged -= OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (_haloRoot) Destroy(_haloRoot);
            _halo = null;
            if (settings && settings.SwarmConfig) SwarmFauna.ClearDirector(settings.SwarmConfig, this);
            if (ReferenceEquals(GoalStack.Source, this)) GoalStack.Source = null;
            if (Current == this) Current = null;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            // the one-off tint work (the cytoplasm's material clone) happens while the ready screen is up, never at the rise
            if (!_cellPrepared && TryGetCell(out var cell) && cell.CytoplasmVisual)
            {
                CellVisualTint.For(cell).Prepare();
                _cellPrepared = true;
            }
            if (IsServer && _racing.Value && !_swarm && !_warnedNoSwarm && _turnStartedAt >= 0f && Time.time - _turnStartedAt > 30f)
            {
                _warnedNoSwarm = true;
                CSDebug.LogError("[Tandava] The hunt has run 30 s with no swarm: is the Tandava swarm config in the cell's spawn " +
                                 "profile, and does it name this settings' SwarmConfig? The match cannot end without one.");
            }
            if (!settings) return;
            UpdateHalo();
            // the gold light over the last burst: reported while it is fresh, then its afterglow fades it (continuity)
            if (Time.time < _flashUntil)
                PrismLit.PublishLight(GetInstanceID(), LitVolume.Sphere(_flashAt, settings.FlashRadius), settings.FlashStrength,
                                      Domains.Gold);
        }

        /// <summary>Server: one variant of each form for this match, drawn once and replicated as one int.</summary>
        void DrawVariants()
        {
            if (!settings) return;
            var counts = settings.Forms.Select(f => Mathf.Max(1, f.Variants?.Count ?? 1)).ToArray();
            _picks = TandavaDirectorCore.DrawVariants(System.Environment.TickCount ^ GetInstanceID(), counts);
            _variants.Value = TandavaDirectorCore.Pack(_picks);
        }

        int Pick(int form)
        {
            if (!settings || form < 0 || form >= settings.Forms.Count) return 0;
            int n = Mathf.Max(1, settings.Forms[form].Variants?.Count ?? 1);
            int pick = IsServer && _picks != null ? _picks[form] : TandavaDirectorCore.Unpack(_variants.Value, form);
            return Mathf.Clamp(pick, 0, n - 1);
        }

        TandavaVariantSpec Variant(int form)
        {
            if (!settings || form < 0 || form >= settings.Forms.Count) return default;
            var v = settings.Forms[form].Variants;
            return v is { Count: > 0 } ? v[Pick(form)] : default;
        }

        string VariantName(int form)
        {
            if (!settings || form < 0 || form >= settings.Forms.Count) return "";
            var v = Variant(form);
            return string.IsNullOrEmpty(v.DisplayName) ? settings.Forms[form].DisplayName : v.DisplayName;
        }

        // ───────────────────────────────────────────────────────────── the hunt

        protected override void OnCountdownTimerEnded()
        {
            if (IsServer) ArmChasers();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
        }

        /// <summary>
        /// Every AI pilot HUNTS the swarm: its steering runs at the swarm's body, spread sideways by seat so a grid of bots
        /// strikes the flank instead of one point. While the swarm FEEDS it strikes the body BEHIND its centre - clear of
        /// the guards orbiting its mouth, where a hit counts and a feeding swarm cannot heal. During the DANCE it flies at
        /// the halo instead - the nearest ring that is whole and unguarded, aimed just past it along the ring's axis so the
        /// line it flies threads the mouth. Steering is <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else,
        /// so it cannot leak into another mode. Honest limit (the Tollway rule, restated for speed): the Squirrel's and
        /// Sparrow's autopilots fly at CRUISE (60 / 35 u/s) and the swarm cruises at 60 and bolts at ~125 - an AI Sparrow
        /// catches it only at a plant. An all-AI lobby is not expected to win.
        /// </summary>
        void ArmChasers()
        {
            int seat = 0;
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;
                float side = (seat++ % 3 - 1) * 45f;   // -45 / 0 / +45 u across the body
                var me = p;
                pilot.SetExternalTargetProvider(() =>
                {
                    var self = me.Vessel?.Transform;
                    if (InDance && self && TryNearestOpenRing(self.position, out var ring))
                    {
                        Vector3 rp = ring.transform.position;
                        float from = Mathf.Sign(Vector3.Dot(self.position - rp, ring.Axis));
                        return rp - ring.Axis * ((from == 0f ? 1f : from) * 30f);
                    }
                    if (!_swarm) return Origin + settings.HatchPoint;
                    Vector3 at = _swarm.AnchorWorld;
                    if (Phase == TandavaPhase.Feed)
                        return at - _swarm.BodyForward * settings.AiFlankBack + _swarm.BodySide * side;
                    Vector3 ahead = (IsServer && _core != null ? V(_core.Goal) : _goal.Value) - at;
                    Vector3 dir = ahead.sqrMagnitude > 1f ? ahead.normalized : _swarm.BodyForward;
                    Vector3 across = Vector3.Cross(dir, Vector3.up);
                    if (across.sqrMagnitude < 1e-4f) across = Vector3.forward;
                    return at + dir * 40f + across.normalized * side;
                });
            }
        }

        void DisarmChasers()
        {
            var players = gameData != null ? gameData.Players : null;
            if (players == null) return;
            foreach (var p in players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }
        }

        void HandleTurnStarted()
        {
            _turnStartedAt = Time.time;
            if (!IsServer) return;
            _racing.Value = true;
            Narrate(GameToastSituation.TandavaMatchStart, TandavaLine.Start);
        }

        bool TryGetCell(out Cell cell)
        {
            cell = null;
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count && !cell; i++) cell = cells[i];
            return cell;
        }

        Vector3 Origin => TryGetCell(out var cell) ? cell.transform.position : Vector3.zero;

        // ───────────────────────────────────────────────────────────── the server's director

        void BuildCore(SwarmFauna swarm)
        {
            var forms = new List<TandavaForm>(settings.Forms.Count);
            float stomach = swarm.StomachCapacityVolume;
            for (int k = 0; k < settings.Forms.Count; k++)
            {
                var spec = settings.Forms[k];
                var v = Variant(k);
                var coilMouths = Coils(v);
                forms.Add(new TandavaForm
                {
                    Name = VariantName(k), Role = spec.Role, PlanIndex = v.PlanIndex, FeedPlanIndex = v.FeedPlanIndex,
                    PlanCount = swarm.FormMemberCount(v.PlanIndex), FillToEvolve = spec.FillToEvolve,
                    Bank = spec.BankShare * stomach, MealVolume = spec.MealVolume, Mouth = S(v.Mouth), FeedMouth = S(v.FeedMouth),
                    CoilPlanIndices = coilMouths.Length > 0 ? v.CoilPlanIndices : System.Array.Empty<int>(), CoilMouths = coilMouths,
                    CoilRoamRadius = v.CoilRoamRadius,
                });
            }
            var overrides = EndConditionOverridesSO.Instance;
            int breakPercent = overrides ? overrides.GetTandavaBreakPercent() : EndConditionOverridesSO.DefaultTandavaBreakPercent;
            var ds = settings.Director.Clone();   // the asset is never written at runtime
            int toBreak = Mathf.Clamp(overrides ? overrides.GetTandavaHaloRingsToBreak() : EndConditionOverridesSO.DefaultTandavaHaloRingsToBreak,
                                      1, Mathf.Max(1, ds.HaloCount));
            _haloToBreak.Value = toBreak;
            ds.HaloToBreak = toBreak;
            ds.ShatterFraction = breakPercent / 100f;
            ds.Centre = S(Origin);
            _core = new TandavaDirectorCore(forms, ds, System.Environment.TickCount);
        }

        void TickCore(SwarmFauna swarm)
        {
            float now = Time.time;
            float dt = _lastTick < 0f ? 0f : now - _lastTick;
            _lastTick = now;

            if (now >= _nextFoodCheck)
            {
                _nextFoodCheck = now + settings.FoodCheckSeconds;
                SurveyFood(swarm);
            }
            SensePilots(dt);

            var state = new TandavaSwarmState
            {
                Alive = swarm.MemberCount, Lost = swarm.MembersLost, Anchor = S(swarm.AnchorWorld),
                Forward = S(swarm.BodyForward), Up = S(swarm.BodyUp), Side = S(swarm.BodySide),
                Stomach0 = swarm.StomachVolume(0), Stomach1 = swarm.StomachVolume(1),
                Stomach2 = swarm.StomachVolume(2), Stomach3 = swarm.StomachVolume(3),
                StomachFill = swarm.StomachFraction, SinceBite = swarm.SecondsSinceBite,
                EatenTotal = swarm.EatenTotal, Starving = swarm.IsStarving,
            };
            _core.Tick(dt, state, _food, _pilots);
            DrainEvents(swarm);

            if (_core.InDance && now >= _nextGuardCheck)
            {
                _nextGuardCheck = now + 0.25f;
                UpdateGuards(swarm);
            }
            _phase.Value = (int)_core.Phase;
            _mood.Value = (int)_core.Mood;
            _plan.Value = _core.WantPlan;
            _goal.Value = V(_core.Goal);
            _anchor.Value = swarm.AnchorWorld;
            _progress.Value = _core.Progress(state);
            _body.Value = _core.BodyFraction(state);
            _timeLeft.Value = _core.TimeRemaining;
            _drum.Value = _core.InDance ? _core.DrumRemaining : _core.RiseRemaining;
        }

        /// <summary>Every plant the swarm can eat (the swarm's own edibility predicate), weighed by its live prisms.</summary>
        void SurveyFood(SwarmFauna swarm)
        {
            _food.Clear();
            var live = FloraHeartRegistry.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var f = live[i];
                if (!f || !swarm.CanEat(f)) continue;
                _food.Add(new TandavaFood
                {
                    // a bite is one leaf: a reef of seven species weighs each plant by its OWN leaf (FoodPerPrism: a plant that reports none)
                    Id = f.GetInstanceID(), Position = S(f.HeartTransform.position),
                    Volume = f.HealthBlockCount * (f.LeafVolume > 0f ? f.LeafVolume : settings.FoodPerPrism),
                });
            }
        }

        /// <summary>Every live pilot's position, and its velocity from the last tick (what the swarm senses).</summary>
        void SensePilots(float dt)
        {
            _pilots.Clear();
            foreach (var p in gameData.Players)
            {
                var t = p?.Vessel?.Transform;
                if (!t) { if (p != null) _pilotLast.Remove(p); continue; }
                Vector3 at = t.position;
                Vector3 vel = dt > 1e-3f && _pilotLast.TryGetValue(p, out var last) ? (at - last) / dt : Vector3.zero;
                if (vel.sqrMagnitude > settings.MaxPlausibleSpeed * settings.MaxPlausibleSpeed) vel = Vector3.zero;   // a respawn
                _pilotLast[p] = at;
                _pilots.Add(new TandavaPilot { Position = S(at), Velocity = S(vel) });
            }
        }

        /// <summary>Apply what the director decided this tick (or what a threaded ring just ended). Server only.</summary>
        void DrainEvents(SwarmFauna swarm)
        {
            for (int q = 0; q < _core.Events.Count; q++)
            {
                var e = _core.Events[q];
                switch (e.Kind)
                {
                    case TandavaEventKind.FormCommitted:
                        _plan.Value = _core.WantPlan;
                        _form.Value = e.B;   // every peer: the burst, the HUD, the cell (OnFormChanged)
                        if (settings.Forms[e.B].Role != TandavaFormRole.Dance)
                            Narrate(GameToastSituation.TandavaFormTaken, TandavaLine.Form, e.B);
                        break;
                    case TandavaEventKind.FeedBegan:
                        if (_meals++ == 0) Narrate(GameToastSituation.TandavaFeeding, TandavaLine.Feeding);
                        break;
                    case TandavaEventKind.MoodChanged:
                        if (e.B == (int)TandavaMood.Lunging && _lunges++ < 2) Narrate(GameToastSituation.TandavaLunge, TandavaLine.Lunge);
                        break;
                    case TandavaEventKind.FeedEnded:
                        if (e.B == (int)TandavaMealEnd.Broken) Narrate(GameToastSituation.TandavaMealBroken, TandavaLine.MealBroken);
                        break;
                    case TandavaEventKind.Rising:
                        Narrate(GameToastSituation.TandavaRising, TandavaLine.Rising);
                        break;
                    case TandavaEventKind.DanceBegan:
                        PlaceHalo(swarm);   // before the phase replicates, so a peer that sees Dance has the halo's place
                        Narrate(GameToastSituation.TandavaHaloLit, TandavaLine.HaloLit);
                        break;
                    case TandavaEventKind.HaloBroken:
                        _haloOut.Value = _core.HaloOutMask;
                        if (e.B == 1 || e.B == _core.S.HaloToBreak - 1)
                            Narrate(GameToastSituation.TandavaHaloBroken, e.B == 1 ? TandavaLine.FirstHalo : TandavaLine.LastHalo,
                                    e.B, _core.S.HaloToBreak);
                        break;
                    case TandavaEventKind.Ended:
                        var outcome = (TandavaOutcome)e.B;
                        _endedForm = _core.FormIx;
                        rule.Publish(outcome, _core.FormIx + 1, settings.Forms.Count, VariantName(_core.FormIx));
                        // a broken dance kills nobody: the figure falls back into the serpent it rose from (a molt)
                        if (outcome == TandavaOutcome.DanceBroken && _core.FormIx > 0)
                        {
                            int back = _core.FormIx - 1;
                            _plan.Value = Variant(back).PlanIndex;
                            _form.Value = back;
                        }
                        _phase.Value = (int)TandavaPhase.Over;
                        _outcome.Value = (int)outcome;   // the turn monitor sees the rule's outcome and ends the turn
                        break;
                }
            }
            _core.Events.Clear();
        }

        void Narrate(GameToastSituation situation, TandavaLine line, int a = 0, int b = 0) =>
            Narrate_ClientRpc((int)situation, (int)line, a, b);

        [ClientRpc]
        void Narrate_ClientRpc(int situation, int line, int a, int b)
        {
            if (!settings) return;
            var l = (TandavaLine)line;
            GameToastAPI.Post((GameToastSituation)situation, Domains.Blue,
                              settings.LineText(l, a, b, l == TandavaLine.Form ? VariantName(a) : null));
        }

        // ───────────────────────────────────────────────────────────── ISwarmDirector (every peer)

        bool ISwarmDirector.TryGetSeed(SwarmFauna swarm, out Vector3 position, out Vector3 heading)
        {
            _swarm = swarm;
            position = Origin + settings.HatchPoint;
            heading = settings.HatchHeading.sqrMagnitude > 1e-6f ? settings.HatchHeading.normalized : Vector3.right;
            return true;
        }

        bool ISwarmDirector.TryGetSeedForm(SwarmFauna swarm, out int form)
        {
            // it hatches WHOLE as the variant of the first form this match drew (a client that has not heard the draw
            // yet hatches as the first variant, and re-sorts into the drawn one on its first published tick)
            form = settings && settings.Forms.Count > 0 ? Variant(0).PlanIndex : 0;
            return settings;
        }

        bool ISwarmDirector.TryGetGoal(SwarmFauna swarm, out Vector3 goal)
        {
            goal = CurrentGoal();
            return true;
        }

        /// <summary>Before the go the swarm holds where it hatched; after it, the server's director (a client reads the
        /// replicated goal, and holds until the first one arrives).</summary>
        Vector3 CurrentGoal()
        {
            var hold = Origin + settings.HatchPoint;
            if (!_racing.Value) return hold;
            if (IsServer) return _core != null ? V(_core.Goal) : hold;
            return _goal.Value != Vector3.zero ? _goal.Value : hold;
        }

        void ISwarmDirector.OnTickPublished(SwarmFauna swarm)
        {
            _swarm = swarm;
            if (IsServer)
            {
                if (_core == null) BuildCore(swarm);
                if (_racing.Value && _core.Outcome == TandavaOutcome.Running) TickCore(swarm);
                else _anchor.Value = swarm.AnchorWorld;
            }
            ApplyToSwarm(swarm);
            if (!IsServer)
            {
                // a client follows the server's swarm, gently
                Vector3 gap = _anchor.Value - swarm.AnchorWorld;
                float d = gap.magnitude;
                if (d > settings.NudgeThreshold && _anchor.Value != Vector3.zero)
                    swarm.TryNudge(gap * (Mathf.Min(settings.MaxNudge, d * settings.NudgeFraction) / d));
            }
        }

        /// <summary>
        /// Every peer, every published tick: the body wears the plan the director wants (a new FORM is a new body -
        /// <see cref="SwarmFauna.RequestForm"/>; the same form's other pose is the same members re-arranged -
        /// <see cref="SwarmFauna.RequestPose"/>), swims at the speed and turn its phase and mood mean, holds its laying
        /// while it feeds, and takes the goal straight in - the fauna's own goal poll runs every few seconds, far too slow
        /// for a creature that bolts.
        /// </summary>
        void ApplyToSwarm(SwarmFauna swarm)
        {
            int want = _plan.Value >= 0 ? _plan.Value : Variant(0).PlanIndex;
            int form = _form.Value;
            if (swarm.FormIndex != want)
            {
                if (form != _appliedForm) swarm.RequestForm(want);
                else swarm.RequestPose(want);
            }
            _appliedForm = form;
            TandavaDirectorCore.LeversFor(_racing.Value ? Phase : TandavaPhase.Over, Mood, settings.Director,
                                          out float cruise, out float turn, out bool hold);
            swarm.SetLevers(cruise, turn, hold);
            swarm.Goal = CurrentGoal();
        }

        void ISwarmDirector.OnFormCommitted(SwarmFauna swarm, int fromForm, int toForm) { }

        // ───────────────────────────────────────────────────────────── every peer: the burst, the HUD, the cell

        /// <summary>The dance form (the one whose role is Dance), or -1.</summary>
        int DanceForm
        {
            get
            {
                if (!settings) return -1;
                for (int k = 0; k < settings.Forms.Count; k++) if (settings.Forms[k].Role == TandavaFormRole.Dance) return k;
                return -1;
            }
        }

        /// <summary>Every peer, on the replicated form: the old shape shatters into gold as the new one takes it, the goal
        /// stack's primary row flares with the new name, and the cell glows gold for the dance alone - from the rise
        /// until the dance ends; every other form, it keeps its own colours.</summary>
        void OnFormChanged(int previous, int current)
        {
            _revision++;
            GoalStack.RefreshAll();
            if (!settings) return;
            FormBurst();
            if (Outcome != TandavaOutcome.Running) return;
            if (current == DanceForm && DanceForm >= 0) TintTo(settings.AscensionPalette);
            else if (previous == DanceForm) RestoreCell();
        }

        void OnPhaseChanged(int previous, int current) => GoalStack.RefreshAll();

        void OnOutcomeChanged(int previous, int current)
        {
            if ((TandavaOutcome)current != TandavaOutcome.Running) RestoreCell();   // the cell keeps its own colours
            GoalStack.RefreshAll();
        }

        /// <summary>Gold shards thrown from this peer's own swarm, and a gold light over it.</summary>
        void FormBurst()
        {
            if (!_swarm) return;
            _burstFrom.Clear();
            _swarm.SampleMemberPositions(_burstFrom, settings.FormBurstShards);
            var theme = gameData ? gameData.ThemeManagerData : null;
            TandavaGoldBurst.Burst(theme, _swarm.AnchorWorld, _burstFrom, settings.FormBurstShards, settings.BurstSpeed, settings.BurstScale);
            Flash(_swarm.AnchorWorld);
        }

        void Flash(Vector3 at)
        {
            _flashAt = at;
            _flashUntil = Time.time + settings.FlashSeconds;
        }

        void TintTo(in CellPalette palette)
        {
            if (!TryGetCell(out var cell)) return;
            CellVisualTint.For(cell).TransitionTo(palette, settings.TransitionSeconds, settings.TransitionFlash);
            _tinted = true;
        }

        /// <summary>Back to the cell's own colours - only if this mode tinted it (an untinted cell is left untouched).</summary>
        void RestoreCell()
        {
            if (!_tinted || !settings || !TryGetCell(out var cell)) return;
            CellVisualTint.For(cell).Restore(settings.RestoreSeconds);
            _tinted = false;
        }

        // ───────────────────────────────────────────────────────────── IGoalSource: the top-left goal stack

        int IGoalSource.Revision => _revision;

        /// <summary>
        /// The goal stack's rows, every peer, from the replicated state: the FORM the swarm wears (the variant's name) and
        /// how close it is to the next - the first thing a pilot needs, and the row that flares when the form changes;
        /// what it is DOING and how much of its body it has (feeding is the row that says "now"); and the CLOCK it must
        /// beat. In the dance the halo is the objective: the rings broken against the rings that break it, and the drum.
        /// </summary>
        bool IGoalSource.TryGetGoals(List<GoalEntry> goals)
        {
            if (!settings || settings.Forms.Count == 0) return false;
            var phase = Phase;
            float body = Mathf.Clamp01(_body.Value);
            var bodyText = settings.BodyFormat.Replace("{0}", Mathf.RoundToInt(100f * body).ToString());
            if (phase == TandavaPhase.Dance)
            {
                goals.Add(GoalEntry.Count(null, settings.HaloLabel, HaloBroken, HaloToBreak));
                goals.Add(GoalEntry.Progress(null, settings.DancingLabel, body, bodyText));
                goals.Add(GoalEntry.Text(null, settings.DrumLabel, Clock(_drum.Value)));
                return true;
            }
            int form = Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1);
            string name = VariantName(form);
            if (settings.Forms[form].Role == TandavaFormRole.Final) name = $"{name} - {settings.FeastLabel}";
            if (phase == TandavaPhase.Rising)
                goals.Add(GoalEntry.Progress(null, name, 1f, settings.RisingLabel));
            else
                goals.Add(GoalEntry.Progress(null, name, _progress.Value, $"{Mathf.RoundToInt(100f * _progress.Value)}%"));
            goals.Add(GoalEntry.Progress(null, MoodLabel(phase), body, bodyText));
            if (_timeLeft.Value >= 0f) goals.Add(GoalEntry.Text(null, settings.TimeLabel, Clock(_timeLeft.Value)));
            return true;
        }

        string MoodLabel(TandavaPhase phase) => phase switch
        {
            TandavaPhase.Feed => settings.FeedingLabel,
            TandavaPhase.Rising => settings.RisingLabel,
            TandavaPhase.Dance => settings.DancingLabel,
            _ => Mood switch
            {
                TandavaMood.Lunging => settings.LungingLabel,
                TandavaMood.Fleeing => settings.FleeingLabel,
                TandavaMood.Wary => settings.WaryLabel,
                _ => settings.RoamingLabel,
            },
        };

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // ───────────────────────────────────────────────────────────── game end (the Brood Rush / SkimRace pattern)

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            DisarmChasers();
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            var winner = rule.ResolveWinner(gameData);
            rule.AssignScores(gameData, winner, 0f);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            string winnerName = winner == Domains.Blue
                ? "The swarm"
                : gameData.RoundStatsList.Where(s => s.Domain == winner).OrderByDescending(s => s.Score).Select(s => s.Name).FirstOrDefault() ?? "";
            _finalResultsSent = true;

            var outcome = Outcome;
            bool won = TandavaScoringRuleSO.PilotsWon(outcome);
            Narrate(won ? GameToastSituation.TandavaBroken : GameToastSituation.TandavaCompleted,
                    !won ? TandavaLine.Completed
                    : outcome == TandavaOutcome.DanceBroken ? TandavaLine.DanceBroken
                    : outcome == TandavaOutcome.HeldOff ? TandavaLine.HeldOff : TandavaLine.Won);
            SyncFinalScoresSnapshot(winnerName, winner, outcome);
        }

        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain, TandavaOutcome outcome)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;
            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var domains = new int[count];
            var kills = new int[count];
            var rings = new int[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                kills[i] = statsList[i].LifeformsKilled;
                rings[i] = statsList[i].SwitchesThreaded;
            }
            int ended = _endedForm >= 0 ? _endedForm : _form.Value;
            int formReached = Mathf.Clamp(ended, 0, Mathf.Max(0, settings.Forms.Count - 1)) + 1;
            SyncFinalScores_ClientRpc(names, scores, domains, kills, rings, new FixedString64Bytes(winnerName), (int)winnerDomain,
                (int)outcome, formReached, settings.Forms.Count, ended);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains, int[] kills, int[] rings,
            FixedString64Bytes winnerName, int winnerDomain, int outcome, int formReached, int formCount, int formEnded)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Tandava] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.LifeformsKilled = kills[i];
                stat.SwitchesThreaded = rings[i];
            }
            rule.Publish((TandavaOutcome)outcome, formReached, formCount, VariantName(formEnded));
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            if (IsServer) DisarmChasers();
            _finalResultsSent = false;
            rule.ResetOutcome();
            foreach (var s in gameData.RoundStatsList)
            {
                s.LifeformsKilled = 0;
                s.SwitchesThreaded = 0;
                s.Score = 0f;
            }
            gameData.InvokeTurnStarted();
        }

        // ───────────────────────────────────────────────────────────── the halo

        /// <summary>Server, at the moment the drum starts: the halo's centre is the dance ground plus the dance plan's halo
        /// offset in the swarm's body axes of that moment, and its plane is the body's (up, side) - the plane the figure
        /// stands in. Replicated once; the rings stand still for the whole drum.</summary>
        void PlaceHalo(SwarmFauna swarm)
        {
            Vector3 fwd = swarm.BodyForward, up = swarm.BodyUp, side = swarm.BodySide;
            var l = Variant(_core.FormIx).HaloCentre;
            _haloUp.Value = up;
            _haloSide.Value = side;
            _haloCentre.Value = V(_core.DancePoint) + fwd * l.x + up * l.y + side * l.z;
            _haloOut.Value = 0;
            _haloGuarded.Value = 0;
            _nextGuardCheck = 0f;
        }

        int HaloCount => settings ? Mathf.Max(1, settings.Director.HaloCount) : 12;

        Vector3 RingDir(int k)
        {
            float th = 2f * Mathf.PI * k / HaloCount;
            return _haloUp.Value * Mathf.Cos(th) + _haloSide.Value * Mathf.Sin(th);
        }

        Vector3 RingTangent(int k)
        {
            float th = 2f * Mathf.PI * k / HaloCount;
            return -_haloUp.Value * Mathf.Sin(th) + _haloSide.Value * Mathf.Cos(th);
        }

        Vector3 RingPos(int k) => _haloCentre.Value + RingDir(k) * settings.HaloRadius;
        Vector3 GuardPost(int k) => _haloCentre.Value + RingDir(k) * settings.GuardPostRadius;

        /// <summary>Server: a ring is GUARDED while enough attendants are at its post - read off the server's own swarm,
        /// the one every other peer's is nudged to.</summary>
        void UpdateGuards(SwarmFauna swarm)
        {
            int element = SwarmFaunaConfigSO.ToIndex(settings.GuardElement), mask = 0;
            for (int k = 0; k < HaloCount; k++)
            {
                if (_core.HaloIsOut(k)) continue;
                if (swarm.CountMembersNear(GuardPost(k), settings.GuardRadius, element) >= settings.GuardMembers) mask |= 1 << k;
            }
            _haloGuarded.Value = mask;
        }

        /// <summary>Every peer, every frame: raise the halo when the drum starts, show the server's broken / guarded masks
        /// (a newly broken ring throws its gold), test the pilots this machine simulates, and strike the halo when the
        /// dance is over.</summary>
        void UpdateHalo()
        {
            bool dance = InDance;
            if (dance && _halo == null && _haloUp.Value.sqrMagnitude > 0.5f) BuildHalo();
            if (_halo == null) return;
            if (!dance) { StrikeHalo(); return; }
            int outMask = _haloOut.Value, guarded = _haloGuarded.Value;
            var theme = gameData ? gameData.ThemeManagerData : null;
            for (int k = 0; k < _halo.Length; k++)
            {
                var r = _halo[k];
                if (!r || r.IsBroken) continue;
                if ((outMask & (1 << k)) != 0)
                {
                    TandavaGoldBurst.Burst(theme, r.transform.position, null, settings.HaloBurstShards, settings.BurstSpeed,
                                           settings.BurstScale, PrismKind.Shielded);
                    Flash(r.transform.position);
                    r.Break(settings.HaloBreakSeconds);
                    continue;
                }
                r.SetGuarded((guarded & (1 << k)) != 0);
            }
            if (gameData != null && gameData.IsTurnRunning) DetectRingCrossings();
        }

        void BuildHalo()
        {
            var theme = gameData ? gameData.ThemeManagerData : null;
            _haloRoot = new GameObject("TandavaHalo");
            _halo = new TandavaHaloRing[HaloCount];
            _lastPilotPos.Clear();
            for (int k = 0; k < _halo.Length; k++)
            {
                var go = new GameObject($"Halo Ring {k}");
                go.transform.SetParent(_haloRoot.transform, false);
                var r = go.AddComponent<TandavaHaloRing>();
                r.Build(k, RingPos(k), RingTangent(k), RingDir(k), settings.HaloMouthRadius, settings.HaloGuardScale,
                        theme, settings.HaloBloomSeconds);
                _halo[k] = r;
            }
        }

        /// <summary>The dance is over (the Antlion taken, or the dance broken): every ring still whole withers.</summary>
        void StrikeHalo()
        {
            for (int k = 0; k < _halo.Length; k++)
                if (_halo[k] && !_halo[k].IsBroken) _halo[k].Break(settings.HaloBreakSeconds);
            _halo = null;
            _lastPilotPos.Clear();
            if (_haloRoot) Destroy(_haloRoot, settings.HaloBreakSeconds + 0.5f);
            _haloRoot = null;
        }

        /// <summary>
        /// Every peer tests ONLY the vessels it simulates (IsNetworkOwner: the host owns every AI) as swept segments - a
        /// fast hull covers more than a mouth per frame - and reports a threaded ring to the server, which judges it
        /// against its own guard state. The GateRaceController pattern.
        /// </summary>
        void DetectRingCrossings()
        {
            float maxStep = settings.MaxPlausibleSpeed * Time.deltaTime * 2f + 5f;
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsNetworkOwner) continue;
                var t = p.Vessel?.Transform;
                if (!t) { _lastPilotPos.Remove(p); continue; }
                Vector3 cur = t.position;
                if (!_lastPilotPos.TryGetValue(p, out var prev)) { _lastPilotPos[p] = cur; continue; }
                _lastPilotPos[p] = cur;
                if ((cur - prev).sqrMagnitude > maxStep * maxStep) continue;   // a respawn or a hitch threads nothing
                for (int k = 0; k < _halo.Length; k++)
                {
                    var r = _halo[k];
                    if (!r || !r.CrossedMouth(prev, cur)) continue;
                    if (IsServer) RingThreadedServer(k, p);
                    else ReportRing_ServerRpc(k);
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportRing_ServerRpc(int ring, ServerRpcParams rpc = default)
        {
            if (!settings || ring < 0 || ring >= HaloCount) return;
            ulong sender = rpc.Receive.SenderClientId;
            IPlayer pilot = null;
            foreach (var p in gameData.Players)
                if (p is Player np && np && np.OwnerClientId == sender && !np.IsInitializedAsAI) { pilot = p; break; }
            // plausibility: the reporter's vessel (this server's replica of it) is near the ring it says it threaded
            var t = pilot?.Vessel?.Transform;
            float reach = settings.HaloMouthRadius + 150f;
            if (!t || (t.position - RingPos(ring)).sqrMagnitude > reach * reach) return;
            RingThreadedServer(ring, pilot);
        }

        /// <summary>Server: a pilot threaded ring <paramref name="ring"/>. It breaks unless its attendants hold it.</summary>
        void RingThreadedServer(int ring, IPlayer pilot)
        {
            if (_core == null || !_core.InDance) return;
            if ((_haloGuarded.Value & (1 << ring)) != 0) return;   // the attendants hold it
            if (!_core.BreakHalo(ring)) return;
            if (pilot?.RoundStats != null) pilot.RoundStats.SwitchesThreaded++;
            if (_swarm) DrainEvents(_swarm);
        }

        bool TryNearestOpenRing(Vector3 from, out TandavaHaloRing best)
        {
            best = null;
            if (_halo == null) return false;
            float bd = float.MaxValue;
            for (int k = 0; k < _halo.Length; k++)
            {
                var r = _halo[k];
                if (!r || r.IsBroken || r.IsGuarded) continue;
                float d = (r.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        static int CountBits(int m) { int n = 0; while (m != 0) { m &= m - 1; n++; } return n; }

        /// <summary>A variant's coil mouths, one per coil plan (a variant authored with the two out of step eats in its
        /// strike pose rather than index past the end).</summary>
        static SVector3[] Coils(in TandavaVariantSpec v)
        {
            int n = v.CoilPlanIndices?.Length ?? 0;
            if (n == 0) return System.Array.Empty<SVector3>();
            if (v.CoilMouths == null || v.CoilMouths.Length != n)
            {
                CSDebug.LogError($"[Tandava] {v.DisplayName}: {n} coil plans but {v.CoilMouths?.Length ?? 0} coil mouths - " +
                                 "re-run Tools/Build/author_tandava_assets.py. It eats in its strike pose.");
                return System.Array.Empty<SVector3>();
            }
            var mouths = new SVector3[n];
            for (int k = 0; k < n; k++) mouths[k] = S(v.CoilMouths[k]);
            return mouths;
        }

        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 V(SVector3 v) => new(v.X, v.Y, v.Z);
    }
}
