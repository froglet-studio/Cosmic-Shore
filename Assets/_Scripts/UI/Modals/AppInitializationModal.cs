using CosmicShore.FTUE.Helpers;
using CosmicShore.Integrations.Playfab.Authentication;
using CosmicShore.Integrations.Playfab.Economy;
using CosmicShore.Integrations.Playfab.PlayerData;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility.Recording;

namespace CosmicShore.UI.Modals
{
    public class AppInitializationModal : MonoBehaviour
    {
        [SerializeField] TMP_Text InitializingText;
        [SerializeField] Animator Animator;
        [SerializeField] GameObject NavBar;
        [SerializeField] GameObject Menu;
        [SerializeField] Image ProgressIndicator;
        [SerializeField] Image ProgressIndicatorBackground;

        static bool NetworkInitialized = false;

        void OnEnable()
        {
            AuthenticationManager.OnLoginSuccess += OnAuthenticated;
            PlayerDataController.OnProfileLoaded += OnProfileLoaded;
            CatalogManager.OnLoadCatalogSuccess += OnCatalogLoaded;
            CatalogManager.OnLoadInventory += OnInventoryLoaded;
        }

        void OnDisable()
        {
            AuthenticationManager.OnLoginSuccess -= OnAuthenticated;
            PlayerDataController.OnProfileLoaded -= OnProfileLoaded;
            CatalogManager.OnLoadCatalogSuccess -= OnCatalogLoaded;
            CatalogManager.OnLoadInventory -= OnInventoryLoaded;
        }

        void Awake()
        {
            if (NetworkInitialized)
            {
                ProgressIndicator.gameObject.SetActive(false);
                ProgressIndicatorBackground.gameObject.SetActive(false);
                NavBar.SetActive(true);
                Menu.SetActive(true);
                Animator.SetTrigger("ClosePanelTrigger");
                gameObject.SetActive(false);
            }
            else
            {
                NavBar.SetActive(false);
                Menu.SetActive(false);
                StartCoroutine(UpdateTextCoroutine());
            }
        }

        IEnumerator UpdateTextCoroutine()
        {
            var stopWatch = 0;

            while (stopWatch < 6)
            {
                InitializingText.text = "Initializing";
                yield return new WaitForSecondsRealtime(.2f);
                InitializingText.text = "Initializing.";
                yield return new WaitForSecondsRealtime(.2f);
                InitializingText.text = "Initializing..";
                yield return new WaitForSecondsRealtime(.2f);
                InitializingText.text = "Initializing...";
                yield return new WaitForSecondsRealtime(.4f);
                stopWatch++;
            }
            InitializingText.text = "Offline Mode";

            CSDebug.LogWarning("Entering Offline Mode");
            StartCoroutine(CloseCoroutine());
        }

        IEnumerator CloseCoroutine()
        {
            NetworkInitialized = true;

            yield return new WaitForSecondsRealtime(.25f);
            ProgressIndicator.gameObject.SetActive(false);
            ProgressIndicatorBackground.gameObject.SetActive(false);
            NavBar.SetActive(true);
            Menu.SetActive(true);
            Animator.SetTrigger("ClosePanelTrigger");
            yield return new WaitUntil(() => Animator.GetCurrentAnimatorStateInfo(0).IsName("ZoomClose") && Animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .8f);

            Animator.StopPlayback();
            gameObject.SetActive(false);

            FTUEEventManager.OnInitializeFTUECalled();
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
        void OnInventoryLoaded()
        {
            ProgressIndicator.rectTransform.sizeDelta = new Vector2(400, 5);
            StartCoroutine(CloseCoroutine());
        }
    }
}