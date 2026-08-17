using System.Collections;
using System.Linq;
using CosmicShore.UI;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerDomainGamesController : MultiplayerMiniGameControllerBase
    {
        [Header("Scoring")]
        [Tooltip("Drag the mode's ScoringRule asset - the per-mode scoring strategy (winner, scores, results).")]
        [SerializeField] protected ScoringRuleSO rule;

        private int readyClientCount;

        /// <summary>
        /// Latched (server-side) once the authoritative final results have been computed and
        /// broadcast via <see cref="SyncFinalResults"/>. Suppresses the base round flow's
        /// Ready button (<see cref="SetupNewRound"/>) and makes repeat end-game calls no-ops.
        /// Reset on network spawn and on in-place replay.
        /// </summary>
        protected bool FinalResultsSent { get; private set; }

        // ── Server-authoritative per-domain score sync (in-game HUD) ─────────────
        // Clients re-summing their own per-player RoundStats can freeze for a client's OWN player
        // when its own NetworkVariable replication lags. Instead the server computes each active
        // domain's metric sum and replicates it here; every peer mirrors it into gameData and the
        // MultiplayerHUD displays it verbatim, so every client matches the host exactly.
        readonly NetworkVariable<int> n_DomainSum0 =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_DomainSum1 =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_DomainSum2 =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        Coroutine _domainSumSyncRoutine;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Publish the mode's scoring rule for every rule consumer (domain sums, HUD metric,
            // turn monitors, results). Null only for not-yet-migrated legacy scenes, which must
            // not clear a previously published rule they don't own.
            if (rule != null)
                gameData.ScoringRule = rule;
            FinalResultsSent = false;

            // Every peer mirrors the synced sums into gameData (read by MultiplayerHUD).
            n_DomainSum0.OnValueChanged += (_, v) => PublishDomainSum(0, v);
            n_DomainSum1.OnValueChanged += (_, v) => PublishDomainSum(1, v);
            n_DomainSum2.OnValueChanged += (_, v) => PublishDomainSum(2, v);
            PublishDomainSum(0, n_DomainSum0.Value);
            PublishDomainSum(1, n_DomainSum1.Value);
            PublishDomainSum(2, n_DomainSum2.Value);

            if (IsServer)
                _domainSumSyncRoutine = StartCoroutine(SyncDomainSumsRoutine());
        }

        public override void OnNetworkDespawn()
        {
            if (_domainSumSyncRoutine != null)
            {
                StopCoroutine(_domainSumSyncRoutine);
                _domainSumSyncRoutine = null;
            }
            base.OnNetworkDespawn();
        }

        void PublishDomainSum(int index, int value)
        {
            if (index < 0 || index >= GameDataSO.ActiveDomains.Length) return;
            gameData.SetDomainMetricSum(GameDataSO.ActiveDomains[index], value);
        }

        /// <summary>
        /// Server-only: recompute each active domain's summed scoring metric from the authoritative
        /// RoundStats and push it through the NetworkVariables, so every client's domain boxes match
        /// the host. Throttled - the value is a small int and NetworkVariables only replicate on change.
        /// </summary>
        IEnumerator SyncDomainSumsRoutine()
        {
            var wait = new WaitForSeconds(0.1f);
            while (true)
            {
                // Read the published rule (not the serialized field) so not-yet-migrated
                // subclasses that publish nothing keep their pre-hoist behavior.
                var activeRule = gameData.ScoringRule;
                if (activeRule != null)
                {
                    n_DomainSum0.Value = ScoringMetrics.SumByDomain(gameData, activeRule.Metric, GameDataSO.ActiveDomains[0]);
                    n_DomainSum1.Value = ScoringMetrics.SumByDomain(gameData, activeRule.Metric, GameDataSO.ActiveDomains[1]);
                    n_DomainSum2.Value = ScoringMetrics.SumByDomain(gameData, activeRule.Metric, GameDataSO.ActiveDomains[2]);
                }
                yield return wait;
            }
        }

        // ── Rule-driven authoritative end-game (the shared six-step tail) ────────
        // Every domain mode ends the same way: server detects the winner in its
        // OnTurnEndedCustom, then calls SyncFinalResults exactly once. The template owns
        // score assignment, the roster snapshot, and the canonical results tail
        // (WinnerName → SortRoundStats → CalculateDomainStats → SetResults →
        // InvokeWinnerCalculated → InvokeMiniGameEnd) so a mode cannot forget a step.

        /// <summary>
        /// Server-side: the one authoritative end-game entry for domain modes. Assigns final
        /// scores via the rule, sorts + aggregates, snapshots the roster (name/score/domain/
        /// metric) and broadcasts the canonical results tail to every peer. Latched - repeat
        /// calls are no-ops. Modes call this from <see cref="OnTurnEndedCustom"/> once their
        /// winning domain is known; <paramref name="finishTime"/> feeds golf-style
        /// <see cref="ScoringRuleSO.AssignScores"/> (pass 0 for points modes).
        /// A <see cref="Domains.Blue"/> winner is a valid NO-WINNER end (e.g. a co-op DNF):
        /// results still broadcast, with no representative name and DEFEAT attribution.
        /// </summary>
        protected void SyncFinalResults(Domains winnerDomain, float finishTime)
        {
            if (!IsServer || FinalResultsSent) return;

            var statsList = gameData.RoundStatsList;
            if (statsList == null || statsList.Count == 0) return;

            // Representative winner-name resolves before AssignScores mutates Score
            // (LiveMetric is Score-independent, but keep the read upfront for clarity).
            string winnerName = winnerDomain == Domains.Blue ? "" : ResolveWinnerRepresentativeName(winnerDomain);
            if (winnerDomain != Domains.Blue && string.IsNullOrEmpty(winnerName)) return; // no roster entry on the winning domain

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-10] [{GetType().Name}] Final results - domain {winnerDomain} wins ('{winnerName}', finishTime={finishTime:F2}). Broadcasting.</color>");
            FinalResultsSent = true;

            rule.AssignScores(gameData, winnerDomain, finishTime);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);

            int count = statsList.Count;
            var names = new FixedString64Bytes[count];
            var scores = new float[count];
            var domains = new int[count];
            var metricValues = new int[count];

            for (int i = 0; i < count; i++)
            {
                names[i] = new FixedString64Bytes(statsList[i].Name);
                scores[i] = statsList[i].Score;
                domains[i] = (int)statsList[i].Domain;
                metricValues[i] = rule.LiveMetric(statsList[i]);
            }

            SyncFinalResults_ClientRpc(names, scores, domains, metricValues,
                new FixedString64Bytes(winnerName), (int)winnerDomain);
        }

        /// <summary>
        /// Representative winner-name = best individual metric contributor on the winning
        /// domain (display strings only - VICTORY/DEFEAT attribution is via WinnerDomain).
        /// First roster entry wins metric ties, keeping the pick deterministic on the server.
        /// </summary>
        protected virtual string ResolveWinnerRepresentativeName(Domains winnerDomain)
        {
            IRoundStats best = null;
            var statsList = gameData.RoundStatsList;
            for (int i = 0; i < statsList.Count; i++)
            {
                var stats = statsList[i];
                if (stats == null || stats.Domain != winnerDomain) continue;
                if (best == null || rule.LiveMetric(stats) > rule.LiveMetric(best))
                    best = stats;
            }
            return best?.Name ?? "";
        }

        [ClientRpc]
        void SyncFinalResults_ClientRpc(
            FixedString64Bytes[] names,
            float[] scores,
            int[] domains,
            int[] metricValues,
            FixedString64Bytes winnerName,
            int winnerDomain)
        {
            for (int i = 0; i < names.Length; i++)
            {
                string sName = names[i].ToString();
                var stat = gameData.RoundStatsList.FirstOrDefault(s => s.Name == sName);
                if (stat == null)
                {
                    CSDebug.LogError($"[{GetType().Name}] Client could not match RoundStats for '{sName}'. " +
                                     $"Available: {string.Join(", ", gameData.RoundStatsList.Select(s => $"'{s.Name}'"))}");
                    continue;
                }
                stat.Score = scores[i];
                stat.Domain = (Domains)domains[i];
                ScoringMetrics.Write(stat, rule.Metric, metricValues[i]);
            }

            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));

            // Authoritative winner - written AFTER SetResults so the explicit values always
            // win: SetResults derives Winner* from Results[0] when unset, which would credit
            // a roster row on a no-winner (Blue) end and flip a DNF into VICTORY.
            // OnWinnerCalculated (below) is the "results ready" signal.
            gameData.WinnerName = winnerName.ToString();
            gameData.WinnerDomain = (Domains)winnerDomain;
            gameData.HasNoWinner = (Domains)winnerDomain == Domains.Blue;

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            OnReadyClicked_ServerRpc(gameData.LocalPlayer.Name);
        }

        [ServerRpc(RequireOwnership = false)]
        void OnReadyClicked_ServerRpc(string playerName)
        {
            readyClientCount++;

            // Use connected clients count (humans only - excludes AI)
            int humanCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnReadyClicked_ServerRpc - {playerName} ready. Count: {readyClientCount}/{humanCount}</color>");
            CSDebug.Log($"[Server] Player Ready. Count: {readyClientCount}/{humanCount}");

            // Broadcast which player is ready to all clients
            NotifyPlayerReady_ClientRpc(playerName);

            if (readyClientCount < humanCount)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFA500>[FLOW-9] [DomainGamesCtrl] Waiting for more players ({readyClientCount}/{humanCount})</color>");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00CED1>[FLOW-9] [DomainGamesCtrl] All players ready! Starting countdown...</color>");
            readyClientCount = 0;
            OnReadyClicked_ClientRpc();
        }

        [ClientRpc]
        void NotifyPlayerReady_ClientRpc(string playerName)
        {
            // Domain attribution reads the live Player.Domain (the authoritative
            // NetDomain mirror) via the Players roster - the same source the in-game
            // domain boxes group by - rather than the name-keyed RoundStatsList,
            // which historically resolved a stale pre-party shadow entry on joined
            // clients (frozen at Jade). RoundStats fallback kept for the window
            // before this peer's roster has re-registered the player.
            var player = gameData.Players.FirstOrDefault(p => p != null && p.Name == playerName);
            var domain = player?.Domain
                         ?? gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName)?.Domain
                         ?? Domains.Blue;
            GameToastAPI.Post(GameToastSituation.PlayerReady, domain, playerName);
        }

        [ClientRpc]
        void OnReadyClicked_ClientRpc()
        {
            StartCountdownTimer();
        }

        /// <summary>
        /// Suppresses the round flow once the final results are latched (HasEndGame=false
        /// modes route ExecuteServerRoundEnd back here otherwise - this prevents the Ready
        /// button from reappearing after the game ended).
        /// </summary>
        protected override void SetupNewRound()
        {
            if (FinalResultsSent) return;

            if (IsServer)
            {
                readyClientCount = 0;
            }

            // First round: MiniGameHUD shows ReadyButton after cinematic.
            // Subsequent rounds: show it immediately.
            if (gameData.RoundsPlayed > 0)
                RaiseToggleReadyButtonEvent(true);

            base.SetupNewRound();
        }

        // Ensure players are physically reset (positions/state) when replaying
        protected override void OnResetForReplay()
        {
            gameData.ResetPlayers();
            base.OnResetForReplay();
        }

        protected override void OnResetForReplayCustom()
        {
            FinalResultsSent = false;
            base.OnResetForReplayCustom();
        }

        protected override void OnPlayerLeavingFromSession(string clientId)
        {
            if (ulong.TryParse(clientId, out var id) &&
                gameData.TryGetPlayerByOwnerClientId(id, out var player))
            {
                // Use Player.Domain (live mirror, kept in sync by OnNetDomainChanged) so
                // the disconnect notification colors correctly even if domain changed
                // mid-game.
                var domain = player.Domain;
                GameToastAPI.Post(GameToastSituation.PlayerDisconnected, domain, player.Name);
                gameData.RemovePlayerData(player.Name);
            }
        }
    }
}