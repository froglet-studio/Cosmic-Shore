using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        const float RedirectDurationSeconds = 3f;

        float _redirectTimer;
        Vector3 _redirectStartPosition;
        Vector3 _redirectStartVelocity;
        Quaternion _redirectStartRotation;
        Vector3 _lastVesselPosition;
        bool _hasLastVesselPosition;

        void AdvanceNanites(float dt, float throttleInput)
        {
            float pressure = naniteBaseSpeed + Intensity * naniteSpeedPerIntensity;
            if (throttleInput < 0.1f)
                pressure += (0.1f - throttleInput) * 7f;

            pressure += FinaleIntensity01 * 13f;
            _naniteRouteDistance += pressure * dt;
        }

        void UpdateOrbitThruster(float input, float dt)
        {
            float deadzoned = Mathf.Abs(input) >= 0.08f ? input : 0f;
            if (Mathf.Abs(deadzoned) > 0f)
            {
                float acceleration = Mathf.Max(orbitThrusterAcceleration, orbitDegreesPerSecond);
                _orbitAngularVelocity = Mathf.Clamp(
                    _orbitAngularVelocity + deadzoned * acceleration * dt,
                    -orbitMaxAngularVelocity,
                    orbitMaxAngularVelocity);

                int sign = deadzoned > 0f ? 1 : -1;
                if (_lastOrbitInputSign != 0 && sign != _lastOrbitInputSign && Time.time >= _nextNaniteDirectionBurstTime)
                {
                    BurstTrailingNanites();
                    _nextNaniteDirectionBurstTime = Time.time + naniteDirectionBurstCooldown;
                }

                _lastOrbitInputSign = sign;
            }
            else if (orbitAngularDrag > 0f)
            {
                _orbitAngularVelocity = Mathf.MoveTowards(_orbitAngularVelocity, 0f, orbitAngularDrag * dt);
            }

            _orbitAngle += _orbitAngularVelocity * dt;
        }

        void CompleteTransfer()
        {
            Vector3 startPosition = _vessel?.Transform.position ?? PositionOnFilament(CurrentFilament, _distanceOnFilament, _orbitAngle * Mathf.Deg2Rad, orbitRadius);
            Quaternion startRotation = _vessel != null ? _vessel.Transform.rotation : Quaternion.LookRotation(CurrentFilament.Direction, CurrentFilament.Up);
            Vector3 routeVelocity = CurrentFilament.Direction * _speed;
            if (_hasLastVesselPosition && Time.deltaTime > 0.0001f)
                routeVelocity = (startPosition - _lastVesselPosition) / Time.deltaTime;

            _currentFilamentIndex++;
            _successfulTransfers++;
            _distanceOnFilament = 0f;
            _speed = Mathf.Max(minimumSpeed, _speed * 0.82f);
            _orbitAngularVelocity *= Mathf.Clamp01(1f - transferAngularDamping);
            _swingTimer = Mathf.Lerp(0.55f, 1.2f, Mathf.InverseLerp(minimumSpeed, CurrentMaximumSpeed, _speed));
            BeginTransferRedirect(startPosition, startRotation, routeVelocity);
            ResetLatchTransferState();
            CSDebug.Log($"[BulkFilaments] Latch transfer {_successfulTransfers}/{_targetTransfers}.");

            if (_currentFilamentIndex >= _targetTransfers)
                FinishRun();
        }

        void BeginTransferRedirect(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            _redirectTimer = RedirectDurationSeconds;
            _redirectStartPosition = position;
            _redirectStartRotation = rotation;
            _redirectStartVelocity = Vector3.ClampMagnitude(velocity, CurrentMaximumSpeed * 1.35f);
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
            ResetLatchTransferState();

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
            Quaternion rotation = Quaternion.LookRotation(filament.Direction, up);

            if (_redirectTimer > 0f)
            {
                float t = 1f - Mathf.Clamp01(_redirectTimer / RedirectDurationSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                Vector3 control = _redirectStartPosition + _redirectStartVelocity * 1.05f;
                Vector3 a = Vector3.Lerp(_redirectStartPosition, control, eased);
                Vector3 b = Vector3.Lerp(control, position, eased);
                position = Vector3.Lerp(a, b, eased);
                position += up * UnderdampedVesselTetherOffset(t);
                rotation = Quaternion.Slerp(_redirectStartRotation, rotation, eased);
                _redirectTimer = Mathf.Max(0f, _redirectTimer - Time.deltaTime);
            }

            _vessel.SetPose(new Pose(position, rotation));
            _lastVesselPosition = position;
            _hasLastVesselPosition = true;
        }

        float UnderdampedVesselTetherOffset(float t)
        {
            t = Mathf.Clamp01(t);
            float pullTooClose = -orbitRadius * 0.46f * Mathf.Exp(-34f * (t - 0.62f) * (t - 0.62f));
            float rebound = orbitRadius * 0.16f * Mathf.Exp(-48f * (t - 0.82f) * (t - 0.82f));
            float dyingRing = orbitRadius * 0.05f * Mathf.Sin(t * Mathf.PI * 3.4f) * Mathf.Exp(-4.8f * t);
            return pullTooClose + rebound + dyingRing;
        }

        Vector3 AttachPoint(FilamentRuntime filament, float distance)
        {
            float axis01 = Mathf.Clamp01(distance / Mathf.Max(0.001f, filament.TravelLength));
            return CenterlinePoint(filament, distance) + FilamentWaveOffset(filament, axis01);
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

                crystal.Position = PositionOnFilament(filament, crystal.Distance, crystal.OrbitAngleRadians + Time.time * 0.42f, orbitRadius + 1.2f);
                crystal.GameObject.transform.position = crystal.Position;
                float pulse = 1f + BeatPulse() * 0.13f + _waveformEnergy * 0.1f;
                crystal.GameObject.transform.localScale = Vector3.one * (1.4f * speedDiamondScaleMultiplier * pulse);
                crystal.GameObject.transform.Rotate(0f, 140f * Time.deltaTime, 90f * Time.deltaTime);
                if (Vector3.Distance(vesselPosition, crystal.Position) > speedDiamondPickupRadius)
                    continue;

                crystal.Collected = true;
                crystal.GameObject.SetActive(false);
                _crystalsCollected++;
                ApplyPowerCrystalPickup(crystal.Position);
                if (gameData?.LocalRoundStats != null)
                    gameData.LocalRoundStats.CrystalsCollected = _crystalsCollected;
            }
        }

        void CheckHazardGraze()
        {
            if (_impactTimer > 0f || _vessel == null)
                return;

            Vector3 vesselPosition = _vessel.Transform.position;
            foreach (var hazardRuntime in _hazardRuntimes)
            {
                GameObject hazard = hazardRuntime.GameObject;
                if (!hazard || !hazard.activeSelf || hazardRuntime.Filament == null)
                    continue;

                hazard.transform.position = PositionOnFilament(
                    hazardRuntime.Filament,
                    hazardRuntime.Distance,
                    hazardRuntime.OrbitAngleRadians + Time.time * 0.18f,
                    orbitRadius + 2.8f);
                hazard.transform.Rotate(
                    20f * Time.deltaTime,
                    hazardRuntime.SpinDegreesPerSecond * Time.deltaTime,
                    35f * Time.deltaTime);
                if (Vector3.Distance(vesselPosition, hazard.transform.position) > 3.3f)
                    continue;

                _speed = Mathf.Max(minimumSpeed * 0.7f, _speed * 0.72f);
                _naniteRouteDistance += 8f;
                _impactTimer = 0.8f;
                break;
            }
        }

        void CheckPulseGatePassage()
        {
            if (_pulseGates.Count == 0 || _currentFilamentIndex >= _filaments.Count)
                return;

            for (int i = 0; i < _pulseGates.Count; i++)
            {
                PulseGateRuntime gate = _pulseGates[i];
                if (gate.Triggered || gate.Filament == null || gate.Filament.Index != _currentFilamentIndex)
                    continue;

                if (_distanceOnFilament < gate.Distance)
                    continue;

                gate.Triggered = true;
                gate.PulseTimer = 0.78f;
                _pulseGates[i] = gate;
                _crystalSpeedBonus += pulseGateStackBonus;
                _speed = Mathf.Clamp(_speed + pulseGateSpeedImpulse, minimumSpeed, CurrentMaximumSpeed);
                PlayPulseGateSound();
                SpawnPulseGateBurst(AttachPoint(gate.Filament, gate.Distance), gate.Filament);
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
