using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace CosmicShore.Utility
{
    /// <summary>
    /// Listens for the gesture that flips <see cref="DevDiagnostics"/>: four or more fingers held
    /// on the screen for <see cref="HoldSeconds"/>, or Shift+F7 on a keyboard (F7 alone is the
    /// diagnostics HUD's show / hide). It lives apart from the HUD because with diagnostics off
    /// there is no HUD to listen. Auto-spawned in the Editor and development builds; an empty
    /// shell in Release (<c>Docs/CONDITIONAL_COMPILATION.md</c>, pattern 1).
    /// </summary>
    public class DevDiagnosticsToggle : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const int FingersToToggle = 4;
        const float HoldSeconds = 2f;

        static DevDiagnosticsToggle s_instance;

        float _heldFor;
        // One flip per hold: every finger has to lift before the next one counts.
        bool _firedThisHold;

        internal static void EnsureSpawned()
        {
            if (s_instance) return;
            var go = new GameObject("[DevDiagnosticsToggle]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<DevDiagnosticsToggle>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.F7].wasPressedThisFrame && kb.shiftKey.isPressed)
                DevDiagnostics.Toggle();

            var screen = Touchscreen.current;
            if (screen == null) return;

            var touches = screen.touches;
            int down = 0;
            for (int i = 0; i < touches.Count; i++)
                if (touches[i].press.isPressed) down++;

            if (down == 0)
            {
                _heldFor = 0f;
                _firedThisHold = false;
                return;
            }
            if (down < FingersToToggle || _firedThisHold)
            {
                _heldFor = 0f;
                return;
            }

            _heldFor += Time.unscaledDeltaTime;
            if (_heldFor < HoldSeconds) return;
            _firedThisHold = true;
            _heldFor = 0f;
            DevDiagnostics.Toggle();
        }
#endif
    }
}
