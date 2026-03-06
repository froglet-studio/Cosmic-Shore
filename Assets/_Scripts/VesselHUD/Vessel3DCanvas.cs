using TMPro;
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

        [Header("Built-in Label")]
        [Tooltip("Default text shown on the canvas. Leave empty to hide.")]
        [SerializeField] private string defaultLabelText = "";

        [Tooltip("Font size for the built-in label (in points).")]
        [SerializeField] private float labelFontSize = 4f;

        Canvas _canvas;
        RectTransform _canvasRect;
        Transform _cameraTransform;
        TMP_Text _label;

        public Canvas Canvas => _canvas;
        public RectTransform ContentRoot => _canvasRect;

        /// <summary>
        /// The built-in TMP label. Use this to read or change text at runtime.
        /// </summary>
        public TMP_Text Label => _label;

        public void Initialize()
        {
            BuildCanvas();
            ApplySettings();
            BuildLabel();

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

        void BuildLabel()
        {
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(_canvasRect, false);

            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.text = defaultLabelText;
            _label.fontSize = labelFontSize;
            _label.alignment = TextAlignmentOptions.Center;
            _label.enableWordWrapping = false;

            var rt = _label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
