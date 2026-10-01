using System;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a drill condition is allowed to read. The runner fills it from the live vessel
    /// (<c>VesselStatus</c>, <c>InputStatus</c>, the action handler's started events, the
    /// preview's own gate course); a test fills it by hand. Counters are MONOTONIC over the run,
    /// so a condition takes a baseline at <see cref="DrillCondition.Begin"/> and compares, and
    /// nothing has to be reset between steps.
    /// </summary>
    public interface IDrillSignals
    {
        float Speed { get; }

        /// <summary>Throttle input, 0..1 (the transformer's own throttle axis).</summary>
        float Throttle { get; }

        /// <summary>Largest stick deflection this frame, 0..1.</summary>
        float Steer { get; }

        bool IsDrifting { get; }

        int SkimCount { get; }
        int GatesThreaded { get; }
        int PressCount(InputEvents input);

        /// <summary>Presses of the control the hull's <paramref name="element"/> ability is bound to.</summary>
        int AbilityActivationCount(Element element);
    }

    /// <summary>
    /// A condition's per-run state, kept OFF the condition so the authored asset is never written
    /// at runtime (a condition object is shared by every run that reads its template).
    /// </summary>
    public struct DrillConditionState
    {
        public float Elapsed;
        public float Held;
        public int Baseline;
    }

    /// <summary>
    /// What completes a Lesson step, or what a Mentor tip waits for. Polymorphic through
    /// <c>[SerializeReference]</c>; every member is a pure function of the signals, its own
    /// authored numbers and its state.
    /// </summary>
    [Serializable]
    public abstract class DrillCondition
    {
        /// <summary>Take any baseline. Called when the condition becomes the live one.</summary>
        public virtual void Begin(IDrillSignals signals, ref DrillConditionState state) => state = default;

        /// <summary>Advance by <paramref name="dt"/> seconds; true once satisfied.</summary>
        public abstract bool Tick(IDrillSignals signals, float dt, ref DrillConditionState state);

        /// <summary>One line for the authoring tool. Never shown to a player.</summary>
        public abstract string Describe();

        /// <summary>Accumulate held time while <paramref name="holding"/>; a release starts it over.</summary>
        protected static bool Hold(bool holding, float seconds, float dt, ref DrillConditionState state)
        {
            state.Held = holding ? state.Held + dt : 0f;
            return state.Held >= seconds;
        }
    }

    /// <summary>Steer with any stick past a deflection, held for a time.</summary>
    [Serializable]
    public sealed class SteerHeldCondition : DrillCondition
    {
        [Range(0.05f, 1f)] public float MinDeflection = 0.5f;
        [Min(0f)] public float Seconds = 1f;

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            Hold(s.Steer >= MinDeflection, Seconds, dt, ref st);

        public override string Describe() => $"Steer >= {MinDeflection:0.##} for {Seconds:0.##}s";
    }

    /// <summary>Hold the throttle past a fraction, for a time.</summary>
    [Serializable]
    public sealed class ThrottleHeldCondition : DrillCondition
    {
        [Range(0.05f, 1f)] public float MinThrottle = 0.8f;
        [Min(0f)] public float Seconds = 1f;

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            Hold(s.Throttle >= MinThrottle, Seconds, dt, ref st);

        public override string Describe() => $"Throttle >= {MinThrottle:0.##} for {Seconds:0.##}s";
    }

    /// <summary>Reach a speed, in world units per second.</summary>
    [Serializable]
    public sealed class SpeedAtLeastCondition : DrillCondition
    {
        [Min(0f)] public float Speed = 100f;

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) => s.Speed >= Speed;

        public override string Describe() => $"Speed >= {Speed:0}";
    }

    /// <summary>Drift, held for a time.</summary>
    [Serializable]
    public sealed class DriftHeldCondition : DrillCondition
    {
        [Min(0f)] public float Seconds = 0.75f;

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            Hold(s.IsDrifting, Seconds, dt, ref st);

        public override string Describe() => $"Drift for {Seconds:0.##}s";
    }

    /// <summary>Press a specific input event some number of times.</summary>
    [Serializable]
    public sealed class InputPressedCondition : DrillCondition
    {
        public InputEvents Input = InputEvents.Button1Action;
        [Min(1)] public int Count = 1;

        public override void Begin(IDrillSignals s, ref DrillConditionState st)
        {
            st = default;
            st.Baseline = s.PressCount(Input);
        }

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            s.PressCount(Input) - st.Baseline >= Count;

        public override string Describe() => $"Press {Input} x{Count}";
    }

    /// <summary>
    /// Use the hull's ability for an element - resolved through the hull's ability map, so the
    /// same authored step means the right control on every ship.
    /// </summary>
    [Serializable]
    public sealed class AbilityActivatedCondition : DrillCondition
    {
        public Element Element = Element.Time;
        [Min(1)] public int Count = 1;

        public override void Begin(IDrillSignals s, ref DrillConditionState st)
        {
            st = default;
            st.Baseline = s.AbilityActivationCount(Element);
        }

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            s.AbilityActivationCount(Element) - st.Baseline >= Count;

        public override string Describe() => $"Use the {Element} ability x{Count}";
    }

    /// <summary>Skim some number of prisms.</summary>
    [Serializable]
    public sealed class SkimsCondition : DrillCondition
    {
        [Min(1)] public int Count = 3;

        public override void Begin(IDrillSignals s, ref DrillConditionState st)
        {
            st = default;
            st.Baseline = s.SkimCount;
        }

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            s.SkimCount - st.Baseline >= Count;

        public override string Describe() => $"Skim x{Count}";
    }

    /// <summary>Thread some number of the preview's gates.</summary>
    [Serializable]
    public sealed class GatesThreadedCondition : DrillCondition
    {
        [Min(1)] public int Count = 1;

        public override void Begin(IDrillSignals s, ref DrillConditionState st)
        {
            st = default;
            st.Baseline = s.GatesThreaded;
        }

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st) =>
            s.GatesThreaded - st.Baseline >= Count;

        public override string Describe() => $"Thread {Count} gate(s)";
    }

    /// <summary>Let time pass (unscaled - the preview runs beside a menu free to touch timeScale).</summary>
    [Serializable]
    public sealed class TimerCondition : DrillCondition
    {
        [Min(0f)] public float Seconds = 3f;

        public override bool Tick(IDrillSignals s, float dt, ref DrillConditionState st)
        {
            st.Elapsed += dt;
            return st.Elapsed >= Seconds;
        }

        public override string Describe() => $"Wait {Seconds:0.##}s";
    }
}
