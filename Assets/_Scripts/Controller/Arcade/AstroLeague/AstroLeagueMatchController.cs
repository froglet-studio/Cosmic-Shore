using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Astro League match director — Rocket League in the HyperSea, flown with
    /// Squirrel vessels. Orchestrates the full match loop on top of the shared
    /// mini-game flow:
    ///
    ///   Ready → countdown kickoff → live play → GOAL! celebration (slow-mo) →
    ///   kickoff count-in → ... → full time → golden-goal overtime if tied →
    ///   winner banner → shared end-game scoreboard.
    ///
    /// Also assigns domains for solo play (human = Jade, AI = Ruby), parks vessels
    /// at team spawns for each kickoff, and gives every AI a striker brain via
    /// AIPilot.SetExternalTargetProvider.
    /// </summary>
    public class AstroLeagueMatchController : SinglePlayerMiniGameControllerBase
    {
        [Header("Astro League")]
        [SerializeField] AstroLeagueSettingsSO settings;
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] AstroLeagueScoreManager scoreManager;
        [SerializeField] AstroLeagueMatchMonitor matchMonitor;
        [SerializeField] AstroLeagueGoal jadeGoal;
        [SerializeField] AstroLeagueGoal rubyGoal;
        [SerializeField] Transform jadeSpawn;
        [SerializeField] Transform rubySpawn;

        [Inject] AudioSystem audioSystem;

        enum MatchPhase { PreMatch, Kickoff, Live, Celebration, Overtime, Finished }
        MatchPhase phase = MatchPhase.PreMatch;

        CancellationTokenSource matchCts;
        float baseFixedDeltaTime;

        protected override bool HasEndGame => true;
        protected override bool ShowEndGameSequence => true;
        protected override bool ShouldResetPlayersOnTurnEnd => false;

        public bool IsOvertime => phase == MatchPhase.Overtime;

        // ── Announcer feed (consumed by AstroLeagueMatchUI) ──────────────────
        public event Action<int> OnKickoffCount;            // 3, 2, 1
        public event Action OnKickoffGo;
        public event Action<Domains, int, int> OnGoalAnnounced;
        public event Action OnOvertimeAnnounced;
        public event Action<Domains, int, int> OnMatchFinished;

        protected override void Start()
        {
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            matchCts = new CancellationTokenSource();
            baseFixedDeltaTime = Time.fixedDeltaTime;

            if (gameData == null)
            {
                CSDebug.LogError("GameDataSO was not injected!", this);
                return;
            }

            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;

            if (scoreManager != null)
                scoreManager.OnGoalRecorded += HandleGoalRecorded;
            if (matchMonitor != null)
                matchMonitor.OnClockExpired += HandleClockExpired;

            // Deliberately NOT calling base.Start(): it raises OnInitializeGame
            // synchronously, racing the spawner adapter's own Start subscription.
            // Defer one frame so every scene Start has run before players spawn.
            InitializeMatchAsync().Forget();
        }

        async UniTaskVoid InitializeMatchAsync()
        {
            try { await UniTask.Yield(matchCts.Token); }
            catch (OperationCanceledException) { return; }

            gameData.InitializeGame();   // → spawner adapter spawns human + AI
            gameData.InvokeClientReady();
            SetupNewRound();

            AssignTeams();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (scoreManager != null)
                scoreManager.OnGoalRecorded -= HandleGoalRecorded;
            if (matchMonitor != null)
                matchMonitor.OnClockExpired -= HandleClockExpired;

            matchCts?.Cancel();
            matchCts?.Dispose();
            matchCts = null;
        }

        // ── Team & AI setup ──────────────────────────────────────────────────

        /// <summary>
        /// Solo-play team assignment: humans defend Jade (-Z), AI defends Ruby (+Z).
        /// Mirrors RoundStats.Domain so scoring and the end-game scoreboard stay
        /// domain-correct, parks everyone at their team spawn, and arms AI strikers.
        /// </summary>
        void AssignTeams()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null) continue;

                var domain = p.IsInitializedAsAI ? Domains.Ruby : Domains.Jade;

                if (p is Player concretePlayer)
                    concretePlayer.SetDomain(domain);
                if (p.RoundStats != null)
                    p.RoundStats.Domain = domain;

                ParkAtTeamSpawn(p);

                if (p.IsInitializedAsAI)
                    ArmStriker(p);
            }
        }

        void ParkAtTeamSpawn(IPlayer p)
        {
            var spawn = p.RoundStats != null && p.RoundStats.Domain == Domains.Ruby ? rubySpawn : jadeSpawn;
            if (spawn == null || p.Vessel == null) return;
            p.SetPoseOfVessel(new Pose(spawn.position, spawn.rotation));
        }

        /// <summary>
        /// Gives an AI player a striker brain: approach the ball from the own-goal
        /// side so contact drives it toward the enemy goal; swing wide to recover
        /// when caught on the wrong side of the play.
        /// </summary>
        void ArmStriker(IPlayer aiPlayer)
        {
            var pilot = aiPlayer.Vessel?.VesselStatus?.AIPilot;
            if (pilot == null || ball == null) return;

            pilot.SetExternalTargetProvider(() => ComputeStrikerTarget(aiPlayer));
        }

        Vector3 ComputeStrikerTarget(IPlayer aiPlayer)
        {
            if (aiPlayer?.Vessel == null || ball == null || settings == null)
                return ball != null ? ball.transform.position : Vector3.zero;

            Vector3 ballPos = ball.transform.position;

            // During kickoffs/celebrations, orbit midfield on our own side.
            if (ball.IsFrozen || phase != MatchPhase.Live && phase != MatchPhase.Overtime)
            {
                var holdSpawn = aiPlayer.RoundStats != null && aiPlayer.RoundStats.Domain == Domains.Ruby
                    ? rubySpawn : jadeSpawn;
                return holdSpawn != null ? holdSpawn.position : ballPos;
            }

            var myDomain = aiPlayer.RoundStats != null ? aiPlayer.RoundStats.Domain : Domains.Ruby;
            var targetGoal = GoalScoredByDomain(myDomain);
            if (targetGoal == null) return ballPos;

            Vector3 shotDir = (targetGoal.MouthCenter - ballPos).normalized;
            Vector3 approachPoint = ballPos - shotDir * settings.strikerApproachLead;

            Vector3 aiPos = aiPlayer.Vessel.Transform.position;
            bool onAttackSide = Vector3.Dot(ballPos - aiPos, shotDir) > 0f;
            if (onAttackSide)
                return approachPoint;

            // Wrong side of the ball: swing wide around it back toward our half.
            Vector3 side = Vector3.Cross(shotDir, Vector3.up).normalized;
            float sideSign = Mathf.Sign(Vector3.Dot(aiPos - ballPos, side));
            if (sideSign == 0f) sideSign = 1f;
            return ballPos - shotDir * settings.strikerRecoverDistance
                          + side * (sideSign * settings.strikerRecoverDistance * 0.6f);
        }

        AstroLeagueGoal GoalScoredByDomain(Domains domain)
        {
            if (jadeGoal != null && jadeGoal.ScoringDomain == domain) return jadeGoal;
            if (rubyGoal != null && rubyGoal.ScoringDomain == domain) return rubyGoal;
            return null;
        }

        // ── Match flow ───────────────────────────────────────────────────────

        protected override void SetupNewTurn()
        {
            phase = MatchPhase.PreMatch;
            scoreManager?.ResetScores();
            matchMonitor?.ConfigureDuration(settings != null ? settings.matchDurationSeconds : 180f);
            ball?.ResetToCenter(); // Frozen showpiece at center until the kickoff
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewTurn();
        }

        protected override void OnCountdownTimerEnded()
        {
            // The shared 3-2-1 canvas countdown was the first kickoff count-in:
            // players go live and the clock starts the moment it hits GO.
            foreach (var p in gameData.Players)
                ParkAtTeamSpawn(p);

            base.OnCountdownTimerEnded(); // SetPlayersActive + StartTurn (clock starts)

            phase = MatchPhase.Live;
            ball?.SetFrozen(false);
            OnKickoffGo?.Invoke();
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);
        }

        void HandleGoalRecorded(Domains scoringDomain, int jadeGoals, int rubyGoals)
        {
            if (phase == MatchPhase.Finished) return;

            OnGoalAnnounced?.Invoke(scoringDomain, jadeGoals, rubyGoals);
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ScoreReveal);

            bool goldenGoal = phase == MatchPhase.Overtime;
            bool mercy = settings != null && Mathf.Max(jadeGoals, rubyGoals) >= settings.goalLimit;

            if (goldenGoal || mercy)
                FinishMatchAsync(jadeGoals, rubyGoals).Forget();
            else
                CelebrateThenKickoffAsync().Forget();
        }

        async UniTaskVoid CelebrateThenKickoffAsync()
        {
            if (matchCts == null) return;
            var token = matchCts.Token;
            phase = MatchPhase.Celebration;
            matchMonitor?.SetClockPaused(true);

            // Slow-mo celebration beat (ball already detonated + camera shaken).
            // Restore to known constants, not captured values — the ball's hitstop
            // can interleave with this window and captured values would re-apply a
            // stale timescale.
            float celebrationScale = settings != null ? settings.celebrationTimeScale : 0.35f;
            float celebrationSeconds = settings != null ? settings.celebrationSeconds : 2.2f;
            Time.timeScale = celebrationScale;
            Time.fixedDeltaTime = baseFixedDeltaTime * celebrationScale;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(celebrationSeconds),
                    ignoreTimeScale: true, cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                return; // Scene teardown mid-celebration
            }
            finally
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDeltaTime;
            }

            if (phase == MatchPhase.Finished) return;

            try
            {
                await RunKickoffAsync(token);
            }
            catch (OperationCanceledException) { /* teardown mid-kickoff */ }
        }

        async UniTask RunKickoffAsync(CancellationToken token)
        {
            phase = MatchPhase.Kickoff;
            ball?.ResetToCenter();

            foreach (var p in gameData.Players)
                ParkAtTeamSpawn(p);

            // Count-in: 3 — 2 — 1 — GO, spread across the kickoff freeze window.
            float step = (settings != null ? settings.kickoffFreezeSeconds : 2.4f) / 3f;
            for (int count = 3; count >= 1; count--)
            {
                OnKickoffCount?.Invoke(count);
                await UniTask.Delay(TimeSpan.FromSeconds(step), cancellationToken: token);
            }

            if (phase == MatchPhase.Finished) return;
            phase = MatchPhase.Live;

            ball?.SetFrozen(false);
            matchMonitor?.SetClockPaused(false);
            OnKickoffGo?.Invoke();
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.SpeedBurst);
        }

        void HandleClockExpired()
        {
            if (phase == MatchPhase.Finished) return;

            bool tied = JadeRubyTied();
            if (tied && settings != null && settings.goldenGoalOvertime)
            {
                phase = MatchPhase.Overtime;
                matchMonitor?.EnterOvertime();
                OnOvertimeAnnounced?.Invoke();
                audioSystem?.PlayGameplaySFX(GameplaySFXCategory.ComebackCharge);
                return;
            }

            FinishMatchAsync(scoreManager?.JadeGoals ?? 0, scoreManager?.RubyGoals ?? 0).Forget();
        }

        async UniTaskVoid FinishMatchAsync(int jadeGoals, int rubyGoals)
        {
            if (phase == MatchPhase.Finished) return;
            phase = MatchPhase.Finished;

            var winner = jadeGoals >= rubyGoals ? Domains.Jade : Domains.Ruby;
            OnMatchFinished?.Invoke(winner, jadeGoals, rubyGoals);
            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.GameEnd);

            foreach (var p in gameData.Players)
            {
                if (p.IsInitializedAsAI)
                    p.Vessel?.VesselStatus?.AIPilot?.ClearExternalTargetProvider();
            }

            // Let the winner banner land before handing off to the scoreboard flow.
            if (matchCts != null)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(2f), ignoreTimeScale: true,
                        cancellationToken: matchCts.Token);
                }
                catch (OperationCanceledException) { return; }
            }

            matchMonitor?.ForceEnd(); // → turn end → round end → shared end-game sequence
        }

        bool JadeRubyTied() =>
            scoreManager != null && scoreManager.JadeGoals == scoreManager.RubyGoals;

        // ── Replay ───────────────────────────────────────────────────────────

        protected override void OnResetForReplay()
        {
            ResetEnvironmentForReplay();
            AssignTeams(); // Re-park vessels and re-arm strikers for the rematch
            base.OnResetForReplay();
        }

        protected override void ResetEnvironmentForReplay()
        {
            phase = MatchPhase.PreMatch;
            scoreManager?.ResetScores();
            ball?.ResetToCenter();
            matchMonitor?.ResetMonitor();
            matchMonitor?.ConfigureDuration(settings != null ? settings.matchDurationSeconds : 180f);
        }
    }
}
