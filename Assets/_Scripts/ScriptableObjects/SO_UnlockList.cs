using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Ordered list of unlock nodes that form the progression chain.
    /// The order in this list determines the unlock sequence.
    ///
    /// Formerly <c>SO_GameModeQuestList</c>. The script GUID and the <c>Quests</c> field name are
    /// preserved across the rename so existing authored assets keep resolving with their data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UnlockList",
        menuName = "ScriptableObjects/Quests/UnlockList")]
    public class SO_UnlockList : ScriptableObject
    {
        [Tooltip("Unlocks in progression order. Index 0 is unlocked from the start.")]
        public List<SO_UnlockData> Quests = new();
    }
}
