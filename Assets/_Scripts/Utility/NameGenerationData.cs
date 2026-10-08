using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Data storage of all the valid strings used to create a player's name.
    /// Currently names are a two word combination in Adjective-Noun Combo (e.g. Happy Apple)
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/GameData/NameGeneration", order = 2)]
    public class NameGenerationData : ScriptableObject
    {
        [Tooltip("The list of all possible strings the game can use as the first word of a player name")]
        public string[] FirstWordList;

        [Tooltip("The list of all possible strings the game can use as the second word in a player name")]
        public string[] SecondWordList;

        public string GenerateName()
        {
            // Random.Range(int, int) excludes its max, so the bound is Length, not Length - 1
            // (which never picked the last word of either list).
            var firstWord = FirstWordList[Random.Range(0, FirstWordList.Length)];
            var secondWord = SecondWordList[Random.Range(0, SecondWordList.Length)];

            return firstWord + " " + secondWord;
        }
    }
}
