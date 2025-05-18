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
            // 1) Ensure nav+missions are off
            _executor.SetupPreIntroUI();

            // 2) Play the captain intro
            yield return _animator.PlayIntro();

            // 3) Show the welcome text & wait for Next
            bool uiDone = false;
            _uiView.ShowStep(step.tutorialText, () => uiDone = true);
            while (!uiDone) yield return null;

            // 4) Now handle payload
            if (step.payload.payloadType == PayloadType.OpenArcadeAction)
            {
                // a) play outro of welcome panel
                yield return _animator.PlayOutro();

                // b) enable nav + missions
                _executor.PrepareArcadeScreen();

                // c) pause so player sees arcade UI
                controller.StepCompleted();
                yield return new WaitForSeconds(_pauseAfterArcade);
            }

            // 5) Advance to the next step (OpenArcadeMenu or LockModes…)
        }
    }
}
