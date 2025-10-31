using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class TrailPool
    {
        private readonly RectTransform _container;
        private readonly VesselPrismController controller;

        public GameObject Prefab { get; }

        public readonly float WorldToUi;
        public readonly float ImageScale;
        public readonly bool  SwingBlocks;

        private GameObject[,] _pool;
        private int _poolSize;
        private readonly int _hardRowCap;

        private RectTransform _root;

        private readonly float _smoothTime;
        private float _curAngleDeg;
        private float _angleVelDeg;

        private Vector2 _legacyBlockBaseSize;

        private bool   _useExplicitLayout;
        private Vector2 _blockSizePx;   
        private float   _rowSpacingPx;  
        private float   _columnGapPx;    

        public float TargetDriftAngle { get; private set; }

        public TrailPool(
            RectTransform container,
            GameObject prefab,
            VesselPrismController controller,
            float worldToUi,
            float imageScale,
            bool swingBlocks,
            float smoothTime,
            Vector2 blockBaseSize,
            int hardRowCap = 8)
        {
            _container   = container;
            Prefab = prefab;
            this.controller = controller;

            WorldToUi   = worldToUi;
            ImageScale  = imageScale;
            SwingBlocks = swingBlocks;

            _smoothTime     = Mathf.Max(0.0001f, smoothTime);
            _curAngleDeg    = 0f;
            TargetDriftAngle = 0f;
            _angleVelDeg    = 0f;

            _hardRowCap          = Mathf.Max(1, hardRowCap);
            _legacyBlockBaseSize = blockBaseSize;

            _useExplicitLayout = false; // until ConfigureLayout is called

            _root = new GameObject("TrailPoolRoot", typeof(RectTransform)).transform as RectTransform;
            if (_root == null) return;
            _root.SetParent(_container, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            AddIgnoreLayout(_root.gameObject);
        }

        /// <summary>
        /// Enable explicit, pixel-based layout. Call this (optionally) after construction and before EnsurePool().
        /// </summary>
        public void ConfigureLayout(Vector2 blockSizePx, float rowSpacingPx, float columnGapPx)
        {
            _useExplicitLayout = true;
            _blockSizePx       = new Vector2(Mathf.Max(1f, blockSizePx.x), Mathf.Max(1f, blockSizePx.y));
            _rowSpacingPx      = Mathf.Max(1f, rowSpacingPx);
            _columnGapPx       = Mathf.Max(0f, columnGapPx);
        }

        public void Dispose()
        {
            if (_root)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
            _pool = null;
            _poolSize = 0;
        }

        public void SetTargetDriftAngle(float angleDeg) => TargetDriftAngle = angleDeg;

        public void Tick(float dt)
        {
            if (_pool == null) return;

            _curAngleDeg = Mathf.SmoothDampAngle(
                _curAngleDeg,
                TargetDriftAngle,
                ref _angleVelDeg,
                _smoothTime,
                Mathf.Infinity,
                dt);

            // Apply yaw per-row; keep children’s world Z locked
            for (int i = 0; i < _poolSize; i++)
            {
                var rowParent = _pool[i, 0]?.transform?.parent as RectTransform;
                if (!rowParent) continue;

                var e = rowParent.localEulerAngles;
                rowParent.localEulerAngles = new Vector3(e.x, _curAngleDeg, e.z);

                for (int j = 0; j < 2; j++)
                {
                    var rt = _pool[i, j]?.transform as RectTransform;
                    if (!rt) continue;
                    NormalizeBlockRect(rt);
                    SetChildWorldZ(rt, -90f);
                }
            }
        }

        /// <summary>
        /// Ensures pool rows/blocks exist. If explicit pixel layout is configured, uses it;
        /// otherwise falls back to legacy world→UI math.
        /// </summary>
        public void EnsurePool(float scaleY = 1f)
        {
            if (_poolSize > 0) return;

            var rect = _container.rect;
            if (rect.height < 1f) return; // defer until layout

            float effectiveRowSpacing =
                _useExplicitLayout
                    ? Mathf.Max(1f, _rowSpacingPx)
                    : controller.MinWaveLength * WorldToUi * (SwingBlocks ? 1f : Mathf.Max(0.0001f, scaleY));

            int desired = Mathf.CeilToInt(rect.height / Mathf.Max(0.0001f, effectiveRowSpacing));
            _poolSize   = Mathf.Clamp(desired, 1, _hardRowCap);

            _pool = new GameObject[_poolSize, 2];

            for (int i = 0; i < _poolSize; i++)
            {
                var rowParent = new GameObject($"TrailRow_{i}", typeof(RectTransform)).transform as RectTransform;
                if (!rowParent) continue;
                rowParent.SetParent(_root, false);
                rowParent.anchorMin = rowParent.anchorMax = new Vector2(0.5f, 0.5f);
                rowParent.pivot = new Vector2(0.5f, 0.5f);
                AddIgnoreLayout(rowParent.gameObject);

                // Place rows top→down; we yaw rows in Tick()
                rowParent.localPosition = new Vector3(
                    0f,
                    -i * effectiveRowSpacing + (rect.height * 0.5f),
                    0f);
                rowParent.localRotation = Quaternion.identity;

                for (int j = 0; j < 2; j++)
                {
                    var block = Object.Instantiate(Prefab);
                    var blockRt = block.transform as RectTransform;
                    if (blockRt)
                    {
                        blockRt.SetParent(rowParent, false);

                        // preserveAspect in case the instance lacks it
                        var img = block.GetComponent<Image>();
                        if (img) img.preserveAspect = true;

                        NormalizeBlockRect(blockRt); // size via (explicit override or legacy base)
                        SetChildWorldZ(blockRt, -90f); // lock world Z

                        // Initial X offset:
                        // - If explicit ColumnGapPx is provided, center columns about it
                        // - Otherwise, legacy: use controller.Gap (mirrored)
                        var initialHalfGap =
                            _useExplicitLayout
                                ? _columnGapPx * 0.5f
                                : controller.Gap;

                        blockRt.localPosition = new Vector3(j * 2f * initialHalfGap - initialHalfGap, 0f, 0f);
                        blockRt.localScale = Vector3.zero;
                    }

                    AddIgnoreLayout(block);
                    block.SetActive(true);
                    _pool[i, j] = block;
                }
            }
        }

        public void UpdateHead(float xShift, float wavelength, float scaleX, float scaleZ, float? driftDot)
        {
            if (_pool == null) return;

            var rectHeight = _container.rect.height;

            // Respect explicit overrides where present
            float effectiveWavelength =
                _useExplicitLayout ? Mathf.Max(1f, _rowSpacingPx)
                                   : Mathf.Max(0.0001f, wavelength);

            float effectiveHalfGap =
                _useExplicitLayout ? (_columnGapPx * 0.5f)
                                   : xShift; // legacy uses xShift directly

            // Head row at center; blocks slide horizontally by effectiveHalfGap
            for (int j = 0; j < 2; j++)
            {
                var head = _pool[0, j].transform as RectTransform;
                if (!head) continue;

                // Visual size: X width (mirrored on left), Y height
                head.localScale = new Vector3(j * 2f * scaleX - scaleX, scaleZ, 1f);

                var parent = head.parent as RectTransform;
                if (parent != null) parent.localPosition = new Vector3(0f, rectHeight * 0.5f, 0f);
                head.localPosition   = new Vector3(j * 2f * effectiveHalfGap - effectiveHalfGap, 0f, 0f);

                NormalizeBlockRect(head);
                SetChildWorldZ(head, -90f);
            }

            // Shift tail rows downward by current effective row spacing
            for (int i = _poolSize - 1; i > 0; i--)
            {
                for (int j = 0; j < 2; j++)
                {
                    var cur  = _pool[i, j].transform as RectTransform;
                    var prev = _pool[i - 1, j].transform as RectTransform;
                    if (!cur || !prev) continue;

                    cur.localScale    = prev.localScale;
                    cur.localPosition = prev.localPosition;

                    var parent = cur.parent as RectTransform;
                    parent.localPosition = new Vector3(
                        0f,
                        -i * effectiveWavelength + (rectHeight * 0.5f),
                        0f);

                    NormalizeBlockRect(cur);
                    SetChildWorldZ(cur, -90f);
                }

                // Only show rows within view height
                bool under = i < Mathf.CeilToInt(rectHeight / Mathf.Max(0.0001f, effectiveWavelength));
                _pool[i, 1].transform.parent.gameObject.SetActive(under);
            }
        }

        // --- helpers ---

        private void NormalizeBlockRect(RectTransform rt)
        {
            if (!rt) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            var size = _useExplicitLayout ? _blockSizePx : _legacyBlockBaseSize;
            rt.sizeDelta = size;
        }

        private static void SetChildWorldZ(RectTransform child, float worldZDeg)
        {
            if (!child) return;
            var parent      = child.parent as RectTransform;
            var targetWorld = Quaternion.Euler(0f, 0f, worldZDeg);
            var parentWorld = parent ? parent.rotation : Quaternion.identity;
            child.localRotation = Quaternion.Inverse(parentWorld) * targetWorld;
        }

        private static void AddIgnoreLayout(GameObject go)
        {
            // Optional: add a LayoutElement and set ignoreLayout=true if you use LayoutGroups
            // var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            // le.ignoreLayout = true;
        }
    }
}
