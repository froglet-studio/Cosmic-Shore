using System.Collections.Generic;
using System.Threading.Tasks;
using CosmicShore.Models.Enums;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Party
{
    /// <summary>
    /// Orchestrates the host's game launch flow from the party lobby:
    /// 1. Host selects game mode, player count, and intensity
    /// 2. Reconciles party members vs requested player count:
    ///    - If more invited clients than (playerCount - 1): kick extras
    ///    - If fewer clients than (playerCount - 1): AI fills the remaining slots
    /// 3. Configures GameDataSO and triggers the game launch
    ///
    /// Wire this on the same persistent GameObject as HostConnectionService.
    /// </summary>
    public class PartyGameLauncher : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;
        [SerializeField] private GameDataSO gameData;

        [Header("Game Config")]
        [Tooltip("Game list used to resolve SO_ArcadeGame from a GameModes enum.")]
        [SerializeField] private SO_GameList gameList;

        [Header("Events")]
        [Tooltip("Raised after the party has been reconciled and gameData is configured, right before scene load.")]
        [SerializeField] private ScriptableEventNoParam onPartyGameReady;

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the host to launch Multiplayer Freestyle with the given settings.
        /// Reconciles the party (kick extras / backfill AI), configures GameDataSO,
        /// hands off the party session, and fires the game launch event.
        /// </summary>
        public async void LaunchMultiplayerFreestyle(int playerCount, int intensity)
        {
            if (!connectionData.IsHost)
            {
                Debug.LogWarning("[PartyGameLauncher] Only the host can launch a game.");
                return;
            }

            var game = FindGame(GameModes.MultiplayerFreestyle);
            if (game == null)
            {
                Debug.LogError("[PartyGameLauncher] MultiplayerFreestyle game not found in game list.");
                return;
            }

            // Clamp to the game's allowed range
            playerCount = Mathf.Clamp(playerCount, game.MinPlayers, game.MaxPlayers);
            intensity = Mathf.Clamp(intensity, game.MinIntensity, game.MaxIntensity);

            // How many human slots are available beyond the host
            int remoteHumanSlots = playerCount - 1;
            int remoteMembers = connectionData.RemotePartyMemberCount;

            // ── Kick extras if too many clients ──────────────────────────────
            if (remoteMembers > remoteHumanSlots)
            {
                await KickExcessMembers(remoteHumanSlots);
            }

            // ── Configure GameDataSO ─────────────────────────────────────────
            int aiSlots = Mathf.Max(0, remoteHumanSlots - connectionData.RemotePartyMemberCount);

            ConfigureGameData(game, playerCount, intensity, aiSlots);

            // ── Hand off party session to multiplayer setup ──────────────────
            HostConnectionService.Instance?.HandOffToMultiplayerSetup(gameData);

            onPartyGameReady?.Raise();

            // ── Launch ───────────────────────────────────────────────────────
            Debug.Log($"[PartyGameLauncher] Launching {game.DisplayName}: " +
                      $"players={playerCount}, intensity={intensity}, " +
                      $"humans={connectionData.RemotePartyMemberCount + 1}, ai={aiSlots}");

            gameData.InvokeGameLaunch();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal
        // ─────────────────────────────────────────────────────────────────────

        private void ConfigureGameData(SO_ArcadeGame game, int playerCount, int intensity, int aiSlots)
        {
            gameData.GameMode = game.Mode;
            gameData.SceneName = game.SceneName;
            gameData.IsMultiplayerMode = game.IsMultiplayer;
            gameData.RequestedAIBackfillCount = aiSlots;

            if (gameData.SelectedPlayerCount != null)
                gameData.SelectedPlayerCount.Value = playerCount;

            if (gameData.SelectedIntensity != null)
                gameData.SelectedIntensity.Value = intensity;
        }

        /// <summary>
        /// Kicks the most-recently-joined party members until only
        /// <paramref name="maxRemoteSlots"/> remote members remain.
        /// </summary>
        private async Task KickExcessMembers(int maxRemoteSlots)
        {
            if (connectionData.PartyMembers == null) return;

            // Build a list of remote member IDs (skip self at index 0)
            var remoteIds = new List<string>();
            foreach (var m in connectionData.PartyMembers)
            {
                if (m.PlayerId != connectionData.LocalPlayerId)
                    remoteIds.Add(m.PlayerId);
            }

            // Kick from the end (most recently joined) until we're at the limit
            int toKick = remoteIds.Count - maxRemoteSlots;
            for (int i = remoteIds.Count - 1; i >= 0 && toKick > 0; i--, toKick--)
            {
                string kickId = remoteIds[i];
                Debug.Log($"[PartyGameLauncher] Kicking excess member: {kickId}");

                if (HostConnectionService.Instance != null)
                    await HostConnectionService.Instance.KickPartyMemberAsync(kickId);
                else
                    connectionData.RemovePartyMember(kickId);
            }
        }

        private SO_ArcadeGame FindGame(GameModes mode)
        {
            if (gameList == null || gameList.Games == null) return null;

            foreach (var game in gameList.Games)
            {
                if (game.Mode == mode)
                    return game;
            }
            return null;
        }
    }
}
