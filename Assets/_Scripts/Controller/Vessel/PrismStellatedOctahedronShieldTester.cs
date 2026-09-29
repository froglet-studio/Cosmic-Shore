using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Standalone test harness for <see cref="PrismStellatedOctahedronShield"/>.
    /// Drop onto any GameObject that already has a BoxCollider, MeshFilter,
    /// MeshRenderer, and a PrismStellatedOctahedronShield component, hit play,
    /// and press Space to toggle the super-shield. Does not require the full
    /// Prism / PrismStateManager lifecycle.
    ///
    /// Mirrors <see cref="PrismOctahedronShieldTester"/> for the stellated
    /// (super-shielded) variant — including its role as the in-editor verification
    /// rig for the GPU shield morph, and the MaterialPropertyBlock stamp path a
    /// Prism-less host takes (see that class). Uses the new Input System package
    /// (<c>UnityEngine.InputSystem</c>) because the project has legacy Input
    /// handling disabled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrismStellatedOctahedronShield))]
    public class PrismStellatedOctahedronShieldTester : MonoBehaviour
    {
        [Tooltip("If true, toggles automatically every intervalSeconds while playing.")]
        [SerializeField] private bool autoToggle = false;

        [Tooltip("Seconds between automatic toggles when autoToggle is enabled.")]
        [SerializeField] private float intervalSeconds = 2f;

        [Tooltip("If true, prints each toggle to the console.")]
        [SerializeField] private bool logToggles = true;

        private PrismStellatedOctahedronShield _shield;
        private float _timer;

        private void Awake()
        {
            _shield = GetComponent<PrismStellatedOctahedronShield>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                _shield.Toggle();
                if (logToggles)
                    Debug.Log($"[SuperShieldTester] Space → shielded={_shield.IsShielded}");
            }

            if (autoToggle)
            {
                _timer += Time.deltaTime;
                if (_timer >= intervalSeconds)
                {
                    _timer = 0f;
                    _shield.Toggle();
                    if (logToggles)
                        Debug.Log($"[SuperShieldTester] auto-toggled → shielded={_shield.IsShielded}");
                }
            }
        }
    }
}
