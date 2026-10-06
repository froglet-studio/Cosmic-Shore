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
    /// Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md) - an ARENA co-op chase. A tadpole swarm hatches at one end
    /// of a long cell and eats its way down a route of oases; each time its body is full and its stomach holds the surplus
    /// it takes its next FORM (Serpent small, medium, large, then the Bull - every form a FLYER: the HyperSea has no
    /// ground). The exit membrane at the far end is SEALED to every form but the last: the banked Bull RISES into the
    /// Lord of the Dance and swims to the dance ground, and the CELL changes for that alone - the membrane, nucleus and
    /// cytoplasm bloom and ease into the dance's colours at the rise, hold them through the dance, and ease back after;
    /// every other form, the cell keeps its own. At the ground a RING OF FIRE (twelve flame switches,
    /// <see cref="TandavaFlame"/>) lights round the figure, its attendant packs patrol it, a drum runs, and when it stops
    /// the swarm takes its FINAL form, the Winged Lion, the one form the exit lets through. Every pilot flies one domain: they win by wiping the swarm out, starving it
    /// (burn its food - with nothing to eat it presses on the sealed membrane and starves), putting out enough of the
    /// ring before the drum stops, or cutting the Winged Lion below its break threshold; they lose when it crosses.
    ///
    /// This controller is the swarm's <see cref="ISwarmDirector"/> on EVERY peer (fauna are client-local, the Brood Rush
    /// precedent). The SERVER runs the stage director (<see cref="TandavaDirectorCore"/>, pure C#, proven in
    /// Tools/Build/swarm_core_harness mode `tandava`) on its own swarm and replicates what it decides - the form, the
    /// phase, the goal, the anchor, the outcome, the ring of fire's placement and its out / guarded masks; a CLIENT's
    /// swarm takes the replicated form and goal and is nudged toward the server's anchor, so every peer chases the same
    /// animal in the same place. Every peer holds its OWN swarm behind the sealed membrane (the same pure rule,
    /// <see cref="TandavaDirectorCore.SealCorrection(SVector3, bool, TandavaDirectorSettings)"/>), draws the same ring
    /// from the replicated placement, and tests the pilots it simulates against it; the server judges every flame.
    /// The cell's colour follows the replicated form on every peer, so it changes at the same moment everywhere.
    /// Scoring is the server's.
    /// </summary>
    public class TandavaController : MultiplayerDomainGamesController, ISwarmDirector
    {
        [Header("Tandava")]
        [Tooltip("Drag TandavaSettings.asset - the route, the forms, the cell's palettes, the narration.")]
        [SerializeField] TandavaSettingsSO settings;
        [Tooltip("Drag TandavaScoringRule.asset - the outcome-based rule (winner, scores, results).")]
        [SerializeField] TandavaScoringRuleSO rule;

        /// <summary>The live Tandava match, if any (the turn monitor's display and the HUD arrow read it).</summary>
        public static TandavaController Current { get; private set; }

        readonly NetworkVariable<int> _form = new(0);
        readonly NetworkVariable<int> _outcome = new((int)TandavaOutcome.Running);
        readonly NetworkVariable<bool> _racing = new(false);
        readonly NetworkVariable<Vector3> _goal = new();
        readonly NetworkVariable<Vector3> _anchor = new();
        readonly NetworkVariable<float> _progress = new(0f);
        // the story, and the ring of fire
        readonly NetworkVariable<int> _phase = new((int)TandavaPhase.Route);
        /// <summary>The dance: the drum's seconds left. Starving at the membrane: the seconds before it has starved. Else -1.</summary>
        readonly NetworkVariable<float> _clock = new(-1f);
        readonly NetworkVariable<Vector3> _ringCentre = new();
        readonly NetworkVariable<Vector3> _ringUp = new();
        readonly NetworkVariable<Vector3> _ringSide = new();
        readonly NetworkVariable<int> _flamesOut = new(0);
        readonly NetworkVariable<int> _flamesGuarded = new(0);
        readonly NetworkVariable<int> _flamesToBreak = new(EndConditionOverridesSO.DefaultTandavaFlamesToBreak);

        TandavaDirectorCore _core;     // server only
        TandavaDirectorSettings _seal; // every peer: the sealed membrane's rule
        SwarmFauna _swarm;
        float _lastTick = -1f, _nextDenialCheck, _nextGuardCheck, _turnStartedAt = -1f;
        int _sealedNarratedForm = -1, _endedForm = -1;
        bool _finalResultsSent, _cellPrepared, _warnedNoSwarm, _tinted;
        // the ring of fire, as this peer draws it
        TandavaFlame[] _flames;
        GameObject _ringRoot;
        readonly Dictionary<IPlayer, Vector3> _lastPilotPos = new();

        protected override bool UseGolfRules => false;
        // flora, the swarm and the cell's trail mass do not reset in place (the Brood Rush / SkimRace precedent)
        protected override bool UseSceneReloadForReplay => true;
        // end-game runs through OnTurnEndedCustom -> SyncFinalScores_ClientRpc
        protected override bool HasEndGame => false;

        public TandavaOutcome Outcome => (TandavaOutcome)_outcome.Value;
        public int FormIndex => _form.Value;
        public bool IsFinalForm => settings && _form.Value >= settings.Forms.Count - 1;
        public string FormName => settings && _form.Value >= 0 && _form.Value < settings.Forms.Count ? settings.Forms[_form.Value].DisplayName : "";
        /// <summary>How close the swarm is to its next form, 0..1 (the lesser of body fill and banked surplus).</summary>
        public float EvolveProgress => _progress.Value;
        /// <summary>The swarm the pilots chase (this peer's), for the HUD arrow.</summary>
        public Transform SwarmTransform => _swarm ? _swarm.transform : null;
        public TandavaPhase Phase => (TandavaPhase)_phase.Value;
        public bool InDance => Phase == TandavaPhase.Dance && Outcome == TandavaOutcome.Running;
        /// <summary>The drum's seconds left in the dance; while starving at the membrane, the seconds before it has
        /// starved; -1 otherwise.</summary>
        public float StoryClock => _clock.Value;
        public int FlamesOut => CountBits(_flamesOut.Value);
        public int FlamesToBreak => _flamesToBreak.Value;

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
            _outcome.OnValueChanged += OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;

            if (settings && settings.SwarmConfig) SwarmFauna.SetDirector(settings.SwarmConfig, this);
            else CSDebug.LogError("[Tandava] TandavaController has no settings / swarm config - the swarm will not be directed.");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) DisarmChasers();
            _form.OnValueChanged -= OnFormChanged;
            _outcome.OnValueChanged -= OnOutcomeChanged;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            if (_ringRoot) Destroy(_ringRoot);
            _flames = null;
            if (settings && settings.SwarmConfig) SwarmFauna.ClearDirector(settings.SwarmConfig, this);
            if (Current == this) Current = null;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            // the one-off tint work (the cytoplasm's material clone) happens while the ready screen is up, never at a form
            if (!_cellPrepared && TryGetCell(out var cell) && cell.CytoplasmVisual)
            {
                CellVisualTint.For(cell).Prepare();
                _cellPrepared = true;
            }
            if (IsServer && _racing.Value && !_swarm && !_warnedNoSwarm && _turnStartedAt >= 0f && Time.time - _turnStartedAt > 30f)
            {
                _warnedNoSwarm = true;
                CSDebug.LogError("[Tandava] The race has run 30 s with no swarm: is the Tandava swarm config in the cell's spawn profile, " +
                                 "and does it name this settings' SwarmConfig? The match cannot end without one.");
            }
            if (settings) UpdateRing();
        }

        // ───────────────────────────────────────────────────────────── the race

        protected override void OnCountdownTimerEnded()
        {
            if (IsServer) ArmChasers();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
        }

        /// <summary>
        /// Every AI pilot CHASES the swarm: its steering runs at the swarm's body, led a little toward where the swarm
        /// is going and spread sideways by seat so a grid of bots strikes the flank instead of one point. During the DANCE
        /// it flies at the ring of fire instead - the nearest flame that is lit and unguarded, aimed at a point just past it
        /// along the flame's axis so the line it flies threads the mouth. Steering is
        /// <see cref="AIPilot.SetExternalTargetProvider"/> and nothing else, so it cannot leak into another mode. Honest
        /// limit (the Tollway rule, restated for speed): the Squirrel's and Sparrow's autopilots fly at CRUISE (60 / 35 u/s)
        /// and the swarm cruises at 50 - an AI Sparrow cannot keep up; only an AI Rhino ramps. An all-AI lobby cannot win.
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
                    if (InDance && self && TryNearestOpenFlame(self.position, out var flame))
                    {
                        Vector3 fp = flame.transform.position;
                        float from = Mathf.Sign(Vector3.Dot(self.position - fp, flame.Axis));
                        return fp - flame.Axis * ((from == 0f ? 1f : from) * 30f);
                    }
                    if (!_swarm) return Origin + settings.StartPoint;
                    Vector3 at = _swarm.AnchorWorld;
                    Vector3 goal = IsServer && _core != null ? V(_core.Goal) : _goal.Value;
                    Vector3 ahead = goal - at;
                    Vector3 dir = ahead.sqrMagnitude > 1f ? ahead.normalized : Vector3.right;
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
            Narrate_ClientRpc((int)GameToastSituation.TandavaMatchStart, new FixedString512Bytes(settings.StartLine));
        }

        bool TryGetCell(out Cell cell)
        {
            cell = null;
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count && !cell; i++) cell = cells[i];
            return cell;
        }

        Vector3 Origin => TryGetCell(out var cell) ? cell.transform.position : Vector3.zero;

        void BuildCore(SwarmFauna swarm)
        {
            var forms = new List<TandavaForm>(settings.Forms.Count);
            for (int k = 0; k < settings.Forms.Count; k++)
            {
                var spec = settings.Forms[k];
                var f = new TandavaForm
                {
                    Name = spec.DisplayName, PlanIndex = spec.PlanIndex,
                    PlanCount = swarm.FormMemberCount(spec.PlanIndex), FillToEvolve = spec.FillToEvolve,
                    MealVolume = spec.MealVolume,
                };
                f.BankToEvolve[0] = spec.BankToEvolve.x; f.BankToEvolve[1] = spec.BankToEvolve.y;
                f.BankToEvolve[2] = spec.BankToEvolve.z; f.BankToEvolve[3] = spec.BankToEvolve.w;
                forms.Add(f);
            }
            var origin = Origin;
            var oases = settings.Oases.Select(o => new TandavaOasis { Centre = S(origin + o.Centre), Radius = o.Radius }).ToArray();
            var overrides = EndConditionOverridesSO.Instance;
            int breakPercent = overrides ? overrides.GetTandavaBreakPercent() : EndConditionOverridesSO.DefaultTandavaBreakPercent;
            int toBreak = Mathf.Clamp(overrides ? overrides.GetTandavaFlamesToBreak() : EndConditionOverridesSO.DefaultTandavaFlamesToBreak,
                                      1, settings.FlameCount);
            _flamesToBreak.Value = toBreak;
            var ds = SealSettings(origin);
            ds.ArriveMargin = settings.ArriveMargin; ds.LeaveWhenStomachFill = settings.LeaveWhenStomachFill;
            ds.GiveUpSeconds = settings.GiveUpSeconds; ds.MaxFeedSeconds = settings.MaxFeedSeconds;
            ds.BreakFraction = breakPercent / 100f; ds.BreakArmFraction = settings.BreakArmFraction;
            ds.StarveStandoff = settings.StarveStandoff; ds.StarvingSeconds = settings.StarvingSeconds;
            ds.StarvedBelowFraction = settings.StarvedBelowFraction;
            ds.Ascension = true; ds.DancePoint = S(origin + settings.DancePoint); ds.DanceArrive = settings.DanceArrive;
            ds.DrumSeconds = settings.DrumSeconds; ds.FlameCount = settings.FlameCount; ds.FlamesToBreak = toBreak;
            _core = new TandavaDirectorCore(forms, oases, ds);
        }

        /// <summary>The sealed membrane's rule (every peer builds the same one from the settings and the cell).</summary>
        TandavaDirectorSettings SealSettings(Vector3 origin) => new()
        {
            ExitPoint = S(origin + settings.ExitPoint), ExitNormal = S(settings.ExitNormal.normalized),
            SealMargin = settings.SealMargin, SealHold = settings.SealHold,
        };

        void TickCore(SwarmFauna swarm)
        {
            float now = Time.time;
            float dt = _lastTick < 0f ? 0f : now - _lastTick;
            _lastTick = now;

            if (now >= _nextDenialCheck)
            {
                _nextDenialCheck = now + settings.DenialCheckSeconds;
                var origin = Origin;
                for (int o = 0; o < settings.Oases.Count; o++)
                {
                    var centre = origin + settings.Oases[o].Centre;
                    float reach = settings.Oases[o].Radius + settings.ArriveMargin;
                    // an oasis is DENIED when no flora the swarm can eat is left in it - the pilots burned it out
                    var plant = FloraHeartRegistry.NearestToPoint(centre, f => !swarm.CanEat(f));
                    _core.SetDenied(o, !plant || (plant.HeartTransform.position - centre).sqrMagnitude > reach * reach);
                }
            }

            var a = swarm.AnchorWorld;
            var state = new TandavaSwarmState
            {
                Alive = swarm.MemberCount, Anchor = S(a),
                Stomach0 = swarm.StomachVolume(0), Stomach1 = swarm.StomachVolume(1),
                Stomach2 = swarm.StomachVolume(2), Stomach3 = swarm.StomachVolume(3),
                StomachFill = swarm.StomachFraction, SinceBite = swarm.SecondsSinceBite,
                EatenTotal = swarm.EatenTotal, Starving = swarm.IsStarving,
            };
            _core.Tick(dt, state);
            DrainEvents(swarm);

            if (_core.InDance && now >= _nextGuardCheck)
            {
                _nextGuardCheck = now + 0.25f;
                UpdateGuards(swarm);
            }
            _phase.Value = (int)_core.Phase;
            _clock.Value = _core.InDance ? _core.DrumRemaining : _core.StarveRemaining;
            _goal.Value = V(_core.Goal);
            _anchor.Value = a;
            _progress.Value = _core.EvolveProgress(state);
        }

        /// <summary>Apply what the director decided this tick (or what a threaded flame just ended). Server only.</summary>
        void DrainEvents(SwarmFauna swarm)
        {
            for (int q = 0; q < _core.Events.Count; q++)
            {
                var e = _core.Events[q];
                switch (e.Kind)
                {
                    case TandavaEventKind.FormCommitted:
                        swarm.RequestForm(settings.Forms[e.B].PlanIndex);
                        _form.Value = e.B;
                        Narrate(GameToastSituation.TandavaFormTaken, settings.Forms[e.B].Line);
                        break;
                    case TandavaEventKind.RouteEaten:
                        Narrate(GameToastSituation.TandavaHeadingForExit, settings.HeadingForExitLine);
                        break;
                    case TandavaEventKind.Starving:
                        Narrate(GameToastSituation.TandavaStarving, settings.StarvingLine);
                        break;
                    case TandavaEventKind.ReadyToDance:
                        // the banked Bull rises into the dance form now and assembles it on the way to the dance ground
                        swarm.RequestForm(settings.Forms[e.B].PlanIndex);
                        _form.Value = e.B;
                        Narrate(GameToastSituation.TandavaReadyToDance, settings.ReadyToDanceLine);
                        break;
                    case TandavaEventKind.AscensionBegun:
                        PlaceRing(swarm);   // before the phase replicates, so a peer that sees Dance has the ring's place
                        Narrate(GameToastSituation.TandavaFormTaken, settings.Forms[e.B].Line);
                        break;
                    case TandavaEventKind.Sealed:
                        // the membrane holds: said once per form, not every time it is pressed
                        if (_sealedNarratedForm == _core.FormIx) break;
                        _sealedNarratedForm = _core.FormIx;
                        Narrate(GameToastSituation.TandavaSealed, settings.SealedLine);
                        break;
                    case TandavaEventKind.FlameOut:
                        _flamesOut.Value = _core.FlamesOutMask;
                        if (e.B == 1 || e.B == _core.S.FlamesToBreak - 1)
                            Narrate(GameToastSituation.TandavaFlameOut,
                                (e.B == 1 ? settings.FirstFlameLine : settings.LastFlameLine)
                                .Replace("{0}", e.B.ToString()).Replace("{1}", _core.S.FlamesToBreak.ToString()));
                        break;
                    case TandavaEventKind.Ended:
                        var outcome = (TandavaOutcome)e.B;
                        _endedForm = _core.FormIx;
                        rule.Publish(outcome, _core.FormIx + 1, settings.Forms.Count, FormNameOf(_core.FormIx));
                        // a broken dance kills nobody: the figure falls back into the last beast it ate its way to
                        if (outcome == TandavaOutcome.DanceBroken && _core.LastEaterIx >= 0)
                        {
                            swarm.RequestForm(settings.Forms[_core.LastEaterIx].PlanIndex);
                            _form.Value = _core.LastEaterIx;
                        }
                        _phase.Value = (int)TandavaPhase.Over;
                        _outcome.Value = (int)outcome;   // the turn monitor sees the rule's outcome and ends the turn
                        break;
                }
            }
            _core.Events.Clear();
        }

        string FormNameOf(int form) => settings && form >= 0 && form < settings.Forms.Count ? settings.Forms[form].DisplayName : "";

        void Narrate(GameToastSituation situation, string line) =>
            Narrate_ClientRpc((int)situation, new FixedString512Bytes(line ?? ""));

        // ───────────────────────────────────────────────────────────── ISwarmDirector (every peer)

        bool ISwarmDirector.TryGetSeed(SwarmFauna swarm, out Vector3 position, out Vector3 heading)
        {
            _swarm = swarm;
            position = Origin + settings.StartPoint;
            heading = settings.StartHeading.sqrMagnitude > 1e-6f ? settings.StartHeading.normalized : Vector3.right;
            return true;
        }

        bool ISwarmDirector.TryGetGoal(SwarmFauna swarm, out Vector3 goal)
        {
            // before the GO the swarm holds at its end of the course; after it, the server's route (a client reads the
            // replicated goal, and holds at the start until the first one arrives)
            goal = Origin + settings.StartPoint;
            if (!_racing.Value) return true;
            if (IsServer) { if (_core != null) goal = V(_core.Goal); }
            else if (_goal.Value != Vector3.zero) goal = _goal.Value;
            return true;
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
            else
            {
                // a client follows the server's swarm: its form, and (gently) its place
                int plan = settings.Forms.Count > 0 ? settings.Forms[Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1)].PlanIndex : 0;
                if (swarm.FormIndex != plan) swarm.RequestForm(plan);
                Vector3 gap = _anchor.Value - swarm.AnchorWorld;
                float d = gap.magnitude;
                if (d > settings.NudgeThreshold && _anchor.Value != Vector3.zero)
                    swarm.TryNudge(gap * (Mathf.Min(settings.MaxNudge, d * settings.NudgeFraction) / d));
            }
            HoldSeal(swarm);
        }

        /// <summary>
        /// The sealed exit, on EVERY peer's own swarm: a form that is not the final one is pushed back inside the membrane
        /// (a small rigid nudge, the membrane holding - never a kill). The rule is the director's pure one, fed the
        /// replicated form, so the server and every client hold their swarms at the same place.
        /// </summary>
        void HoldSeal(SwarmFauna swarm)
        {
            _seal ??= SealSettings(Origin);
            var push = TandavaDirectorCore.SealCorrection(S(swarm.AnchorWorld), IsFinalForm, _seal);
            if (push != SVector3.Zero) swarm.TryNudge(V(push));
        }

        void ISwarmDirector.OnFormCommitted(SwarmFauna swarm, int fromForm, int toForm) { }

        // ───────────────────────────────────────────────────────────── the cell changes for the dance alone

        /// <summary>The dance form (the last but one), or -1 without one.</summary>
        int DanceForm => settings && settings.Forms.Count >= 3 ? settings.Forms.Count - 2 : -1;

        /// <summary>
        /// Every peer, on the replicated form: the cell keeps its OWN colours for every form but the dance. The moment
        /// the Bull rises into the Lord of the Dance it blooms and eases into the dance's palette, holds it while the
        /// figure stands, and eases back to its own colours when the dance ends (the Winged Lion, or the Bull again).
        /// </summary>
        void OnFormChanged(int previous, int current)
        {
            if (!settings || Outcome != TandavaOutcome.Running) return;
            if (current == DanceForm && DanceForm >= 0) TintTo(settings.AscensionPalette);
            else if (previous == DanceForm) RestoreCell();
        }

        void OnOutcomeChanged(int previous, int current)
        {
            if ((TandavaOutcome)current != TandavaOutcome.Running) RestoreCell();   // the cell keeps its own colours
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

        [ClientRpc]
        void Narrate_ClientRpc(int situation, FixedString512Bytes line)
        {
            GameToastAPI.Post((GameToastSituation)situation, Domains.Blue, line.ToString());
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
            Narrate(won ? GameToastSituation.TandavaBroken : GameToastSituation.TandavaEscaped,
                    !won ? settings.EscapedLine : outcome == TandavaOutcome.DanceBroken ? settings.DanceBrokenLine : settings.WonLine);
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
            var flames = new int[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                kills[i] = statsList[i].LifeformsKilled;
                flames[i] = statsList[i].SwitchesThreaded;
            }
            int ended = _endedForm >= 0 ? _endedForm : _form.Value;
            int formReached = Mathf.Clamp(ended, 0, Mathf.Max(0, settings.Forms.Count - 1)) + 1;
            SyncFinalScores_ClientRpc(names, scores, domains, kills, flames, new FixedString64Bytes(winnerName), (int)winnerDomain,
                (int)outcome, formReached, settings.Forms.Count, new FixedString64Bytes(FormNameOf(ended)));
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains, int[] kills, int[] flames,
            FixedString64Bytes winnerName, int winnerDomain, int outcome, int formReached, int formCount, FixedString64Bytes formName)
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
                stat.SwitchesThreaded = flames[i];
            }
            rule.Publish((TandavaOutcome)outcome, formReached, formCount, formName.ToString());
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

        // ───────────────────────────────────────────────────────────── the ring of fire

        /// <summary>Server, at the moment the ascension begins: the ring's centre is the dance ground plus the dance plan's
        /// ring offset in the swarm's body axes of that moment, and its plane is the body's (up, side) - the plane the
        /// statue stands in. Replicated once; the flames stand still for the whole drum.</summary>
        void PlaceRing(SwarmFauna swarm)
        {
            Vector3 fwd = swarm.BodyForward, up = swarm.BodyUp, side = swarm.BodySide;
            var l = settings.FlameRingCentreLocal;
            _ringUp.Value = up;
            _ringSide.Value = side;
            _ringCentre.Value = Origin + settings.DancePoint + fwd * l.x + up * l.y + side * l.z;
            _flamesOut.Value = 0;
            _flamesGuarded.Value = 0;
            _nextGuardCheck = 0f;
        }

        Vector3 FlameDir(int k)
        {
            float th = 2f * Mathf.PI * k / Mathf.Max(1, settings.FlameCount);
            return _ringUp.Value * Mathf.Cos(th) + _ringSide.Value * Mathf.Sin(th);
        }

        Vector3 FlameTangent(int k)
        {
            float th = 2f * Mathf.PI * k / Mathf.Max(1, settings.FlameCount);
            return -_ringUp.Value * Mathf.Sin(th) + _ringSide.Value * Mathf.Cos(th);
        }

        Vector3 FlamePos(int k) => _ringCentre.Value + FlameDir(k) * settings.FlameRingRadius;
        Vector3 GuardPost(int k) => _ringCentre.Value + FlameDir(k) * settings.GuardPostRadius;

        /// <summary>Server: a flame is GUARDED while enough attendants are at its guard post - read off the server's own
        /// swarm, the one every other peer's is nudged to.</summary>
        void UpdateGuards(SwarmFauna swarm)
        {
            int element = SwarmFaunaConfigSO.ToIndex(settings.GuardElement), mask = 0;
            for (int k = 0; k < settings.FlameCount; k++)
            {
                if (_core.FlameIsOut(k)) continue;
                if (swarm.CountMembersNear(GuardPost(k), settings.GuardRadius, element) >= settings.GuardMembers) mask |= 1 << k;
            }
            _flamesGuarded.Value = mask;
        }

        /// <summary>Every peer, every frame: raise the ring when the dance begins, show the server's out / guarded masks,
        /// test the pilots this machine simulates, and strike the ring when the dance is over.</summary>
        void UpdateRing()
        {
            bool dance = InDance;
            if (dance && _flames == null && _ringUp.Value.sqrMagnitude > 0.5f) BuildRing();
            if (_flames == null) return;
            if (!dance) { StrikeRing(); return; }
            int outMask = _flamesOut.Value, guarded = _flamesGuarded.Value;
            for (int k = 0; k < _flames.Length; k++)
            {
                var f = _flames[k];
                if (!f || f.IsOut) continue;
                if ((outMask & (1 << k)) != 0) { f.PutOut(settings.FlameOutSeconds); continue; }
                f.SetGuarded((guarded & (1 << k)) != 0);
            }
            if (gameData != null && gameData.IsTurnRunning) DetectFlameCrossings();
        }

        void BuildRing()
        {
            var theme = gameData ? gameData.ThemeManagerData : null;
            _ringRoot = new GameObject("TandavaRingOfFire");
            _flames = new TandavaFlame[settings.FlameCount];
            _lastPilotPos.Clear();
            for (int k = 0; k < _flames.Length; k++)
            {
                var go = new GameObject($"Flame {k}");
                go.transform.SetParent(_ringRoot.transform, false);
                var f = go.AddComponent<TandavaFlame>();
                f.Build(k, FlamePos(k), FlameTangent(k), FlameDir(k), settings.FlameMouthRadius, settings.FlameGutter,
                        theme, settings.FlameBloomSeconds);
                _flames[k] = f;
            }
        }

        /// <summary>The dance is over (the final form taken, or the dance broken): every flame still burning withers.</summary>
        void StrikeRing()
        {
            for (int k = 0; k < _flames.Length; k++)
                if (_flames[k] && !_flames[k].IsOut) _flames[k].PutOut(settings.FlameOutSeconds);
            _flames = null;
            _lastPilotPos.Clear();
            if (_ringRoot) Destroy(_ringRoot, settings.FlameOutSeconds + 0.5f);
            _ringRoot = null;
        }

        /// <summary>
        /// Every peer tests ONLY the vessels it simulates (IsNetworkOwner: the host owns every AI) as swept segments - a
        /// fast hull covers more than a mouth per frame - and reports a threaded flame to the server, which judges it
        /// against its own guard state. The GateRaceController pattern.
        /// </summary>
        void DetectFlameCrossings()
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
                for (int k = 0; k < _flames.Length; k++)
                {
                    var f = _flames[k];
                    if (!f || !f.CrossedMouth(prev, cur)) continue;
                    if (IsServer) FlameThreadedServer(k, p);
                    else ReportFlame_ServerRpc(k);
                    break;
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void ReportFlame_ServerRpc(int flame, ServerRpcParams rpc = default)
        {
            if (!settings || flame < 0 || flame >= settings.FlameCount) return;
            ulong sender = rpc.Receive.SenderClientId;
            IPlayer pilot = null;
            foreach (var p in gameData.Players)
                if (p is Player np && np && np.OwnerClientId == sender && !np.IsInitializedAsAI) { pilot = p; break; }
            // plausibility: the reporter's vessel (this server's replica of it) is near the flame it says it threaded
            var t = pilot?.Vessel?.Transform;
            float reach = settings.FlameMouthRadius + 150f;
            if (!t || (t.position - FlamePos(flame)).sqrMagnitude > reach * reach) return;
            FlameThreadedServer(flame, pilot);
        }

        /// <summary>Server: a pilot threaded flame <paramref name="flame"/>. It goes out unless its attendants hold it.</summary>
        void FlameThreadedServer(int flame, IPlayer pilot)
        {
            if (_core == null || !_core.InDance) return;
            if ((_flamesGuarded.Value & (1 << flame)) != 0) return;   // the attendants hold it
            if (!_core.BreakFlame(flame)) return;
            if (pilot?.RoundStats != null) pilot.RoundStats.SwitchesThreaded++;
            if (_swarm) DrainEvents(_swarm);
        }

        bool TryNearestOpenFlame(Vector3 from, out TandavaFlame best)
        {
            best = null;
            if (_flames == null) return false;
            float bd = float.MaxValue;
            for (int k = 0; k < _flames.Length; k++)
            {
                var f = _flames[k];
                if (!f || f.IsOut || f.IsGuarded) continue;
                float d = (f.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        static int CountBits(int m) { int n = 0; while (m != 0) { m &= m - 1; n++; } return n; }

        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 V(SVector3 v) => new(v.X, v.Y, v.Z);
    }
}
