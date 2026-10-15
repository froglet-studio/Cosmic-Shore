using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class ScreenSwitcher : MonoBehaviour
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

            // The credits screen. Its own modal TYPE rather than a panel inside SETTINGS: it is
            // opened FROM the settings modal, and a modal type is what ScreenSwitcher unwinds by,
            // so gamepad B out of the credits lands back on Settings instead of closing both.
            // It also has to stay reachable if a second entry point (a home-hub tile) is ever
            // added. Shipping without it breaches FMOD's EULA clause 3 - see CreditsModal.
            CREDITS = 18,
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

        // The scene's Reflex container, for the credits window this component BUILDS. A runtime
        // `new GameObject` gets no injection, and ModalWindowManager carries an [Inject]
        // AudioSystem -- CLAUDE.md's rule is to inject at the creating site rather than rely on
        // the callee's fallback, which exists for the spawn site that forgets.
        [Inject] private Container _container;

        [Inject] private MenuFreestyleEventsContainerSO freestyleEvents;

        [Header("Disabled Screens")]
        [Tooltip("Screens in this list are skipped during navigation and cannot be opened via buttons or controller input.\n" +
                 "Their nav-bar links are marked MenuAvailability.Locked at Start, so they READ as locked " +
                 "rather than looking enabled and doing nothing. This list is the single source of truth for " +
                 "PERMANENTLY closed screens - adding a screen here is all it takes.\n" +
                 "It is not the only source: a COMMERCE screen (the Store) is closed by " +
                 "Resources/CommerceAvailability instead, so the paid-EA conversion is one asset edit and " +
                 "carries its own reading (Unavailable rather than Locked). Do not author one in both.")]
        [SerializeField] private List<MenuScreens> disabledScreens = new() { MenuScreens.PORT };

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

        // The strip (this rect) slides inside its viewport by anchoredPosition, one viewport width
        // per screen index; see EnsureStripViewport and LayoutScreensToViewport.
        private RectTransform _strip;
        private float _laidOutWidth = -1f;
        private const string StripViewportName = "Screens Viewport";
        // The layer the strip was authored in (Menu_Main: Safe Area). Its rect is where screen
        // CONTENT goes; the strip itself moves out to the full canvas so backgrounds can bleed.
        private RectTransform _contentLayer;
        private Vector4 _laidOutInsets = new(-1f, -1f, -1f, -1f);
        private readonly List<SafeAreaBleed> _bleeds = new();
        private bool _bleedsBuilt;
        private float _designWidth = 1920f;
        private static readonly Vector3[] Corners = new Vector3[4];
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

        /// <summary>True while the EventSystem's selection is a live control on the HOME screen.</summary>
        private bool HomeControlSelected()
        {
            var es = EventSystem.current;
            var selected = es ? es.currentSelectedGameObject : null;
            var root = CurrentScreenRoot();
            return selected && root && selected.activeInHierarchy && selected.transform.IsChildOf(root.transform)
                   && selected.TryGetComponent<Selectable>(out var s) && s.IsInteractable();
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
            // The flagged hub first (Arcade): the first available one in hierarchy order is Toy Box,
            // which made A on a fresh Home open the Toy Box while the A keycap sat on Arcade.
            foreach (var hub in scope.GetComponentsInChildren<MenuHubButton>(false))
            {
                if (!hub.PreferredFocus) continue;
                var view = hub.GetComponent<MenuAvailabilityView>();
                if (view && !view.IsAvailable) continue;
                if (hub.TryGetComponent(out Selectable s) && s.IsInteractable()) return s;
            }

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
            freestyleEvents.OnGameStateTransitionEnd.OnRaised += HandleFreestyleSettled;
        }

        private void UnsubscribeFreestyleEvents()
        {
            if (!freestyleEvents) return;
            freestyleEvents.OnMenuStateTransitionEnd.OnRaised -= HandleFreestyleExitCompleted;
            _pendingFreestyleExit = null;
            freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
            freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
            freestyleEvents.OnGameStateTransitionEnd.OnRaised -= HandleFreestyleTransitionEnd;
            freestyleEvents.OnMenuStateTransitionEnd.OnRaised -= HandleFreestyleTransitionEnd;
            freestyleEvents.OnGameStateTransitionEnd.OnRaised -= HandleFreestyleSettled;
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
            if (_rootCanvas.TryGetComponent<CanvasScaler>(out var scaler)
                && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                _designWidth = scaler.referenceResolution.x;
            EnsureStripViewport();

            CSDebug.LogVerbose(CSLogChannel.MenuUI, $"[ScreenSwitcher] Start - rootCanvas={_rootCanvas.name}, viewport={GetViewportWidthInCanvasUnits()}, screens={GetScreenCount()}");

            CacheScreenComponents();
            EnsureCreditsModal();
            LayoutScreensToViewport();
            MarkDisabledNavLinks();
            UpdateHubButtonsVisibility();

            // currentScreen is serialized (0, HANGAR), so restoring HANGAR used to hit NavigateTo's
            // "already on this screen" return: the strip was never positioned and the hangar grid
            // was never built. Starting from no screen makes the first navigation a real enter.
            currentScreen = -1;

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

            // Every NavigateTo guard (freestyle, a disabled screen) can refuse the
            // first navigation. The strip still needs a screen to rest on, and HOME is the one the
            // menu opens on.
            if (currentScreen < 0)
                currentScreen = Mathf.Max(0, GetIndexForScreen(MenuScreens.HOME));
            SnapStripToScreen(currentScreen);

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
            // The viewport's width follows the safe area and the window (a desktop resize, an iPad
            // split view); re-lay the strip out on the frame it changes.
            RelayoutIfViewportResized();

            // Self-healing input gate: never depend solely on the freestyle events having
            // been delivered (a missed subscription here is exactly how the appshell kept
            // reacting to vessel ability buttons). Read the LIVE freestyle state each frame
            // and (re)apply the EventSystem gating whenever it flips.
            bool inFreestyle = InFreestyle;

            // ...and heal the GATE itself, not only the flag that says it was applied. Two other
            // systems write sendNavigationEvents (ModePreviewSession and MaelstromPreviewHost save
            // and restore it around their own flight), and a restore that lands after freestyle
            // began re-opened the pad to the appshell for the rest of the flight, with
            // _appliedFreestyleGate still reading true - so the comparison below never fired again.
            if (inFreestyle && _appliedFreestyleGate
                && EventSystem.current && EventSystem.current.sendNavigationEvents)
                ApplyFreestyleInputGate(true);

            if (inFreestyle != _appliedFreestyleGate)
            {
                ApplyFreestyleInputGate(inFreestyle);

                // The hub row and the pad's selection are computed from the SAME live state, so
                // they get the same self-heal: a hub row stranded at alpha 0 and non-interactable
                // is the app's primary navigation gone, with no way back except paging screens.
                UpdateHubButtonsVisibility();
                Refocus();
            }

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
                // A on a focused Home control is that control's Submit (a hub opens its own window);
                // answering it here too opened the Arcade on top of whatever the pad was on. The
                // shortcut stands only while nothing on Home holds the pad's selection.
                if (Gamepad.current.buttonSouth.wasPressedThisFrame && !HomeControlSelected())
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

        #region Viewport Layout

        /// <summary>
        /// Returns the width, in canvas units, of the viewport the strip slides inside: its PARENT,
        /// the full-canvas layer <see cref="EnsureStripViewport"/> puts it in. That width is one
        /// screen SLOT.
        ///
        /// <para>It must not measure the strip itself. The strip carries a ContentSizeFitter
        /// (horizontal Min Size) that collapses its width to 0 on the first layout pass, so a self
        /// measurement read one width in <c>Start</c> and the canvas width on every later call. On a
        /// Dynamic Island iPhone the panels were laid out 2202 px apart and slid 2556 px per step;
        /// at 4:3, 1440 units apart and slid 1920. Either way, every navigation left the screen
        /// further off.</para>
        /// </summary>
        private float GetViewportWidthInCanvasUnits()
        {
            if (transform.parent is RectTransform viewport && viewport.rect.width > 0f)
                return viewport.rect.width;

            if (_canvasRect != null)
                return _canvasRect.rect.width;

            // Fallback: assume 1:1 canvas-to-pixel mapping
            return Screen.width;
        }

        /// <summary>
        /// How far the content layer sits in from each canvas edge, as (left, bottom, right, top) in
        /// canvas units: the safe-area margins. Zero on any display whose safe area is the screen.
        /// </summary>
        private Vector4 GetContentInsets()
        {
            if (!_contentLayer || !_canvasRect) return Vector4.zero;

            _contentLayer.GetWorldCorners(Corners);
            Vector2 min = _canvasRect.InverseTransformPoint(Corners[0]);
            Vector2 max = _canvasRect.InverseTransformPoint(Corners[2]);
            Rect canvas = _canvasRect.rect;
            return new Vector4(Mathf.Max(0f, min.x - canvas.xMin), Mathf.Max(0f, min.y - canvas.yMin),
                               Mathf.Max(0f, canvas.xMax - max.x), Mathf.Max(0f, canvas.yMax - max.y));
        }

        /// <summary>
        /// Moves the strip into a full-canvas viewport carrying a <see cref="RectMask2D"/>, placed in
        /// the root canvas just before the layer the strip was authored in. Screens therefore still
        /// draw under everything in that layer (NavBar, modals, toasts), as they did as its first
        /// child.
        ///
        /// <para>Why full-canvas and not the content layer: each screen gets a full-screen SLOT,
        /// its content laid out in the safe rect within the slot, and its backgrounds bleed to the
        /// slot edges (<see cref="SafeAreaBleed"/>). That is the desktop look on a notched phone:
        /// nothing leaks around the corners, and the slots are a full screen apart so a neighbour
        /// can never show.</para>
        /// </summary>
        private void EnsureStripViewport()
        {
            _strip = transform as RectTransform;
            if (!_strip || !_canvasRect) return;

            var parent = _strip.parent as RectTransform;
            if (!parent) return;
            if (parent.name == StripViewportName && parent.GetComponent<RectMask2D>()) return;

            // The ancestor that is a direct child of the root canvas: the viewport goes before it.
            Transform rootLayer = _strip;
            while (rootLayer.parent && rootLayer.parent != _canvasRect)
                rootLayer = rootLayer.parent;
            if (rootLayer.parent != _canvasRect) return;

            _contentLayer = parent == _canvasRect ? null : parent;

            var go = new GameObject(StripViewportName, typeof(RectTransform), typeof(RectMask2D))
            {
                layer = gameObject.layer
            };
            var viewport = (RectTransform)go.transform;
            viewport.SetParent(_canvasRect, false);
            SafeAreaLayer.Stretch(viewport);
            viewport.SetSiblingIndex(rootLayer.GetSiblingIndex());
            _strip.SetParent(viewport, false);
        }

        /// <summary>
        /// Lays every screen out in its own full-viewport slot, with its content in the safe rect
        /// inside that slot, so the layout works at any aspect ratio and any inset.
        /// </summary>
        private void LayoutScreensToViewport()
        {
            float slot = GetViewportWidthInCanvasUnits();
            Vector4 inset = GetContentInsets();
            int count = GetScreenCount();

            // The strip itself: its left edge on the viewport's left edge, full viewport height.
            // With the pivot on that edge its width cannot move anything, so the ContentSizeFitter
            // that zeroes it is harmless, and anchoredPosition.x is exactly the strip's offset
            // into the viewport.
            if (_strip)
            {
                _strip.anchorMin = new Vector2(0f, 0f);
                _strip.anchorMax = new Vector2(0f, 1f);
                _strip.pivot = new Vector2(0f, 0.5f);
                _strip.sizeDelta = new Vector2(slot * count, 0f);
            }

            for (int i = 0; i < count; i++)
            {
                RectTransform rt = GetScreenRootRT(i);
                if (rt == null) continue;

                // Slot i spans [i * slot, (i + 1) * slot]; the panel is its safe rect.
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(i * slot + inset.x, inset.y);
                rt.offsetMax = new Vector2((i + 1) * slot - inset.z, -inset.w);
            }

            _laidOutWidth = slot;
            _laidOutInsets = inset;
            ApplyBleeds(inset);
        }

        /// <summary>
        /// Sizes every background bleed for the current insets, building them the first time a
        /// display has any (a desktop never does, so it never gets one).
        /// </summary>
        private void ApplyBleeds(Vector4 inset)
        {
            if (!_bleedsBuilt)
            {
                if (inset.x < 0.5f && inset.y < 0.5f && inset.z < 0.5f && inset.w < 0.5f) return;
                BuildBleeds();
                _bleedsBuilt = true;
            }

            Vector3 canvasScale = _canvasRect ? _canvasRect.lossyScale : Vector3.one;
            foreach (var bleed in _bleeds)
                bleed.Apply(inset, canvasScale);
        }

        /// <summary>
        /// Finds the backgrounds that should reach the screen edges and gives each a
        /// <see cref="SafeAreaBleed"/>. A background is a visible <see cref="Image"/> as wide as its
        /// safe rect (or as the 1920 design width, where the safe rect is wider), reached through
        /// ancestors that are as wide. It bleeds left and right, and down or up only where it reaches
        /// that rect's bottom or top. It searches:
        /// <list type="bullet">
        /// <item>every screen panel, whose rect IS its slot's safe rect;</item>
        /// <item>the content layer's own children (NavBar, ModalWindows, ...), against the content
        /// layer's rect.</item>
        /// </list>
        /// A narrower image bleeds only if it is a side panel (<see cref="TryBleedSidePanel"/>);
        /// anything else is content and stays where it is.
        /// </summary>
        private void BuildBleeds()
        {
            for (int i = 0; i < GetScreenCount(); i++)
            {
                RectTransform root = GetScreenRootRT(i);
                if (root) CollectBleeds(root, root, 0);
            }

            if (!_contentLayer) return;
            for (int i = 0; i < _contentLayer.childCount; i++)
            {
                if (_contentLayer.GetChild(i) is RectTransform child)
                    CollectBleeds(child, _contentLayer, 0);
            }
        }

        private void CollectBleeds(RectTransform node, RectTransform reference, int depth)
        {
            const float tolerance = 1f;
            const int maxDepth = 4;

            Rect r = reference.rect;
            node.GetWorldCorners(Corners);
            Vector2 min = reference.InverseTransformPoint(Corners[0]);
            Vector2 max = reference.InverseTransformPoint(Corners[2]);

            // Full width means full width on the screen it was authored for: the whole safe rect,
            // or the design width (1920) where the safe rect is wider.
            float fullWidth = Mathf.Min(r.width, _designWidth);
            if (max.x - min.x < fullWidth - tolerance)
            {
                TryBleedSidePanel(node, reference, min, max);
                return;
            }

            if (node.TryGetComponent<Image>(out var image) && SafeAreaBleed.CanBleed(image))
            {
                // Down or up only where it reaches the safe rect's edge (or passes it).
                bool bottom = min.y - r.yMin <= tolerance;
                bool top = r.yMax - max.y <= tolerance;
                _bleeds.Add(SafeAreaBleed.Create(image, reference, true, bottom, true, top));
            }

            if (depth >= maxDepth) return;
            for (int i = 0; i < node.childCount; i++)
            {
                if (node.GetChild(i) is RectTransform child && child.name != SafeAreaBleed.CopyName)
                    CollectBleeds(child, reference, depth + 1);
            }
        }

        /// <summary>
        /// A side panel is as tall as its parent and docked on the safe rect's left or right edge,
        /// like the party panel of the Arcade/Arena windows. On a desktop it sits on the screen
        /// edge; on a phone its frame bleeds out to that edge, and its content stays where it is.
        /// Not searched further: what is inside it is content.
        /// </summary>
        private void TryBleedSidePanel(RectTransform node, RectTransform reference, Vector2 min, Vector2 max)
        {
            const float tolerance = 1f;

            if (!node.TryGetComponent<Image>(out var image) || !SafeAreaBleed.CanBleed(image)) return;
            if (node.parent is not RectTransform parent) return;

            Rect r = reference.rect;
            bool left = min.x - r.xMin <= tolerance;
            bool right = r.xMax - max.x <= tolerance;
            if (!left && !right) return;

            parent.GetWorldCorners(Corners);
            float parentHeight = reference.InverseTransformPoint(Corners[2]).y
                                 - reference.InverseTransformPoint(Corners[0]).y;
            if (max.y - min.y < parentHeight - tolerance) return;

            bool bottom = min.y - r.yMin <= tolerance;
            bool top = r.yMax - max.y <= tolerance;
            _bleeds.Add(SafeAreaBleed.Create(image, reference, left, bottom, right, top));
        }

        /// <summary>The strip's anchoredPosition that puts screen <paramref name="index"/> in the viewport.</summary>
        private Vector2 StripOffsetFor(int index) => new(-index * _laidOutWidth, 0f);

        private void SnapStripToScreen(int index)
        {
            if (!_strip) return;

            if (navigateCoroutine != null)
            {
                StopCoroutine(navigateCoroutine);
                navigateCoroutine = null;
            }

            _strip.anchoredPosition = StripOffsetFor(index);
        }

        private void RelayoutIfViewportResized()
        {
            // Null until Start has laid the strip out.
            if (!_strip) return;
            if (Mathf.Approximately(GetViewportWidthInCanvasUnits(), _laidOutWidth)
                && GetContentInsets() == _laidOutInsets) return;

            LayoutScreensToViewport();

            // A slide in flight re-reads its end every frame; only a resting strip needs moving.
            if (navigateCoroutine == null)
                SnapStripToScreen(currentScreen);
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

        /// <summary>
        /// The commerce surface a screen IS, or null when it is not one — the Store screen today.
        /// The mapping lives here rather than in <see cref="SO_CommerceAvailability"/> so that asset
        /// stays a statement about commerce and never becomes a second screen table.
        /// </summary>
        private static CommerceSurface? CommerceSurfaceFor(MenuScreens screen)
            => screen == MenuScreens.STORE ? CommerceSurface.StoreScreen : null;

        private bool IsScreenDisabled(MenuScreens screen)
        {
            if (disabledScreens != null && disabledScreens.Contains(screen)) return true;

            // A de-scoped commerce screen is skipped for the same reason an authored one is:
            // nothing behind it works (Docs/STEAM_RELEASE_TASKS.md R4). It is read from the config
            // rather than authored into disabledScreens so the paid-EA conversion stays ONE asset
            // edit, and so the two facts - "is it navigable" and "what does its link say" - can
            // never be flipped separately and leave a screen navigable but refusing.
            var surface = CommerceSurfaceFor(screen);
            return surface.HasValue && !SO_CommerceAvailability.Instance.IsAvailable(surface.Value);
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
        /// <para>A COMMERCE screen is the same mechanism with a different authority: its state comes
        /// from <see cref="SO_CommerceAvailability"/>, because that de-scope flips as a build posture
        /// rather than as a permanent design decision, and it reads Unavailable rather than Locked.
        /// The look is still this one component's in both cases.</para>
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

                // A de-scoped commerce screen carries the COMMERCE posture's own state and
                // wording: the Store reads Unavailable ("this is not built", because nothing in it
                // is purchasable this window), where an authored disabled screen reads Locked
                // ("not yet"). Anything the config calls Available has only reached this loop
                // because it is authored in disabledScreens, so it falls through to Locked below -
                // a nav entry that is skipped AND reads as open is the exact defect this method
                // exists to prevent.
                //
                // Unreachable TODAY: MenuScreens.STORE has no entry in `screens` at all, so no index
                // resolves to it and the store has no nav link to mark (Docs/MENU_PROGRESSION_AND_IAP.md
                // section 1). Kept because the IsScreenDisabled half of the same pair is NOT dead - it
                // is what makes NavigateTo(STORE) refuse instead of falling through to index 0 and
                // landing the player on the Hangar - and the day STORE gains a screen entry its link
                // has to read correctly without anyone remembering this.
                var surface = CommerceSurfaceFor(GetScreenIdForIndex(i));
                if (surface.HasValue && !SO_CommerceAvailability.Instance.IsAvailable(surface.Value))
                {
                    SO_CommerceAvailability.Mark(link, surface.Value);
                    continue;
                }

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
            // Disabled is checked FIRST so a closed screen always explains itself.
            // (ARK used to be the arcade and was host-only in a party. It is the Elemental Converter now - a
            // local animation every player may open - and the arcade lives in its own modal.)
            if (IsScreenDisabled(screen))
            {
                RefuseDisabledScreen(screen);
                return;
            }

            int index = GetIndexForScreen(screen);
            NavigateTo(index, animate);
        }

        /// <summary>
        /// The host opened an ARENA card, so the guest's app shell shows the Arena grid under the
        /// launch window. Host-driven only - nothing on the guest's own UI calls it. (An arcade card
        /// has no counterpart: the arcade screen it used to follow to was ARK, which is now the
        /// Elemental Converter, and a guest must not be pulled there.)
        /// </summary>
        public void FollowHostToArenaWindow()
        {
            if (ModalIsActive(ModalWindows.ARENA)) return;
            OpenModal(ModalWindows.ARENA);
        }

        /// <summary>LIVE freestyle state, for anything the host is driving the local player into.</summary>
        public bool IsInFreestyle => InFreestyle;

        /// <summary>
        /// True while a freestyle blend is still running. <see cref="IsInFreestyle"/> goes false at
        /// the START of an exit, so a poller that only asks that one can act half way through the
        /// camera ease and the UI fade - which reads as a window appearing over a moving shot.
        /// </summary>
        public bool IsFreestyleSettling => crystalClickHandler && crystalClickHandler.IsTransitioning;

        /// <summary>
        /// Bring the local player out of freestyle because the HOST is pulling them somewhere -
        /// then run <paramref name="onExited"/>.
        ///
        /// <para>Freestyle is not merely a screen the appshell is not on: <see cref="NavigateTo"/>
        /// refuses outright, the screens CanvasGroup is faded out and non-raycastable,
        /// <c>ModalWindowIn</c> refuses to open at all while the input gate is engaged, and
        /// <see cref="HandleEnterFreestyle"/> has already closed every modal. So a host who opened
        /// an arcade card reached a flying guest with an event that could not draw anything, and
        /// the guest kept flying while the rest of the party sat in a lobby.</para>
        ///
        /// <para>The callback runs on <c>OnMenuStateTransitionEnd</c>, never on the start event and
        /// never on the live flag: <see cref="HandleExitFreestyle"/> answers the START of the
        /// transition with another <c>CloseAllModals</c>, so anything opened before the end is
        /// closed again on the way out. Returns false and does nothing when there is no freestyle
        /// to leave (the caller should just proceed), or when a transition is already running.</para>
        /// </summary>
        public bool RequestExitFreestyle(System.Action onExited)
        {
            if (!InFreestyle) return false;
            if (!crystalClickHandler || !freestyleEvents) return false;

            // One pending follow at a time - a host flicking between cards must not stack
            // callbacks that each re-open a lobby that has already moved on.
            if (_pendingFreestyleExit != null)
            {
                _pendingFreestyleExit = onExited;
                return true;
            }

            _pendingFreestyleExit = onExited;
            freestyleEvents.OnMenuStateTransitionEnd.OnRaised += HandleFreestyleExitCompleted;
            crystalClickHandler.ToggleTransition();
            return true;
        }

        System.Action _pendingFreestyleExit;

        void HandleFreestyleExitCompleted()
        {
            if (freestyleEvents)
                freestyleEvents.OnMenuStateTransitionEnd.OnRaised -= HandleFreestyleExitCompleted;

            var pending = _pendingFreestyleExit;
            _pendingFreestyleExit = null;
            pending?.Invoke();
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
                case MenuScreens.PROFILE:
                    UserActionSystem.Instance.CompleteAction(UserActionType.ViewProfileMenu);
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

            // Slide effect: one viewport width per index, in the viewport's own units, so the
            // screen lands inside the safe area at any aspect ratio.
            if (animate && _strip)
            {
                if (_menuAudio)
                    _menuAudio.PlayAudio();

                if (navigateCoroutine != null)
                    StopCoroutine(navigateCoroutine);
                navigateCoroutine = StartCoroutine(SmoothMove(ScreenIndex, easing));
            }
            else
            {
                SnapStripToScreen(ScreenIndex);
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

        /// <summary>
        /// Guarantees a CREDITS modal is reachable from this scene, building one when none is
        /// registered.
        ///
        /// <para>FMOD's EULA (<c>Assets/Plugins/FMOD/LICENSE.txt</c>, clause 3) requires an in-game
        /// credit naming <c>FMOD</c> and <c>Firelight Technologies Pty Ltd.</c> on every tier, and
        /// the same screen carries every other attribution the tree owes
        /// (<c>Docs/THIRD_PARTY_REGISTER.md</c> §7). <c>CreditsReleaseGuard</c> refuses to
        /// ship a release build whose manifest has lost that line — but a manifest nobody can read
        /// discharges nothing, so the WINDOW is ensured here for the same reason the DATA is
        /// guarded there. A modal that can be deleted out of a scene is not a guarantee.</para>
        ///
        /// <para>It stands down completely the moment a human authors one: an authored CREDITS
        /// modal in <see cref="Modals"/> is found first and nothing is built, so replacing this
        /// with proper chrome is an ordinary scene edit.</para>
        /// </summary>
        private void EnsureCreditsModal()
        {
            Modals ??= new List<ModalWindowManager>();

            foreach (var authored in Modals)
                if (authored != null && authored.ModalType == ModalWindows.CREDITS)
                    return;

            // A CreditsModal may exist in the scene without being registered (authored, but the
            // Modals list not updated). Adopt it rather than building a second one - two windows
            // of one type is the state ModalStackEntry carries an instance to survive, but it is
            // still a bug the player would see as a credits screen behind a credits screen.
            // Explicit null test, never `??`: Unity's Object overloads `==` to report a destroyed
            // object as null while the C# null-coalescing operator does not, so `??` on a
            // UnityEngine.Object is a trap even where (as here) the finder returns a true null.
            var modal = FindFirstObjectByType<CreditsModal>(FindObjectsInactive.Include);
            if (modal == null)
            {
                modal = CreditsModal.Build(_canvasRect);
                if (modal != null && _container != null)
                    GameObjectInjector.InjectRecursive(modal.gameObject, _container);
            }

            if (modal == null)
            {
                CSDebug.LogError(
                    "[ScreenSwitcher] Could not create the credits window. The game would then " +
                    "ship with no in-game FMOD credit, which breaches FMOD's EULA clause 3 - see " +
                    "Docs/THIRD_PARTY_REGISTER.md §7.");
                return;
            }

            modal.ModalType = ModalWindows.CREDITS;
            modal.AttachScreenSwitcher(this);
            Modals.Add(modal);
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

        // Menu objects HandleFreestyleSettled switched off, each of which was active before.
        private readonly List<GameObject> _deactivatedWhileFlying = new();

        /// <summary>
        /// On a device tier that asks (<see cref="PlatformProfileSO.DeactivateMenuWhileFlying"/>,
        /// MobileLow): once the enter-freestyle blend has settled, DEACTIVATE the hidden screen
        /// roots and the nav bar. A CanvasGroup at alpha 0 still runs every Update, coroutine and
        /// canvas rebuild under it - the hidden screens' per-frame work is paid for the whole flight.
        /// <see cref="RestoreMenuAfterFlying"/> puts back exactly what was active, on the way out.
        ///
        /// The HOME root is never switched off: it carries things that must keep listening while
        /// the pilot flies - the party-invite popup subscribes in OnEnable (an invite arriving
        /// mid-flight would be missed), and HomeScreen subscribes to profile changes in Start and
        /// drops them in OnDisable (one flight would end its updates for the session). This
        /// switcher's own branch is never touched either: its subscriptions and slides must survive.
        /// Docs/PLATFORM_UNIFICATION.md §3.6.
        /// </summary>
        private void HandleFreestyleSettled()
        {
            var profile = PlatformProfile.Current;
            if (!profile || !profile.DeactivateMenuWhileFlying) return;
            if (!InFreestyle || _deactivatedWhileFlying.Count > 0) return;

            if (screens != null)
                foreach (var entry in screens)
                    if (entry?.root && entry.id != MenuScreens.HOME) DeactivateWhileFlying(entry.root.gameObject);

            if (NavBar) DeactivateWhileFlying(NavBar.gameObject);
        }

        private void DeactivateWhileFlying(GameObject go)
        {
            if (!go.activeSelf || transform.IsChildOf(go.transform)) return;
            _deactivatedWhileFlying.Add(go);
            go.SetActive(false);
        }

        private void RestoreMenuAfterFlying()
        {
            foreach (var go in _deactivatedWhileFlying)
                if (go) go.SetActive(true);
            _deactivatedWhileFlying.Clear();
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

            // Bring back what the device tier switched off while flying, before anything below
            // fades it in or tells the current screen it is being re-entered.
            RestoreMenuAfterFlying();

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

        private IEnumerator SmoothMove(int targetIndex, float seconds)
        {
            Vector2 start = _strip.anchoredPosition;
            float t = 0f;
            while (t <= 1.0f)
            {
                t += Time.unscaledDeltaTime / seconds;
                // The end is re-read every frame, so a re-layout mid-slide still lands on the screen.
                _strip.anchoredPosition = Vector2.Lerp(start, StripOffsetFor(targetIndex), Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            navigateCoroutine = null;
        }

        #endregion
    }
}
