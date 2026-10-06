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
    /// of a long cell and races for the exit membrane at the other, stopping at oases to eat; each time its body is full
    /// and its stomach holds the surplus it takes its next FORM (Serpent small, medium, large, then the Bull), and the
    /// CELL changes with it - the membrane, nucleus and cytoplasm ease into the form's colours through a bloom at the
    /// moment of the change. Every pilot flies one domain: they win by wiping the swarm out, starving it (burn the oasis
    /// ahead) or cutting its final form below the break threshold; they lose when it crosses the exit, scored by the
    /// form it escaped as.
    ///
    /// This controller is the swarm's <see cref="ISwarmDirector"/> on EVERY peer (fauna are client-local, the Brood Rush
    /// precedent). The SERVER runs the stage director (<see cref="TandavaDirectorCore"/>, pure C#, proven in
    /// Tools/Build/swarm_core_harness mode `tandava`) on its own swarm and replicates what it decides - the form, the
    /// goal, the anchor, the outcome; a CLIENT's swarm takes the replicated form and goal and is nudged toward the
    /// server's anchor, so every peer chases the same animal in the same place. The cell's colour follows the replicated
    /// form on every peer, so it changes at the same moment everywhere. Scoring is the server's.
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

        TandavaDirectorCore _core;     // server only
        SwarmFauna _swarm;
        float _lastTick = -1f, _nextDenialCheck, _turnStartedAt = -1f;
        bool _finalResultsSent, _cellPrepared, _warnedNoSwarm;

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
        }

        // ───────────────────────────────────────────────────────────── the race

        protected override void OnCountdownTimerEnded()
        {
            if (IsServer) ArmChasers();
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
        }

        /// <summary>
        /// Every AI pilot CHASES the swarm: its steering runs at the swarm's body, led a little toward where the swarm
        /// is going and spread sideways by seat so a grid of bots strikes the flank instead of one point. Steering is
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
                pilot.SetExternalTargetProvider(() =>
                {
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
            // every peer: the cell becomes the first form's cell as the race begins
            if (settings && settings.Forms.Count > 0) TintTo(settings.Forms[Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1)].Palette, true);
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
            var ds = new TandavaDirectorSettings
            {
                ArriveMargin = settings.ArriveMargin, LeaveWhenStomachFill = settings.LeaveWhenStomachFill,
                GiveUpSeconds = settings.GiveUpSeconds, MaxFeedSeconds = settings.MaxFeedSeconds,
                ExitPoint = S(origin + settings.ExitPoint), ExitNormal = S(settings.ExitNormal.normalized),
                BreakFraction = breakPercent / 100f, BreakArmFraction = settings.BreakArmFraction,
            };
            _core = new TandavaDirectorCore(forms, oases, ds);
        }

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

            for (int q = 0; q < _core.Events.Count; q++)
            {
                var e = _core.Events[q];
                switch (e.Kind)
                {
                    case TandavaEventKind.FormCommitted:
                        swarm.RequestForm(settings.Forms[e.B].PlanIndex);
                        _form.Value = e.B;
                        Narrate_ClientRpc((int)GameToastSituation.TandavaFormTaken, new FixedString512Bytes(settings.Forms[e.B].Line));
                        break;
                    case TandavaEventKind.HeadingForExit:
                        Narrate_ClientRpc((int)GameToastSituation.TandavaHeadingForExit, new FixedString512Bytes(settings.HeadingForExitLine));
                        break;
                    case TandavaEventKind.Ended:
                        var outcome = (TandavaOutcome)e.B;
                        rule.Publish(outcome, _core.FormIx + 1, settings.Forms.Count, FormName);
                        _outcome.Value = (int)outcome;   // the turn monitor sees the rule's outcome and ends the turn
                        break;
                }
            }
            _core.Events.Clear();

            _goal.Value = V(_core.Goal);
            _anchor.Value = a;
            _progress.Value = _core.EvolveProgress(state);
        }

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
                return;
            }
            // a client follows the server's swarm: its form, and (gently) its place
            int plan = settings.Forms.Count > 0 ? settings.Forms[Mathf.Clamp(_form.Value, 0, settings.Forms.Count - 1)].PlanIndex : 0;
            if (swarm.FormIndex != plan) swarm.RequestForm(plan);
            Vector3 gap = _anchor.Value - swarm.AnchorWorld;
            float d = gap.magnitude;
            if (d > settings.NudgeThreshold && _anchor.Value != Vector3.zero)
                swarm.TryNudge(gap * (Mathf.Min(settings.MaxNudge, d * settings.NudgeFraction) / d));
        }

        void ISwarmDirector.OnFormCommitted(SwarmFauna swarm, int fromForm, int toForm) { }

        // ───────────────────────────────────────────────────────────── the cell changes with the swarm

        void OnFormChanged(int previous, int current)
        {
            if (!settings || current < 0 || current >= settings.Forms.Count) return;
            TintTo(settings.Forms[current].Palette, true);
        }

        void OnOutcomeChanged(int previous, int current)
        {
            var outcome = (TandavaOutcome)current;
            if (outcome == TandavaOutcome.Running || !TryGetCell(out var cell)) return;
            var tint = CellVisualTint.For(cell);
            if (outcome == TandavaOutcome.Escaped) tint.TransitionTo(settings.EscapedPalette, settings.TransitionSeconds, settings.TransitionFlash);
            else tint.Restore(settings.RestoreSeconds);   // the cell keeps its own colours again
        }

        void TintTo(in CellPalette palette, bool bloom)
        {
            if (!TryGetCell(out var cell)) return;
            CellVisualTint.For(cell).TransitionTo(palette, settings.TransitionSeconds, bloom ? settings.TransitionFlash : 0f);
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
            var toast = TandavaScoringRuleSO.PilotsWon(outcome) ? GameToastSituation.TandavaBroken : GameToastSituation.TandavaEscaped;
            Narrate_ClientRpc((int)toast, new FixedString512Bytes(TandavaScoringRuleSO.PilotsWon(outcome) ? settings.WonLine : settings.EscapedLine));
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
            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                kills[i] = statsList[i].LifeformsKilled;
            }
            int formReached = Mathf.Clamp(_form.Value, 0, Mathf.Max(0, settings.Forms.Count - 1)) + 1;
            SyncFinalScores_ClientRpc(names, scores, domains, kills, new FixedString64Bytes(winnerName), (int)winnerDomain,
                (int)outcome, formReached, settings.Forms.Count, new FixedString64Bytes(FormName));
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(FixedString64Bytes[] names, float[] scores, int[] domains, int[] kills,
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
                s.Score = 0f;
            }
            gameData.InvokeTurnStarted();
        }

        static SVector3 S(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 V(SVector3 v) => new(v.X, v.Y, v.Z);
    }
}
