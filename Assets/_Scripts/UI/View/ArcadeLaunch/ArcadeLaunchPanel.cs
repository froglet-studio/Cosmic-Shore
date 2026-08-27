using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One card's whole launch surface — everything the player configures before the match, in one
    /// panel.
    ///
    /// <para><b>The two-screen flow is gone.</b> Configure-then-pick-a-vessel existed because a
    /// card could be flown in several hulls; every arcade mode now locks to one, so the second
    /// screen had nothing left to ask and the first had no reason to be a separate step. What
    /// remains is a single panel per card, and this is its contract with
    /// <see cref="ArcadeGameConfigureModal"/>.</para>
    ///
    /// <para><b>The panel owns its widgets; the modal owns the decisions.</b> A panel exposes the
    /// controls it contains (intensity row, domain tiles, start button) and the modal subscribes to
    /// them, so config validation, the network commit, ready-up and launch stay in exactly one
    /// place no matter which panel is on screen. A panel never writes
    /// <c>ArcadeGameConfigSO</c>, never talks to <c>ArcadeConfigSyncManager</c>, and never
    /// launches anything — the moment a panel starts making those calls there are two authorities
    /// on the same state, which is the failure the single-writer rule exists to prevent.</para>
    ///
    /// <para>Two panels ship: <see cref="MinigameLaunchPanel"/> for a mode with an arena of its
    /// own, and <see cref="MaelstromLaunchPanel"/> for the meta-mode that draws other modes.
    /// <see cref="Handles"/> is how a card finds its panel, so a third is a new subclass and one
    /// entry in the modal's list.</para>
    /// </summary>
    public abstract class ArcadeLaunchPanel : MonoBehaviour
    {
        [Header("Shared header")]
        [SerializeField, Tooltip("The card's DisplayName.")]
        protected TMPro.TMP_Text gameNameText;

        [SerializeField, Tooltip("Description + rotating tips.")]
        protected GameBriefingView briefing;

        [SerializeField, Tooltip("Optional favourite star for this card.")]
        protected FavoriteIcon favoriteIcon;

        [Header("Shared controls the modal drives")]
        [SerializeField, Tooltip("This panel's intensity row, in ascending order (1..4). The modal " +
                                 "subscribes to these while the panel is the active one.")]
        protected List<IntensitySelectButton> intensityButtons = new(4);

        [SerializeField, Tooltip("This panel's domain tiles (Jade, Ruby, Gold).")]
        protected List<DomainInfoData> domainTiles = new(3);

        [SerializeField, Tooltip("Start / ready-up button.")]
        protected Button startButton;

        [SerializeField, Tooltip("'Waiting for others…' label, shown after this player confirms.")]
        protected GameObject waitingForOthersLabel;

        [Header("Own window (optional)")]
        [SerializeField, Tooltip("Set ONLY when this panel lives in its own modal window rather " +
                                 "than inside ArcadeGameConfigureModal's. The panel then opens and " +
                                 "closes that window with itself, and a close from the window's own " +
                                 "controls (its X, gamepad B) is routed back to the modal so the " +
                                 "network close and the preview teardown still run.\n\n" +
                                 "LEAVE EMPTY for a panel that is a child of the arcade modal - " +
                                 "pointing it at the modal that owns it would have that modal " +
                                 "closing itself from inside its own close.")]
        ModalWindowManager hostModal;

        [Header("Roster")]
        [SerializeField, Tooltip("Seats + the fill-with-AI toggle. Optional: a panel without one " +
                                 "simply shows no roster.")]
        protected LobbySlotRow lobbyRow;

        [SerializeField, Tooltip("The Add AI mode toggle when it lives on the PANEL rather than " +
                                 "inside a LobbySlotRow - the one-panel layout puts it beside the " +
                                 "domain tiles, where the AI it places actually land.\n\n" +
                                 "Optional and independent of lobbyRow: whichever is wired raises " +
                                 "the same event, and both may be wired at once.")]
        protected UnityEngine.UI.Toggle fillWithAIToggle;

        bool _suppressFillToggleCallback;

        /// <summary>The ✕ on an AI seat: remove the placed AI with this ordinal.</summary>
        public event Action<int> OnKickAIRequested;

        /// <summary>The Add AI mode toggle moved. True = domain taps now place AI.</summary>
        public event Action<bool> OnAddAIModeChanged;

        /// <summary>
        /// Raised when this panel's OWN window was closed by its own controls, so the modal can run
        /// the real close (notify clients, tear the preview down) instead of a window simply
        /// animating out with the session still live.
        /// </summary>
        public event Action OnHostModalClosed;

        /// <summary>The card currently drawn, or null.</summary>
        public SO_ArcadeGame Game { get; private set; }

        /// <summary>The window this panel lives in, or null when it is inside the arcade modal's.</summary>
        public ModalWindowManager HostModal => hostModal;

        public IReadOnlyList<IntensitySelectButton> IntensityButtons => intensityButtons;
        public IReadOnlyList<DomainInfoData> DomainTiles => domainTiles;
        public Button StartButton => startButton;
        public GameObject WaitingForOthersLabel => waitingForOthersLabel;
        public FavoriteIcon Favorite => favoriteIcon;

        /// <summary>
        /// The live preview window this panel contains, or null when it has none (Maelstrom shows
        /// a clip instead — it draws OTHER modes, so it has no arena of its own to stand up).
        /// </summary>
        public virtual ModePreviewWindow PreviewWindow => null;

        /// <summary>Whether this panel is the one that draws <paramref name="game"/>.</summary>
        public abstract bool Handles(SO_ArcadeGame game);

        protected virtual void OnEnable()
        {
            if (lobbyRow)
            {
                lobbyRow.OnKickAIRequested += RaiseKickAI;
                lobbyRow.OnAddAIModeChanged += RaiseAddAIMode;
            }

            if (fillWithAIToggle) fillWithAIToggle.onValueChanged.AddListener(HandleFillToggle);

            if (hostModal) hostModal.OnModalClosed += RaiseHostModalClosed;
        }

        protected virtual void OnDisable()
        {
            if (lobbyRow)
            {
                lobbyRow.OnKickAIRequested -= RaiseKickAI;
                lobbyRow.OnAddAIModeChanged -= RaiseAddAIMode;
            }

            if (fillWithAIToggle) fillWithAIToggle.onValueChanged.RemoveListener(HandleFillToggle);

            if (hostModal) hostModal.OnModalClosed -= RaiseHostModalClosed;
        }

        /// <summary>
        /// Fill the panel for a card. Subclasses override to add their own half (a live preview, a
        /// clip and a pool list) and must call base to get the shared header.
        /// </summary>
        public virtual void Bind(SO_ArcadeGame game, int intensity)
        {
            Game = game;

            if (gameNameText)
                gameNameText.text = game ? game.DisplayName : string.Empty;

            if (briefing)
                briefing.Show(game);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ArcadeLaunch] {GetType().Name} bound to '{(game ? game.DisplayName : "none")}' " +
                $"at intensity {intensity}.");
        }

        /// <summary>The intensity changed while this panel is up.</summary>
        public virtual void HandleIntensityChanged(int intensity) { }

        /// <summary>Redraw the roster. A panel with no <see cref="lobbyRow"/> ignores this.</summary>
        public virtual void RefreshRoster(GameDataSO gameData, int totalPlayers, int humanCount,
                                          System.Collections.Generic.IReadOnlyList<CosmicShore.Data.Domains> aiDomains,
                                          int readyCount, bool localReady, bool isHost, bool addAiArmed)
        {
            if (lobbyRow)
                lobbyRow.Refresh(gameData, totalPlayers, humanCount, aiDomains,
                                 readyCount, localReady, isHost, addAiArmed);

            // The PANEL-level Add AI toggle is host-only too. The row hides its own copy inside
            // Refresh, but the one-panel layout wires the toggle HERE, beside the domain tiles -
            // and without this gate a client saw a live ADD AI control whose taps went nowhere.
            if (fillWithAIToggle)
            {
                fillWithAIToggle.gameObject.SetActive(isHost);
                fillWithAIToggle.interactable = isHost;
            }
        }

        /// <summary>
        /// Reflect the Add AI toggle without raising its event — the modal owns the armed state,
        /// so the toggle follows it rather than racing it.
        /// </summary>
        public virtual void SetAddAIModeSilently(bool on)
        {
            if (lobbyRow) lobbyRow.SetAddAIModeSilently(on);

            if (fillWithAIToggle && fillWithAIToggle.isOn != on)
            {
                _suppressFillToggleCallback = true;
                fillWithAIToggle.isOn = on;
                _suppressFillToggleCallback = false;
            }
        }

        /// <summary>Show the star's state for this card.</summary>
        public virtual void RefreshFavorite(bool favorited)
        {
            if (favoriteIcon) favoriteIcon.Favorited = favorited;
        }

        /// <summary>Ready-up state: the Start button and the waiting label are mutually exclusive.</summary>
        public virtual void SetReadyUpState(bool confirmed)
        {
            if (startButton) startButton.gameObject.SetActive(!confirmed);
            if (waitingForOthersLabel) waitingForOthersLabel.SetActive(confirmed);
        }

        /// <summary>
        /// Grey out the controls only the host owns (intensity, the fill toggle). Domain and Start
        /// stay live for every player — those are each player's own choices.
        /// </summary>
        public virtual void SetHostControlsInteractable(bool interactable)
        {
            foreach (var button in intensityButtons)
            {
                if (!button) continue;
                // IntensitySelectButton keeps its own SELECTED/ACTIVE/LOCKED visuals; whether a
                // click is even accepted is the UGUI Button's, which is the one the modal has
                // always gated on for clients.
                var uiButton = button.GetComponent<Button>();
                if (uiButton) uiButton.interactable = interactable;
            }
        }

        /// <summary>Bring this panel up, opening its own window when it has one.</summary>
        public virtual void Show()
        {
            gameObject.SetActive(true);
            if (hostModal) hostModal.ModalWindowIn();
        }

        /// <summary>
        /// Take this panel down. A panel in its own window closes the WINDOW and leaves its content
        /// alone - the window animates out over half a second, and deactivating the content under it
        /// would cut that animation off mid-frame. A panel inside the arcade modal has no window of
        /// its own, so it simply switches off.
        /// </summary>
        public virtual void Hide()
        {
            if (hostModal)
            {
                hostModal.ModalWindowOut();
                return;
            }
            gameObject.SetActive(false);
        }

        void RaiseKickAI(int aiOrdinal) => OnKickAIRequested?.Invoke(aiOrdinal);
        void RaiseAddAIMode(bool on) => OnAddAIModeChanged?.Invoke(on);

        void HandleFillToggle(bool on)
        {
            if (_suppressFillToggleCallback) return;
            RaiseAddAIMode(on);
        }

        /// <summary>Whether Add AI placement mode is currently armed.</summary>
        public virtual bool AddAIModeArmed =>
            fillWithAIToggle ? fillWithAIToggle.isOn : lobbyRow && lobbyRow.AddAIModeArmed;
        void RaiseHostModalClosed() => OnHostModalClosed?.Invoke();
    }
}
