using System.Collections;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Forces the menu autopilot vessel into freestyle (player-controlled) mode by driving
    /// <see cref="Gameplay.MenuCrystalClickHandler.ToggleTransition"/>. Advances when the
    /// menu→freestyle camera/UI blend completes (<c>OnGameStateTransitionEnd</c>).
    ///
    /// The runner only starts the FTUE after the vessel is spawned (OnClientReady), so the
    /// toggle's readiness guards pass; a bounded retry + timeout keeps the flow from hanging
    /// if the very first toggle is dropped.
    /// </summary>
    public class QuestEnterFreestyleNode : QuestNodeSO
    {
        public override QuestNodeCategory Category => QuestNodeCategory.Gameplay;
        public override string TypeTooltip => "Forces the menu vessel from autopilot into freestyle (player-controlled) flight; advances when the camera/UI blend completes.";
        public override string EditorSummary => "Autopilot → player control";

        /// <summary>The door INTO gameplay — starts a gameplay row in the editor layout.</summary>
        public override QuestVenue Venue => QuestVenue.Gameplay;

        [Tooltip("Safety timeout (seconds). If the transition never completes, advance anyway so the FTUE never hard-locks.")]
        [Min(1f)] public float timeoutSeconds = 8f;

        public override IEnumerator Execute(QuestRuntimeContext ctx, System.Action<string> advance)
        {
            if (ctx.CrystalHandler == null || ctx.FreestyleEvents == null)
            {
                Debug.LogError("[Quest] EnterFreestyleNode: MenuCrystalClickHandler / freestyle events not wired — skipping.");
                advance(QuestPorts.Next);
                yield break;
            }

            // Already there — nothing to do.
            if (ctx.CrystalHandler.IsInFreestyle)
            {
                ctx.BeginFlightTraining();
                advance(QuestPorts.Next);
                yield break;
            }

            bool done = false;
            void OnEnterComplete()
            {
                done = true;
                ctx.BeginFlightTraining(); // HUD off, action buttons gated — flight school is live
                advance(QuestPorts.Next);
            }

            ctx.FreestyleEvents.OnGameStateTransitionEnd.OnRaised += OnEnterComplete;
            ctx.AddCleanup(() => ctx.FreestyleEvents.OnGameStateTransitionEnd.OnRaised -= OnEnterComplete);

            ctx.CrystalHandler.ToggleTransition();

            float elapsed = 0f, retry = 0f;
            while (!done && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;

                // If the toggle was ignored (vessel not quite ready), the handler stays out of
                // freestyle and is not transitioning — nudge it again. Once a transition starts,
                // IsInFreestyle flips true immediately and further toggles are no-ops.
                if (!ctx.CrystalHandler.IsInFreestyle)
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
                Debug.LogWarning("[Quest] EnterFreestyleNode: transition did not complete in time — advancing.");
                ctx.BeginFlightTraining();
                advance(QuestPorts.Next);
            }
        }
    }
}
