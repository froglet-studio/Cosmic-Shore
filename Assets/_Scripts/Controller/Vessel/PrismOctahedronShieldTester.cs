using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Standalone test harness for <see cref="PrismOctahedronShield"/>. Drop
    /// onto any GameObject that already has a BoxCollider, MeshFilter,
    /// MeshRenderer, and a PrismOctahedronShield component, hit play, and
    /// press the configured key to toggle the shield. Does not require the
    /// full Prism / PrismStateManager lifecycle.
    ///
    /// The <c>OctahedronShieldTest.prefab</c> under <c>Assets/_Prefabs/Tools/</c>
    /// is preconfigured with everything this harness needs — drag it into
    /// any scene and press Space.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrismOctahedronShield))]
    public class PrismOctahedronShieldTester : MonoBehaviour
    {
        [Tooltip("Key that toggles the shield at runtime.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Space;

        [Tooltip("If true, toggles automatically every intervalSeconds while playing.")]
        [SerializeField] private bool autoToggle = false;

        [Tooltip("Seconds between automatic toggles when autoToggle is enabled.")]
        [SerializeField] private float intervalSeconds = 2f;

        [Tooltip("If true, prints each toggle to the console.")]
        [SerializeField] private bool logToggles = true;

        private PrismOctahedronShield _shield;
        private float _timer;

        private void Awake()
        {
            _shield = GetComponent<PrismOctahedronShield>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                _shield.Toggle();
                if (logToggles)
                    Debug.Log($"[ShieldTester] toggled → shielded={_shield.IsShielded}");
            }

            if (autoToggle)
            {
                _timer += Time.deltaTime;
                if (_timer >= intervalSeconds)
                {
                    _timer = 0f;
                    _shield.Toggle();
                    if (logToggles)
                        Debug.Log($"[ShieldTester] auto-toggled → shielded={_shield.IsShielded}");
                }
            }
        }
    }
}
