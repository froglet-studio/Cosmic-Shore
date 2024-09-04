using CosmicShore.App.Systems.UserActions;
using CosmicShore.App.UI.Modals;
using CosmicShore.App.UI.Screens;
using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CosmicShore.App.UI
{
    public class ScreenSwitcher : MonoBehaviour, IDragHandler, IEndDragHandler
    {
        public enum MenuScreens
        {
            STORE = 0,
            ARCADE = 1,
            HOME = 2,
            PORT = 3,
            HANGAR = 4,
        }

        public enum ModalWindows
        {
            // STORE MODALS
            PURCHASE_ITEM_CONFIRMATION,

            // ARCADE MODALS
            ARCADE_GAME_CONFIGURE,
            DAILY_CHALLENGE,

            // HOME MODALS
            PROFILE,
            PROFILE_ICON_SELECT,
            SETTINGS,
            XP_TRACK,

            // PORT MODALS
            FACTION_MISSION,
            SQUAD_MEMBER_CONFIGURE,

            // HANGAR MODALS
            HANGAR_TRAINING,
        }

        [SerializeField] float percentThreshold = 0.2f; // Sensitivity of swipe detector. Smaller number = more sensitive
        [SerializeField] float easing = 0.5f; // Makes the transition less jarring
        [SerializeField] int currentScreen; // Keeps track of how many screens you have in the menu system. From 0 to 4, home = 2

        [SerializeField] Transform NavBar;
        [SerializeField] HangarScreen HangarMenu;
        [SerializeField] LeaderboardsMenu LeaderboardMenu;

        Vector3 panelLocation;
        Coroutine navigateCoroutine;

        const int STORE = (int)MenuScreens.STORE;
        const int PORT = (int)MenuScreens.PORT;
        const int HOME = (int)MenuScreens.HOME;
        const int HANGAR = (int)MenuScreens.HANGAR;
        const int ARCADE = (int)MenuScreens.ARCADE;

        [SerializeField] Image NavBarLine;
        [SerializeField] List<Sprite> NavBarLineSprites;

        [Header("Modal Windows")]
        [SerializeField] ModalWindowManager ArcadeGameConfigureModal;
        [SerializeField] ModalWindowManager DailyChallengeModal;
        [SerializeField] ModalWindowManager HangarTrainingGameModal;
        [SerializeField] ModalWindowManager FactionMissionModal;

        static string ReturnToScreenPrefKey = "ReturnToScreen";
        static string ReturnToModalPrefKey = "ReturnToModal";

        public void SetReturnToScreen(MenuScreens screen)
        {
            PlayerPrefs.SetInt(ReturnToScreenPrefKey, (int)screen);
            PlayerPrefs.Save();
        }
        public void SetReturnToModal(ModalWindows modal)
        {
            PlayerPrefs.SetInt(ReturnToModalPrefKey, (int)modal);
            PlayerPrefs.Save();
        }
        public static void ClearReturnState()
        {
            PlayerPrefs.DeleteKey(ReturnToScreenPrefKey);
            PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod]
        static void RunOnStart()
        {
            ClearReturnState();
        }

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
                NavigateTo(HOME, false);
            }

            if (PlayerPrefs.HasKey(ReturnToModalPrefKey))
            {
                StartCoroutine(LaunchModalCoroutine());
            }
        }

        IEnumerator LaunchModalCoroutine()
        {
            yield return new WaitForEndOfFrame();
            var modal = PlayerPrefs.GetInt(ReturnToModalPrefKey);
            switch ((ModalWindows)modal)
            {
                case ModalWindows.ARCADE_GAME_CONFIGURE:
                    ArcadeGameConfigureModal.ModalWindowIn();
                    break;
                case ModalWindows.DAILY_CHALLENGE:
                    DailyChallengeModal.ModalWindowIn();
                    break;
                case ModalWindows.HANGAR_TRAINING:
                    HangarTrainingGameModal.ModalWindowIn();
                    break;
                case ModalWindows.FACTION_MISSION:
                    FactionMissionModal.ModalWindowIn();
                    break;
            }
            PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            PlayerPrefs.Save();
        }

        void Update()
        {
            if (Gamepad.current != null && Gamepad.current[GamepadButton.DpadLeft].wasPressedThisFrame)
            {
                NavigateLeft();
            }
            if (Gamepad.current != null && Gamepad.current[GamepadButton.DpadRight].wasPressedThisFrame)
            {
                NavigateRight();
            }
        }

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

        public void NavigateTo(int ScreenIndex, bool animate = true)
        {
            ScreenIndex = Mathf.Clamp(ScreenIndex, 0, transform.childCount - 1);

            if (ScreenIndex == currentScreen)
                return;

            if (ScreenIndex == ARCADE)
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeMenu);

            if (ScreenIndex == HANGAR)
            {
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewHangarMenu);
                HangarMenu.LoadView();
            }

            if (ScreenIndex == HOME)
                GameManager.UnPauseGame();
            else
                GameManager.PauseGame();

            Vector3 newLocation = new Vector3(-ScreenIndex * Screen.width, 0, 0);
            panelLocation = newLocation;

            if (animate)
            {
                GetComponent<MenuAudio>().PlayAudio();
                if (navigateCoroutine != null)
                    StopCoroutine(navigateCoroutine);
                navigateCoroutine = StartCoroutine(SmoothMove(transform.position, newLocation, easing));
            }
            else
                transform.position = newLocation;

            currentScreen = ScreenIndex;
            UpdateNavBar(currentScreen);
        }

        public void OnClickStoreNav()
        {
            NavigateTo(STORE);
        }
        public void OnClickPortNav()
        {
            LeaderboardMenu.LoadView();
            NavigateTo(PORT);
        }
        public void OnClickHomeNav()
        {
            NavigateTo(HOME);
        }
        public void OnClickHangarNav()
        {
            //HangarMenu.LoadView();
            NavigateTo(HANGAR);
        }
        public void OnClickArcadeNav()
        {
            NavigateTo(ARCADE);
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
        void UpdateNavBar(int index)
        {
            // Deselect them all
            for (var i = 0; i < NavBar.childCount; i++)
            {
                NavBar.GetChild(i).GetChild(0).gameObject.SetActive(true);
                NavBar.GetChild(i).GetChild(1).gameObject.SetActive(false);
            }

            // Select the one
            NavBar.GetChild(index).GetChild(0).gameObject.SetActive(false);
            NavBar.GetChild(index).GetChild(1).gameObject.SetActive(true);

            NavBarLine.sprite = NavBarLineSprites[index];
        }
        IEnumerator SmoothMove(Vector3 startpos, Vector3 endpos, float seconds)
        {
            float t = 0f;
            while (t <= 1.0)
            {
                t += Time.unscaledDeltaTime / seconds;
                transform.position = Vector3.Lerp(startpos, endpos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
    }
}