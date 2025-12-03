using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class UITrailController : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private VesselPrismController vesselPrismController;
        [SerializeField] private DriftTrailActionExecutor driftTrailAction;

        [Header("UI Target")]
        [SerializeField] private RectTransform trailDisplayContainer;

        [Header("Layout (Pixels)")]
        [SerializeField] private Vector2 blockSizePx = new(30f, 30f);
        [SerializeField] private float   scaleMultiplier = 1f;

        [Header("Behaviour")]
        [SerializeField] private bool swingBlocks = false;

        [Header("World→UI Tuning")]
        [SerializeField] private float pixelsPerWorldUnit = 2f;  // legacy ~2
        [SerializeField] private float imageScale         = 0.02f;

        [Header("Pool")]
        [SerializeField] private int hardColumnCap = 256;

        [Header("Diagnostics")]
        [Tooltip("If true, ownership/autopilot checks are ignored so you always build & run.")]
        [SerializeField] private bool ignoreOwnershipChecks = true;
        [Tooltip("Spam log OnBlockCreated payloads and sizing every event.")]
        [SerializeField] private bool verboseLogs = true;

        // provided at runtime by HUD controller
        private GameObject _blockPrefab;

        // state
        private GameObject[,] _pool;   // [col, row(0/1)]
        private int _columns;
        private float _driftDot;
        private bool _subPrism;
        private bool _subDrift;
        private IVesselStatus _status;

        private Vector2 Eff1(Vector2 v) => v * Mathf.Max(0.01f, scaleMultiplier);

        public void Initialize(IVesselStatus status)
        {
            _status = status ?? throw new NullReferenceException("[UITrailController] Initialize(status) got null IVesselStatus.");
            TryInitialize();
        }

        public void TearDown()
        {
            if (_subPrism)  vesselPrismController.OnBlockCreated -= OnPrismBlockCreated;
            if (_subDrift && driftTrailAction) driftTrailAction.OnChangeDriftAltitude -= OnDriftDotChanged;
            _subPrism = _subDrift = false;

            if (_pool != null)
            {
                for (int i = 0; i < _pool.GetLength(0); i++)
                    for (int j = 0; j < 2; j++)
                        if (_pool[i, j])
                        {
                            var parent = _pool[i, j].transform.parent;
                            Destroy(_pool[i, j]);
                            if (parent) Destroy(parent.gameObject);
                        }
            }
            _pool    = null;
            _columns = 0;
        }

        public void SetBlockPrefab(GameObject prefab, IVesselStatus status)
        {
            _status ??= status;
            _blockPrefab = prefab;
            var img = _blockPrefab.GetComponent<Image>();
            if (img) img.preserveAspect = true;

            // force rebuild with new prefab
            TearDown();
            TryInitialize();
        }

        private void RequireReady()
        {
            if (_status == null)
            {
                _status = null;
            }
            if (!ignoreOwnershipChecks)
            {
                if (!_status.IsNetworkOwner) throw new InvalidOperationException("[UITrailController] IsNetworkOwner == false.");
                if (_status.AutoPilotEnabled) throw new InvalidOperationException("[UITrailController] AutoPilotEnabled == true.");
            }
            if (!trailDisplayContainer)     throw new NullReferenceException("[UITrailController] trailDisplayContainer is null.");
            if (!vesselPrismController)     throw new NullReferenceException("[UITrailController] vesselPrismController is null.");
            if (!_blockPrefab)              throw new NullReferenceException("[UITrailController] block prefab not set. Call VesselHUDController.SetBlockPrefab().");
        }

        private void TryInitialize()
        {
            RequireReady();
            StartCoroutine(InitWhenRectReady());

            vesselPrismController.OnBlockCreated += OnPrismBlockCreated;
            _subPrism = true;

            if (driftTrailAction)
            {
                driftTrailAction.OnChangeDriftAltitude += OnDriftDotChanged;
                _subDrift = true;
            }
        }

        private IEnumerator InitWhenRectReady()
        {
            yield return new WaitForEndOfFrame();
            var r = trailDisplayContainer.rect;
            if (r.width < 2f || r.height < 2f)
            {
                // intentionally no graceful fallback — you will see it if not sized
                throw new InvalidOperationException($"[UITrailController] trailDisplayContainer rect invalid (w:{r.width}, h:{r.height}). Ensure layout is active.");
            }
        }

        private void OnDriftDotChanged(float dot)
        {
            _driftDot = Mathf.Clamp(dot, -0.9999f, 0.9999f);
        }

        private void BuildPool(int neededColumns)
        {
            if (neededColumns <= 0) throw new ArgumentOutOfRangeException(nameof(neededColumns));
            neededColumns = Mathf.Clamp(neededColumns, 1, hardColumnCap);

            _columns = neededColumns;
            _pool = new GameObject[_columns, 2];

            for (int i = 0; i < _columns; i++)
            {
                var colGO = new GameObject($"TrailCol_{i}", typeof(RectTransform));
                var colRT = (RectTransform)colGO.transform;
                colRT.SetParent(trailDisplayContainer, false);
                colRT.localScale = Vector3.one;
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0.5f);

                for (int j = 0; j < 2; j++)
                {
                    var block = Instantiate(_blockPrefab, colRT, false);
                    var brt = (RectTransform)block.transform;
                    brt.localScale = Vector3.zero;
                    brt.sizeDelta  = Eff1(blockSizePx);
                    _pool[i, j] = block;
                }
            }
        }

        private void OnPrismBlockCreated(float xShiftW, float wavelengthW, float scaleXW, float scaleYW, float scaleZW)
        {
            // NO early returns — we want the exception if something is off
            RequireReady();

            float ui = Mathf.Max(0.0001f, pixelsPerWorldUnit);
            float wavelengthPx, xShiftPx, scaleXPx, scaleZPx;

            if (swingBlocks)
            {
                xShiftPx    = xShiftW    * (scaleYW * 0.5f) * ui;
                wavelengthPx= wavelengthW* ui;
                scaleXPx    = scaleXW    * scaleYW * imageScale;
                scaleZPx    = scaleZW    * imageScale;
            }
            else
            {
                xShiftPx    = xShiftW    * ui * scaleYW;
                wavelengthPx= wavelengthW* ui * scaleYW;
                scaleXPx    = scaleXW    * scaleYW * imageScale;
                scaleZPx    = scaleZW    * scaleYW * imageScale;
            }

            var rect = trailDisplayContainer.rect;
            float width = rect.width;

            if (_pool == null || _columns == 0)
            {
                if (wavelengthPx < 1f) wavelengthPx = Eff1(blockSizePx).x; // force something sane
                int needed = Mathf.CeilToInt(width / Mathf.Max(1f, wavelengthPx)) + 2;
                if (verboseLogs) Debug.Log($"[UITrailController] BuildPool — width:{width:F1} px, wavelengthPx:{wavelengthPx:F3}, columns:{needed}");
                BuildPool(needed);
            }

            // head scales (X=thickness, Y=height)
            Vector3 headScaleRow0 = new Vector3(scaleZPx, -scaleXPx, 1f);
            Vector3 headScaleRow1 = new Vector3(scaleZPx,  scaleXPx, 1f);

            // head column (index 0)
            for (int j = 0; j < 2; j++)
            {
                var block  = _pool[0, j];
                var parent = (RectTransform)block.transform.parent;
                parent.localPosition = new Vector3(width * 0.5f, 0f, 0f);

                if (driftTrailAction)
                {
                    float tilt = Mathf.Acos(Mathf.Clamp(_driftDot, -0.9999f, 0.9999f)) * Mathf.Rad2Deg;
                    parent.localRotation = Quaternion.Euler(0f, 0f, (_driftDot >= 0 ? -1f : 1f) * tilt);
                }

                var brt = (RectTransform)block.transform;
                float y = (j == 0) ? (-xShiftPx) : (xShiftPx);
                brt.localPosition = new Vector3(0f, y, 0f);
                brt.sizeDelta     = Eff1(blockSizePx);
                brt.localScale    = (j == 0) ? headScaleRow0 : headScaleRow1;
            }

            // conveyor shift
            for (int i = _columns - 1; i > 0; i--)
            {
                float colX = -i * wavelengthPx + width * 0.5f;
                bool under = i < Mathf.CeilToInt(width / Mathf.Max(1f, wavelengthPx));

                for (int j = 0; j < 2; j++)
                {
                    var cur  = _pool[i, j];
                    var prev = _pool[i - 1, j];

                    var curParent = (RectTransform)cur.transform.parent;
                    curParent.localPosition = new Vector3(colX, 0f, 0f);
                    curParent.gameObject.SetActive(under);

                    ((RectTransform)cur.transform).localScale    = ((RectTransform)prev.transform).localScale;
                    ((RectTransform)cur.transform).localPosition = ((RectTransform)prev.transform).localPosition;

                    if (driftTrailAction)
                        curParent.localRotation = ((RectTransform)prev.transform.parent).localRotation;
                }
            }

            if (verboseLogs)
            {
                Debug.Log(
                    $"[UITrailController] OnBlockCreated " +
                    $"xShiftW:{xShiftW:F3} wlW:{wavelengthW:F3} sX:{scaleXW:F3} sY:{scaleYW:F3} sZ:{scaleZW:F3} | " +
                    $"xShiftPx:{xShiftPx:F2} wlPx:{wavelengthPx:F2} | " +
                    $"width:{width:F1} cols:{_columns} driftDot:{_driftDot:F5}"
                );
            }
        }

        [ContextMenu("Dump State")]
        private void DumpState()
        {
            var rect = trailDisplayContainer ? trailDisplayContainer.rect : new Rect(0,0,0,0);
            Debug.Log(
                "[UITrailController] DumpState\n" +
                $"- status: {(_status==null ? "NULL" : "OK")} (IsOwner:{_status?.IsNetworkOwner}, AutoPilot:{_status?.AutoPilotEnabled})\n" +
                $"- trailDisplayContainer: {trailDisplayContainer}\n" +
                $"- vesselPrismController: {vesselPrismController}\n" +
                $"- driftTrailAction: {driftTrailAction}\n" +
                $"- blockPrefab: {_blockPrefab}\n" +
                $"- rect: {rect.width}x{rect.height}\n" +
                $"- columns: {_columns}\n" +
                $"- swingBlocks: {swingBlocks}\n" +
                $"- pixelsPerWorldUnit: {pixelsPerWorldUnit}, imageScale:{imageScale}\n" +
                $"- scaleMultiplier:{scaleMultiplier}\n" +
                $"- driftDot:{_driftDot}"
            );
        }
    }
}
