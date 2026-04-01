using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
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
            PROFILE = 5,
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

            // PORT MODALS
            FACTION_MISSION        = 7,
            SQUAD_MEMBER_CONFIGURE = 8,

            // HANGAR MODALS
            HANGAR_TRAINING        = 9,

            // ARCADE (as modal overlay)
            ARCADE                 = 10,
        }

        [System.Serializable]
        public class ScreenEntry
        {
            public MenuScreens id;
            public RectTransform root;
        }

        [Header("Swipe Settings")]
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

        [Tooltip("CanvasGroup on the Screens root. Disabled during freestyle to hide all screens without SetActive.")]
        [SerializeField] private CanvasGroup screensCanvasGroup;

        [Inject] private MenuFreestyleEventsContainerSO freestyleEvents;
        [Inject] private HostConnectionDataSO hostConnectionData;

        [Header("Disabled Screens")]
        [Tooltip("Screens in this list are skipped during navigation and cannot be opened via buttons or controller input.")]
        [SerializeField] private List<MenuScreens> disabledScreens = new() { MenuScreens.PORT, MenuScreens.ARK };

        [Header("Arcade Panel")]
        [Tooltip("Arcade modal window. Opens as overlay when Arcade nav is clicked.")]
        [SerializeField] private ModalWindowManager ArcadeModal;

        private Vector3 panelLocation;
        private Coroutine navigateCoroutine;
        private bool _isInFreestyle;

        // Cached canvas references for aspect-ratio-safe sliding
        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private MenuAudio _menuAudio;

        // Cached IScreen components per screen index for lifecycle callbacks
        private readonly Dictionary<int, IScreen> _screenMap = new();

        [Header("Nav Bar Visuals")]
        [SerializeField] private Image NavBarLine;
        [SerializeField] private List<Sprite> NavBarLineSprites;

        [Header("Modal Windows")]
        [Tooltip("All modal windows in the scene. Used for return-state restoration and closing on freestyle entry.")]
        [SerializeField] private List<ModalWindowManager> Modals;

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
        
        public bool HasActiveModal => activeModalStack.Count > 0;

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
                CSDebug.LogWarning(
                    "[ScreenSwitcher] 'screens' list is empty. " +
                    "Falling back to transform children order. " +
                    "You can manually assign screens in the inspector for full control."
                );
            }
        }

        private void OnEnable()
        {
            if (freestyleEvents)
            {
                freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleEnterFreestyle;
                freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleExitFreestyle;
            }
        }

        private void OnDisable()
        {
            if (freestyleEvents)
            {
                freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
                freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
            }
        }

        private void Start()
        {
            _rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _menuAudio = GetComponent<MenuAudio>();

            CacheScreenComponents();
            LayoutScreensToViewport();

            panelLocation = transform.position;

            if (PlayerPrefs.HasKey(ReturnToScreenPrefKey))
            {
                var screenEnumInt = PlayerPrefs.GetInt(ReturnToScreenPrefKey);
                var screenEnum = (MenuScreens)screenEnumInt;

                // Fall back to HOME if the saved screen is now disabled
                if (IsScreenDisabled(screenEnum))
                    screenEnum = MenuScreens.HOME;

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
            if (_isInFreestyle) return;
            if (Gamepad.current == null) return;
            if (HasActiveModal) return;

            if (ScreenIsActive(MenuScreens.HOME))
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    OpenArcadePanel();
                    return;
                }

                if (Gamepad.current.buttonWest.wasPressedThisFrame)
                {
                    OpenModalByType(ModalWindows.SETTINGS);
                    return;
                }
            }

            if (Gamepad.current.leftTrigger.wasPressedThisFrame)
                NavigateLeft();
            if (Gamepad.current.rightTrigger.wasPressedThisFrame)
                NavigateRight();
        }

        #endregion

        #region Drag Handling

        public void OnDrag(PointerEventData data)
        {
            //transform.position = panelLocation - new Vector3(data.pressPosition.x - data.position.x, 0, 0);
        }

        public void OnEndDrag(PointerEventData data)
        {
            // float percentage = (data.pressPosition.x - data.position.x) / Screen.width;
            //
            // if (percentage >= percentThreshold && currentScreen < GetScreenCount() - 1)
            //     NavigateRight();
            // else if (percentage <= -percentThreshold && currentScreen > 0)
            //     NavigateLeft();
            // else
            // {
            //     // Reset back to current screen
            //     if (navigateCoroutine != null)
            //         StopCoroutine(navigateCoroutine);
            //
            //     navigateCoroutine = StartCoroutine(SmoothMove(transform.position, panelLocation, easing));
            // }
        }

        #endregion

        #region Viewport Layout

        /// <summary>
        /// Returns the current viewport width in canvas units.
        /// This adapts to any aspect ratio and CanvasScaler configuration.
        /// </summary>
        private float GetViewportWidthInCanvasUnits()
        {
            if (_canvasRect != null)
                return _canvasRect.rect.width;

            // Fallback: assume 1:1 canvas-to-pixel mapping
            return Screen.width;
        }

        /// <summary>
        /// Returns the world-space (pixel) distance for one screen slide.
        /// </summary>
        private float GetSlideDistance()
        {
            if (_rootCanvas != null)
                return GetViewportWidthInCanvasUnits() * _rootCanvas.scaleFactor;

            return Screen.width;
        }

        /// <summary>
        /// Resizes and repositions each screen panel to fill the actual viewport width,
        /// so the layout works correctly at any aspect ratio.
        /// </summary>
        private void LayoutScreensToViewport()
        {
            float viewportWidth = GetViewportWidthInCanvasUnits();
            int count = GetScreenCount();

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = GetScreenRootRT(i);
                if (rt == null) continue;

                // Anchor to left edge, stretch vertically
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(viewportWidth, 0f);
                rt.anchoredPosition = new Vector2(i * viewportWidth, 0f);
            }
        }

        /// <summary>
        /// Returns the RectTransform for the screen at the given visual index.
        /// </summary>
        private RectTransform GetScreenRootRT(int index)
        {
            if (screens is { Count: > 0 } && index >= 0 && index < screens.Count)
                return screens[index]?.root;

            if (index >= 0 && index < transform.childCount)
                return transform.GetChild(index) as RectTransform;

            return null;
        }

        #endregion

        #region Screen Mapping Helpers

        private void CacheScreenComponents()
        {
            int count = GetScreenCount();
            for (int i = 0; i < count; i++)
            {
                RectTransform rt = GetScreenRootRT(i);
                if (rt == null) continue;

                // Check same GameObject first, then scan children
                var screen = rt.GetComponentInChildren<IScreen>(true);
                if (screen != null)
                    _screenMap[i] = screen;
            }
        }

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

                CSDebug.LogWarning($"[ScreenSwitcher] Screen '{screen}' not found in screens list. Falling back to enum value index.");
            }

            return (int)screen;
        }

        private bool IsScreenDisabled(MenuScreens screen)
        {
            return disabledScreens != null && disabledScreens.Contains(screen);
        }

        private bool IsIndexDisabled(int index)
        {
            return IsScreenDisabled(GetScreenIdForIndex(index));
        }

        #endregion

        #region Navigation Core

        private void NavigateTo(MenuScreens screen, bool animate = true)
        {
            // Arcade is host-only in multiplayer sessions
            if (screen == MenuScreens.ARK && !IsHostOrSolo())
                return;

            if (IsScreenDisabled(screen))
                return;

            int index = GetIndexForScreen(screen);
            NavigateTo(index, animate);
        }

        bool IsHostOrSolo()
        {
            if (hostConnectionData == null) return true;
            if (hostConnectionData.PartyMembers == null || hostConnectionData.PartyMembers.Count <= 1) return true;
            return hostConnectionData.IsHost;
        }

        private void NavigateTo(int ScreenIndex, bool animate = true)
        {
            // Block screen navigation while in freestyle mode
            if (_isInFreestyle) return;

            int max = GetScreenCount() - 1;
            if (max < 0)
            {
                CSDebug.LogError("[ScreenSwitcher] No screens available. Please configure the 'screens' list or add child panels.");
                return;
            }

            ScreenIndex = Mathf.Clamp(ScreenIndex, 0, max);

            if (IsIndexDisabled(ScreenIndex))
                return;

            if (ScreenIndex == currentScreen)
                return;

            // Notify the outgoing screen
            if (_screenMap.TryGetValue(currentScreen, out var exitingScreen))
                exitingScreen.OnScreenExit();

            // Map index → logical enum id
            MenuScreens screenId = GetScreenIdForIndex(ScreenIndex);

            // Screen-specific initialization (matches development branch)
            switch (screenId)
            {
                case MenuScreens.HANGAR:
                    UserActionSystem.Instance.CompleteAction(UserActionType.ViewHangarMenu);
                    if (HangarMenu)
                        HangarMenu.LoadView();
                    break;
                case MenuScreens.PORT:
                    if (LeaderboardMenu)
                        LeaderboardMenu.LoadView();
                    break;
            }

            // Pause game on non-HOME screens (frees CPU for UI rendering)
            if (screenId == MenuScreens.HOME)
                PauseSystem.TogglePauseGame(false);
            else
                PauseSystem.TogglePauseGame(true);

            // Notify the incoming screen
            if (_screenMap.TryGetValue(ScreenIndex, out var enteringScreen))
                enteringScreen.OnScreenEnter();

            // Slide effect: 1 viewport width per index (works at any aspect ratio)
            Vector3 newLocation = new Vector3(-ScreenIndex * GetSlideDistance(), 0, 0);
            panelLocation = newLocation;

            if (animate)
            {
                if (_menuAudio)
                    _menuAudio.PlayAudio();

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

            if (ArcadeModal)
                ArcadeModal.ModalWindowIn();
        }

        private void OpenModalByType(ModalWindows modalType)
        {
            if (Modals == null) return;

            foreach (var modal in Modals)
            {
                if (modal != null && modal.ModalType == modalType)
                {
                    modal.ModalWindowIn();
                    return;
                }
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

        public void OnClickProfileNav()
        {
            NavigateTo(MenuScreens.PROFILE);
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
            int target = currentScreen - 1;
            while (target >= 0 && IsIndexDisabled(target))
                target--;

            if (target < 0)
                return;

            NavigateTo(target);
        }

        private void NavigateRight()
        {
            int max = GetScreenCount() - 1;
            int target = currentScreen + 1;
            while (target <= max && IsIndexDisabled(target))
                target++;

            if (target > max)
                return;

            NavigateTo(target);
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
        }

        #endregion

        #region Freestyle State

        private void HandleEnterFreestyle()
        {
            _isInFreestyle = true;

            // Notify the current screen that it's being exited
            if (_screenMap.TryGetValue(currentScreen, out var exitingScreen))
                exitingScreen.OnScreenExit();

            // Close any open modals (CanvasGroup-based, no SetActive toggling)
            CloseAllModals();

            // Hide NavBar and Screens via CanvasGroup
            SetNavBarVisible(false);
            SetCanvasGroupVisible(screensCanvasGroup, false);
        }

        private void HandleExitFreestyle()
        {
            _isInFreestyle = false;

            // Close any modals that were open
            CloseAllModals();

            // Show NavBar and Screens
            SetNavBarVisible(true);
            SetCanvasGroupVisible(screensCanvasGroup, true);

            // Notify the current screen that it's being re-entered
            if (_screenMap.TryGetValue(currentScreen, out var enteringScreen))
                enteringScreen.OnScreenEnter();
        }

        private static void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
        {
            if (!cg) return;
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }

        private void SetNavBarVisible(bool visible)
        {
            if (!NavBar) return;

            if (!NavBar.TryGetComponent<CanvasGroup>(out var cg))
                cg = NavBar.gameObject.AddComponent<CanvasGroup>();

            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }

        private void CloseAllModals()
        {
            if (Modals == null) return;

            foreach (var modal in Modals)
            {
                if (!modal) continue;
                var cg = modal.GetComponent<CanvasGroup>();
                if (cg && cg.alpha > 0.01f)
                    modal.ModalWindowOut();
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
