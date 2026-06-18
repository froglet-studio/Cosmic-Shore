using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Data-driven view for the Maelstrom scene, which serves THREE roles (the persistent
    /// <see cref="TournamentController"/> decides which via its phase):
    ///
    ///   • <b>Active layout</b> (intro lobby OR between-round hub, phase Lobby) — round counter, the
    ///     per-player roster (domain-tinted cards), the per-domain team scores (ordered by the overall
    ///     leader), a scroll of completed-round cards, and a networked Ready button whose countdown
    ///     (30s, or 5s once everyone's ready) is rendered in the button label.
    ///   • <b>Summary layout</b> (phase Summary) — the winning-domain banner + the full round history;
    ///     a Next button reveals the rank panel.
    ///   • <b>Rank panel</b> (after Next) — the lightweight final domain ranking + host-only Play Again /
    ///     Main Menu.
    ///
    /// Runs on every peer; only the host drives transitions (Play Again / Main Menu / the ready-up
    /// deadline) — clients follow the Single load. The roster + history are read from the persistent
    /// <see cref="TournamentDataSO"/> (the scene is UI-only, so <c>gameData.Players/Results</c> are
    /// already cleared here).
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Tooltip("Networked ready-up + countdown for the lobby/hub. Optional: if unwired, the Ready " +
                 "button degrades to a host-only immediate Start (no 30s/5s countdown).")]
        [SerializeField] TournamentLobbyNetwork lobbyNetwork;

        [Header("Shared")]
        [SerializeField] TMP_Text titleText;

        [Header("Active layout (lobby + hub)")]
        [SerializeField] GameObject activeRoot;
        [Tooltip("\"ROUND N\" — the round about to be played.")]
        [SerializeField] TMP_Text roundCounterText;
        [Tooltip("Free top slot for extra info (race target, etc). Defaults to the win-target hint.")]
        [SerializeField] TMP_Text extraInfoText;

        [Header("Active — player roster")]
        [SerializeField] PlayerScoreCard playerCardPrefab;
        [SerializeField] Transform playerCardContainer;

        [Header("Active — team scores")]
        [SerializeField] TournamentDomainScoreView teamScorePanelPrefab;
        [SerializeField] Transform teamScoreContainer;

        [Header("Active — round history scroll")]
        [SerializeField] TournamentRoundCard roundCardPrefab;
        [SerializeField] Transform historyContent;
        [Tooltip("Optional — used to auto-scroll to the latest round on entry.")]
        [SerializeField] ScrollRect historyScrollRect;

        [Header("Active — ready button")]
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyButtonLabel;
        [Tooltip("Optional \"x / y ready\" tally text.")]
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
        [Tooltip("Host-only. Restarts the whole tournament (fresh lobby).")]
        [SerializeField] Button playAgainButton;
        [Tooltip("Host-only. Returns the party to the lava-lamp menu.")]
        [SerializeField] Button mainMenuButton;
        [Tooltip("The shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses).")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        [Header("Avatars")]
        [SerializeField] SO_ProfileIconList profileIconList;
        [SerializeField] SO_AIProfileList aiProfileList;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        bool _active;               // active (lobby/hub) layout is showing — drives the Update countdown
        bool _summaryActionTaken;   // anti-spam guard for the host-only Play Again / Main Menu

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
                string state = lobbyNetwork.LocalReady ? "READY ✓" : "TAP TO READY";
                readyButtonLabel.text = $"{state}   {secs}";
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

            int gamesPlayed = tournamentData != null ? tournamentData.GamesPlayed : 0;
            if (roundCounterText) roundCounterText.text = $"ROUND {gamesPlayed + 1}";
            if (extraInfoText && tournamentData != null)
                extraInfoText.text = $"First team to {tournamentData.WinTarget}";

            PopulateRoster();
            PopulateTeamScores();
            PopulateHistory(historyContent, highlightLatest: true);
            AutoScrollToLatest();

            ConfigureReadyButton();
        }

        void PopulateRoster()
        {
            if (!playerCardPrefab || !playerCardContainer) return;
            ClearChildren(playerCardContainer);

            var roster = OrderRoster(BuildActiveRoster());
            for (int i = 0; i < roster.Count; i++)
            {
                var s = roster[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);
                card.Setup(s.Name, PointsFor(s.Domain).ToString(), DomainColor(s.Domain), i);
                card.SetAvatar(ResolveAvatar(s));
            }
        }

        void PopulateTeamScores()
        {
            if (!teamScorePanelPrefab || !teamScoreContainer || tournamentData == null) return;
            ClearChildren(teamScoreContainer);

            var local = GetLocalDomain();
            var sorted = tournamentData.BuildSortedStandings();
            for (int i = 0; i < sorted.Count; i++)
            {
                var standing = sorted[i];
                var panel = Instantiate(teamScorePanelPrefab, teamScoreContainer);
                panel.Setup(standing.Domain, DomainColor(standing.Domain), standing.TotalPoints, i + 1,
                            standing.Domain == local);
            }
        }

        void PopulateHistory(Transform content, bool highlightLatest)
        {
            if (!roundCardPrefab || !content || tournamentData == null) return;
            ClearChildren(content);

            var history = tournamentData.History;
            for (int i = 0; i < history.Count; i++)
            {
                var card = Instantiate(roundCardPrefab, content);
                bool isCurrent = highlightLatest && i == history.Count - 1;
                card.Setup(history[i], DomainColor, isCurrent);
            }
        }

        void AutoScrollToLatest()
        {
            // Snap the scroll to the bottom (latest round) so the most recent card is in view on entry.
            if (!historyScrollRect) return;
            Canvas.ForceUpdateCanvases();
            historyScrollRect.verticalNormalizedPosition = 0f;
        }

        void ConfigureReadyButton()
        {
            if (!readyButton) return;

            // Networked path: everyone gets a Ready button (the countdown + all-ready snap are host-
            // authoritative in TournamentLobbyNetwork). Degraded path (no component wired): host-only
            // immediate Start.
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

            // Degraded path: no networked countdown wired — host starts the round immediately.
            if (IsHost && TournamentController.Instance != null)
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

            PopulateHistory(summaryHistoryContent, highlightLatest: false);

            if (nextButton) nextButton.gameObject.SetActive(true);
        }

        public void OnNextButtonPressed()
        {
            // Local navigation — reveal the rank panel on this peer (no network state change).
            SetRoot(summaryRoot, false);
            ShowRank();
        }

        // ── Rank panel ──────────────────────────────────────────────────────────────

        void ShowRank()
        {
            _active = false;
            SetRoot(rankRoot, true);

            PopulateRankRows();

            // Play Again / Main Menu are host-only; clients just view the ranking.
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
            TournamentController.Instance.RestartTournament();   // → fresh lobby (Single load)
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
        /// The roster for the active layout. Between rounds it's the latest completed round's snapshot
        /// (full roster incl. AI + avatars). On the round-0 lobby (no history yet, AI not spawned) it's
        /// the connected human players — every peer sees all Player NetworkObjects via the spawn manager.
        /// </summary>
        List<TournamentPlayerSnapshot> BuildActiveRoster()
        {
            if (tournamentData != null && tournamentData.History.Count > 0)
                return tournamentData.History[tournamentData.History.Count - 1].Players;

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

        // Orders the roster by the overall tournament leader (domain standing), then by last-round rank
        // within a domain — so teammates group under their team and the leading team shows first.
        List<TournamentPlayerSnapshot> OrderRoster(List<TournamentPlayerSnapshot> roster)
        {
            if (roster == null) return new List<TournamentPlayerSnapshot>();
            var order = tournamentData != null
                ? tournamentData.BuildSortedStandings().Select(s => s.Domain).ToList()
                : new List<Domains>();

            return roster
                .OrderBy(p => { int idx = order.IndexOf(p.Domain); return idx < 0 ? int.MaxValue : idx; })
                .ThenBy(p => p.Rank <= 0 ? int.MaxValue : p.Rank)
                .ToList();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────

        string ModeName() => tournamentData != null ? tournamentData.ModeName : "Maelstrom";

        Domains WinningDomain()
        {
            if (tournamentData == null) return Domains.Blue;
            var sorted = tournamentData.BuildSortedStandings();
            return sorted.Count > 0 ? sorted[0].Domain : Domains.Blue;
        }

        int PointsFor(Domains domain)
        {
            if (tournamentData == null) return 0;
            var s = tournamentData.Standings.Find(x => x.Domain == domain);
            return s != null ? s.TotalPoints : 0;
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
        /// UI-only scene (cleared before the load, no vessel respawns it), so fall back to the persistent
        /// local Player NetworkObject. Blue (the no-team sentinel) marks nothing.
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
