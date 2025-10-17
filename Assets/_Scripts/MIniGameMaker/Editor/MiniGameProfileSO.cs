using UnityEngine;

namespace CosmicShore.Tools.MiniGameMaker
{
    [CreateAssetMenu(fileName="MiniGameProfile", menuName="CosmicShore/Editor/Mini-Game Profile")]
    public sealed class MiniGameProfileSO : ScriptableObject
    {
        [Header("Core Data")]
        public ScriptableObject miniGameData; // MiniGameDataSO

        [Header("Scoring")]
        public bool golfRules;
        public ScriptableObject[] scoringConfigs; // use your ScoringConfig[] if the editor asm can reference it

        [Header("Turn")]
        public float turnDurationSeconds = 60f;

        [Header("Events (optional)")]
        public ScriptableObject[] eventsToAssign; // e.g., ScriptableEvent assets
    }
}