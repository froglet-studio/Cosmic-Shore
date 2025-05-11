using UnityEngine;

namespace CosmicShore.FTUE
{
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "CosmicShore/FTUE/Tutorial Step", order = 0)]
    public class TutorialStep : ScriptableObject
    {
        public TutorialStepType stepType;
        public string stepText; // Used in CaptainDialog + InGameInstruction

        [Header("Scene Handling")]
        public string sceneToLoad; // Only used if stepType is StartFreestyle

        [Header("Next Step")]
        public TutorialStep nextStep;
    }
}
