using CosmicShore.App.Systems;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class PauseButton : MonoBehaviour
    {
        public void OnClickPauseButton() => PauseSystem.TogglePauseGame(!PauseSystem.Paused);
    }
}