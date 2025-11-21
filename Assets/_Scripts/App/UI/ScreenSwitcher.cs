using CosmicShore.App.Systems.UserActions;
using CosmicShore.App.UI.Modals;
using CosmicShore.App.UI.Screens;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.App.UI
{
    public class ScreenSwitcher : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        public enum MenuScreens
        {
            HANGAR = 0,
            ARK    = 1,
            HOME   = 2,
            PORT   = 3,
            PROFILE= 4,
        }

        public enum ModalWindows
        {
            NONE = -1,

            // OLD STORE MODALS
            PURCHASE_ITEM_CONFIRMATION = 0,

            // ARCADE MODALS (now modal, not a screen)
            ARCADE_GAME_CONFIGURE = 1,
            DAILY_CHALLENGE       = 2,

            // HOME MODALS
            PROFILE                = 3,
            PROFILE_ICON_SELECT    = 4,
            SETTINGS               = 5,
            XP_TRACK               = 6,

            // PORT MODALS
            FACTION_MISSION        = 7,
            SQUAD_MEMBER_CONFIGURE = 8,

            // HANGAR MODALS
            HANGAR_TRAINING        = 9,
        }

        [SerializeField] float percentThreshold = 0.2f;
        [SerializeField] float easing = 0.5f;
        [SerializeField] int currentScreen;
        [SerializeField] List<ModalWindows> activeModalStack = new();

        [SerializeField] Transform NavBar;
        [SerializeField] HangarScreen HangarMenu;
        [SerializeField] LeaderboardsMenu LeaderboardMenu;

        Vector3 panelLocation;
        Coroutine navigateCoroutine;

        // New constant mapping for reordered screens
        const int HANGAR_SCREEN  = (int)MenuScreens.HANGAR;
        const int ARK_SCREEN     = (int)MenuScreens.ARK;
        const int HOME_SCREEN    = (int)MenuScreens.HOME;
        const int PORT_SCREEN    = (int)MenuScreens.PORT;
        const int PROFILE_SCREEN = (int)MenuScreens.PROFILE;

        [SerializeField] Image NavBarLine;
        [SerializeField] List<Sprite> NavBarLineSprites;

        [Header("Nav Tab Icons (optional)")]
        [Tooltip("Active images for each screen index (0=Hangar,1=Ark,2=Home,3=Port,4=Profile)")]
        [SerializeField] List<GameObject> NavActiveImages;
        [Tooltip("Inactive images for each screen index (0=Hangar,1=Ark,2=Home,3=Port,4=Profile)")]
        [SerializeField] List<GameObject> NavInactiveImages;

        [Header("Modal Windows")]
        [SerializeField] List<ModalWindowManager> Modals;
        [SerializeField] ModalWindowManager ArcadeGameConfigureModal;
        [SerializeField] ModalWindowManager DailyChallengeModal;
        [SerializeField] ModalWindowManager HangarTrainingGameModal;
        [SerializeField] ModalWindowManager FactionMissionModal;
        [Header("Arcade")]
        [SerializeField] private GameObject arcadeModalRoot;


        static string ReturnToScreenPrefKey = "ReturnToScreen";
        static string ReturnToModalPrefKey = "ReturnToModal";

        #region Modal Stack

        public void PushModal(ModalWindows modalType)
        {
            activeModalStack.Add(modalType);
            SetReturnToModal(activeModalStack.Last());
        }

        public void PopModal()
        {
            if (activeModalStack.Count == 0)
                return;

            activeModalStack.RemoveAt(activeModalStack.Count - 1);

            if (activeModalStack.Count == 0)
                SetReturnToModal(ModalWindows.NONE);
            else
                SetReturnToModal(activeModalStack.Last());
        }

        #endregion

        #region Return State

        public void SetReturnToScreen(MenuScreens screen)
        {
            PlayerPrefs.SetInt(ReturnToScreenPrefKey, (int)screen);
            PlayerPrefs.Save();
        }

        public void SetReturnToModal(ModalWindows modal)
        {
            if (modal == ModalWindows.NONE)
                PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            else
                PlayerPrefs.SetInt(ReturnToModalPrefKey, (int)modal);
            PlayerPrefs.Save();
        }
        
        public static void ClearReturnState()
        {
            PlayerPrefs.DeleteKey(ReturnToScreenPrefKey);
            PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            PlayerPrefs.Save();
        }
        
        public bool ScreenIsActive(MenuScreens screen)
        {
            return currentScreen == (int)screen;
        }

        public bool ModalIsActive(ModalWindows modal)
        {
            if (activeModalStack.Count == 0)
                return false;

            return activeModalStack.Last() == modal;
        }

        [RuntimeInitializeOnLoadMethod]
        static void RunOnStart()
        {
            ClearReturnState();
        }

        #endregion

        #region Lifecycle

        void Start()
        {
            if (PlayerPrefs.HasKey(ReturnToScreenPrefKey))
            {
                var screen = PlayerPrefs.GetInt(ReturnToScreenPrefKey);
                NavigateTo(screen, false);
                PlayerPrefs.DeleteKey(ReturnToScreenPrefKey);
                PlayerPrefs.Save();
            }
            else
            {
                // default to HOME in the new order (index 2)
                NavigateTo(HOME_SCREEN, false);
            }

            if (PlayerPrefs.HasKey(ReturnToModalPrefKey))
            {
                StartCoroutine(LaunchModalCoroutine());
            }
        }

        IEnumerator LaunchModalCoroutine()
        {
            yield return new WaitForEndOfFrame();
            var modalType = PlayerPrefs.GetInt(ReturnToModalPrefKey);
            foreach (var modal in Modals)
            {
                if (modal.ModalType == (ModalWindows)modalType)
                    modal.ModalWindowIn();
            }
        }

        void Update()
        {
            if (Gamepad.current != null)
            {
                if (Gamepad.current.leftTrigger.wasPressedThisFrame)
                    NavigateLeft();
                if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                    NavigateRight();
            }
        }

        #endregion

        #region Drag

        public void OnDrag(PointerEventData data)
        {
            transform.position = panelLocation - new Vector3(data.pressPosition.x - data.position.x, 0, 0);
        }

        public void OnEndDrag(PointerEventData data)
        {
            float percentage = (data.pressPosition.x - data.position.x) / Screen.width;

            if (percentage >= percentThreshold && currentScreen < transform.childCount - 1)
                NavigateRight();
            else if (percentage <= -percentThreshold && currentScreen > 0)
                NavigateLeft();
            else
            {
                // Reset back to current screen
                if (navigateCoroutine != null)
                    StopCoroutine(navigateCoroutine);

                navigateCoroutine = StartCoroutine(SmoothMove(transform.position, panelLocation, easing));
            }
        }

        #endregion

        #region Navigation Core

        public void NavigateTo(int ScreenIndex, bool animate = true)
        {
            ScreenIndex = Mathf.Clamp(ScreenIndex, 0, transform.childCount - 1);

            if (ScreenIndex == currentScreen)
                return;

            // Per-screen side-effects in new order
            if (ScreenIndex == HANGAR_SCREEN && HangarMenu != null)
            {
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewHangarMenu);
                HangarMenu.LoadView();
            }

            // Pause behaviour – same idea as before:
            // HOME = unpaused, everything else = paused
            if (ScreenIndex == HOME_SCREEN)
                PauseSystem.TogglePauseGame(false);
            else
                PauseSystem.TogglePauseGame(true);

            Vector3 newLocation = new Vector3(-ScreenIndex * Screen.width, 0, 0);
            panelLocation = newLocation;

            if (animate)
            {
                var audio = GetComponent<MenuAudio>();
                if (audio != null)
                    audio.PlayAudio();

                if (navigateCoroutine != null)
                    StopCoroutine(navigateCoroutine);
                navigateCoroutine = StartCoroutine(SmoothMove(transform.position, newLocation, easing));
            }
            else
            {
                transform.position = newLocation;
            }

            currentScreen = ScreenIndex;
            SetReturnToScreen((MenuScreens)currentScreen);
            UpdateNavBar(currentScreen);
        }

        public void NavigateLeft()
        {
            if (currentScreen <= 0)
                return;

            NavigateTo(currentScreen - 1);
        }

        public void NavigateRight()
        {
            if (currentScreen >= transform.childCount - 1)
                return;

            NavigateTo(currentScreen + 1);
        }

        #endregion

        #region Nav Button Handlers (updated mapping)

        // This was originally "Store" – now first tab = Hangar
        public void OnClickStoreNav()
        {
            NavigateTo(HANGAR_SCREEN);
        }

        // New Ark screen
        public void OnClickArkNav()
        {
            NavigateTo(ARK_SCREEN);
        }

        public void OnClickHomeNav()
        {
            NavigateTo(HOME_SCREEN);
        }

        public void OnClickPortNav()
        {
            if (LeaderboardMenu != null)
                LeaderboardMenu.LoadView();

            NavigateTo(PORT_SCREEN);
        }

        public void OnClickProfileNav()
        {
            NavigateTo(PROFILE_SCREEN);
        }

        // Arcade is now a separate modal (no longer a screen)
        public void OnClickArcadeNav()
        {
            OpenArcadeModal();
        }

        // Small arrow buttons
        public void OnClickLeftArrow()
        {
            NavigateLeft();
        }

        public void OnClickRightArrow()
        {
            NavigateRight();
        }

        #endregion

        #region Arcade Modal

        void OpenArcadeModal()
        {
            if (arcadeModalRoot != null)
                arcadeModalRoot.SetActive(true);

            // If you still want analytics + pause:
            UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeMenu);
            //PauseSystem.TogglePauseGame(true);
        }

        #endregion

        #region NavBar & Icons

        void UpdateNavBar(int index)
        {
            // Existing behaviour: switch child[0]/child[1] under NavBar
            for (var i = 0; i < NavBar.childCount; i++)
            {
                NavBar.GetChild(i).GetChild(0).gameObject.SetActive(true);
                NavBar.GetChild(i).GetChild(1).gameObject.SetActive(false);
            }

            if (index >= 0 && index < NavBar.childCount)
            {
                NavBar.GetChild(index).GetChild(0).gameObject.SetActive(false);
                NavBar.GetChild(index).GetChild(1).gameObject.SetActive(true);
            }

            if (NavBarLine != null &&
                NavBarLineSprites != null &&
                index >= 0 && index < NavBarLineSprites.Count)
            {
                NavBarLine.sprite = NavBarLineSprites[index];
            }

            // NEW: active/inactive icon pairs
            for (int i = 0; i < NavActiveImages.Count; i++)
            {
                bool isActive = (i == index);

                if (NavActiveImages[i] != null)
                    NavActiveImages[i].SetActive(isActive);

                if (i < NavInactiveImages.Count && NavInactiveImages[i] != null)
                    NavInactiveImages[i].SetActive(!isActive);
            }
        }

        #endregion

        #region Helpers

        IEnumerator SmoothMove(Vector3 startpos, Vector3 endpos, float seconds)
        {
            float t = 0f;
            while (t <= 1.0f)
            {
                t += Time.unscaledDeltaTime / seconds;
                transform.position = Vector3.Lerp(startpos, endpos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        #endregion
    }
}
