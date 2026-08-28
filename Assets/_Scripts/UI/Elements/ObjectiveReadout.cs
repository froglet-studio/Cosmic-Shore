using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The top-left OBJECTIVE readout: the mode's objective glyph beside the count still needed
    /// to win. It replaces the ring cluster the number used to sit inside (BigCircle + three
    /// rotating rings + a timer face), which was chrome around a number that had nothing to do
    /// with time - every turn monitor raises <c>onUpdateTurnMonitorDisplay</c> with the metric
    /// REMAINING, so the ring was drawing a clock face over an objective count.
    ///
    /// It adds no plumbing. The number is the same <see cref="TMP_Text"/>
    /// <see cref="MiniGameHUDView.UpdateCountdownTimer"/> already writes, so no turn monitor, no
    /// event and no end condition changes; this component owns only the ICON, resolved from the
    /// mode's <see cref="ScoringMetric"/> through <see cref="ObjectiveIconSetSO"/>.
    ///
    /// A mode with no target metric (<see cref="ScoringMetric"/> unmapped, or no scoring rule
    /// yet) draws no glyph rather than a wrong one - blank is the honest state, the same rule the
    /// ability lockup's control chip follows.
    /// </summary>
    public class ObjectiveReadout : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The objective glyph. Tinted here, so the art stays pure white.")]
        [SerializeField] Image icon;

        [Tooltip("Optional. The count still needed to win - the SAME text the HUD view drives " +
                 "through UpdateCountdownTimer. Assigned only so this component can hide the " +
                 "whole readout when the mode publishes no number.")]
        [SerializeField] TMP_Text valueText;

        [Tooltip("Leave empty to load Resources/ObjectiveIconSet.")]
        [SerializeField] ObjectiveIconSetSO iconSet;

        [Header("Style")]
        [Tooltip("Glyph tint. Style Foundation section 2: Light E6E9FF for HUD chrome.")]
        [SerializeField] Color iconTint = new Color(0.902f, 0.914f, 1f, 1f);

        bool _hasMetric;

        void Awake()
        {
            if (iconSet == null) iconSet = ObjectiveIconSetSO.Load();
            if (icon) icon.color = iconTint;
            Apply(null);
        }

        /// <summary>
        /// Point the readout at a mode's scoring metric. Idempotent and safe to call before the
        /// game config has synced - <paramref name="metric"/> null means "not known yet", which
        /// hides the glyph instead of guessing.
        /// </summary>
        public void SetMetric(ScoringMetric? metric)
        {
            _hasMetric = metric.HasValue;
            Apply(_hasMetric ? (iconSet != null ? iconSet.For(metric.Value) : null) : null);
        }

        void Apply(Sprite sprite)
        {
            if (!icon) return;
            icon.sprite = sprite;
            // An Image with no sprite still draws a white box, so absence has to switch it off.
            icon.enabled = sprite != null;
        }

        /// <summary>
        /// True once a metric has been resolved. The HUD uses it to decide whether the readout
        /// is worth showing at all in a mode that publishes no objective count.
        /// </summary>
        public bool HasMetric => _hasMetric;

        /// <summary>The number this readout labels, for a caller that wants to style it.</summary>
        public TMP_Text ValueText => valueText;
    }
}
