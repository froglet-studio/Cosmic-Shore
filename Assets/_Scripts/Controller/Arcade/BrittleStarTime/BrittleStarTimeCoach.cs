using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's race coach for an AI Brittle Star (2026-11-16): when to hold Speed Mode. Pure, so the
    /// controller, the tests and the offline harness (brittle_star_time_course_harness --race) ask one rule.
    ///
    /// <para><b>Why the mode coaches the wish.</b> The hull's own Speed law (BrittleStarAIBoostPolicy) lets go 0.5 s before
    /// EVERY ring and engages only 1.5 s from the next, whatever the corner: on these short legs it spent most of a race in
    /// Creation Mode at 22.5 u/s (240 s at I1 offline, 0 of 2 recorded AI races finished). The coach reads the course
    /// instead (<see cref="BrittleStarTimeLine"/>): hold Speed while the nose is on the line, let go at each ring's let-go
    /// gate, light it again once the nose comes round. It acts only through the hull's own autopilot wish
    /// (<c>BrittleStarExecutor.SetAutopilotSpeedWish</c>), which presses the same replicated control a person does: input
    /// only, and the vessel is untouched.</para>
    ///
    /// <para><b>Skill.</b> Each difficulty lifts at a different nose angle and lets go a little early or late, and every
    /// seat errs by its own fixed amount per ring (<see cref="LetGoError"/>), so rivals differ and stay beatable.</para>
    /// </summary>
    public static class BrittleStarTimeCoach
    {
        /// <summary>The seat's difficulty, Easy..Legendary (<c>GameDataSO.AIDifficultyFor</c> 1..4).</summary>
        public enum Skill { Easy = 0, Medium = 1, Hard = 2, Legendary = 3 }

        public static Skill SkillFor(int difficulty) => (Skill)Mathf.Clamp(difficulty - 1, 0, 3);

        /// <summary>Speed Mode is lit while the nose is within this of the line, per skill.</summary>
        public static readonly float[] LightDegrees = { 20f, 30f, 40f, 45f };

        /// <summary>...and let go once the nose is past this (hysteresis above <see cref="LightDegrees"/>).</summary>
        public static readonly float[] DropDegrees = { 35f, 50f, 65f, 75f };

        /// <summary>The share of a corner's speed (<see cref="BrittleStarTimeLine.CornerSpeed"/>) the coach trusts, per
        /// intensity (rows I1..I4) and skill (Easy..Legendary), searched with <see cref="NoseFollowByIntensity"/>.
        /// The line is cut for a person: no nose lag, and Speed Roll at I4. An autopilot never rolls and its nose trails
        /// its command, so it takes every corner slower than the line's, and an Easy one slower still.</summary>
        public static readonly float[][] CornerShareByIntensity =
        {
            new[] { 0.5f, 0.5f, 0.5f, 0.9f },
            new[] { 0.5f, 0.5f, 0.6f, 0.6f },
            new[] { 0.7f, 0.8f, 0.9f, 0.9f },
            new[] { 0.8f, 0.8f, 0.9f, 0.9f },
        };

        public static float CornerShareFor(int intensity, Skill skill) =>
            CornerShareByIntensity[Mathf.Clamp(intensity, 1, 4) - 1][(int)skill];

        /// <summary>Each seat's own let-go error, +/- u per ring, per skill.</summary>
        public static readonly float[] LetGoErrorU = { 40f, 30f, 20f, 10f };

        /// <summary>The AI nose follow rate per second the mode sets on each seat, per intensity (rows I1..I4) and skill
        /// (Easy..Legendary); the hull's default is 1.5 (VesselTransformer.RotationFollowRate). A mode setting on the AI's
        /// own steering, as Redline and Switchback set theirs; the vessel is untouched. Searched offline
        /// (`brittle_star_time_course_harness --tune`) so each skill races at a fixed pace against a person who reads the
        /// course - Easy 2x, Medium 1.5x, Hard 1.25x, Legendary 1.05x the model's reader - on EVERY rung, rather than
        /// growing softer as the rungs grow harder. The search pairs it with <see cref="CornerShareByIntensity"/>, so a
        /// row need not rise with skill: a Hard I3 seat trusts the hairpin more and needs less nose. I4's Legendary
        /// tops out at 1.23x (the coil, unrolled, at 8/s); I1's Easy and Medium fly slower than the fleet's 1.5/s. The Legendary column (13.66 at I1-I2, 15.04 at
        /// I3-I4) is BrittleStar run1, searched in the real game (Amoebius judge, time_ai_search.py, 2026-11-17).</summary>
        public static readonly float[][] NoseFollowByIntensity =
        {
            new[] { 0.90f, 1.10f, 4.51f, 13.66f },
            new[] { 1.59f, 4.44f, 2.71f, 13.66f },
            new[] { 2.16f, 8.00f, 1.44f, 15.04f },
            new[] { 4.39f, 8.00f, 8.00f, 15.04f },
        };

        public static float NoseFollowFor(int intensity, Skill skill) =>
            NoseFollowByIntensity[Mathf.Clamp(intensity, 1, 4) - 1][(int)skill];

        /// <summary>A fixed per-seat, per-ring error in [-1, 1] (an integer hash, no randomness).</summary>
        public static float Hash11(int seat, int ring)
        {
            unchecked
            {
                uint h = (uint)(seat * 73856093) ^ (uint)(ring * 19349663) ^ 0x9E3779B9u;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }

        /// <summary>Seat <paramref name="seat"/>'s let-go error at ring <paramref name="ring"/>, u.</summary>
        public static float LetGoError(int seat, int ring, Skill skill) => Hash11(seat, ring) * LetGoErrorU[(int)skill];

        /// <summary>Where this seat lets go of Speed Mode before ring <paramref name="ring"/>, u (0 = holds it through): the
        /// line's let-go rule (<see cref="BrittleStarTimeLine.LetGoDistance"/>) on the corner speed this skill trusts,
        /// plus the seat's own error.</summary>
        public static float LetGoFor(IReadOnlyList<RaceGate> lap, int ring, float topSpeed, int seat, Skill skill,
                                     int intensity)
        {
            float corner = BrittleStarTimeLine.CornerSpeed(lap, ring, topSpeed) * CornerShareFor(intensity, skill);
            float d = BrittleStarTimeLine.LetGoFrom(topSpeed, corner);
            return d <= 0f ? 0f : Mathf.Max(0f, d + LetGoError(seat, ring, skill));
        }

        /// <summary>
        /// Does an AI at <paramref name="pos"/> heading <paramref name="forward"/>, flying ring <paramref name="ring"/>,
        /// want Speed Mode? <paramref name="lit"/> is whether it holds Speed now (the hysteresis).
        /// </summary>
        public static bool WantsSpeed(IReadOnlyList<RaceGate> lap, int ring, Vector3 pos, Vector3 forward, float topSpeed,
                                      int seat, Skill skill, int intensity, bool lit)
        {
            if (lap == null || lap.Count == 0) return false;
            ring = ((ring % lap.Count) + lap.Count) % lap.Count;
            Vector3 aim = BrittleStarTimeLine.SteerPoint(lap, ring, pos);
            float off = RaceCourseGeometry.Angle(forward, aim - pos);
            if (off > (lit ? DropDegrees[(int)skill] : LightDegrees[(int)skill])) return false;
            float letGo = LetGoFor(lap, ring, topSpeed, seat, skill, intensity);
            if (letGo <= 0f) return true;
            // Inside the let-go gate and still on the near side of the ring: coast in.
            var g = lap[ring];
            Vector3 flow = BrittleStarTimeLine.Flow(lap, ring);
            bool approaching = Vector3.Dot(pos - g.Position, flow) < 0f;
            return !(approaching && (g.Position - pos).magnitude <= letGo);
        }
    }
}
