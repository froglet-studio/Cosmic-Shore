using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Drumfire - the <b>Dolphin-only rhythm range</b>, and the one mode built to TEACH a hull
    /// rather than to test one.
    ///
    /// <para>A great porous drum of prisms (<see cref="SpawnableDrum"/>) hangs at the cell
    /// centre. Every pilot is given their own firing lane: a straight line of evenly spaced
    /// crystals struck through their own spawn slot, which passes the drum at a standoff instead
    /// of running into it. The Dolphin's only weapon is the conic jaw blast, armed by SKIMMING
    /// and fired by touching a CRYSTAL, so the lane IS the trigger track - and because the lane
    /// runs past the target rather than at it, the drum is always off to one side and every shot
    /// needs a deliberate aim. Drift to lock the course down the lane, swing the nose onto the
    /// drum, take the next crystal: <b>fly, aim, shoot, repeat</b>. That loop is the whole
    /// mode.</para>
    ///
    /// <para><b>TIME ends it and VOLUME is the score</b> (<see cref="DrumfireTimeTurnMonitor"/>,
    /// <see cref="ScoringMetric.VolumeDestroyed"/>). There is no target to race to, so this
    /// controller's turn end is the only one in the family that fires on the clock rather than on
    /// a rule reporting its objective reached - everything after that point (resolve the winning
    /// domain, assign scores, snapshot them to every peer) is the shape
    /// <see cref="RampageController"/> established.</para>
    ///
    /// <para><b>No AI hook, deliberately.</b> The platform pilot already does exactly what this
    /// mode asks: it seeks the nearest collectible cell item (here, the next crystal on its lane)
    /// and, once its course is committed, DRIFTS and swings its nose onto the densest cluster of
    /// hostile mass it can find (<see cref="Cell.GetExplosionTarget"/>) - which in this arena is
    /// the drum. Installing <c>AIPilot.SetExternalTargetProvider</c> would override crystal
    /// seeking outright and disarm every AI Dolphin, which is the rule RAMPAGE.md records; a
    /// drift-look provider is unnecessary because the default already points at the target.</para>
    ///
    /// <para>Vessel restriction is the platform's two-place clamp fed by the single entry in
    /// <c>ArcadeGameDrumfire.Vessels</c>, not anything here.</para>
    /// </summary>
    public class DrumfireController : MultiplayerDomainGamesController
    {
        [Header("Scoring")]
        [Tooltip("Drag DrumfireScoringRule.asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] ScoringRuleSO rule;

        bool _finalResultsSent;

        // Points, not golf: the most volume torn out of the drum wins.
        protected override bool UseGolfRules => false;
        protected override bool UseSceneReloadForReplay => true;

        // End-game runs through OnTurnEndedCustom (server-side winner resolution) →
        // SyncFinalScores_ClientRpc, which raises WinnerCalculated + MiniGameEnd itself. Suppress
        // the base turn→round→game flow so those are not raised a second time.
        protected override bool HasEndGame => false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            gameData.ScoringRule = rule;
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
            _finalResultsSent = false;
        }

        // ── Server-authoritative game end (the clock, not a target) ───────

        /// <summary>
        /// Called from SyncTurnEnd_ClientRpc BEFORE ExecuteServerTurnEnd → SetupNewRound, so
        /// _finalResultsSent is set in time to suppress the Ready button.
        ///
        /// Unlike every sibling this runs because the CLOCK expired, so there is no "did anyone
        /// reach the target" question - the winner is simply whichever active domain leads on
        /// summed volume, which is what the shared <see cref="ScoringRuleSO.ResolveWinner"/>
        /// already answers.
        /// </summary>
        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
            if (!IsServer || _finalResultsSent) return;
            if (gameData.RoundStatsList == null || gameData.RoundStatsList.Count == 0) return;

            var winningDomain = rule.ResolveWinner(gameData);
            if (winningDomain == Domains.Blue) return;

            // Representative winner NAME is the biggest individual contributor on the winning
            // domain (a legacy display field - victory/defeat attribution reads WinnerDomain).
            var winnerRep = gameData.RoundStatsList
                .Where(s => s.Domain == winningDomain)
                .OrderByDescending(s => s.HostileVolumeDestroyed)
                .FirstOrDefault();
            if (winnerRep == null) return;

            // finishTime is unused by this rule (a points mode scores the metric itself), but the
            // signature is shared across every rule, so the real match length is passed rather
            // than a placeholder.
            float finishTime = Mathf.Max(0f, Time.time - gameData.TurnStartTime);
            rule.AssignScores(gameData, winningDomain, finishTime);

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            _finalResultsSent = true;
            SyncFinalScoresSnapshot(winnerRep.Name, winningDomain);
        }

        /// <summary>
        /// Suppress the base flow's SetupNewRound once the match has ended. HasEndGame=false makes
        /// ExecuteServerRoundEnd call SetupNewRound instead of ExecuteServerGameEnd; without this
        /// the Ready button would appear behind the results.
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
            var volumeArray = new float[count];
            var prismsArray = new int[count];

            for (int i = 0; i < count; i++)
            {
                nameArray[i] = new FixedString64Bytes(statsList[i].Name);
                scoreArray[i] = statsList[i].Score;
                domainArray[i] = (int)statsList[i].Domain;
                volumeArray[i] = statsList[i].HostileVolumeDestroyed;
                prismsArray[i] = statsList[i].HostilePrismsDestroyed;
            }

            SyncFinalScores_ClientRpc(nameArray, scoreArray, domainArray, volumeArray, prismsArray,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        [ClientRpc]
        void SyncFinalScores_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            float[] volumeDestroyed,
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
                    CSDebug.LogError($"[Drumfire] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                // The metric itself travels: BuildResults ranks on Score but the scoreboard's
                // secondary line and the domain sums read the raw stats, and a client whose last
                // NetworkVariable delta had not landed would otherwise show a stale total beside
                // an authoritative rank.
                stat.HostileVolumeDestroyed = volumeDestroyed[i];
                stat.HostilePrismsDestroyed = prismsDestroyed[i];
            }

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
                s.HostileVolumeDestroyed = 0f;
                s.HostilePrismsDestroyed = 0;
                s.Score = 0f;
            }

            gameData.InvokeTurnStarted();
        }
    }
}
