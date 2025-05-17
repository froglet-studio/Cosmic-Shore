//using CosmicShore.App.Systems.CTA;
//using CosmicShore.App.UI;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//namespace CosmicShore.FTUE
//{
//    public class TutorialStepExecutor : MonoBehaviour
//    {
//        [Header("Arcade Setup")]
//        [SerializeField] private CanvasGroup navigationBar;
//        [SerializeField] private GameObject missionsGameObject;
//        [SerializeField] private List<CallToActionTarget> gameCards;
//        [SerializeField] private ScreenSwitcher screenSwitcher;
//        [SerializeField] private FTUEIntroAnimator introAnimator;
//        [SerializeField] private TutorialFlowController tutorialFlowController;

//        public void SetupPreIntroUI()
//        {
//            navigationBar.interactable = false;
//            navigationBar.alpha = 0f;
//            missionsGameObject.SetActive(false);
//        }

//        public void PrepareArcadeScreen()
//        {
//            screenSwitcher.OnClickArcadeNav();
//            LockAllExceptFreestyle();
//        }

//        public void ExecutePayload(TutorialStepPayload payload, System.Action onComplete)
//        {
//            switch (payload.payloadType)
//            {
//                case PayloadType.OpenArcadeAction:
//                    StartCoroutine(PlayOutro(() =>
//                    {
//                        navigationBar.interactable = false;
//                        navigationBar.alpha = 1f;
//                        missionsGameObject.SetActive(true);
//                        onComplete?.Invoke();
//                    }));
//                    tutorialFlowController.JumpToStep(TutorialStepType.OpenArcadeMenu);
//                    break;

//                case PayloadType.UserChoice:
//                case PayloadType.SceneActivation:
//                    StartCoroutine(PlayOutro(onComplete));
//                    break;

//                default:
//                    onComplete?.Invoke();
//                    break;
//            }
//        }

//        private IEnumerator PlayOutro(System.Action onComplete)
//        {
//            yield return introAnimator.PlayOutro(onComplete);
//        }

//        private void LockAllExceptFreestyle()
//        {
//            foreach (CallToActionTarget card in gameCards)
//            {
//                var btn = card.GetComponent<UnityEngine.UI.Button>();
//                btn.interactable = (card.TargetID == CallToActionTargetType.PlayGameFreestyle);
//            }
//        }
//    }
//}
