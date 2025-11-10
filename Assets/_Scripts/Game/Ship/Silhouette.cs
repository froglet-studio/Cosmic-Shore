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
        [SerializeField] private DriftTrailAction      driftTrailAction;

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

        private DriftTrailAction.ChangeDriftAltitude _driftHandler;

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
            if (driftTrailAction)
            {
                _driftHandler = OnDriftChanged;
                driftTrailAction.OnChangeDriftAltitude += _driftHandler;
            }
        }

        void OnDisable()
        {
            if (vesselPrismController)
            {
                vesselPrismController.OnBlockCreated -= OnBlockCreated;
                vesselPrismController.OnBlockSpawned -= OnBlockSpawned_Color;
            }
            if (driftTrailAction != null && _driftHandler != null)
            {
                driftTrailAction.OnChangeDriftAltitude -= _driftHandler;
                _driftHandler = null;
            }
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

            // Silhouette.cs — inside OnBlockSpawned_Color(Prism prism)
            Color tint;
            bool haveTint = TryExtractPrismColor(prism, out tint) && IsUsableColor(tint);

// If dangerous, use palette.danger (alpha untouched)
            bool isDanger = false;
            try { isDanger = prism.prismProperties != null && prism.prismProperties.IsDangerous; } catch { }

            if (isDanger && config && config.useDomainPaletteColors && config.domainPalette != null)
            {
                // prefer dedicated danger color
                tint = config.domainPalette.danger; // make sure you add this field in the SO
                haveTint = true;
            }
            else if (!haveTint && config && config.useDomainPaletteColors && config.domainPalette != null)
            {
                var dom = _vessel?.VesselStatus?.Domain ?? Domains.Unassigned;
                tint = config.domainPalette.Get(dom);
                haveTint = true;
            }

            if (haveTint)
            {
                // tint the head and let conveyor propagate (existing code)
                for (int r = 0; r < 2; r++)
                {
                    var img = _pool[0, r]?.GetComponent<UnityEngine.UI.Image>();
                    if (img) img.color = tint; // alpha untouched
                }
                for (int i = 1; i < _cols; i++)
                for (int r = 0; r < 2; r++)
                {
                    var img = _pool[i, r]?.GetComponent<UnityEngine.UI.Image>();
                    var prev= _pool[i-1, r]?.GetComponent<UnityEngine.UI.Image>();
                    if (img && prev) img.color = Color.Lerp(img.color, prev.color, Alpha);
                }
            }

        }

        void BuildPoolIfNeeded(float scaleY, float wavelength)
        {
            if (_pool != null || !trailDisplayContainer || !blockPrefab || config == null) return;

            var rect = (RectTransform)trailDisplayContainer;
            float minWL = Mathf.Max(0.0001f,
                vesselPrismController ? vesselPrismController.MinWaveLength : wavelength);

            int needed = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                ? Mathf.CeilToInt(rect.rect.width  / (minWL * config.worldToUIScale * Mathf.Max(0.0001f, scaleY)))
                : Mathf.CeilToInt(rect.rect.height / (minWL * config.worldToUIScale * Mathf.Max(0.0001f, scaleY)));

            _cols = Mathf.Max(config.minColumns, needed);

            _pool    = new RectTransform[_cols, 2];
            _parents = new RectTransform[_cols];

            float width  = rect.rect.width;
            float height = rect.rect.height;

            // NOTE: columnGapMultiplier applied here too
            float stride = Mathf.Max(1f, minWL * config.worldToUIScale)
                           * Mathf.Max(0.0001f, config.columnGapMultiplier);

            for (int i = 0; i < _cols; i++)
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
                if (_parents[i])
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
            float w   = rect.rect.width;
            float h   = rect.rect.height;

            // world → UI
            float stride   = Mathf.Max(1f, wavelength * config.worldToUIScale * Mathf.Max(0.0001f, sy))
                             * Mathf.Max(0.0001f, config.columnGapMultiplier); // NEW
            float xShiftUI = Mathf.Abs(xShift) * config.worldToUIScale * sy * config.gapMultiplier;
            float thickUI  = Mathf.Abs(2f * sx * sy * config.imageScale * config.worldToUIScale) * config.thicknessMultiplier;
            float lenUI    = Mathf.Abs(sz * sy * config.imageScale * config.worldToUIScale)      * config.lengthMultiplier;

            // head
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
                if (img)
                {
                    Vector2 size = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                        ? new Vector2(lenUI, Mathf.Max(0f, thickUI))
                        : new Vector2(Mathf.Max(0f, thickUI), lenUI);

                    rt.sizeDelta = Vector2.Lerp(rt.sizeDelta, size, Alpha);
                }
            }

            // conveyor
            for (int i = _cols - 1; i > 0; i--)
            {
                Vector3 parentTarget = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
                    ? new Vector3(w * 0.5f - i * stride, 0f, 0f)
                    : new Vector3(0f, h * 0.5f - i * stride, 0f);

                bool under = (config.flow == SilhouetteConfigSO.FlowDirection.HorizontalRTL)
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

        static bool IsUsableColor(Color c) => c.a >= 0.01f && Mathf.Max(c.r, c.g, c.b) >= 0.01f;

        bool TryExtractPrismColor(Prism prism, out Color color)
        {
            color = Color.white;
            var rend = prism.GetComponentInChildren<Renderer>();
            if (!rend) return false;

            var mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);

            if (TryGetColor(mpb, "_BaseColor", out color)) return true;
            if (TryGetColor(mpb, "_Color",     out color)) return true;
            if (TryGetColor(mpb, "_Tint",      out color)) return true;

            var mat = rend.sharedMaterial ?? rend.material;
            if (!mat) return false;

            if (mat.HasProperty("_BaseColor")) { color = mat.GetColor("_BaseColor"); return true; }
            if (mat.HasProperty("_Color"))     { color = mat.GetColor("_Color");     return true; }
            if (mat.HasProperty("_Tint"))      { color = mat.GetColor("_Tint");      return true; }
            return false;
        }

        bool TryGetColor(MaterialPropertyBlock mpb, string name, out Color c)
        {
            c = Color.white;
            if (mpb == null) return false;
            try
            {
                var v = mpb.GetVector(name);
                c = new Color(v.x, v.y, v.z, v.w);
                return IsUsableColor(c);
            }
            catch { return false; }
        }
        
        public void SetDangerVisual(bool _enabled)
        {
            if (config == null || !config.enableDangerVisual) return;
            if (_pool == null || _cols == 0) { _dangerActive = _enabled; return; }

            if (_enabled == _dangerActive) return; // no-op
            _dangerActive = _enabled;

            // Choose sprite source: danger prefab → Image.sprite
            Sprite dangerSprite = null;
            if (_enabled && config.dangerBlockPrefab)
            {
                var img = config.dangerBlockPrefab.GetComponentInChildren<Image>();
                if (img) dangerSprite = img.sprite;
            }

            for (int i = 0; i < _cols; i++)
            {
                for (int r = 0; r < 2; r++)
                {
                    var rt  = _pool[i, r];
                    if (!rt) continue;
                    var img = rt.GetComponent<Image>();
                    if (!img) continue;

                    if (_enabled)
                    {
                        // store original sprite once
                        if (_originalSprite == null) _originalSprite = img.sprite;

                        if (dangerSprite) img.sprite = dangerSprite;

                        // tint with dangerColor (alpha untouched)
                        img.color = config.dangerColor;
                    }
                    else
                    {
                        // revert sprite & leave color management to OnBlockSpawned_Color / palette
                        if (_originalSprite) img.sprite = _originalSprite;
                    }
                }
            }
        }
    }
}
