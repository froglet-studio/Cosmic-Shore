using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rampage - the **Dolphin-only** demolition race, and the destructive analog of Crystal
    /// Capture ("Scurry"): every domain races to destroy the prism target first (hostile
    /// prisms only - another domain's mass; shattering your own trail is worthless).
    /// Structural clone of <see cref="ScurryController"/>: 1 round / 1 turn,
    /// server-authoritative winner detection in OnTurnEndedCustom, final scores replicated by
    /// snapshot ClientRpc. The destruction stat itself auto-increments via
    /// StatsManager.PrismDestroyed (SOAP block-destroyed channel), so no per-event listener is
    /// needed here.
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
    /// <see cref="DogFightController"/> / PEEL_THE_CAGE.md for why the server clamp is the one that
    /// matters in multiplayer.</para>
    /// </summary>
    public class RampageController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag RampageScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        private bool _finalResultsSent;

        // Golf: winners carry their finish time, losers a DnfThreshold+remaining sentinel
        // (see RampageScoringRuleSO.AssignScores) - lower is better, like SkimRace.
        protected override bool UseGolfRules => true;
        protected override bool UseSceneReloadForReplay => true;

        // Rampage handles end-game through OnTurnEndedCustom (server-side winner detection) →
        // SyncFinalScores_ClientRpc, which calls InvokeWinnerCalculated + InvokeMiniGameEnd.
        // Suppress the base controller's turn→round→game flow so we don't get a duplicate
        // InvokeWinnerCalculated from SyncGameEnd_ClientRpc.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── Server-authoritative game end ─────────────────────────────────

        /// <summary>
        /// Server-side winner detection, mirroring the Crystal Capture pattern.
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound,
        /// so _finalResultsSent is set in time to suppress the Ready button.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            // Winning domain (highest destruction sum, Jade→Ruby→Gold tie-break) delegated to
            // the rule; representative winner-name = best individual contributor on that domain
            // (legacy display field - victory/defeat attribution uses WinnerDomain).
            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.HostilePrismsDestroyed)
                .FirstOrDefault();
            if (winnerRep == null) return;

            // Winners score the match time (Time.time - TurnStartTime, the server's turn
            // clock); losers the remaining-prisms sentinel. The rule owns the encoding;
            // the snapshot RPC replicates the final Scores so clients rebuild identically.
            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound when the game just ended.
        /// HasEndGame=false causes ExecuteServerRoundEnd to call SetupNewRound instead of
        /// ExecuteServerGameEnd - this override prevents the Ready button from appearing.
        /// </summary>
        protected override void SetupNewRound()
        {
            if (_finalResultsSent) return;
            base.SetupNewRound();
        }

        // ── Score sync ───────────────────────────────────────────────────

        void SyncFinalScoresSnapshot(string winnerName, Domains winnerDomain)
        {
            var statsList = gameData.RoundStatsList;
            int count = statsList.Count;

            var nameArray = new FixedString64Bytes[count];
            var scoreArray = new float[count];
            var domainArray = new int[count];
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] prismsDestroyed,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[Rampage] Client could not match RoundStats for '{sName}'. " +
                                   $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

            // Authoritative winner - written to gameData, consumed by EndGameControllers
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        // ── Replay ───────────────────────────────────────────────────────

        protected override void OnResetForReplayCustom()
        {
            base.OnResetForReplayCustom();
            _finalResultsSent = false;

            foreach (var s in gameData.RoundStatsList)
            {
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
