using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class SpiderVesselHUDView : VesselHUDView
    {
        [Header("Spider - Swing Indicator")]
        [SerializeField] private Image swingIndicator;
        [SerializeField] private Color swingActiveColor = Color.cyan;
        [SerializeField] private Color swingInactiveColor = Color.white;

        [Header("Spider - Debug Telemetry")]
        [Tooltip("Tuning readout (speed / L / h / state) so terminal velocity and the pump/bleed are visible while dialing the dissipation knobs. Ship with this OFF.")]
        [SerializeField] private bool showDebugTelemetry;
        [Tooltip("TMP text the telemetry renders into. Optional — no text, no readout.")]
        [SerializeField] private TMP_Text telemetryText;

        public override void Initialize()
        {
            if (swingIndicator)
            {
                swingIndicator.color = swingInactiveColor;
                swingIndicator.enabled = false;
            }

            if (telemetryText)
                telemetryText.enabled = false;
        }

        public void SetSwinging(bool isSwinging)
        {
            if (!swingIndicator) return;

            swingIndicator.enabled = isSwinging;
            swingIndicator.color = isSwinging ? swingActiveColor : swingInactiveColor;
        }

        /// <summary>
        /// Debug-only readout of the swing physics state. String formatting
        /// allocates, which is acceptable here: the call is gated behind
        /// <see cref="ShowDebugTelemetry"/> and exists purely for tuning.
        /// </summary>
        public void SetTelemetry(float speed, float angularMomentum, float circleRadius, string state)
        {
            if (!telemetryText) return;

            if (!showDebugTelemetry)
            {
                if (telemetryText.enabled)
                    telemetryText.enabled = false;
                return;
            }

            if (!telemetryText.enabled)
                telemetryText.enabled = true;

            telemetryText.text = $"v {speed:F1}\nL {angularMomentum:F1}\nh {circleRadius:F1}\n{state}";
        }
    }
}
