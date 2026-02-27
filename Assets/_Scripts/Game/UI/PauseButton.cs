using CosmicShore.App.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class PauseButton : MonoBehaviour
    {
        Button _button;

        void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnClickPauseButton() => PauseSystem.TogglePauseGame(!PauseSystem.Paused);

        public void SetInteractable(bool interactable)
        {
            if (_button) _button.interactable = interactable;
        }
    }
}
