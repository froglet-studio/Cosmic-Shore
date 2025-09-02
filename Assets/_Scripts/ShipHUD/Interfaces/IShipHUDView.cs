using CosmicShore;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    public interface IShipHUDView
    {

        /// <summary>
        /// Called by the HUD controller when the player presses a button.
        /// </summary>
        void OnInputPressed(int buttonNumber);

        /// <summary>
        /// Called when the input is released (optional).
        /// </summary>
        void OnInputReleased(int buttonNumber);


    }
}
