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

            // RETIRED. PlayerDataSelectModal used to answer to this while the older ProfileModal
            // held PROFILE; the older one is retired and its replacement now IS the profile modal,
            // so it answers to PROFILE. The value stays reserved rather than reused, for the same
            // reason as the deleted member above: a stale ReturnToModal pref carrying 4 must find
            // nothing, not somebody else's window.
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

            // The Toy Box's second window: one toy, its description, and the button that takes
            // the player to it in the lava lamp. Its own modal TYPE rather than a panel inside
            // TOYBOX, for the reason the Maelstrom's launch panel is its own window - the two
            // layouts share almost nothing, and a modal type is what ScreenSwitcher unwinds by,
            // so gamepad B out of the toy lands back on the grid instead of closing the Toy Box.
            TOYBOX_CONFIGURE = 16,

            // The Arena's launch window. Its own modal TYPE for the reason the Maelstrom's is:
            // the window is separate (it carries the vessel picker an arcade card has no use
            // for), the authority is not - it is still driven by the ONE ArcadeGameConfigureModal
            // through an ArenaLaunchPanel whose HostModal is this window.
            ARENA_GAME_CONFIGURE = 17,
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
        [Tooltip("Screens in this list are skipped during navigation and cannot be opened via buttons or controller input.\n" +
                 "Their nav-bar links are marked MenuAvailability.Locked at Start, so they READ as locked " +
                 "rather than looking enabled and doing nothing. This list stays the single source of truth - " +
                 "adding a screen here is all it takes.")]
        [SerializeField] private List<MenuScreens> disabledScreens = new() { MenuScreens.PORT, MenuScreens.ARK };

        [Tooltip("Reason a disabled screen's nav link gives when pressed. Empty leaves the refusal sting to " +
                 "speak alone. Deliberately generic: MenuScreens names (ARK, PORT) are internal.")]
        [SerializeField] private string disabledScreenMessage = "Not open yet.";

        [Header("Arcade Panel")]
        [Tooltip("Arcade modal window. Opens as overlay when Arcade nav is clicked.")]
        [SerializeField] private ModalWindowManager ArcadeModal;

        [Header("Home Hub Buttons")]
        [Tooltip("Seconds the four home-hub buttons (Mission / Toy Box / Arena / Arcade) take to fade " +
                 "OUT when any modal opens and back IN when the last one closes on HOME. 0 snaps. " +
                 "The buttons' shared parent gets a CanvasGroup (added if missing) - no scene wiring.")]
        [SerializeField] private float hubButtonsFadeSeconds = 0.2f;
        [Tooltip("Objects that stand down WITH the hub buttons - the HOME header's avatar and username " +
                 "today. Each gets a CanvasGroup (added if missing) and follows the same fade, so a " +
                 "window never opens over a live profile chip. Empty entries are ignored.")]
        [SerializeField] private List<GameObject> hubCompanions = new();

        [Header("Gamepad Freestyle Toggle")]
        [Tooltip("Crystal click handler that toggles freestyle mode. Y button (buttonNorth) invokes ToggleTransition.")]
        [SerializeField] private MenuCrystalClickHandler crystalClickHandler;
        [Tooltip("Seconds after a freestyle transition completes before Y can toggle again.")]
        [SerializeField] private float freestyleToggleCooldown = 3f;

        private Vector3 panelLocation;
        private Coroutine navigateCoroutine;
        private bool _isInFreestyle;
        private float _freestyleToggleCooldownUntil;

        // The hub row's CanvasGroup (the four MenuHubButtons' shared parent), every companion's,
        // and their one shared fade.
        private readonly List<CanvasGroup> _hubGroups = new();
        private bool _hubGroupsResolved;
        private Coroutine _hubButtonsFade;

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
            UpdateHubButtonsVisibility();
            Refocus();
        }

        #region Home hub buttons

        /// <summary>
        /// The four hub buttons live under ONE parent on the HOME screen; that parent's
        /// CanvasGroup (added here if the scene authored none) is what the gate drives. Found by
        /// component rather than wired, so a hub entry added or moved in the scene is covered.
        /// The <see cref="hubCompanions"/> (avatar, username) join the same list, so one fade
        /// moves the whole header.
        /// </summary>
        private void ResolveHubButtonsGroup()
        {
            if (_hubGroupsResolved) return;
            _hubGroupsResolved = true;
            _hubGroups.Clear();

            var hub = GetComponentInChildren<MenuHubButton>(true);
            if (hub && hub.transform.parent)
                _hubGroups.Add(EnsureGroup(hub.transform.parent.gameObject));

            foreach (var companion in hubCompanions)
                if (companion) _hubGroups.Add(EnsureGroup(companion));
        }

        private static CanvasGroup EnsureGroup(GameObject go)
        {
            if (!go.TryGetComponent<CanvasGroup>(out var cg))
                cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>
        /// The hub buttons are visible only with NO modal open, on HOME, outside freestyle - they
        /// fade out the moment any window opens over them and come back when the last one
        /// closes. Interactable / raycasts follow immediately so a fading-out button cannot take
        /// a click through the incoming window. Freestyle hides the whole screens group itself,
        /// so this never has to fight that state.
        /// </summary>
        private void UpdateHubButtonsVisibility()
        {
            ResolveHubButtonsGroup();
            if (_hubGroups.Count == 0) return;

            bool visible = activeModalStack.Count == 0
                        && !InFreestyle
                        && GetScreenIdForIndex(currentScreen) == MenuScreens.HOME;

            foreach (var group in _hubGroups)
            {
                if (!group) continue;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }

            float target = visible ? 1f : 0f;
            if (_hubButtonsFade != null)
            {
                StopCoroutine(_hubButtonsFade);
                _hubButtonsFade = null;
            }

            if (hubButtonsFadeSeconds <= 0f || !isActiveAndEnabled)
            {
                SetHubAlpha(target);
                return;
            }
            _hubButtonsFade = StartCoroutine(FadeHubButtons(target));
        }

        private void SetHubAlpha(float alpha)
        {
            foreach (var group in _hubGroups)
                if (group) group.alpha = alpha;
        }

        // Unscaled: the menu sits at timeScale 0 on every non-HOME screen and under most modals.
        // Every group fades from the ROW's current alpha, so a companion added mid-fade lands
        // with the row rather than a beat behind it.
        private IEnumerator FadeHubButtons(float target)
        {
            float from = _hubGroups[0] ? _hubGroups[0].alpha : 1f - target;
            float elapsed = 0f;
            while (elapsed < hubButtonsFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetHubAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / hubButtonsFadeSeconds)));
                yield return null;
            }
            SetHubAlpha(target);
            _hubButtonsFade = null;
        }

        #endregion

        #region Gamepad focus

        /// <summary>
        /// Put the EventSystem's selection where the pad should be: inside the top modal when one
        /// is open, else on the current screen. Nothing else in the appshell may call Select()
        /// on its own schedule - the arcade modal used to select its toggle at Start, from inside
        /// a window at alpha 0, so A on the home screen opened the first game card.
        /// </summary>
        private void Refocus()
        {
            if (InFreestyle) return; // the pad belongs to the vessel; ApplyFreestyleInputGate cleared it
            var eventSystem = EventSystem.current;
            if (!eventSystem) return;

            GameObject scope = activeModalStack.Count > 0 && activeModalStack.Last().modal
                ? activeModalStack.Last().modal.gameObject
                : CurrentScreenRoot();
            if (!scope) return;

            // Already inside the right scope: leave the player's own D-pad position alone.
            var current = eventSystem.currentSelectedGameObject;
            if (current && current.activeInHierarchy && current.transform.IsChildOf(scope.transform))
                return;

            var target = PreferredSelectable(scope);
            if (target) eventSystem.SetSelectedGameObject(target.gameObject);
        }

        private GameObject CurrentScreenRoot()
        {
            if (screens == null || currentScreen < 0 || currentScreen >= screens.Count) return null;
            var root = screens[currentScreen].root;
            return root ? root.gameObject : null;
        }

        /// <summary>
        /// The first control worth landing on. On a hub screen that is the first AVAILABLE hub
        /// entry (Arcade), so A does what the screen is for; a Locked or Unavailable entry is still
        /// reachable by D-pad but is not where the pad starts. Elsewhere, the first live Selectable.
        /// </summary>
        private static Selectable PreferredSelectable(GameObject scope)
        {
            foreach (var hub in scope.GetComponentsInChildren<MenuHubButton>(false))
            {
                var view = hub.GetComponent<MenuAvailabilityView>();
                if (view && !view.IsAvailable) continue;
                if (hub.TryGetComponent(out Selectable s) && s.IsInteractable()) return s;
            }

            foreach (var s in scope.GetComponentsInChildren<Selectable>(false))
                if (s.IsInteractable() && s.navigation.mode != Navigation.Mode.None) return s;

            return null;
        }

        #endregion

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
                CSDebug.LogError("[ScreenSwitcher] No parent Canvas found - screen sliding will not work.");
                return;
            }
            _rootCanvas = parentCanvas.rootCanvas;
            _canvasRect = _rootCanvas.GetComponent<RectTransform>();
            _menuAudio = GetComponent<MenuAudio>();

            CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] Start - rootCanvas={_rootCanvas.name}, viewport={GetViewportWidthInCanvasUnits()}, screens={GetScreenCount()}");

            CacheScreenComponents();
            LayoutScreensToViewport();
            MarkDisabledNavLinks();
            UpdateHubButtonsVisibility();

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
                          or ModalWindows.ARCADE
                          or ModalWindows.ARENA_GAME_CONFIGURE)
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
        ///
        /// <para>It reads THIS transform's rect rather than the canvas rect, and the difference is
        /// the horizontal half of the safe area. <see cref="LayoutScreensToViewport"/> sizes every
        /// screen panel to this width and offsets panel <c>i</c> by <c>i * width</c>; with the menu
        /// canvas split into a full-bleed layer and a fitted content layer
        /// (<c>Docs/UI_ARCHITECTURE_AUDIT.md</c> §1.3), the strip lives inside the content layer,
        /// so measuring the CANVAS would leave every panel full canvas width inside a
        /// horizontally-inset parent — the vertical half of the safe area respected and the
        /// horizontal half silently not.</para>
        ///
        /// <para>On any display whose safe area is the full screen — every desktop — the content
        /// layer is authored full-stretch with zero offsets, so its rect IS the canvas rect and
        /// this returns exactly what it returned before. A non-notched display cannot regress.</para>
        /// </summary>
        private float GetViewportWidthInCanvasUnits()
        {
            if (transform is RectTransform self && self.rect.width > 0f)
                return self.rect.width;

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

        /// <summary>
        /// Gives every disabled screen's nav-bar link the shared LOCKED state, so it reads as
        /// closed rather than looking enabled and doing nothing on press
        /// (<c>Docs/HomeHub/ARCHITECTURE.md</c> §2 - an entry that is simply not drawn tells the
        /// player the game has fewer things in it than it does).
        ///
        /// <para>Driven from <see cref="disabledScreens"/> at runtime rather than authored on the
        /// links, for the reason that list exists at all: it is the single source of truth. A screen
        /// added to it tomorrow is marked with no scene edit, and a screen removed from it goes back
        /// to normal without one either - two authored copies of the same fact would drift.</para>
        ///
        /// <para>Only the disabled links get a view. An Available entry has nothing to present, and
        /// the component would cost every other link a colour capture for nothing.</para>
        /// </summary>
        private void MarkDisabledNavLinks()
        {
            int count = GetScreenCount();
            for (int i = 0; i < count; i++)
            {
                if (!IsIndexDisabled(i)) continue;

                var link = ResolveNavLinkObject(i);
                if (!link) continue;

                var view = MenuAvailabilityView.Ensure(link);
                if (!view) continue;

                view.SetLockedMessage(disabledScreenMessage);
                view.SetAvailability(MenuAvailability.Locked);
            }
        }

        /// <summary>
        /// The nav-bar button GameObject for a screen index, resolved the same two ways
        /// <see cref="UpdateNavBar"/> highlights one - the explicit icon lists first (each entry's
        /// PARENT is its button), then the legacy container walk. Null when neither is configured,
        /// which is a nav bar with nothing to mark rather than an error.
        /// </summary>
        private GameObject ResolveNavLinkObject(int index)
        {
            if (NavActiveImages != null && index >= 0 && index < NavActiveImages.Count &&
                NavActiveImages[index] && NavActiveImages[index].transform.parent)
                return NavActiveImages[index].transform.parent.gameObject;

            // Legacy: NavBar points at the buttons container and each button holds [inactive, active].
            if (NavBar && index >= 0 && index < NavBar.childCount)
            {
                var child = NavBar.GetChild(index);
                if (child && child.childCount >= 2) return child.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Answers a press on a disabled screen out loud. Routed through that link's own
        /// <see cref="MenuAvailabilityView"/> so the sting and the wording are the ones every other
        /// locked surface in the shell uses; the bare sting is the fallback for a nav bar whose link
        /// could not be resolved.
        /// </summary>
        private void RefuseDisabledScreen(MenuScreens screen)
        {
            var link = ResolveNavLinkObject(GetIndexForScreen(screen));
            if (link && link.TryGetComponent(out MenuAvailabilityView view))
            {
                view.TryPress();
                return;
            }

            var system = AudioSystem.Instance;
            if (system) system.PlayMenuAudio(MenuAudioCategory.Denied);
        }

        #endregion

        #region Navigation Core

        private void NavigateTo(MenuScreens screen, bool animate = true)
        {
            // Disabled is checked FIRST so a closed screen always explains itself. It used to sit
            // below the host-only guard, which meant a party guest pressing ARK got the silent
            // return from the wrong rule and no refusal at all.
            if (IsScreenDisabled(screen))
            {
                RefuseDisabledScreen(screen);
                return;
            }

            // Arcade is host-only in multiplayer sessions
            if (screen == MenuScreens.ARK && !IsHostOrSolo())
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

        /// <summary>
        /// The Arena counterpart of <see cref="FollowHostToArcadeScreen"/>: the host opened an
        /// ARENA card, so the guest's app shell shows the Arena grid under the launch window the
        /// same way the arcade screen sits under an arcade card. Host-driven only - nothing on the
        /// guest's own UI calls it.
        /// </summary>
        public void FollowHostToArenaWindow()
        {
            if (ModalIsActive(ModalWindows.ARENA)) return;
            OpenModal(ModalWindows.ARENA);
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
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - in freestyle");
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
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - screen disabled ({GetScreenIdForIndex(ScreenIndex)})");
                return;
            }

            if (ScreenIndex == currentScreen)
            {
                CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] NavigateTo({ScreenIndex}) blocked - already on this screen");
                return;
            }

            CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] NavigateTo({ScreenIndex}) - sliding from {currentScreen} to {ScreenIndex} ({GetScreenIdForIndex(ScreenIndex)})");

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
            UpdateHubButtonsVisibility();
            Refocus();
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

        /// <summary>
        /// Opens the profile MODAL (the avatar + display-name editor), as distinct from
        /// <see cref="OnClickProfileNav"/>, which navigates to the profile SCREEN.
        ///
        /// <para>The avatar buttons on the Profile and Home screens call this rather than a direct
        /// <c>ModalWindowIn</c> on the window, per <c>Docs/HomeHub/ARCHITECTURE.md</c> §1: the
        /// switcher already owns the modal stack, the return-to-modal pref and the close sweeps, so
        /// a button reaching past it would be a second authority. A parameterless wrapper because a
        /// UnityEvent persistent call cannot pass an enum - the same shape as the hub's
        /// <see cref="OnClickToyboxNav"/>.</para>
        /// </summary>
        public void OnClickProfileModal() => OpenModal(ModalWindows.PROFILE);

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
            UpdateHubButtonsVisibility();

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
            UpdateHubButtonsVisibility();

            // Notify the current screen that it's being re-entered
            if (_screenMap.TryGetValue(currentScreen, out var enteringScreen))
                enteringScreen.OnScreenEnter();

            Refocus();
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

                // Visible OR open - the two disagree more often than they look like they should,
                // and this used to test only the first and then act through ModalWindowOut, which
                // is gated on the second. A modal that was visible but not `isOn` (ModalWindowIn
                // refuses to open while the freestyle gate is engaged; a launch panel shows itself
                // before opening its host) therefore ignored this call completely and stayed on
                // screen over the flight.
                var cg = modal.GetComponent<CanvasGroup>();
                bool visible = cg && cg.alpha > 0.01f;
                if (visible || modal.IsOpen)
                    modal.ForceCloseImmediate();
            }

            // The ARCADE modal is wired on its own field and is NOT required to be in `Modals` -
            // and it is the one carrying a live preview RenderTexture, so a copy that never got
            // added to that list is precisely the one whose absence is most visible.
            if (ArcadeModal)
                ArcadeModal.ForceCloseImmediate();
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
