using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel's OBJECTIVE box: the word "OBJECTIVE", the metric's icon (the crystal for
    /// Scurry, the joust mark for Joust), how you win in one line, and a live counter.
    ///
    /// <para><b>The counter has no target.</b> It shows the count going up, never "3 / 39" - the
    /// box says what winning IS, and watching the number climb is the feedback; a denominator is
    /// pressure the launch screen does not need.</para>
    ///
    /// <para>The icon comes from the ONE metric→icon table every objective surface shares
    /// (<see cref="ModeControlsLibrarySO.ObjectiveIcons"/>), so the box and the micro toast can
    /// never disagree about what a crystal looks like.</para>
    /// </summary>
    public class ObjectiveBoxView : MonoBehaviour
    {
        [SerializeField, Tooltip("The metric's icon. Hidden when the metric has none authored.")]
        Image icon;

        [SerializeField, Tooltip("The live counter. Counts up from 0; never shows a target.")]
        TMP_Text countText;

        [SerializeField, Tooltip("Optional: how you win, in one authored line (the definition's " +
                                 "ObjectiveText).")]
        TMP_Text objectiveText;

        [SerializeField, Tooltip("Scale the icon and counter reach at the top of a pulse.")]
        [Min(1f)] float pulseScale = 1.35f;

        [SerializeField, Tooltip("Seconds a pulse takes to settle back to rest.")]
        [Min(0.05f)] float pulseSeconds = 0.35f;

        float _pulse;      // 1 at the moment of an increment, decaying to 0
        int _count;

        // The pulse wears the scoring player's DOMAIN colour - resolved live by the caller at
        // each event (never snapshotted, per the domain-sync rule), and lerped back to the
        // authored rest colours as the pulse decays.
        Color _flashTint = Color.white;
        Color _iconRest = Color.white;
        Color _countRest = Color.white;
        bool _restCaptured;

        /// <summary>Fill the box for a mode. Resolves the icon from the shared table.</summary>
        public void Bind(ScoringMetric metric, string howYouWin)
        {
            if (!_restCaptured)
            {
                if (icon) _iconRest = icon.color;
                if (countText) _countRest = countText.color;
                _restCaptured = true;
            }

            var library = Resources.Load<ModeControlsLibrarySO>(ModeControlsLibrarySO.ResourcePath);
            var sprite = library ? library.IconForMetric(metric) : null;

            if (icon)
            {
                icon.gameObject.SetActive(sprite);
                if (sprite) icon.sprite = sprite;
            }

            if (objectiveText)
            {
                bool has = !string.IsNullOrWhiteSpace(howYouWin);
                objectiveText.gameObject.SetActive(has);
                if (has) objectiveText.text = howYouWin.Trim();
            }

            SetCount(0, pulse: false);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// The live count. Pulses on an increase - the same beat the micro toast pops on.
        /// <paramref name="flash"/> is the scoring player's live domain signal colour; the pulse
        /// wears it and settles back to the authored rest. Null keeps the last tint (white until
        /// one is ever supplied).
        /// </summary>
        public void SetCount(int count, bool pulse = true, Color? flash = null)
        {
            if (flash.HasValue) _flashTint = flash.Value;
            if (pulse && count > _count) _pulse = 1f;
            _count = count;
            if (countText) countText.text = count.ToString();
        }

        public void Clear() => gameObject.SetActive(false);

        void Update()
        {
            if (_pulse <= 0f) return;

            // Unscaled: the menu can hold timeScale at 0 while the panel is open.
            _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime / pulseSeconds);
            float k = Mathf.SmoothStep(0f, 1f, _pulse);
            float scale = 1f + (pulseScale - 1f) * k;

            if (icon)
            {
                icon.rectTransform.localScale = Vector3.one * scale;
                icon.color = Color.Lerp(_iconRest, _flashTint, k);
            }
            if (countText)
            {
                countText.rectTransform.localScale = Vector3.one * scale;
                countText.color = Color.Lerp(_countRest, _flashTint, k);
            }
        }
    }
}
