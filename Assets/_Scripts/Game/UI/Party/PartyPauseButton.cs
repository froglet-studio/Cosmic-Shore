using CosmicShore.Game.Arcade.Party;
using CosmicShore.Soap;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game.UI.Party
{
    /// <summary>
    /// Replaces the normal pause button behavior during party mode.
    /// On pause: hands vessel control to AI, pauses input, shows party panel.
    /// On resume: takes back vessel control, resumes input, hides party panel.
    /// Does NOT pause the game (Time.timeScale stays at 1).
    /// </summary>
    public class PartyPauseButton : MonoBehaviour
    {
        [SerializeField] GameDataSO gameData;
        [SerializeField] PartyPausePanel partyPausePanel;

        bool _isPaused;
        bool _wasInputPausedBefore;

        public bool IsPaused => _isPaused;

        /// <summary>
        /// Called by the pause button in the party scene.
        /// Toggles between paused (AI control) and active (player control).
        /// </summary>
        public void OnPauseToggle()
        {
            if (_isPaused)
                ResumePlay();
            else
                PausePlay();
        }

        /// <summary>
        /// Pause: hand vessel to AI, show party panel.
        /// </summary>
        public void PausePlay()
        {
            if (_isPaused) return;
            _isPaused = true;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel != null)
            {
                // Remember if input was already paused (to restore correctly on resume)
                _wasInputPausedBefore = localPlayer.InputStatus.Paused;

                // Hand control to AI
                localPlayer.Vessel.ToggleAIPilot(true);

                // Pause player input
                _ = SetInputPauseDelayed(true);
            }

            if (partyPausePanel)
                partyPausePanel.Show();
        }

        /// <summary>
        /// Resume: take back vessel from AI, resume input, hide party panel.
        /// </summary>
        public void ResumePlay()
        {
            if (!_isPaused) return;
            _isPaused = false;

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel != null)
            {
                // Take back control from AI
                localPlayer.Vessel.ToggleAIPilot(false);

                // Restore input state
                if (!_wasInputPausedBefore)
                    _ = SetInputPauseDelayed(false);
            }

            if (partyPausePanel)
                partyPausePanel.Hide();
        }

        /// <summary>
        /// Force pause without toggling (e.g., when party panel is forced open).
        /// </summary>
        public void ForcePause()
        {
            PausePlay();
        }

        async UniTaskVoid SetInputPauseDelayed(bool pause)
        {
            await UniTask.Yield();
            gameData.LocalPlayer?.InputController.SetPause(pause);
        }
    }
}
