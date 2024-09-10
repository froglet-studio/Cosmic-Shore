using System;
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
        [Range(1,4)]
        [SerializeField] public int DailyChallengeIntensity;
        [SerializeField] public GameplayReward DailyChallengeTierOneReward;
        [SerializeField] public GameplayReward DailyChallengeTierTwoReward;
        [SerializeField] public GameplayReward DailyChallengeTierThreeReward;
        [SerializeField] public GameplayReward IntensityOneReward;
        [SerializeField] public GameplayReward IntensityTwoReward;
        [SerializeField] public GameplayReward IntensityThreeReward;
        [SerializeField] public GameplayReward IntensityFourReward;
        
        public SO_QuestChain SO_QuestChain;
    }
}