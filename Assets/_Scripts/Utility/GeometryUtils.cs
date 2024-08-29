using UnityEngine;
using System.Collections.Generic;

public static class GeometryUtils
{
    public struct LineData
    {
        public Vector3 Start;
        public Vector3 Direction;
        public float Magnitude;

        public LineData(Vector3 start, Vector3 end)
        {
            Start = start;
            Direction = (end - start).normalized;
            Magnitude = Vector3.Distance(start, end);
        }
    }

    public static LineData PrecomputeLineData(Vector3 lineStart, Vector3 lineEnd)
    {
        return new LineData(lineStart, lineEnd);
    }

    public static float DistanceFromPointToLine(Vector3 point, LineData lineData)
    {
        Vector3 pointVector = point - lineData.Start;
        float dotProduct = Vector3.Dot(pointVector, lineData.Direction);

        if (dotProduct < 0)
        {
            return Vector3.Distance(point, lineData.Start);
        }
        else if (dotProduct > lineData.Magnitude)
        {
            return Vector3.Distance(point, lineData.Start + lineData.Direction * lineData.Magnitude);
        }
        else
        {
            Vector3 projection = lineData.Start + lineData.Direction * dotProduct;
            return Vector3.Distance(point, projection);
        }
    }

    public static List<float> DistancesFromPointsToLine(List<Vector3> points, LineData lineData)
    {
        List<float> distances = new List<float>(points.Count);
        foreach (Vector3 point in points)
        {
            distances.Add(DistanceFromPointToLine(point, lineData));
        }
        return distances;
    }
}