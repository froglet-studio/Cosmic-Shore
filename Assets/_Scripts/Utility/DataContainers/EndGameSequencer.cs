using System.Collections;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// End-game reveal coordinator. On <c>GameDataSO.OnWinnerCalculated</c> it:
    ///   1. hands the local vessel a random AI cinematic flourish so it keeps flying
    ///      (loop / drift / spiral) during the reveal - it is NOT halted,
    ///   2. plays the GameEnd SFX,
    ///   3. reveals the <c>ScoreRevealToast</c> with a randomized win/lose line
    ///      (<see cref="EndGameMessageSetSO"/>) on the GameOverCounter text,
    ///   4. holds for <see cref="revealDuration"/> seconds,
    ///   5. raises <c>OnShowGameEndScreen</c> - the signal the <c>Scoreboard</c> (and
    ///      <c>LifeForm</c> ecology cleanup) already listen for.
    ///
    /// Lives on the <c>EndGameStatsPanel</c> object inside <c>EndGameStatsPanel.prefab</c>,
    /// which is nested by every game canvas, so the reveal works in every game scene.
    /// It also re-homes the two end-of-game progression notifications (quest complete /
    /// intensity unlocked); the unlock + persistence are owned by
    /// <see cref="GameModeProgressionService"/>, this only surfaces the toast.
    /// </summary>
    public class EndGameSequencer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [Tooltip("Randomized winner / loser lines shown on the reveal toast. Falls back to " +
                 "the scoring rule's VICTORY/DEFEAT header when unset.")]
        [SerializeField] EndGameMessageSetSO messageSet;

        [Header("Reveal UI (auto-found by name under the canvas if left empty)")]
        [Tooltip("CanvasGroup on the ScoreRevealToast - faded in to reveal the toast.")]
        [SerializeField] CanvasGroup toastCanvasGroup;
        [Tooltip("The GameOverCounter text inside the toast - shows the win/lose line.")]
        [SerializeField] TMP_Text gameOverCounterText;

        [Header("Style-B reveal (Docs/MENU_REDESIGN_B.md section 22.4) - all optional")]
        [Tooltip("Above the line: 'Jade wins' in the winning domain's colour, or Victory / Defeat " +
                 "when no domain won.")]
        [SerializeField] TMP_Text winnerText;
        [Tooltip("The short rule under the line, in the same colour as the winner text.")]
        [SerializeField] UnityEngine.UI.Image winnerRule;
        [Tooltip("Under the rule: the mode's own number - 'Race time 2:41.37', from the scoring rule.")]
        [SerializeField] TMP_Text detailText;
        [Tooltip("Played as the reveal comes in - the winner line, the rule growing out, the detail - " +
                 "each on its own delay. The toast shows through its CanvasGroup, so they are started " +
                 "here rather than on enable.")]
        [SerializeField] KitEntrance[] revealEntrances = new KitEntrance[0];

        [Header("Timing")]
        [Tooltip("Seconds the reveal toast holds before the scoreboard appears.")]
        [SerializeField, Min(0f)] float revealDuration = 3f;

        [Header("Toast Animation")]
        [SerializeField, Min(0f)] float toastInDuration = 0.45f;
        [SerializeField, Min(0f)] float toastOutDuration = 0.35f;
        [SerializeField] float toastPunchScale = 1.15f;

        [Header("Vessel Flourish")]
        [Tooltip("When true the local vessel performs a RANDOM AI cinematic maneuver during " +
                 "the reveal instead of being frozen. When false it uses 'Vessel Behavior'.")]
        [SerializeField] bool randomizeVesselBehavior = true;
        [SerializeField] AICinematicBehaviorType vesselBehavior = AICinematicBehaviorType.MoveForward;

        // Only the behaviors AICinematicBehavior actually implements; the rest fall back
        // to MoveForward, so picking from these keeps the flourish visibly varied.
        static readonly AICinematicBehaviorType[] ImplementedBehaviors =
        {
            AICinematicBehaviorType.MoveForward,
            AICinematicBehaviorType.Loop,
            AICinematicBehaviorType.Drift,
            AICinematicBehaviorType.Spiral,
        };

        bool _isRunning;
        Coroutine _revealRoutine;
        Sequence _toastSeq;

        void OnEnable()
        {
            if (gameData != null)
            {
                gameData.OnWinnerCalculated.OnRaised += HandleWinnerCalculated;
                gameData.OnResetForReplay.OnRaised += HandleResetForReplay;
            }

            var progression = GameModeProgressionService.Instance;
            if (progression != null)
            {
                progression.OnQuestCompleted += HandleQuestCompleted;
                progression.OnIntensityUnlocked += HandleIntensityUnlocked;
            }
        }

        void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnWinnerCalculated.OnRaised -= HandleWinnerCalculated;
                gameData.OnResetForReplay.OnRaised -= HandleResetForReplay;
            }

            var progression = GameModeProgressionService.Instance;
            if (progression != null)
            {
                progression.OnQuestCompleted -= HandleQuestCompleted;
                progression.OnIntensityUnlocked -= HandleIntensityUnlocked;
            }

            if (_revealRoutine != null)
            {
                // The end screen is raised at the END of this coroutine; stopping it here means no scoreboard. Loud,
                // so whatever disabled this object mid-reveal shows up in the console (Scoreboard's safety net then
                // shows the board).
                CSDebug.LogWarning($"[EndGameSequencer] '{name}' was disabled during the end reveal - the end screen " +
                                   "would never have been raised. Find what disables it at the end of a match.");
                StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
            _toastSeq?.Kill();
            _isRunning = false;
        }

        /// <summary>
        /// An IN-PLACE replay (Cellular Duel's rematch: no scene reload, so this component is
        /// never disabled) is a new game. Without this the latch stayed set from game one, the
        /// rematch's OnWinnerCalculated returned early, nothing raised OnShowGameEndScreen, and
        /// every peer was left in the arena with no scoreboard and no way out. Cleared here and
        /// not at the end of RunReveal, so a repeated raise within one game still cannot run the
        /// reveal (and the end screen) twice.
        /// </summary>
        void HandleResetForReplay()
        {
            if (_revealRoutine != null)
            {
                StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }
            _toastSeq?.Kill();
            _isRunning = false;
        }

        void HandleWinnerCalculated()
        {
            if (_isRunning) return;
            _isRunning = true;
            _revealRoutine = StartCoroutine(RunReveal());
        }

        IEnumerator RunReveal()
        {
            // The reveal is cosmetic; the OnShowGameEndScreen raise at the end is NOT — it is the
            // only path to the Scoreboard, and in a tournament the Scoreboard's host-only Continue
            // is the only way to advance the party. An exception anywhere in the flourish / SFX /
            // toast (a coroutine dies on its first unhandled throw) must degrade to a skipped
            // flourish — never to a finished game with no end-game screen.
            try
            {
                bool didWin = DidLocalPlayerWin();

                HaltOtherVessels();
                StartLocalVesselFlourish();

                if (AudioSystem.Instance != null)
                    AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GameEnd);

                DrawRevealLines(didWin);
                ShowToast(ResolveMessage(didWin));
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[EndGameSequencer] Reveal flourish threw — continuing to the end screen anyway.");
            }

            // Unscaled: a zero timescale at game end (pause) must not freeze the reveal forever.
            yield return new WaitForSecondsRealtime(revealDuration);

            try
            {
                HideToast();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }

            int shown = Scoreboard.ShowCount;
            gameData.InvokeShowGameEndScreen();
            // A raise no board answered left the player in the arena with no way out: find the board and show it.
            if (Scoreboard.ShowCount == shown) Scoreboard.ShowUnreached();
            _revealRoutine = null;
        }

        bool DidLocalPlayerWin()
        {
            if (gameData == null || gameData.LocalPlayer == null) return false;

            // Domain modes set WinnerDomain authoritatively.
            if (gameData.WinnerDomain != Domains.Blue)
                return gameData.LocalPlayer.Domain == gameData.WinnerDomain;

            // Non-domain modes fall back to the per-domain stats winner check.
            return gameData.IsLocalDomainWinner(out _);
        }

        string ResolveMessage(bool didWin)
        {
            if (messageSet != null)
                return messageSet.GetRandomMessage(didWin);

            // Fallback to the scoring rule's VICTORY/DEFEAT header when no set is wired.
            var rule = gameData != null ? gameData.ScoringRule : null;
            if (rule != null && gameData.LocalRoundStats != null)
                return rule.BuildReveal(gameData, gameData.LocalRoundStats, didWin).Header;

            return didWin ? "VICTORY" : "DEFEAT";
        }

        /// <summary>
        /// Freezes every vessel EXCEPT the local player's (which flies the flourish), so the
        /// field is still behind the reveal. Uses IsStationary + SetPause - both reset every
        /// round (vessels are respawned on scene reload; SetPause is cleared by StartPlayer) -
        /// so nothing leaks into the next tournament game.
        /// </summary>
        void HaltOtherVessels()
        {
            if (gameData == null || gameData.Players == null) return;

            var local = gameData.LocalPlayer;
            foreach (var player in gameData.Players)
            {
                if (player == null || player == local) continue;
                if (player.Vessel?.VesselStatus != null)
                    player.Vessel.VesselStatus.IsStationary = true;
                player.InputController?.SetPause(true);
            }
        }

        /// <summary>
        /// Hands the local vessel a (random) AI cinematic flourish so it keeps flying
        /// during the reveal instead of being frozen. Mirrors the old cinematic's
        /// <c>SetLocalVesselAI(true, …)</c>: disable human input, enable the AI pilot,
        /// stop the Sparrow's prism spawn, then start the chosen behavior.
        /// </summary>
        void StartLocalVesselFlourish()
        {
            var player = gameData != null ? gameData.LocalPlayer : null;
            var status = player?.Vessel?.VesselStatus;
            if (status == null) return;

            // Pause human input so it doesn't fight the AI flourish. Use SetPause - NOT
            // InputController.enabled = false - because the InputController lives on the
            // PERSISTENT Player object: a disabled component is never re-enabled, so the
            // vessel would stay uncontrollable in the next tournament round. SetPause(true)
            // is the proven menu-autopilot pattern and is reset every round by
            // Player.StartPlayer() -> SetPause(false).
            if (player.InputController != null)
                player.InputController.SetPause(true);
            player.Vessel.ToggleAIPilot(true);

            // Sparrow keeps laying prisms forever otherwise.
            if (status.VesselType == VesselClassType.Sparrow && status.VesselPrismController != null)
                status.VesselPrismController.StopSpawn();

            var behavior = status.AICinematicBehavior;
            if (behavior == null) return;
            behavior.Initialize(status, status.AIPilot);
            behavior.StartCinematicBehavior(PickBehavior());
        }

        AICinematicBehaviorType PickBehavior() =>
            randomizeVesselBehavior
                ? ImplementedBehaviors[Random.Range(0, ImplementedBehaviors.Length)]
                : vesselBehavior;

        /// <summary>
        /// The lines around the message. The message itself is the crowd line (CLUTCHED IT); these
        /// say who won and by what, in the mode's own terms.
        /// </summary>
        void DrawRevealLines(bool didWin)
        {
            if (!winnerText && !winnerRule && !detailText) return;

            var domain = gameData != null ? gameData.WinnerDomain : Domains.Blue;
            bool named = domain != Domains.Blue && (int)domain != 0;
            var palette = gameData != null ? gameData.ThemeManagerData : null;
            Color accent = named && palette != null
                ? palette.GetDomainUIColor(domain)
                : UITheme.Resolve(null, didWin ? UIColorToken.TextLight : UIColorToken.NeutralLightest);

            if (winnerText)
            {
                winnerText.text = named ? $"{domain} wins" : didWin ? "Victory" : "Defeat";
                winnerText.color = accent;
            }
            if (winnerRule) winnerRule.color = accent;

            if (!detailText) return;
            detailText.text = string.Empty;
            var rule = gameData != null ? gameData.ScoringRule : null;
            if (rule == null || gameData.LocalRoundStats == null) return;
            var reveal = rule.BuildReveal(gameData, gameData.LocalRoundStats, didWin);
            if (string.IsNullOrEmpty(reveal.Label)) return;
            string value = reveal.FormatAsTime
                ? ScoreResultBuilder.FormatTime(reveal.Value)
                : reveal.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            detailText.text = TabularText.Wrap($"{SentenceCase(reveal.Label)} {value}", detailText.font);
        }

        /// <summary>'RACE TIME' -> 'Race time'. The rules author their labels in caps for the old board.</summary>
        public static string SentenceCase(string caps)
        {
            if (string.IsNullOrEmpty(caps)) return caps;
            string lower = caps.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        void ShowToast(string message)
        {
            EnsureRevealRefs();

            if (gameOverCounterText != null)
                gameOverCounterText.text = message;

            if (toastCanvasGroup == null) return;

            _toastSeq?.Kill();
            var t = toastCanvasGroup.transform;
            toastCanvasGroup.alpha = 0f;
            toastCanvasGroup.blocksRaycasts = false;
            t.localScale = Vector3.one * 0.85f;

            for (int i = 0; i < revealEntrances.Length; i++)
                if (revealEntrances[i]) revealEntrances[i].Play();

            _toastSeq = DOTween.Sequence()
                .Append(toastCanvasGroup.DOFade(1f, toastInDuration))
                .Join(t.DOScale(toastPunchScale, toastInDuration).SetEase(Ease.OutBack))
                .Append(t.DOScale(1f, 0.15f).SetEase(Ease.OutQuad));
        }

        void HideToast()
        {
            if (toastCanvasGroup == null) return;
            _toastSeq?.Kill();
            _toastSeq = DOTween.Sequence()
                .Append(toastCanvasGroup.DOFade(0f, toastOutDuration))
                .Join(toastCanvasGroup.transform.DOScale(0.85f, toastOutDuration).SetEase(Ease.InQuad));
        }

        /// <summary>
        /// Resolves the toast CanvasGroup + GameOverCounter text by name when they are not
        /// wired in the inspector (both live under the same EndGameStatsPanel prefab).
        /// </summary>
        void EnsureRevealRefs()
        {
            if (toastCanvasGroup == null)
            {
                var toast = FindUnderRoot("ScoreRevealToast");
                // TryGetComponent, not `GetComponent() ?? AddComponent()`: a missing component is
                // a fake-null object in the Editor, which the coalesce keeps.
                if (toast != null && !toast.TryGetComponent(out toastCanvasGroup))
                    toastCanvasGroup = toast.gameObject.AddComponent<CanvasGroup>();
            }

            if (gameOverCounterText == null && toastCanvasGroup != null)
            {
                var counter = toastCanvasGroup.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(x => x.name == "GameOverCounter");
                gameOverCounterText = counter != null
                    ? counter.GetComponent<TMP_Text>()
                    : toastCanvasGroup.GetComponentInChildren<TMP_Text>(true);
            }
        }

        Transform FindUnderRoot(string targetName)
        {
            var root = transform.root;
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(x => x.name == targetName);
        }

        void HandleQuestCompleted(SO_UnlockData quest)
        {
            if (quest == null || quest.IsPlaceholder) return;
            ToastNotificationAPI.Show($"Quest Complete!\n{quest.DisplayName}", ToastKind.Success);
        }

        void HandleIntensityUnlocked(GameModes mode, int intensity)
        {
            var progression = GameModeProgressionService.Instance;
            var quest = progression != null ? progression.GetQuestForMode(mode) : null;
            string label = quest != null ? quest.DisplayName : mode.ToString();
            ToastNotificationAPI.Show($"{label} Intensity {intensity} Unlocked!", ToastKind.Success);
        }
    }
}
