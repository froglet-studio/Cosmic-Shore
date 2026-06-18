using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Data-driven view for the Maelstrom scene, three layouts picked by <see cref="TournamentController"/>'s
    /// phase:
    ///
    ///   • <b>Active</b> (intro lobby OR between-round hub, phase Lobby) — a top bar (mode pool, round
    ///     index, leading domain + cumulative standings), a scroll of <b>Tournament Data Cards</b> (one
    ///     per completed round, each nesting its players' <b>Player Data Cards</b>; a round-0 preview card
    ///     shows the upcoming roster), and a networked Ready/START button whose 30s/5s countdown renders
    ///     in the label.
    ///   • <b>Summary</b> (phase Summary) — the winning-domain banner + the full round history; Next →
    ///     rank panel.
    ///   • <b>Rank panel</b> — the lightweight final domain ranking + host-only Play Again / Main Menu.
    ///
    /// Runs on every peer; only the host drives transitions. Roster/history are read from the persistent
    /// <see cref="TournamentDataSO"/> (the scene is UI-only, so <c>gameData.Players/Results</c> are cleared).
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Tooltip("Networked ready-up + countdown. Optional: if unwired, the START button degrades to a " +
                 "host-only immediate start (no 30s/5s countdown).")]
        [SerializeField] TournamentLobbyNetwork lobbyNetwork;

        [Header("Shared")]
        [SerializeField] TMP_Text titleText;

        [Header("Active — top bar")]
        [SerializeField] GameObject activeRoot;
        [Tooltip("The mode pool, e.g. \"HEX RACE - JOUST - CRYSTAL CAPTURE\" (shows the pool, never what's next).")]
        [SerializeField] TMP_Text gameModesText;
        [Tooltip("\"ROUND N\" — the round about to be played (length is variable, so avoid \"/ 6\").")]
        [SerializeField] TMP_Text roundCounterText;
        [Tooltip("Subtitle, e.g. \"First domain to 6 points wins\" — the X is filled from WinTarget.")]
        [SerializeField] TMP_Text raceRuleText;
        [Tooltip("The leading domain's name (tinted via leadingDomainColorTargets).")]
        [SerializeField] TMP_Text leadingDomainText;
        [SerializeField] Graphic[] leadingDomainColorTargets;
        [Tooltip("Optional cumulative standings strip, e.g. \"JADE 4   RUBY 2   GOLD 1\" — shows the race tally.")]
        [SerializeField] TMP_Text standingsText;

        [Header("Active — round scroll")]
        [Tooltip("Tournament Data Card prefab (round header + nested Player Data Cards).")]
        [SerializeField] TournamentRoundCard roundCardPrefab;
        [SerializeField] Transform historyContent;
        [SerializeField] ScrollRect historyScrollRect;

        [Header("Active — START / ready button")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonLabel;
        [Tooltip("Optional \"x / y ready\" tally.")]
        [SerializeField] TMP_Text readyTallyText;

        [Header("Summary layout")]
        [SerializeField] GameObject summaryRoot;
        [SerializeField] TMP_Text winnerBannerText;
        [SerializeField] Graphic[] winnerBannerColorTargets;
        [Tooltip("Round history for the summary (all rounds). Reuses roundCardPrefab.")]
        [SerializeField] Transform summaryHistoryContent;
        [SerializeField] Button nextButton;

        [Header("Rank panel (after Next)")]
        [SerializeField] GameObject rankRoot;
        [SerializeField] TournamentDomainScoreView rankRowPrefab;
        [SerializeField] Transform rankContainer;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button mainMenuButton;
        [Tooltip("Shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses).")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        [Header("Avatars")]
        [SerializeField] SO_ProfileIconList profileIconList;
        [SerializeField] SO_AIProfileList aiProfileList;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        bool _active;
        bool _summaryActionTaken;
        int _lastShownSecs = -1;

        void Awake()
        {
            if (readyButton) readyButton.onClick.AddListener(OnReadyButtonPressed);
            if (nextButton) nextButton.onClick.AddListener(OnNextButtonPressed);
            if (playAgainButton) playAgainButton.onClick.AddListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.AddListener(OnMainMenuPressed);
        }

        void OnDestroy()
        {
            if (readyButton) readyButton.onClick.RemoveListener(OnReadyButtonPressed);
            if (nextButton) nextButton.onClick.RemoveListener(OnNextButtonPressed);
            if (playAgainButton) playAgainButton.onClick.RemoveListener(OnPlayAgainPressed);
            if (mainMenuButton) mainMenuButton.onClick.RemoveListener(OnMainMenuPressed);
        }

        void Start()
        {
            // Lift the loading splash the Single load left opaque (no vessel here to raise OnClientReady).
            if (gameData != null) gameData.InvokeClientReady();

            bool summary = TournamentController.Instance != null && TournamentController.Instance.IsShowingSummary;
            if (summary) ShowSummary();
            else ShowActive();
        }

        void Update()
        {
            if (!_active || lobbyNetwork == null) return;

            int secs = lobbyNetwork.SecondsRemaining;
            if (readyButtonLabel)
            {
                string state = lobbyNetwork.LocalReady ? "READY ✓" : "START";
                readyButtonLabel.text = $"{state}   {secs}";

                if (secs != _lastShownSecs)   // AAA polish: punch on each tick
                {
                    _lastShownSecs = secs;
                    readyButtonLabel.transform.DOKill(true);
                    readyButtonLabel.transform.localScale = Vector3.one;
                    readyButtonLabel.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f).SetUpdate(true);
                }
            }
            if (readyTallyText)
                readyTallyText.text = $"{lobbyNetwork.ReadyCount}/{lobbyNetwork.TotalPlayers} ready";
        }

        // ── Active layout (lobby + hub) ─────────────────────────────────────────────

        void ShowActive()
        {
            _active = true;
            SetRoot(activeRoot, true);
            SetRoot(summaryRoot, false);
            SetRoot(rankRoot, false);

            if (titleText) titleText.text = ModeName().ToUpperInvariant();
            if (gameModesText) gameModesText.text = GameModesPool();

            int gamesPlayed = tournamentData != null ? tournamentData.GamesPlayed : 0;
            if (roundCounterText) roundCounterText.text = $"ROUND {gamesPlayed + 1}";
            if (raceRuleText && tournamentData != null)
                raceRuleText.text = $"First domain to {tournamentData.WinTarget} points wins";

            RenderLeadingDomain();
            if (standingsText) standingsText.text = StandingsTally();

            PopulateRoundCards(historyContent, includePreviewWhenEmpty: true);
            AutoScrollToLatest();

            ConfigureReadyButton();
        }

        void RenderLeadingDomain()
        {
            var lead = WinningDomain();   // best-first standings leader
            if (leadingDomainText)
                leadingDomainText.text = lead == Domains.Blue ? "—" : lead.ToString().ToUpperInvariant();

            if (leadingDomainColorTargets != null)
            {
                var c = DomainColor(lead);
                foreach (var g in leadingDomainColorTargets) if (g) g.color = c;
            }
        }

        // One Tournament Data Card per completed round (with per-player Round + Total scores), newest last
        // + highlighted. Total Score is the player's DOMAIN cumulative tournament points AS-OF that round
        // (so it climbs across cards). Before any round is played, a single preview card shows the
        // upcoming roster (no round score, no winner, totals at 0).
        void PopulateRoundCards(Transform content, bool includePreviewWhenEmpty)
        {
            if (!roundCardPrefab || !content || tournamentData == null) return;
            ClearChildren(content);

            var history = tournamentData.History;
            if (history.Count == 0)
            {
                if (includePreviewWhenEmpty)
                {
                    var preview = Instantiate(roundCardPrefab, content);
                    preview.SetupPreview(1, OrderRoster(BuildActiveRoster()), DomainColor, ResolveAvatar, _ => 0);
                }
                return;
            }

            // Running per-domain points, accumulated round by round, so each card shows the standings
            // as they stood after that round (matches Standings.TotalPoints at the final round).
            var running = new Dictionary<Domains, int>();
            for (int i = 0; i < history.Count; i++)
            {
                var rec = history[i];
                for (int place = 0; place < rec.DomainOrder.Count; place++)
                {
                    var d = rec.DomainOrder[place];
                    running.TryGetValue(d, out int cur);
                    running[d] = cur + tournamentData.PointsForPlace(place + 1);
                }

                var asOf = new Dictionary<Domains, int>(running);   // snapshot for the card's closure
                var card = Instantiate(roundCardPrefab, content);
                card.Setup(rec, DomainColor, ResolveAvatar,
                           d => asOf.TryGetValue(d, out int v) ? v : 0,
                           isCurrent: i == history.Count - 1);
            }
        }

        void AutoScrollToLatest()
        {
            // AAA polish: smoothly scroll to the latest (bottom) round card on entry. Assumes
            // newest-at-bottom append order (verticalNormalizedPosition 0 = bottom).
            if (!historyScrollRect) return;
            Canvas.ForceUpdateCanvases();
            historyScrollRect.verticalNormalizedPosition = 1f;
            historyScrollRect.DOKill();
            historyScrollRect.DOVerticalNormalizedPos(0f, 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        void ConfigureReadyButton()
        {
            if (!readyButton) return;

            bool show = lobbyNetwork != null || IsHost;
            readyButton.gameObject.SetActive(show);
            readyButton.interactable = true;

            if (lobbyNetwork == null && readyButtonLabel)
                readyButtonLabel.text = "START";
        }

        public void OnReadyButtonPressed()
        {
            if (lobbyNetwork != null)
            {
                lobbyNetwork.ToggleLocalReady();   // host-authoritative countdown drives the actual start
                return;
            }

            if (IsHost && TournamentController.Instance != null)   // degraded path — no countdown wired
            {
                if (readyButton) readyButton.interactable = false;
                TournamentController.Instance.BeginNextRound();
            }
        }

        /// <summary>Back-compat hook for the old host Start button wiring — routes to the ready flow.</summary>
        public void OnHostStartPressed() => OnReadyButtonPressed();

        // ── Summary layout ──────────────────────────────────────────────────────────

        void ShowSummary()
        {
            _active = false;
            SetRoot(activeRoot, false);
            SetRoot(summaryRoot, true);
            SetRoot(rankRoot, false);

            if (titleText) titleText.text = $"{ModeName().ToUpperInvariant()} RESULTS";

            var winner = WinningDomain();
            if (winnerBannerText)
                winnerBannerText.text = winner == Domains.Blue ? "GAME OVER" : $"{winner.ToString().ToUpperInvariant()} WINS";
            if (winnerBannerColorTargets != null)
            {
                var c = DomainColor(winner);
                foreach (var g in winnerBannerColorTargets) if (g) g.color = c;
            }

            PopulateRoundCards(summaryHistoryContent, includePreviewWhenEmpty: false);

            if (nextButton) nextButton.gameObject.SetActive(true);
        }

        public void OnNextButtonPressed()
        {
            SetRoot(summaryRoot, false);   // local navigation — reveal the rank panel on this peer
            ShowRank();
        }

        // ── Rank panel ──────────────────────────────────────────────────────────────

        void ShowRank()
        {
            _active = false;
            SetRoot(rankRoot, true);

            PopulateRankRows();

            if (playAgainButton) playAgainButton.gameObject.SetActive(IsHost);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(IsHost);
        }

        void PopulateRankRows()
        {
            if (!rankRowPrefab || !rankContainer || tournamentData == null) return;
            ClearChildren(rankContainer);

            var local = GetLocalDomain();
            var sorted = tournamentData.BuildSortedStandings();
            for (int i = 0; i < sorted.Count; i++)
            {
                var standing = sorted[i];
                var row = Instantiate(rankRowPrefab, rankContainer);
                row.Setup(standing.Domain, DomainColor(standing.Domain), standing.TotalPoints, i + 1,
                          standing.Domain == local);
            }
        }

        public void OnPlayAgainPressed()
        {
            if (!IsHost || _summaryActionTaken) return;
            if (TournamentController.Instance == null)
            {
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null — cannot restart.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            TournamentController.Instance.RestartTournament();
        }

        public void OnMainMenuPressed()
        {
            if (!IsHost || _summaryActionTaken) return;
            if (onClickToMainMenu == null)
            {
                CSDebug.LogError("[TournamentSceneView] onClickToMainMenu event not wired — cannot return to menu.");
                return;
            }
            _summaryActionTaken = true;
            DisableEndButtons();
            onClickToMainMenu.Raise();
        }

        void DisableEndButtons()
        {
            if (playAgainButton) playAgainButton.gameObject.SetActive(false);
            if (mainMenuButton) mainMenuButton.gameObject.SetActive(false);
        }

        // ── Roster sourcing / ordering ──────────────────────────────────────────────

        /// <summary>
        /// The roster for the round-0 preview. Between rounds the cards come from History instead. On the
        /// round-0 lobby (no history yet, AI not spawned) it's the connected human players — every peer
        /// sees all Player NetworkObjects via the spawn manager.
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

        // Orders a roster by the overall tournament leader (domain standing), then by enum order — so
        // teammates group under their team and the leading team shows first.
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

        // The mode POOL (display names), joined — shows variety, never what's next.
        string GameModesPool()
        {
            if (tournamentData == null || tournamentData.GameQueue == null) return string.Empty;
            return string.Join(" - ", tournamentData.GameQueue
                .Where(g => g != null && !string.IsNullOrEmpty(g.DisplayName))
                .Select(g => g.DisplayName.ToUpperInvariant()));
        }

        // Cumulative race tally, best-first: "JADE 4   RUBY 2   GOLD 1".
        string StandingsTally()
        {
            if (tournamentData == null) return string.Empty;
            var sorted = tournamentData.BuildSortedStandings();
            var sb = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append("   ");
                sb.Append(sorted[i].Domain.ToString().ToUpperInvariant()).Append(' ').Append(sorted[i].TotalPoints);
            }
            return sb.ToString();
        }

        Domains WinningDomain()
        {
            if (tournamentData == null) return Domains.Blue;
            var sorted = tournamentData.BuildSortedStandings();
            return sorted.Count > 0 ? sorted[0].Domain : Domains.Blue;
        }

        Color DomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.gray;

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

        /// <summary>
        /// The local player's domain for the "(You)" markers. <c>gameData.LocalPlayer</c> is null on this
        /// UI-only scene, so fall back to the persistent local Player NetworkObject. Blue tags nothing.
        /// </summary>
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
    }
}
