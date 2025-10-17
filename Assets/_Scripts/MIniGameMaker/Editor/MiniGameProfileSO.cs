// Assets/_Tools/.../Editor/Data/MiniGameProfileSO.cs
using UnityEngine;
using CosmicShore.Game; // for IPlayer

namespace CosmicShore.Tools.MiniGameMaker
{
    [CreateAssetMenu(fileName="MiniGameProfile", menuName="CosmicShore/Editor/Mini-Game Profile")]
    public sealed class MiniGameProfileSO : ScriptableObject
    {
        [Header("Core Data")]
        public CosmicShore.SOAP.MiniGameDataSO miniGameData;

        [Header("Scoring")]
        public bool golfRules;
        public ScriptableObject[] scoringConfigs;

        [Header("Turn")]
        public float turnDurationSeconds = 60f;

        [Header("Spawner")]
        public bool spawnDefaultPlayerAndAI;            
        public IPlayer.InitializeData[] initializeDatas; 

        [Header("Events (optional)")]
        public ScriptableObject[] eventsToAssign;
    }
}