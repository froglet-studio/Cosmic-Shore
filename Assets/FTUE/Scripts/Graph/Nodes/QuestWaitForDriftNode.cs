using System.Collections;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player drifts (LT + RT) — polls the local vessel's
    /// <see cref="IVesselStatus.IsDrifting"/> flag, input-scheme-agnostic, and requires it
    /// to hold for a short sustain so a trigger flick doesn't pass the gate. Fires a
    /// success haptic when satisfied.
    /// </summary>
    public class QuestWaitForDriftNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the local vessel is drifting (LT+RT) sustained for Hold Seconds, then pulses the success haptic and advances.";
        public override string EditorSummary => $"Drift held {holdSeconds:0.##}s";

        [Tooltip("How long the drift must be sustained (real-time seconds).")]
        [Min(0f)] public float holdSeconds = 0.4f;

        [Tooltip("Haptic pulse when the player performs the drift correctly.")]
        public HapticType successHaptic = HapticType.ButtonPress;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            float held = 0f;
            while (true)
            {
                var status = ctx.GameData?.LocalPlayer?.Vessel?.VesselStatus;
                if (status != null && status.IsDrifting)
                {
                    held += Time.unscaledDeltaTime;
                    if (held >= holdSeconds)
                    {
                        if (successHaptic != HapticType.None)
                            HapticController.PlayHaptic(successHaptic);
                        advance(QuestPorts.Next);
                        yield break;
                    }
                }
                else
                {
                    held = 0f;
                }

                yield return null;
            }
        }
    }
}
