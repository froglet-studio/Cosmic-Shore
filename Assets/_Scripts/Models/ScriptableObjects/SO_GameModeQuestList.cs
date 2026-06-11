using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Models
{
    /// <summary>
    /// Ordered list of game-mode quests that form the progression chain.
    /// The order in this list determines the unlock sequence.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameModeQuestList",
        menuName = "ScriptableObjects/Quests/GameModeQuestList")]
    public class SO_GameModeQuestList : ScriptableObject
    {
        [Tooltip("Quests in progression order. Index 0 is unlocked from the start.")]
        public List<SO_GameModeQuestData> Quests = new();
    }
}
