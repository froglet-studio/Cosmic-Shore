using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Quest Chain", menuName = "CosmicShore/QuestChain", order = 12)]
    public class SO_QuestChain : ScriptableObject
    {
        public List<Quest> Quests;
    }
}