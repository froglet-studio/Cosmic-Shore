using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>How a ghost demo works the control its step teaches.</summary>
    public enum GhostDemoPress
    {
        /// <summary>No button: the autopilot's own flying is the demonstration (steer, throttle, speed, rings).</summary>
        None = 0,
        /// <summary>Press and let go once (an ability, a button).</summary>
        Tap = 1,
        /// <summary>Hold for most of the demo (drift).</summary>
        Hold = 2,
    }

    /// <summary>The control one ghost demo presses, and how.</summary>
    public readonly struct GhostDemoPlan
    {
        public readonly GhostDemoPress Kind;
        public readonly InputEvents Input;

        public GhostDemoPlan(GhostDemoPress kind, InputEvents input)
        {
            Kind = kind;
            Input = input;
        }

        public bool HasPress => Kind != GhostDemoPress.None;
    }

    /// <summary>
    /// The decisions behind a <see cref="DrillCue.GhostDemo"/> step (TRAINING_PLAN §10.7): the
    /// preview's own autopilot flies the step once, pressing the control the step teaches, and
    /// then hands the stick to the player. Pure - the runner presses, the session hands over.
    ///
    /// <para><b>Input-only.</b> The demo presses what a pilot could press, through the vessel's
    /// action handler, and nothing else (Docs/claude/FTUE_DIALOGUE_AI.md, the AI rules).</para>
    /// </summary>
    public static class DrillGhostDemo
    {
        /// <summary>
        /// The control a step's condition is about, on this hull. A step whose control the hull
        /// does not have gets no press: the autopilot still flies the demo, it just does not
        /// press a button the ship lacks.
        /// </summary>
        /// <param name="map">The hull's ability map; resolves an ability step to its button.</param>
        /// <param name="canDrift">The hull has a bound drift action.</param>
        /// <param name="driftInput">That action's input event.</param>
        public static GhostDemoPlan PlanFor(DrillCondition condition, ElementalAbilityMapSO map,
                                            bool canDrift, InputEvents driftInput)
        {
            switch (condition)
            {
                case DriftHeldCondition:
                    return canDrift ? new GhostDemoPlan(GhostDemoPress.Hold, driftInput) : default;

                case AbilityActivatedCondition ability:
                {
                    // FullSpeedStraightAction is the enum's zero and the "no button" sentinel a
                    // passive entry is authored with (DrillHullFacts.AbilityHasInput reads it the same way).
                    var entry = map != null ? map.GetEntry(ability.Element) : null;
                    return entry != null && entry.Input != InputEvents.FullSpeedStraightAction
                        ? new GhostDemoPlan(GhostDemoPress.Tap, entry.Input)
                        : default;
                }

                case InputPressedCondition pressed:
                    return new GhostDemoPlan(GhostDemoPress.Tap, pressed.Input);

                default:
                    return default;
            }
        }

        /// <summary>
        /// Whether the demo holds its control down <paramref name="elapsed"/> seconds into a demo
        /// lasting <paramref name="duration"/>. The press waits <paramref name="leadIn"/> so the
        /// player sees the ship before it acts, and a hold lets go the same lead-in before the end
        /// so the hand-over never starts mid-drift.
        /// </summary>
        public static bool IsPressed(in GhostDemoPlan plan, float elapsed, float duration,
                                     float leadIn, float tapSeconds)
        {
            if (elapsed < leadIn) return false;
            return plan.Kind switch
            {
                GhostDemoPress.Tap => elapsed < leadIn + tapSeconds,
                GhostDemoPress.Hold => elapsed < duration - leadIn,
                _ => false,
            };
        }
    }
}
