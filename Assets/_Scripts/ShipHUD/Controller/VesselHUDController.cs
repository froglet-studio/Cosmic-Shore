using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        private R_VesselActionHandler _actions;
        private VesselHUDView _view;
        private IVesselStatus _status;
        [SerializeField] private int jawResourceIndex;
        [SerializeField] private Vector2 blockBaseSize = new(30f, 30f);
        private float _driftDot = 0.9999f;

        private TrailPool _trailPool;
        private GameObject BlockPrefab { get; set; }

        private bool _pendingPoolBuild;

        private bool IsLocalOwner =>
            _status is { IsOwnerClient: true };

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _status = vesselStatus;
            _view = view;

            if (!IsLocalOwner)
            {
                if (_view)
                    _view.gameObject.SetActive(false);

                return;
            }

            if (_status.AutoPilotEnabled)
            {
                view.gameObject.SetActive(false);
                return;
            }

            _actions = vesselStatus.ActionHandler;
            if (_actions != null)
            {
                _actions.OnInputEventStarted += HandleStart;
                _actions.OnInputEventStopped += HandleStop;

                _actions.ToggleSubscription(true);
            }
            else
            {
                Debug.LogWarning("[VesselHUDController] ActionHandler is null.");
            }

            BindJaws();
            BindDrift();
            BindTrail();
            PrimeInitialUI();
        }

        public void TearDown()
        {
            if (_actions)
            {
                _actions.OnInputEventStarted -= HandleStart;
                _actions.OnInputEventStopped -= HandleStop;
                _actions.ToggleSubscription(false);
                _actions = null;
            }

            if (_view)
            {
                var resources = _status?.ResourceSystem?.Resources;
                if (resources != null &&
                    _view.jawResourceIndex >= 0 &&
                    _view.jawResourceIndex < resources.Count &&
                    resources[_view.jawResourceIndex] != null)
                {
                    resources[_view.jawResourceIndex].OnResourceChange -= OnJawResourceChanged;
                }

                if (_view.driftTrailAction)
                    _view.driftTrailAction.OnChangeDriftAltitude -= OnDriftDotChanged;

                if (_view.vesselPrismController)
                    _view.vesselPrismController.OnBlockCreated -= VesselPrismCreated;
            }

            _trailPool?.Dispose();
            _trailPool = null;
            _pendingPoolBuild = false;
        }

        private void LateUpdate()
        {
            if (_trailPool != null)
            {
                float dot = Mathf.Clamp(_driftDot, -0.9999f, 0.9999f);
                float angle = -Mathf.Acos(dot) * Mathf.Rad2Deg; // [0 .. -180]
                _trailPool.SetTargetDriftAngle(angle);
                _trailPool.Tick(Time.deltaTime);
            }
        
            if (_pendingPoolBuild && _view && _view.trailDisplayContainer != null) return;
        
            var r = _view.trailDisplayContainer.rect;
        
            if (!(r.width > 1f) || !(r.height > 1f) || _trailPool == null) return;
            _pendingPoolBuild = false;
            _trailPool.EnsurePool();
        }

        private void PrimeInitialUI()
        {
            var resources = _status?.ResourceSystem?.Resources;
            if (resources != null &&
                _view.jawResourceIndex >= 0 &&
                _view.jawResourceIndex < resources.Count &&
                resources[_view.jawResourceIndex] != null)
            {
                var normalized = 0f;
                try
                {
                    normalized = resources[_view.jawResourceIndex].CurrentAmount;
                }
                catch
                {
                }

                OnJawResourceChanged(normalized);
            }
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop(InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (!_view) return;
            if (!IsLocalOwner) return;

            for (var i = 0; i < _view.highlights.Count; i++)
            {
                if (_view.highlights[i].input == ev)
                    _view.highlights[i].image.enabled = on;
            }
        }


        #region Silhouette / Jaws / Drift

        private void BindJaws()
        {
            if (!_view) return;
            if (!_view.topJaw || !_view.bottomJaw) return;
            if (_view.jawResourceIndex < 0) return;

            var resources = _status?.ResourceSystem?.Resources;
            if (resources == null || _view.jawResourceIndex >= resources.Count) return;

            var res = resources[_view.jawResourceIndex];

            res.OnResourceChange += OnJawResourceChanged;
        }

        private void OnJawResourceChanged(float normalized)
        {
            if (_view.silhouetteParts != null)
            {
                foreach (var go in _view.silhouetteParts.Where(go => go)) go.SetActive(true);
            }

            if (_view.topJaw) _view.topJaw.rectTransform.localRotation = Quaternion.Euler(0, 0, 21f * normalized);
            if (_view.bottomJaw)
                _view.bottomJaw.rectTransform.localRotation = Quaternion.Euler(0, 0, -21f * normalized);

            var col = normalized > 0.98f ? Color.green : Color.white;
            if (_view.topJaw) _view.topJaw.color = col;
            if (_view.bottomJaw) _view.bottomJaw.color = col;
        }

        private void BindDrift()
        {
            if (!_view) return;
            if (!_view.driftTrailAction) return;

            _view.driftTrailAction.OnChangeDriftAltitude += OnDriftDotChanged;
        }

        private void OnDriftDotChanged(float dot)
        {
            _driftDot = Mathf.Clamp(dot, -0.9999f, 0.9999f);

            if (!_view.silhouetteContainer) return;
            var angleZ = Mathf.Asin(_driftDot) * Mathf.Rad2Deg; // little jank welcome
            _view.silhouetteContainer.localRotation = Quaternion.Euler(0, 0, angleZ);
        }

        #endregion

        #region Trail HUD

        public void SetBlockPrefab(GameObject prefab)
        {
            BlockPrefab = prefab;

            if (_trailPool == null && _view != null && _view.vesselPrismController && _view.trailDisplayContainer &&
                BlockPrefab != null)
                BindTrail();
        }

        private void BindTrail()
        {
            if (!_view) return;

            if (!BlockPrefab)
            {
                Debug.LogWarning("HUD: no trail BlockPrefab");
                return;
            }

            // Make sure the prefab’s Image preserves aspect at runtime (no need to touch the asset)
            var img = BlockPrefab.GetComponent<Image>();
            if (img) img.preserveAspect = true;

            // Create vertical rows TrailPool (smoothing ~0.08s feels good)
            _trailPool = new TrailPool(
                _view.trailDisplayContainer,
                BlockPrefab,
                _view.vesselPrismController,
                _view.worldToUIScale,
                _view.imageScale,
                _view.swingBlocks,
                smoothTime: 0.08f,
                blockBaseSize: blockBaseSize, hardRowCap: 8);

            // If rect hasn’t been laid out yet, defer EnsurePool
            var r = _view.trailDisplayContainer.rect;
            if (r is { width: > 1f, height: > 1f })
            {
                _trailPool.EnsurePool();
                _pendingPoolBuild = false;
            }
            else
            {
                _pendingPoolBuild = true; // try in LateUpdate
            }

            _view.vesselPrismController.OnBlockCreated += VesselPrismCreated;
        }

        private void VesselPrismCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_status is { AutoPilotEnabled: true }) return;
            if (_trailPool == null) return;

            // WorldToUi + image scaling logic matches old math paths
            var ui = _trailPool.WorldToUi;
            if (_trailPool.SwingBlocks)
            {
                _trailPool.UpdateHead(
                    xShift: xShift * (scaleY / 2f) * ui,
                    wavelength: wavelength * ui,
                    scaleX: scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ: scaleZ * _trailPool.ImageScale,
                    driftDot: _view.driftTrailAction ? _driftDot : (float?)null
                );
            }
            else
            {
                _trailPool.UpdateHead(
                    xShift: xShift * ui * scaleY,
                    wavelength: wavelength * ui * scaleY,
                    scaleX: scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ: scaleZ * scaleY * _trailPool.ImageScale,
                    driftDot: _view.driftTrailAction ? _driftDot : (float?)null
                );
            }
        }

        #endregion
    }
}