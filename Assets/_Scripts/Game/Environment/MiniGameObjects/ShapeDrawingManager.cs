using System.Collections;
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
        [SerializeField] Cell cellScript; // ASSIGN THIS IN INSPECTOR
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Visuals")]
        [SerializeField] LineRenderer guideLine;
        [SerializeField] Camera revealCamera;
        [SerializeField] float shapeScale = 1f;

        [Header("Events")]
        public UnityEvent OnShapeCompleted;
        public UnityEvent OnFreestyleResumed;

        ShapeDefinition _activeShape;
        Vector3 _shapeOrigin;
        int _currentWaypointIndex;
        bool _isActive;
        VesselStatus _vesselStatus;

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
        }

        public bool IsInShapeMode => _isActive;

        public void StartShapeSequence(ShapeDefinition def, Vector3 origin)
        {
            _activeShape = def;
            _shapeOrigin = origin;
            _isActive = true;
            _currentWaypointIndex = 0;

            // --- FIX 2: Disable Cell to stop Lifeforms ---
            // Disabling the component calls OnDisable(), which runs StopSpawner() inside Cell.cs
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
                // Ensure player is mobile (redundant safety check)
                _vesselStatus.IsStationary = false;
                _vesselStatus.VesselPrismController.StopSpawn(); 
            }

            // Position Player
            yield return StartCoroutine(PlacePlayer());

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
            
            // This normally triggers Cell.cs OnCellItemUpdated -> but since Cell is disabled, it won't react!
            shapeCrystalManager.SpawnAtPosition(crystalId, pos);
        }

        void HandleCrystalHit(int crystalId)
        {
            if (!_isActive) return;

            int waypointIndex = crystalId - 1;
            if (waypointIndex != _currentWaypointIndex) return;

            // Start trails only after hitting the first crystal
            if (_currentWaypointIndex == 0 && _vesselStatus)
            {
                _vesselStatus.VesselPrismController.StartSpawn();
            }

            // Toggle trail based on shape data
            if (_activeShape.IsTrailEnabledForSegment(_currentWaypointIndex))
                _vesselStatus.VesselPrismController.StartSpawn();
            else
                _vesselStatus.VesselPrismController.StopSpawn();

            _currentWaypointIndex++;
            SpawnCrystal(_currentWaypointIndex);
        }

        void UpdateGuideLine()
        {
            if (!guideLine || !_vesselStatus || _currentWaypointIndex >= _activeShape.waypoints.Count)
            {
                if(guideLine) guideLine.enabled = false;
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

        void FinishShape()
        {
            if (guideLine) guideLine.enabled = false;
            if (_vesselStatus) _vesselStatus.VesselPrismController.StopSpawn();

            StartCoroutine(RevealSequence());
        }

        IEnumerator RevealSequence()
        {
            OnShapeCompleted?.Invoke();

            if (revealCamera)
            {
                revealCamera.transform.position = _shapeOrigin + Vector3.up * _activeShape.revealCameraDistance;
                revealCamera.transform.LookAt(_shapeOrigin);
                revealCamera.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(4f); 
            ExitShapeMode();
        }

        public void ExitShapeMode()
        {
            _isActive = false;
            _activeShape = null;

            if (revealCamera) revealCamera.gameObject.SetActive(false);
            if (guideLine) guideLine.enabled = false;
            
            shapeCrystalManager.DestroyAllCrystals();
            shapeCrystalManager.enabled = false;

            // --- Note: Cell re-enabling is handled by Controller.ReturnToLobby() ---
            
            OnFreestyleResumed?.Invoke();
        }
    }
}