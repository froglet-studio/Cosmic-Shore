using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE
{
    [AddComponentMenu("FTUE/Handlers/LockModesExceptFreestyleHandler")]
    public class LockModesExceptFreestyleHandler : MonoBehaviour, ITutorialStepHandler
    {
        public TutorialStepType HandlesType => TutorialStepType.LockModesExceptFreestyle;

        [SerializeField] private FTUEIntroAnimatorAdapter _animator;
        [SerializeField] private TutorialUIViewAdapter _uiView;

        public IEnumerator ExecuteStep(TutorialStep step, IFlowController controller)
        {
            Debug.Log("Playing this step!");

            _uiView.ToggleCanvas(true);

            yield return _animator.PlayIntro();
         
            _uiView.ShowStep(step.tutorialText, controller.StepCompleted);
        }
    }
}
