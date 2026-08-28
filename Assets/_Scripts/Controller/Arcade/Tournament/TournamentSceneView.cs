using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Data-driven view for the Maelstrom scene. Two panels, picked by <see cref="TournamentController"/>'s
    /// phase:
    ///
    ///   • <b>Intro/active panel</b> (activeRoot) - used for the lobby, the between-round hub, AND the
    ///     end-of-tournament results entry. Shows the top bar (mode pool, round, leading domain), the
    ///     scroll of <b>Tournament Data Cards</b> (one per round, newest on top), and the button:
    ///       – lobby/hub: <b>START</b> + an animated countdown that <b>auto-starts</b> the round.
    ///       – complete (summary phase): <b>NEXT</b> → reveals the summary panel (no countdown).
    ///   • <b>Summary panel</b> (summaryRoot) - winning-domain banner + the final domain ranking, with
    ///     host-only Play Again and an everyone-visible Main Menu (the host's press takes the whole
    ///     party back over the live Relay; a client's press leaves the party and returns solo).
    ///
    /// Domain colours come from the live theme's per-domain UI accent
    /// (<see cref="SO_ColorSet.GetDomainUIAccentColor"/> via <c>gameData.ThemeManagerData</c> - no
    /// Graphic arrays, no parallel palette asset). Runs on every
    /// peer; only the host drives transitions. Roster/history are read from the persistent
    /// <see cref="TournamentDataSO"/> (the scene is UI-only, so <c>gameData.Players/Results</c> are cleared).
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;
        [Tooltip("Networked ready-up + countdown. Optional: without it the countdown still ticks/auto-starts " +
                 "locally (host) for panel testing.")]
        [SerializeField] TournamentLobbyNetwork lobbyNetwork;

        [Header("Shared")]
        [SerializeField] TMP_Text titleText;

        [Header("Active - top bar")]
        [SerializeField] GameObject activeRoot;
        [Tooltip("The mode pool, e.g. \"GAMEMODES : HEX RACE - SCRUM - JOUST\".")]
        [SerializeField] TMP_Text gameModesText;
        [Tooltip("\"ROUND N\".")]
        [SerializeField] TMP_Text roundCounterText;
        [Tooltip("Subtitle - auto-filled \"First domain to N points wins\", where N is the Maelstrom " +
                 "win target from FrogletTools > Game Modes > End Game Conditions (TournamentDataSO.EffectiveWinTarget).")]
        [SerializeField] TMP_Text raceRuleText;
        [Tooltip("\"LEADING DOMAIN : JADE\" (domain name coloured from the theme accent).")]
        [SerializeField] TMP_Text leadingDomainText;

        [Header("Active - round scroll")]
        [SerializeField] TournamentRoundCard roundCardPrefab;
        [SerializeField] Transform historyContent;
        [SerializeField] ScrollRect historyScrollRect;

        [Header("Active - START / NEXT button + countdown")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonLabel;
        [Tooltip("Animated countdown text, e.g. \"Game will start in 12s\". Pulses each tick.")]
        [SerializeField] TMP_Text countdownText;
        [Tooltip("Optional \"x / y ready\" tally.")]
        [SerializeField] TMP_Text readyTallyText;
        [Tooltip("Display countdown used when no TournamentLobbyNetwork is wired (host auto-starts at 0).")]
        [SerializeField, Min(1f)] float localCountdownSeconds = 30f;

        [Header("Summary panel")]
        [SerializeField] GameObject summaryRoot;
        [Tooltip("Summary title - set to the mode name (\"MAELSTROM\").")]
        [SerializeField] TMP_Text summaryTitleText;
        [Tooltip("\"GAME WON!\" if the local player's domain won, else \"GAME OVER\".")]
        [SerializeField] TMP_Text summaryInfoText;
        [Tooltip("\"WINNING DOMAIN : JADE\" (domain name coloured).")]
        [SerializeField] TMP_Text summaryWinningDomainText;
        [Tooltip("\"DOMAIN RANK :\" + the ranked domains (coloured, animated in).")]
        [SerializeField] TMP_Text summaryRankText;
        [Tooltip("Per-player summary card (MaelstromSummaryScoreCardContainer).")]
        [SerializeField] TournamentSummaryPlayerCard summaryCardPrefab;
        [SerializeField] Transform summaryCardContainer;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button mainMenuButton;
        [Tooltip("Shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses).")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        [Header("Avatars")]
        [SerializeField] SO_ProfileIconList profileIconList;
        [SerializeField] SO_AIProfileList aiProfileList;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        bool _active;
        bool _summaryMode;          // complete phase - the intro panel shows NEXT → summary
        bool _summaryActionTaken;   // anti-spam for Play Again / Main Menu
        bool _localStarted;         // local-fallback auto-start guard
        int _lastShownSecs = -1;
        float _localCountdownEnd;

        void Awake()
        {
            if (readyButton) readyButton.onClick.AddListener(OnReadyButtonPressed);
            if (playAgainButton) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenuPressed);
        }

        void OnDestroy()
        {
            if (readyButton) readyButton.onClick.RemoveListener(OnReadyButtonPressed);
            if (playAgainButton) playAgainButton.onClick.RemoveListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.RemoveListener(OnMainMenuPressed);
        }

        void Start()
        {
            // Lift the loading splash the Single load left opaque (no vessel here to raise OnClientReady).
            if (gameData != null) gameData.InvokeClientReady();

            bool summary = TournamentController.Instance != null && TournamentController.Instance.IsShowingSummary;
            ShowActive(summary);
        }

        void Update()
        {
            if (!_active || _summaryMode) return;

            int secs = CountdownSeconds();

            if (countdownText)
            {
                countdownText.text = $"Game will start in {secs}s";
                if (secs != _lastShownSecs)
                {
                    _lastShownSecs = secs;
                    Pulse(countdownText.transform);
                }
            }

            if (lobbyNetwork != null)
            {
                if (readyButtonLabel) readyButtonLabel.text = lobbyNetwork.LocalReady ? "READY ✓" : "START";
                if (readyTallyText) readyTallyText.text = $"{lobbyNetwork.ReadyCount}/{lobbyNetwork.TotalPlayers} ready";
            }
            else if (IsHost && !_localStarted && secs <= 0)
            {
                // Local fallback: auto-start at 0 (the networked path auto-starts inside TournamentLobbyNetwork).
                _localStarted = true;
                TournamentController.Instance?.BeginNextRound();
            }
        }

        // ── Intro / active panel (lobby, hub, summary-entry) ─────────────────────────

        void ShowActive(bool summaryMode)
        {
            _active = true;
            _summaryMode = summaryMode;
            _localStarted = false;
            _lastShownSecs = -1;
            _localCountdownEnd = Time.unscaledTime + localCountdownSeconds;

            SetRoot(activeRoot, true);
            SetRoot(summaryRoot, false);

            int gamesPlayed = tournamentData != null ? tournamentData.GamesPlayed : 0;

            if (titleText) titleText.text = summaryMode ? $"{ModeName().ToUpperInvariant()} RESULTS" : ModeName().ToUpperInvariant();
            if (gameModesText) gameModesText.text = $"GAMEMODES : {GameModesPool()}";
            if (roundCounterText) roundCounterText.text = summaryMode ? $"{gamesPlayed} ROUNDS PLAYED" : $"ROUND {gamesPlayed + 1}";
            if (raceRuleText && tournamentData != null) raceRuleText.text = $"First domain to {tournamentData.EffectiveWinTarget} points wins";
            RenderLeadingDomain();

            PopulateRoundCards(includePreviewWhenEmpty: !summaryMode);
            AutoScrollToCurrent();

            // Button + countdown.
            if (countdownText) countdownText.gameObject.SetActive(!summaryMode);
            if (readyTallyText) readyTallyText.gameObject.SetActive(!summaryMode && lobbyNetwork != null);
            if (readyButton) readyButton.gameObject.SetActive(true);
            if (readyButtonLabel)
                readyButtonLabel.text = summaryMode
                    ? "NEXT"
                    : (lobbyNetwork != null && lobbyNetwork.LocalReady ? "READY ✓" : "START");
        }

        void RenderLeadingDomain()
        {
            if (!leadingDomainText) return;
            var lead = WinningDomain();
            leadingDomainText.text = lead == Domains.Blue
                ? "LEADING DOMAIN : -"
                : $"LEADING DOMAIN : <color=#{ColorUtility.ToHtmlStringRGB(DomainColor(lead))}>{lead.ToString().ToUpperInvariant()}</color>";
        }

        // One Tournament Data Card per completed round, in CHRONOLOGICAL order (Round 1 at the top,
        // newest at the bottom). The last card is marked current; the scroll auto-scrolls DOWN to it.
        // Total Score is the domain cumulative points as-of that round. Before any round, a single
        // preview card shows the upcoming roster.
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
                }
                return;
            }

            // Running per-domain totals, accumulated chronologically (so each card shows the standings
            // after that round). Cards are instantiated in the same order → Round 1 first (top).
            // PointsForPlacement (not PointsForPlace) so this recomputation matches the RecordResults
            // fold exactly - the LAST-placed domain of a round earns 0 whatever the domain count.
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
            }
        }

        // Scroll DOWN to the latest round (bottom). Deferred a frame so the layout group / size fitter
        // has rebuilt the content height first - setting the position before layout is why the earlier
        // attempt landed on empty space. (Requires a VerticalLayoutGroup + ContentSizeFitter on Content.)
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

        public void OnReadyButtonPressed()
        {
            // Defense-in-depth against a double-wired button (inspector onClick + this code listener):
            // ShowSummaryPanel() clears _active, so a stray second synchronous invocation can't fall
            // through to the round-start path and launch a game off the summary screen. The onClick is
            // code-wired only now (the inspector OnHostStartPressed entries were removed from
            // Maelstrom.unity - they double-fired NEXT/Play Again/Main Menu into BeginNextRound).
            if (!_active) return;

            if (_summaryMode) { ShowSummaryPanel(); return; }   // NEXT → results

            if (lobbyNetwork != null)
            {
                lobbyNetwork.ToggleLocalReady();   // host-authoritative countdown drives the start
                return;
            }

            if (IsHost && !_localStarted)   // degraded path - start immediately
            {
                _localStarted = true;
                TournamentController.Instance?.BeginNextRound();
            }
        }

        /// <summary>Back-compat hook for the old host Start button wiring.</summary>
        public void OnHostStartPressed() => OnReadyButtonPressed();

        // ── Summary panel (results) ──────────────────────────────────────────────────

        void ShowSummaryPanel()
        {
            _active = false;
            _summaryMode = false;
            SetRoot(activeRoot, false);
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

            // Play Again stays host-only (a client cannot restart the party's tournament), but Main
            // Menu is available to every peer - the host takes the whole party back, a client leaves.
            if (playAgainButton) playAgainButton.gameObject.SetActive(IsHost);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(true);
        }

        // "DOMAIN RANK :" + the ranked domains (each coloured), revealed with an AAA typewriter + pop.
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
            AnimateRankText(summaryRankText);
        }

        static void AnimateRankText(TMP_Text txt)
        {
            txt.transform.DOKill();
            txt.DOKill();
            txt.ForceMeshUpdate();
            int total = Mathf.Max(1, txt.textInfo.characterCount);

            txt.maxVisibleCharacters = 0;
            txt.transform.localScale = Vector3.one * 0.9f;
            txt.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack).SetUpdate(true);
            DOTween.To(() => txt.maxVisibleCharacters, x => txt.maxVisibleCharacters = x, total, 0.7f)
                .SetEase(Ease.Linear).SetUpdate(true);
        }

        // Per-player summary cards (avatar + name + Total Score), tinted to each player's domain. The
        // FIRST card is full size, the rest 0.9 (set before the pop-in so it animates to the right scale).
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
        List<TournamentPlayerSnapshot> SummaryRoster()
        {
            if (tournamentData != null && tournamentData.History.Count > 0)
                return new List<TournamentPlayerSnapshot>(tournamentData.History[tournamentData.History.Count - 1].Players);
            return BuildActiveRoster();
        }

        int StandingPoints(Domains domain)
        {
            if (tournamentData == null) return 0;
            var s = tournamentData.Standings.Find(x => x.Domain == domain);
            return s != null ? s.TotalPoints : 0;
        }

        public void OnPlayAgainPressed()
        {
            if (!IsHost || _summaryActionTaken) return;
            if (TournamentController.Instance == null)
            {
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null - cannot restart.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            TournamentController.Instance.RestartTournament();
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
                    CSDebug.LogError("[TournamentSceneView] onClickToMainMenu event not wired - cannot return to menu.");
                    return;
                }
                _summaryActionTaken = true;
                DisableEndButtons();
                onClickToMainMenu.Raise();
                return;
            }

            // Client: SceneLoader.ReturnToMainMenu defers scene loads to the server, so raising the
            // SOAP event here would fade to black and wait on the host forever. Leave the party
            // instead (same path as the Scoreboard's Leave Lobby) - disconnects, loads Menu_Main
            // locally, and restarts a solo Relay session; TournamentController clears tournament
            // state on the Menu_Main load.
            if (PartyInviteController.Instance == null)
            {
                CSDebug.LogError("[TournamentSceneView] PartyInviteController not available - cannot leave to main menu.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            PartyInviteController.Instance.LeavePartyAndReturnToMenuAsync().Forget();
        }

        void DisableEndButtons()
        {
            if (playAgainButton) playAgainButton.gameObject.SetActive(false);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(false);
        }

        // ── Roster sourcing / ordering ──────────────────────────────────────────────

        /// <summary>
        /// The roster for the round-0 preview - the connected human players (every peer sees all Player
        /// NetworkObjects via the spawn manager). Between rounds the cards come from History instead.
        /// </summary>
        List<TournamentPlayerSnapshot> BuildActiveRoster()
        {
            var list = new List<TournamentPlayerSnapshot>();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                foreach (var no in nm.SpawnManager.SpawnedObjectsList)
                {
                    if (no != null && no.TryGetComponent<Player>(out var p))
                        list.Add(new TournamentPlayerSnapshot
                        {
                            Name = p.Name, Domain = p.Domain, AvatarId = p.AvatarId, IsAI = p.IsInitializedAsAI,
                        });
                }
            }
            return list;
        }

        List<TournamentPlayerSnapshot> OrderRoster(List<TournamentPlayerSnapshot> roster)
        {
            if (roster == null) return new List<TournamentPlayerSnapshot>();
            var order = tournamentData != null
                ? tournamentData.BuildSortedStandings().Select(s => s.Domain).ToList()
                : new List<Domains>();

            return roster
                .OrderBy(p => { int idx = order.IndexOf(p.Domain); return idx < 0 ? int.MaxValue : idx; })
                .ThenBy(p => (int)p.Domain)
                .ToList();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        string ModeName() => tournamentData != null ? tournamentData.ModeName : "Maelstrom";

        string GameModesPool()
        {
            if (tournamentData == null || tournamentData.GameQueue == null) return string.Empty;
            return string.Join(" - ", tournamentData.GameQueue
                .Where(g => g != null && !string.IsNullOrEmpty(g.DisplayName))
                .Select(g => g.DisplayName.ToUpperInvariant()));
        }

        Domains WinningDomain()
        {
            if (tournamentData == null) return Domains.Blue;
            var sorted = tournamentData.BuildSortedStandings();
            return sorted.Count > 0 ? sorted[0].Domain : Domains.Blue;
        }

        // Domain colour from the live theme's UI accent (SO_ColorSet.GetDomainUIAccentColor), grey when unwired.
        Color DomainColor(Domains domain)
        {
            if (gameData != null && gameData.ThemeManagerData != null) return gameData.ThemeManagerData.GetDomainUIAccentColor(domain);
            return Color.gray;
        }

        Sprite ResolveAvatar(TournamentPlayerSnapshot s)
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

        static void Pulse(Transform t)
        {
            t.DOKill(true);
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f).SetUpdate(true);
        }

        int CountdownSeconds()
        {
            if (lobbyNetwork != null) return lobbyNetwork.SecondsRemaining;
            return Mathf.Max(0, Mathf.CeilToInt(_localCountdownEnd - Time.unscaledTime));
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
    }
}
