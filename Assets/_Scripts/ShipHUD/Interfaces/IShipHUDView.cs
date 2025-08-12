using CosmicShore;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipHUDView
    {
        // Called right after spawn to set up logic/event hooks
        void Initialize(IShipHUDController controller);
        ResourceDisplay GetResourceDisplay(string resourceName);
        Transform GetSilhouetteContainer();
        Transform GetTrailContainer();
        void AnimateBoostFillDown(int resourceIndex, float duration, float startingAmount);
        void AnimateBoostFillUp(int resourceIndex, float duration, float endingAmount);
        /// <summary>
        /// Called by the HUD controller when the player presses a button.
        /// </summary>
        void OnInputPressed(int buttonNumber);

        /// <summary>
        /// Called when the input is released (optional).
        /// </summary>
        void OnInputReleased(int buttonNumber);
        
        void OnSeedAssembleStarted();
        void OnSeedAssembleCompleted();
        void OnOverheatBuildStarted();
        void OnOverheated();
        void OnHeatDecayCompleted();
        void OnFullAutoStarted();
        void OnFullAutoStopped();
        void OnFireGunFired();
        void OnStationaryToggled(bool isOn);

    }
}
