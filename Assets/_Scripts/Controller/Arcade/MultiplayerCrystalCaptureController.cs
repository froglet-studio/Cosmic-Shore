using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerCrystalCaptureController : MultiplayerDomainGamesController
    {
        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // Crystal Capture handles end-game through OnTurnEndedCustom (server-side winner
        // detection) → the base SyncFinalResults template, which broadcasts the canonical
        // results tail. Suppress the base controller's turn→round→game flow so we don't get
        // a duplicate InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection. Called from SyncTurnEnd_ClientRpc BEFORE
        /// ExecuteServerTurnEnd → SetupNewRound, so FinalResultsSent latches in time to
        /// suppress the Ready button. Winning domain (highest crystal sum, Jade→Ruby→Gold
        /// tie-break) delegated to the rule; score assignment, roster snapshot, and the
        /// canonical results tail are owned by the base SyncFinalResults template.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || FinalResultsSent) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            SyncFinalResults(winningDomain, 0f);
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();

            foreach (var s in gameData.RoundStatsList)
            {
                s.CrystalsCollected = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
