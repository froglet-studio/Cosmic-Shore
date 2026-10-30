// Scoreboard.cs - dynamic per-player cards, always multiplayer view
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Obvious.Soap;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// End-game scoreboard. Instantiates one <see cref="PlayerScoreCard"/> per player
    /// under <see cref="playerCardContainer"/>, tints each card to its player's domain
    /// color, and shows a "{DOMAIN} VICTORY" banner.
    ///
    /// Subclasses override <see cref="FormatPlayerScore"/> and optionally
    /// <see cref="FormatSecondaryStat"/> / <see cref="SortPlayers"/> to customize display.
    /// </summary>
    public class Scoreboard : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Data")]
        [SerializeField] protected GameDataSO gameData;
        [Tooltip("Shuffle (Maelstrom meta) data - source of the per-domain placement crystals when " +
                 "IsMaelstromMode. Leave null for non-tournament scenes; the reward then stays the flat winner reward.")]
        [SerializeField] protected MaelstromDataSO tournamentData;
        [SerializeField] private ScriptableEventNoParam OnResetForReplay;

        [Header("References")]
        [Tooltip("Game controller for this mode. Wire the scene's MiniGameControllerBase subclass (SP or MP).")]
        [FormerlySerializedAs("multiplayerController")]
        [SerializeField] private MiniGameControllerBase gameController;
        [SerializeField] private GameObject endGameObject;

        [Header("UI Containers")]
        [SerializeField] Transform scoreboardPanel;
        [SerializeField] Transform statsContainer;
        [SerializeField] StatRowUI statRowPrefab;

        [Header("Banner")]
        [SerializeField] Image BannerImage;
        [SerializeField] protected TMP_Text BannerText;

        [Header("Player Score Cards")]
        [Tooltip("Container transform that will host one PlayerScoreCard per player (e.g. ScrollView/Viewport/Content).")]
        [SerializeField] protected Transform playerCardContainer;
        [Tooltip("Prefab instance to clone for each player row.")]
        [SerializeField] protected PlayerScoreCard playerCardPrefab;
        [Tooltip("Profile icon list used to resolve player avatars by AvatarId.")]
        [SerializeField] protected SO_ProfileIconList profileIconList;
        [Tooltip("AI profile list used to resolve AI avatars by name.")]
        [SerializeField] protected SO_AIProfileList aiProfileList;

        [Header("Rev 8 glass ladder (Docs/MENU_REDESIGN_B.md section 49)")]
        [Tooltip("Spawned into the card container above each team's rows when any domain fields two or more pilots. " +
                 "Empty: no headers, every row shows its own score.")]
        [SerializeField] private ScoreboardTeamHeader teamHeaderPrefab;
        [Tooltip("A winning team's line when every member shares one score (a team race time). {score} is that score.")]
        [SerializeField] private string teamWonLine = "Won - {score}";
        [Tooltip("A winning team's line when its members' scores differ (each row shows its own).")]
        [SerializeField] private string teamWonNoScoreLine = "Won";
        [Tooltip("Any other team's line when its members share one score. {score} is that score.")]
        [SerializeField] private string teamLine = "{score}";
        [Tooltip("What the local pilot earned and the wallet counting up. Empty: no strip.")]
        [SerializeField] private ScoreboardRewardStrip rewardStrip;

        [Header("Team Versus (Rev 23 D)")]
        [Tooltip("Two teams face to face, shown instead of the ladder for a two-team match (ScoreboardLadder.BuildVersus). " +
                 "Empty keeps the ladder for every match.")]
        [SerializeField] private ScoreboardVersusView versusView;
        [Tooltip("A team's row in the versus layout - half the ladder's width, the counts under the name.")]
        [SerializeField] private PlayerScoreCard versusRowPrefab;

        [Header("Crystal Rewards")]
        [Tooltip("Crystals by finishing place, best first: index 0 = 1st, index 1 = 2nd, and so on. " +
                 "Places past the end of the list earn 0, and LAST place always earns 0 regardless " +
                 "of the list. Applies to every mode, tournament included.")]
        [SerializeField] protected List<int> placementCrystalRewards = new() { 200, 50, 0 };

        [Header("Play Again")]
        [Tooltip("Play Again button - host only in multiplayer. Hidden for non-host clients; the host's Play Again forces everyone to replay.")]
        [SerializeField] private GameObject playAgainButton;

        [Tooltip("Optional label on Play Again. When set it reads REMATCH? for a client (whose press " +
                 "is a vote) and PLAY AGAIN for the host (whose press actually restarts), plus the " +
                 "live tally once anyone votes. Leave unassigned if the prefab has no label.")]
        [SerializeField] private TMPro.TMP_Text playAgainLabel;

        [Tooltip("Optional. The row of faces under Play Again showing WHO has asked for a rematch. " +
                 "Left empty, one is ensured on the Play Again button itself and adopts a " +
                 "descendant named \"PlayerAvatars\" as its container - so a prefab carrying only " +
                 "the art lights up with no wiring.")]
        [SerializeField] private RematchVoteRoster rematchVoteRoster;

        [Header("Host / Client Buttons")]
        [Tooltip("Main Menu button - host only in multiplayer (host-initiated return takes everyone). Always visible in single-player.")]
        [SerializeField] private GameObject mainMenuButton;

        [Tooltip("Leave Lobby button - non-host clients only. Disconnects from the party session and returns to Menu_Main.")]
        [SerializeField] private GameObject leaveLobbyButton;

        [Tooltip("Main-menu SOAP event - the same asset the Main Menu button raises (via PauseMenu.OnClickMainMenu) and SceneLoader listens to. When it fires, the host nav buttons hide so the transition can't be spam-clicked.")]
        [SerializeField] private ScriptableEventNoParam onClickToMainMenu;

        [Header("Maelstrom")]
        [Tooltip("Continue button - HOST ONLY, shown after EVERY tournament game (incl. the last). " +
                 "Mid-lineup it advances the party to the next game; on the last game it loads the " +
                 "Maelstrom results screen. Wire its onClick to OnContinueButtonPressed(). " +
                 "Leave unassigned in non-tournament scenes.")]
        [SerializeField] private GameObject continueButton;

        [Header("End-screen safety net")]
        [Tooltip("Seconds after the winner is known within which the end screen must have been raised (the reveal holds " +
                 "3 s). Past it the board shows itself and says the reveal was cut short.")]
        [SerializeField, Min(1f)] private float endScreenGraceSeconds = 5f;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        [Header("Style-B board (Docs/MENU_REDESIGN_B.md section 22.4)")]
        [Tooltip("Wire the four buttons in code rather than by persistent calls. The style-B board " +
                 "is a nested prefab, so a persistent call into this component would be a per-" +
                 "instance override - the shape that left the legacy board's Play Again calling a " +
                 "null target.")]
        [SerializeField] private bool wireButtonsInCode;
        [Tooltip("'Skim Race - Intensity 3' above the banner.")]
        [SerializeField] private TMP_Text modeText;

        [Header("Words (every line the board writes; empty shows nothing)")]
        [Tooltip("The banner when a domain won. {domain} is its name.")]
        [SerializeField] private string bannerWinLine = "{domain} victory";
        [Tooltip("The banner when no domain won.")]
        [SerializeField] private string bannerNoWinnerLine = "Game over";
        [Tooltip("The line above the banner. {mode} is the game, {intensity} its intensity.")]
        [SerializeField] private string modeLine = "{mode} - Intensity {intensity}";
        [Tooltip("The line above the banner for a game without an intensity. {mode} is the game.")]
        [SerializeField] private string modeNoIntensityLine = "{mode}";
        [Tooltip("The line under each pilot's name, in their domain colour. {domain} is their team, {vessel} the hull " +
                 "they flew (a human or an AI).")]
        [SerializeField] private string pilotLine = "{domain}  ·  {vessel}";
        [Tooltip("The line under a pilot's name when their hull is not known (they left before the end). {domain} is " +
                 "their team.")]
        [SerializeField] private string pilotNoVesselLine = "{domain}";
        [Tooltip("Play again, for the host (whose press restarts the match).")]
        [SerializeField] private string playAgainHostLine = "Play again";
        [Tooltip("Play again, for a guest (whose press is a rematch vote).")]
        [SerializeField] private string playAgainGuestLine = "Rematch?";
        [Tooltip("A guest's button once their vote is in.")]
        [SerializeField] private string rematchSentLine = "Rematch sent";
        [Tooltip("The host's button once anyone votes. {votes} voted of {pilots}.")]
        [SerializeField] private string hostTallyLine = "Play again ({votes}/{pilots})";
        [Tooltip("A guest's button once anyone votes, before they vote. {votes} voted of {pilots}.")]
        [SerializeField] private string guestTallyLine = "Rematch? ({votes}/{pilots})";

        #endregion

        #region Private Fields

        private ScoreboardStatsProvider statsProvider;
        private CanvasGroup _scoreboardCanvasGroup;
        private RectTransform _scoreboardRect;
        private Sequence _entranceSeq;
        private readonly List<PlayerScoreCard> _spawnedCards = new();
        private readonly List<ScoreboardTeamHeader> _spawnedHeaders = new();

        // Mode rule's team-total domain placement for the game just shown (index 0 = 1st);
        // null outside tournament mode. Computed once per ShowScoreboard, consumed by the
        // shuffle crystal badge + wallet award so they match the standings fold exactly.
        private List<Domains> _shufflePlacement;

        #endregion

        #region Unity Lifecycle

        // Every board in the scene, active or not, so an end screen that reached none can still find one.
        static readonly List<Scoreboard> s_boards = new();

        /// <summary>Counts every <see cref="ShowScoreboard"/>, so the end-game raiser can tell whether its raise
        /// reached a board at all.</summary>
        public static int ShowCount { get; private set; }

        /// <summary>
        /// The end screen was raised and no board answered - its object (or a parent) was switched off after load,
        /// which also unsubscribed it, and the player was left in the arena with no way out (2026-10-17, the owner:
        /// "Skim Race solo with AI, reveal shows then nothing"). Turns each board back on, says what was off, and shows it.
        /// </summary>
        public static void ShowUnreached()
        {
            foreach (var board in s_boards)
            {
                if (!board) continue;
                var off = new List<string>();
                for (var t = board.transform; t; t = t.parent)
                    if (!t.gameObject.activeSelf) { off.Add(t.name); t.gameObject.SetActive(true); }
                if (!board.enabled) { off.Add($"{board.name}'s Scoreboard component"); board.enabled = true; }
                CSDebug.LogWarning($"[Scoreboard] The end screen reached no scoreboard ({(off.Count > 0 ? string.Join(", ", off) + " was off" : "it was not subscribed")}) - showing it anyway.");
                board.ShowScoreboard();
            }
        }

        /// <summary>
        /// The winner is known: the end screen should follow once the reveal ends. EndGameSequencer raises it from a
        /// coroutine, and a coroutine dies silently if its object is disabled mid-reveal - the player is then left in
        /// the arena with no board and nothing in the console. This waits on a timer that does not depend on any
        /// object staying enabled and, if no board has shown by then, shows it and says so.
        /// </summary>
        void ArmEndScreenSafetyNet() => EndScreenSafetyNetAsync(ShowCount, endScreenGraceSeconds).Forget();

        static async UniTaskVoid EndScreenSafetyNetAsync(int shownBefore, float seconds)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds), ignoreTimeScale: true);
            if (ShowCount != shownBefore || s_boards.Count == 0) return;
            CSDebug.LogWarning($"[Scoreboard] The winner was decided {seconds:0} s ago and no end screen was raised - the " +
                               "reveal (EndGameSequencer) was cut short. Showing the scoreboard.");
            ShowUnreached();
        }

        void Awake()
        {
            s_boards.Add(this);
            EndScreenWitness.Watch(scoreboardPanel, "scoreboard panel");
            statsProvider = GetComponent<ScoreboardStatsProvider>();
            if (!statsProvider)
                CSDebug.LogWarning("[Scoreboard] No ScoreboardStatsProvider found.");

            ResolveGameController();
            if (wireButtonsInCode) WireButtons();
            HideScoreboard();
        }

        void WireButtons()
        {
            Wire(playAgainButton, OnPlayAgainButtonPressed);
            Wire(continueButton, OnContinueButtonPressed);
            Wire(mainMenuButton, OnMainMenuButtonPressed);
            Wire(leaveLobbyButton, OnLeaveLobbyButtonPressed);
        }

        static void Wire(GameObject host, UnityEngine.Events.UnityAction action)
        {
            if (host && host.TryGetComponent(out Button button)) button.onClick.AddListener(action);
        }

        /// <summary>
        /// Main Menu (host only - a guest sees Leave lobby): the whole party returns to the menu.
        /// The legacy board routed this through the pause menu's handler, whose host branch raises
        /// the same event.
        /// </summary>
        public void OnMainMenuButtonPressed() => onClickToMainMenu.Raise();

        /// <summary>
        /// Finds the scene's game controller when the inspector reference is empty.
        ///
        /// This exists so GameCanvas.prefab can be dropped into a NEW game-mode scene and work
        /// without hand-wiring: there is exactly one <see cref="MiniGameControllerBase"/> per
        /// gameplay scene, and it is always the one Play Again must talk to. An explicit inspector
        /// assignment still wins, so existing scenes are unaffected.
        ///
        /// Leaving this to per-scene wiring is what turned a shared prefab into N hand-maintained
        /// copies - every scene had to carry its own override just to point at its own controller.
        /// </summary>
        void ResolveGameController()
        {
            if (gameController != null) return;

            gameController = FindAnyObjectByType<MiniGameControllerBase>(FindObjectsInactive.Include);

            if (gameController == null)
            {
                // Not an error: menu / tool scenes legitimately host GameCanvas with no controller.
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[Scoreboard] No MiniGameControllerBase in this scene - Play Again is unavailable");
            }
        }

        void OnEnable()
        {
            SubscribeRematchVotes();

            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += ShowScoreboard;
            if (gameData?.OnWinnerCalculated != null)
                gameData.OnWinnerCalculated.OnRaised += ArmEndScreenSafetyNet;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised += HideScoreboard;

            // A new turn (or an in-place replay) is a new game for the award latch below.
            if (gameData?.OnMiniGameTurnStarted != null)
                gameData.OnMiniGameTurnStarted.OnRaised += ClearCrystalAwardLatch;
            if (resetEvent != null) resetEvent.OnRaised += ClearCrystalAwardLatch;

            // Hide the host nav buttons the moment the main-menu transition is
            // committed (the event only fires after PauseMenu's host guard), so the
            // host can't spam Main Menu / Play Again while the scene unloads.
            if (onClickToMainMenu != null) onClickToMainMenu.OnRaised += HideHostNavButtons;
        }

        void OnDisable()
        {
            UnsubscribeRematchVotes();

            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;
            if (gameData?.OnWinnerCalculated != null)
                gameData.OnWinnerCalculated.OnRaised -= ArmEndScreenSafetyNet;

            var resetEvent = OnResetForReplay ?? gameData?.OnResetForReplay;
            if (resetEvent != null) resetEvent.OnRaised -= HideScoreboard;

            if (gameData?.OnMiniGameTurnStarted != null)
                gameData.OnMiniGameTurnStarted.OnRaised -= ClearCrystalAwardLatch;
            if (resetEvent != null) resetEvent.OnRaised -= ClearCrystalAwardLatch;

            if (onClickToMainMenu != null) onClickToMainMenu.OnRaised -= HideHostNavButtons;
        }

        /// <summary>
        /// Tracks the controller this board is subscribed to, so a late
        /// <see cref="ResolveGameController"/> can subscribe without risking a double-subscribe
        /// (OnEnable can run before the scene's controller exists, and did: the tally label was
        /// then dead for the whole match with nothing to say so).
        /// </summary>
        MultiplayerMiniGameControllerBase _rematchSource;

        void SubscribeRematchVotes()
        {
            if (!(gameController is MultiplayerMiniGameControllerBase mp)) return;
            if (ReferenceEquals(_rematchSource, mp)) return;

            UnsubscribeRematchVotes();
            mp.OnRematchVotesChanged += HandleRematchVotesChanged;
            _rematchSource = mp;
        }

        void UnsubscribeRematchVotes()
        {
            if (_rematchSource == null) return;
            _rematchSource.OnRematchVotesChanged -= HandleRematchVotesChanged;
            _rematchSource = null;
        }

        #endregion

        #region Core

        void ShowScoreboard()
        {
            // A destroyed board still subscribed (its OnDisable never ran) is a C# object whose Unity side is gone: it
            // would pay the wallet, find no panel and count as the board that answered, so the real one's safety nets
            // stood down. Drop it and say so.
            if (!this)
            {
                if (gameData && gameData.OnShowGameEndScreen != null) gameData.OnShowGameEndScreen.OnRaised -= ShowScoreboard;
                if (gameData && gameData.OnWinnerCalculated != null) gameData.OnWinnerCalculated.OnRaised -= ArmEndScreenSafetyNet;
                CSDebug.LogWarning("[Scoreboard] A destroyed scoreboard was still subscribed to the end screen - dropped it.");
                return;
            }
            if (!gameData) { CSDebug.LogError("[Scoreboard] GameData is null!"); return; }

// Fail OPEN: the panel (with its Main Menu / Play Again buttons) must appear even
            // when a population step throws — otherwise the player has no way back to the menu.
            try
            {
                // Crystal payouts are placement-based in EVERY mode now, so resolve the team-total
                // domain order whenever a rule exists - not only in tournament. Without a rule,
                // CrystalsForPlacement falls back to the rank order already present in Results.
                _shufflePlacement = gameData.ScoringRule != null
                    ? gameData.ScoringRule.ResolvePlacementOrder(gameData)
                    : null;

                ConfigureLobbyButtons();
                RenderModeLine();
                ShowMultiplayerView();
                PopulateDynamicStats();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                CSDebug.LogError("[Scoreboard] Population threw — showing the panel anyway so the player can exit.");
            }

            if (!scoreboardPanel)
            {
                CSDebug.LogWarning($"[Scoreboard] '{name}' in scene '{gameObject.scene.name}' has no scoreboard panel - " +
                                   "it cannot show the end screen. Wire scoreboardPanel on its Scoreboard component.");
                return;
            }

            scoreboardPanel.gameObject.SetActive(true);
            EnsureBoardVisible();
            // Counted only once a panel is really up, so the raisers' safety nets still fire when a raise reached
            // nothing that could show.
            ShowCount++;
            VerifyBoardShownAsync().Forget();
            // The board owns the screen now: the vessel's ability row would otherwise draw over its bottom edge. The
            // next turn (or a replay) brings the row back through the HUD's own lifecycle.
            gameData.LocalPlayer?.Vessel?.VesselStatus?.VesselHUDController?.HideHUD();

            // Entrance animation disabled for now: PlayEntranceAnimation() slid the panel
            // by mutating its own anchoredPosition (read current pos as the rest target,
            // shoved it down by `offset`, tweened back via DOAnchorPos) and never restored
            // it on HideScoreboard(). On a stretch-anchored panel a re-entrant/interrupted
            // show captured the already-displaced position as the new rest target, so the
            // panel drifted off-base in modes that re-show the board (Joust / Crystal Capture).
            // Show at the authored position with no slide. (Re-enable once the rest pos is
            // captured at Awake and restored on hide.)
            ShowScoreboardImmediate();
        }

        /// <summary>
        /// Shows Main Menu + Play Again for the host, Leave Lobby for non-host clients.
        /// Non-host clients cannot restart the game - the host's Play Again forces everyone to replay,
        /// so exposing the button to clients would be misleading.
        /// </summary>
        void ConfigureLobbyButtons()
        {
            _rematchVoteCast = false;

            // The controller may only have appeared after OnEnable ran (ResolveGameController is
            // allowed to find it lazily), so re-attempt the tally subscription here - this is the
            // one method that runs on every scoreboard, on every peer.
            SubscribeRematchVotes();

            var nm = NetworkManager.Singleton;
            bool isClient = nm == null || !nm.IsServer;

            // Maelstrom mode: the host gets Continue on EVERY game (including the last). Continue on
            // the last game takes the party to the Maelstrom results screen, which is where Play
            // Again / Main Menu live now - so those two are never shown on the per-game scoreboard.
            //
            // A client keeps LEAVE, and that is the point. This branch used to hide all four, so a
            // client had no button at all here - and the pause menu hid its Main Menu too, and the
            // Maelstrom hub between games offers only READY. The only screen in the whole tournament
            // that ever let a client out was the FINAL summary. So a client who wanted to stop was
            // held until the host finished the entire race-to-N, or killed the application. A
            // tournament nobody can leave is not a tournament anybody should have to finish.
            if (gameData != null && gameData.IsMaelstromMode)
            {
                bool isHost = !isClient;
                if (continueButton)   continueButton.SetActive(isHost);
                if (playAgainButton)  playAgainButton.SetActive(false);
                if (mainMenuButton)   mainMenuButton.SetActive(false);
                if (leaveLobbyButton) leaveLobbyButton.SetActive(isClient);
                StopRematchVoteRoster();
                return;   // no rematch vote mid-tournament: the next game is the lineup's, not a replay
            }

            // Normal (non-tournament) game: host gets Main Menu + Play Again, clients get Leave.
            if (continueButton)   continueButton.SetActive(false);
            if (mainMenuButton)   mainMenuButton.SetActive(!isClient);
            if (leaveLobbyButton) leaveLobbyButton.SetActive(isClient);

            // Play Again is shown to EVERYONE. For the host it restarts the match; for a client it
            // is a REMATCH VOTE - the press is recorded, everyone sees the tally, and the host
            // decides. Hiding it was defensible (a client's press cannot force a replay on the
            // party) and left a client unable to say the most common thing anybody wants to say at
            // a scoreboard, with the host guessing whether anyone wanted another round.
            if (playAgainButton)  playAgainButton.SetActive(true);
            if (playAgainLabel)   playAgainLabel.text = isClient ? playAgainGuestLine : playAgainHostLine;

            StartRematchVoteRoster();
        }

        /// <summary>
        /// Show a face per rematch vote under the Play Again button. The row is ENSURED rather than
        /// required: a scoreboard that carries the art alone gets the component, and one that
        /// carries neither simply shows no row. Nothing here is per-scene wiring.
        /// </summary>
        void StartRematchVoteRoster()
        {
            EnsureRematchVoteRoster();
            if (!rematchVoteRoster) return;

            rematchVoteRoster.AdoptSources(gameData, profileIconList);
            rematchVoteRoster.Begin();
        }

        void StopRematchVoteRoster()
        {
            if (rematchVoteRoster) rematchVoteRoster.End();
        }

        void EnsureRematchVoteRoster()
        {
            if (rematchVoteRoster || !playAgainButton) return;

            // The button is the host - the row belongs to it, and hanging the component there is
            // what lets the roster find the authored "PlayerAvatars" strip beneath it by name.
            if (!playAgainButton.TryGetComponent(out rematchVoteRoster))
                rematchVoteRoster = playAgainButton.GetComponentInChildren<RematchVoteRoster>(true);
            if (!rematchVoteRoster)
                rematchVoteRoster = playAgainButton.AddComponent<RematchVoteRoster>();
        }

        void HideScoreboard()
        {
            _entranceSeq?.Kill();
            StopRematchVoteRoster();
            if (scoreboardPanel) scoreboardPanel.gameObject.SetActive(false);
            if (endGameObject) endGameObject.SetActive(false);
            if (rewardStrip) rewardStrip.Hide();
            ClearPlayerCards();
        }

        void PlayEntranceAnimation()
        {
            if (!scoreboardPanel) return;

            _entranceSeq?.Kill();

            if (!_scoreboardRect)
                _scoreboardRect = scoreboardPanel.GetComponent<RectTransform>();
            if (!_scoreboardCanvasGroup)
            {
                _scoreboardCanvasGroup = scoreboardPanel.GetComponent<CanvasGroup>();
                if (!_scoreboardCanvasGroup)
                    _scoreboardCanvasGroup = scoreboardPanel.gameObject.AddComponent<CanvasGroup>();
            }

            float duration = animSettings ? animSettings.scoreboardEntranceDuration : 0.35f;
            float offset = animSettings ? animSettings.scoreboardSlideOffset : 120f;
            var ease = animSettings ? animSettings.scoreboardEntranceEase : Ease.OutCubic;
            float bannerPunchDur = animSettings ? animSettings.bannerPunchDuration : 0.3f;
            float bannerPunchScale = animSettings ? animSettings.bannerPunchScale : 1.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            var targetPos = _scoreboardRect.anchoredPosition;
            _scoreboardRect.anchoredPosition = new Vector2(targetPos.x, targetPos.y - offset);
            _scoreboardCanvasGroup.alpha = 0f;

            _entranceSeq = DOTween.Sequence()
                .Join(_scoreboardRect.DOAnchorPos(targetPos, duration).SetEase(ease))
                .Join(_scoreboardCanvasGroup.DOFade(1f, duration));

            if (BannerText)
            {
                BannerText.transform.localScale = Vector3.one * 0.5f;
                _entranceSeq.Join(BannerText.transform
                    .DOScale(bannerPunchScale, bannerPunchDur * 0.5f)
                    .SetEase(Ease.OutBack));
                _entranceSeq.Append(BannerText.transform
                    .DOScale(1f, bannerPunchDur * 0.5f)
                    .SetEase(Ease.OutQuad));
            }

            _entranceSeq.SetUpdate(unscaled);
        }

        /// <summary>
        /// Shows the scoreboard at its authored position with no slide/fade. Replaces
        /// <see cref="PlayEntranceAnimation"/> while the entrance slide is disabled - see the
        /// note in <see cref="ShowScoreboard"/>. Forces full alpha and unit banner scale in case
        /// a previously-killed entrance tween left either mid-animation.
        /// </summary>
        void ShowScoreboardImmediate()
        {
            if (!scoreboardPanel) return;

            _entranceSeq?.Kill();

            var cg = scoreboardPanel.GetComponent<CanvasGroup>();
            if (cg) cg.alpha = 1f;

            if (BannerText) BannerText.transform.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            s_boards.Remove(this);
            _entranceSeq?.Kill();
        }

        /// <summary>
        /// A frame after showing, the board must still be on screen: something that switches the end screen (or the
        /// canvas under it) off in the same frame would otherwise leave the player with nothing, and a quiet console.
        /// </summary>
        async UniTaskVoid VerifyBoardShownAsync()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (!this || !scoreboardPanel) return;
            string hidden = WhatHidesBoard();
            if (hidden == null) return;
            CSDebug.LogWarning($"[Scoreboard] The scoreboard was hidden right after it was shown ({hidden}) - showing it again.");
            scoreboardPanel.gameObject.SetActive(true);
            EnsureBoardVisible();
            ShowScoreboardImmediate();
        }

        /// <summary>What keeps the shown board off screen, or null when nothing does.</summary>
        string WhatHidesBoard()
        {
            for (var t = scoreboardPanel; t; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return $"'{t.name}' is inactive";
                if (t.TryGetComponent<CanvasGroup>(out var group) && group.alpha <= 0.01f) return $"'{t.name}' is at alpha 0";
                if (t.TryGetComponent<Canvas>(out var canvas) && !canvas.enabled) return $"the canvas '{t.name}' is disabled";
            }
            return null;
        }

        /// <summary>
        /// Anything above the board that would keep it off screen once shown - an inactive parent, a parent CanvasGroup
        /// at alpha 0, a disabled Canvas - is put right and named, so a hidden end screen is a warning, never silence.
        /// </summary>
        void EnsureBoardVisible()
        {
            for (var t = scoreboardPanel; t; t = t.parent)
            {
                if (!t.gameObject.activeSelf)
                {
                    CSDebug.LogWarning($"[Scoreboard] '{t.name}' above the scoreboard was inactive - turning it on.");
                    t.gameObject.SetActive(true);
                }
                if (t.TryGetComponent<CanvasGroup>(out var group) && group.alpha <= 0.01f)
                {
                    CSDebug.LogWarning($"[Scoreboard] '{t.name}' above the scoreboard was at alpha 0 - showing it.");
                    group.alpha = 1f;
                }
                if (t.TryGetComponent<Canvas>(out var canvas) && !canvas.enabled)
                {
                    CSDebug.LogWarning($"[Scoreboard] The canvas '{t.name}' holding the scoreboard was disabled - enabling it.");
                    canvas.enabled = true;
                }
            }
            if (scoreboardPanel is RectTransform rect && (rect.rect.width < 1f || rect.rect.height < 1f))
                CSDebug.LogWarning($"[Scoreboard] The scoreboard is {rect.rect.width:0}x{rect.rect.height:0} - it cannot be seen.");
            var lossy = scoreboardPanel.lossyScale;
            if (Mathf.Abs(lossy.x) < 1e-4f || Mathf.Abs(lossy.y) < 1e-4f)
                CSDebug.LogWarning($"[Scoreboard] The scoreboard's world scale is {lossy} - a parent is scaled to 0, so it cannot be seen.");

            if (!CSDebug.IsVerbose(CSLogChannel.ArcadeMatch)) return;
            var host = scoreboardPanel.GetComponentInParent<Canvas>();
            // Overlay canvases drawn over the board (a higher sort order) - anything opaque among them hides it.
            var above = new List<string>();
            if (host)
                foreach (var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (other.isRootCanvas && other != host.rootCanvas && other.isActiveAndEnabled &&
                        other.renderMode == RenderMode.ScreenSpaceOverlay && other.sortingOrder >= host.rootCanvas.sortingOrder)
                        above.Add($"{other.name}({other.sortingOrder})");
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Scoreboard] Overlay canvases over the board: {(above.Count > 0 ? string.Join(", ", above) : "none")}");
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[Scoreboard] Shown: activeInHierarchy={scoreboardPanel.gameObject.activeInHierarchy}, " +
                $"rect={(scoreboardPanel is RectTransform r ? r.rect.size.ToString() : "?")}, lossyScale={lossy}, " +
                $"canvas={(host ? $"{host.rootCanvas.name} mode={host.rootCanvas.renderMode} order={host.rootCanvas.sortingOrder} enabled={host.rootCanvas.enabled}" : "none")}");
        }

        #endregion

        #region Multiplayer View (the only view)

        /// <summary>
        /// Builds the per-player card layout. Solo play renders as a single card.
        /// </summary>
        protected virtual void ShowMultiplayerView()
        {
            // Prefer the single source of truth: the mode's ranked + formatted results
            // (GameDataSO.Results, produced once by the ScoringRule). Modes that don't
            // produce results (freestyle, DuelForCell) fall back to the legacy path.
            if (gameData.Results is { Count: > 0 })
            {
                var winnerDomain = gameData.WinnerDomain != Domains.Blue
                    ? gameData.WinnerDomain
                    : gameData.Results[0].Domain;
                SetBannerForDomain(winnerDomain);
                PopulateFromResults(gameData.Results);
                return;
            }

            var orderedStats = SortPlayers(gameData.RoundStatsList);
            SetBannerForDomain(DetermineWinnerDomain(orderedStats));
            PopulatePlayerCards(orderedStats);
        }

        /// <summary>
        /// Default ordering uses DomainStatsList if present (winner first), otherwise
        /// returns the list as-is. Subclasses override to apply mode-specific sorting
        /// (golf rules, crystals, etc).
        /// </summary>
        protected virtual List<IRoundStats> SortPlayers(List<IRoundStats> stats)
        {
            if (stats == null) return new List<IRoundStats>();
            return stats.ToList();
        }

        /// <summary>
        /// Winning domain for the banner. Prefers the server-authoritative
        /// <see cref="GameDataSO.WinnerDomain"/> - the SAME value the end-game
        /// cinematic uses - so the banner and the cinematic can't disagree on a tie
        /// and there is one source of truth for "who won". Modes that don't set it
        /// (single-player / co-op / DuelForCell) leave it <see cref="Domains.Blue"/>
        /// - it is reset on every scene load (SceneLoader → GameDataSO.ResetRuntimeData)
        /// and on replay (ResetRuntimeDataForReplay) - and fall back to the per-domain
        /// sum order exactly as before. Subclasses may override.
        /// (Interim step: R10 will replace WinnerDomain + DomainStatsList with one
        /// synced ranked-results list - see Docs/ScoringSystem/REFACTOR.md.)
        /// </summary>
        protected virtual Domains DetermineWinnerDomain(List<IRoundStats> orderedStats)
        {
            if (gameData.WinnerDomain != Domains.Blue)
                return gameData.WinnerDomain;
            if (gameData.DomainStatsList is { Count: > 0 })
                return gameData.DomainStatsList[0].Domain;
            if (orderedStats is { Count: > 0 })
                return orderedStats[0].Domain;
            return Domains.Blue;
        }

        /// <summary>
        /// Subclasses override to format each player's primary score (time, crystals, etc).
        /// </summary>
        protected virtual string FormatPlayerScore(IRoundStats stats)
        {
            return ((int)stats.Score).ToString();
        }

        /// <summary>
        /// Optional secondary line (e.g. "Jousts: 3"). Return null or empty to hide.
        /// </summary>
        protected virtual string FormatSecondaryStat(IRoundStats stats) => null;

        protected virtual void SetBannerForDomain(Domains domain)
        {
            if (BannerImage) BannerImage.color = GetDomainColor(domain);
            if (BannerText) BannerText.text = BannerLine(domain, bannerWinLine, bannerNoWinnerLine);
            if (BannerText && domain != Domains.Blue && (int)domain != 0) BannerText.color = GetDomainColor(domain);
        }

        /// <summary>
        /// "Jade victory" - or "Game over" when no domain won. Blue is the "no domain" sentinel
        /// (GameDataSO.WinnerDomain's reset value), so it never names a winner: "BLUE VICTORY" used
        /// to be reachable on a board where nobody flies Blue.
        /// </summary>
        public static string BannerLine(Domains domain, string winLine, string noWinnerLine) => domain switch
        {
            Domains.Jade or Domains.Ruby or Domains.Gold => TemplateText.Fill(winLine, "domain", domain.ToString()),
            _ => TemplateText.Fill(noWinnerLine),
        };

        void RenderModeLine()
        {
            if (!modeText || gameData == null) return;
            string mode = PauseMenu.SpacedName(gameData.GameMode.ToString());
            int intensity = gameData.SelectedIntensity != null ? gameData.SelectedIntensity.Value : 0;
            modeText.text = intensity > 0
                ? TemplateText.Fill(modeLine, "mode", mode, "intensity", intensity.ToString())
                : TemplateText.Fill(modeNoIntensityLine, "mode", mode);
        }

        void DecorateCard(PlayerScoreCard card, string pilotName, Domains domain)
        {
            card.SetDomain(PilotLine(domain, FlownVesselName(pilotName)), GetDomainColor(domain));
            card.SetLocal(gameData != null && gameData.LocalPlayer != null && gameData.LocalPlayer.Name == pilotName);
        }

        /// <summary>
        /// The line under a pilot's name: their team and the hull they flew ("Ruby · Squirrel"), so a board of
        /// several hulls says who flew what. Blue is the "no domain" sentinel, so a pilot without a team shows the
        /// hull alone.
        /// </summary>
        string PilotLine(Domains domain, string vessel)
        {
            bool noTeam = domain == Domains.Blue || (int)domain == 0;
            if (string.IsNullOrEmpty(vessel))
                return noTeam ? string.Empty : TemplateText.Fill(pilotNoVesselLine, "domain", domain.ToString());
            return noTeam ? vessel : TemplateText.Fill(pilotLine, "domain", domain.ToString(), "vessel", vessel);
        }

        /// <summary>
        /// The hull <paramref name="pilotName"/> is flying now, at the end of the match - humans and AI alike,
        /// matched by name the way the avatar is (<see cref="ResolveAvatarSpriteByName"/>). Null when the pilot is
        /// gone (left before the end) or never got a hull.
        /// </summary>
        string FlownVesselName(string pilotName)
        {
            var player = gameData?.Players?.FirstOrDefault(p => p.IsAlive() && p.Name == pilotName);
            if (player == null || !player.Vessel.IsAlive() || !player.Vessel.VesselStatus.IsAlive()) return null;

            var hull = player.Vessel.VesselStatus.VesselType;
            return hull is VesselClassType.Any or VesselClassType.Random ? null : PauseMenu.SpacedName(hull.ToString());
        }

        void PopulatePlayerCards(List<IRoundStats> orderedStats)
        {
            ClearPlayerCards();
            if (versusView) versusView.SetShown(false);

            if (orderedStats == null || orderedStats.Count == 0) return;
            if (!playerCardContainer || !playerCardPrefab)
            {
                CSDebug.LogWarning($"[Scoreboard] PopulatePlayerCards skipped - " +
                    $"container={(playerCardContainer != null ? "OK" : "NULL")}, " +
                    $"prefab={(playerCardPrefab != null ? "OK" : "NULL")}");
                return;
            }

            string winnerName = orderedStats[0].Name;

            for (int i = 0; i < orderedStats.Count; i++)
            {
                var stats = orderedStats[i];
                var card = Instantiate(playerCardPrefab, playerCardContainer);

                Color domainColor = GetDomainColor(stats.Domain);
                card.Setup(stats.Name, FormatPlayerScore(stats), domainColor, i);

                card.SetAvatar(ResolveAvatarSprite(stats));
                DecorateCard(card, stats.Name, stats.Domain);

                string secondary = FormatSecondaryStat(stats);
                if (!string.IsNullOrEmpty(secondary))
                    card.ShowSecondaryStat(secondary);

                // Reward badge: shuffle = this card's DOMAIN per-game {2,1,0}; else winner-only flat.
                int reward = CardCrystalReward(stats.Domain, stats.Name, winnerName);
                if (reward > 0)
                    card.ShowCrystalReward(reward);

                _spawnedCards.Add(card);
            }

            // The scoreboard is the single crystal-award path (local player only).
            AwardCrystalsToLocalPlayer(winnerName);
        }

        /// <summary>
        /// Renders cards from the single source of truth - the mode's ranked, formatted
        /// <see cref="GameDataSO.Results"/>. Order, primary text and secondary line all come
        /// from the ScoringRule, so the scoreboard and the end-game cinematic can't disagree.
        /// </summary>
        void PopulateFromResults(List<ScoreResult> results)
        {
            ClearPlayerCards();

            if (results == null || results.Count == 0) return;
            if (!playerCardContainer || !playerCardPrefab)
            {
                CSDebug.LogWarning($"[Scoreboard] PopulateFromResults skipped - " +
                    $"container={(playerCardContainer != null ? "OK" : "NULL")}, " +
                    $"prefab={(playerCardPrefab != null ? "OK" : "NULL")}");
                return;
            }

            string winnerName = results[0].Name;

            // Rev 23 D: a two-team match is drawn as the two teams facing each other; anything else stays the ladder.
            var versus = versusView && versusRowPrefab && versusView.IsAvailable
                ? ScoreboardLadder.BuildVersus(results, gameData.WinnerDomain) : null;
            if (versusView) versusView.SetShown(versus != null);
            if (versus != null)
            {
                PopulateVersus(results, versus, winnerName);
                AwardCrystalsToLocalPlayer(winnerName);
                return;
            }

            var ladder = ScoreboardLadder.Build(results, gameData.WinnerDomain);
            int stagger = 0;

            foreach (var entry in ladder)
            {
                if (entry.IsHeader)
                {
                    if (!teamHeaderPrefab) continue;
                    var header = Instantiate(teamHeaderPrefab, playerCardContainer);
                    header.Bind(entry.Domain.ToString(), GetDomainColor(entry.Domain), TeamLine(entry));
                    _spawnedHeaders.Add(header);
                    continue;
                }

                // A header carries the team's shared score once; with no header prefab every row keeps its own.
                SpawnRow(playerCardPrefab, playerCardContainer, results[entry.ResultIndex], entry,
                         entry.ShowsScore || !teamHeaderPrefab, stagger++, winnerName);
            }

            AwardCrystalsToLocalPlayer(winnerName);
        }

        void PopulateVersus(List<ScoreResult> results, ScoreboardLadder.Side[] sides, string winnerName)
        {
            int stagger = 0;
            for (int s = 0; s < sides.Length; s++)
            {
                var side = sides[s];
                versusView.BindSide(s, side.Domain.ToString(), GetDomainColor(side.Domain), side.Won, side.Score);
                foreach (var entry in side.Rows)
                    SpawnRow(versusRowPrefab, versusView.Rows(s), results[entry.ResultIndex], entry,
                             entry.ShowsScore, stagger++, winnerName);
            }
            versusView.SetShare(sides[0].Share, sides[0].Domain.ToString(), sides[1].Domain.ToString());
        }

        void SpawnRow(PlayerScoreCard prefab, Transform parent, ScoreResult r, ScoreboardLadder.Entry entry, bool showScore,
                      int stagger, string winnerName)
        {
            var card = Instantiate(prefab, parent);

            card.Setup(r.Name, r.ScoreText, GetDomainColor(r.Domain), stagger);
            card.SetRank(r.Rank > 0 ? r.Rank : entry.ResultIndex + 1);
            card.SetAvatar(ResolveAvatarSpriteByName(r.Name));
            card.SetOutcome(entry.Won);
            DecorateCard(card, r.Name, r.Domain);
            card.ShowScore(showScore);
            card.SetShare(entry.Share);

            if (!string.IsNullOrEmpty(r.Secondary))
                card.ShowSecondaryStat(r.Secondary);

            int reward = CardCrystalReward(r.Domain, r.Name, winnerName);
            if (reward > 0)
                card.ShowCrystalReward(reward);

            _spawnedCards.Add(card);
        }

        string TeamLine(ScoreboardLadder.Entry header)
        {
            string line = header.SharedScore != null
                ? (header.Won ? teamWonLine : teamLine)
                : (header.Won ? teamWonNoScoreLine : string.Empty);
            return string.IsNullOrEmpty(line) ? string.Empty : line.Replace("{score}", header.SharedScore ?? string.Empty);
        }

        void ClearPlayerCards()
        {
            foreach (var card in _spawnedCards)
            {
                if (card) Destroy(card.gameObject);
            }
            _spawnedCards.Clear();
            foreach (var header in _spawnedHeaders)
                if (header) Destroy(header.gameObject);
            _spawnedHeaders.Clear();

            // Safety: also destroy any leftover cards in the container (e.g. editor-placed templates)
            if (playerCardContainer)
            {
                foreach (Transform child in playerCardContainer)
                    Destroy(child.gameObject);
            }
        }

        // Static: the wallet is one per player, so the latch is one per process. An instance field let
        // a second live Scoreboard (a duplicate in the scene, or a stale one surviving Play/Stop/Play)
        // pay the same game again 2-27 ms later (crystals_earned near-duplicates, 2026-10-16..19).
        static bool _crystalsAwardedThisGame;

        void ClearCrystalAwardLatch() => _crystalsAwardedThisGame = false;

        /// <summary>
        /// The single crystal-award path (the Scoreboard is the only writer of the wallet). The
        /// local player earns their DOMAIN's placement crystals from
        /// <see cref="placementCrystalRewards"/> - the same table in every mode, tournament
        /// included - credited on every peer for its own local human, once per game.
        /// </summary>
        void AwardCrystalsToLocalPlayer(string winnerName)
        {
            // Once per game, whatever raises the end screen. ShowScoreboard has no latch of its
            // own and runs once per OnShowGameEndScreen raise; GameCanvas carried a second
            // EndGameSequencer, so every match raised it twice and paid the wallet twice.
            if (_crystalsAwardedThisGame) return;

            var localName = gameData.LocalPlayer?.Name;
            if (string.IsNullOrEmpty(localName)) return;

            var service = PlayerDataService.Instance;
            if (service == null) return;

            var localDomain = gameData.LocalRoundStats != null ? gameData.LocalRoundStats.Domain : Domains.Blue;
            int amount = CrystalsForPlacement(localDomain);
            string source = gameData.IsMaelstromMode ? "tournament_placement" : "game_placement";

            _crystalsAwardedThisGame = true;
            if (rewardStrip) rewardStrip.Hide();
            if (amount <= 0) return;   // e.g. a last-place domain earns nothing this game

            // Wallet write is an external-service boundary: it runs mid-way through building the
            // end-game screen (before the panel activates), so a service hiccup must degrade to a
            // lost reward log line - never to a missing scoreboard.
            try
            {
                int newBalance = service.AddCrystals(amount, source);
                if (rewardStrip) rewardStrip.Show(amount, newBalance - amount, newBalance);
                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[Scoreboard] Awarded {amount} crystals to '{localName}' ({source}) - new balance={newBalance}");
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[Scoreboard] Crystal award failed for '{localName}' ({source}, {amount}): {e}");
            }
        }

        /// <summary>
        /// The "+N crystals" badge amount for one card. Shuffle/tournament: the card's DOMAIN per-game
        /// placement ({2,1,0}); otherwise the winner-only flat reward (0 for non-winners). 0 = no badge.
        /// </summary>
        int CardCrystalReward(Domains domain, string name, string winnerName) =>
            CrystalsForPlacement(domain);

        /// <summary>
        /// Crystals earned by a domain, from its finishing place. One table for every mode:
        /// 1st and 2nd pay out, last place always pays nothing.
        ///
        /// Placement comes from the mode rule's team-total order when one exists (the same
        /// aggregation that decides WinnerDomain), otherwise from the rank order already baked into
        /// <c>gameData.Results</c>. A domain that never appears earns nothing.
        /// </summary>
        int CrystalsForPlacement(Domains domain)
        {
            if (placementCrystalRewards == null || placementCrystalRewards.Count == 0) return 0;

            var order = _shufflePlacement;
            if (order == null || order.Count == 0)
            {
                // Rank-derived fallback: Results are already sorted best-first by the mode.
                if (gameData.Results == null || gameData.Results.Count == 0) return 0;
                order = new List<Domains>();
                foreach (var r in gameData.Results)
                    if (!order.Contains(r.Domain)) order.Add(r.Domain);
            }

            int index = order.IndexOf(domain);
            if (index < 0) return 0;

            // Last place earns nothing even if the table would pay it - with two domains that makes
            // the runner-up a loser, not a silver medallist, which is the intended read.
            if (order.Count > 1 && index == order.Count - 1) return 0;

            return index < placementCrystalRewards.Count
                ? Mathf.Max(0, placementCrystalRewards[index])
                : 0;
        }

        // Single source of truth for domain color: the same ColorSet the vessels and
        // prisms read from (GameDataSO.ThemeManagerData.ColorSet -> TrailHighlightColor).
        // No per-Scoreboard palette or hardcoded fallbacks - see Docs/ScoringSystem/REFACTOR.md R5.
        Color GetDomainColor(Domains domain)
        {
            return gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.gray;
        }

        Sprite ResolveAvatarSprite(IRoundStats stats) => ResolveAvatarSpriteByName(stats.Name);

        Sprite ResolveAvatarSpriteByName(string playerName)
        {
            // AI players - look up by name in AI profile list (struct, not nullable)
            if (aiProfileList != null && aiProfileList.AiProfiles != null)
            {
                foreach (var p in aiProfileList.AiProfiles)
                {
                    if (p.Name == playerName)
                        return p.AvatarSprite;
                }
            }

            // Human players - look up by AvatarId via gameData.Players
            if (profileIconList != null && profileIconList.ProfileIcons != null && gameData?.Players != null)
            {
                var player = gameData.Players.FirstOrDefault(pl => pl.Name == playerName);
                if (player != null)
                {
                    foreach (var icon in profileIconList.ProfileIcons)
                    {
                        if (icon.Id == player.AvatarId) return icon.IconSprite;
                    }
                    if (profileIconList.ProfileIcons.Count > 0)
                        return profileIconList.ProfileIcons[0].IconSprite;
                }
            }

            return null;
        }

        #endregion

        #region Dynamic Stats

        void PopulateDynamicStats()
        {
            if (statsContainer)
                foreach (Transform child in statsContainer)
                    Destroy(child.gameObject);

            if (!statsProvider || !statsContainer || !statRowPrefab)
            {
                return;
            }

            var stats = statsProvider.GetStats();
            if (stats == null) return;

            foreach (var stat in stats)
            {
                var row = Instantiate(statRowPrefab, statsContainer);
                row.Initialize(stat.Label, stat.Value, stat.Icon);
            }
        }

        #endregion

        #region Play Again

        /// <summary>
        /// Play Again is host-only in multiplayer - non-host clients don't see the button
        /// (see <see cref="ConfigureLobbyButtons"/>). A host click forces everyone to replay
        /// through the controller's server-authoritative reset pipeline.
        /// </summary>
        public void OnPlayAgainButtonPressed()
        {
            if (UGSStatsManager.Instance != null)
                UGSStatsManager.Instance.TrackPlayAgain();

            // A CLIENT's press is a rematch VOTE, not a restart - only the host can force the party
            // into another round. The button used to be hidden from clients entirely and this guard
            // was its backstop; now the guard is the fork.
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer)
            {
                CastRematchVote();
                return;
            }

            // In tournament mode Play Again is not shown on the per-game scoreboard (it lives on
            // the Maelstrom results screen via MaelstromSceneView). So this path is only ever
            // reached by non-tournament games.

            if (gameController == null)
            {
                CSDebug.LogError("[Scoreboard] gameController not assigned - wire the scene's MiniGameControllerBase in the inspector.");
                return;
            }

            HideHostNavButtons();
            gameController.RequestReplay();
        }

        /// <summary>
        /// Host-only "Continue" handler (tournament mode). Shown after every tournament game incl.
        /// the last (see <see cref="ConfigureLobbyButtons"/>): mid-lineup it advances the party to
        /// the next game; on the last game <see cref="MaelstromController.AdvanceToNextGame"/> loads
        /// the Maelstrom results screen instead. Wire the Continue button's onClick here.
        /// </summary>
        public void OnContinueButtonPressed()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsServer)
            {
                CSDebug.LogWarning("[Scoreboard] Continue ignored - only the host advances the tournament.");
                return;
            }

            HideHostNavButtons();
            if (MaelstromController.Instance != null)
                MaelstromController.Instance.AdvanceToNextGame();
            else
                CSDebug.LogError("[Scoreboard] MaelstromController.Instance is null - cannot advance the tournament.");
        }

        /// <summary>
        /// Hides the host-only navigation buttons (Play Again + Main Menu + Continue) once a
        /// navigation action is committed - a button clicked or the main-menu SOAP event raised -
        /// so the host can't spam-click during the scene transition. The next game end
        /// re-activates the right ones via <see cref="ConfigureLobbyButtons"/>.
        /// </summary>
        void HideHostNavButtons()
        {
            // The vote row lives INSIDE the Play Again button, so it goes down with it - left
            // running it would keep polling and rebuilding chips under a hidden parent.
            StopRematchVoteRoster();
            if (playAgainButton) playAgainButton.SetActive(false);
            if (mainMenuButton)  mainMenuButton.SetActive(false);
            if (continueButton)  continueButton.SetActive(false);
        }

        /// <summary>
        /// Client-side "Leave Lobby" button handler. Disconnects from the host's party
        /// session and returns to Menu_Main. Host/single-player users see the regular
        /// Main Menu button instead (which is wired to the SOAP main-menu event).
        /// </summary>
        /// <summary>
        /// Sends this client's rematch vote and locks the button so it reads as cast. The tally
        /// comes back to every peer through <c>OnRematchVotesChanged</c>, so the host sees it too.
        /// </summary>
        void CastRematchVote()
        {
            ResolveGameController();
            var controller = gameController as MultiplayerMiniGameControllerBase;
            if (controller == null || !controller.IsSpawned)
            {
                CSDebug.LogWarning("[Scoreboard] Rematch vote ignored - no spawned multiplayer controller.");
                return;
            }

            string playerName = gameData?.LocalPlayer?.Name ?? string.Empty;
            var domain = gameData?.LocalPlayer?.Domain ?? Domains.Blue;
            controller.RequestRematch_ServerRpc(playerName, (int)domain);

            _rematchVoteCast = true;
            // ASCII only, and not for prettiness: the label's font (ChakraPetch-Regular SDF) carries
            // 97 glyphs - printable ASCII, NBSP and an ellipsis - with NO fallback asset on the font,
            // none in TMP Settings, and m_missingGlyphCharacter 0. The "REMATCH ✓" this used to say
            // would have drawn a blank where the tick is. It never showed because the label was
            // unwired; wiring it is what would have surfaced it.
            if (playAgainLabel) playAgainLabel.text = rematchSentLine;
        }

        bool _rematchVoteCast;

        /// <summary>
        /// Live tally on the button. The HOST is the audience that matters - it is the only peer
        /// whose press does anything - so it sees "PLAY AGAIN (2/3)" and can decide. A client that
        /// has already voted keeps its confirmation rather than being overwritten by the count.
        /// </summary>
        void HandleRematchVotesChanged(int votes, int humans)
        {
            if (!playAgainLabel) return;
            if (votes <= 0) return;

            var nm = NetworkManager.Singleton;
            bool isHost = nm == null || nm.IsServer;

            string v = votes.ToString(), n = Mathf.Max(votes, humans).ToString();
            playAgainLabel.text = isHost
                ? TemplateText.Fill(hostTallyLine, "votes", v, "pilots", n)
                : (_rematchVoteCast ? rematchSentLine : TemplateText.Fill(guestTallyLine, "votes", v, "pilots", n));
        }

        public void OnLeaveLobbyButtonPressed()
        {
            if (leaveLobbyButton) leaveLobbyButton.SetActive(false);

            if (PartyInviteController.Instance == null)
            {
                CSDebug.LogError("[Scoreboard] PartyInviteController not available - cannot leave lobby.");
                return;
            }

            PartyInviteController.Instance.LeavePartyAndReturnToMenuAsync().Forget();
        }

        #endregion
    }
}
