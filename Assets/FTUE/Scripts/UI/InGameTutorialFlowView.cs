using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class InGameTutorialFlowView : MonoBehaviour
    {
        [SerializeField] private FTUEProgress _ftueProgress;
        [SerializeField] private TutorialSequenceSet _tutorialSequenceSet;
        [SerializeField] private GameObject _skipButton;

        private bool _timerStarted;

        /// <summary>
        /// Call this whenever the menu opens or progress might have changed.
        /// </summary>
        public void CheckFTUE()
        {
            // Only start once, and only in Phase
            if (!_timerStarted
             && _ftueProgress.currentPhase == TutorialPhase.Phase2_GameplayTimer)
            {
                _timerStarted = true;
                StartCoroutine(GameplayTimerCoroutine());
            }
        }

        private IEnumerator GameplayTimerCoroutine()
        {
            float remaining = 60f;
            while (remaining > 0f)
            {
                remaining -= Time.deltaTime;
                yield return null;
            }

            // Phase2 done
            _ftueProgress.currentPhase = TutorialPhase.Phase3_Other;
            _skipButton.SetActive(true);  // for example
            Debug.Log("[FTUE] Phase2 (timer) complete");
        }
    }
}
