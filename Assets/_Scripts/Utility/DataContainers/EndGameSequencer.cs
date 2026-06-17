using System.Collections;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Slim end-game coordinator. Replaces the removed end-game cinematic — the
    /// Scoreboard is now the sole end-game UI.
    ///
    /// On <c>GameDataSO.OnWinnerCalculated</c> it:
    ///   1. halts every vessel (nothing keeps flying behind the scoreboard),
    ///   2. plays the GameEnd SFX,
    ///   3. raises <c>OnShowGameEndScreen</c> — the signal the Scoreboard and
    ///      <c>LifeForm</c> (ecology cleanup) already listen for.
    ///
    /// It also re-homes the two end-of-game progression notifications (quest
    /// complete / intensity unlocked) the cinematic used to show. The unlock and
    /// persistence themselves are owned by <see cref="GameModeProgressionService"/>;
    /// this only surfaces the toast.
    /// </summary>
    public class EndGameSequencer : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Header("Timing")]
        [Tooltip("Seconds to wait between winner-calculation and showing the " +
                 "scoreboard. 0 = immediate (the Scoreboard plays its own entrance " +
                 "animation, so there is no instant pop).")]
        [SerializeField, Min(0f)] float preScoreboardDelay = 0f;

        bool _isRunning;
        Coroutine _delayRoutine;

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

            if (_delayRoutine != null)
            {
                StopCoroutine(_delayRoutine);
                _delayRoutine = null;
            }
            _isRunning = false;
        }

        void HandleWinnerCalculated()
        {
            if (_isRunning) return;
            _isRunning = true;

            HaltAllVessels();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GameEnd);

            if (preScoreboardDelay > 0f)
                _delayRoutine = StartCoroutine(ShowScoreboardAfterDelay());
            else
                gameData.InvokeShowGameEndScreen();
        }

        IEnumerator ShowScoreboardAfterDelay()
        {
            yield return new WaitForSeconds(preScoreboardDelay);
            gameData.InvokeShowGameEndScreen();
            _delayRoutine = null;
        }

        /// <summary>
        /// Freezes every spawned vessel so the field is still while the scoreboard is
        /// up. <c>IsStationary</c> gates <c>VesselTransformer</c> movement (the AI pilot
        /// respects it too); pausing input stops steering/abilities. Replay reloads the
        /// scene for domain modes, so vessels respawn un-frozen.
        /// </summary>
        void HaltAllVessels()
        {
            if (gameData == null || gameData.Players == null) return;

            foreach (var player in gameData.Players)
            {
                if (player == null) continue;
                if (player.Vessel?.VesselStatus != null)
                    player.Vessel.VesselStatus.IsStationary = true;
                player.InputController?.SetPause(true);
            }
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
