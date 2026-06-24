using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        readonly List<LightningRuntime> _lightningBolts = new();
        Material _lightningMaterial;
        float _nextWallLightningTime;
        float _nextFilamentLightningTime;

        void ResetLightningSchedule()
        {
            _nextWallLightningTime = Random.Range(0.08f, 0.18f);
            _nextFilamentLightningTime = Random.Range(3.2f, 5.4f);
        }

        void ResetLightningState()
        {
            _lightningBolts.Clear();
            _nextWallLightningTime = 0f;
            _nextFilamentLightningTime = 0f;
        }

        void ApplyPowerCrystalPickup(Vector3 position)
        {
            _crystalSpeedBonus += powerCrystalStackBonus;
            _speed = Mathf.Clamp(_speed + powerCrystalSpeedImpulse, minimumSpeed, CurrentMaximumSpeed);
            _impactTimer = Mathf.Max(_impactTimer, 0.18f);
            PlayPowerCrystalSound();
            SpawnPickupLightning(position);
        }

        void AnimateLightning(float dt)
        {
            if (!_runtimeRoot || !_lightningMaterial)
                return;

            _nextWallLightningTime -= dt;
            _nextFilamentLightningTime -= dt;

            if (_nextWallLightningTime <= 0f)
            {
                SpawnWallLightning();
                _nextWallLightningTime = Random.Range(0.08f, 0.22f);
            }

            if (_isRunning && _nextFilamentLightningTime <= 0f)
            {
                SpawnFilamentLightning();
                _nextFilamentLightningTime = Random.Range(3.5f, 6.3f) - Intensity * 0.28f;
            }

            UpdateLightningBolts(dt);
        }

        void SpawnPickupLightning(Vector3 center)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 end = center
                              + Random.onUnitSphere * Random.Range(7f, 13f)
                              + Vector3.up * Random.Range(2f, 8f);
                CreateLightningBolt(center, end, 0.34f, 0.9f, false, 0.2f);
            }
        }

        void SpawnWallLightning()
        {
            if (_filaments.Count == 0)
                return;

            float baseY = CurrentFilament.Center.y + Random.Range(-tubeRadius * 0.36f, tubeRadius * 0.9f);
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);
            int waves = Random.Range(2, 4);
            for (int i = 0; i < waves; i++)
            {
                float direction = Random.value < 0.5f ? -1f : 1f;
                float angle = baseAngle + direction * i * Random.Range(0.16f, 0.34f);
                float y = baseY + i * Random.Range(tubeRadius * 0.05f, tubeRadius * 0.16f);
                float arc = direction * Random.Range(0.24f, 0.58f);
                Vector3 start = TubeWallPoint(angle, y);
                Vector3 end = TubeWallPoint(angle + arc, y + Random.Range(tubeRadius * 0.2f, tubeRadius * 0.52f));
                CreateLightningBolt(start, end, 0.58f, tubeRadius * 0.082f, false, 0.46f);
                SpawnWallLightningBranches(start, end, 0.48f);
            }
        }

        void SpawnWallLightningBranches(Vector3 start, Vector3 end, float lifetime)
        {
            for (int i = 0; i < 5; i++)
            {
                float t = Random.Range(0.14f, 0.9f);
                Vector3 branchStart = Vector3.Lerp(start, end, t);
                Vector3 radial = new Vector3(branchStart.x, 0f, branchStart.z).normalized;
                if (radial.sqrMagnitude < 0.01f)
                    radial = Vector3.forward;
                Vector3 tangent = Vector3.Cross(Vector3.up, radial).normalized;
                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 branchEnd = branchStart
                                    + tangent * side * Random.Range(tubeRadius * 0.055f, tubeRadius * 0.14f)
                                    + Vector3.up * Random.Range(tubeRadius * 0.045f, tubeRadius * 0.18f)
                                    + radial * Random.Range(-tubeRadius * 0.02f, tubeRadius * 0.035f);
                CreateLightningBolt(branchStart, branchEnd, lifetime * Random.Range(0.65f, 1f), tubeRadius * 0.055f, false, 0.2f);
            }
        }

        void SpawnFilamentLightning()
        {
            if (_currentFilamentIndex + 1 >= _filaments.Count)
                return;

            FilamentRuntime from = CurrentFilament;
            FilamentRuntime to = _filaments[_currentFilamentIndex + 1];
            Vector3 start = AttachPoint(from, Mathf.Min(from.TravelLength, _distanceOnFilament + Random.Range(22f, 70f)));
            Vector3 end = AttachPoint(to, Random.Range(0f, Mathf.Min(to.TravelLength, to.TransferDistance * 0.55f)));
            start += (from.Up * Random.Range(-0.8f, 0.8f) + from.Side * Random.Range(-0.8f, 0.8f)) * orbitRadius;
            end += (to.Up * Random.Range(-0.8f, 0.8f) + to.Side * Random.Range(-0.8f, 0.8f)) * orbitRadius;
            CreateLightningBolt(start, end, 0.52f, tubeRadius * 0.045f, true, 0.58f);
        }

        Vector3 TubeWallPoint(float angle, float y)
        {
            float radius = tubeRadius * Random.Range(0.92f, 1.04f);
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        void CreateLightningBolt(Vector3 start, Vector3 end, float lifetime, float jaggedness, bool hazard, float width)
        {
            int points = hazard ? 8 : 12;
            var line = MakeLine(hazard ? "Filament Lightning Hazard" : "Wormhole Lightning", points, width, _lightningMaterial);
            SetJaggedBolt(line, start, end, jaggedness);
            _lightningBolts.Add(new LightningRuntime(line, start, end, lifetime, hazard));
        }

        void SetJaggedBolt(LineRenderer line, Vector3 start, Vector3 end, float jaggedness)
        {
            Vector3 axis = end - start;
            Vector3 normal = Vector3.Cross(axis.normalized, Vector3.up);
            if (normal.sqrMagnitude < 0.01f)
                normal = Vector3.Cross(axis.normalized, Vector3.right);
            normal.Normalize();
            Vector3 side = Vector3.Cross(axis.normalized, normal).normalized;

            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (float)(line.positionCount - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                Vector3 jitter = (normal * Random.Range(-jaggedness, jaggedness) +
                                  side * Random.Range(-jaggedness, jaggedness)) * envelope;
                line.SetPosition(i, Vector3.Lerp(start, end, t) + jitter);
            }
        }

        void UpdateLightningBolts(float dt)
        {
            for (int i = _lightningBolts.Count - 1; i >= 0; i--)
            {
                LightningRuntime bolt = _lightningBolts[i];
                bolt.Age += dt;

                if (bolt.Line)
                    bolt.Line.widthMultiplier = bolt.Width * Mathf.Clamp01(1f - bolt.Age / bolt.Lifetime);

                if (bolt.Hazard && !bolt.HasHit && _vessel != null &&
                    DistancePointToSegment(_vessel.Transform.position, bolt.Start, bolt.End) < orbitRadius * 0.65f)
                {
                    bolt.HasHit = true;
                    _crystalSpeedBonus = 0f;
                    _speed = minimumSpeed;
                    _impactTimer = 0.9f;
                }

                if (bolt.Age < bolt.Lifetime)
                {
                    _lightningBolts[i] = bolt;
                    continue;
                }

                if (bolt.Line)
                    Destroy(bolt.Line.gameObject);
                _lightningBolts.RemoveAt(i);
            }
        }

        static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude)
                : 0f;
            return Vector3.Distance(point, a + segment * t);
        }

        struct LightningRuntime
        {
            public readonly LineRenderer Line;
            public readonly Vector3 Start;
            public readonly Vector3 End;
            public readonly float Lifetime;
            public readonly float Width;
            public readonly bool Hazard;
            public float Age;
            public bool HasHit;

            public LightningRuntime(LineRenderer line, Vector3 start, Vector3 end, float lifetime, bool hazard)
            {
                Line = line;
                Start = start;
                End = end;
                Lifetime = lifetime;
                Width = line ? line.widthMultiplier : 0.2f;
                Hazard = hazard;
                Age = 0f;
                HasHit = false;
            }
        }
    }
}
