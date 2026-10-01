using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a hull is flown, named by how many thumbsticks it takes. Picks which shared flight
    /// block the Lesson teaches AND which account key records having learned it
    /// (Docs/ModePreview/TRAINING_PLAN.md §3), so the two can never disagree. Read off the live
    /// vessel's <c>IVesselStatus.IsSingleStickControls</c>.
    /// </summary>
    public enum FlightScheme
    {
        TwoThumb = 0,
        OneThumb = 1,
    }

    /// <summary>Where a beat's prompt points.</summary>
    public enum DrillAnchor
    {
        Window = 0,
        AbilityRow = 1,
        ObjectiveArrow = 2,
        NextGate = 3,
    }

    /// <summary>What the coach does besides speaking while a beat is up.</summary>
    public enum DrillCue
    {
        None = 0,
        PulseAbilityRow = 1,
        HighlightNextGate = 2,
        /// <summary>Reserved: the preview's autopilot flies the step once first (TRAINING_PLAN §10, "later").</summary>
        GhostDemo = 3,
    }

    /// <summary>
    /// When a Lesson step belongs in a hull's Lesson at all. A step that does not apply is
    /// dropped by the composer, never shown as an instruction the hull cannot follow.
    /// </summary>
    public enum DrillApplicability
    {
        Always = 0,
        /// <summary>Only on a hull with a bound drift action (D8: drift stays in the Lesson on ships that have it).</summary>
        HullDrifts = 1,
        /// <summary>Only when the step's <see cref="DrillBeat.Element"/> ability exists AND is bound to a control.</summary>
        AbilityHasInput = 2,
    }

    /// <summary>The Mentor's ordering tiers: basics of this ship, how to win this mode, how to win it faster.</summary>
    public enum MentorTier
    {
        ShipBasics = 0,
        WinTheMode = 1,
        WinFaster = 2,
    }

    /// <summary>
    /// One thing the coach says. Every word is authored (CLAUDE.md § Code Style: player-facing
    /// text has a human-facing control) - the prompt is a TEMPLATE whose tokens
    /// (<see cref="DrillTokens"/>) fill in the facts, never a C# literal.
    /// </summary>
    [Serializable]
    public abstract class DrillBeat
    {
        [Tooltip("Stable id. Used to suppress a step per hull and to remember which tips a player has " +
                 "seen, so renaming it resets that memory. Lower-case, no spaces.")]
        public string Id;

        [Tooltip("What the coach says. Tokens: {vessel} {mode} {ability:Time} {abilityDescription:Time} " +
                 "{glyph:Time} {glyph:Steer} {glyph:Throttle} {glyph:Drift}. ASCII only - the UI font has no " +
                 "other glyphs. Empty = this beat says nothing (that is how a line is deleted).")]
        [TextArea(2, 4)] public string Prompt;

        [Tooltip("The element this beat is about, or None. Picks the ability row to pulse and the control " +
                 "chip to draw, and is what AbilityHasInput tests. None = no chip, no row pulse.")]
        public Element Element = Element.None;

        public DrillAnchor Anchor = DrillAnchor.Window;
        public DrillCue Cue = DrillCue.None;
    }

    /// <summary>
    /// A Lesson step: a prompt plus the condition that completes it. Only the active step's
    /// condition is live (TRAINING_PLAN §4.3).
    /// </summary>
    [Serializable]
    public sealed class LessonStep : DrillBeat
    {
        public DrillApplicability Applicability = DrillApplicability.Always;

        [Tooltip("What completes this step. Empty = a step that completes at once (a pure caption).")]
        [SerializeReference] public DrillCondition Condition;

        [Tooltip("Seconds before the hint replaces the prompt. 0 = the library's default.")]
        [Min(0f)] public float HintAfterSeconds;

        [Tooltip("Shown if the player has not completed the step after HintAfterSeconds. Same tokens as the " +
                 "prompt. Empty = no hint.")]
        [TextArea(2, 4)] public string HintPrompt;
    }

    /// <summary>
    /// A Mentor tip: said in authored order, for its dwell time, optionally waiting for a
    /// good MOMENT (which changes WHEN it is said, never WHICH - D3).
    /// </summary>
    [Serializable]
    public sealed class MentorTip : DrillBeat
    {
        public MentorTier Tier = MentorTier.ShipBasics;

        [Tooltip("Order inside the tier: lower first.")]
        public int Priority;

        [Tooltip("Seconds on screen. 0 = the library's default.")]
        [Min(0f)] public float DwellSeconds;

        [Tooltip("Optional: wait for this before saying the tip. A moment that does not come within the " +
                 "library's timeout lets the tip go anyway.")]
        [SerializeReference] public DrillCondition Moment;
    }
}
