using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        readonly List<Vector3> _naniteVelocities = new();
        readonly List<Vector3> _naniteOffsets = new();
        readonly List<float> _naniteRouteOffsets = new();
        readonly List<bool> _naniteHasPosition = new();

        void ResetNaniteMotionState()
        {
            _naniteVelocities.Clear();
            _naniteOffsets.Clear();
            _naniteRouteOffsets.Clear();
            _naniteHasPosition.Clear();
        }

        void AddNaniteMotionSeed(int index)
        {
            float side = Mathf.Sin(index * 12.9898f) * orbitRadius * 0.86f;
            float up = Mathf.Cos(index * 78.233f) * orbitRadius * 0.68f;
            float depth = (index % 7) * 2.7f + Mathf.Sin(index * 5.17f) * 3.5f;

            _naniteOffsets.Add(new Vector3(side, up, depth));
            _naniteRouteOffsets.Add(index * 1.65f + Mathf.Abs(Mathf.Sin(index * 1.37f)) * 8f);
            _naniteVelocities.Add(Vector3.zero);
            _naniteHasPosition.Add(false);
        }

        void AnimateNanites()
        {
            if (_nanites.Count == 0 || _filaments.Count == 0)
                return;

            EnsureNaniteMotionLists();

            float visualRoute = VisibleNaniteRouteDistance();
            Vector3 swarmCenter = Vector3.zero;
            int activeNanites = 0;

            for (int i = 0; i < _nanites.Count; i++)
            {
                GameObject nanite = _nanites[i];
                if (!nanite)
                    continue;

                if (i < _naniteRespawnTimers.Count && _naniteRespawnTimers[i] > 0f)
                {
                    _naniteRespawnTimers[i] = Mathf.Max(0f, _naniteRespawnTimers[i] - Time.deltaTime);
                    _naniteHasPosition[i] = false;
                    if (nanite.activeSelf)
                        nanite.SetActive(false);
                    continue;
                }

                if (!nanite.activeSelf)
                    nanite.SetActive(true);

                Vector3 target = NaniteTargetPosition(i, visualRoute);
                if (!_naniteHasPosition[i])
                    SeedNanitePosition(i, nanite.transform, target);
                else
                    SteerNaniteToward(i, nanite.transform, target, Time.deltaTime);

                nanite.transform.localScale = Vector3.one * (1f + BeatPulse() * 0.1f);
                swarmCenter += nanite.transform.position;
                activeNanites++;
            }

            if (activeNanites > 0)
                UpdateNaniteWake(swarmCenter / activeNanites);
        }

        float VisibleNaniteRouteDistance()
        {
            float visualRoute = Mathf.Max(0f, _naniteRouteDistance);
            if (!_isRunning)
                return visualRoute;

            float desiredTail = Mathf.Max(10f, naniteVisualTailDistance);
            return Mathf.Clamp(
                _naniteRouteDistance,
                PlayerRouteDistance - naniteCatchBuffer * 1.22f,
                PlayerRouteDistance - desiredTail);
        }

        Vector3 NaniteTargetPosition(int index, float visualRoute)
        {
            float routeOffset = index < _naniteRouteOffsets.Count ? _naniteRouteOffsets[index] : index * 1.65f;
            float wobble = Mathf.Sin(Time.time * 1.35f + index * 2.19f) * 6f;
            float targetRoute = Mathf.Max(0f, visualRoute - routeOffset + wobble);
            FilamentRuntime filament = FilamentAtRouteDistance(targetRoute, out float localDistance);
            if (filament == null)
                return Vector3.zero;

            Vector3 offset = index < _naniteOffsets.Count ? _naniteOffsets[index] : Vector3.zero;
            float swarmBreath = Mathf.Sin(Time.time * 1.7f + index * 0.61f);
            float side = offset.x + swarmBreath * 8f;
            float up = offset.y + Mathf.Cos(Time.time * 1.43f + index * 0.91f) * 7f;
            float depth = offset.z + Mathf.Sin(Time.time * 0.86f + index) * 4f;
            return AttachPoint(filament, localDistance - depth)
                   + filament.Side * side
                   + filament.Up * up
                   - filament.Direction * depth;
        }

        void SeedNanitePosition(int index, Transform transform, Vector3 target)
        {
            transform.position = target + Random.onUnitSphere * Random.Range(4f, 16f);
            _naniteVelocities[index] = Random.onUnitSphere * Random.Range(8f, 22f);
            _naniteHasPosition[index] = true;
        }

        void SteerNaniteToward(int index, Transform transform, Vector3 target, float dt)
        {
            if (dt <= 0f)
                return;

            Vector3 current = transform.position;
            Vector3 toTarget = target - current;
            float gap = Mathf.Max(0f, PlayerRouteDistance - _naniteRouteDistance);
            float danger = Mathf.Clamp01((naniteCatchBuffer * 1.8f - gap) / Mathf.Max(1f, naniteCatchBuffer));
            float maxSpeed = Mathf.Lerp(48f, 150f + _speed * 0.65f, danger + FinaleIntensity01 * 0.35f);
            Vector3 desired = Vector3.ClampMagnitude(toTarget * 3.4f + SeparationForNanite(index, current), maxSpeed);

            float acceleration = Mathf.Lerp(120f, 360f, danger + FinaleIntensity01 * 0.4f);
            Vector3 velocity = Vector3.MoveTowards(_naniteVelocities[index], desired, acceleration * dt);
            _naniteVelocities[index] = velocity;
            transform.position = current + velocity * dt;

            Vector3 facing = velocity.sqrMagnitude > 0.05f ? velocity : toTarget;
            if (facing.sqrMagnitude > 0.05f)
            {
                Quaternion look = Quaternion.LookRotation(facing.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Clamp01(dt * 7f));
            }
        }

        Vector3 SeparationForNanite(int index, Vector3 current)
        {
            Vector3 separation = Vector3.zero;
            float radius = Mathf.Max(7f, orbitRadius * 0.55f);
            float radiusSq = radius * radius;

            for (int i = 0; i < _nanites.Count; i++)
            {
                if (i == index || i >= _naniteHasPosition.Count || !_naniteHasPosition[i] || !_nanites[i])
                    continue;

                Vector3 diff = current - _nanites[i].transform.position;
                float sqr = diff.sqrMagnitude;
                if (sqr <= 0.001f || sqr > radiusSq)
                    continue;

                separation += diff.normalized * ((radius - Mathf.Sqrt(sqr)) * 8f);
            }

            return separation;
        }

        void UpdateNaniteWake(Vector3 swarmCenter)
        {
            if (!_naniteWakeLine || _vessel == null)
                return;

            _naniteWakeLine.gameObject.SetActive(_isRunning || _missionFinaleActive);
            Vector3 vesselPosition = _vessel.Transform.position;
            for (int i = 0; i < _naniteWakeLine.positionCount; i++)
            {
                float t = i / (float)(_naniteWakeLine.positionCount - 1);
                Vector3 p = Vector3.Lerp(swarmCenter, vesselPosition, t);
                Vector3 jitter = Random.onUnitSphere * Mathf.Sin(Time.time * 8f + i) * (1f - t) * 1.25f;
                _naniteWakeLine.SetPosition(i, p + jitter);
            }

            float gap = Mathf.Max(0f, PlayerRouteDistance - _naniteRouteDistance);
            float danger = Mathf.Clamp01((naniteCatchBuffer * 1.5f - gap) / Mathf.Max(1f, naniteCatchBuffer));
            _naniteWakeLine.widthMultiplier = Mathf.Lerp(0.18f, 0.72f, danger + BeatPulse() * 0.12f);
        }

        void BurstTrailingNanites()
        {
            if (_nanites.Count == 0 || _vessel == null)
                return;

            Vector3 vesselPosition = _vessel.Transform.position;
            int popped = 0;
            for (int pass = 0; pass < _nanites.Count && popped < 3; pass++)
            {
                int index = (pass * 7 + _successfulTransfers) % _nanites.Count;
                if (index < _naniteRespawnTimers.Count && _naniteRespawnTimers[index] > 0f)
                    continue;

                GameObject nanite = _nanites[index];
                if (!nanite || !nanite.activeSelf)
                    continue;

                if (Vector3.Distance(nanite.transform.position, vesselPosition) > orbitRadius * 3.6f)
                    continue;

                SpawnNanitePop(nanite.transform.position);
                _naniteRespawnTimers[index] = Random.Range(0.48f, 0.9f);
                _naniteHasPosition[index] = false;
                nanite.SetActive(false);
                popped++;
            }

            if (popped > 0)
                PlayNanitePopSound();
        }

        void EnsureNaniteMotionLists()
        {
            if (_naniteOffsets.Count != _naniteVelocities.Count ||
                _naniteRouteOffsets.Count != _naniteVelocities.Count ||
                _naniteHasPosition.Count != _naniteVelocities.Count)
            {
                ResetNaniteMotionState();
            }

            while (_naniteVelocities.Count < _nanites.Count)
                AddNaniteMotionSeed(_naniteVelocities.Count);
        }
    }
}
