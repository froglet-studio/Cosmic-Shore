using UnityEngine;

namespace CosmicShore.Core
{
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "ScriptableObjects/FTUE/Tutorial Step", order = 0)]
    public class TutorialStep : ScriptableObject
    {
        public TutorialStepType stepType = TutorialStepType.None;
        [TextArea(2, 5)] public string tutorialText;

        public TutorialStepPayload payload;
    }
}
