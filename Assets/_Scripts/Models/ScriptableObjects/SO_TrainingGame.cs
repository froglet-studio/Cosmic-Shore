using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "New Game", menuName = "CosmicShore/Game/TrainingGame", order = 0)]
    [System.Serializable]
    public class SO_TrainingGame : ScriptableObject
    {
        [SerializeField] public SO_ArcadeGame Game;
        [SerializeField] public SO_Element ElementOne;
        [SerializeField] public SO_Element ElementTwo;
        [SerializeField] public SO_Ship ShipClass;
        public SO_QuestChain SO_QuestChain;
    }
}