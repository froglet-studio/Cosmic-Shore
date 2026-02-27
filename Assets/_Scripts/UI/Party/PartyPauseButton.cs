using CosmicShore.Gameplay;
using CosmicShore.Soap;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.UI
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

        public void OnPauseToggle()
        {
            if (_isPaused)
                ResumePlay();
            else
                PausePlay();
        }

        public void PausePlay()
        {
            if (_isPaused) return;
            _isPaused = true;

            var localPlayer = gameData ? gameData.LocalPlayer : null;
            if (localPlayer?.Vessel != null)
            {
                if (localPlayer.InputStatus != null)
                    _wasInputPausedBefore = localPlayer.InputStatus.Paused;

                localPlayer.Vessel.ToggleAIPilot(true);
                SetInputPauseDelayed(true).Forget();
            }
            else
            {
                CSDebug.Log("[PartyPause] PausePlay: no local player/vessel, showing panel only.");
            }

            if (partyPausePanel)
                partyPausePanel.Show();
        }

        public void ResumePlay()
        {
            if (!_isPaused) return;
            _isPaused = false;

            var localPlayer = gameData ? gameData.LocalPlayer : null;
            if (localPlayer?.Vessel != null)
            {
                localPlayer.Vessel.ToggleAIPilot(false);

                if (!_wasInputPausedBefore)
                    SetInputPauseDelayed(false).Forget();
            }

            if (partyPausePanel)
                partyPausePanel.Hide();
        }

        public void ForcePause()
        {
            PausePlay();
        }

        async UniTaskVoid SetInputPauseDelayed(bool pause)
        {
            await UniTask.Yield();
            var localPlayer = gameData ? gameData.LocalPlayer : null;
            if (localPlayer?.InputController != null)
                localPlayer.InputController.SetPause(pause);
        }
    }
}
