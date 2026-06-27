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

        void ApplyPowerCrystalPickup(Vector3 position, Color crystalColor)
        {
            _crystalSpeedBonus += powerCrystalStackBonus;
            _speed = Mathf.Clamp(_speed + powerCrystalSpeedImpulse, minimumSpeed, CurrentMaximumSpeed);
            _impactTimer = Mathf.Max(_impactTimer, 0.18f);
            PlayPowerCrystalSound();
            SpawnPickupLightning(position, crystalColor);
            SpawnSpeedDiamondBurst(position, crystalColor);
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
                _nextWallLightningTime = Random.Range(0.08f, 0.22f) * Mathf.Lerp(1f, 0.38f, FinaleIntensity01);
            }

            if (_isRunning && _nextFilamentLightningTime <= 0f)
            {
                SpawnFilamentLightning();
                _nextFilamentLightningTime = (Random.Range(3.5f, 6.3f) - Intensity * 0.28f) * Mathf.Lerp(1f, 0.42f, FinaleIntensity01);
            }

            UpdateLightningBolts(dt);
        }

        void SpawnPickupLightning(Vector3 center, Color color)
        {
            CreateParticleBurst("Speed Diamond Contact Flash", center, color, 42, 0.36f, 24f, 0.55f, 1.9f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 end = center
                              + Random.onUnitSphere * Random.Range(8f, 18f)
                              + Vector3.up * Random.Range(2f, 10f);
                CreateLightningBolt(center, end, 0.42f, 1.45f, false, 0.26f);
            }
        }

        void SpawnSpeedDiamondBurst(Vector3 center, Color color)
        {
            CreateParticleBurst("Speed Diamond Glow Burst", center, color, 72, 0.72f, 28f, 0.35f, 1.7f);
            CreateParticleBurst("Speed Diamond Dark Glass Burst", center, new Color(0.02f, 0.025f, 0.05f, 0.92f), 38, 0.9f, 18f, 0.4f, 2.2f);
            for (int i = 0; i < 20; i++)
            {
                Vector3 direction = Random.onUnitSphere;
                if (direction.y < -0.2f)
                    direction.y = Mathf.Abs(direction.y);

                Vector3 end = center + direction.normalized * Random.Range(12f, 24f);
                CreateLightningBolt(center, end, 0.34f, 2.4f, false, 0.42f);
                CreateTransientShard(center, direction.normalized * Random.Range(22f, 48f), Random.Range(0.42f, 0.95f), color);
            }
        }

        void SpawnPulseGateBurst(Vector3 center, FilamentRuntime filament)
        {
            CreateParticleBurst("Pulse Gate Surge", center, new Color(1f, 0.7f, 0.18f, 0.82f), 64, 0.72f, 26f, 0.28f, 1.45f);
            CreateParticleBurst("Pulse Gate Carbon Wake", center, new Color(0.015f, 0.006f, 0.035f, 0.82f), 34, 0.95f, 15f, 0.42f, 2.1f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + Random.Range(-0.16f, 0.16f);
                Vector3 start = center + (filament.Up * Mathf.Cos(angle) + filament.Side * Mathf.Sin(angle)) * orbitRadius * 0.6f;
                Vector3 end = center + (filament.Up * Mathf.Cos(angle + 0.42f) + filament.Side * Mathf.Sin(angle + 0.42f)) * orbitRadius * 1.75f;
                end += filament.Direction * Random.Range(-8f, 16f);
                CreateLightningBolt(start, end, 0.34f, 2.0f, false, 0.18f);
            }
        }

        void SpawnNanitePop(Vector3 center)
        {
            CreateParticleBurst("Bulk Nanite Pop", center, new Color(1f, 0.16f, 0.12f, 0.84f), 38, 0.48f, 20f, 0.24f, 1.25f);
            CreateParticleBurst("Bulk Nanite Dark Chaff", center, new Color(0.02f, 0f, 0.012f, 0.92f), 24, 0.72f, 13f, 0.38f, 1.65f);
            for (int i = 0; i < 9; i++)
            {
                Vector3 end = center + Random.onUnitSphere * Random.Range(5f, 11f);
                CreateLightningBolt(center, end, 0.24f, 1.1f, false, 0.18f);
            }
        }

        void SpawnContactBurst(Vector3 center, Color color, float scale = 1f)
        {
            CreateParticleBurst("Bulk Contact Burst", center, color, Mathf.RoundToInt(34 * scale), 0.46f * Mathf.Sqrt(scale), 18f * scale, 0.28f, 1.5f);
            CreateParticleBurst("Bulk Contact Dark Flecks", center, new Color(0.012f, 0.004f, 0.03f, 0.9f), Mathf.RoundToInt(18 * scale), 0.68f, 12f * scale, 0.34f, 1.8f);
            SpawnCloseCameraCollisionShower(center, color, scale);
            for (int i = 0; i < Mathf.RoundToInt(4 * scale); i++)
            {
                Vector3 end = center + Random.onUnitSphere * Random.Range(5f * scale, 13f * scale);
                CreateLightningBolt(center, end, 0.18f + scale * 0.08f, 0.75f * scale, false, 0.12f + 0.08f * scale);
            }
        }

        void SpawnFinaleLaunchBurst(Vector3 center, Vector3 direction)
        {
            CreateParticleBurst("Bulk Break Finale Plasma", center, new Color(0.38f, 1f, 0.48f, 0.86f), 120, 1.15f, 38f, 0.32f, 2f);
            CreateParticleBurst("Bulk Break Finale Star Glass", center, new Color(0.9f, 0.7f, 1f, 0.82f), 112, 1.45f, 54f, 0.16f, 1.15f);
            CreateParticleBurst("Bulk Break Finale Dark Shell", center, new Color(0.012f, 0.006f, 0.04f, 0.92f), 90, 1.8f, 34f, 0.55f, 2.6f);
            for (int i = 0; i < 28; i++)
            {
                Vector3 spread = (direction + Random.onUnitSphere * 0.62f).normalized;
                CreateLightningBolt(center - direction * Random.Range(2f, 10f), center + spread * Random.Range(34f, 92f), 0.58f, 5.4f, false, 0.52f);
            }
        }

        void SpawnWallLightning()
        {
            if (_filaments.Count == 0)
                return;

            float baseY = CurrentFilament.Center.y + Random.Range(-tubeRadius * 0.36f, tubeRadius * 0.9f);
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);
            int waves = Random.Range(2, 4) + Mathf.RoundToInt(FinaleIntensity01 * 3f);
            for (int i = 0; i < waves; i++)
            {
                float direction = Random.value < 0.5f ? -1f : 1f;
                float angle = baseAngle + direction * i * Random.Range(0.16f, 0.34f);
                float y = baseY + i * Random.Range(tubeRadius * 0.05f, tubeRadius * 0.16f);
                float arc = direction * Random.Range(0.24f, 0.58f);
                Vector3 start = TubeWallPoint(angle, y);
                Vector3 end = TubeWallPoint(angle + arc, y + Random.Range(tubeRadius * 0.2f, tubeRadius * 0.52f));
                CreateLightningBolt(start, end, 0.5f, tubeRadius * 0.07f, false, 0.28f);
                SpawnWallLightningBranches(start, end, 0.48f);
            }
        }

        void CreateParticleBurst(string burstName, Vector3 center, Color color, int count, float lifetime, float speed, float minSize = 0.35f, float maxSize = 1.4f)
        {
            if (!_runtimeRoot)
                return;

            var burst = new GameObject(burstName);
            burst.transform.SetParent(_runtimeRoot.transform, false);
            burst.transform.position = center;
            var particles = burst.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(count, 16);

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            if (renderer)
                renderer.sharedMaterial = _shardMaterial ? _shardMaterial : _lightningMaterial;

            particles.Play();
            Destroy(burst, lifetime + 1.2f);
        }

        void CreateTransientShard(Vector3 center, Vector3 velocity, float lifetime, Color? colorOverride = null)
        {
            if (!_runtimeRoot)
                return;

            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "Bulk Speed Diamond Shard";
            shard.transform.SetParent(_runtimeRoot.transform, false);
            shard.transform.position = center;
            shard.transform.localScale = new Vector3(0.55f, 0.12f, 1.6f) * Random.Range(0.75f, 1.6f);
            shard.transform.rotation = Random.rotation;
            var renderer = shard.GetComponent<Renderer>();
            if (colorOverride.HasValue && _shardMaterial)
            {
                Material material = new(_shardMaterial) { name = "Bulk Colored Speed Shard" };
                SetMaterialColor(material, colorOverride.Value);
                renderer.sharedMaterial = material;
            }
            else
            {
                renderer.sharedMaterial = _shardMaterial ? _shardMaterial : _crystalMaterial;
            }
            Destroy(shard.GetComponent<Collider>());
            _transientShards.Add(new TransientShardRuntime
            {
                Transform = shard.transform,
                Velocity = velocity,
                AngularVelocity = Random.onUnitSphere * Random.Range(220f, 520f),
                Lifetime = lifetime,
                BaseScale = shard.transform.localScale
            });
        }

        void UpdateTransientShards(float dt)
        {
            for (int i = _transientShards.Count - 1; i >= 0; i--)
            {
                TransientShardRuntime shard = _transientShards[i];
                shard.Age += dt;
                if (shard.Transform)
                {
                    float life01 = Mathf.Clamp01(shard.Age / Mathf.Max(0.01f, shard.Lifetime));
                    shard.Transform.position += shard.Velocity * dt;
                    shard.Transform.Rotate(shard.AngularVelocity * dt, Space.World);
                    shard.Transform.localScale = shard.BaseScale * (1f - life01);
                }

                if (shard.Age < shard.Lifetime)
                {
                    _transientShards[i] = shard;
                    continue;
                }

                if (shard.Transform)
                    Destroy(shard.Transform.gameObject);
                _transientShards.RemoveAt(i);
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
