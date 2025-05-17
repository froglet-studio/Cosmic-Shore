using UnityEngine;

namespace CosmicShore.FTUE
{
    [CreateAssetMenu(fileName = "TutorialStep", menuName = "CosmicShore/FTUE/Tutorial Step", order = 0)]
    public class TutorialStep : ScriptableObject
    {
        public TutorialStepType stepType = TutorialStepType.None;
        [TextArea(2, 5)] public string tutorialText;
        public bool showArrow;
        public string sceneToLoadIfNeeded; // used for StartFreestyle

        public TutorialStepPayload payload;
    }
}
