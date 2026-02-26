using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Geometry Utils Tests — Validates math utility functions.
    ///
    /// WHY THIS MATTERS:
    /// GeometryUtils provides core math for distance calculations and vector clamping
    /// used throughout the game (prism positioning, trail distance checks, collision
    /// proximity). If these math functions are wrong, gameplay systems that depend
    /// on spatial queries will behave unpredictably.
    /// </summary>
    [TestFixture]
    public class GeometryUtilsTests
    {
        #region LineData Construction

        [Test]
        public void LineData_Constructor_SetsStart()
        {
            var line = new GeometryUtils.LineData(
                new Vector3(1, 2, 3),
                new Vector3(4, 5, 6)
            );

            Assert.AreEqual(new Vector3(1, 2, 3), line.Start);
        }

        [Test]
        public void LineData_Constructor_DirectionIsNormalized()
        {
            var line = new GeometryUtils.LineData(
                Vector3.zero,
                new Vector3(10, 0, 0)
            );

            Assert.AreEqual(1f, line.Direction.magnitude, 0.0001f,
                "Direction should be a unit vector.");
            Assert.AreEqual(Vector3.right, line.Direction);
        }

        [Test]
        public void LineData_Constructor_MagnitudeIsDistanceBetweenPoints()
        {
            var start = new Vector3(0, 0, 0);
            var end = new Vector3(3, 4, 0);

            var line = new GeometryUtils.LineData(start, end);

            Assert.AreEqual(5f, line.Magnitude, 0.0001f,
                "Magnitude should be the distance between start and end (3-4-5 triangle).");
        }

        #endregion

        #region DistanceFromPointToLine

        [Test]
        public void DistanceFromPointToLine_PointOnLine_ReturnsZero()
        {
            var line = GeometryUtils.PrecomputeLineData(
                Vector3.zero,
                new Vector3(10, 0, 0)
            );

            float distance = GeometryUtils.DistanceFromPointToLine(
                new Vector3(5, 0, 0), line
            );

            Assert.AreEqual(0f, distance, 0.0001f);
        }

        [Test]
        public void DistanceFromPointToLine_PerpendicularPoint_ReturnsPerpendicularDistance()
        {
            var line = GeometryUtils.PrecomputeLineData(
                Vector3.zero,
                new Vector3(10, 0, 0)
            );

            // Point at (5, 3, 0) — perpendicular distance to X-axis is 3.
            float distance = GeometryUtils.DistanceFromPointToLine(
                new Vector3(5, 3, 0), line
            );

            Assert.AreEqual(3f, distance, 0.0001f);
        }

        [Test]
        public void DistanceFromPointToLine_PointBeforeStart_ReturnsDistanceToStart()
        {
            var line = GeometryUtils.PrecomputeLineData(
                new Vector3(5, 0, 0),
                new Vector3(10, 0, 0)
            );

            // Point at origin — before the line start.
            // Distance should be to the start point (5,0,0).
            float distance = GeometryUtils.DistanceFromPointToLine(
                Vector3.zero, line
            );

            Assert.AreEqual(5f, distance, 0.0001f);
        }

        [Test]
        public void DistanceFromPointToLine_PointBeyondEnd_ReturnsDistanceToEnd()
        {
            var line = GeometryUtils.PrecomputeLineData(
                Vector3.zero,
                new Vector3(5, 0, 0)
            );

            // Point at (10, 0, 0) — beyond the line end.
            float distance = GeometryUtils.DistanceFromPointToLine(
                new Vector3(10, 0, 0), line
            );

            Assert.AreEqual(5f, distance, 0.0001f);
        }

        [Test]
        public void DistanceFromPointToLine_PointAtStart_ReturnsZero()
        {
            var line = GeometryUtils.PrecomputeLineData(
                new Vector3(3, 4, 5),
                new Vector3(6, 7, 8)
            );

            float distance = GeometryUtils.DistanceFromPointToLine(
                new Vector3(3, 4, 5), line
            );

            Assert.AreEqual(0f, distance, 0.0001f);
        }

        #endregion

        #region DistancesFromPointsToLine (Batch)

        [Test]
        public void DistancesFromPointsToLine_ReturnsCorrectCount()
        {
            var line = GeometryUtils.PrecomputeLineData(
                Vector3.zero, new Vector3(10, 0, 0)
            );

            var points = new List<Vector3>
            {
                new(1, 0, 0),
                new(5, 3, 0),
                new(10, 4, 0)
            };

            var distances = GeometryUtils.DistancesFromPointsToLine(points, line);

            Assert.AreEqual(3, distances.Count);
        }

        [Test]
        public void DistancesFromPointsToLine_EmptyList_ReturnsEmptyList()
        {
            var line = GeometryUtils.PrecomputeLineData(
                Vector3.zero, Vector3.one
            );

            var distances = GeometryUtils.DistancesFromPointsToLine(
                new List<Vector3>(), line
            );

            Assert.AreEqual(0, distances.Count);
        }

        #endregion

        #region ClampMagnitude

        [Test]
        public void ClampMagnitude_WithinRange_ReturnsUnchanged()
        {
            var input = new Vector3(3, 0, 0); // magnitude = 3
            float magnitude;

            var result = GeometryUtils.ClampMagnitude(input, 1f, 5f, out magnitude);

            Assert.AreEqual(3f, magnitude, 0.0001f);
            Assert.AreEqual(input, result);
        }

        [Test]
        public void ClampMagnitude_BelowMin_ScalesToMin()
        {
            var input = new Vector3(1, 0, 0); // magnitude = 1
            float magnitude;

            var result = GeometryUtils.ClampMagnitude(input, 5f, 10f, out magnitude);

            Assert.AreEqual(5f, result.magnitude, 0.0001f,
                "Vector should be scaled up to minimum magnitude.");
        }

        [Test]
        public void ClampMagnitude_AboveMax_ScalesToMax()
        {
            var input = new Vector3(20, 0, 0); // magnitude = 20
            float magnitude;

            var result = GeometryUtils.ClampMagnitude(input, 1f, 10f, out magnitude);

            Assert.AreEqual(10f, result.magnitude, 0.0001f,
                "Vector should be scaled down to maximum magnitude.");
        }

        [Test]
        public void ClampMagnitude_PreservesDirection()
        {
            var input = new Vector3(3, 4, 0); // magnitude = 5, direction = (0.6, 0.8, 0)
            float magnitude;

            var result = GeometryUtils.ClampMagnitude(input, 10f, 20f, out magnitude);

            // Direction should be preserved even though magnitude changed.
            Assert.AreEqual(input.normalized.x, result.normalized.x, 0.001f);
            Assert.AreEqual(input.normalized.y, result.normalized.y, 0.001f);
            Assert.AreEqual(input.normalized.z, result.normalized.z, 0.001f);
        }

        [Test]
        public void ClampMagnitude_OutputsMagnitude()
        {
            var input = new Vector3(3, 4, 0); // magnitude = 5
            float magnitude;

            GeometryUtils.ClampMagnitude(input, 1f, 10f, out magnitude);

            Assert.AreEqual(5f, magnitude, 0.0001f,
                "Output magnitude should be the original vector's magnitude.");
        }

        #endregion
    }
}
