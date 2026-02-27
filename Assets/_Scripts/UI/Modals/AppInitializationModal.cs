using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using Unity.Services.Core;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shows an initialization overlay until UGS authentication completes.
    /// Replaces legacy PlayFab event subscriptions with direct UGS auth polling.
    ///
    /// The bootstrap flow (BootstrapController → AppManager → AuthenticationServiceFacade)
    /// normally completes authentication before Menu_Main loads, so this modal closes
    /// almost immediately. A timeout fallback ensures the UI is never permanently blocked.
    /// </summary>
    public class AppInitializationModal : MonoBehaviour
    {
        [SerializeField] TMP_Text InitializingText;
        [SerializeField] Animator Animator;
        [SerializeField] GameObject NavBar;
        [SerializeField] GameObject Menu;
        [SerializeField] Image ProgressIndicator;
        [SerializeField] Image ProgressIndicatorBackground;

        static bool NetworkInitialized = false;

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
                // Keep NavBar and Menu enabled so scene systems (MultiplayerSetup,
                // ServerPlayerVesselInitializer, etc.) can start their lifecycle.
                // The modal overlay visually covers them until auth completes.
                StartCoroutine(WaitForAuthCoroutine());
            }
        }

        /// <summary>
        /// Polls UGS authentication state. Closes as soon as auth succeeds,
        /// or after a timeout enters offline mode and closes anyway.
        /// </summary>
        IEnumerator WaitForAuthCoroutine()
        {
            // If UGS auth is already complete (bootstrap finished before scene load), close immediately.
            if (IsUGSSignedIn())
            {
                StartCoroutine(CloseCoroutine());
                yield break;
            }

            float elapsed = 0f;
            const float timeout = 8f;
            const float pollInterval = 0.3f;

            while (elapsed < timeout)
            {
                int dots = ((int)(elapsed / pollInterval)) % 4;
                InitializingText.text = "Initializing" + new string('.', dots);

                yield return new WaitForSecondsRealtime(pollInterval);
                elapsed += pollInterval;

                if (IsUGSSignedIn())
                {
                    StartCoroutine(CloseCoroutine());
                    yield break;
                }
            }

            // Timeout reached — enter offline mode and unblock the menu.
            InitializingText.text = "Offline Mode";
            CSDebug.LogWarning("Entering Offline Mode");
            StartCoroutine(CloseCoroutine());
        }

        static bool IsUGSSignedIn()
        {
            try
            {
                return UnityServices.State == ServicesInitializationState.Initialized
                    && AuthenticationService.Instance != null
                    && AuthenticationService.Instance.IsSignedIn;
            }
            catch
            {
                return false;
            }
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
        }
    }
}