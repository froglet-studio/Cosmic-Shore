using CosmicShore.App.Systems;
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
        [Range(0,4)]
        [SerializeField] public int DailyChallengeIntensity;
        [SerializeField] public DailyChallengeReward DailyChallengeTierOneReward;
        [SerializeField] public DailyChallengeReward DailyChallengeTierTwoReward;
        [SerializeField] public DailyChallengeReward DailyChallengeTierThreeReward;
        [SerializeField] public DailyChallengeReward IntensityOneReward;
        [SerializeField] public DailyChallengeReward IntensityTwoReward;
        [SerializeField] public DailyChallengeReward IntensityThreeReward;
        [SerializeField] public DailyChallengeReward IntensityFourReward;
        
        public SO_QuestChain SO_QuestChain;
    }
}