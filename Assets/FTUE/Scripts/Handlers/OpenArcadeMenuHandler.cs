using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE
{
    [AddComponentMenu("FTUE/Handlers/OpenArcadeMenuHandler")]
    public class OpenArcadeMenuHandler : MonoBehaviour, ITutorialStepHandler
    {
        public TutorialStepType HandlesType => TutorialStepType.OpenArcadeMenu;

        [SerializeField]
        private TutorialExecutorAdapter _executor;         // wraps PrepareArcadeScreen()

        [SerializeField]
        private FTUEIntroAnimatorAdapter _animator;        // wraps PlayIntro()/PlayOutro()

        [Tooltip("How long to wait after opening the arcade UI before moving on")]
        [SerializeField]
        private float _pauseDuration = 0.5f;

        public IEnumerator ExecuteStep(TutorialStep step, IFlowController controller)
        {
            // 1) Fade out the welcome panel
            yield return _animator.PlayOutro();

            // 2) Enable nav & missions
            _executor.PrepareArcadeScreen();

            // 3) Wait a beat so the player sees the arcade UI pop in
            yield return new WaitForSeconds(_pauseDuration);

            // 4) Now advance to the LockModesExceptFreestyleHandler
            controller.StepCompleted();
        }
    }
}
