using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage - the destructive analog of Crystal Capture ("Scurry"): every domain races to
    /// destroy the prism target first (hostile prisms only - another domain's mass; shattering
    /// your own trail is worthless). Structural clone of
    /// <see cref="MultiplayerCrystalCaptureController"/>: 1 round / 1 turn, server-authoritative
    /// winner detection in OnTurnEndedCustom, final results replicated by the shared
    /// <see cref="MultiplayerDomainGamesController.SyncFinalResults"/> template.
    /// The destruction stat itself auto-increments via StatsManager.PrismDestroyed (SOAP
    /// block-destroyed channel), so no per-event listener is needed here.
    /// </summary>
    public class RampageController : MultiplayerDomainGamesController
    {
        // The `rule` field lives on MultiplayerDomainGamesController (hoisted in Y1.2) -
        // declaring it again here would shadow the base field and leave the scene's single
        // serialized `rule:` key binding ambiguously. Drag RampageScoringRule.asset onto the
        // inherited Scoring slot; the base publishes it to gameData.ScoringRule on spawn.

        [Header("AI")]
        [Tooltip("The arena cell whose density grids the AI hunts - each AI's target is the " +
                 "densest region of mass HOSTILE to its domain (the same query aggression-1 " +
                 "fauna use), so the Rhino's ram scores on contact.")]
        [SerializeField] Cell arenaCell;
        [Tooltip("Seconds between AI mass-target refreshes. FindDensestRegion runs a Burst job, " +
                 "so pilots sample on this cadence and fly at the cached point between samples.")]
        [SerializeField, Min(0.25f)] float aiRetargetSeconds = 1.5f;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RampageScoringRuleSO.AssignScores) - lower is better, like HexRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // Rampage handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // the base SyncFinalResults template, which broadcasts the canonical results tail.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
        }

        // ── AI mass hunters (server) ──────────────────────────────────────

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer) return;
            base.OnCountdownTimerEnded(); // ClientRpc: SetPlayersActive + StartTurn
            ArmMassHunters();
        }

        /// <summary>
        /// Points every AI pilot at the densest region of mass HOSTILE to its domain -
        /// <see cref="Cell.GetExplosionTarget"/>, the same density-grid query aggression-1
        /// fauna use (no physics queries, no parallel spatial store - see
        /// Docs/SPATIAL_INDEX.md). Ramming through the cluster destroys it, so the AI
        /// genuinely competes in the race. Mirrors Astro League's ArmStrikers pattern.
        /// </summary>
        void ArmMassHunters()
        {
            foreach (var p in gameData.Players)
            {
                if (p == null || !p.IsInitializedAsAI) continue;
                var pilot = p.Vessel?.VesselStatus?.AIPilot;
                if (pilot == null) continue;

                var captured = p;
                Vector3 cached = captured.Vessel.Transform.position;
                float nextSample = 0f;
                pilot.SetExternalTargetProvider(() =>
                {
                    if (arenaCell != null && Time.time >= nextSample)
                    {
                        nextSample = Time.time + aiRetargetSeconds;
                        cached = arenaCell.GetExplosionTarget(captured.Domain);
                    }
                    return cached;
                });
            }
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time for the
        /// base SetupNewRound to suppress the Ready button. Winning domain (highest destruction
        /// sum, Jade→Ruby→Gold tie-break) delegated to the rule; score assignment, the
        /// representative winner name (best individual contributor on the winning domain), the
        /// roster snapshot and the canonical results tail are owned by the base template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            // Winners score the match time (Time.time - TurnStartTime, the server's turn
            // clock); losers the remaining-prisms sentinel. The rule owns the encoding.
            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            SyncFinalResults(winningDomain, finishTime);
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
