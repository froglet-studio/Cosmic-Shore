using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        float SampleFilamentLength(System.Random random)
        {
            float diameterRatio = filamentLengthMeanDiameter + RandomNormal(random) * filamentLengthStdDevDiameter;
            diameterRatio = Mathf.Clamp(diameterRatio, MinFilamentDiameterRatio, MaxFilamentDiameterRatio);
            return tubeRadius * 2f * diameterRatio;
        }

        Vector3 FirstFilamentStart(System.Random random)
        {
            Vector2 rim = UnitFromAngle(RandomRange(random, 0f, Mathf.PI * 2f));
            return new Vector3(rim.x, 0f, rim.y) * (tubeRadius * 0.86f);
        }

        Vector3 NextFilamentStart(FilamentRuntime previous, System.Random random)
        {
            Vector3 transferPoint = AttachPoint(previous, previous.TransferDistance);
            Vector2 nudge = UnitFromAngle(RandomRange(random, 0f, Mathf.PI * 2f));
            float nudgeSize = RandomRange(random, 0f, filamentTransferNudge);
            Vector3 start = transferPoint + Vector3.up * filamentRisePerTransfer + new Vector3(nudge.x, 0f, nudge.y) * nudgeSize;
            return ClampTubeRadius(start, tubeRadius * 0.88f);
        }

        Vector3 FilamentDirectionFromStart(Vector3 start, float length, System.Random random)
        {
            Vector2 startXz = new(start.x, start.z);
            Vector2 inward = startXz.sqrMagnitude > 1f ? -startXz.normalized : UnitFromAngle(RandomRange(random, 0f, Mathf.PI * 2f));
            Vector3 best = Vector3.right;
            float bestOverflow = float.MaxValue;

            for (int attempt = 0; attempt < 18; attempt++)
            {
                float turn = RandomRange(random, 18f, 74f) * (random.NextDouble() < 0.5 ? -1f : 1f);
                Vector2 horizontal = Rotate(inward, turn * Mathf.Deg2Rad);
                float slope = RandomRange(random, 0.045f, 0.095f);
                Vector3 direction = new Vector3(horizontal.x, slope, horizontal.y).normalized;
                float overflow = FilamentEndpointOverflow(start, direction, length);
                if (overflow <= 0f)
                    return direction;

                if (overflow < bestOverflow)
                {
                    bestOverflow = overflow;
                    best = direction;
                }
            }

            return best;
        }

        float FilamentEndpointOverflow(Vector3 start, Vector3 direction, float length)
        {
            float halfTravel = length * FilamentTravelRatio * 0.5f;
            Vector3 center = start + direction * halfTravel;
            Vector3 a = center - direction * (length * 0.5f);
            Vector3 b = center + direction * (length * 0.5f);
            float allowed = tubeRadius * 0.97f;
            return Mathf.Max(HorizontalRadius(a), HorizontalRadius(b)) - allowed;
        }

        static Vector3 ClampTubeRadius(Vector3 point, float maxRadius)
        {
            Vector2 xz = new(point.x, point.z);
            if (xz.sqrMagnitude <= maxRadius * maxRadius)
                return point;

            Vector2 clamped = xz.normalized * maxRadius;
            return new Vector3(clamped.x, point.y, clamped.y);
        }

        static float HorizontalRadius(Vector3 point)
        {
            return Mathf.Sqrt(point.x * point.x + point.z * point.z);
        }

        static Vector2 Rotate(Vector2 vector, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(vector.x * c - vector.y * s, vector.x * s + vector.y * c).normalized;
        }

        static Vector2 UnitFromAngle(float radians)
        {
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        static float RandomRange(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        static float RandomNormal(System.Random random)
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = 1.0 - random.NextDouble();
            double sample = System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
            return (float)sample;
        }
    }
}
