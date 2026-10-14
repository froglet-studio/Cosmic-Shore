using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One omni square's two round-5 turns (Omni Motion Lab, "rigid arcs that hop clear"), MEASURED from the art file's
    /// keys, which are exact rigid turns about the crystal's centre: the pentagon key a 72-degree turn about the
    /// square's pentagon axis, Step1 + Step2 a 120-degree turn about its triangle axis, and Step1 that turn halfway,
    /// lifted (its offset split into a lift straight out and a sideways step).
    ///
    /// Shared by the game's shaped twin (<c>CrystalSpaceSquares</c>, which writes the turns as blend-shape frames) and the
    /// Elemental Converter (<c>OmniCombinePlayer</c>, which poses them every frame), so both turn a square the same way.
    /// JS twin: <c>poseSquares()</c> (pent path 1, tri path 2) in <c>Docs/Studios/OmniMotionLab.html</c>.
    /// </summary>
    public readonly struct SquareTurnFit
    {
        public readonly Vector3 Centre, PentagonAxis, TriangleAxis, Side;
        public readonly float PentagonSign, TriangleSign, Lift;

        SquareTurnFit(Vector3 centre, Vector3 pentagonAxis, float pentagonSign, Vector3 triangleAxis, float triangleSign, float lift, Vector3 side)
        {
            Centre = centre;
            PentagonAxis = pentagonAxis;
            PentagonSign = pentagonSign;
            TriangleAxis = triangleAxis;
            TriangleSign = triangleSign;
            Lift = lift;
            Side = side;
        }

        /// <summary>
        /// Fits the square whose corners are <paramref name="corners"/> (indices into <paramref name="rest"/>, centred on
        /// <paramref name="centre"/>) to its keys: <paramref name="pentagonKey"/>, <paramref name="step1"/> and
        /// <paramref name="step2"/> give each corner's offset under the art file's three keys. Refused, named, when a key
        /// is not a rigid turn within <paramref name="tolerance"/>.
        /// </summary>
        public static bool TryFit(IReadOnlyList<int> corners, Vector3[] rest, Vector3 centre, Func<int, Vector3> pentagonKey,
            Func<int, Vector3> step1, Func<int, Vector3> step2, float tolerance, out SquareTurnFit turn, out string problem)
        {
            turn = default;
            if (!TryFitTurn(corners, rest, pentagonKey, 72f, tolerance, out var pentagonAxis, out float pentagonSign, out problem)
                || !TryFitTurn(corners, rest, i => step1(i) + step2(i), 120f, tolerance, out var triangleAxis, out float triangleSign, out problem))
                return false;
            var half = Quaternion.AngleAxis(triangleSign * 60f, triangleAxis) * centre;
            var stepCentre = Vector3.zero;
            foreach (int i in corners) stepCentre += rest[i] + step1(i);
            stepCentre /= corners.Count;
            var offset = stepCentre - half;
            var outward = half.normalized;
            float lift = Vector3.Dot(offset, outward);
            turn = new SquareTurnFit(centre, pentagonAxis, pentagonSign, triangleAxis, triangleSign, lift, offset - outward * lift);
            return true;
        }

        /// <summary>
        /// The square <paramref name="e"/> (0..1) of the way through its pentagon turn or its triangle turn: a corner at
        /// rest <c>v</c> is at <c>rotation * v + shift</c>. The pentagon turn hops out by <paramref name="pentagonHop"/>
        /// (world units) on a sine; the triangle turn by <paramref name="triangleLift"/> and <paramref name="triangleSide"/>
        /// times the art file's own Step1 lift and sideways step (1, 1 passes exactly through Step1).
        /// </summary>
        public void At(bool pentagon, float e, float pentagonHop, float triangleLift, float triangleSide,
            out Quaternion rotation, out Vector3 shift)
        {
            float hop = Mathf.Sin(Mathf.PI * e);
            if (pentagon)
            {
                rotation = Quaternion.AngleAxis(PentagonSign * 72f * e, PentagonAxis);
                shift = (rotation * Centre).normalized * (pentagonHop * hop);
                return;
            }
            rotation = Quaternion.AngleAxis(TriangleSign * 120f * e, TriangleAxis);
            var sideTurn = Quaternion.AngleAxis(TriangleSign * (120f * e - 60f), TriangleAxis);
            shift = (rotation * Centre).normalized * (triangleLift * Lift * hop) + sideTurn * Side * (triangleSide * hop);
        }

        /// <summary>
        /// The axis (through the crystal's centre) and sense of the rigid turn by <paramref name="degrees"/> that
        /// <paramref name="offset"/> makes of a plate: every corner's offset is perpendicular to the axis, so the axis is
        /// the cross product of the two most different offsets; the sense is whichever reproduces the offsets.
        /// Refused when no sense reproduces every corner within <paramref name="tolerance"/>.
        /// </summary>
        static bool TryFitTurn(IReadOnlyList<int> idx, Vector3[] rest, Func<int, Vector3> offset, float degrees, float tolerance,
            out Vector3 axis, out float sign, out string problem)
        {
            axis = Vector3.zero;
            sign = 1f;
            int a = idx[0];
            foreach (int i in idx) if (offset(i).sqrMagnitude > offset(a).sqrMagnitude) a = i;
            Vector3 best = Vector3.zero;
            foreach (int i in idx)
            {
                var c = Vector3.Cross(offset(a), offset(i));
                if (c.sqrMagnitude > best.sqrMagnitude) best = c;
            }
            if (best.sqrMagnitude < 1e-16f)
            {
                problem = "a square's key does not turn it (its offsets are parallel or zero)";
                return false;
            }
            axis = best.normalized;
            float bestError = float.MaxValue;
            foreach (float s in new[] { 1f, -1f })
            {
                var turn = Quaternion.AngleAxis(s * degrees, axis);
                float error = 0f;
                foreach (int i in idx) error = Mathf.Max(error, (turn * rest[i] - (rest[i] + offset(i))).magnitude);
                if (error < bestError) { bestError = error; sign = s; }
            }
            if (bestError > tolerance)
            {
                problem = $"a square's key is not a rigid {degrees}-degree turn about the crystal's centre (misses by {bestError:G3})";
                return false;
            }
            problem = null;
            return true;
        }
    }
}
