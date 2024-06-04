using UnityEngine;

namespace CosmicShore
{

    [CreateAssetMenu(fileName = "New Game", menuName = "CosmicShore/Game/TrainingGame", order = 0)]
    [System.Serializable]
    public class SO_TrainingGame : SO_ArcadeGame
    {
        [SerializeField] public Element ElementOne;
        [SerializeField] public Element ElementTwo;
        public SO_QuestChain SO_QuestChain;
    }
}