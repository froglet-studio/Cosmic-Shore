using System.Collections;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player drifts — polls the local vessel's
    /// <see cref="IVesselStatus.IsDrifting"/> flag (input-scheme-agnostic) with a short
    /// sustain so a trigger flick doesn't pass the gate.
    ///
    /// With <see cref="requireBothDirections"/> (default) the player must perform BOTH a
    /// left drift (LT-dominant) and a right drift (RT-dominant), read from the trigger
    /// analogs while drifting; progress ("1 / 2") is appended to the active instruction
    /// text. Each direction pulses the success haptic.
    /// </summary>
    public class QuestWaitForDriftNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Waits until the local vessel drifts (sustained). Default: BOTH directions required — left drift (LT) and right drift (RT), each held for Hold Seconds, with 'n / 2' progress on the instruction text. Untick for a single sustained drift of any kind.";
        public override string EditorSummary => requireBothDirections
            ? $"Drift LEFT + RIGHT ({holdSeconds:0.##}s each)"
            : $"Drift held {holdSeconds:0.##}s";

        [Tooltip("Require a left drift AND a right drift (dominant trigger while drifting). Untick = any sustained drift passes.")]
        public bool requireBothDirections = true;

        [Tooltip("How long each drift must be sustained (real-time seconds).")]
        [Min(0f)] public float holdSeconds = 0.4f;

        [Tooltip("Analog threshold: a trigger must dominate the other by this much to count as that direction.")]
        [Range(0.05f, 0.9f)] public float triggerDominance = 0.25f;

        [Tooltip("Haptic pulse per completed drift (and on the gate completing).")]
        public HapticType successHaptic = HapticType.ButtonPress;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            float held = 0f, leftHeld = 0f, rightHeld = 0f;
            bool leftDone = false, rightDone = false;

            if (requireBothDirections)
                ctx.InstructionView?.SetProgress(0, 2);

            while (true)
            {
                var status = ctx.GameData?.LocalPlayer?.Vessel?.VesselStatus;
                bool drifting = status != null && status.IsDrifting;

                if (!requireBothDirections)
                {
                    held = drifting ? held + Time.unscaledDeltaTime : 0f;
                    if (held >= holdSeconds)
                    {
                        Pulse();
                        advance(QuestPorts.Next);
                        yield break;
                    }
                    yield return null;
                    continue;
                }

                var input = ctx.GameData?.LocalPlayer?.InputController?.InputStatus;
                if (drifting && input != null)
                {
                    float lt = input.LeftTriggerAnalog;
                    float rt = input.RightTriggerAnalog;

                    if (!leftDone && lt - rt > triggerDominance)
                    {
                        leftHeld += Time.unscaledDeltaTime;
                        if (leftHeld >= holdSeconds)
                        {
                            leftDone = true;
                            Pulse();
                            ctx.InstructionView?.SetProgress(rightDone ? 2 : 1, 2);
                        }
                    }
                    else if (!rightDone && rt - lt > triggerDominance)
                    {
                        rightHeld += Time.unscaledDeltaTime;
                        if (rightHeld >= holdSeconds)
                        {
                            rightDone = true;
                            Pulse();
                            ctx.InstructionView?.SetProgress(leftDone ? 2 : 1, 2);
                        }
                    }
                }
                else
                {
                    leftHeld = 0f;
                    rightHeld = 0f;
                }

                if (leftDone && rightDone)
                {
                    ctx.InstructionView?.SetProgress(0, 0);
                    advance(QuestPorts.Next);
                    yield break;
                }

                yield return null;
            }
        }

        void Pulse()
        {
            if (successHaptic != HapticType.None)
                HapticController.PlayHaptic(successHaptic);
        }
    }
}
