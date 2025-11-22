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
            STORE = 0,
            ARK = 1,
            HOME   = 2,
            PORT   = 3,
            HANGAR = 4,
        }

        public enum ModalWindows
        {
            NONE = -1,
            // STORE MODALS
            PURCHASE_ITEM_CONFIRMATION = 0,

            // ARCADE MODALS
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

        [System.Serializable]
        public class ScreenEntry
        {
            public MenuScreens id;
            public RectTransform root;
        }

        [Header("Swipe Settings")]
        [SerializeField] private float percentThreshold = 0.2f; // Smaller = more sensitive
        [SerializeField] private float easing = 0.5f;           // Slide duration

        [Header("State")]
        [SerializeField] private int currentScreen; // index into visual order
        [SerializeField] private List<ModalWindows> activeModalStack = new();

        [Header("Screens (manual mapping)")]
        [Tooltip("Explicit mapping of MenuScreens enum to their root panels.\nIf left empty, will fall back to transform children order.")]
        [SerializeField] private List<ScreenEntry> screens = new();

        [Header("Scene References")]
        [SerializeField] private Transform NavBar;
        [SerializeField] private HangarScreen HangarMenu;
        [SerializeField] private LeaderboardsMenu LeaderboardMenu;

        [Header("Arcade Panel (separate)")]
        [Tooltip("Root GameObject for the Arcade panel/modal. It should start disabled and will be enabled when the Arcade tab is clicked.")]
        [SerializeField] private GameObject arcadePanelRoot;

        private Vector3 panelLocation;
        private Coroutine navigateCoroutine;

        // Old constants kept for compatibility
        private const int STORE  = (int)MenuScreens.STORE;
        private const int ARCADE = (int)MenuScreens.ARK;
        private const int HOME   = (int)MenuScreens.HOME;
        private const int PORT   = (int)MenuScreens.PORT;
        private const int HANGAR = (int)MenuScreens.HANGAR;

        [Header("Nav Bar Line")]
        [SerializeField] private Image NavBarLine;
        [SerializeField] private List<Sprite> NavBarLineSprites;

        [Header("Nav Tab Icons (optional)")]
        [Tooltip("Active images for each screen index (visual order: 0,1,2,...)")]
        [SerializeField] private List<GameObject> NavActiveImages;
        [Tooltip("Inactive images for each screen index (visual order: 0,1,2,...)")]
        [SerializeField] private List<GameObject> NavInactiveImages;

        [Header("Modal Windows")]
        [SerializeField] private List<ModalWindowManager> Modals;
        [SerializeField] private ModalWindowManager ArcadeGameConfigureModal;
        [SerializeField] private ModalWindowManager DailyChallengeModal;
        [SerializeField] private ModalWindowManager HangarTrainingGameModal;
        [SerializeField] private ModalWindowManager FactionMissionModal;

        private static readonly string ReturnToScreenPrefKey = "ReturnToScreen";
        private static readonly string ReturnToModalPrefKey  = "ReturnToModal";

        #region Modal Stack API

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

            SetReturnToModal(activeModalStack.Count == 0 ? ModalWindows.NONE : activeModalStack.Last());
        }

        #endregion

        #region Return State / Queries

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

        private static void ClearReturnState()
        {
            PlayerPrefs.DeleteKey(ReturnToScreenPrefKey);
            PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            PlayerPrefs.Save();
        }
        
        public bool ScreenIsActive(MenuScreens screen)
        {
            return GetScreenIdForIndex(currentScreen) == screen;
        }

        public bool ModalIsActive(ModalWindows modal)
        {
            if (activeModalStack.Count == 0)
                return false;

            return activeModalStack.Last() == modal;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void RunOnStart()
        {
            ClearReturnState();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (screens == null || screens.Count == 0)
            {
                Debug.LogWarning(
                    "[ScreenSwitcher] 'screens' list is empty. " +
                    "Falling back to transform children order. " +
                    "You can manually assign screens in the inspector for full control."
                );
            }
        }

        private void Start()
        {
            panelLocation = transform.position;

            if (PlayerPrefs.HasKey(ReturnToScreenPrefKey))
            {
                var screenEnumInt = PlayerPrefs.GetInt(ReturnToScreenPrefKey);
                var screenEnum = (MenuScreens)screenEnumInt;
                NavigateTo(screenEnum, false);
                PlayerPrefs.DeleteKey(ReturnToScreenPrefKey);
                PlayerPrefs.Save();
            }
            else
            {
                NavigateTo(MenuScreens.HOME, false);
            }

            if (PlayerPrefs.HasKey(ReturnToModalPrefKey))
            {
                StartCoroutine(LaunchModalCoroutine());
            }
        }

        private IEnumerator LaunchModalCoroutine()
        {
            yield return new WaitForEndOfFrame();
            var modalType = PlayerPrefs.GetInt(ReturnToModalPrefKey);
            foreach (var modal in Modals.Where(modal => modal.ModalType == (ModalWindows)modalType))
            {
                modal.ModalWindowIn();
            }
        }

        private void Update()
        {
            if (Gamepad.current == null) return;
            if (Gamepad.current.leftTrigger.wasPressedThisFrame)
                NavigateLeft();
            if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                NavigateRight();
        }

        #endregion

        #region Drag Handling

        public void OnDrag(PointerEventData data)
        {
            transform.position = panelLocation - new Vector3(data.pressPosition.x - data.position.x, 0, 0);
        }

        public void OnEndDrag(PointerEventData data)
        {
            float percentage = (data.pressPosition.x - data.position.x) / Screen.width;

            if (percentage >= percentThreshold && currentScreen < GetScreenCount() - 1)
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

        #region Screen Mapping Helpers

        private int GetScreenCount()
        {
            if (screens != null && screens.Count > 0)
                return screens.Count;

            return transform.childCount;
        }

        private MenuScreens GetScreenIdForIndex(int index)
        {
            if (screens is not { Count: > 0 }) return (MenuScreens)index;
            if (index >= 0 && index < screens.Count && screens[index] != null)
                return screens[index].id;

            // Fallback: assume enum value matches visual index
            return (MenuScreens)index;
        }

        private int GetIndexForScreen(MenuScreens screen)
        {
            if (screens != null && screens.Count > 0)
            {
                int idx = screens.FindIndex(s => s != null && s.id == screen);
                if (idx >= 0) return idx;

                Debug.LogWarning($"[ScreenSwitcher] Screen '{screen}' not found in screens list. Falling back to enum value index.");
            }

            return (int)screen;
        }

        #endregion

        #region Navigation Core

        private void NavigateTo(MenuScreens screen, bool animate = true)
        {

            // if (screen == MenuScreens.ARK)
            // {
            //     OpenArcadePanel();
            //     return;
            // }

            int index = GetIndexForScreen(screen);
            NavigateTo(index, animate);
        }

        private void NavigateTo(int ScreenIndex, bool animate = true)
        {
            int max = GetScreenCount() - 1;
            if (max < 0)
            {
                Debug.LogError("[ScreenSwitcher] No screens available. Please configure the 'screens' list or add child panels.");
                return;
            }

            ScreenIndex = Mathf.Clamp(ScreenIndex, 0, max);

            if (ScreenIndex == currentScreen)
                return;

            // Map index → logical enum id
            MenuScreens screenId = GetScreenIdForIndex(ScreenIndex);

            switch (screenId)
            {
                // If someone tries to navigate to the ARCADE index,
                // treat it as opening the separate arcade panel instead of sliding.
                // case MenuScreens.ARK:
                //     OpenArcadePanel();
                //     return;
                case MenuScreens.HANGAR:
                {
                    UserActionSystem.Instance.CompleteAction(UserActionType.ViewHangarMenu);
                    if (HangarMenu)
                        HangarMenu.LoadView();
                    break;
                }
            }

            if (screenId == MenuScreens.HOME)
                PauseSystem.TogglePauseGame(false);
            else
                PauseSystem.TogglePauseGame(true);

            // Slide effect: 1 screen width per index
            Vector3 newLocation = new Vector3(-ScreenIndex * Screen.width, 0, 0);
            panelLocation = newLocation;

            if (animate)
            {
                var menuAudio = GetComponent<MenuAudio>();
                if (menuAudio)
                    menuAudio.PlayAudio();

                if (navigateCoroutine != null)
                    StopCoroutine(navigateCoroutine);
                navigateCoroutine = StartCoroutine(SmoothMove(transform.position, newLocation, easing));
            }
            else
            {
                transform.position = newLocation;
            }

            currentScreen = ScreenIndex;
            SetReturnToScreen(screenId);
            UpdateNavBar(currentScreen);
        }

        #endregion

        #region Arcade Panel Logic

        private void OpenArcadePanel()
        {
            UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeMenu);
            if (arcadePanelRoot)
            {
                arcadePanelRoot.SetActive(true);
            }
        }

        #endregion

        #region Nav Button Handlers (legacy, kept)

        public void OnClickStoreNav()
        {
            NavigateTo(MenuScreens.STORE);
        }

        public void OnClickPortNav()
        {
            if (LeaderboardMenu != null)
                LeaderboardMenu.LoadView();

            NavigateTo(MenuScreens.PORT);
        }

        public void OnClickHomeNav()
        {
            NavigateTo(MenuScreens.HOME);
        }

        public void OnClickHangarNav()
        {
            NavigateTo(MenuScreens.HANGAR);
        }

        public void OnClickArkNav()
        {
            NavigateTo(MenuScreens.ARK);
        }

        public void OnClickArcadeNav()
        {
            OpenArcadePanel();
        }

        public void OnClickLeftArrow()
        {
            NavigateLeft();
        }

        public void OnClickRightArrow()
        {
            NavigateRight();
        }

        private void NavigateLeft()
        {
            if (currentScreen <= 0)
                return;

            NavigateTo(currentScreen - 1);
        }

        private void NavigateRight()
        {
            if (currentScreen >= GetScreenCount() - 1)
                return;

            NavigateTo(currentScreen + 1);
        }


        #endregion

        #region NavBar & Icons

        private void UpdateNavBar(int index)
        {
            if (NavBar)
            {
                for (var i = 0; i < NavBar.childCount; i++)
                {
                    var child = NavBar.GetChild(i);
                    if (child.childCount < 2) continue;

                    child.GetChild(0).gameObject.SetActive(true);
                    child.GetChild(1).gameObject.SetActive(false);
                }

                if (index >= 0 && index < NavBar.childCount)
                {
                    var active = NavBar.GetChild(index);
                    if (active.childCount >= 2)
                    {
                        active.GetChild(0).gameObject.SetActive(false);
                        active.GetChild(1).gameObject.SetActive(true);
                    }
                }
            }

            if (NavBarLine &&
                NavBarLineSprites != null &&
                index >= 0 && index < NavBarLineSprites.Count)
            {
                NavBarLine.sprite = NavBarLineSprites[index];
            }

            for (int i = 0; i < NavActiveImages.Count; i++)
            {
                bool isActive = (i == index);

                if (NavActiveImages[i])
                    NavActiveImages[i].SetActive(isActive);

                if (i < NavInactiveImages.Count && NavInactiveImages[i])
                    NavInactiveImages[i].SetActive(!isActive);
            }
        }

        #endregion

        #region Helpers

        private IEnumerator SmoothMove(Vector3 startpos, Vector3 endpos, float seconds)
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
