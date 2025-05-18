using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.FTUE
{
    /// <summary>
    /// Drives the FTUE by delegating each TutorialStep to its ITutorialStepHandler
    /// </summary>
    public class TutorialFlowController : MonoBehaviour, IFlowController
    {
        [SerializeField] private TutorialSequenceSet sequence;
        [SerializeField] private FTUEProgress ftueProgress;

        private List<ITutorialStepHandler> _handlers;
        private int _currentIndex = 0;

        private void Awake()
        {
            // Find all components on this GameObject that implement ITutorialStepHandler
            _handlers = GetComponents<MonoBehaviour>()
                        .OfType<ITutorialStepHandler>()
                        .ToList();

            if (_handlers.Count == 0)
                Debug.LogWarning("[FTUE] No ITutorialStepHandler implementations found.");
        }

        private void Start()
        {
            var steps = sequence.GetSteps(ftueProgress.currentPhase);

            if (sequence == null || steps == null || steps.Count == 0)
            {
                Debug.LogWarning("[FTUE] No tutorial sequence assigned.");
                return;
            }
        }

        public void StartFTUE()
        {
            _currentIndex = 0;
            StopAllCoroutines();
            StartCoroutine(RunCurrentStep());
        }

        private IEnumerator RunCurrentStep()
        {
            var steps = sequence.GetSteps(ftueProgress.currentPhase);

            if (_currentIndex >= steps.Count)
                yield break;

            var step = steps[_currentIndex];

            var handler = _handlers.FirstOrDefault(h => h.HandlesType == step.stepType);

            if (handler == null)
            {
                Debug.LogWarning($"[FTUE] No handler for {step.stepType}, skipping");
                _currentIndex++;
                yield return RunCurrentStep();
                yield break;
            }

            Debug.Log($"Current Step -- step{step.stepType}");
            yield return handler.ExecuteStep(step, this);
        }

        /// <summary>
        /// Called by each handler when it’s done.
        /// </summary>
        public void StepCompleted()
        {
           
            var steps = sequence.GetSteps(ftueProgress.currentPhase);

            
            var finishedStep = steps[_currentIndex];

            // Move to the next index so _currentIndex always points to "up next"
            _currentIndex++;

            // If we just finished LockModesExceptFreestyle, play the outro and bail out
            if (finishedStep.stepType == TutorialStepType.LockModesExceptFreestyle)
            {
                StartCoroutine(GetComponent<FTUEIntroAnimator>().PlayOutro(() => Debug.Log("Step Completed")));
                return;
            }

            
            while (_currentIndex < steps.Count
    && string.IsNullOrWhiteSpace(steps[_currentIndex].tutorialText))
            {
                Debug.Log($"[FTUE] Skipping empty step {_currentIndex} ({steps[_currentIndex].stepType})");
                _currentIndex++;
            }

            if (_currentIndex < steps.Count)
                StartCoroutine(RunCurrentStep());
            else
                StartCoroutine(FinishFlow());

            ftueProgress.nextIndex = _currentIndex;
        }

        /// <summary>
        /// Jump immediately to the step matching <paramref name="stepType"/>,
        /// cancelling any in-flight coroutine and starting from that step.
        /// </summary>
        public void JumpToStep(TutorialStepType stepType)
        {
            // Find the index of the step with the given type
            var steps = sequence.GetSteps(ftueProgress.currentPhase);
            int found = steps.FindIndex(s => s.stepType == stepType);

            if (found < 0)
            {
                Debug.LogWarning($"[FTUE] JumpToStep: no step of type {stepType} found.");
                return;
            }

            // Cancel whatever we were doing
            StopAllCoroutines();

            // Update the current index
            _currentIndex = found;

            // Kick off the flow at that step
            StartCoroutine(RunCurrentStep());
        }


        private IEnumerator FinishFlow()
        {
            yield return GetComponent<FTUEIntroAnimator>().PlayOutro(() => Debug.Log("Step Completed"));
            //var outro = _handlers.OfType<IOutroHandler>().FirstOrDefault();
            //if (outro != null)
            //{
            //    Debug.Log("Outro Playing");
            //    yield return outro.PlayOutro();
            //}

            // Persist progress & load the next scene
            ftueProgress.currentPhase = TutorialPhase.Phase2_GameplayTimer;
            ftueProgress.nextIndex = 0;


            Debug.Log("[FTUE] Completed.");
        }
    }
}
