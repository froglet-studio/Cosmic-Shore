using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Holds the randomized win/lose "taunt" lines shown on the end-game reveal toast
    /// (the GameOverCounter text). Revives the message data that used to live on the
    /// removed CinematicDefinitionSO assets (victoryToastStrings / defeatToastStrings).
    /// <see cref="CosmicShore.Utility.EndGameSequencer"/> picks one at random per game.
    /// </summary>
    [CreateAssetMenu(fileName = "EndGameMessageSet",
        menuName = "ScriptableObjects/Game/End Game Message Set", order = 0)]
    public class EndGameMessageSetSO : ScriptableObject
    {
        [Header("Winner lines - shown when the local player is on the winning domain")]
        [SerializeField] private string[] winnerMessages =
        {
            "ABSOLUTELY CRACKED",
            "STANDING ON BUSINESS",
            "BUILT DIFFERENT",
            "CERTIFIED W",
            "MAIN CHARACTER ENERGY",
            "GOATED WITH THE SAUCE",
            "YOU ATE THAT UP",
            "SHEEESH!",
            "CLUTCHED IT",
            "BAG SECURED",
            "DIFFERENT BREED",
            "TOOK THE DUB",
            "THAT WAS...WOW!",
        };

        [Header("Loser lines - shown when the local player is on a losing domain")]
        [SerializeField] private string[] loserMessages =
        {
            "ABSOLUTELY BOTTLED IT",
            "BETTER LUCK NEXT TIME",
            "CAUGHT IN 4K",
            "TOOK THE L",
            "IT'S GIVING DEFEAT",
            "SKILL ISSUE",
            "WOMP WOMP",
            "RATIO'D",
            "GET COOKED",
            "DOWN BAD",
            "FUMBLED THE BAG",
            "NOT BEATING THE ALLEGATIONS",
            "GG GO NEXT",
        };

        /// <summary>
        /// Random line for the local player's result. Falls back to a plain
        /// "VICTORY"/"DEFEAT" when the matching pool is empty.
        /// </summary>
        public string GetRandomMessage(bool didWin)
        {
            var pool = didWin ? winnerMessages : loserMessages;
            if (pool == null || pool.Length == 0)
                return didWin ? "VICTORY" : "DEFEAT";
            return pool[Random.Range(0, pool.Length)];
        }
    }
}
