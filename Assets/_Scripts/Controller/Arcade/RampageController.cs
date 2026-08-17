using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage - the **Dolphin-only** demolition race, and the destructive analog of Crystal
    /// Capture ("Scurry"): every domain races to destroy the prism target first (hostile
    /// prisms only - another domain's mass; shattering your own trail is worthless).
    /// Structural clone of <see cref="MultiplayerCrystalCaptureController"/>: 1 round / 1 turn,
    /// server-authoritative winner detection in OnTurnEndedCustom, final results replicated by
    /// the shared <see cref="MultiplayerDomainGamesController.SyncFinalResults"/> template.
    /// The destruction stat itself auto-increments via StatsManager.PrismDestroyed (SOAP
    /// block-destroyed channel), so no per-event listener is needed here.
    ///
    /// <para><b>Why the Dolphin, and why ONE crystal.</b> The Dolphin's damage verb is not its
    /// hull, it is the conic jaw blast - and that blast is armed by SKIMMING (150 skims fills
    /// the Energy meter) and fired by touching a CRYSTAL, which spends the whole meter in one
    /// shot (<c>DOLPHIN_ENERGY_ECONOMY.md</c> §1). So the vessel already contains the loop this
    /// mode wants: graze the forest to charge, then cash the charge on the one crystal in the
    /// arena. The arena carries exactly one (<c>fixedCrystalCount: 1</c>), which turns the
    /// vessel's private economy into a contested object - including the denial play of taking
    /// it empty to move it away from a fully-charged rival. Nothing here scripts that; it falls
    /// out of the crystal being singular and the meter being spent on contact.</para>
    ///
    /// <para><b>The AI is the platform default, deliberately.</b> This controller installs NO
    /// <c>AIPilot.SetExternalTargetProvider</c> hook: an AI pilot already seeks the nearest
    /// collectible cell item, which in this arena is the one contested crystal, and already knows
    /// to DRIFT once it has the crystal lined up - swinging its nose onto a cluster of hostile
    /// mass (<see cref="Cell.GetExplosionTarget"/>, the fauna hunting query) while its course
    /// stays locked on the prize. That is exactly the mode's loop, so a mode-local targeting brain
    /// would be a second implementation of behaviour the platform already has. A two-phase
    /// "graze until charged, then break for the crystal" provider was written here and REMOVED for
    /// that reason - it overrode crystal seeking outright, which is the one thing the AI must not
    /// stop doing.</para>
    ///
    /// <para>Vessel restriction is NOT enforced here - it is the platform's two-place clamp
    /// (<c>GameDataSO.SyncFromArcadeGame</c> for the machine that pressed Start,
    /// <c>ServerPlayerVesselInitializer.ResolveSpawnVesselType</c> server-side at spawn), fed by
    /// the single entry in <c>ArcadeGameRampage.Vessels</c>. See
    /// <see cref="DogFightController"/> / RIBCAGE.md for why the server clamp is the one that
    /// matters in multiplayer.</para>
    /// </summary>
    public class RampageController : MultiplayerDomainGamesController
    {
        // The `rule` field lives on MultiplayerDomainGamesController (hoisted in Y1.2) -
        // declaring it again here would shadow the base field and leave the scene's single
        // serialized `rule:` key binding ambiguously. Drag RampageScoringRule.asset onto the
        // inherited Scoring slot; the base publishes it to gameData.ScoringRule on spawn.

        // No AI fields and no SetExternalTargetProvider hook: the Rhino-era arenaCell /
        // aiRetargetSeconds pair went with ArmMassHunters when Rampage was rebuilt as the
        // Dolphin's race (see the class doc). The end-game latch is the base template's
        // FinalResultsSent - the mode-local _finalResultsSent it replaced is gone too.

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
