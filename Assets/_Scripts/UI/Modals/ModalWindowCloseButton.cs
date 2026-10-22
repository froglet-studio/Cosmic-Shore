using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Closes the window this button sits in, the same way gamepad B does: through
    /// <see cref="ModalWindowManager.ModalWindowOut"/>, so a click and a B press play the same out
    /// animation, the same close sound and unwind the same modal stack entry.
    ///
    /// <para>Found by walking up rather than wired, so a window prefab can carry its own Back
    /// button into any modal it is placed in. The arcade's old Back deactivated the modal root and
    /// then navigated HOME through two persistent calls - a second close path B never took (style-B
    /// menu redesign, item 3).</para>
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ModalWindowCloseButton : MonoBehaviour
    {
        private Button _button;
        private ModalWindowManager _window;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (_button) _button.onClick.RemoveListener(Close);
        }

        private void Close()
        {
            if (!_window) _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window)
                _window.ModalWindowOut();
            else
                CSDebug.LogWarning($"[ModalWindowCloseButton] '{name}' is not inside a ModalWindowManager - nothing to close.");
        }
    }
}
