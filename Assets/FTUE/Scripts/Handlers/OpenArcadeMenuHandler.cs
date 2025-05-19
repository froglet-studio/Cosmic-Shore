using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE
{
    [AddComponentMenu("FTUE/Handlers/OpenArcadeMenuHandler")]
    public class OpenArcadeMenuHandler : MonoBehaviour, ITutorialStepHandler
    {
        public TutorialStepType HandlesType => TutorialStepType.OpenArcadeMenu;

        [SerializeField]
        private TutorialExecutorAdapter _executor;         

        [SerializeField]
        private FTUEIntroAnimatorAdapter _animator;        

        [Tooltip("How long to wait after opening the arcade UI before moving on")]
        [SerializeField]
        private float _pauseDuration = 0.5f;

        public IEnumerator ExecuteStep(TutorialStep step, IFlowController controller)
        {
            yield return _animator.PlayOutro();

            _executor.PrepareArcadeScreen();

            yield return new WaitForSeconds(_pauseDuration);

            controller.StepCompleted();
        }
    }
}
