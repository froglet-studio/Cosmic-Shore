using System.Collections.Generic;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class SquirrelVignetteController : MonoBehaviour
    {
        [Header("Volume")]
        [Tooltip("Assign the Global Volume that has a Vignette override.")]
        [SerializeField] Volume _volume;

        [Header("Detection — Distance")]
        [SerializeField] float _warningRange = 50f;
        [SerializeField] float _dangerRange  = 20f;

        [Header("Detection — Direction")]
        [Tooltip("Dot-product cutoff: enemy forward must point this close to you. 0.7 ≈ within 45°.")]
        [SerializeField, Range(0f, 1f)] float _facingDotThreshold = 0.5f;

        [Header("Detection — Speed")]
        [Tooltip("Minimum closing speed (m/s) for the threat to register.")]
        [SerializeField] float _minClosingSpeed = 2f;

        [Header("Intensity")]
        [SerializeField, Range(0f, 1f)] float _warningIntensity = 0.8f;
        [SerializeField, Range(0f, 1f)] float _dangerIntensity  = 1f;
        [SerializeField] float _fadeInSpeed  = 4f;
        [SerializeField] float _fadeOutSpeed = 1.5f;
        [SerializeField] float _pulseSpeed   = 3f;

        [Header("Debug")]
        [Tooltip("Preview intensity without needing enemies nearby. 0 = off.")]
        [SerializeField, Range(0f, 1f)] float _previewIntensity = 0f;
        [Tooltip("Log threat evaluation every N seconds. 0 = off.")]
        [SerializeField] float _debugLogInterval = 2f;

        [Inject] GameDataSO _gameData;

        enum ThreatLevel { None, Warning, Danger }

        Vignette _vignette;
        Transform _localVesselTransform;
        Vector3 _prevLocalPos;
        Vector3 _localVelocity;
        readonly Dictionary<IPlayer, Vector3> _prevEnemyPositions = new();
        float _nextLogTime;

        void Start()
        {
            if (_volume == null)
            {
                Debug.LogError("[SquirrelVignetteController] No Volume assigned — wire the Global Volume in the inspector.");
                return;
            }

            if (!_volume.profile.TryGet(out _vignette))
            {
                _vignette = _volume.profile.Add<Vignette>(true);
                _vignette.active = true;
            }

            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0f;
        }

        void Update()
        {
            if (_vignette == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            ResolveLocalVessel();

            if (_previewIntensity > 0f)
            {
                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, _previewIntensity, _fadeInSpeed * dt);
                return;
            }

            if (_localVesselTransform == null)
            {
                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, 0f, _fadeOutSpeed * dt);
                return;
            }

            UpdateLocalVelocity(dt);

            ThreatLevel threat = GetThreatLevel(dt);

            float targetIntensity;
            float lerpSpeed;
            switch (threat)
            {
                case ThreatLevel.Danger:
                    float pulse = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
                    targetIntensity = Mathf.Lerp(_warningIntensity, _dangerIntensity, pulse);
                    lerpSpeed = _fadeInSpeed;
                    break;
                case ThreatLevel.Warning:
                    targetIntensity = _warningIntensity;
                    lerpSpeed = _fadeInSpeed;
                    break;
                default:
                    targetIntensity = 0f;
                    lerpSpeed = _fadeOutSpeed;
                    break;
            }

            _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, targetIntensity, lerpSpeed * dt);

            CacheEnemyPositions();
        }

        void ResolveLocalVessel()
        {
            if (_localVesselTransform != null) return;
            var lp = _gameData?.LocalPlayer;
            if (lp?.Vessel == null) return;
            _localVesselTransform = (lp.Vessel as MonoBehaviour)?.transform;
            if (_localVesselTransform != null)
                _prevLocalPos = _localVesselTransform.position;
        }

        void UpdateLocalVelocity(float dt)
        {
            var pos = _localVesselTransform.position;
            _localVelocity = (pos - _prevLocalPos) / dt;
            _prevLocalPos = pos;
        }

        ThreatLevel GetThreatLevel(float dt)
        {
            var localPlayer = _gameData?.LocalPlayer;
            bool doLog = _debugLogInterval > 0f && Time.time >= _nextLogTime;
            if (doLog) _nextLogTime = Time.time + _debugLogInterval;

            if (localPlayer == null)
            {
                if (doLog) Debug.Log("[Vignette] LocalPlayer is null");
                return ThreatLevel.None;
            }

            ThreatLevel highest = ThreatLevel.None;
            int enemyCount = 0;

            foreach (var player in _gameData.Players)
            {
                if (ReferenceEquals(player, localPlayer)) continue;
                if (player.Domain == localPlayer.Domain) continue;
                enemyCount++;

                var enemyT = (player.Vessel as MonoBehaviour)?.transform;
                if (enemyT == null) { if (doLog) Debug.Log("[Vignette] enemy vessel transform null"); continue; }

                Vector3 toLocal = _localVesselTransform.position - enemyT.position;
                float dist = toLocal.magnitude;

                if (dist > _warningRange || dist < 0.01f)
                {
                    if (doLog) Debug.Log($"[Vignette] enemy dist={dist:F1} out of range ({_warningRange})");
                    continue;
                }

                float facingDot = Vector3.Dot(enemyT.forward, toLocal / dist);
                if (facingDot < _facingDotThreshold)
                {
                    if (doLog) Debug.Log($"[Vignette] enemy dist={dist:F1} facingDot={facingDot:F2} below threshold {_facingDotThreshold}");
                    continue;
                }

                if (_prevEnemyPositions.TryGetValue(player, out var prevEnemyPos))
                {
                    Vector3 enemyVelocity = (enemyT.position - prevEnemyPos) / dt;
                    Vector3 relVelocity = enemyVelocity - _localVelocity;
                    // positive = enemy closing toward player
                    float closingSpeed = Vector3.Dot(relVelocity, toLocal / dist);
                    if (closingSpeed < _minClosingSpeed)
                    {
                        if (doLog) Debug.Log($"[Vignette] enemy dist={dist:F1} closingSpeed={closingSpeed:F1} below min {_minClosingSpeed}");
                        continue;
                    }
                    if (doLog) Debug.Log($"[Vignette] THREAT dist={dist:F1} facing={facingDot:F2} closing={closingSpeed:F1}");
                }

                ThreatLevel level = dist <= _dangerRange ? ThreatLevel.Danger : ThreatLevel.Warning;
                if (level == ThreatLevel.Danger) return ThreatLevel.Danger;
                highest = ThreatLevel.Warning;
            }

            if (doLog) Debug.Log($"[Vignette] enemies={enemyCount} threat={highest} vignette={_vignette?.intensity.value:F2}");
            return highest;
        }

        void CacheEnemyPositions()
        {
            _prevEnemyPositions.Clear();
            var localPlayer = _gameData?.LocalPlayer;
            if (localPlayer == null) return;
            foreach (var player in _gameData.Players)
            {
                if (ReferenceEquals(player, localPlayer)) continue;
                var vesselMb = player.Vessel as MonoBehaviour;
                if (vesselMb == null) continue;
                _prevEnemyPositions[player] = vesselMb.transform.position;
            }
        }
    }
}
