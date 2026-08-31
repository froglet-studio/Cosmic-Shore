using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The preview's "+1" toast - a tiny pop where the IN-GAME toast feed lives, so the preview
    /// teaches the same reflex the match uses: do the objective thing, see the tick where you
    /// will see it in the game. One label and one icon; a new event restarts it (latest wins,
    /// the same rule the game toast feed follows).
    ///
    /// <para>Author it as an inactive child at the in-game toast's screen position; the panel
    /// drives it off the same objective-progress event that pulses the objective box.</para>
    /// </summary>
    public class PreviewMicroToast : MonoBehaviour
    {
        [SerializeField, Tooltip("The whole toast, faded as one.")]
        CanvasGroup group;

        [SerializeField, Tooltip("'+1' (or '+2' for a multi-count event).")]
        TMP_Text label;

        [SerializeField, Tooltip("The metric's icon beside the label. Hidden when none.")]
        Image icon;

        [SerializeField, Tooltip("Seconds at full opacity before the fade.")]
        [Min(0.1f)] float holdSeconds = 0.8f;

        [SerializeField, Tooltip("Seconds the fade-out takes.")]
        [Min(0.05f)] float fadeSeconds = 0.4f;

        [SerializeField, Tooltip("Scale the toast pops in at, settling to 1.")]
        [Min(1f)] float popScale = 1.3f;

        float _life;      // counts down: holdSeconds + fadeSeconds → 0
        Color _labelRest;
        Color _iconRest;
        bool _restCaptured;

        /// <summary>
        /// Pop the toast. <paramref name="tint"/> is the LIVE domain signal colour of the player
        /// who scored - the toast wears your team's colour, resolved at event time so a mid-menu
        /// domain change re-colours the very next tick. Null keeps the authored colours.
        /// </summary>
        public void Show(int delta, Sprite metricIcon, Color? tint = null)
        {
            if (!_restCaptured)
            {
                if (label) _labelRest = label.color;
                if (icon) _iconRest = icon.color;
                _restCaptured = true;
            }

            if (label)
            {
                label.text = delta > 0 ? $"+{delta}" : delta.ToString();
                label.color = tint ?? _labelRest;
            }

            if (icon)
            {
                icon.gameObject.SetActive(metricIcon);
                if (metricIcon) icon.sprite = metricIcon;
                icon.color = tint ?? _iconRest;
            }

            _life = holdSeconds + fadeSeconds;
            gameObject.SetActive(true);
            if (group) group.alpha = 1f;
            transform.localScale = Vector3.one * popScale;
        }

        public void Hide()
        {
            _life = 0f;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (_life <= 0f) { gameObject.SetActive(false); return; }

            _life -= Time.unscaledDeltaTime;

            // Settle the pop quickly, then hold, then fade.
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one,
                                                Time.unscaledDeltaTime * 10f);
            if (group && _life < fadeSeconds)
                group.alpha = Mathf.Clamp01(_life / fadeSeconds);
        }
    }
}
