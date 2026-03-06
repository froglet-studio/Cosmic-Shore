using System;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Game", menuName = "ScriptableObjects/Game/TrainingGame", order = 0)]
    [System.Serializable]
    public class SO_TrainingGame : ScriptableObject
    {
        [SerializeField] public SO_ArcadeGame Game;
        [SerializeField] public SO_Element ElementOne;
        [SerializeField] public SO_Element ElementTwo;
        [FormerlySerializedAs("_SO_Ship")]
        [SerializeField] public SO_Vessel _SO_Vessel;
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