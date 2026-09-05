using System.Collections;
using System.Linq;
using CosmicShore.UI;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public class MultiplayerDomainGamesController : MultiplayerMiniGameControllerBase
    {
        private int readyClientCount;

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
                var rule = gameData.ScoringRule;
                if (rule != null)
                {
                    n_DomainSum0.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[0]);
                    n_DomainSum1.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[1]);
                    n_DomainSum2.Value = rule.DomainValue(gameData, GameDataSO.ActiveDomains[2]);
                }
                yield return wait;
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            if (!IsServer)
                return;

            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnCountdownTimerEnded (server) - activating players. Players={gameData.Players.Count}, RoundStats={gameData.RoundStatsList.Count}</color>");
            OnCountdownTimerEnded_ClientRpc();
        }

        [ClientRpc]
        void OnCountdownTimerEnded_ClientRpc()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#00CED1>[FLOW-9] [DomainGamesCtrl] OnCountdownTimerEnded_ClientRpc - SetPlayersActive + StartTurn</color>");
            gameData.SetPlayersActive();
            gameData.StartTurn();
            EnsureLocalHumanCanMove();
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

        protected override void SetupNewRound()
        {
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

        protected override void EndGame()
        {
            if (!ShowEndGameSequence) return;
            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            if (IsServer)
            {
                StartCoroutine(EndGameSyncRoutine());
            }
        }

        private IEnumerator EndGameSyncRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            gameData.InvokeMiniGameEnd();
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