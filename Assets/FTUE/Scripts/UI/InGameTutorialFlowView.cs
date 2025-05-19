using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Events;
using CosmicShore.FTUE;
using UnityEngine.SceneManagement;

namespace CosmicShore
{
    public class InGameTutorialFlowView : MonoBehaviour
    {
        [SerializeField] private FTUEProgress _ftueProgress;
        [SerializeField] private TutorialSequenceSet _tutorialSequenceSet;
        [SerializeField] private FTUEIntroAnimatorAdapter _animatorAdapter;
        [SerializeField] private TutorialUIViewAdapter _uiViewAdapter;
        [SerializeField] private GameObject _skipButton;

        private bool _phase2Started;

        private void OnEnable()
        {
            FTUEEventManager.OnNextPressed += HandlePhase2Next;
            FTUEEventManager.OnGameModeStarted += HandleGameModeStarted;
        }

        private void OnDisable()
        {
            FTUEEventManager.OnNextPressed -= HandlePhase2Next;
            FTUEEventManager.OnGameModeStarted -= HandleGameModeStarted;
        }

        private void HandleGameModeStarted(GameModes mode)
        {
            if (mode == GameModes.Freestyle
             && _ftueProgress.currentPhase == TutorialPhase.Phase2_GameplayTimer)
            {
                CheckFTUE();
            }
        }

        /// <summary>
        /// Call when the menu opens or FTUEProgress changes.
        /// </summary>
        public void CheckFTUE()
        {
            if (_phase2Started
             || _ftueProgress.currentPhase != TutorialPhase.Phase2_GameplayTimer)
                return;

            _phase2Started = true;
            StartCoroutine(Phase2Intro());
        }

        private IEnumerator Phase2Intro()
        {
            List<TutorialStep> steps = _tutorialSequenceSet
                .GetSteps(TutorialPhase.Phase2_GameplayTimer);

            if (steps.Count == 0)
            {
                HandlePhase2Next();
                yield break;
            }

            yield return _animatorAdapter.PlayIntro();

            var stepIndex = Mathf.Clamp(_ftueProgress.nextIndex, 0, steps.Count - 1);
            var step = steps[stepIndex];
            _uiViewAdapter.ShowStep(step.tutorialText, null);
        }

        /// <summary>
        /// Called by the Next button via FTUEEventManager.
        /// </summary>
        public void HandlePhase2Next()
        {
            _uiViewAdapter.ToggleCanvas(false);

            _ftueProgress.nextIndex++;

            StartCoroutine(GameplayTimerCoroutine());
        }

        private IEnumerator GameplayTimerCoroutine()
        {
            float remaining = 60f;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }

            _ftueProgress.currentPhase = TutorialPhase.Phase3_Other;
            _skipButton.SetActive(true);
            Debug.Log("[FTUE] Phase2 (timer) complete");
        }

        public void ReturnToMainMenu()
        {
            SceneManager.LoadScene("Menu_Main");
        }
    }
}
