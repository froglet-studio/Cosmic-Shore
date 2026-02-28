using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Maps a GameMode to the visual environment prefab that should be
    /// instantiated when that mini-game round starts.
    /// </summary>
    [Serializable]
    public class MiniGameEnvironmentEntry
    {
        public GameModes gameMode;
        [Tooltip("Prefab to instantiate as the visual environment (Cell, Nucleus, etc.).")]
        public GameObject environmentPrefab;
    }

    [CreateAssetMenu(
        fileName = "PartyGameConfig",
        menuName = "ScriptableObjects/Party/PartyGameConfig")]
    public class PartyGameConfigSO : ScriptableObject
    {
        [Header("Players")]
        [Tooltip("Minimum human players required to start (AI fills remaining slots). Set to 1 for offline/solo mode.")]
        [Range(1, 3)] public int MinPlayers = 1;

        [Tooltip("Maximum players in the party (human + AI).")]
        [Range(2, 3)] public int MaxPlayers = 3;

        [Header("Rounds")]
        [Tooltip("Total number of mini-game rounds in a party session.")]
        [Range(1, 10)] public int TotalRounds = 5;

        [Tooltip("Mini-game modes available for randomization.")]
        public List<GameModes> AvailableMiniGames = new()
        {
            GameModes.MultiplayerCrystalCapture,
            GameModes.HexRace,
            GameModes.MultiplayerJoust,
        };

        [Header("Environment Prefabs")]
        [Tooltip("Map each mini-game mode to its visual environment prefab (Cell, Nucleus, etc.). " +
                 "These are instantiated locally on each client when a round starts.")]
        public List<MiniGameEnvironmentEntry> EnvironmentPrefabs = new();

        [Header("Timing")]
        [Tooltip("Seconds to wait for players in online matchmaking before filling with AI.")]
        public float LobbyWaitTimeSeconds = 120f;

        [Tooltip("Seconds to wait in lobby when playing solo (1 player). Gives the player time to see the lobby.")]
        public float SoloLobbyWaitSeconds = 10f;

        [Tooltip("Countdown seconds before a mini-game round starts.")]
        public float PreRoundCountdownSeconds = 3f;

        [Tooltip("Delay (seconds) after countdown before loading the mini-game.")]
        public float PostCountdownDelaySeconds = 1f;

        [Tooltip("Delay (seconds) after a round ends before showing the party panel.")]
        public float PostRoundDelaySeconds = 2f;

        [Header("Round Duration")]
        [Tooltip("Maximum duration (seconds) for each mini-game round. The round ends when either " +
                 "the game-specific end condition fires or this timer expires, whichever comes first.")]
        public float RoundDurationSeconds = 60f;
    }
}
