using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Economy;
using CosmicShore.Integrations.PlayFab.PlayerData;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Modals
{
    public class AppInitializationModal : MonoBehaviour
    {
        [SerializeField] TMP_Text InititizingText;
        [SerializeField] Animator Animator;
        [SerializeField] Image ProgressIndicator;
        [SerializeField] Image ProgressIndicatorBackground;

        void OnEnable()
        {
            AuthenticationManager.OnLoginSuccess += OnAuthenticated;
            PlayerDataController.OnProfileLoaded += OnProfileLoaded;
            CatalogManager.OnLoadCatalogSuccess += OnCatalogLoaded;
            CatalogManager.OnLoadInventory += OnInventoryLoaded;
        }

        void Awake()
        {
            //PauseSystem.TogglePauseGame();
            //Time.timeScale = 0;
            StartCoroutine(UpdateTextCoroutine());
        }

        IEnumerator UpdateTextCoroutine()
        {
            var stopWatch = 0;

            while (stopWatch < 6)
            {
                InititizingText.text = "Inititizing";
                yield return new WaitForSecondsRealtime(.2f);
                InititizingText.text = "Inititizing.";
                yield return new WaitForSecondsRealtime(.2f);
                InititizingText.text = "Inititizing..";
                yield return new WaitForSecondsRealtime(.2f);
                InititizingText.text = "Inititizing...";
                yield return new WaitForSecondsRealtime(.4f);
                stopWatch++;
            }
            InititizingText.text = "Offline Mode";

            Debug.LogWarning("Entering Offline Mode");
            StartCoroutine(CloseCoroutine());
        }

        IEnumerator CloseCoroutine()
        {
            yield return new WaitForSecondsRealtime(.25f);
            ProgressIndicator.gameObject.SetActive(false);
            ProgressIndicatorBackground.gameObject.SetActive(false);
            Animator.SetTrigger("ClosePanelTrigger");
            yield return new WaitUntil(() => Animator.GetCurrentAnimatorStateInfo(0).IsName("ZoomClose") && Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .8f);

            Animator.StopPlayback();
            gameObject.SetActive(false);
        }

        void OnAuthenticated()
        {
            ProgressIndicator.rectTransform.sizeDelta = new Vector2(100, 5);
        }
        void OnProfileLoaded() 
        { 
            ProgressIndicator.rectTransform.sizeDelta = new Vector2(200, 5);
        }

        void OnCatalogLoaded()
        { 
            ProgressIndicator.rectTransform.sizeDelta = new Vector2(300, 5);
        }
        void OnInventoryLoaded() {
            ProgressIndicator.rectTransform.sizeDelta = new Vector2(400, 5);
            StartCoroutine(CloseCoroutine());

            //Time.timeScale = 1;
        }
    }
}
