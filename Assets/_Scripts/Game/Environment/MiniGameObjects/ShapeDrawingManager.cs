using System.Collections;
using System.Collections.Generic;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.Events;

namespace CosmicShore.Game.ShapeDrawing
{
    public class ShapeDrawingManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] ShapeDrawingCrystalManager shapeCrystalManager;
        [SerializeField] LocalCrystalManager localCrystalManager;
        [SerializeField] Cell cellScript;
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Visuals")]
        [SerializeField] LineRenderer guideLine;
        [SerializeField] LineRenderer ghostLine;
        [SerializeField] Camera revealCamera;
        [SerializeField] float shapeScale = 1f;

        [Header("Guide Line Style")]
        [SerializeField] float guideLineWidth = 1.5f;
        [SerializeField] Color guideLineColor = new Color(0f, 1f, 1f, 0.8f);

        [Header("Ghost Line Style")]
        [SerializeField] float ghostLineWidth = 1f;
        [SerializeField] Color ghostLineColor = new Color(1f, 1f, 1f, 0.2f);

        [Header("Scoring")]
        [Tooltip("How often (in seconds) to sample the player position for accuracy scoring.")]
        [SerializeField] float positionSampleInterval = 0.15f;
        [Tooltip("Maximum distance (units) from ideal path that still counts as 100% accurate for that sample.")]
        [SerializeField] float perfectDistanceThreshold = 15f;
        [Tooltip("Distance at which accuracy drops to 0% for that sample.")]
        [SerializeField] float zeroAccuracyDistance = 80f;

        [Header("Reveal")]
        [Tooltip("How long the reveal camera stays active before returning to lobby.")]
        [SerializeField] float revealDuration = 5f;

        [Header("Events")]
        public UnityEvent OnShapeCompleted;
        public UnityEvent OnFreestyleResumed;
        public UnityEvent<ShapeScoreData> OnScoreCalculated;

        ShapeDefinition _activeShape;
        Vector3 _shapeOrigin;
        int _currentWaypointIndex;
        bool _isActive;
        VesselStatus _vesselStatus;

        // Scoring state
        float _shapeStartTime;
        readonly List<Vector3> _playerPathSamples = new();
        float _nextSampleTime;
        bool _trackingPath;

        void Awake()
        {
            EnsureGuideLine();
            EnsureGhostLine();
        }

        void OnEnable()
        {
            if (shapeCrystalManager)
                shapeCrystalManager.OnWaypointCrystalHit += HandleCrystalHit;
        }

        void OnDisable()
        {
            if (shapeCrystalManager)
                shapeCrystalManager.OnWaypointCrystalHit -= HandleCrystalHit;
        }

        void Update()
        {
            if (!_isActive || _activeShape == null) return;
            UpdateGuideLine();
            SamplePlayerPosition();
        }

        public bool IsInShapeMode => _isActive;

        public void StartShapeSequence(ShapeDefinition def, Vector3 origin)
        {
            def.EnsureWaypoints();

            _activeShape = def;
            _shapeOrigin = origin;
            _isActive = true;
            _currentWaypointIndex = 0;

            // Reset scoring
            _playerPathSamples.Clear();
            _trackingPath = false;

            // Disable Cell to stop Lifeforms
            if (cellScript) cellScript.enabled = false;

            // Ensure Standard Manager is OFF
            if (localCrystalManager) localCrystalManager.enabled = false;

            // Enable Shape Manager
            if (shapeCrystalManager) shapeCrystalManager.enabled = true;
            shapeCrystalManager.DestroyAllCrystals();

            StartCoroutine(SequenceRoutine());
        }

        IEnumerator SequenceRoutine()
        {
            // Get Vessel
            _vesselStatus = gameData.LocalPlayer?.Vessel?.Transform?.GetComponent<VesselStatus>();

            if (_vesselStatus)
            {
                _vesselStatus.IsStationary = false;
                _vesselStatus.VesselPrismController.StopSpawn();
                _vesselStatus.VesselPrismController.ClearTrails();
            }

            // Draw ghost shape outline
            DrawGhostShape();

            // Position Player
            yield return StartCoroutine(PlacePlayer());

            // Start timing
            _shapeStartTime = Time.time;

            // Spawn First Crystal
            SpawnCrystal(_currentWaypointIndex);

            // Enable Line Renderer
            if (guideLine) guideLine.enabled = true;
        }

        IEnumerator PlacePlayer()
        {
            if (!_vesselStatus) yield break;

            Vector3 startPos = _activeShape.GetWorldPlayerStart(_shapeOrigin, shapeScale);
            Quaternion startRot = Quaternion.Euler(_activeShape.playerStartEuler);

            _vesselStatus.IsStationary = true;
            _vesselStatus.Vessel.Transform.SetPositionAndRotation(startPos, startRot);

            yield return new WaitForSeconds(0.5f);

            _vesselStatus.IsStationary = false;
        }

        void SpawnCrystal(int index)
        {
            if (index >= _activeShape.waypoints.Count)
            {
                FinishShape();
                return;
            }

            int crystalId = index + 1;
            Vector3 pos = _activeShape.GetWorldWaypoint(index, _shapeOrigin, shapeScale);

            shapeCrystalManager.SpawnAtPosition(crystalId, pos);
        }

        void HandleCrystalHit(int crystalId)
        {
            if (!_isActive) return;

            int waypointIndex = crystalId - 1;
            if (waypointIndex != _currentWaypointIndex) return;

            // Start trails and path tracking after hitting the first crystal
            if (_currentWaypointIndex == 0 && _vesselStatus)
            {
                _vesselStatus.VesselPrismController.StartSpawn();
                _trackingPath = true;
                _nextSampleTime = Time.time;
            }

            // Toggle trail based on shape data
            if (_activeShape.IsTrailEnabledForSegment(_currentWaypointIndex))
                _vesselStatus.VesselPrismController.StartSpawn();
            else
                _vesselStatus.VesselPrismController.StopSpawn();

            _currentWaypointIndex++;
            SpawnCrystal(_currentWaypointIndex);
        }

        // ── Guide Line ──────────────────────────────────────────────────────

        void UpdateGuideLine()
        {
            if (!guideLine || !_vesselStatus || _currentWaypointIndex >= _activeShape.waypoints.Count)
            {
                if (guideLine) guideLine.enabled = false;
                return;
            }

            int targetId = _currentWaypointIndex + 1;
            if (cellData.TryGetCrystalById(targetId, out var crystal) && crystal != null)
            {
                guideLine.enabled = true;
                guideLine.SetPosition(0, _vesselStatus.Vessel.Transform.position);
                guideLine.SetPosition(1, crystal.transform.position);
            }
            else
            {
                guideLine.enabled = false;
            }
        }

        // ── Ghost Shape ─────────────────────────────────────────────────────

        void DrawGhostShape()
        {
            if (!ghostLine || _activeShape == null) return;

            var worldPoints = _activeShape.GetAllWorldWaypoints(_shapeOrigin, shapeScale);

            // Build a continuous polyline, but handle pen-up segments by inserting breaks.
            // For simplicity, draw all waypoints as one polyline — pen-up segments still show
            // as faint dashed connections (the ghost is just a guide).
            ghostLine.positionCount = worldPoints.Length;
            ghostLine.SetPositions(worldPoints);
            ghostLine.enabled = true;
            ghostLine.loop = false;
        }

        void HideGhostShape()
        {
            if (!ghostLine) return;
            ghostLine.enabled = false;
            ghostLine.positionCount = 0;
        }

        // ── LineRenderer Auto-Creation ───────────────────────────────────────

        void EnsureGuideLine()
        {
            if (guideLine) return;

            var go = new GameObject("GuideLine");
            go.transform.SetParent(transform);
            guideLine = go.AddComponent<LineRenderer>();
            ConfigureLineRenderer(guideLine, guideLineColor, guideLineWidth, 2);
            guideLine.enabled = false;
        }

        void EnsureGhostLine()
        {
            if (ghostLine) return;

            var go = new GameObject("GhostLine");
            go.transform.SetParent(transform);
            ghostLine = go.AddComponent<LineRenderer>();
            ConfigureLineRenderer(ghostLine, ghostLineColor, ghostLineWidth, 0);
            ghostLine.enabled = false;
        }

        static void ConfigureLineRenderer(LineRenderer lr, Color color, float width, int positionCount)
        {
            lr.useWorldSpace = true;
            lr.positionCount = positionCount;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // Use a default sprite material so it shows up without a custom material
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.material.color = color;
        }

        // ── Scoring ─────────────────────────────────────────────────────────

        void SamplePlayerPosition()
        {
            if (!_trackingPath || !_vesselStatus) return;
            if (Time.time < _nextSampleTime) return;

            _nextSampleTime = Time.time + positionSampleInterval;
            _playerPathSamples.Add(_vesselStatus.Vessel.Transform.position);
        }

        ShapeScoreData CalculateScore()
        {
            float elapsed = Time.time - _shapeStartTime;
            float accuracy = CalculateAccuracy();

            return new ShapeScoreData(
                _activeShape.shapeName,
                elapsed,
                _activeShape.parTime,
                accuracy
            );
        }

        float CalculateAccuracy()
        {
            if (_playerPathSamples.Count == 0 || _activeShape.waypoints.Count < 2) return 0f;

            // Build the ideal path segments in world space
            var worldWaypoints = _activeShape.GetAllWorldWaypoints(_shapeOrigin, shapeScale);

            float totalAccuracy = 0f;
            int validSamples = 0;

            foreach (var sample in _playerPathSamples)
            {
                // Find minimum distance from sample to any ideal path segment
                float minDist = float.MaxValue;

                for (int i = 0; i < worldWaypoints.Length - 1; i++)
                {
                    // Skip pen-up segments (trail disabled while moving to next)
                    if (!_activeShape.IsTrailEnabledForSegment(i + 1) &&
                        !_activeShape.IsTrailEnabledForSegment(i))
                        continue;

                    float dist = DistanceToSegment(sample, worldWaypoints[i], worldWaypoints[i + 1]);
                    if (dist < minDist) minDist = dist;
                }

                if (minDist < float.MaxValue)
                {
                    // Map distance to 0-1 accuracy score
                    float sampleAccuracy;
                    if (minDist <= perfectDistanceThreshold)
                        sampleAccuracy = 1f;
                    else if (minDist >= zeroAccuracyDistance)
                        sampleAccuracy = 0f;
                    else
                        sampleAccuracy = 1f - (minDist - perfectDistanceThreshold) /
                                         (zeroAccuracyDistance - perfectDistanceThreshold);

                    totalAccuracy += sampleAccuracy;
                    validSamples++;
                }
            }

            return validSamples > 0 ? (totalAccuracy / validSamples) * 100f : 0f;
        }

        static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < 0.0001f) return Vector3.Distance(point, a);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / sqrLen);
            Vector3 closest = a + t * ab;
            return Vector3.Distance(point, closest);
        }

        // ── Finish & Reveal ─────────────────────────────────────────────────

        void FinishShape()
        {
            _trackingPath = false;
            if (guideLine) guideLine.enabled = false;
            if (_vesselStatus) _vesselStatus.VesselPrismController.StopSpawn();

            var score = CalculateScore();

            Debug.Log($"[ShapeDrawing] Shape '{score.ShapeName}' completed! " +
                      $"Time: {score.ElapsedTime:F1}s, Accuracy: {score.AccuracyPercent:F0}%, " +
                      $"Stars: {score.StarRating}/5");

            StartCoroutine(RevealSequence(score));
        }

        IEnumerator RevealSequence(ShapeScoreData score)
        {
            OnShapeCompleted?.Invoke();
            OnScoreCalculated?.Invoke(score);

            // Hide the ghost shape — the player's trail IS the shape now
            HideGhostShape();

            if (revealCamera)
            {
                // Position camera for a top-down view of the completed shape
                revealCamera.transform.position = _shapeOrigin +
                    Quaternion.Euler(_activeShape.revealCameraEuler) * (Vector3.back * _activeShape.revealCameraDistance);
                revealCamera.transform.rotation = Quaternion.Euler(_activeShape.revealCameraEuler);
                revealCamera.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(revealDuration);
            ExitShapeMode();
        }

        public void ExitShapeMode()
        {
            _isActive = false;
            _activeShape = null;
            _trackingPath = false;

            if (revealCamera) revealCamera.gameObject.SetActive(false);
            if (guideLine) guideLine.enabled = false;
            HideGhostShape();

            shapeCrystalManager.DestroyAllCrystals();
            shapeCrystalManager.enabled = false;

            OnFreestyleResumed?.Invoke();
        }
    }
}
