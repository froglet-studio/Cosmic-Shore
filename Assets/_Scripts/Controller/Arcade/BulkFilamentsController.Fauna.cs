using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        readonly List<BulkFaunaRuntime> _bulkFauna = new();
        AudioClip _faunaDeathClip;
        float _nextFaunaAttackCheckTime;

        void CreateBulkFauna()
        {
            if (!_runtimeRoot || _filaments.Count < 3)
                return;

            int count = Mathf.Clamp(6 + Intensity * 2, 8, 14);
            float routeEnd = _filaments[_filaments.Count - 1].RouteStartDistance * 0.88f;
            for (int i = 0; i < count; i++)
            {
                bool giant = i % 5 == 3;
                float route01 = (i + 0.65f) / (count + 1f);
                float route = Mathf.Lerp(filamentRisePerTransfer * 2.4f, routeEnd, route01);
                FilamentRuntime filament = FilamentAtRouteDistance(route, out float localDistance);
                if (filament == null)
                    continue;

                var fauna = new GameObject(giant ? $"Bulk Giant Squid Fauna {i:00}" : $"Bulk Fauna {i:00}");
                fauna.transform.SetParent(_runtimeRoot.transform, false);
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(orbitRadius + 48f, tubeRadius * (giant ? 0.58f : 0.46f));
                fauna.transform.position = PositionOnFilament(filament, localDistance, angle, radius);
                CreateFaunaBody(fauna.transform, giant);
                CreateFaunaSpriteOverlay(fauna.transform, i, giant);

                _bulkFauna.Add(new BulkFaunaRuntime
                {
                    Transform = fauna.transform,
                    Filament = filament,
                    RouteDistance = route,
                    OrbitAngleRadians = angle,
                    Radius = radius,
                    SwimPhase = Random.Range(0f, Mathf.PI * 2f),
                    SwimSpeed = Random.Range(-6f, 8f),
                    OrbitSpeed = Random.Range(-0.18f, 0.22f),
                    BaseScale = giant ? Random.Range(1.4f, 1.9f) : Random.Range(0.72f, 1.05f),
                    Giant = giant,
                    Alive = true,
                    Tendrils = CreateFaunaTendrils(fauna.transform, giant)
                });
            }
        }

        void ResetBulkFauna()
        {
            _bulkFauna.Clear();
            _nextFaunaAttackCheckTime = 0f;
        }

        void AnimateBulkFauna(float dt)
        {
            if (_bulkFauna.Count == 0 || _filaments.Count == 0)
                return;

            for (int i = 0; i < _bulkFauna.Count; i++)
                AnimateFauna(_bulkFauna[i], dt);

            TryNaniteFaunaDistraction();
        }

        void AnimateFauna(BulkFaunaRuntime fauna, float dt)
        {
            if (fauna == null || !fauna.Transform)
                return;

            if (!fauna.Alive)
            {
                fauna.EatTimer += dt;
                float shrink = Mathf.Clamp01(1f - fauna.EatTimer / 0.85f);
                fauna.Transform.localScale = Vector3.one * fauna.BaseScale * shrink;
                if (fauna.EatTimer >= 0.85f)
                    fauna.Transform.gameObject.SetActive(false);
                return;
            }

            fauna.RouteDistance += fauna.SwimSpeed * dt;
            fauna.OrbitAngleRadians += fauna.OrbitSpeed * dt + Mathf.Sin(Time.time * 0.62f + fauna.SwimPhase) * dt * 0.045f;
            FilamentRuntime filament = FilamentAtRouteDistance(Mathf.Max(0f, fauna.RouteDistance), out float localDistance);
            if (filament == null)
                return;

            float swim = Mathf.Sin(Time.time * (fauna.Giant ? 0.82f : 1.35f) + fauna.SwimPhase);
            Vector3 position = PositionOnFilament(filament, localDistance, fauna.OrbitAngleRadians, fauna.Radius + swim * 8f);
            fauna.Transform.position = position;
            fauna.Transform.localScale = Vector3.one * fauna.BaseScale * (1f + BeatPulse() * 0.08f + swim * 0.035f);
            fauna.Transform.rotation = Quaternion.LookRotation((position - AttachPoint(filament, localDistance)).normalized, filament.Up);
            UpdateFaunaTendrils(fauna, filament, localDistance);
        }

        void TryNaniteFaunaDistraction()
        {
            if (!_isRunning || Time.time < _nextFaunaAttackCheckTime || _naniteRouteDistance <= 0f)
                return;

            _nextFaunaAttackCheckTime = Time.time + Random.Range(0.55f, 1.2f);
            float bestDistance = 9999f;
            BulkFaunaRuntime best = null;
            foreach (BulkFaunaRuntime fauna in _bulkFauna)
            {
                if (fauna == null || !fauna.Alive)
                    continue;

                float routeDelta = Mathf.Abs(fauna.RouteDistance - _naniteRouteDistance);
                bool betweenChaseAndShip = fauna.RouteDistance < PlayerRouteDistance + 90f && fauna.RouteDistance > _naniteRouteDistance - 70f;
                if (betweenChaseAndShip && routeDelta < bestDistance)
                {
                    bestDistance = routeDelta;
                    best = fauna;
                }
            }

            if (best == null || bestDistance > (best.Giant ? 125f : 76f) || Random.value > 0.52f)
                return;

            best.Alive = false;
            best.EatTimer = 0f;
            _naniteRouteDistance = Mathf.Max(0f, _naniteRouteDistance - (best.Giant ? 34f : 16f));
            SpawnFaunaDeathBurst(best.Transform.position, best.Giant);
            if (best.Giant)
                PlayFaunaDeathSound();
        }

        void CreateFaunaBody(Transform parent, bool giant)
        {
            float scale = giant ? 3.2f : 1.7f;
            CreateNanitePart(parent, PrimitiveType.Sphere, Vector3.zero, Vector3.one * scale, _glyphMaterial ? _glyphMaterial : _whiteEnergyMaterial);
            CreateNanitePart(parent, PrimitiveType.Cube, Vector3.back * scale * 0.52f, new Vector3(0.34f, 0.34f, 1.7f) * scale, _whiteEnergyMaterial);
        }

        LineRenderer[] CreateFaunaTendrils(Transform parent, bool giant)
        {
            int count = giant ? 7 : 4;
            var tendrils = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                tendrils[i] = MakeLine($"{parent.name} Tendril {i}", 5, giant ? 0.22f : 0.12f, _whiteEnergyMaterial);
                tendrils[i].transform.SetParent(parent, true);
            }
            return tendrils;
        }

        void UpdateFaunaTendrils(BulkFaunaRuntime fauna, FilamentRuntime filament, float localDistance)
        {
            if (fauna.Tendrils == null)
                return;

            Vector3 baseDirection = -filament.Direction;
            for (int i = 0; i < fauna.Tendrils.Length; i++)
            {
                LineRenderer line = fauna.Tendrils[i];
                if (!line)
                    continue;

                float angle = fauna.OrbitAngleRadians + i * Mathf.PI * 2f / fauna.Tendrils.Length;
                Vector3 curl = (filament.Side * Mathf.Cos(angle) + filament.Up * Mathf.Sin(angle)).normalized;
                Vector3 root = fauna.Transform.position + curl * (fauna.Giant ? 2.8f : 1.4f);
                for (int p = 0; p < line.positionCount; p++)
                {
                    float t = p / (float)(line.positionCount - 1);
                    float wave = Mathf.Sin(Time.time * 4.2f + i * 1.7f + t * 5.4f) * (fauna.Giant ? 4.2f : 1.8f);
                    line.SetPosition(p, root + baseDirection * t * (fauna.Giant ? 24f : 12f) + curl * (wave + t * 5f));
                }
            }
        }

        void SpawnFaunaDeathBurst(Vector3 position, bool giant)
        {
            Color color = giant ? new Color(0.38f, 0.92f, 1f, 1f) : new Color(0.32f, 1f, 0.68f, 1f);
            CreateParticleBurst("Bulk Fauna Eaten Plasma", position, color, giant ? 130 : 62, giant ? 1.1f : 0.72f, giant ? 34f : 22f, 0.24f, giant ? 2.8f : 1.5f);
            for (int i = 0; i < (giant ? 16 : 7); i++)
                CreateLightningBolt(position, position + Random.onUnitSphere * Random.Range(9f, giant ? 48f : 22f), 0.34f, 1.9f, false, 0.18f);
        }

        void PlayFaunaDeathSound()
        {
            EnsureBulkAudioSources();
            _faunaDeathClip ??= MakeProceduralClip("Bulk Giant Fauna Echo Cry", 1.55f, 54f, 142f, 0.68f, 0.08f);
            PlayOneShotClip(_faunaDeathClip, 1.2f, "giant_fauna_death");
        }

        sealed class BulkFaunaRuntime
        {
            public Transform Transform;
            public FilamentRuntime Filament;
            public LineRenderer[] Tendrils;
            public float RouteDistance;
            public float OrbitAngleRadians;
            public float Radius;
            public float SwimPhase;
            public float SwimSpeed;
            public float OrbitSpeed;
            public float BaseScale;
            public float EatTimer;
            public bool Giant;
            public bool Alive;
        }
    }
}
