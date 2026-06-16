using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        void AdvanceNanites(float dt, float throttleInput)
        {
            float pressure = naniteBaseSpeed + Intensity * naniteSpeedPerIntensity;
            if (throttleInput < 0.1f)
                pressure += (0.1f - throttleInput) * 7f;

            _naniteRouteDistance += pressure * dt;
        }

        void TryTransferLatch()
        {
            if (_missTimer > 0f || _currentFilamentIndex >= _targetTransfers)
                return;

            float delta = Mathf.Abs(_distanceOnFilament - CurrentFilament.TransferDistance);
            if (delta <= CurrentLatchWindow())
            {
                CompleteTransfer();
                return;
            }

            _missTimer = missCooldown;
            _speed *= 0.94f;
            _impactTimer = 0.28f;
        }

        void CompleteTransfer()
        {
            _currentFilamentIndex++;
            _successfulTransfers++;
            _distanceOnFilament = 0f;
            _speed = Mathf.Max(minimumSpeed, _speed * 0.82f);
            _swingTimer = Mathf.Lerp(0.55f, 1.2f, Mathf.InverseLerp(minimumSpeed, maximumSpeed + Intensity * 4f, _speed));

            if (_currentFilamentIndex >= _targetTransfers)
                FinishRun();
        }

        void RespawnAtPreviousFilament(string reason)
        {
            _respawns++;
            _currentFilamentIndex = Mathf.Max(0, _currentFilamentIndex - 1);
            var filament = CurrentFilament;
            _distanceOnFilament = Mathf.Min(filament.TransferDistance * 0.28f, filament.TravelLength * 0.35f);
            _speed = minimumSpeed * 0.85f;
            _impactTimer = 1f;
            _missTimer = missCooldown;
            _naniteRouteDistance = Mathf.Min(_naniteRouteDistance + 12f, PlayerRouteDistance - naniteRespawnSetback);

            if (gameData?.LocalRoundStats != null)
                gameData.LocalRoundStats.Score += respawnTimePenalty;

            CSDebug.Log($"[BulkFilaments] Respawned after {reason}.");
        }

        void FinishRun()
        {
            if (_turnFinished)
                return;

            _turnFinished = true;
            _isRunning = false;
            UpdateRoundStats(final: true);
            gameData.InvokeGameTurnConditionsMet();
        }

        float CurrentLatchWindow()
        {
            float speed01 = Mathf.InverseLerp(minimumSpeed, maximumSpeed + Intensity * 4f, _speed);
            return Mathf.Lerp(slowSpeedLatchWindow, fastSpeedLatchWindow, speed01);
        }

        void UpdateVesselPose()
        {
            FilamentRuntime filament = CurrentFilament;
            Vector3 position = PositionOnFilament(filament, _distanceOnFilament, _orbitAngle * Mathf.Deg2Rad, orbitRadius);
            Vector3 swing = filament.Side * Mathf.Sin((1f - _swingTimer) * Mathf.PI * 3f) * (_swingTimer * _speed * 0.11f);
            position += swing;

            Vector3 up = (position - AttachPoint(filament, _distanceOnFilament)).normalized;
            if (up.sqrMagnitude < 0.01f)
                up = Vector3.up;

            _vessel.VesselStatus.IsStationary = true;
            _vessel.VesselStatus.Course = filament.Direction;
            _vessel.VesselStatus.Speed = _speed;
            _vessel.SetPose(new Pose(position, Quaternion.LookRotation(filament.Direction, up)));
        }

        Vector3 AttachPoint(FilamentRuntime filament, float distance)
        {
            float halfTravel = filament.Length * FilamentTravelRatio * 0.5f;
            float axisDistance = Mathf.Lerp(-halfTravel, halfTravel, Mathf.Clamp01(distance / filament.TravelLength));
            return filament.Center + filament.Direction * axisDistance;
        }

        FilamentRuntime FilamentAtRouteDistance(float routeDistance, out float localDistance)
        {
            if (_filaments.Count == 0)
            {
                localDistance = 0f;
                return null;
            }

            for (int i = _filaments.Count - 1; i >= 0; i--)
            {
                FilamentRuntime filament = _filaments[i];
                if (routeDistance < filament.RouteStartDistance)
                    continue;

                localDistance = Mathf.Clamp(routeDistance - filament.RouteStartDistance, 0f, filament.TravelLength);
                return filament;
            }

            localDistance = 0f;
            return _filaments[0];
        }

        Vector3 PositionOnFilament(FilamentRuntime filament, float distance, float orbitAngleRadians, float radius)
        {
            Vector3 attach = AttachPoint(filament, distance);
            Vector3 orbit = (filament.Up * Mathf.Cos(orbitAngleRadians) + filament.Side * Mathf.Sin(orbitAngleRadians)) * radius;
            return attach - filament.Up * tetherRise + orbit;
        }

        void CollectNearbyCrystals()
        {
            if (_currentFilamentIndex >= _filaments.Count)
                return;

            Vector3 vesselPosition = _vessel.Transform.position;
            var filament = _filaments[_currentFilamentIndex];
            foreach (var crystal in filament.Crystals)
            {
                if (crystal.Collected || !crystal.GameObject)
                    continue;

                crystal.GameObject.transform.Rotate(0f, 140f * Time.deltaTime, 90f * Time.deltaTime);
                if (Vector3.Distance(vesselPosition, crystal.Position) > 4.2f)
                    continue;

                crystal.Collected = true;
                crystal.GameObject.SetActive(false);
                _crystalsCollected++;
                if (gameData?.LocalRoundStats != null)
                    gameData.LocalRoundStats.CrystalsCollected = _crystalsCollected;
            }
        }

        void CheckHazardGraze()
        {
            if (_impactTimer > 0f || _vessel == null)
                return;

            Vector3 vesselPosition = _vessel.Transform.position;
            foreach (var hazard in _hazards)
            {
                if (!hazard || !hazard.activeSelf)
                    continue;

                hazard.transform.Rotate(20f * Time.deltaTime, 70f * Time.deltaTime, 35f * Time.deltaTime);
                if (Vector3.Distance(vesselPosition, hazard.transform.position) > 3.3f)
                    continue;

                _speed = Mathf.Max(minimumSpeed * 0.7f, _speed * 0.72f);
                _naniteRouteDistance += 8f;
                _impactTimer = 0.8f;
                break;
            }
        }

        void UpdateRoundStats(bool final = false)
        {
            if (gameData?.LocalRoundStats == null)
                return;

            float crystalCredit = _crystalsCollected * 2f;
            float respawnPenalty = _respawns * respawnTimePenalty;
            gameData.LocalRoundStats.CrystalsCollected = _crystalsCollected;
            gameData.LocalRoundStats.Score = Mathf.Max(0.01f, _elapsedTime + respawnPenalty - crystalCredit);

            if (final)
                gameData.LocalRoundStats.OmniCrystalsCollected = _successfulTransfers;
        }
    }
}
