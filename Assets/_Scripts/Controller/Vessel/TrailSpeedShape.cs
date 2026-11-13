using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A trail prism shaped by how the vessel is flying, never by what is near it: X (one rail's width) is
    /// uniform, Z stretches toward Soar speed, Y grows at a crawl and in a held turn, and in a turn the inside
    /// rail shortens while the outside rail lengthens and thins to meet its neighbours.
    ///
    /// <para>This is the "Free curves" rule Caleb picked in the Manta Trail Lab
    /// (https://claude.ai/artifact/UcrAbovUrX7NPCx1mLoB9k, <c>Docs/Studios/MantaTrailLab.html</c> <c>shapeFor('free')</c>,
    /// <c>Docs/Manta/POLISH.md</c>), ported line for line. Each field names the lab key it came from.
    /// <c>TrailSpeedShapeTests</c> pins the lab's numbers. Off by default, so a vessel that never authors
    /// it lays exactly what it did before.</para>
    ///
    /// <para>It replaces the environment sizing the Manta used to run (<c>ScoutTrailPrismScaler</c>, which
    /// widened the trail from a probe of nearby prisms). Pure: no Unity state is read here.</para>
    /// </summary>
    [Serializable]
    public class TrailSpeedShape
    {
        [Tooltip("On: every two-rail prism this controller lays takes its X, Y and Z from speed and turning " +
                 "(the Manta's trail). Off: the BaseScale lay, unchanged.")]
        public bool Enabled;

        [Header("Width")]
        [Tooltip("One rail's width (m), the same at every speed. Lab: shRailW.")]
        [Min(0.5f)] public float RailWidth = 8f;

        [Header("Length (Z)")]
        [Tooltip("At or below this speed (u/s) a prism keeps BaseScale.z; above it, Z grows. Lab: shVRef.")]
        public float ReferenceSpeed = 162f;
        [Tooltip("The speed (u/s) where Z reaches Soar Length. Lab: shFreeV1.")]
        public float SoarSpeed = 600f;
        [Tooltip("Z (m) at Soar Speed and above. Lab: shFreeZMax.")]
        public float SoarLength = 10f;
        [Tooltip("Longest a prism may be, as a fraction of the gap to the next prism on its rail (1 = end to end). " +
                 "Below 1 neighbours never touch. Lab: shFill.")]
        [Min(0f)] public float MaxLengthOfSpacing = 0.9f;
        [Tooltip("On: the length is capped against the lay spacing (which stretches above Soar). Off: against " +
                 "the wavelength. Lab: shFrameAware.")]
        public bool FrameAwareSpacing = true;

        [Header("Stretch (above Soar)")]
        [Tooltip("The speed (u/s) where the stretch is complete: prisms furthest apart, longest and thinnest. " +
                 "Time 10 with both triggers is about 1080. Lab: shTopV.")]
        public float TopSpeed = 1100f;
        [Tooltip("At Top Speed the gap between prisms is the wavelength times this, so production falls behind the " +
                 "vessel. 1 = no stretch. Lab: shTopSp.")]
        [Min(1f)] public float TopSpacingMultiplier = 3f;
        [Tooltip("Z (m) at Top Speed, still under Max Length Of Spacing of the stretched gap. Lab: shTopZ.")]
        public float TopLength = 20f;
        [Tooltip("Y multiplier at Top Speed (x the cruise height). Lab: shTopY.")]
        [Range(0.05f, 1f)] public float TopHeightMultiplier = 0.4f;

        [Header("Height (Y)")]
        [Tooltip("Below this speed (u/s) prisms grow taller, fully so at a quarter of it. Lab: shFreeVLow.")]
        public float CrawlSpeed = 60f;
        [Tooltip("Y multiplier at a crawl (x BaseScale.y). Lab: shFreeYLow.")]
        [Min(0f)] public float CrawlHeightMultiplier = 4f;
        [Tooltip("Extra height (x) at a full single-trigger Yawstery turn. Lab: shYawY.")]
        [Min(0f)] public float YawsteryHeightGain = 0.8f;
        [Tooltip("The outside rail of a turn loses this fraction of its height at a full turn. Lab: shThin.")]
        [Range(0f, 1f)] public float OutsideThin = 0.25f;
        [Tooltip("Thinnest prism (m). Lab: shYMin.")]
        [Min(0.01f)] public float MinHeight = 0.1f;
        [Tooltip("Tallest prism (m). Lab: shYMax.")]
        [Min(0.01f)] public float MaxHeight = 3.5f;

        [Header("Turns")]
        [Tooltip("How much the inside rail shortens and the outside lengthens by the turn's geometry " +
                 "(1 = exactly the ratio of each rail's arc to the vessel's). Lab: shArc.")]
        [Range(0f, 1f)] public float TurnGeometry = 1f;

        /// <summary>Hermite smoothstep of <paramref name="v"/> between <paramref name="a"/> and <paramref name="b"/>
        /// (the lab's <c>smooth</c>; not <see cref="Mathf.SmoothStep"/>, which interpolates between two values).</summary>
        public static float Smooth(float a, float b, float v)
        {
            if (Mathf.Approximately(a, b)) return v >= b ? 1f : 0f;
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// How much longer (above 1) or shorter (below 1) this rail's arc is than the vessel's own path in a turn.
        /// </summary>
        /// <param name="speed">Vessel speed, u/s.</param>
        /// <param name="yawRate">Yaw rate in rad/s, positive turning right (toward the vessel's +right).</param>
        /// <param name="side">+1 the right rail, -1 the left.</param>
        /// <param name="railCentreOffset">Distance from the vessel's path to the rail's centre line, m.</param>
        public static float ArcFactor(float speed, float yawRate, int side, float railCentreOffset)
        {
            if (Mathf.Abs(yawRate) < 1e-3f || speed < 1f) return 1f;
            float radius = speed / Mathf.Abs(yawRate);
            bool inside = (int)Mathf.Sign(yawRate) == side;
            return Mathf.Clamp((radius + (inside ? -1f : 1f) * railCentreOffset) / radius, 0.2f, 3f);
        }

        /// <summary>
        /// How far apart prisms go on one rail at this speed: the wavelength up to Soar, then stretching to
        /// <see cref="TopSpacingMultiplier"/> times it at <see cref="TopSpeed"/>, so at the top production falls
        /// behind the vessel.
        /// </summary>
        public float LaySpacing(float wavelength, float speed) =>
            wavelength * Mathf.Lerp(1f, TopSpacingMultiplier, Smooth(SoarSpeed, TopSpeed, speed));

        /// <summary>
        /// The speed shape's lay pacing: lay by distance flown, not by a timer. Add this frame's flight with
        /// <see cref="StepLay"/>, then keep calling <see cref="NextLay"/> for further prisms the same frame owes.
        /// A timer (<c>UniTask.Delay(wavelength / speed)</c>) ends on a frame, so it laid up to twice the
        /// wavelength apart and flipped as the frame time wobbled; this lays every prism exactly one spacing from
        /// the last, at any frame rate, through a hitch included.
        /// </summary>
        /// <param name="travelled">Distance flown since the last prism; carried between frames. Pass
        /// <see cref="float.PositiveInfinity"/> to lay at once (the first prism after the trail starts).</param>
        /// <param name="step">Distance flown this frame.</param>
        /// <param name="wavelength">The distance between prisms on one rail (<see cref="LaySpacing"/>).</param>
        /// <param name="back">At a lay: how far behind the vessel, back along its path, this prism goes.</param>
        /// <returns>True when a prism is laid.</returns>
        public static bool StepLay(ref float travelled, float step, float wavelength, out float back)
        {
            back = 0f;
            if (float.IsPositiveInfinity(travelled))
            {
                travelled = 0f;
                return true;
            }
            travelled += Mathf.Max(0f, step);
            return NextLay(ref travelled, wavelength, out back);
        }

        /// <summary>One more prism if the flight already counted owes it (a frame that flew past more than one
        /// spacing point): it goes at the next point along the path, <paramref name="back"/> behind the vessel.</summary>
        public static bool NextLay(ref float travelled, float wavelength, out float back)
        {
            back = 0f;
            wavelength = Mathf.Max(1e-3f, wavelength);
            if (travelled < wavelength) return false;
            back = travelled - wavelength;
            travelled = back;
            return true;
        }

        /// <summary>
        /// One rail's prism scale (x, y, z) for this moment of flight.
        /// </summary>
        /// <param name="baseHeight">BaseScale.y, the cruise height.</param>
        /// <param name="baseLength">BaseScale.z, the cruise length.</param>
        /// <param name="halfGap">Half the gap between the rails (|halfGap| at the lay), m.</param>
        /// <param name="speed">Vessel speed, u/s.</param>
        /// <param name="turn01">Turn intensity 0..1 (the net trigger, as SetTurnTrail receives it).</param>
        /// <param name="yawRate">Yaw rate, rad/s, positive turning right.</param>
        /// <param name="side">+1 the right rail, -1 the left.</param>
        /// <param name="spacing">The lay spacing on this rail along the vessel's path, m (<see cref="LaySpacing"/>).</param>
        public Vector3 Evaluate(float baseHeight, float baseLength, float halfGap, float speed, float turn01,
            float yawRate, int side, float spacing)
        {
            turn01 = Mathf.Clamp01(turn01);
            float railCentre = Mathf.Abs(halfGap) + RailWidth * 0.5f;
            float f = Mathf.Lerp(1f, ArcFactor(speed, yawRate, side, railCentre), TurnGeometry);
            float cap = spacing * MaxLengthOfSpacing * f;

            // Up to Soar the prism grows to Soar Length; above it the whole trail stretches toward Top Speed: longer,
            // thinner, and (LaySpacing) further apart, always under Max Length Of Spacing of the gap.
            float top = Smooth(SoarSpeed, TopSpeed, speed);
            float z = Mathf.Lerp(Mathf.Lerp(baseLength, SoarLength, Smooth(ReferenceSpeed, SoarSpeed, speed)),
                TopLength, top) * f;
            float y = baseHeight
                      * Mathf.Lerp(1f, CrawlHeightMultiplier, 1f - Smooth(CrawlSpeed * 0.25f, CrawlSpeed, speed))
                      * Mathf.Lerp(1f, TopHeightMultiplier, top)
                      * (1f + YawsteryHeightGain * turn01);
            if (f > 1f) y *= 1f - OutsideThin * turn01;

            return new Vector3(RailWidth, Mathf.Clamp(y, MinHeight, MaxHeight), Mathf.Min(z, cap));
        }
    }
}
