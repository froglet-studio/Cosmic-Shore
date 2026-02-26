using System.Collections;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Soap;
using UnityEngine;
using Obvious.Soap;
using UnityEngine.Events;
using UnityEngine.InputSystem;

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

        [Header("SnowChanger")]
        [Tooltip("SnowChanger prefab that spawns directional shards pointing at the current crystal.")]
        [SerializeField] SnowChanger snowChangerPrefab;

        [Header("Visuals")]
        [SerializeField] LineRenderer guideLine;
        [SerializeField] LineRenderer ghostLine;
        [SerializeField] Camera revealCamera;
        [SerializeField] float shapeScale = 7f;

        [Header("Shape Orientation")]
        [Tooltip("Rotation applied to shape waypoints. Default (-90,0,0) rotates XY-defined shapes to the horizontal XZ plane.")]
        [SerializeField] Vector3 shapeOrientationEuler = new Vector3(-90f, 0f, 0f);

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

        [Header("Preview Cinematic")]
        [Tooltip("Seconds the camera holds on the top-down shape view.")]
        [SerializeField] float previewHoldTime = 2f;
        [Tooltip("Seconds for each camera transition (to overview and back to player).")]
        [SerializeField] float previewTransitionTime = 1.5f;

        [Header("Reveal")]
        [Tooltip("Duration of the camera pan from player to the reveal viewpoint.")]
        [SerializeField] float cameraPanDuration = 2f;

        [Header("Debug")]
        [Tooltip("Press this key to save a screenshot (PC only).")]
        [SerializeField] Key screenshotKey = Key.F12;

        [Header("Pool Return")]
        [Tooltip("Raised when exiting shape mode. Attach EventListenerNoParam on shape prisms to call ReturnToPool.")]
        [SerializeField] ScriptableEventNoParam onReturnShapePrismsEvent;

        [Header("Events")]
        public UnityEvent OnShapeCompleted;
        public UnityEvent OnFreestyleResumed;
        public UnityEvent<ShapeScoreData> OnScoreCalculated;
        [Tooltip("Fired after the camera pan completes. UI should show accuracy + Next button.")]
        public UnityEvent OnRevealStarted;
        [Tooltip("Fired after the shape preview cinematic finishes. Controller should show the Ready button.")]
        public UnityEvent OnPreviewComplete;

        ShapeDefinition _activeShape;
        Vector3 _shapeOrigin;
        int _currentWaypointIndex;
        bool _isActive;
        bool _waitingForNext;
        bool _drawingStarted;
        IVesselStatus _vesselStatus;
        CustomCameraController _panCameraController;
        SnowChanger _snowChangerInstance;

        // Scoring state
        float _shapeStartTime;
        readonly List<Vector3> _playerPathSamples = new();
        float _nextSampleTime;
        bool _trackingPath;

        // ── Shape Orientation Helpers ────────────────────────────────────────

        Quaternion ShapeRotation => Quaternion.Euler(shapeOrientationEuler);

        Vector3 GetWorldWaypoint(int index)
        {
            if (index < 0 || index >= _activeShape.waypoints.Count) return _shapeOrigin;
            return _shapeOrigin + ShapeRotation * (_activeShape.waypoints[index] * shapeScale);
        }

        /// <summary>
        /// Places the player just behind the first crystal, facing toward it.
        /// </summary>
        Vector3 GetWorldPlayerStart()
        {
            Vector3 wp0 = GetWorldWaypoint(0);

            if (_activeShape.waypoints.Count > 1)
            {
                // Direction the player will fly: from wp0 toward wp1
                Vector3 wp1 = GetWorldWaypoint(1);
                Vector3 pathDir = (wp0 - wp1).normalized;
                if (pathDir.sqrMagnitude < 0.001f) pathDir = Vector3.back;
                return wp0 + pathDir * 30f;
            }

            // Fallback: offset from origin toward wp0
            Vector3 dir = (_shapeOrigin - wp0).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.back;
            return wp0 + dir * 30f;
        }

        /// <summary>
        /// Player faces the first crystal from the start position.
        /// </summary>
        Quaternion GetPlayerStartRotation()
        {
            Vector3 playerPos = GetWorldPlayerStart();
            Vector3 wp0 = GetWorldWaypoint(0);
            Vector3 lookDir = (wp0 - playerPos).normalized;
            if (lookDir.sqrMagnitude < 0.001f) return Quaternion.identity;
            return Quaternion.LookRotation(lookDir, Vector3.up);
        }

        Vector3[] GetAllWorldWaypoints()
        {
            _activeShape.EnsureWaypoints();
            var rot = ShapeRotation;
            var result = new Vector3[_activeShape.waypoints.Count];
            for (int i = 0; i < _activeShape.waypoints.Count; i++)
                result[i] = _shapeOrigin + rot * (_activeShape.waypoints[i] * shapeScale);
            return result;
        }

        Vector3 GetRevealCameraPosition()
        {
            return _shapeOrigin +
                Quaternion.Euler(_activeShape.revealCameraEuler) *
                (Vector3.back * _activeShape.revealCameraDistance);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

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
            if (Keyboard.current != null && Keyboard.current[screenshotKey].wasPressedThisFrame)
                TakeDebugScreenshot();

            if (!_isActive || _activeShape == null || !_drawingStarted) return;
            UpdateGuideLine();
            SamplePlayerPosition();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public bool IsInShapeMode => _isActive;

        /// <summary>
        /// Phase 1: Setup, place player, play preview cinematic, then fire OnPreviewComplete.
        /// The controller should show the Ready button when OnPreviewComplete fires.
        /// </summary>
        public void StartShapeSequence(ShapeDefinition def, Vector3 origin)
        {
            def.EnsureWaypoints();

            _activeShape = def;
            _shapeOrigin = origin;
            _isActive = true;
            _drawingStarted = false;
            _currentWaypointIndex = 0;
            _waitingForNext = false;

            // Reset scoring
            _playerPathSamples.Clear();
            _trackingPath = false;

            // Cache vessel status via the vessel interface (NOT GetComponent)
            _vesselStatus = gameData.LocalPlayer?.Vessel?.VesselStatus;
            if (_vesselStatus != null)
            {
                _vesselStatus.VesselPrismController.StopSpawn();
                _vesselStatus.VesselPrismController.ClearTrails();
            }

            // Disable Cell to stop Lifeforms
            if (cellScript) cellScript.enabled = false;

            // Ensure Standard Manager is OFF
            if (localCrystalManager) localCrystalManager.enabled = false;

            // Enable Shape Manager
            if (shapeCrystalManager) shapeCrystalManager.enabled = true;
            shapeCrystalManager.DestroyAllCrystals();

            StartCoroutine(PreviewSequence());
        }

        /// <summary>
        /// Phase 2: Called by the controller after countdown ends.
        /// Releases input and begins the actual crystal-chasing drawing.
        /// </summary>
        public void BeginDrawing()
        {
            if (!_isActive || _drawingStarted) return;
            _drawingStarted = true;

            // Release input
            if (_vesselStatus != null)
                _vesselStatus.IsStationary = false;

            _shapeStartTime = Time.time;
            SpawnCrystal(_currentWaypointIndex);

            // Spawn SnowChanger shards after the first crystal exists
            SpawnSnowChanger();

            if (guideLine) guideLine.enabled = true;

            Debug.Log($"[ShapeDrawing] Drawing started. VesselStatus: {(_vesselStatus != null ? "OK" : "NULL")}");
        }

        /// <summary>
        /// Call from the UI Next button to clear trails, restore the camera,
        /// and return to the lobby.
        /// </summary>
        public void ContinueFromReveal()
        {
            if (!_waitingForNext) return;
            ExitShapeMode();
        }

        public void ExitShapeMode()
        {
            StopAllCoroutines();

            _isActive = false;
            _drawingStarted = false;
            _activeShape = null;
            _trackingPath = false;
            _waitingForNext = false;

            // Return shape prisms to pool via SO event (before clearing trail references)
            if (onReturnShapePrismsEvent) onReturnShapePrismsEvent.Raise();

            // Clear trail list references after prisms have been returned
            if (_vesselStatus != null)
                _vesselStatus.VesselPrismController.ClearTrails();

            // Destroy SnowChanger instance
            DestroySnowChanger();

            // Restore the player camera if we took it over
            if (_panCameraController)
            {
                _panCameraController.enabled = true;
                _panCameraController = null;
            }

            if (revealCamera) revealCamera.gameObject.SetActive(false);
            if (guideLine) guideLine.enabled = false;
            HideGhostShape();

            shapeCrystalManager.DestroyAllCrystals();
            shapeCrystalManager.enabled = false;

            OnFreestyleResumed?.Invoke();
        }

        // ── Preview Sequence (Phase 1) ───────────────────────────────────────

        IEnumerator PreviewSequence()
        {
            // Place player at shape start position, lock input
            yield return StartCoroutine(PlacePlayer());

            // Draw ghost shape outline
            DrawGhostShape();

            // Camera flies to shape overview, holds, then returns to player
            yield return StartCoroutine(ShapePreviewCinematic());

            // Fire event — controller shows ready button
            OnPreviewComplete?.Invoke();
        }

        IEnumerator PlacePlayer()
        {
            if (_vesselStatus == null) yield break;

            Vector3 startPos = GetWorldPlayerStart();
            Quaternion startRot = GetPlayerStartRotation();

            _vesselStatus.IsStationary = true;
            _vesselStatus.Vessel.Transform.SetPositionAndRotation(startPos, startRot);

            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator ShapePreviewCinematic()
        {
            Transform camTransform = AcquireCamera();
            if (!camTransform) yield break;

            Vector3 camStart = camTransform.position;
            Quaternion rotStart = camTransform.rotation;

            Vector3 revealPos = GetRevealCameraPosition();
            Quaternion revealRot = Quaternion.Euler(_activeShape.revealCameraEuler);

            // Fly to shape overview
            float elapsed = 0f;
            while (elapsed < previewTransitionTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / previewTransitionTime));
                camTransform.position = Vector3.Lerp(camStart, revealPos, t);
                camTransform.rotation = Quaternion.Slerp(rotStart, revealRot, t);
                yield return null;
            }

            // Hold on the shape overview
            yield return new WaitForSeconds(previewHoldTime);

            // Transition back to behind the player
            Vector3 playerPos = _vesselStatus != null
                ? _vesselStatus.Vessel.Transform.position
                : GetWorldPlayerStart();
            Vector3 playerFwd = _vesselStatus != null
                ? _vesselStatus.Vessel.Transform.forward
                : Vector3.forward;
            Vector3 behindPlayer = playerPos - playerFwd * 30f + Vector3.up * 10f;

            Vector3 lookDir = playerPos - behindPlayer;
            Quaternion behindRot = lookDir.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDir, Vector3.up)
                : revealRot;

            elapsed = 0f;
            while (elapsed < previewTransitionTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / previewTransitionTime));

                if (_vesselStatus != null)
                {
                    playerPos = _vesselStatus.Vessel.Transform.position;
                    playerFwd = _vesselStatus.Vessel.Transform.forward;
                    behindPlayer = playerPos - playerFwd * 30f + Vector3.up * 10f;
                    lookDir = playerPos - behindPlayer;
                    if (lookDir.sqrMagnitude > 0.001f)
                        behindRot = Quaternion.LookRotation(lookDir, Vector3.up);
                }

                camTransform.position = Vector3.Lerp(revealPos, behindPlayer, t);
                camTransform.rotation = Quaternion.Slerp(revealRot, behindRot, t);
                yield return null;
            }

            ReleaseCamera();
        }

        // ── Drawing Logic ────────────────────────────────────────────────────

        void SpawnCrystal(int index)
        {
            if (index >= _activeShape.waypoints.Count)
            {
                FinishShape();
                return;
            }

            int crystalId = index + 1;
            Vector3 pos = GetWorldWaypoint(index);

            shapeCrystalManager.SpawnAtPosition(crystalId, pos);
        }

        void HandleCrystalHit(int crystalId)
        {
            if (!_isActive || !_drawingStarted) return;

            int waypointIndex = crystalId - 1;
            if (waypointIndex != _currentWaypointIndex) return;

            // Start trails and path tracking after hitting the first crystal
            if (_currentWaypointIndex == 0 && _vesselStatus != null)
            {
                _vesselStatus.VesselPrismController.StartSpawn();
                _trackingPath = true;
                _nextSampleTime = Time.time;
                Debug.Log("[ShapeDrawing] First crystal hit — tracking started.");
            }

            // Toggle trail based on shape data
            if (_vesselStatus != null)
            {
                if (_activeShape.IsTrailEnabledForSegment(_currentWaypointIndex))
                    _vesselStatus.VesselPrismController.StartSpawn();
                else
                    _vesselStatus.VesselPrismController.StopSpawn();
            }

            _currentWaypointIndex++;
            SpawnCrystal(_currentWaypointIndex);
        }

        // ── SnowChanger ─────────────────────────────────────────────────────

        void SpawnSnowChanger()
        {
            if (!snowChangerPrefab) return;

            DestroySnowChanger();

            _snowChangerInstance = Instantiate(snowChangerPrefab, _shapeOrigin, Quaternion.identity);
            _snowChangerInstance.Initialize();
        }

        void DestroySnowChanger()
        {
            if (_snowChangerInstance)
            {
                Destroy(_snowChangerInstance.gameObject);
                _snowChangerInstance = null;
            }
        }

        // ── Guide Line ──────────────────────────────────────────────────────

        void UpdateGuideLine()
        {
            if (!guideLine || _vesselStatus == null || _currentWaypointIndex >= _activeShape.waypoints.Count)
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

            var worldPoints = GetAllWorldWaypoints();

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

            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader)
            {
                lr.material = new Material(shader);
                lr.material.color = color;
            }
        }

        // ── Camera Helpers ──────────────────────────────────────────────────

        Transform AcquireCamera()
        {
            _panCameraController = null;
            Transform camTransform = null;

            if (CameraManager.Instance)
            {
                var ctrl = CameraManager.Instance.GetActiveController();
                if (ctrl is CustomCameraController pcc)
                {
                    _panCameraController = pcc;
                    camTransform = pcc.transform;
                    pcc.enabled = false;
                }
            }

            if (camTransform == null && Camera.main)
                camTransform = Camera.main.transform;

            return camTransform;
        }

        void ReleaseCamera()
        {
            if (_panCameraController)
            {
                _panCameraController.enabled = true;
                _panCameraController.SnapToTarget();
                _panCameraController = null;
            }
        }

        // ── Scoring ─────────────────────────────────────────────────────────

        void SamplePlayerPosition()
        {
            if (!_trackingPath || _vesselStatus == null) return;
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

            var worldWaypoints = GetAllWorldWaypoints();

            float totalAccuracy = 0f;
            int validSamples = 0;

            foreach (var sample in _playerPathSamples)
            {
                float minDist = float.MaxValue;

                for (int i = 0; i < worldWaypoints.Length - 1; i++)
                {
                    if (!_activeShape.IsTrailEnabledForSegment(i + 1) &&
                        !_activeShape.IsTrailEnabledForSegment(i))
                        continue;

                    float dist = DistanceToSegment(sample, worldWaypoints[i], worldWaypoints[i + 1]);
                    if (dist < minDist) minDist = dist;
                }

                if (minDist < float.MaxValue)
                {
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
            if (_vesselStatus != null) _vesselStatus.VesselPrismController.StopSpawn();

            var score = CalculateScore();

            Debug.Log($"[ShapeDrawing] ── Shape Complete ──────────────────────\n" +
                      $"  Shape:        {score.ShapeName}\n" +
                      $"  Elapsed:      {score.ElapsedTime:F2}s  (par {_activeShape.parTime:F1}s)\n" +
                      $"  Accuracy:     {score.AccuracyPercent:F1}%\n" +
                      $"  Stars:        {score.StarRating}/5\n" +
                      $"  Path samples: {_playerPathSamples.Count}\n" +
                      $"  Waypoints:    {_activeShape.waypoints.Count}\n" +
                      $"───────────────────────────────────────────");

            StartCoroutine(RevealSequence(score));
        }

        IEnumerator RevealSequence(ShapeScoreData score)
        {
            OnShapeCompleted?.Invoke();
            OnScoreCalculated?.Invoke(score);

            HideGhostShape();
            DestroySnowChanger();

            Transform camTransform = AcquireCamera();

            if (camTransform && _activeShape != null)
            {
                Vector3 startPos = camTransform.position;
                Quaternion startRot = camTransform.rotation;

                Vector3 targetPos = GetRevealCameraPosition();
                Quaternion targetRot = Quaternion.Euler(_activeShape.revealCameraEuler);

                float elapsed = 0f;
                while (elapsed < cameraPanDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cameraPanDuration));
                    camTransform.position = Vector3.Lerp(startPos, targetPos, t);
                    camTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }

            _waitingForNext = true;
            OnRevealStarted?.Invoke();
        }

        // ── Debug Screenshot ─────────────────────────────────────────────

        /// <summary>
        /// Captures a screenshot and saves it to the persistent data path.
        /// Wire to a UI button or press screenshotKey (default F12).
        /// </summary>
        public void TakeDebugScreenshot()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filePath = Path.Combine(folder, $"Shape_{timestamp}.png");
            ScreenCapture.CaptureScreenshot(filePath);
            Debug.Log($"[ShapeDrawing] Screenshot saved: {filePath}");
        }
    }
}
