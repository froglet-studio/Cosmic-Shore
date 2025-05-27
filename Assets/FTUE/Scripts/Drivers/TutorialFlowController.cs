using CosmicShore.App.Systems.CTA;
using CosmicShore.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.FTUE
{
    /// <summary>
    /// Drives the FTUE by delegating each TutorialStep to its ITutorialStepHandler
    /// </summary>
    public class TutorialFlowController : MonoBehaviour, IFlowController
    {
        [SerializeField] internal TutorialSequenceSet sequence;
        [SerializeField] internal FTUEProgress ftueProgress;

        private List<ITutorialStepHandler> _handlers;
        private int _currentIndex = 0;

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeToEvents();
        }

        private void Awake()
        {
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

        private void SubscribeToEvents()
        {
            FTUEEventManager.InitializeFTUE += StartFTUE;
            FTUEEventManager.OnCTAClicked += OnCTAClicked;
        }

        private void UnsubscribeToEvents()
        {
            FTUEEventManager.InitializeFTUE -= StartFTUE;
            FTUEEventManager.OnCTAClicked -= OnCTAClicked;
        }

        public void StartFTUE()
        {
            if (ftueProgress.ftueDebugKey)
                return;

            if (ftueProgress.currentPhase == TutorialPhase.Phase1_Intro)
            {
                _currentIndex = 0;
                StopAllCoroutines();
                StartCoroutine(RunCurrentStep());
            }
            else if (ftueProgress.currentPhase == TutorialPhase.Phase3_Other)
            {
                StartPhase3();
            }
        }

        private IEnumerator RunCurrentStep()
        {
            var steps = sequence.GetSteps(ftueProgress.currentPhase);

            if (_currentIndex >= steps.Count)
                yield break;

            var step = steps[_currentIndex];

            var handler = _handlers.FirstOrDefault(h => h.HandlesType == step.stepType);

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

        private void OnCTAClicked(CallToActionTargetType type)
        {
            JumpToStep(TutorialStepType.FreestylePrompt);
        }

        internal void StartPhase3()
        {
            // Lets start the phase 3 here! 
        }
    }
}
