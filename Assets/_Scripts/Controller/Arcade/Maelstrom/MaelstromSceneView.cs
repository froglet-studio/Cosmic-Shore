using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Data-driven view for the Maelstrom scene. THREE screens, two roots:
    ///
    /// <list type="number">
    /// <item><b>The HUB</b> (<c>MaelstromConfigureModal</c>) - between every round. The top bar
    /// (pool, round, leading domain), the scroll of round cards, a live preview of the round about
    /// to be played, and a <b>READY</b> button. Every pilot readies; once they all have, the
    /// countdown snaps to three seconds. Nobody ready still starts, at thirty - and both endings
    /// show the same 3-2-1, because they are the same deadline.</item>
    /// <item><b>The STATS screen</b> - the same root once the tournament is decided, with
    /// <b>NEXT</b> in place of READY, no countdown, and the preview showing the last arena rather
    /// than offering to fly it.</item>
    /// <item><b>The SUMMARY panel</b> - the winning domain, the final ranking and the per-player
    /// cards, with Play Again (host), Main Menu (everyone) and <b>STATS</b>, which goes back to
    /// screen 2. The last two rounds of a tournament therefore end on the stats screen, and NEXT
    /// is what asks for the trophy.</item>
    /// </list>
    ///
    /// <para><b>It binds itself.</b> Every field below is looked up by name under its own root when
    /// the inspector leaves it empty, so re-laying the scene does not silently leave a screen half
    /// wired - which is exactly how the previous lobby countdown came to be dead (its component was
    /// never placed and nothing said so). An explicit inspector reference always wins.</para>
    ///
    /// <para>Domain colours come from the live theme's per-domain UI accent
    /// (<see cref="SO_ColorSet.GetDomainUIAccentColor"/> via <c>gameData.ThemeManagerData</c>).
    /// Runs on every peer; only the host drives transitions. Roster/history are read from the
    /// persistent <see cref="MaelstromDataSO"/> (the scene's own
    /// <c>gameData.Players/Results</c> are cleared per scene).</para>
    /// </summary>
    public class MaelstromSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] MaelstromDataSO tournamentData;

        [Tooltip("Ready-up + countdown driver. Ensured in code when empty - it needs no scene " +
                 "object of its own, which is the whole point of it (see MaelstromLobby).")]
        [SerializeField] MaelstromLobby lobby;

        [Tooltip("The live preview of the next round. Ensured in code on ConfigurationContent " +
                 "when empty.")]
        [SerializeField] MaelstromPreviewHost previewHost;

        [Header("Hub / stats screen (MaelstromConfigureModal)")]
        [FormerlySerializedAs("activeRoot")]
        [SerializeField] GameObject configureRoot;
        [Tooltip("The panel the entrance/exit animation moves - normally the modal's Content child.")]
        [SerializeField] RectTransform configureContent;
        [SerializeField] TMP_Text titleText;
        [Tooltip("\"GAME POOL : 10 OF 16 MODES (INTENSITY 2)\".")]
        [FormerlySerializedAs("gameModesText")]
        [SerializeField] TMP_Text poolText;
        [Tooltip("\"ROUND N\" in the hub, \"N ROUNDS PLAYED\" on the stats screen.")]
        [FormerlySerializedAs("roundCounterText")]
        [SerializeField] TMP_Text roundStatusText;
        [Tooltip("Subtitle - \"First domain to N points wins\" from the End Game Conditions tool.")]
        [FormerlySerializedAs("raceRuleText")]
        [SerializeField] TMP_Text infoText;
        [SerializeField] TMP_Text leadingDomainText;

        [Tooltip("The 3-2-1. Hidden until the last few seconds, in BOTH the all-ready and the " +
                 "auto-start cases - they are the same deadline, so they look the same.")]
        [FormerlySerializedAs("countdownText")]
        [SerializeField] TMP_Text gameStartText;

        [Tooltip("Optional \"2/4 READY\" line. Without it the tally rides the READY button's label.")]
        [SerializeField] TMP_Text readyTallyText;

        [Tooltip("Where ConfigurationContent's preview is hosted. Only needed so the preview host " +
                 "can be ensured on the right object.")]
        [SerializeField] RectTransform configurationContent;

        [Header("Hub - round scroll")]
        [SerializeField] MaelstromRoundCard roundCardPrefab;
        [SerializeField] Transform historyContent;
        [SerializeField] ScrollRect historyScrollRect;

        [Header("Hub - buttons")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonLabel;
        [Tooltip("Shown INSTEAD of Ready on the stats screen: the tournament is over, so there is " +
                 "no round to ready for - only the results to go on to.")]
        [SerializeField] Button nextButton;

        [Header("Summary panel")]
        [SerializeField] GameObject summaryRoot;
        [SerializeField] RectTransform summaryContent;
        [SerializeField] TMP_Text summaryTitleText;
        [Tooltip("\"GAME WON!\" if the local player's domain won, else \"GAME OVER\".")]
        [SerializeField] TMP_Text summaryInfoText;
        [SerializeField] TMP_Text summaryWinningDomainText;
        [Tooltip("\"DOMAIN RANK :\" + the ranked domains (coloured, animated in).")]
        [SerializeField] TMP_Text summaryRankText;
        [SerializeField] MaelstromSummaryPlayerCard summaryCardPrefab;
        [SerializeField] Transform summaryCardContainer;
        [Tooltip("Back to the stats screen - the same round-by-round breakdown, reachable again " +
                 "after the trophy.")]
        [SerializeField] Button statsScreenButton;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button mainMenuButton;
        [Tooltip("Shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses).")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        [Header("Launch")]
        [Tooltip("Optional full-screen white Image faded up as the round loads, so the hub cuts " +
                 "to the game instead of vanishing. Left empty the launch is simply quieter.")]
        [SerializeField] Image launchFlashSheet;

        [Header("Avatars")]
        [SerializeField] SO_ProfileIconList profileIconList;
        [SerializeField] SO_AIProfileList aiProfileList;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        // Named MaelstromScreen, not Screen: a nested `Screen` shadows UnityEngine.Screen
        // inside this class, and it also makes the project's enum-reference gate attribute every
        // Screen.* call in the codebase to this declaration.
        enum MaelstromScreen { None = 0, Hub = 1, Stats = 2, Summary = 3 }

        MaelstromScreen _screen = MaelstromScreen.None;
        bool _summaryActionTaken;     // anti-spam for Play Again / Main Menu
        bool _launched;               // one-shot launch flourish
        int _lastShownSecs = -1;
        int _lastReadyCount = -1;
        int _lastTotalPlayers = -1;
        bool _lastLocalReady;
        SO_ArcadeGame _revealedGame;
        CanvasGroup _configureGroup;
        CanvasGroup _summaryGroup;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        void Awake()
        {
            AutoBind();

            if (readyButton) readyButton.onClick.AddListener(OnReadyPressed);
            if (nextButton) nextButton.onClick.AddListener(OnNextPressed);
            if (statsScreenButton) statsScreenButton.onClick.AddListener(OnStatsScreenPressed);
            if (playAgainButton) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenuPressed);

            EnsureLobby();
            EnsurePreviewHost();
        }

        void OnDestroy()
        {
            if (readyButton) readyButton.onClick.RemoveListener(OnReadyPressed);
            if (nextButton) nextButton.onClick.RemoveListener(OnNextPressed);
            if (statsScreenButton) statsScreenButton.onClick.RemoveListener(OnStatsScreenPressed);
            if (playAgainButton) playAgainButton.onClick.RemoveListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.RemoveListener(OnMainMenuPressed);
        }

        void Start()
        {
            // Lift the loading splash the Single load left opaque (no vessel here raises
            // OnClientReady on its own until the hub's spawn pair does).
            if (gameData != null) gameData.InvokeClientReady();

            bool decided = MaelstromController.Instance != null && MaelstromController.Instance.IsShowingSummary;
            ShowConfigureScreen(decided ? MaelstromScreen.Stats : MaelstromScreen.Hub);
        }

        void Update()
        {
            if (_screen == MaelstromScreen.Hub) TickHub();
        }

        // ── The hub ──────────────────────────────────────────────────────────────

        void TickHub()
        {
            if (lobby == null) return;

            RefreshPendingRound();
            RefreshReadyState();
            RefreshCountdown();
        }

        /// <summary>
        /// The draw lands a frame or two into the hub (the host draws on its first update; a
        /// client waits for the ticket), so the preview is armed HERE rather than at screen entry -
        /// and only when the answer actually changes, because arming it re-checks a cell swap.
        /// </summary>
        void RefreshPendingRound()
        {
            var game = lobby.PendingGame;
            if (game == _revealedGame) return;

            _revealedGame = game;
            if (game == null) return;

            previewHost?.ShowRound(game, lobby.PendingIntensity, allowFlight: true);
            MaelstromTransitions.PulseScale(configurationContent, 0.06f);
        }

        void RefreshReadyState()
        {
            bool localReady = lobby.LocalReady;
            int ready = lobby.ReadyCount;
            int total = lobby.TotalPlayers;

            // Rebuilt only when something changed: this runs every frame, and formatting two
            // strings per frame for a screen that changes when somebody presses a button is
            // allocation for nothing.
            bool dirty = ready != _lastReadyCount || total != _lastTotalPlayers || localReady != _lastLocalReady;
            if (dirty)
            {
                _lastTotalPlayers = total;
                _lastLocalReady = localReady;

                if (readyButtonLabel)
                {
                    // The tally rides the button when nothing else shows it, so a hub whose scene
                    // has no tally line still answers "who are we waiting for?".
                    string tally = readyTallyText || total <= 0 ? string.Empty : $"  {ready}/{total}";
                    readyButtonLabel.text = localReady ? $"READY ✓{tally}" : $"READY{tally}";
                }

                if (readyTallyText)
                    readyTallyText.text = total > 0 ? $"{ready}/{total} READY" : string.Empty;
            }

            if (ready != _lastReadyCount)
            {
                _lastReadyCount = ready;
                MaelstromTransitions.PulseScale(readyTallyText ? readyTallyText.transform : readyButton?.transform);
            }
        }

        void RefreshCountdown()
        {
            int secs = lobby.SecondsRemaining;
            bool show = lobby.ShowFinalCountdown;

            if (gameStartText)
            {
                if (gameStartText.gameObject.activeSelf != show)
                    gameStartText.gameObject.SetActive(show);

                if (show)
                {
                    gameStartText.text = $"GAME STARTS IN {Mathf.Max(secs, 1)}";
                    if (secs != _lastShownSecs)
                        MaelstromTransitions.CountdownTick(gameStartText, secs, Color.white, CountdownRestColor());
                }
            }

            if (secs != _lastShownSecs) _lastShownSecs = secs;

            // At zero the round is loading. Play the cut once, on every peer: the host's load and
            // the clients' follow both land within a tick of it.
            if (!_launched && lobby.Ticket.IsArmed && secs <= 0)
            {
                _launched = true;
                previewHost?.Hide();
                MaelstromTransitions.LaunchFlourish(_configureGroup, launchFlashSheet);
            }
        }

        Color CountdownRestColor()
        {
            var lead = WinningDomain();
            return lead == Domains.Blue ? Color.white : DomainColor(lead);
        }

        // ── Screens ──────────────────────────────────────────────────────────────

        void ShowConfigureScreen(MaelstromScreen which)
        {
            _screen = which;
            bool stats = which == MaelstromScreen.Stats;

            _launched = false;
            _lastShownSecs = -1;
            _lastReadyCount = -1;
            _revealedGame = null;

            SetRoot(configureRoot, true);
            SetRoot(summaryRoot, false);

            if (!stats) lobby?.ArmForNewHub(gameData, tournamentData);

            int gamesPlayed = tournamentData != null ? tournamentData.GamesPlayed : 0;

            if (titleText) titleText.text = stats
                ? $"{ModeName().ToUpperInvariant()} RESULTS"
                : ModeName().ToUpperInvariant();
            if (poolText) poolText.text = $"GAME POOL : {GameModesPool()}";
            if (roundStatusText) roundStatusText.text = stats
                ? $"{gamesPlayed} ROUNDS PLAYED"
                : $"ROUND {gamesPlayed + 1}";
            if (infoText && tournamentData != null)
                infoText.text = stats
                    ? "Final standings"
                    : $"First domain to {tournamentData.EffectiveWinTarget} points wins";
            RenderLeadingDomain();

            PopulateRoundCards(includePreviewWhenEmpty: !stats);
            AutoScrollToCurrent();

            // Buttons: exactly one of READY / NEXT, never both. The stats screen has no round to
            // ready for, and the hub has nothing to move on to.
            if (readyButton) readyButton.gameObject.SetActive(!stats);
            if (nextButton) nextButton.gameObject.SetActive(stats);
            if (gameStartText) gameStartText.gameObject.SetActive(false);
            if (readyTallyText) readyTallyText.gameObject.SetActive(!stats);

            // MAIN MENU is available in the HUB between games, not only on the final summary -
            // without it a tournament is a thing you cannot leave until it finishes. Only lit
            // when the scene actually parents it somewhere visible from here.
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(true);
            if (playAgainButton) playAgainButton.gameObject.SetActive(false);

            if (stats) ShowStatsEnvironment();

            MaelstromTransitions.PanelIn(_configureGroup, configureContent);
            WarnIfHubExitIsUnreachable();
            WarnIfASecondLaunchAuthorityIsPresent();
        }

        bool _warnedSecondAuthority;

        /// <summary>
        /// The hub's panel was built by copying the arcade modal, which brings
        /// <c>ArcadeGameConfigureModal</c> along with the layout. Only its LAYOUT is wanted here:
        /// that component is the arcade's whole launch flow - it owns a static Instance, resets
        /// <c>ArcadeGameConfigSO</c> in <c>Start</c>, and drives its own ready-up and launch - so
        /// leaving it live puts a second authority in a scene where <see cref="MaelstromLobby"/>
        /// is the one deciding when a round starts.
        ///
        /// <para>Reported rather than switched off: it is the scene author's component and it may
        /// be there deliberately. But two things that can both launch a game is the shape of a
        /// defect nobody attributes to the right screen, so it says so once.</para>
        /// </summary>
        void WarnIfASecondLaunchAuthorityIsPresent()
        {
            if (_warnedSecondAuthority || !configureRoot) return;
            if (!configureRoot.TryGetComponent<ArcadeGameConfigureModal>(out _)) return;

            _warnedSecondAuthority = true;
            CSDebug.LogWarning(
                "[MaelstromSceneView] The hub root also carries an ArcadeGameConfigureModal. Only " +
                "its layout is used here - MaelstromLobby owns the ready-up and the launch - so " +
                "the component is redundant and is a second thing in this scene that can start a " +
                "game. Remove it from MaelstromConfigureModal unless you are using it on purpose; " +
                "the preview is driven through the MinigameLaunchPanel on ConfigurationContent.");
        }

        /// <summary>
        /// The stats screen's preview: the arena of the round just played, orbiting, with no
        /// tap-to-play. The tournament is over - offering a warm-up for a round that will never
        /// happen is an affordance the screen cannot honour.
        /// </summary>
        void ShowStatsEnvironment()
        {
            if (previewHost == null || tournamentData == null) return;

            var history = tournamentData.History;
            if (history == null || history.Count == 0)
            {
                previewHost.Hide();
                return;
            }

            var last = history[history.Count - 1];
            var game = GameByDisplayName(last.ModeDisplayName);
            if (game == null)
            {
                previewHost.Hide();
                return;
            }

            previewHost.ShowRound(game, Mathf.Max(1, last.Intensity), allowFlight: false);
        }

        SO_ArcadeGame GameByDisplayName(string displayName)
        {
            if (tournamentData == null || tournamentData.GameQueue == null || string.IsNullOrEmpty(displayName))
                return null;

            for (int i = 0; i < tournamentData.GameQueue.Count; i++)
            {
                var g = tournamentData.GameQueue[i];
                if (g != null && g.DisplayName == displayName) return g;
            }
            return null;
        }

        void ShowSummaryPanel()
        {
            _screen = MaelstromScreen.Summary;

            previewHost?.Hide();
            MaelstromTransitions.PanelOut(_configureGroup, configureContent);

            SetRoot(configureRoot, false);
            SetRoot(summaryRoot, true);

            var winner = WinningDomain();
            var local = GetLocalDomain();

            if (summaryTitleText) summaryTitleText.text = ModeName().ToUpperInvariant();

            if (summaryInfoText)
                summaryInfoText.text = (local != Domains.Blue && local == winner) ? "GAME WON!" : "GAME OVER";

            if (summaryWinningDomainText)
                summaryWinningDomainText.text = winner == Domains.Blue
                    ? "WINNING DOMAIN : -"
                    : $"WINNING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(DomainColor(winner))}>{winner.ToString().ToUpperInvariant()}</color>";

            BuildSummaryRankText();
            PopulateSummaryCards();

            // Play Again stays host-only (a client cannot restart the party's tournament); Main
            // Menu and Stats are for everyone.
            if (playAgainButton) playAgainButton.gameObject.SetActive(IsHost);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(true);
            if (statsScreenButton) statsScreenButton.gameObject.SetActive(true);

            MaelstromTransitions.PanelIn(_summaryGroup, summaryContent);
        }

        // ── Buttons ──────────────────────────────────────────────────────────────

        public void OnReadyPressed()
        {
            if (_screen != MaelstromScreen.Hub) return;
            lobby?.ToggleLocalReady();
            RefreshReadyState();
            MaelstromTransitions.PulseScale(readyButton ? readyButton.transform : null, 0.16f);
        }

        /// <summary>Stats screen → the trophy.</summary>
        public void OnNextPressed()
        {
            if (_screen != MaelstromScreen.Stats) return;
            ShowSummaryPanel();
        }

        /// <summary>Summary → back to the round-by-round breakdown.</summary>
        public void OnStatsScreenPressed()
        {
            if (_screen != MaelstromScreen.Summary) return;
            MaelstromTransitions.PanelOut(_summaryGroup, summaryContent);
            ShowConfigureScreen(MaelstromScreen.Stats);
        }

        /// <summary>
        /// Back-compat hooks for older button wiring. Kept because a persistent inspector
        /// <c>onClick</c> pointing at a method that no longer exists does not fail loudly - it
        /// logs once and the button silently does nothing, which on THIS screen means a player
        /// who cannot ready up.
        /// </summary>
        public void OnHostStartPressed() => OnReadyPressed();

        /// <inheritdoc cref="OnHostStartPressed"/>
        public void OnReadyButtonPressed() => OnReadyPressed();

        public void OnPlayAgainPressed()
        {
            if (!IsHost || _summaryActionTaken) return;
            if (MaelstromController.Instance == null)
            {
                CSDebug.LogError("[MaelstromSceneView] MaelstromController.Instance is null - cannot restart.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            MaelstromController.Instance.RestartMaelstrom();
        }

        public void OnMainMenuPressed()
        {
            if (_summaryActionTaken) return;

            if (IsHost)
            {
                // Host-initiated return keeps the live Relay - SceneLoader drives a Netcode scene
                // load so the whole party lands in Menu_Main together.
                if (onClickToMainMenu == null)
                {
                    CSDebug.LogError("[MaelstromSceneView] onClickToMainMenu event not wired - cannot return to menu.");
                    return;
                }
                _summaryActionTaken = true;
                DisableEndButtons();
                onClickToMainMenu.Raise();
                return;
            }

            // Client: SceneLoader.ReturnToMainMenu defers scene loads to the server, so raising the
            // SOAP event here would fade to black and wait on the host forever. Leave the party
            // instead (same path as the Scoreboard's Leave Lobby).
            if (PartyInviteController.Instance == null)
            {
                CSDebug.LogError("[MaelstromSceneView] PartyInviteController not available - cannot leave to main menu.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            PartyInviteController.Instance.LeavePartyAndReturnToMenuAsync().Forget();
        }

        void DisableEndButtons()
        {
            previewHost?.Hide();
            if (playAgainButton) playAgainButton.gameObject.SetActive(false);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(false);
            if (statsScreenButton) statsScreenButton.gameObject.SetActive(false);
            if (readyButton) readyButton.gameObject.SetActive(false);
            if (nextButton) nextButton.gameObject.SetActive(false);
        }

        // ── Round cards ──────────────────────────────────────────────────────────

        // One card per completed round, in CHRONOLOGICAL order (Round 1 at the top, newest at the
        // bottom). The last card is marked current; the scroll auto-scrolls DOWN to it. Total Score
        // is the domain cumulative points as-of that round. Before any round, a single preview card
        // shows the upcoming roster.
        void PopulateRoundCards(bool includePreviewWhenEmpty)
        {
            if (!roundCardPrefab || !historyContent || tournamentData == null) return;
            ClearChildren(historyContent);

            var history = tournamentData.History;
            if (history.Count == 0)
            {
                if (includePreviewWhenEmpty)
                {
                    var preview = Instantiate(roundCardPrefab, historyContent);
                    preview.SetupPreview(1, OrderRoster(BuildActiveRoster()), ResolveAvatar, _ => 0, DomainColor);
                    MaelstromTransitions.CardIn(preview.transform, 0);
                }
                return;
            }

            // Running per-domain totals, accumulated chronologically (so each card shows the
            // standings after that round). PointsForPlacement (not PointsForPlace) so this
            // recomputation matches the RecordResults fold exactly - the LAST-placed domain of a
            // round earns 0 whatever the domain count.
            var running = new Dictionary<Domains, int>();
            for (int i = 0; i < history.Count; i++)
            {
                var rec = history[i];
                for (int place = 0; place < rec.DomainOrder.Count; place++)
                {
                    var d = rec.DomainOrder[place];
                    running.TryGetValue(d, out int cur);
                    running[d] = cur + tournamentData.PointsForPlacement(place + 1, rec.DomainOrder.Count);
                }

                var asOf = new Dictionary<Domains, int>(running);
                var card = Instantiate(roundCardPrefab, historyContent);
                card.Setup(rec, ResolveAvatar, d => asOf.TryGetValue(d, out int v) ? v : 0, DomainColor,
                           isCurrent: i == history.Count - 1);
                MaelstromTransitions.CardIn(card.transform, i);
            }
        }

        // Scroll DOWN to the latest round (bottom). Deferred a frame so the layout group / size
        // fitter has rebuilt the content height first - setting the position before layout is why
        // the earlier attempt landed on empty space.
        void AutoScrollToCurrent()
        {
            if (!historyScrollRect || !isActiveAndEnabled) return;
            StartCoroutine(ScrollToBottomRoutine());
        }

        IEnumerator ScrollToBottomRoutine()
        {
            yield return null;   // let the layout build
            if (historyContent is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            if (historyScrollRect) historyScrollRect.verticalNormalizedPosition = 0f;
        }

        // ── Summary content ──────────────────────────────────────────────────────

        // "DOMAIN RANK :" + the ranked domains (each coloured), revealed with a typewriter + pop.
        void BuildSummaryRankText()
        {
            if (!summaryRankText) return;

            var sb = new StringBuilder("DOMAIN RANK :");
            if (tournamentData != null)
            {
                var sorted = tournamentData.BuildSortedStandings();
                for (int i = 0; i < sorted.Count; i++)
                    sb.Append($"\n<color=#{ColorUtility.ToHtmlStringRGB(DomainColor(sorted[i].Domain))}>{sorted[i].Domain.ToString().ToUpperInvariant()}</color>");
            }
            summaryRankText.text = sb.ToString();
            MaelstromTransitions.Reveal(summaryRankText);
        }

        // Per-player summary cards (avatar + name + Total Score), tinted to each player's domain.
        // The FIRST card is full size, the rest 0.9 (set before the pop-in so it animates to the
        // right scale).
        void PopulateSummaryCards()
        {
            if (!summaryCardPrefab || !summaryCardContainer || tournamentData == null) return;
            ClearChildren(summaryCardContainer);

            var roster = OrderRoster(SummaryRoster());
            for (int i = 0; i < roster.Count; i++)
            {
                var s = roster[i];
                var card = Instantiate(summaryCardPrefab, summaryCardContainer);
                card.transform.localScale = i == 0 ? Vector3.one : Vector3.one * 0.9f;
                card.Setup(s.Name, ResolveAvatar(s), s.Domain, StandingPoints(s.Domain), DomainColor(s.Domain));
                card.PlayEntrance(i);
            }
        }

        // The final roster = the last completed round's snapshot (full roster incl. AI + avatars).
        List<MaelstromPlayerSnapshot> SummaryRoster()
        {
            if (tournamentData != null && tournamentData.History.Count > 0)
                return new List<MaelstromPlayerSnapshot>(tournamentData.History[tournamentData.History.Count - 1].Players);
            return BuildActiveRoster();
        }

        int StandingPoints(Domains domain)
        {
            if (tournamentData == null) return 0;
            var s = tournamentData.Standings.Find(x => x.Domain == domain);
            return s != null ? s.TotalPoints : 0;
        }

        void RenderLeadingDomain()
        {
            if (!leadingDomainText) return;
            var lead = WinningDomain();
            leadingDomainText.text = lead == Domains.Blue
                ? "LEADING DOMAIN : -"
                : $"LEADING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(DomainColor(lead))}>{lead.ToString().ToUpperInvariant()}</color>";
            MaelstromTransitions.PulseScale(leadingDomainText.transform);
        }

        // ── Roster sourcing / ordering ───────────────────────────────────────────

        /// <summary>
        /// The roster for the round-0 preview - the connected human players (every peer sees all
        /// Player NetworkObjects via the spawn manager). Between rounds the cards come from History.
        /// </summary>
        List<MaelstromPlayerSnapshot> BuildActiveRoster()
        {
            var list = new List<MaelstromPlayerSnapshot>();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (no != null && no.TryGetComponent<Player>(out var p))
                        list.Add(new MaelstromPlayerSnapshot
                        {
                            Name = p.Name, Domain = p.Domain, AvatarId = p.AvatarId, IsAI = p.IsInitializedAsAI,
                        });
                }
            }
            return list;
        }

        List<MaelstromPlayerSnapshot> OrderRoster(List<MaelstromPlayerSnapshot> roster)
        {
            if (roster == null) return new List<MaelstromPlayerSnapshot>();
            var order = tournamentData != null
                ? tournamentData.BuildSortedStandings().Select(s => s.Domain).ToList()
                : new List<Domains>();

            return roster
                .OrderBy(p => { int idx = order.IndexOf(p.Domain); return idx < 0 ? int.MaxValue : idx; })
                .ThenBy(p => (int)p.Domain)
                .ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        string ModeName() => tournamentData != null ? tournamentData.ModeName : "Maelstrom";

        /// <summary>
        /// The pool line: how many modes THIS run can draw, out of the whole roster, and the
        /// intensity ceiling that decides it. It counts rather than enumerating because the banner
        /// is one short line - it could not hold sixteen mode names - and because an enumeration of
        /// the QUEUE would name modes the chosen intensity cannot draw. The launch panel's
        /// <c>MaelstromPoolListView</c> is where the roster is listed properly.
        /// </summary>
        string GameModesPool()
        {
            if (tournamentData == null || tournamentData.GameQueue == null) return string.Empty;

            int total = tournamentData.GameQueue.Count(g => g != null);
            int ceiling = Mathf.Clamp(tournamentData.IntensityCeiling <= 0 ? 1 : tournamentData.IntensityCeiling, 1, 4);
            int drawable = tournamentData.GamesForIntensity(ceiling).Count;

            return $"{drawable} OF {total} MODES (INTENSITY {ceiling})";
        }

        Domains WinningDomain()
        {
            if (tournamentData == null) return Domains.Blue;
            var sorted = tournamentData.BuildSortedStandings();
            return sorted.Count > 0 ? sorted[0].Domain : Domains.Blue;
        }

        // Domain colour from the live theme's UI accent, grey when unwired.
        Color DomainColor(Domains domain)
        {
            if (gameData != null && gameData.ThemeManagerData != null)
                return gameData.ThemeManagerData.GetDomainUIAccentColor(domain);
            return Color.gray;
        }

        Sprite ResolveAvatar(MaelstromPlayerSnapshot s)
        {
            if (s.IsAI && aiProfileList != null && aiProfileList.aiProfiles != null)
            {
                foreach (var p in aiProfileList.aiProfiles)
                    if (p.Name == s.Name) return p.AvatarSprite;
            }

            if (profileIconList != null && profileIconList.profileIcons != null)
            {
                foreach (var icon in profileIconList.profileIcons)
                    if (icon.Id == s.AvatarId) return icon.IconSprite;
                if (profileIconList.profileIcons.Count > 0)
                    return profileIconList.profileIcons[0].IconSprite;
            }
            return null;
        }

        Domains GetLocalDomain()
        {
            if (gameData != null && gameData.LocalPlayer != null)
                return gameData.LocalPlayer.Domain;

            var nm = NetworkManager.Singleton;
            var playerObj = nm != null ? nm.LocalClient?.PlayerObject : null;
            if (playerObj != null && playerObj.TryGetComponent<Player>(out var local))
                return local.Domain;

            return Domains.Blue;
        }

        static void SetRoot(GameObject root, bool active)
        {
            if (root) root.SetActive(active);
        }

        static void ClearChildren(Transform container)
        {
            if (!container) return;
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);
        }

        // ── Ensuring / binding ───────────────────────────────────────────────────

        void EnsureLobby()
        {
            if (!lobby) lobby = GetComponent<MaelstromLobby>();
            if (!lobby) lobby = gameObject.AddComponent<MaelstromLobby>();
            lobby.ArmForNewHub(gameData, tournamentData);
        }

        void EnsurePreviewHost()
        {
            if (previewHost) return;
            previewHost = GetComponentInChildren<MaelstromPreviewHost>(true);
            if (previewHost) return;

            // Hosted ON ConfigurationContent when the scene offers it, so the window it builds
            // lands inside the panel the layout already sized for it.
            var host = configurationContent ? configurationContent.gameObject : gameObject;
            previewHost = host.AddComponent<MaelstromPreviewHost>();
        }

        /// <summary>
        /// Fill any field the inspector left empty by looking it up by name under its own root.
        /// The scene is re-laid regularly and a missing reference here is silent - a panel simply
        /// never updates - so the names are treated as part of the contract and the misspelling the
        /// scene actually ships (<c>MaelstormSummaryScrollView</c>) is accepted alongside the
        /// correct one.
        /// </summary>
        void AutoBind()
        {
            if (!configureRoot) configureRoot = FindChild(transform, "MaelstromConfigureModal", "ArcadeGameConfigureModal")?.gameObject;
            if (!summaryRoot) summaryRoot = FindChild(transform, "Summary Panel", "SummaryPanel")?.gameObject;

            var cfg = configureRoot ? configureRoot.transform : null;
            var sum = summaryRoot ? summaryRoot.transform : null;

            if (cfg != null)
            {
                if (!configureContent) configureContent = FindChild(cfg, "Content") as RectTransform;
                if (!titleText) titleText = Text(cfg, "Title Text (TMP)", "TitleText");
                if (!poolText) poolText = Text(cfg, "PoolText");
                if (!roundStatusText) roundStatusText = Text(cfg, "RoundStatusText");
                if (!infoText) infoText = Text(cfg, "InfoText");
                if (!leadingDomainText) leadingDomainText = Text(cfg, "LeadingDomainText");
                if (!gameStartText) gameStartText = Text(cfg, "GameStartText");
                if (!readyTallyText) readyTallyText = Text(cfg, "ReadyTallyText");
                if (!configurationContent) configurationContent = FindChild(cfg, "ConfigurationContent") as RectTransform;

                if (!readyButton) readyButton = Find<Button>(cfg, "ReadyButton", "Start Button");
                if (!nextButton) nextButton = Find<Button>(cfg, "NextButton");
                if (!readyButtonLabel && readyButton)
                    readyButtonLabel = readyButton.GetComponentInChildren<TMP_Text>(true);

                if (!historyScrollRect)
                    historyScrollRect = Find<ScrollRect>(cfg, "MaelstormSummaryScrollView", "MaelstromSummaryScrollView");
                if (!historyScrollRect)
                    historyScrollRect = cfg.GetComponentInChildren<ScrollRect>(true);
                if (!historyContent && historyScrollRect) historyContent = historyScrollRect.content;
            }

            if (sum != null)
            {
                if (!summaryContent) summaryContent = FindChild(sum, "Content") as RectTransform;
                if (!summaryTitleText) summaryTitleText = Text(sum, "Title Text (TMP)", "TitleText");
                if (!summaryInfoText) summaryInfoText = Text(sum, "InfoText");
                if (!summaryRankText) summaryRankText = Text(sum, "RankText");
                if (!summaryWinningDomainText) summaryWinningDomainText = Text(sum, "LeadingDomainText", "WinningDomainText");
                if (!summaryCardContainer) summaryCardContainer = FindChild(sum, "Content");

                if (!statsScreenButton) statsScreenButton = Find<Button>(sum, "StatsScreenButton", "Stats Button");
                if (!playAgainButton) playAgainButton = Find<Button>(sum, "PlayAgain Button", "PlayAgainButton");
                if (!mainMenuButton) mainMenuButton = Find<Button>(sum, "Main Menu Button", "MainMenuButton");
            }

            _configureGroup = EnsureGroup(configureRoot);
            _summaryGroup = EnsureGroup(summaryRoot);
        }

        static CanvasGroup EnsureGroup(GameObject root)
        {
            if (!root) return null;
            return root.TryGetComponent<CanvasGroup>(out var g) ? g : root.AddComponent<CanvasGroup>();
        }

        TMP_Text Text(Transform root, params string[] names)
        {
            var t = FindChild(root, names);
            return t ? t.GetComponent<TMP_Text>() : null;
        }

        static T Find<T>(Transform root, params string[] names) where T : Component
        {
            var t = FindChild(root, names);
            return t ? t.GetComponent<T>() : null;
        }

        /// <summary>
        /// Depth-first search for the first descendant matching any of <paramref name="names"/>,
        /// INCLUDING inactive ones - the 3-2-1 and the NEXT button both ship switched off, which is
        /// exactly when they need finding.
        /// </summary>
        static Transform FindChild(Transform root, params string[] names)
        {
            if (!root || names == null) return null;

            for (int n = 0; n < names.Length; n++)
            {
                var hit = SearchDepthFirst(root, names[n]);
                if (hit) return hit;
            }
            return null;
        }

        static Transform SearchDepthFirst(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child;

                var deeper = SearchDepthFirst(child, name);
                if (deeper) return deeper;
            }
            return null;
        }

        bool _warnedHubExitUnreachable;

        /// <summary>
        /// Activating a button inside a DEACTIVATED parent shows nothing, and shows nothing
        /// SILENTLY - the call succeeds, the flag reads true, and the player still has no way out
        /// of the hub. Main Menu is authored on the summary screen, so if the scene leaves it
        /// parented under <c>summaryRoot</c> the hub exit is inert and no amount of code here can
        /// reach it: it needs a second instance under the configure root.
        /// </summary>
        void WarnIfHubExitIsUnreachable()
        {
            if (_warnedHubExitUnreachable || !mainMenuButton) return;
            if (mainMenuButton.gameObject.activeInHierarchy) return;

            _warnedHubExitUnreachable = true;
            CSDebug.LogWarning(
                "[MaelstromSceneView] The hub's MAIN MENU button is active but not visible - it is " +
                "parented under an inactive root (almost certainly the Summary Panel). Until a copy " +
                "of it lives under ArcadeGameConfigureModal, a player has NO way to leave a " +
                "tournament between rounds.");
        }
    }
}
