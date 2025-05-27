using System.Collections;
using UnityEngine;

namespace CosmicShore.FTUE
{
    [AddComponentMenu("FTUE/Handlers/IntroWelcomeHandler")]
    public class IntroWelcomeHandler : MonoBehaviour, ITutorialStepHandler
    {
        public TutorialStepType HandlesType => TutorialStepType.IntroWelcome;

        [SerializeField] private TutorialExecutorAdapter _executor;
        [SerializeField] private FTUEIntroAnimatorAdapter _animator;
        [SerializeField] private TutorialUIViewAdapter _uiView;

        [Tooltip("How long to wait after the arcade UI opens before moving on")]
        [SerializeField] private float _pauseAfterArcade = 0.5f;

        public IEnumerator ExecuteStep(TutorialStep step, IFlowController controller)
        {
            _executor.SetupPreIntroUI();

            yield return _animator.PlayIntro();

            bool uiDone = false;
            _uiView.ShowStep(step.tutorialText, () => uiDone = true);
            while (!uiDone) yield return null;

            if (step.payload.payloadType == PayloadType.OpenArcadeAction)
            {
                yield return _animator.PlayOutro();

                _executor.PrepareArcadeScreen();

                controller.StepCompleted();
                yield return new WaitForSeconds(_pauseAfterArcade);
            }
        }
    }
}
