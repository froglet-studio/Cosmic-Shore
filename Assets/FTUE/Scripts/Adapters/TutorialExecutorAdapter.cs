using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Systems.CTA;
using CosmicShore.App.UI;
using CosmicShore.FTUE;

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

    public void SetupPreIntroUI()
    {
        navigationBar.interactable = false;
        navigationBar.alpha = 0f;
        missionsGameObject.SetActive(false);
    }

    public void PrepareArcadeScreen()
    {
        //screenSwitcher.OnClickArcadeNav();
        LockAllExceptFreestyle();
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

    private void LockAllExceptFreestyle()
    {
        foreach (var card in gameCards)
        {
            var btn = card.GetComponentInChildren<UnityEngine.UI.Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[{nameof(LockAllExceptFreestyle)}] no Button found on {card.name}");
                continue;
            }
            else
            {
                Debug.Log("Button found");
            }

            bool isFreestyle = card.TargetID == CallToActionTargetType.PlayGameFreestyle;
            btn.interactable = isFreestyle;
        }
    }
}
