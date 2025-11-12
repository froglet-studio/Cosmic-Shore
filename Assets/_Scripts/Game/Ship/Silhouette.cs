using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game;
using CosmicShore.Core;

namespace CosmicShore
{
    public class Silhouette : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private VesselPrismController vesselPrismController;
        [SerializeField] private DriftTrailActionExecutor      driftTrailAction;

        [Header("HUD Refs")]
        [SerializeField] private Transform  trailDisplayContainer;
        [SerializeField] private GameObject blockPrefab;

        [Header("Config")]
        [SerializeField] private SilhouetteConfigSO config;

        private RectTransform[,] _pool;
        private RectTransform[]  _parents;
        private int _cols;

        private IVessel _vessel;
        private float _dot = .9999f;

        private DriftTrailActionExecutor.ChangeDriftAltitude _driftHandler;

        float _xShift, _wavelength, _sx, _sy, _sz;
        bool  _haveHead;
        Sprite _originalSprite;
        bool   _dangerActive;

        float Alpha => (config != null && config.smooth)
            ? (1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.0001f, config.smoothingSeconds)))
            : 1f;

        void OnEnable()
        {
            if (vesselPrismController)
            {
                vesselPrismController.OnBlockCreated += OnBlockCreated;
                vesselPrismController.OnBlockSpawned += OnBlockSpawned_Color;
            }

            if (!driftTrailAction) return;
            _driftHandler = OnDriftChanged;
            driftTrailAction.OnChangeDriftAltitude += _driftHandler;
        }

        void OnDisable()
        {
            if (vesselPrismController)
            {
                vesselPrismController.OnBlockCreated -= OnBlockCreated;
                vesselPrismController.OnBlockSpawned -= OnBlockSpawned_Color;
            }

            if (driftTrailAction == null || _driftHandler == null) return;
            driftTrailAction.OnChangeDriftAltitude -= _driftHandler;
            _driftHandler = null;
        }

        public void Initialize(IVesselStatus status, VesselHUDView hudView)
        {
            _vessel = status?.Vessel;
        }

        public void SetBlockPrefab(GameObject prefab)
        {
            blockPrefab = prefab;
            DestroyPool();
            _cols = 0;
        }

        [ContextMenu("Reset Trail UI")]
        void ResetTrailUI()
        {
            DestroyPool();
            _cols = 0;
            BuildPoolIfNeeded(_sy > 0 ? _sy : 1f, _wavelength > 0 ? _wavelength : 1f);
            if (_haveHead) ApplyHeadAndConveyor(_xShift, _wavelength, _sx, _sy, _sz);
        }

        public void Clear()
        {
            if (!trailDisplayContainer) return;
            foreach (Transform t in trailDisplayContainer) t.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!_haveHead || _pool == null || !trailDisplayContainer || _vessel?.VesselStatus == null) return;
            ApplyHeadAndConveyor(_xShift, _wavelength, _sx, _sy, _sz);
        }

        void OnDriftChanged(float dot) => _dot = dot;

        void OnBlockCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_vessel?.VesselStatus == null || _vessel.VesselStatus.AutoPilotEnabled) return;

            BuildPoolIfNeeded(scaleY, wavelength);

            _xShift     = xShift;
            _wavelength = wavelength;
            _sx         = scaleX;
            _sy         = scaleY;
            _sz         = scaleZ;
            _haveHead   = true;

            ApplyHeadAndConveyor(_xShift, _wavelength, _sx, _sy, _sz);
        }

        void OnBlockSpawned_Color(Prism prism)
        {
            if (!prism || _pool == null || _cols < 1) return;

            Color tint = default;
            var haveTint = false;

            var isDanger = false;
            try { isDanger = prism.prismProperties != null && prism.prismProperties.IsDangerous; }
            catch
            {
                // ignored
            }

            if (isDanger && config && config.useDomainPaletteColors && config.domainPalette)
            {
                tint = config.domainPalette.danger;
                haveTint = true;
            }
            else if (config && config.useDomainPaletteColors && config.domainPalette)
            {
                var dom = _vessel?.VesselStatus?.Domain ?? Domains.Unassigned;
                tint = config.domainPalette.Get(dom);
                haveTint = true;
            }

            if (!haveTint) return;
            for (var r = 0; r < 2; r++)
            {
                var img = _pool[0, r]?.GetComponent<Image>();
                if (img) img.color = tint; 
            }
            for (var i = 1; i < _cols; i++)
            for (var r = 0; r < 2; r++)
            {
                var img = _pool[i, r]?.GetComponent<Image>();
                var prev= _pool[i-1, r]?.GetComponent<Image>();
                if (img && prev) img.color = Color.Lerp(img.color, prev.color, Alpha);
            }

        }

        void BuildPoolIfNeeded(float scaleY, float wavelength)
        {
            if (_pool != null || !trailDisplayContainer || !blockPrefab || !config) return;

            var rect = (RectTransform)trailDisplayContainer;
            var minWl = Mathf.Max(0.0001f,
                vesselPrismController ? vesselPrismController.MinWaveLength : wavelength);

            var needed = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                ? Mathf.CeilToInt(rect.rect.width  / (minWl * config.worldToUIScale * Mathf.Max(0.0001f, scaleY)))
                : Mathf.CeilToInt(rect.rect.height / (minWl * config.worldToUIScale * Mathf.Max(0.0001f, scaleY)));

            _cols = Mathf.Max(config.minColumns, needed);

            _pool    = new RectTransform[_cols, 2];
            _parents = new RectTransform[_cols];

            var width  = rect.rect.width;
            var height = rect.rect.height;

            var stride = Mathf.Max(1f, minWl * config.worldToUIScale)
                         * Mathf.Max(0.0001f, config.columnGapMultiplier);

            for (var i = 0; i < _cols; i++)
            {
                var go  = new GameObject($"TrailSeg_{i}", typeof(RectTransform));
                var prt = (RectTransform)go.transform;
                prt.SetParent(trailDisplayContainer, false);
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
                prt.localRotation = Quaternion.Euler(0, 0, config.columnRotationOffsetDeg);
                prt.localPosition = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector3(width * 0.5f  - i * stride, 0f, 0f)
                    : new Vector3(0f,    height * 0.5f - i * stride, 0f);

                _parents[i] = prt;

                for (int r = 0; r < 2; r++)
                {
                    var block = Instantiate(blockPrefab, prt, false);
                    var rt    = (RectTransform)block.transform;
                    rt.localScale    = Vector3.one;
                    rt.sizeDelta     = Vector2.zero;
                    rt.localPosition = Vector3.zero;
                    _pool[i, r] = rt;
                }
            }
        }

        void DestroyPool()
        {
            if (_pool == null) return;
            for (int i = 0; i < _cols; i++)
            {
                for (int r = 0; r < 2; r++)
                {
                    if (_pool[i, r])
                    {
                        var go = _pool[i, r].gameObject;
                        if (Application.isEditor) DestroyImmediate(go); else Destroy(go);
                    }
                }

                if (!_parents[i]) continue;
                {
                    var go = _parents[i].gameObject;
                    if (Application.isEditor) DestroyImmediate(go); else Destroy(go);
                }
            }
            _pool = null; _parents = null; _cols = 0;
        }

        void ApplyHeadAndConveyor(float xShift, float wavelength, float sx, float sy, float sz)
        {
            if (_pool == null || _cols == 0) return;

            var rect  = (RectTransform)trailDisplayContainer;
            var w   = rect.rect.width;
            var h   = rect.rect.height;
            var stride   = Mathf.Max(1f, wavelength * config.worldToUIScale * Mathf.Max(0.0001f, sy))
                           * Mathf.Max(0.0001f, config.columnGapMultiplier);
            var xShiftUI = Mathf.Abs(xShift) * config.worldToUIScale * sy * config.gapMultiplier;
            var thickUI  = Mathf.Abs(2f * sx * sy * config.imageScale * config.worldToUIScale) * config.thicknessMultiplier;
            var lenUI    = Mathf.Abs(sz * sy * config.imageScale * config.worldToUIScale)      * config.lengthMultiplier;

            for (int r = 0; r < 2; r++)
            {
                var rt     = _pool[0, r];
                var parent = _parents[0];

                Vector3 pPos = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector3(w * 0.5f, 0f, 0f)
                    : new Vector3(0f, h * 0.5f, 0f);

                parent.localPosition = Vector3.Lerp(parent.localPosition, pPos, Alpha);

                float tilt = -Mathf.Acos(Mathf.Clamp(_dot - 0.0001f, -0.9999f, 0.9999f)) * Mathf.Rad2Deg;
                parent.localRotation = Quaternion.Slerp(parent.localRotation,
                    Quaternion.Euler(0, 0, tilt + config.columnRotationOffsetDeg), Alpha);

                Vector3 bPos = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector3(0f, r * 2f * xShiftUI - xShiftUI, 0f)
                    : new Vector3(r * 2f * xShiftUI - xShiftUI, 0f, 0f);

                rt.localPosition = Vector3.Lerp(rt.localPosition, bPos, Alpha);

                var img = rt.GetComponent<Image>();
                if (!img) continue;
                var size = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector2(lenUI, Mathf.Max(0f, thickUI))
                    : new Vector2(Mathf.Max(0f, thickUI), lenUI);

                rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, size, Alpha);
            }

            // conveyor
            for (int i = _cols - 1; i > 0; i--)
            {
                var parentTarget = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector3(w * 0.5f - i * stride, 0f, 0f)
                    : new Vector3(0f, h * 0.5f - i * stride, 0f);

                var under = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? i < Mathf.CeilToInt(w / Mathf.Max(1f, stride))
                    : i < Mathf.CeilToInt(h / Mathf.Max(1f, stride));

                for (int r = 0; r < 2; r++)
                {
                    var cur  = _pool[i, r];
                    var prev = _pool[i - 1, r];
                    var curP  = _parents[i];
                    var prevP = _parents[i - 1];

                    curP.localPosition   = Vector3.Lerp(curP.localPosition, parentTarget, Alpha);
                    curP.gameObject.SetActive(under);

                    cur.sizeDelta        = Vector2.Lerp(cur.sizeDelta,     prev.sizeDelta,     Alpha);
                    cur.localPosition    = Vector3.Lerp(cur.localPosition, prev.localPosition, Alpha);
                    curP.localRotation   = Quaternion.Slerp(curP.localRotation, prevP.localRotation, Alpha);
                }
            }
        }

        public void SetDangerVisual(bool dangerEnabled)
        {
            if (!config || !config.enableDangerVisual) return;
            if (_pool == null || _cols == 0) { _dangerActive = dangerEnabled; return; }

            if (dangerEnabled == _dangerActive) return; 
            _dangerActive = dangerEnabled;
            Sprite dangerSprite = null;
            if (dangerEnabled && config.dangerBlockPrefab)
            {
                var img = config.dangerBlockPrefab.GetComponentInChildren<Image>();
                if (img) dangerSprite = img.sprite;
            }

            for (var i = 0; i < _cols; i++)
            {
                for (var r = 0; r < 2; r++)
                {
                    var rt  = _pool[i, r];
                    if (!rt) continue;
                    var img = rt.GetComponent<Image>();
                    if (!img) continue;

                    if (dangerEnabled)
                    {
                        if (!_originalSprite) _originalSprite = img.sprite;
                        if (dangerSprite) img.sprite = dangerSprite;
                        img.color = config.dangerColor;
                    }
                    else
                    {
                        if (_originalSprite) img.sprite = _originalSprite;
                    }
                }
            }
        }
    }
}
