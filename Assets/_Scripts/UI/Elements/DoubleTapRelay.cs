using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.UI
{
    /// <summary>
    /// Passes a click on only when it is the SECOND within <see cref="window"/> seconds. Home's full-screen
    /// tap surface launches freestyle through this (the owner, 2026-10-17: "one tap in the screen is
    /// taking me into the game, it should be double tap only"), so a stray click on empty Home no longer
    /// drops the player into flight. The pad's Y and the fly controls still call
    /// MenuCrystalClickHandler.ToggleTransition directly, on one press.
    /// </summary>
    public class DoubleTapRelay : MonoBehaviour
    {
        [SerializeField, Min(0.1f), Tooltip("Seconds between the two taps of a double tap.")]
        private float window = 0.4f;

        [SerializeField, Tooltip("Raised on the second tap.")]
        private UnityEvent onDoubleTap = new();

        float _last = -10f;

        /// <summary>Wire a Button's onClick here.</summary>
        public void Tap()
        {
            float now = Time.unscaledTime;
            if (now - _last <= window)
            {
                _last = -10f;
                onDoubleTap.Invoke();
                return;
            }
            _last = now;
        }
    }
}
