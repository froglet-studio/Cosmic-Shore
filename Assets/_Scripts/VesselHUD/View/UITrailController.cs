using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    /// <summary>
    /// Mirrors the vessel's trail into UI with explicit, pixel-based layout knobs.
    /// </summary>
    public sealed class UITrailController : MonoBehaviour
    {
        [Header("Sources")] [SerializeField] private VesselPrismController vesselPrismController;
        [SerializeField] private DriftTrailActionExecutor driftTrailAction;

        [Header("UI Target")] [SerializeField] private RectTransform trailDisplayContainer;

        [Header("Layout (Pixels)")]
        [Tooltip("Pixel size of each trail block image (width x height) before ScaleMultiplier.")]
        [SerializeField]
        private Vector2 blockSizePx = new(30f, 30f);

        [Tooltip("Vertical distance in pixels between successive blocks in a column.")] [SerializeField]
        private float rowSpacingPx = 6f;

        [Tooltip("Horizontal gap in pixels between the two columns (center spacing).")] [SerializeField]
        private float columnGapPx = 16f;

        [Tooltip("Uniform multiplier applied to BlockSizePx, RowSpacingPx, ColumnGapPx.")] [SerializeField]
        private float scaleMultiplier = 1f;

        [Header("Behaviour")] [Tooltip("If true, apply swing math variant like the old path.")] [SerializeField]
        private bool swingBlocks;

        [Header("Tuning")] [Tooltip("Smoothing seconds passed to the pool for UI lerp smoothing.")] [SerializeField]
        private float smoothingSeconds = 0.08f;

        [Tooltip("Hard cap on rows per column.")] [SerializeField]
        private int hardRowCap = 8;

        // Prefab is provided by HUD controller (do not serialize here)
        private GameObject _blockUIPrefab;

        private TrailPool _pool;
        private bool _prismSub;
        private float _driftDot;
        private IVesselStatus _status;

        /// <summary>
        /// Call once. Will only build when prefab and refs are ready.
        /// </summary>
        public void Initialize(IVesselStatus status)
        {
            _status = status;
            TryInitialize();
        }

        public void TearDown()
        {
            if (_prismSub && vesselPrismController)
                vesselPrismController.OnBlockCreated -= OnPrismBlockCreated;

            if (driftTrailAction)
                driftTrailAction.OnChangeDriftAltitude -= OnDriftDotChanged;

            _pool?.Dispose();
            _pool = null;
            _prismSub = false;
        }

        public void SetBlockPrefab(GameObject prefab)
        {
            if (!prefab)
            {
                Debug.LogWarning("[UITrailController] Ignoring null block prefab.");
                return;
            }

            _blockUIPrefab = prefab;
            EnsurePrefabAspect(_blockUIPrefab);

            // Only (re)initialize if we already have a status and refs;
            // otherwise we’ll init later when Initialize(status) is called.
            if (_pool != null)
            {
                TearDown();
            }

            TryInitialize();
        }

        private void TryInitialize()
        {
            // 1) Must have status
            if (_status == null) return;

            // 2) Skip for non-owner or autopilot ships
            if (!_status.IsOwnerClient) return;
            if (_status.AutoPilotEnabled) return;

            // 3) Don’t double-build
            if (_pool != null) return;

            // 4) Need all refs + prefab
            if (!trailDisplayContainer || !vesselPrismController || !_blockUIPrefab)
            {
                if (!trailDisplayContainer)
                    Debug.LogWarning("[UITrailController] trailDisplayContainer missing.");
                if (!vesselPrismController)
                    Debug.LogWarning("[UITrailController] vesselPrismController missing.");
                if (!_blockUIPrefab)
                    Debug.LogWarning("[UITrailController] blockUIPrefab not set (must be sent from HUD).");
                return;
            }

            // --- existing pool construction (unchanged) ---
            var effBlockSize = blockSizePx * Mathf.Max(0.01f, scaleMultiplier);
            var effRowSpace = rowSpacingPx * Mathf.Max(0.01f, scaleMultiplier);
            var effGap = columnGapPx * Mathf.Max(0.01f, scaleMultiplier);

            _pool = new TrailPool(
                container: trailDisplayContainer,
                prefab: _blockUIPrefab,
                controller: vesselPrismController,
                worldToUi: 1f,
                imageScale: 1f,
                swingBlocks: swingBlocks,
                smoothTime: smoothingSeconds,
                blockBaseSize: effBlockSize,
                hardRowCap: hardRowCap
            );

            _pool.ConfigureLayout(effBlockSize, effRowSpace, effGap);
            StartCoroutine(EnsureRectAndPool());

            vesselPrismController.OnBlockCreated += OnPrismBlockCreated;
            _prismSub = true;

            if (driftTrailAction)
                driftTrailAction.OnChangeDriftAltitude += OnDriftDotChanged;
        }

        /// <summary>
        /// Handy runtime button for tuning — right-click component → Reinitialize (runtime).
        /// </summary>
        [ContextMenu("Reinitialize (runtime)")]
        private void ReinitializeContextMenu()
        {
            // Keep current prefab & refs, just rebuild with new serialized layout values.
            TearDown();
            TryInitialize();
            Debug.Log("[UITrailController] Reinitialized with current layout settings.");
        }

        private static void EnsurePrefabAspect(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img) img.preserveAspect = true;
        }

        private IEnumerator EnsureRectAndPool()
        {
            yield return new WaitForEndOfFrame();

            if (_pool == null) yield break;
            var r = trailDisplayContainer.rect;
            if (r.width > 1f && r.height > 1f)
            {
                _pool.EnsurePool();
            }
            else
            {
                yield return null;
                r = trailDisplayContainer.rect;
                if (r.width > 1f && r.height > 1f) _pool.EnsurePool();
            }
        }

        private void OnDriftDotChanged(float dot)
        {
            _driftDot = Mathf.Clamp(dot, -0.9999f, 0.9999f);
        }

        private void OnPrismBlockCreated(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ)
        {
            if (_pool == null) return;

            // Keep your existing mirror math; pool handles layout in pixels.
            var ui = _pool.WorldToUi;
            if (_pool.SwingBlocks)
            {
                _pool.UpdateHead(
                    xShift: xShift * (scaleY / 2f) * ui,
                    wavelength: wavelength * ui,
                    scaleX: scaleX * scaleY * _pool.ImageScale,
                    scaleZ: scaleZ * _pool.ImageScale,
                    driftDot: _driftDot
                );
            }
            else
            {
                _pool.UpdateHead(
                    xShift: xShift * ui * scaleY,
                    wavelength: wavelength * ui * scaleY,
                    scaleX: scaleX * scaleY * _pool.ImageScale,
                    scaleZ: scaleZ * scaleY * _pool.ImageScale,
                    driftDot: _driftDot
                );
            }
        }
    }
}