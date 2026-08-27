using System.Collections;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Waits until the player skims N prisms. Rides the shared skim-boost SOAP channel
    /// (<c>ScriptableEventBoostChanged</c>, raised by <c>SkimmerBoostPrismEffectSO</c> per
    /// skimmed prism) — filtered to the local vessel, and counting only boost INCREASES
    /// (the same channel also fires on per-frame boost decay). Drives the instruction
    /// view's counter ("3 / 10") and pulses haptics per skim / on completion.
    /// </summary>
    public class QuestWaitForSkimNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Counts prisms skimmed (via the skim-boost event, local vessel only, boost-increase filtered) and advances at the target. Shows a live counter on the instruction view.";
        public override string EditorSummary => $"Skim {targetSkims} prisms";

        [Tooltip("How many prisms must be skimmed.")]
        [Min(1)] public int targetSkims = 10;

        [Tooltip("Haptic pulse per counted skim. Default None — the skimmer's own haptics effect already pulses on skim.")]
        public HapticType perSkimHaptic = HapticType.None;

        [Tooltip("Haptic pulse when the target count is reached.")]
        public HapticType successHaptic = HapticType.ButtonPress;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.OnSkimBoost == null)
            {
                Debug.LogError("[Quest] WaitForSkimNode: skim-boost event not wired on runner — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            int count = 0;
            float lastMultiplier = float.MinValue;
            bool done = false;

            ctx.InstructionView?.SetProgress(0, targetSkims);

            void OnBoostChanged(BoostChangedPayload payload)
            {
                if (done) return;

                // Shared global channel: every vessel (and per-frame decay) raises it.
                var localStatus = ctx.GameData?.LocalPlayer?.Vessel?.VesselStatus;
                if (localStatus != null && !ReferenceEquals(payload.VesselStatus, localStatus))
                    return;

                bool increased = payload.BoostMultiplier > lastMultiplier + 0.0001f;
                lastMultiplier = payload.BoostMultiplier;
                if (!increased) return;

                count++;
                ctx.InstructionView?.SetProgress(count, targetSkims);

                if (perSkimHaptic != HapticType.None)
                    HapticController.PlayHaptic(perSkimHaptic);

                if (count >= targetSkims)
                {
                    done = true;
                    if (successHaptic != HapticType.None)
                        HapticController.PlayHaptic(successHaptic);
                    ctx.InstructionView?.SetProgress(0, 0);
                    advance(QuestPorts.Next);
                }
            }

            ctx.OnSkimBoost.OnRaised += OnBoostChanged;
            ctx.AddCleanup(() => ctx.OnSkimBoost.OnRaised -= OnBoostChanged);
        }
    }
}
