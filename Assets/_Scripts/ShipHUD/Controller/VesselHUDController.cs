using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselHUDController : MonoBehaviour, IVesselHUDController
    {
        private R_VesselActionHandler _actions;
        private VesselHUDView _view;
        private IVesselStatus _status;
        [SerializeField] private int jawResourceIndex;

        private float _driftDot = 0.9999f; 

        private TrailPool _trailPool;
        
        
        public GameObject BlockPrefab { get; private set; }
        

        public virtual void Initialize(IVesselStatus vesselStatus, VesselHUDView view)
        {
            _status = vesselStatus;
            _view   = view;

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
            }

            BindJaws();
            BindDrift();
            BindTrail();
            PrimeInitialUI();
        }
        
        private void PrimeInitialUI()
        {
            // silhouette visibility immediately
            // bool show = !_status.AutoPilotEnabled && (_status.Player?.IsActive ?? true);
            // if (_view.silhouetteParts != null)
            //     foreach (var go in _view.silhouetteParts) if (go) go.SetActive(show);

            if (_view.silhouetteContainer) _view.silhouetteContainer.localRotation = Quaternion.identity;


            var resources = _status?.ResourceSystem?.Resources;
            if (resources != null &&
                _view.jawResourceIndex >= 0 &&
                _view.jawResourceIndex < resources.Count &&
                resources[_view.jawResourceIndex] != null)
            {

                var normalized = 0f;
                try { normalized = resources[_view.jawResourceIndex].CurrentAmount; } catch { /* fallback 0 */ }
                OnJawResourceChanged(normalized);
            }
        }

        private void HandleStart(InputEvents ev) => Toggle(ev, true);
        private void HandleStop (InputEvents ev) => Toggle(ev, false);

        private void Toggle(InputEvents ev, bool on)
        {
            if (_view == null) return;

            for (var i = 0; i < _view.highlights.Count; i++)
            {
                if (_view.highlights[i].input == ev && _view.highlights[i].image != null)
                    _view.highlights[i].image.enabled = on;
            }
        }

        #region Silhouette

        private void BindJaws()
        {
            if (_view == null) return;
            if (_view.topJaw == null || _view.bottomJaw == null) return;
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
                foreach (var go in _view.silhouetteParts)
                    if (go) go.SetActive(true);
            }

            if (_view.topJaw)    _view.topJaw.rectTransform.localRotation    = Quaternion.Euler(0, 0,  21f * normalized);
            if (_view.bottomJaw) _view.bottomJaw.rectTransform.localRotation = Quaternion.Euler(0, 0, -21f * normalized);

            var col = normalized > 0.98f ? Color.green : Color.white;
            if (_view.topJaw)    _view.topJaw.color    = col;
            if (_view.bottomJaw) _view.bottomJaw.color = col;
        }

        private void BindDrift()
        {
            if (_view == null) return;
            if (_view.driftTrailAction == null) return;

            _view.driftTrailAction.OnChangeDriftAltitude += OnDriftDotChanged;
        }

        private void OnDriftDotChanged(float dot)
        {
            _driftDot = Mathf.Clamp(dot, -0.9999f, 0.9999f);
            
            // if (_status != null && _status.Player != null && _view.silhouetteParts != null)
            // {
            //     var show = !_status.AutoPilotEnabled && _status.Player.IsActive;
            //     foreach (var go in _view.silhouetteParts.Where(go => go)) go.SetActive(show);
            // }

            if (_view.silhouetteContainer == null) return;
            var angleZ = Mathf.Asin(_driftDot) * Mathf.Rad2Deg;
            _view.silhouetteContainer.localRotation = Quaternion.Euler(0, 0, angleZ);
        }

        public void SetBlockPrefab(GameObject prefab)
        {
            if (_view == null) return;

            // _view.trailBlockPrefab = prefab;
            BlockPrefab = prefab; 
            
            if (_trailPool == null && _view.trailSpawner && _view.trailDisplayContainer && BlockPrefab != null)
                BindTrail();
        }
        
        private void BindTrail()
        {
            if (_view == null) return;

            if (_view.trailSpawner == null) { Debug.LogWarning("HUD: no TrailSpawner"); return; }
            if (_view.trailDisplayContainer == null) { Debug.LogWarning("HUD: no TrailDisplayContainer"); return; }
            if (BlockPrefab == null) { Debug.LogWarning("HUD: no TrailBlockPrefab"); return; }

            _trailPool = new TrailPool(
                _view.trailDisplayContainer,
                BlockPrefab,
                _view.trailSpawner,
                _view.worldToUIScale,
                _view.imageScale,
                _view.swingBlocks
            );
            _view.trailSpawner.OnBlockCreated += OnTrailBlockCreated;
        }

        private void OnTrailBlockCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_status is { AutoPilotEnabled: true }) return;
            if (_trailPool == null) return;
            Debug.Log("Trail events");
            var uiScale = _trailPool.WorldToUi;
            if (_trailPool.SwingBlocks)
            {
                _trailPool.EnsurePool();
                _trailPool.UpdateHead(
                    xShift:     xShift * (scaleY / 2f) * uiScale,
                    wavelength: wavelength * uiScale,
                    scaleX:     scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ:     scaleZ * _trailPool.ImageScale,
                    driftDot:   _view.driftTrailAction ? _driftDot : null
                );
            }
            else
            {
                _trailPool.EnsurePool(scaleY);
                _trailPool.UpdateHead(
                    xShift:     xShift * uiScale * scaleY,
                    wavelength: wavelength * uiScale * scaleY,
                    scaleX:     scaleX * scaleY * _trailPool.ImageScale,
                    scaleZ:     scaleZ * scaleY * _trailPool.ImageScale,
                    driftDot:   _view.driftTrailAction ? _driftDot : null
                );
            }
        }

        private sealed class TrailPool
        {
            private readonly RectTransform _container;
            private readonly GameObject _blockPrefab;
            private readonly TrailSpawner _spawner;

            public readonly float WorldToUi;
            public readonly float ImageScale;
            public readonly bool  SwingBlocks;

            private GameObject[,] _pool;
            private int _poolSize;

            public TrailPool(RectTransform container, GameObject prefab, TrailSpawner spawner,
                             float worldToUi, float imageScale, bool swingBlocks)
            {
                _container  = container;
                _blockPrefab = prefab;
                _spawner = spawner;
                WorldToUi = worldToUi;
                ImageScale = imageScale;
                SwingBlocks = swingBlocks;
            }

            public void EnsurePool(float scaleY = 1f)
            {
                if (_poolSize > 0) return;

                var rectWidth = _container.rect.width;
                var denom = _spawner.MinWaveLength * WorldToUi * (SwingBlocks ? 1f : scaleY);
                _poolSize = Mathf.Max(1, Mathf.CeilToInt(rectWidth / Mathf.Max(0.0001f, denom)));

                _pool = new GameObject[_poolSize, 2];

                for (int i = 0; i < _poolSize; i++)
                {
                    var colParent = new GameObject($"TrailCol_{i}", typeof(RectTransform)).transform as RectTransform;
                    colParent.SetParent(_container, false);

                    for (int j = 0; j < 2; j++)
                    {
                        var block = Object.Instantiate(_blockPrefab, _container);
                        var blockRt = block.transform as RectTransform;
                        blockRt.SetParent(colParent, false);

                        // parent X = center + offset to the right
                        colParent.localPosition = new Vector3(
                            -i * _spawner.MinWaveLength * WorldToUi + (rectWidth * 0.5f), 0f, 0f);

                        // block Y = ± gap (top/bottom)
                        blockRt.localPosition = new Vector3(0f, j * 2f * _spawner.Gap - _spawner.Gap, 0f);
                        blockRt.localScale = Vector3.zero;
                        block.SetActive(true);

                        _pool[i, j] = block;
                    }
                }
            }

            public void UpdateHead(float xShift, float wavelength, float scaleX, float scaleZ, float? driftDot)
            {
                if (_pool == null) return;
                var rectWidth = _container.rect.width;

                for (int j = 0; j < 2; j++)
                {
                    var head = _pool[0, j].transform as RectTransform;
                    head.localScale    = new Vector3(scaleZ, j * 2f * scaleX - scaleX, 1f);
                    head.parent.localPosition = new Vector3(rectWidth * 0.5f, 0f, 0f);
                    head.localPosition  = new Vector3(0f, j * 2f * xShift - xShift, 0f);
                }

                if (driftDot.HasValue)
                {
                    float dot = Mathf.Clamp(driftDot.Value, -0.9999f, 0.9999f);
                    float angle = -Mathf.Acos(dot) * Mathf.Rad2Deg; // matches old feel
                    _pool[0, 0].transform.parent.localRotation = Quaternion.Euler(0, 0, angle);
                }

                // shift tail columns
                for (int i = _poolSize - 1; i > 0; i--)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        var cur  = _pool[i, j].transform as RectTransform;
                        var prev = _pool[i - 1, j].transform as RectTransform;

                        cur.localScale = prev.localScale;
                        cur.localPosition = prev.localPosition;

                        // update parent x to maintain spacing by current wavelength
                        var parent = cur.parent as RectTransform;
                        parent.localPosition = new Vector3(-i * wavelength + (rectWidth * 0.5f), 0f, 0f);
                    }

                    bool under = i < Mathf.CeilToInt(rectWidth / Mathf.Max(0.0001f, wavelength));
                    _pool[i, 1].transform.parent.gameObject.SetActive(under);

                    if (driftDot.HasValue && under)
                    {
                        _pool[i, 0].transform.parent.localRotation =
                            _pool[i - 1, 0].transform.parent.localRotation;
                    }
                }
            }
        }
        #endregion
   
    }
}
