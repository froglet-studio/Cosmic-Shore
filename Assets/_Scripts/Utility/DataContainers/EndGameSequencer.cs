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
                gameData.OnWinnerCalculated.OnRaised += HandleWinnerCalculated;

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
                gameData.OnWinnerCalculated.OnRaised -= HandleWinnerCalculated;

            var progression = GameModeProgressionService.Instance;
            if (progression != null)
            {
                progression.OnQuestCompleted -= HandleQuestCompleted;
                progression.OnIntensityUnlocked -= HandleIntensityUnlocked;
            }

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
            // The reveal is cosmetic; the OnShowGameEndScreen raise at the end is NOT - it is the
            // only path to the Scoreboard, and in a tournament the Scoreboard's host-only Continue
            // is the only way to advance the party. An exception anywhere in the flourish / SFX /
            // toast (a coroutine dies on its first unhandled throw) must degrade to a skipped
            // flourish - never to a finished game with no end-game screen.
            try
            {
                HaltOtherVessels();
                StartLocalVesselFlourish();
                AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GameEnd);
                ShowToast(ResolveMessage(DidLocalPlayerWin()));
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[EndGameSequencer] End-game reveal failed - skipping to the end-game screen: {e}");
            }

            yield return new WaitForSeconds(revealDuration);

            try
            {
                HideToast();
            }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[EndGameSequencer] HideToast failed: {e}");
            }

            gameData.InvokeShowGameEndScreen();
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
                if (toast != null)
                    toastCanvasGroup = toast.GetComponent<CanvasGroup>()
                        ?? toast.gameObject.AddComponent<CanvasGroup>();
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

        void HandleQuestCompleted(SO_GameModeQuestData quest)
        {
            if (quest == null || quest.IsPlaceholder) return;
            ToastNotificationAPI.Show($"Quest Complete!\n{quest.DisplayName}");
        }

        void HandleIntensityUnlocked(GameModes mode, int intensity)
        {
            var progression = GameModeProgressionService.Instance;
            var quest = progression != null ? progression.GetQuestForMode(mode) : null;
            string label = quest != null ? quest.DisplayName : mode.ToString();
            ToastNotificationAPI.Show($"{label} Intensity {intensity} Unlocked!");
        }
    }
}
