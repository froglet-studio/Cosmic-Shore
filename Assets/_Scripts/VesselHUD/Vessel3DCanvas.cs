using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Creates and manages a World Space canvas positioned above the vessel.
    /// Attach this to the vessel prefab root (or a child).
    /// Call <see cref="Initialize"/> after the vessel is fully set up.
    /// </summary>
    public class Vessel3DCanvas : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vessel3DCanvasSettingsSO settings;

        [Header("Content (optional)")]
        [Tooltip("If set, this prefab is instantiated as the canvas content on Initialize.")]
        [SerializeField] private GameObject contentPrefab;

        Canvas _canvas;
        RectTransform _canvasRect;
        Transform _cameraTransform;

        public Canvas Canvas => _canvas;
        public RectTransform ContentRoot => _canvasRect;

        public void Initialize()
        {
            BuildCanvas();
            ApplySettings();

            if (contentPrefab)
            {
                var instance = Instantiate(contentPrefab, _canvasRect);
                var rt = instance.GetComponent<RectTransform>();
                if (rt)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = Vector3.one;
                }
            }
        }

        void BuildCanvas()
        {
            var canvasGO = new GameObject("Vessel3DCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        void ApplySettings()
        {
            if (!settings)
            {
                // Sensible defaults when no SO is assigned
                _canvasRect.localPosition = new Vector3(0f, 2f, 0f);
                _canvasRect.sizeDelta = new Vector2(100f, 50f);
                _canvas.sortingOrder = 10;
                return;
            }

            _canvasRect.localPosition = settings.localOffset;
            _canvasRect.sizeDelta = settings.canvasSize * settings.pixelsPerUnit;
            _canvasRect.localScale = Vector3.one / settings.pixelsPerUnit;
            _canvas.sortingOrder = settings.sortingOrder;
        }

        void LateUpdate()
        {
            if (!_canvas || !ShouldBillboard()) return;

            EnsureCamera();
            if (!_cameraTransform) return;

            var canvasTransform = _canvas.transform;
            if (settings && settings.lockVerticalAxis)
            {
                var lookDir = _cameraTransform.position - canvasTransform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.001f)
                    canvasTransform.rotation = Quaternion.LookRotation(lookDir);
            }
            else
            {
                canvasTransform.rotation = _cameraTransform.rotation;
            }
        }

        bool ShouldBillboard() => !settings || settings.billboard;

        void EnsureCamera()
        {
            if (_cameraTransform) return;
            var cam = Camera.main;
            if (cam) _cameraTransform = cam.transform;
        }
    }
}
