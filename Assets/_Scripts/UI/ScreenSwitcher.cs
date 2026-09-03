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
using UnityEngine.InputSystem.UI;
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
            // 2 was DAILY_CHALLENGE - the PlayFab-era modal, superseded by the weekly
            // challenge and deleted. Do not reuse the value: a stale ReturnToModal pref
            // could still carry it.

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

            // The Maelstrom's launch panel lives in its OWN window rather than as a second
            // panel inside ARCADE_GAME_CONFIGURE: its layout shares almost nothing with a
            // minigame card's (a clip instead of the live preview, a pool list instead of the
            // controls block). It is still driven by the ONE ArcadeGameConfigureModal - the
            // window is separate, the authority is not.
            MAELSTROM_GAME_CONFIGURE = 11,

            // The weekly challenge's leaderboard. Its own window rather than a panel inside the
            // arcade modal: it is opened from the weekly card AND has to be reachable while that
            // modal is closed, and a modal type is what ScreenSwitcher unwinds by.
            WEEKLY_CHALLENGE_LEADERBOARD = 12,

            // HOME HUB MODALS
            //
            // The home screen is a hub of four things to play: Mission, Toy Box, Arena, Arcade.
            // Each is its own modal so they can be designed, gated and shipped independently -
            // ARENA is a full arcade-shaped card grid behind a lock, MISSION is not built yet, and
            // both are opened (or refused) through the same MenuHubButton the Arcade uses.
            TOYBOX  = 13,
            ARENA   = 14,
            MISSION = 15,
        }

        [System.Serializable]
        public class ScreenEntry
        {
            public MenuScreens id;
            public RectTransform root;
        }

        /// <summary>
        /// One open modal. The owning <see cref="ModalWindowManager"/> is carried alongside
        /// the type so the stack can be unwound by identity (a type alone cannot tell two
        /// instances apart) and reconciled against what is actually on screen.
        /// </summary>
        [System.Serializable]
        private struct ModalStackEntry
        {
            public ModalWindows type;
            public ModalWindowManager modal;
        }

        [Header("Swipe Settings")]
        [SerializeField] private float easing = 0.5f;           // Slide duration

        [Header("State")]
        [SerializeField] private int currentScreen; // index into visual order
        [SerializeField] private List<ModalStackEntry> activeModalStack = new();

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

        [Header("Gamepad Freestyle Toggle")]
        [Tooltip("Crystal click handler that toggles freestyle mode. Y button (buttonNorth) invokes ToggleTransition.")]
        [SerializeField] private MenuCrystalClickHandler crystalClickHandler;
        [Tooltip("Seconds after a freestyle transition completes before Y can toggle again.")]
        [SerializeField] private float freestyleToggleCooldown = 3f;

        private Vector3 panelLocation;
        private Coroutine navigateCoroutine;
        private bool _isInFreestyle;
        private float _freestyleToggleCooldownUntil;

        // Cached canvas references for aspect-ratio-safe sliding
        private Canvas _rootCanvas;
        private RectTransform _canvasRect;
        private MenuAudio _menuAudio;

        // Cached IScreen components per screen index for lifecycle callbacks
        private readonly Dictionary<int, IScreen> _screenMap = new();

        [Header("Nav Bar Visuals")]
        [SerializeField] private Image NavBarLine;
        [SerializeField] private List<Sprite> NavBarLineSprites;

        [Header("Nav Tab Icons (optional)")]
        [Tooltip("Active images for each screen index (visual order: 0,1,2,...)")]
        [SerializeField] private List<GameObject> NavActiveImages;
        [Tooltip("Inactive images for each screen index (visual order: 0,1,2,...)")]
        [SerializeField] private List<GameObject> NavInactiveImages;

        [Header("Modal Windows")]
        [Tooltip("All modal windows in the scene. Used for return-state restoration and closing on freestyle entry.")]
        [SerializeField] private List<ModalWindowManager> Modals;

        private static readonly string ReturnToScreenPrefKey = "ReturnToScreen";
        private static readonly string ReturnToModalPrefKey  = "ReturnToModal";

        #region Modal Stack API

        public void PushModal(ModalWindows modalType, ModalWindowManager modal)
        {
            PruneClosedModals();
            activeModalStack.Add(new ModalStackEntry { type = modalType, modal = modal });
            CommitModalStackState();
        }

        /// <summary>
        /// Unwinds <paramref name="modal"/>'s entry - by identity, not by stack position, so
        /// modals closing out of order (or twice) can never remove somebody else's entry.
        /// </summary>
        public void PopModal(ModalWindows modalType, ModalWindowManager modal)
        {
            int index = modal
                ? activeModalStack.FindLastIndex(entry => entry.modal == modal)
                : activeModalStack.FindLastIndex(entry => entry.type == modalType);

            if (index >= 0)
                activeModalStack.RemoveAt(index);

            PruneClosedModals();
            CommitModalStackState();
        }

        /// <summary>
        /// Drops entries whose modal was destroyed or is no longer being shown. A modal can
        /// be closed without ModalWindowOut ever running - the Arcade panel's back button
        /// SetActive(false)s the modal root, and a scene unload destroys them outright - and
        /// a stranded entry holds <see cref="UpdateScreensInteractable"/> shut forever, which
        /// reads to the player as "every button on the menu is dead".
        /// Returns true when the stack changed.
        /// </summary>
        private bool PruneClosedModals()
        {
            bool changed = false;

            for (int i = activeModalStack.Count - 1; i >= 0; i--)
            {
                var modal = activeModalStack[i].modal;
                if (modal && modal.IsOpen) continue;

                activeModalStack.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        private void CommitModalStackState()
        {
            SetReturnToModal(activeModalStack.Count == 0 ? ModalWindows.NONE : activeModalStack.Last().type);
            UpdateScreensInteractable();
            UpdateModalStackInteractable();
        }

        /// <summary>
        /// Screens stay visible under an open modal but must not accept input - without
        /// this, buttons on the screen behind the modal remain clickable. Toggles only
        /// interactable: alpha stays 1 (screens visible behind the modal) and
        /// blocksRaycasts stays on (clicks outside the modal don't fall through to the
        /// 3D scene). Freestyle hides the whole group itself, so never fight that state.
        /// </summary>
        private void UpdateScreensInteractable()
        {
            if (!screensCanvasGroup) return;
            if (InFreestyle) return;

            screensCanvasGroup.interactable = activeModalStack.Count == 0;
        }

        /// <summary>
        /// With stacked modals (e.g. Arcade -> Arcade Game Configure) only the TOP modal
        /// may accept input; without this the window underneath keeps live buttons for
        /// clicks that get past the backdrop and for gamepad/keyboard navigation, which
        /// no raycast blocker can stop. Only modals currently in the stack are touched -
        /// closed modals stay owned by ModalWindowManager's own show/hide, and the
        /// re-promoted modal gets its input back when the one above it pops.
        /// </summary>
        private void UpdateModalStackInteractable()
        {
            for (int i = 0; i < activeModalStack.Count; i++)
            {
                var modal = activeModalStack[i].modal;
                if (!modal) continue;
                if (!modal.TryGetComponent<CanvasGroup>(out var cg)) continue;

                cg.interactable = i == activeModalStack.Count - 1;
            }
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

            return activeModalStack.Last().type == modal;
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
            TrySubscribeFreestyleEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFreestyleEvents();

            // Scene-unload safety: the UI module's actions live on a shared asset instance
            // that outlives this scene — never leave Submit/Cancel/Move disabled for the
            // next scene's appshell if we go down mid-freestyle (e.g. launching a game).
            if (_appliedFreestyleGate)
                ApplyFreestyleInputGate(false);
        }

        // Deferred-subscription pattern (CLAUDE.md ▸ DI): [Inject] fields populate AFTER
        // Awake()/OnEnable() but before Start(), so the OnEnable attempt silently no-ops on
        // scene load and Start() retries. Without the retry, _isInFreestyle and
        // sendNavigationEvents=false never engage — the appshell keeps paging screens and
        // opening panels off the gamepad while the player is flying a vessel in freestyle
        // (unnoticed until the Sparrow, whose abilities use South/East/both triggers).
        private void TrySubscribeFreestyleEvents()
        {
            if (!freestyleEvents) return;
            UnsubscribeFreestyleEvents(); // dedup guard — safe to call from both OnEnable and Start

            freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleEnterFreestyle;
            freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleExitFreestyle;
            freestyleEvents.OnGameStateTransitionEnd.OnRaised += HandleFreestyleTransitionEnd;
            freestyleEvents.OnMenuStateTransitionEnd.OnRaised += HandleFreestyleTransitionEnd;
        }

        private void UnsubscribeFreestyleEvents()
        {
            if (!freestyleEvents) return;
            freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
            freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
            freestyleEvents.OnGameStateTransitionEnd.OnRaised -= HandleFreestyleTransitionEnd;
            freestyleEvents.OnMenuStateTransitionEnd.OnRaised -= HandleFreestyleTransitionEnd;
        }

        private void Start()
        {
            // Injected fields are live now — retry the subscription OnEnable had to skip.
            TrySubscribeFreestyleEvents();

            var parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError("[ScreenSwitcher] No parent Canvas found! Screen sliding will not work.");
                return;
            }
            _rootCanvas = parentCanvas.rootCanvas;
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _menuAudio = GetComponent<MenuAudio>();

            Debug.Log($"[ScreenSwitcher] Start - rootCanvas={_rootCanvas.name}, viewport={GetViewportWidthInCanvasUnits()}, screens={GetScreenCount()}");

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
            var modalType = (ModalWindows)PlayerPrefs.GetInt(ReturnToModalPrefKey);

            // Clear immediately so a stale key never persists across scene loads
            PlayerPrefs.DeleteKey(ReturnToModalPrefKey);
            PlayerPrefs.Save();

            // Game-related modals require context (selected game, party state) that is
            // lost on scene transition - never auto-reopen them after returning from a game.
            // ARCADE is included because re-opening the arcade overlay on return causes
            // stale game configuration to resurface.
            if (modalType is ModalWindows.ARCADE_GAME_CONFIGURE
                          or ModalWindows.ARCADE)
                yield break;

            foreach (var modal in Modals.Where(modal => modal.ModalType == modalType))
            {
                modal.ModalWindowIn();
            }
        }

        private void Update()
        {
            // Self-healing input gate: never depend solely on the freestyle events having
            // been delivered (a missed subscription here is exactly how the appshell kept
            // reacting to vessel ability buttons). Read the LIVE freestyle state each frame
            // and (re)apply the EventSystem gating whenever it flips.
            bool inFreestyle = InFreestyle;
            if (inFreestyle != _appliedFreestyleGate)
                ApplyFreestyleInputGate(inFreestyle);

            // Same self-healing contract for the modal gate: a modal that went away without
            // ModalWindowOut would otherwise hold every screen non-interactable forever.
            // Ahead of the gamepad early-out below - this must run on mouse/touch too.
            if (activeModalStack.Count > 0 && PruneClosedModals())
                CommitModalStackState();

            if (Gamepad.current == null) return;

            // Y (buttonNorth) toggles freestyle from any state - checked before
            // the freestyle early-return so it works as both enter and exit.
            // A cooldown prevents accidental rapid toggling after each transition.
            if (crystalClickHandler
                && Gamepad.current.buttonNorth.wasPressedThisFrame
                && Time.unscaledTime >= _freestyleToggleCooldownUntil
                && !HasActiveModal
                && ScreenIsActive(MenuScreens.HOME))
            {
                crystalClickHandler.ToggleTransition();
                return;
            }

            if (inFreestyle) return;
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

        /// <summary>
        /// Take a party GUEST to the arcade screen because the HOST opened a card there - the one
        /// sanctioned way past the host-only guard above.
        ///
        /// <para>That guard stops a guest BROWSING the arcade and launching their own game, which
        /// is right; it also blocked the guest from ever standing on the screen the host is
        /// driving them to, so the card modal opened over whatever screen they happened to be on.
        /// Being pulled by the host is not the same act as navigating there, so this is a separate
        /// entry point rather than a hole in the guard - nothing on a guest's own UI calls it.</para>
        /// </summary>
        public void FollowHostToArcadeScreen()
        {
            if (IsScreenDisabled(MenuScreens.ARK)) return;
            if (ScreenIsActive(MenuScreens.ARK)) return;
            NavigateTo(GetIndexForScreen(MenuScreens.ARK));
        }

        bool IsHostOrSolo()
        {
            if (hostConnectionData == null) return true;
            if (hostConnectionData.PartyMembers == null || hostConnectionData.PartyMembers.Count <= 1) return true;
            return hostConnectionData.IsPartyHost;
        }

        private void NavigateTo(int ScreenIndex, bool animate = true)
        {
            // Block screen navigation while in freestyle mode (live state, not just the flag)
            if (InFreestyle)
            {
                Debug.Log($"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - in freestyle");
                return;
            }

            int max = GetScreenCount() - 1;
            if (max < 0)
            {
                CSDebug.LogError("[ScreenSwitcher] No screens available. Please configure the 'screens' list or add child panels.");
                return;
            }

            ScreenIndex = Mathf.Clamp(ScreenIndex, 0, max);

            if (IsIndexDisabled(ScreenIndex))
            {
                Debug.Log($"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - screen disabled ({GetScreenIdForIndex(ScreenIndex)})");
                return;
            }

            if (ScreenIndex == currentScreen)
            {
                Debug.Log($"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - already on this screen");
                return;
            }

            Debug.Log($"[ScreenSwitcher] NavigateTo({ScreenIndex}) - sliding from {currentScreen} to {ScreenIndex} ({GetScreenIdForIndex(ScreenIndex)})");

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

        /// <summary>
        /// Open the modal registered under <paramref name="modalType"/>, or report that none is.
        ///
        /// <para>Public because the home hub's buttons are the second caller: a hub button names a
        /// modal TYPE and lets the switcher find it, so a new hub entry is a serialized enum value
        /// plus a <c>ModalWindowManager</c> in <see cref="Modals"/> - never a direct reference
        /// wired from the button to the window, which is how a modal ends up opened by two
        /// authorities.</para>
        /// </summary>
        public bool OpenModal(ModalWindows modalType)
        {
            if (Modals == null) return false;

            foreach (var modal in Modals)
            {
                if (modal != null && modal.ModalType == modalType)
                {
                    modal.ModalWindowIn();
                    return true;
                }
            }

            CSDebug.LogWarning($"[ScreenSwitcher] No modal registered for '{modalType}' - " +
                               "add its ModalWindowManager to the Modals list.");
            return false;
        }

        private void OpenModalByType(ModalWindows modalType) => OpenModal(modalType);

        #endregion

        #region Nav Button Handlers (legacy, kept)

        public void OnClickStoreNav()
        {
            Debug.Log("[ScreenSwitcher] OnClickStoreNav");
            NavigateTo(MenuScreens.STORE);
        }

        public void OnClickPortNav()
        {
            Debug.Log("[ScreenSwitcher] OnClickPortNav");
            NavigateTo(MenuScreens.PORT);
        }

        public void OnClickHomeNav()
        {
            Debug.Log("[ScreenSwitcher] OnClickHomeNav");
            NavigateTo(MenuScreens.HOME);
        }

        public void OnClickHangarNav()
        {
            Debug.Log("[ScreenSwitcher] OnClickHangarNav");
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

        /// <summary>The home hub's Toy Box entry - the app-shell face of the freestyle toybox.</summary>
        public void OnClickToyboxNav() => OpenModal(ModalWindows.TOYBOX);

        /// <summary>The home hub's Arena entry.</summary>
        public void OnClickArenaNav() => OpenModal(ModalWindows.ARENA);

        /// <summary>The home hub's Mission entry.</summary>
        public void OnClickMissionNav() => OpenModal(ModalWindows.MISSION);

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
            // Two supported ways to highlight the active nav tab:
            //
            //  1. Explicit per-button icon lists (NavActiveImages / NavInactiveImages).
            //     Each entry is one button's Active/Inactive icon child, in screen
            //     visual order. This is the authoritative mechanism when populated
            //     because it only ever toggles the icon GameObjects - never the
            //     button GameObjects themselves.
            //
            //  2. Legacy fallback: NavBar points directly at the buttons container and
            //     each button's first two children are [inactiveIcon, activeIcon].
            //
            // The two must not run together. NavBar is also used by SetNavBarVisible to
            // hide the *entire* nav bar (gradient + line + buttons + arrows) during
            // freestyle, so it intentionally points at the outer container - which is
            // NOT the buttons container. Running the child-toggle loop against that
            // outer container would SetActive() the buttons container's children (the
            // individual button GameObjects), making a whole button disappear. So the
            // legacy loop only runs when the explicit icon lists are not configured.
            bool useExplicitImages = NavActiveImages != null && NavActiveImages.Count > 0;

            if (!useExplicitImages && NavBar)
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

            if (useExplicitImages)
            {
                for (int i = 0; i < NavActiveImages.Count; i++)
                {
                    bool isActive = (i == index);

                    if (NavActiveImages[i])
                        NavActiveImages[i].SetActive(isActive);

                    if (NavInactiveImages != null && i < NavInactiveImages.Count && NavInactiveImages[i])
                        NavInactiveImages[i].SetActive(!isActive);
                }
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

            ApplyFreestyleInputGate(true);
        }

        private void HandleFreestyleTransitionEnd()
        {
            _freestyleToggleCooldownUntil = Time.unscaledTime + freestyleToggleCooldown;
        }

        /// <summary>
        /// LIVE freestyle state: the event-driven flag OR'd with the crystal handler's own
        /// state, so gamepad gating can never desync from reality if a transition event is
        /// missed (e.g. a subscription-timing failure).
        /// </summary>
        private bool InFreestyle =>
            _isInFreestyle || (crystalClickHandler && crystalClickHandler.IsInFreestyle);

        private bool _appliedFreestyleGate;

        /// <summary>
        /// Hands the gamepad to the vessel (or back to the appshell). Idempotent — called
        /// from the transition events AND self-healed from Update on live-state flips.
        /// CanvasGroup.interactable can't do this job: the vessel HUD group stays
        /// interactable for touch, so the pad would otherwise both fly the ship AND
        /// navigate/submit the HUD.
        /// </summary>
        private void ApplyFreestyleInputGate(bool inFreestyle)
        {
            _appliedFreestyleGate = inFreestyle;

            var eventSystem = EventSystem.current;
            if (!eventSystem) return;

            if (inFreestyle)
                eventSystem.SetSelectedGameObject(null);

            // Honored by the legacy StandaloneInputModule; kept for completeness.
            eventSystem.sendNavigationEvents = !inFreestyle;

            // The InputSystemUIInputModule does not reliably honor sendNavigationEvents —
            // deterministically silence its gamepad-facing actions (move/submit/cancel)
            // while flying. Pointer/click/touch actions stay live so touch UI keeps working.
            if (eventSystem.currentInputModule is InputSystemUIInputModule module)
            {
                ToggleActionRef(module.move, !inFreestyle);
                ToggleActionRef(module.submit, !inFreestyle);
                ToggleActionRef(module.cancel, !inFreestyle);
            }
        }

        private static void ToggleActionRef(InputActionReference reference, bool enable)
        {
            var action = reference ? reference.action : null;
            if (action == null) return;
            if (enable) action.Enable();
            else action.Disable();
        }

        private void HandleExitFreestyle()
        {
            _isInFreestyle = false;

            // Close any modals that were open
            CloseAllModals();

            // Give the appshell the gamepad back.
            ApplyFreestyleInputGate(false);

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
