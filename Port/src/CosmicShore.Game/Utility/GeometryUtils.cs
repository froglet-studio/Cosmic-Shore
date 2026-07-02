using CosmicShore.Engine;
using System.Collections.Generic;

namespace CosmicShore.Utility
{
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
                Vector3 delta = end - start;
                Magnitude = delta.magnitude;
                Direction = Magnitude > 1e-5f ? delta / Magnitude : Vector3.zero; // one sqrt; 1e-5 matches Vector3.normalized's kEpsilon zero-threshold exactly
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

        public static Vector3 ClampMagnitude(Vector3 vector, float minMagnitude, float maxMagnitude, out float magnitude)
        { 
            magnitude = vector.magnitude;   
            if (magnitude < minMagnitude)
                return vector * minMagnitude/magnitude;
      
            else if (magnitude > maxMagnitude)
                return vector * maxMagnitude/magnitude;
            return vector;
        }

    }
}
