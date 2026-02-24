using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Party
{
    [CreateAssetMenu(
        fileName = "PartyGameConfig",
        menuName = "ScriptableObjects/Party/PartyGameConfig")]
    public class PartyGameConfigSO : ScriptableObject
    {
        [Header("Players")]
        [Tooltip("Minimum human players required to start (AI fills remaining slots).")]
        [Range(1, 3)] public int MinPlayers = 2;

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

        [Header("Timing")]
        [Tooltip("Seconds to wait for players before filling with AI.")]
        public float LobbyWaitTimeSeconds = 120f;

        [Tooltip("Countdown seconds before a mini-game round starts.")]
        public float PreRoundCountdownSeconds = 3f;

        [Tooltip("Delay (seconds) after countdown before loading the mini-game.")]
        public float PostCountdownDelaySeconds = 1f;

        [Tooltip("Delay (seconds) after a round ends before showing the party panel.")]
        public float PostRoundDelaySeconds = 2f;
    }
}
