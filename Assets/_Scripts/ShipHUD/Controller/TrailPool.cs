using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class TrailPool
    {
        private readonly RectTransform _container;
        private readonly GameObject _blockPrefab;
        private readonly VesselPrismController controller;

        public GameObject Prefab => _blockPrefab;
        public readonly float WorldToUi;
        public readonly float ImageScale;
        public readonly bool  SwingBlocks;

        private GameObject[,] _pool;
        private int _poolSize;
        private readonly int _hardRowCap;

        // Layout-safe root (we do NOT rotate this)
        private RectTransform _root;

        // Drift smoothing
        private readonly float _smoothTime;
        private float _curAngleDeg;
        private float _targetAngleDeg;
        private float _angleVelDeg;
        private Vector2 _blockBaseSize;

        public float TargetDriftAngle => _targetAngleDeg;

        public TrailPool(RectTransform container, GameObject prefab, VesselPrismController controller,
            float worldToUi, float imageScale, bool swingBlocks, float smoothTime, Vector2 blockBaseSize,
            int hardRowCap = 8)
        {
            _container   = container;
            _blockPrefab = prefab;
            this.controller     = controller;
            WorldToUi    = worldToUi;
            ImageScale   = imageScale;
            SwingBlocks  = swingBlocks;

            _smoothTime     = Mathf.Max(0.0001f, smoothTime);
            _curAngleDeg    = 0f;
            _targetAngleDeg = 0f;
            _angleVelDeg    = 0f;
            _hardRowCap     = Mathf.Max(1, hardRowCap);
            _blockBaseSize = blockBaseSize;

            _root = new GameObject("TrailPoolRoot", typeof(RectTransform)).transform as RectTransform;
            _root.SetParent(_container, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot     = new Vector2(0.5f, 0.5f);
            AddIgnoreLayout(_root.gameObject);
        }

        public void Dispose()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
                _root = null;
            }
            _pool = null;
            _poolSize = 0;
        }

        public void SetTargetDriftAngle(float angleDeg) => _targetAngleDeg = angleDeg;

        public void Tick(float dt)
        {
            if (_pool == null) return;

            _curAngleDeg = Mathf.SmoothDampAngle(
                _curAngleDeg,
                _targetAngleDeg,
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

        public void EnsurePool(float scaleY = 1f)
        {
            if (_poolSize > 0) return;

            var rect = _container.rect;
            if (rect.height < 1f) return; // defer until layout

            float denom   = controller.MinWaveLength * WorldToUi * (SwingBlocks ? 1f : Mathf.Max(0.0001f, scaleY));
            int desired   = Mathf.Max(1, Mathf.CeilToInt(rect.height / Mathf.Max(0.0001f, denom)));
            _poolSize     = Mathf.Clamp(desired, 1, _hardRowCap);

            _pool = new GameObject[_poolSize, 2];

            for (int i = 0; i < _poolSize; i++)
            {
                var rowParent = new GameObject($"TrailRow_{i}", typeof(RectTransform)).transform as RectTransform;
                rowParent.SetParent(_root, false);
                rowParent.anchorMin = rowParent.anchorMax = new Vector2(0.5f, 0.5f);
                rowParent.pivot     = new Vector2(0.5f, 0.5f);
                AddIgnoreLayout(rowParent.gameObject);

                // position row downwards; we yaw rows in Tick()
                rowParent.localPosition = new Vector3(
                    0f,
                    -i * controller.MinWaveLength * WorldToUi + (rect.height * 0.5f),
                    0f);
                rowParent.localRotation = Quaternion.identity;

                for (int j = 0; j < 2; j++)
                {
                    var block   = Object.Instantiate(_blockPrefab);
                    var blockRt = block.transform as RectTransform;
                    blockRt.SetParent(rowParent, false);

                    // in case this instance lacks preserveAspect
                    var img = block.GetComponent<Image>();
                    if (img != null) img.preserveAspect = true;

                    NormalizeBlockRect(blockRt);    // stable rect (e.g., 120x120)
                    SetChildWorldZ(blockRt, -90f);  // lock world Z

                    // left/right along X by Gap; vertically centered
                    blockRt.localPosition = new Vector3(j * 2f * controller.Gap - controller.Gap, 0f, 0f);
                    blockRt.localScale    = Vector3.zero;

                    AddIgnoreLayout(block);
                    block.SetActive(true);
                    _pool[i, j] = block;
                }
            }
        }

        /// <summary>
        /// xShift: horizontal half-separation of the pair (mirrored),
        /// wavelength: vertical spacing between rows (in UI units),
        /// scaleX: "width" of each block vis (applied on X),
        /// scaleZ: "height" of each block vis (applied on Y),
        /// driftDot: optional to keep compatibility with non-drift ships.
        /// </summary>
        public void UpdateHead(float xShift, float wavelength, float scaleX, float scaleZ, float? driftDot)
        {
            if (_pool == null) return;

            var rectHeight = _container.rect.height;

            // Head row at center; blocks slide horizontally by xShift
            for (int j = 0; j < 2; j++)
            {
                var head = _pool[0, j].transform as RectTransform;
                if (!head) continue;

                // Vertical depiction: localScale.x = visual width, localScale.y = visual height
                head.localScale = new Vector3(j * 2f * scaleX - scaleX, scaleZ, 1f);

                var parent = head.parent as RectTransform;
                parent.localPosition = new Vector3(0f, rectHeight * 0.5f, 0f);
                head.localPosition   = new Vector3(j * 2f * xShift - xShift, 0f, 0f);

                NormalizeBlockRect(head);
                SetChildWorldZ(head, -90f);
            }

            // Shift tail rows downward by current wavelength
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
                        -i * Mathf.Max(0.0001f, wavelength) + (rectHeight * 0.5f),
                        0f);

                    NormalizeBlockRect(cur);
                    SetChildWorldZ(cur, -90f);
                }

                bool under = i < Mathf.CeilToInt(rectHeight / Mathf.Max(0.0001f, wavelength));
                _pool[i, 1].transform.parent.gameObject.SetActive(under);
            }
        }

        // --- helpers ---

        private void NormalizeBlockRect(RectTransform rt)
        {
            if (!rt) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = _blockBaseSize; 
        }

        // Force child's WORLD rotation to an exact Z angle (independent of parent rotation)
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
            // var le = go.GetComponent<LayoutElement>();
            // if (!le) le = go.AddComponent<LayoutElement>();
            // le.ignoreLayout = true;
        }
    }
}
