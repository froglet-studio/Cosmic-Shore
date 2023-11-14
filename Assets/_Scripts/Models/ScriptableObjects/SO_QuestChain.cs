using CosmicShore.App.Systems.Quests;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "Quest Chain", menuName = "CosmicShore/QuestChain", order = 12)]
    public class SO_QuestChain : ScriptableObject
    {
        public List<Quest> Quests;
    }
}