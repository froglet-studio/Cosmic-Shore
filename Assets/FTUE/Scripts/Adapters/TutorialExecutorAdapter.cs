using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    [AddComponentMenu("FTUE/Adapters/TutorialExecutorAdapter")]
    public class TutorialExecutorAdapter : MonoBehaviour, ITutorialExecutor
    {
        [Header("Arcade Setup")]
        [SerializeField] private CanvasGroup navigationBar;
        [SerializeField] private GameObject missionsGameObject;
        [SerializeField] private List<CallToActionTarget> gameCards;
        [SerializeField] private ScreenSwitcher screenSwitcher;
        [SerializeField] private IAnimator animator;
        [SerializeField] private TutorialFlowController flowController;
        [Tooltip("The one game card left unlocked during the tutorial. Defaults to the first " +
                 "game in the quest progression chain (Scurry).")]
        [SerializeField] private CallToActionTargetType tutorialGameTarget = CallToActionTargetType.PlayGameScurry;

        public void SetupPreIntroUI()
        {
            navigationBar.interactable = false;
            navigationBar.alpha = 0f;
            missionsGameObject.SetActive(false);
        }

        public void PrepareArcadeScreen()
        {
            //screenSwitcher.OnClickArcadeNav();
            LockAllExceptTutorialGame();
        }

        public IEnumerator ExecutePayload(TutorialStepPayload payload, Action onComplete)
        {
            switch (payload.payloadType)
            {
                case PayloadType.OpenArcadeAction:
                    yield return animator.PlayOutro();
                    navigationBar.interactable = true;
                    navigationBar.alpha = 1f;
                    missionsGameObject.SetActive(true);
                    onComplete?.Invoke();
                    break;

                case PayloadType.UserChoice:
                case PayloadType.SceneActivation:
                    yield return animator.PlayOutro();
                    onComplete?.Invoke();
                    break;

                default:
                    onComplete?.Invoke();
                    break;
            }
        }

        private void LockAllExceptTutorialGame()
        {
            foreach (var card in gameCards)
            {
                var btn = card.GetComponentInChildren<UnityEngine.UI.Button>();
                if (btn == null)
                {
                    Debug.LogWarning($"[{nameof(LockAllExceptTutorialGame)}] no Button found on {card.name}");
                    continue;
                }
                else
                {
                    Debug.Log("Button found");
                }

                bool isTutorialGame = card.TargetID == tutorialGameTarget;
                btn.interactable = isTutorialGame;
            }
        }
    }
}
