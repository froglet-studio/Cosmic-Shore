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
            if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
            {
                Debug.LogWarning("[FTUE] No tutorial sequence assigned.");
                return;
            }

            //StartCoroutine(RunCurrentStep());
        }

        public void StartFTUE()
        {
            _currentIndex = 0;
            StopAllCoroutines();
            StartCoroutine(RunCurrentStep());
        }

        private IEnumerator RunCurrentStep()
        {
            if (_currentIndex >= sequence.steps.Count)
                yield break;

            var step = sequence.steps[_currentIndex];
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
            // Remember which step we just finished
            var finishedStep = sequence.steps[_currentIndex];

            // Move to the next index so _currentIndex always points to "up next"
            _currentIndex++;

            // If we just finished LockModesExceptFreestyle, play the outro and bail out
            if (finishedStep.stepType == TutorialStepType.LockModesExceptFreestyle)
            {
                StartCoroutine(GetComponent<FTUEIntroAnimator>().PlayOutro(() => Debug.Log("Step Completed")));
                // Look for an IOutroHandler (your adapter wrapping FTUEIntroAnimator)
                //var outro = _handlers.OfType<IOutroHandler>().FirstOrDefault();
                //if (outro != null)
                //{
                //    // Fire the outro coroutine and then stop—no further steps
                //    //StartCoroutine(ou(outro));
                //}
                //else
                //{
                //    Debug.LogWarning("[FTUE] No outro handler found to close LockModes step.");
                //}
                return;
            }

            // Otherwise we continue skipping blank steps as before
            while (_currentIndex < sequence.steps.Count
                && string.IsNullOrWhiteSpace(sequence.steps[_currentIndex].tutorialText))
            {
                Debug.Log($"[FTUE] Skipping empty step {_currentIndex} ({sequence.steps[_currentIndex].stepType})");
                _currentIndex++;
            }

            if (_currentIndex < sequence.steps.Count)
                StartCoroutine(RunCurrentStep());
            else
                StartCoroutine(FinishFlow());
        }

        /// <summary>
        /// Jump immediately to the step matching <paramref name="stepType"/>,
        /// cancelling any in-flight coroutine and starting from that step.
        /// </summary>
        public void JumpToStep(TutorialStepType stepType)
        {
            // Find the index of the step with the given type
            int found = sequence.steps.FindIndex(s => s.stepType == stepType);
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
            ftueProgress.pendingSet = sequence;
            ftueProgress.nextIndex = _currentIndex + 1;
 

            Debug.Log("[FTUE] Completed.");
        }
    }
}
