using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Manages a manually-created World Space canvas on the vessel prefab.
    /// Set up the Canvas in the editor, assign it here, then call <see cref="Initialize"/>.
    /// </summary>
    public class Vessel3DCanvas : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Vessel3DCanvasSettingsSO settings;

        [Header("Canvas Reference")]
        [Tooltip("Assign the World Space Canvas you created in the editor.")]
        [SerializeField] private Canvas canvas;

        Transform _cameraTransform;

        public Canvas Canvas => canvas;
        public RectTransform ContentRoot => canvas ? canvas.GetComponent<RectTransform>() : null;

        public void Initialize()
        {
            if (!canvas)
            {
                Debug.LogError($"[Vessel3DCanvas] No Canvas assigned on {gameObject.name}. " +
                               "Create a World Space Canvas as a child and assign it.", this);
                return;
            }

            ApplySettings();
        }

        void ApplySettings()
        {
            var canvasRect = canvas.GetComponent<RectTransform>();

            if (!settings)
            {
                canvasRect.localPosition = new Vector3(0f, 2f, 0f);
                canvasRect.sizeDelta = new Vector2(100f, 50f);
                canvas.sortingOrder = 10;
                return;
            }

            canvasRect.localPosition = settings.localOffset;
            canvasRect.sizeDelta = settings.canvasSize * settings.pixelsPerUnit;
            canvasRect.localScale = Vector3.one / settings.pixelsPerUnit;
            canvas.sortingOrder = settings.sortingOrder;
        }

        void LateUpdate()
        {
            if (!canvas || !ShouldBillboard()) return;

            EnsureCamera();
            if (!_cameraTransform) return;

            var canvasTransform = canvas.transform;
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
