using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Returns the player from freestyle to the menu. Two modes:
    ///  • Passive (default): waits for the player to trigger the return themselves.
    ///  • Force Exit: drives the transition programmatically — place it AFTER a gate
    ///    (e.g. WaitForInput on the B button) so the exit happens exactly when the
    ///    player performs the taught action, and never before.
    /// Advances when the freestyle→menu blend completes.
    /// </summary>
    public class QuestExitFreestyleNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gate;
        public override string TypeTooltip => "Passive: waits for the player to return to the menu. Force Exit: drives the return itself (pair with a WaitForInput gate, e.g. 'press B'). Advances when the menu blend completes.";
        public override string EditorSummary => forceExit ? "Force return to menu" : "Until player returns to menu";

        /// <summary>The door BACK to the app shell — starts an app-shell row in the editor layout.</summary>
        public override QuestVenue Venue => QuestVenue.AppShell;

        [Tooltip("Drive the freestyle→menu transition programmatically instead of waiting for the player's own back action.")]
        public bool forceExit;

        [Tooltip("Force-exit only: safety timeout (seconds) before advancing anyway.")]
        [Min(1f)] public float timeoutSeconds = 8f;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.FreestyleEvents == null)
            {
                Debug.LogError("[Quest] ExitFreestyleNode: freestyle events not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            // Already back in the menu — don't wait for an event that won't come.
            if (ctx.CrystalHandler != null && !ctx.CrystalHandler.IsInFreestyle)
            {
                ctx.EndFlightTraining();
                ctx.InstructionView?.Hide();
                advance(QuestPorts.Next);
                yield break;
            }

            bool done = false;

            // The instruction overlay fades the moment the player TAPS the volume button
            // (transition START) — not seconds later when the menu blend completes.
            void OnMenuStart() => ctx.InstructionView?.Hide();

            void OnMenuComplete()
            {
                done = true;
                ctx.EndFlightTraining();     // release the action-button gate
                ctx.InstructionView?.Hide(); // safety: clear the overlay even if Start never fired
                advance(QuestPorts.Next);
            }

            ctx.FreestyleEvents.OnMenuStateTransitionStart.OnRaised += OnMenuStart;
            ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised += OnMenuComplete;
            ctx.AddCleanup(() =>
            {
                ctx.FreestyleEvents.OnMenuStateTransitionStart.OnRaised -= OnMenuStart;
                ctx.FreestyleEvents.OnMenuStateTransitionEnd.OnRaised -= OnMenuComplete;
            });

            if (!forceExit || ctx.CrystalHandler == null)
                yield break; // passive: the player triggers the return themselves

            ctx.CrystalHandler.ToggleTransition();

            float elapsed = 0f, retry = 0f;
            while (!done && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                // If the toggle was swallowed (mid-transition guard), nudge again.
                if (ctx.CrystalHandler.IsInFreestyle)
                {
                    retry += Time.unscaledDeltaTime;
                    if (retry >= 0.75f)
                    {
                        retry = 0f;
                        ctx.CrystalHandler.ToggleTransition();
                    }
                }

                yield return null;
            }

            if (!done)
            {
                Debug.LogWarning("[Quest] ExitFreestyleNode: forced exit did not complete in time — advancing.");
                ctx.EndFlightTraining();
                ctx.InstructionView?.Hide();
                advance(QuestPorts.Next);
            }
        }
    }
}
