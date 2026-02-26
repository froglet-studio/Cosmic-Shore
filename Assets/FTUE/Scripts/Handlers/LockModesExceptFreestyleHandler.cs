using System.Collections;
using CosmicShore.FTUE.Adapters;
using CosmicShore.FTUE.Data;
using CosmicShore.FTUE.Interfaces;
using UnityEngine;

namespace CosmicShore.FTUE.Handlers
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
