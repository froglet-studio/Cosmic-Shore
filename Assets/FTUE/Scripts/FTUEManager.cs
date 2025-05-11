using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.FTUE
{
    public class FTUEManager : MonoBehaviour
    {
        [SerializeField] private TutorialStep startingStep;
        [SerializeField] private FTUEScreenController screenController;
        [SerializeField] private FTUEProgress ftueProgress;

        private TutorialStep _currentStep;
        private bool _waitingForNext = false;

        private void Awake()
        {
            if (ftueProgress != null && ftueProgress.pendingStep != null)
            {
                startingStep = ftueProgress.pendingStep;
                ftueProgress.Clear();
            }
        }

        private void Start()
        {
            if (startingStep == null || screenController == null)
            {
                Debug.LogError("FTUEManager: Missing references.");
                return;
            }

            StartCoroutine(PlayStep(startingStep));
        }

        private IEnumerator PlayStep(TutorialStep step)
        {
            _currentStep = step;
            _waitingForNext = false;

            switch (step.stepType)
            {
                case TutorialStepType.CaptainDialog:
                    screenController.ShowCaptainDialog(step.stepText);
                    yield return WaitForNextInput();
                    break;

                case TutorialStepType.StartFreestyle:
                    ftueProgress.pendingStep = step.nextStep;
                    SceneManager.LoadScene(step.sceneToLoad);
                    yield break;

                case TutorialStepType.InGameInstruction:
                    screenController.ShowInstruction(step.stepText);
                    yield return WaitForNextInput();
                    break;

                case TutorialStepType.ReturnToMenu:
                    SceneManager.LoadScene("MainMenu");
                    yield break;
            }

            if (step.nextStep != null)
            {
                yield return new WaitForSeconds(0.2f);
                StartCoroutine(PlayStep(step.nextStep));
            }
        }

        private IEnumerator WaitForNextInput()
        {
            _waitingForNext = true;
            while (_waitingForNext)
                yield return null;
        }

        public void OnNextPressed()
        {
            _waitingForNext = false;
        }
    }
}
