using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What an <see cref="AIPilot"/> knows about its line this frame, handed to its boost driver.
    /// Everything a boost decision needs and nothing it does not: the boost driver never steers,
    /// it only decides whether this is a straight worth spending on. See <c>AI/AI_BOOST.md</c>.
    /// </summary>
    public readonly struct AIBoostContext
    {
        /// <summary>World offset from the vessel to the objective the pilot is flying at.</summary>
        public readonly Vector3 ToTarget;

        /// <summary>Direction of TRAVEL (the course; the nose outside a drift).</summary>
        public readonly Vector3 Heading;

        /// <summary>The nose.</summary>
        public readonly Vector3 Forward;

        /// <summary>Current speed, u/s, never negative.</summary>
        public readonly float Speed;

        /// <summary>The stick deflection the pilot last wrote, 0..1 (the larger axis).</summary>
        public readonly float Stick;

        /// <summary>The pilot is flying a break-off away from its objective, not at it.</summary>
        public readonly bool BreakingOrbit;

        /// <summary>The pilot is holding its commit drift (course locked on the objective).</summary>
        public readonly bool CommitDriftHeld;

        /// <summary>The vessel cannot translate right now (a stance, a stop) - never boost.</summary>
        public readonly bool Restricted;

        public AIBoostContext(Vector3 toTarget, Vector3 heading, Vector3 forward, float speed, float stick,
                              bool breakingOrbit, bool commitDriftHeld, bool restricted)
        {
            ToTarget = toTarget;
            Heading = heading;
            Forward = forward;
            Speed = Mathf.Abs(speed);
            Stick = Mathf.Clamp01(stick);
            BreakingOrbit = breakingOrbit;
            CommitDriftHeld = commitDriftHeld;
            Restricted = restricted;
        }

        public float Distance => ToTarget.magnitude;

        /// <summary>Degrees between where the vessel is travelling and where the objective is.</summary>
        public float AngleToTarget => Vector3.Angle(Heading, ToTarget);

        /// <summary>Seconds to the objective at the current speed - the straight still ahead.</summary>
        public float RunwaySeconds => Distance / Mathf.Max(Speed, 1f);
    }

    /// <summary>
    /// A hull's AI BOOST POLICY: when an autopilot spends that hull's speed resource. One subclass
    /// per boost MECHANIC (hold, pellet, drift-charge, skim ring, throttle dash), never per mode -
    /// each reads the hull's own bound ability and the ability's own numbers, so a retune of the
    /// ability retunes the bot with it.
    ///
    /// <para><b>The shared law</b> is the one a human races by and the one the Manta's
    /// <see cref="MantaAnalogTurnBoostExecutor"/> drive and the Rhino ramp already encode: spend on
    /// a STRAIGHT at the objective (course within <see cref="alignDegrees"/>, stick inside
    /// <see cref="stickBand"/>, at least <see cref="engageRunwaySeconds"/> of it left) and stop
    /// spending before the TURN (under <see cref="releaseRunwaySeconds"/> from the objective, where
    /// the next leg starts).</para>
    ///
    /// <para><b>Opt-in, per hull, per mode.</b> A prefab with no policy is untouched. A mode in
    /// <see cref="disabledInModes"/> gets the hull's PRE-POLICY behaviour exactly - the driver never
    /// starts and the ability cycler keeps the entry it would otherwise hand over - so listing a
    /// mode is always a pure revert, never a third behaviour.</para>
    /// </summary>
    public abstract class AIBoostPolicySO : ScriptableObject
    {
        [Header("The straight")]
        [Tooltip("Engage only while the course is within this many degrees of the objective.")]
        [SerializeField, Range(0f, 90f)] float alignDegrees = 12f;
        [Tooltip("A held boost lets go once the course swings past this many degrees (hysteresis " +
                 "above Align Degrees so a small correction does not flicker the boost).")]
        [SerializeField, Range(0f, 120f)] float releaseAlignDegrees = 25f;
        [Tooltip("Engage only while the pilot's stick deflection is under this - the Manta drive's " +
                 "band (MantaAnalogTurnBoostExecutor.aiBoostStickBand), i.e. 'it is flying straight'.")]
        [SerializeField, Range(0f, 1f)] float stickBand = 0.35f;
        [Tooltip("A held boost lets go once the stick goes past this (hysteresis above Stick Band).")]
        [SerializeField, Range(0f, 1f)] float releaseStickBand = 0.6f;

        [Header("Runway")]
        [Tooltip("Seconds of straight (distance to the objective at the CURRENT speed) needed to start " +
                 "spending. Spending into a short leg buys a wide turn, not time.")]
        [SerializeField, Min(0f)] float engageRunwaySeconds = 1.5f;
        [Tooltip("Stop spending inside this many seconds of the objective: the next leg begins there " +
                 "and boosting into an unknown turn is what loses races. Conserve for the next straight.")]
        [SerializeField, Min(0f)] float releaseRunwaySeconds = 0.5f;

        [Header("Where it applies")]
        [Tooltip("Modes in which this policy stands down and the hull flies exactly as it did before " +
                 "the policy existed (e.g. a mode whose score depends on the hull NOT boosting through " +
                 "its objective).")]
        [SerializeField] List<GameModes> disabledInModes = new();
        [Tooltip("Only drive a pilot that is an AI PLAYER - not the menu's lava-lamp autopilot flying " +
                 "the human's own vessel. For a policy whose ability leaves something in the world.")]
        [SerializeField] bool requireAIPlayer;

        public float AlignDegrees => alignDegrees;
        public float EngageRunwaySeconds => engageRunwaySeconds;
        public float ReleaseRunwaySeconds => releaseRunwaySeconds;
        public bool RequireAIPlayer => requireAIPlayer;

        public bool AllowsMode(GameModes mode) => disabledInModes == null || !disabledInModes.Contains(mode);

        /// <summary>A straight worth starting to spend on (before the runway test).</summary>
        public bool IsStraight(in AIBoostContext c) =>
            !c.Restricted && !c.BreakingOrbit && c.AngleToTarget <= alignDegrees && c.Stick <= stickBand;

        /// <summary>Still straight enough to KEEP spending - the wider release band.</summary>
        public bool StillStraight(in AIBoostContext c) =>
            !c.Restricted && !c.BreakingOrbit && c.AngleToTarget <= releaseAlignDegrees && c.Stick <= releaseStickBand;

        /// <summary>At least <paramref name="scale"/> × the engage runway is left.</summary>
        public bool HasRunway(in AIBoostContext c, float scale = 1f) => c.RunwaySeconds >= engageRunwaySeconds * scale;

        /// <summary>The objective, and the turn after it, is close: stop spending.</summary>
        public bool TurnAhead(in AIBoostContext c) => c.RunwaySeconds < releaseRunwaySeconds;

        /// <summary>
        /// Build this pilot's driver, or null when the hull binds none of the ability this policy
        /// drives - a mis-assigned policy is then a quiet no-op, never a broken pilot.
        /// </summary>
        public abstract AIBoostDriver CreateDriver(IVesselStatus status, ActionExecutorRegistry executors);
    }

    /// <summary>
    /// One pilot's live boost state. A policy asset is shared by every AI flying that hull, so all
    /// per-pilot state lives here.
    /// </summary>
    public abstract class AIBoostDriver
    {
        /// <summary>
        /// True for an ability this driver presses itself. <see cref="AIPilot"/>'s blind ability
        /// cycler skips it (the commit-control exclusion's twin): the cycler's StopAction lands on
        /// the same shared state and would cut a boost the driver is holding.
        /// </summary>
        public virtual bool Drives(ShipActionSO action) => false;

        /// <summary>Decide this frame. Called from AIPilot.Update while the autopilot flies.</summary>
        public abstract void Tick(in AIBoostContext context);

        /// <summary>Let go of everything this driver holds. Every path out of autopilot calls it.</summary>
        public abstract void Release();

        /// <summary>While true the pilot must not START a commit drift (the Dolphin is spending
        /// the charge that drift would cancel).</summary>
        public virtual bool HoldsOffCommit => false;

        /// <summary>While true the pilot holds its stick centred (a Squirrel flying the ring it laid).</summary>
        public virtual bool HoldsLine => false;

        /// <summary>One-shot: the driver wants the pilot to let go of its commit drift now.</summary>
        public virtual bool TakeCommitReleaseRequest() => false;
    }
}
