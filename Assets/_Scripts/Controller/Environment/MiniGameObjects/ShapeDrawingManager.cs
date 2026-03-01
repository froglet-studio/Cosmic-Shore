using System.Collections;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using Obvious.Soap;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    public class ShapeDrawingManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] ShapeDrawingCrystalManager shapeCrystalManager;
        [SerializeField] LocalCrystalManager localCrystalManager;
        [SerializeField] Cell cellScript;
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;
        [SerializeField] Button pauseButton;

        [Header("SnowChanger")]
        [Tooltip("SnowChanger prefab that spawns directional shards pointing at the current crystal.")]
        [SerializeField] SnowChanger snowChangerPrefab;

        [Header("Visuals")]
        [SerializeField] LineRenderer guideLine;
        [SerializeField] LineRenderer ghostLine;
        [SerializeField] Camera revealCamera;
        [SerializeField] float shapeScale = 10f;

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
        [Tooltip("Fraction of average segment length that counts as 100% accurate. 0.02 = within 2% of segment length.")]
        [SerializeField] float perfectDistanceFraction = 0.02f;
        [Tooltip("Fraction of average segment length at which accuracy drops to 0%. 0.15 = 15% of segment length.")]
        [SerializeField] float zeroAccuracyFraction = 0.15f;

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

        [Header("Prism Keeping")]
        [Tooltip("Duration of the shrink+reposition animation when keeping drawn prisms.")]
        [SerializeField] float prismShrinkDuration = 1.5f;
        [Tooltip("Final scale multiplier for kept prisms (relative to original). 0.15 = 15% of original size.")]
        [SerializeField] float prismShrinkScale = 0.15f;

        [Header("End Shape HUD")]
        [Tooltip("UI panel shown after completing a shape. Displays stats, screenshot and exit buttons.")]
        [SerializeField] EndShapeDetailHUD endShapeHUD;

        [Header("Pool Return")]
        [Tooltip("Raised when entering shape mode. Attach EventListenerNoParam on prisms to call ReturnToPool for a clean slate.")]
        [SerializeField] ScriptableEventNoParam onShapeGameModeStarted;
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
        readonly List<(Vector3 position, int segmentIndex)> _playerPathSamples = new();
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
            if (endShapeHUD)
            {
                endShapeHUD.OnExitPressed += HandleExitFromHUD;
                endShapeHUD.OnScreenshotPressed += TakeDebugScreenshot;
                endShapeHUD.Hide();
            }
        }

        void OnDisable()
        {
            if (shapeCrystalManager)
                shapeCrystalManager.OnWaypointCrystalHit -= HandleCrystalHit;
            if (endShapeHUD)
            {
                endShapeHUD.OnExitPressed -= HandleExitFromHUD;
                endShapeHUD.OnScreenshotPressed -= TakeDebugScreenshot;
            }
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

            // Return existing prisms to pool via SO event — clean slate for shape mode
            if (onShapeGameModeStarted) onShapeGameModeStarted.Raise();

            // Freeze player — Ready button will release via IsStationary = false
            if (_vesselStatus != null)
                _vesselStatus.IsStationary = true;

            // Disable pause during shape drawing
            if (pauseButton) pauseButton.interactable = false;

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

            // Release player — vessel's natural prism spawning takes over
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
        /// Called by EndShapeDetailHUD exit button via event.
        /// </summary>
        void HandleExitFromHUD()
        {
            if (endShapeHUD) endShapeHUD.Hide();
            ExitShapeMode();
        }

        public void ContinueFromReveal()
        {
            if (!_waitingForNext) return;
            if (endShapeHUD) endShapeHUD.Hide();
            ExitShapeMode();
        }

        public void ExitShapeMode()
        {
            StopAllCoroutines();

            _isActive = false;
            _drawingStarted = false;
            _trackingPath = false;
            _waitingForNext = false;

            // Capture player-drawn prisms BEFORE returning anything to pool
            var capturedPrisms = CapturePlayerPrisms();

            // Return remaining shape prisms to pool via SO event
            if (onReturnShapePrismsEvent) onReturnShapePrismsEvent.Raise();

            // Animate captured prisms into the original shape outline
            if (capturedPrisms.Count > 0 && _activeShape != null)
                StartCoroutine(ShrinkPrismsIntoShape(capturedPrisms, _activeShape, _shapeOrigin));

            _activeShape = null;

            // Destroy SnowChanger instance
            DestroySnowChanger();

            // Restore the player camera if we took it over
            if (_panCameraController)
            {
                _panCameraController.enabled = true;
                _panCameraController.SnapToTarget();
                _panCameraController = null;
            }

            if (revealCamera) revealCamera.gameObject.SetActive(false);
            if (guideLine) guideLine.enabled = false;
            HideGhostShape();

            shapeCrystalManager.DestroyAllCrystals();
            shapeCrystalManager.enabled = false;

            // Restore vessel HUD and pause button
            _vesselStatus?.VesselHUDController?.ShowHUD();
            if (pauseButton) pauseButton.interactable = true;

            OnFreestyleResumed?.Invoke();
        }

        // ── Prism Keeping ────────────────────────────────────────────────────

        /// <summary>
        /// Captures all player-drawn prisms from VesselPrismController trails,
        /// detaches them from the pool system so they won't be returned.
        /// </summary>
        List<Prism> CapturePlayerPrisms()
        {
            var result = new List<Prism>();
            var prismController = _vesselStatus?.VesselPrismController;
            if (prismController == null) return result;

            // Collect from both trails
            foreach (var prism in prismController.Trail.TrailList)
            {
                if (prism != null && prism.gameObject != null)
                    result.Add(prism);
            }

            // Detach each prism from pool system
            foreach (var prism in result)
            {
                // Null out pool callback so ReturnToPool() becomes a no-op
                prism.OnReturnToPool = null;

                // Disable EventListenerNoParam components so SO events don't return them
                var listeners = prism.GetComponents<EventListenerNoParam>();
                foreach (var l in listeners)
                    l.enabled = false;
            }

            return result;
        }

        /// <summary>
        /// Animates captured prisms: shrinks them and repositions to form the original shape outline.
        /// </summary>
        IEnumerator ShrinkPrismsIntoShape(List<Prism> prisms, ShapeDefinition shape, Vector3 origin)
        {
            if (prisms.Count == 0) yield break;

            // Create a persistent container for the miniature shape
            var container = new GameObject($"CompletedShape_{shape.shapeName}");
            container.transform.position = origin;

            // Calculate target positions along the shape outline
            var shapeRotation = Quaternion.Euler(shapeOrientationEuler);
            shape.EnsureWaypoints();
            var waypoints = shape.waypoints;
            if (waypoints == null || waypoints.Count < 2) yield break;

            // Build world-space shape path segments and total length
            var worldPoints = new Vector3[waypoints.Count];
            for (int i = 0; i < waypoints.Count; i++)
                worldPoints[i] = origin + shapeRotation * (waypoints[i] * shapeScale * prismShrinkScale);

            float totalLength = 0f;
            var segLengths = new float[worldPoints.Length - 1];
            for (int i = 0; i < worldPoints.Length - 1; i++)
            {
                segLengths[i] = Vector3.Distance(worldPoints[i], worldPoints[i + 1]);
                totalLength += segLengths[i];
            }

            // Distribute prisms evenly along the shape path
            var targetPositions = new Vector3[prisms.Count];
            for (int i = 0; i < prisms.Count; i++)
            {
                float dist = (float)i / prisms.Count * totalLength;
                float accumulated = 0f;
                int seg = 0;
                for (seg = 0; seg < segLengths.Length - 1; seg++)
                {
                    if (accumulated + segLengths[seg] > dist) break;
                    accumulated += segLengths[seg];
                }
                float frac = segLengths[seg] > 0f ? (dist - accumulated) / segLengths[seg] : 0f;
                targetPositions[i] = Vector3.Lerp(worldPoints[seg], worldPoints[Mathf.Min(seg + 1, worldPoints.Length - 1)], frac);
            }

            // Record starting state
            var startPositions = new Vector3[prisms.Count];
            var startScales = new Vector3[prisms.Count];
            for (int i = 0; i < prisms.Count; i++)
            {
                if (!prisms[i]) continue;
                startPositions[i] = prisms[i].transform.position;
                startScales[i] = prisms[i].transform.localScale;
            }

            var targetScale = Vector3.one * prismShrinkScale;

            // Animate over duration
            float elapsed = 0f;
            while (elapsed < prismShrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / prismShrinkDuration));

                for (int i = 0; i < prisms.Count; i++)
                {
                    if (!prisms[i]) continue;
                    prisms[i].transform.position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                    prisms[i].transform.localScale = Vector3.Lerp(startScales[i], targetScale, t);
                }

                yield return null;
            }

            // Reparent to container for clean hierarchy
            foreach (var prism in prisms)
            {
                if (prism) prism.transform.SetParent(container.transform);
            }
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

            // Start path tracking after hitting the first crystal
            if (_currentWaypointIndex == 0)
            {
                _trackingPath = true;
                _nextSampleTime = Time.time;
                Debug.Log("[ShapeDrawing] First crystal hit — tracking started.");
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
            _playerPathSamples.Add((_vesselStatus.Vessel.Transform.position, _currentWaypointIndex));
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
            int waypointCount = worldWaypoints.Length;

            // Compute average segment length — thresholds scale with the shape's size
            float totalSegLength = 0f;
            int segCount = 0;
            for (int i = 0; i < waypointCount - 1; i++)
            {
                totalSegLength += Vector3.Distance(worldWaypoints[i], worldWaypoints[i + 1]);
                segCount++;
            }
            float avgSegLength = segCount > 0 ? totalSegLength / segCount : 100f;
            float perfectThreshold = avgSegLength * perfectDistanceFraction;
            float zeroThreshold = avgSegLength * zeroAccuracyFraction;

            float totalAccuracy = 0f;
            int validSamples = 0;

            foreach (var (samplePos, segIdx) in _playerPathSamples)
            {
                float minDist = float.MaxValue;

                // Only check the segment the player was on and its immediate neighbors.
                // segIdx is the NEXT waypoint the player was heading toward.
                // The relevant segment is [segIdx-1 → segIdx], plus neighbors for tolerance.
                int segFrom = Mathf.Max(0, segIdx - 2);
                int segTo = Mathf.Min(waypointCount - 2, segIdx);

                for (int i = segFrom; i <= segTo; i++)
                {
                    if (!_activeShape.IsTrailEnabledForSegment(i + 1) &&
                        !_activeShape.IsTrailEnabledForSegment(i))
                        continue;

                    float dist = DistanceToSegment(samplePos, worldWaypoints[i], worldWaypoints[i + 1]);
                    if (dist < minDist) minDist = dist;
                }

                if (minDist < float.MaxValue)
                {
                    float sampleAccuracy;
                    if (minDist <= perfectThreshold)
                        sampleAccuracy = 1f;
                    else if (minDist >= zeroThreshold)
                        sampleAccuracy = 0f;
                    else
                        sampleAccuracy = 1f - (minDist - perfectThreshold) /
                                         (zeroThreshold - perfectThreshold);

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

            // Hide vessel HUD, show end-of-shape detail HUD with stats
            _vesselStatus?.VesselHUDController?.HideHUD();
            if (endShapeHUD) endShapeHUD.Show(score);
        }

        // ── Debug Screenshot ─────────────────────────────────────────────

        /// <summary>
        /// Captures a screenshot excluding UI layers.
        /// Temporarily disables all Canvas components, waits one frame for URP to render
        /// the scene without UI, reads pixels from the screen buffer, then restores canvases.
        /// </summary>
        public void TakeDebugScreenshot()
        {
            StartCoroutine(CaptureShapeScreenshot());
        }

        IEnumerator CaptureShapeScreenshot()
        {
            if (Screen.width == 0 || Screen.height == 0) yield break;

            // Temporarily hide all UI canvases so the screenshot captures only the 3D scene.
            // This avoids Camera.Render() which is not reliably supported in URP.
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var wasEnabled = new bool[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                wasEnabled[i] = canvases[i].enabled;
                canvases[i].enabled = false;
            }

            // Wait one frame so URP renders the scene without UI, then capture after render completes
            yield return null;
            yield return new WaitForEndOfFrame();

            var screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenshot.Apply();

            // Restore canvases immediately
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i]) canvases[i].enabled = wasEnabled[i];
            }

            // Save to disk
            string folder = Path.Combine(Application.persistentDataPath, "Screenshots");
            Directory.CreateDirectory(folder);
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filePath = Path.Combine(folder, $"Shape_{timestamp}.png");
            File.WriteAllBytes(filePath, screenshot.EncodeToPNG());
            Destroy(screenshot);

            Debug.Log($"[ShapeDrawing] Screenshot saved (UI excluded): {filePath}");
        }
    }
}
