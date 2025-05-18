using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE
{
    [AddComponentMenu("FTUE/Handlers/FreestylePromptHandler")]
    public class FreestylePromptHandler : MonoBehaviour, ITutorialStepHandler
    {
        public TutorialStepType HandlesType => TutorialStepType.FreestylePrompt;

        [SerializeField] private FTUEIntroAnimatorAdapter _animator;
        [SerializeField] private TutorialUIViewAdapter _uiView;

        public IEnumerator ExecuteStep(TutorialStep step, IFlowController controller)
        {
            Debug.Log("Playing this step");

            //yield return new WaitForSeconds(1f);

            _uiView.ToggleCanvas(true);
            
            yield return _animator.PlayIntro();

            _uiView.ShowStep(step.tutorialText, controller.StepCompleted);
        }
    }
}
