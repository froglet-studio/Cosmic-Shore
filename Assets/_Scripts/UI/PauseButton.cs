using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    public class PauseButton : MonoBehaviour
    {
        public void OnClickPauseButton() => PauseSystem.TogglePauseGame(!PauseSystem.Paused);
    }
}