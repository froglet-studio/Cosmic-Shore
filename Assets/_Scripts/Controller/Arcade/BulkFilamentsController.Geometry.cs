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
            Vector3 transferPoint = CenterlinePoint(previous, previous.TransferDistance);
            Vector2 nudge = UnitFromAngle(RandomRange(random, 0f, Mathf.PI * 2f));
            float nudgeSize = RandomRange(random, 0f, filamentTransferNudge);
            Vector3 start = transferPoint + Vector3.up * filamentRisePerTransfer + new Vector3(nudge.x, 0f, nudge.y) * nudgeSize;
            return ClampTubeRadius(start, tubeRadius * 0.88f);
        }

        void ConfigureFilamentMotion(FilamentRuntime filament, System.Random random)
        {
            filament.BaseCenter = filament.Center;
            filament.BaseDirection = filament.Direction;
            filament.RotationPhaseDegrees = RandomRange(random, 0f, 360f);
            float direction = random.NextDouble() < 0.5 ? -1f : 1f;
            filament.RotationSpeedDegrees = RandomRange(random, filamentRotationMinDegreesPerSecond, filamentRotationMaxDegreesPerSecond) * direction;
            filament.WaveAmplitude = filamentWaveAmplitude * RandomRange(random, 0.75f, 1.35f);
            filament.WaveSpeed = filamentWaveSpeed * RandomRange(random, 0.75f, 1.45f);
            filament.WavePhase = RandomRange(random, 0f, Mathf.PI * 2f);

            float periodRatio = 1f;
            for (int i = 0; i < filament.WaveFrequencies.Length; i++)
            {
                periodRatio *= RandomRange(random, 0.8f, 1.2f);
                filament.WaveFrequencies[i] = (i + 1f) * periodRatio * RandomRange(random, 0.72f, 1.28f);
                filament.WavePhases[i] = RandomRange(random, 0f, Mathf.PI * 2f);
                filament.WaveWeights[i] = 1f / (i + 1.35f);
            }
        }

        void UpdateDynamicFilamentPoses()
        {
            if (_filaments.Count == 0)
                return;

            float time = Time.time;
            for (int i = 0; i < _filaments.Count; i++)
            {
                FilamentRuntime filament = _filaments[i];
                float rotation = filament.RotationPhaseDegrees + time * filament.RotationSpeedDegrees;
                Quaternion twist = Quaternion.AngleAxis(rotation, Vector3.up);
                filament.Center = RotateHorizontalAroundOrigin(filament.BaseCenter, twist);
                filament.Direction = RotateHorizontalDirection(filament.BaseDirection, twist).normalized;
                filament.Side = Vector3.Cross(Vector3.up, filament.Direction).normalized;
                if (filament.Side.sqrMagnitude < 0.01f)
                    filament.Side = Vector3.right;
                filament.Up = Vector3.Cross(filament.Direction, filament.Side).normalized;
                UpdateFilamentBeam(filament);
            }
        }

        static Vector3 RotateHorizontalAroundOrigin(Vector3 point, Quaternion rotation)
        {
            Vector3 horizontal = rotation * new Vector3(point.x, 0f, point.z);
            return new Vector3(horizontal.x, point.y, horizontal.z);
        }

        static Vector3 RotateHorizontalDirection(Vector3 direction, Quaternion rotation)
        {
            Vector3 horizontal = rotation * new Vector3(direction.x, 0f, direction.z);
            return new Vector3(horizontal.x, direction.y, horizontal.z);
        }

        Vector3 CenterlinePoint(FilamentRuntime filament, float distance)
        {
            float halfTravel = filament.Length * FilamentTravelRatio * 0.5f;
            float axisDistance = Mathf.Lerp(-halfTravel, halfTravel, Mathf.Clamp01(distance / filament.TravelLength));
            return filament.Center + filament.Direction * axisDistance;
        }

        Vector3 FilamentSurfacePoint(FilamentRuntime filament, float axis01)
        {
            axis01 = Mathf.Clamp01(axis01);
            float axisDistance = Mathf.Lerp(-0.5f, 0.5f, axis01) * filament.Length;
            return filament.Center + filament.Direction * axisDistance + FilamentWaveOffset(filament, axis01);
        }

        Vector3 FilamentWaveOffset(FilamentRuntime filament, float axis01)
        {
            float time = Time.time * filament.WaveSpeed + filament.WavePhase;
            float sideWave = 0f;
            float upWave = 0f;
            for (int i = 0; i < filament.WaveFrequencies.Length; i++)
            {
                float frequency = filament.WaveFrequencies[i];
                float phase = filament.WavePhases[i];
                float weight = filament.WaveWeights[i];
                sideWave += Mathf.Sin(axis01 * Mathf.PI * 2f * frequency + phase + time) * weight;
                upWave += Mathf.Cos(axis01 * Mathf.PI * 2f * (frequency * 0.83f) + phase * 1.37f - time * 0.72f) * weight;
            }

            float envelope = Mathf.Sin(axis01 * Mathf.PI);
            return (filament.Side * sideWave + filament.Up * upWave * 0.58f) * filament.WaveAmplitude * envelope;
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
