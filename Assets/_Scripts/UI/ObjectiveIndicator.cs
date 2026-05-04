using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Edge-of-screen pointer that highlights an off-screen objective. The
    /// objective is supplied by an <see cref="IObjectiveProvider"/> — wired in
    /// the inspector or via <see cref="Configure"/> at runtime. The indicator
    /// is game-mode-agnostic: it only handles world-to-screen math, edge
    /// clamping, rotation, and show/hide.
    ///
    /// While the objective is on-screen the icon is hidden; while it is
    /// off-screen the icon clamps to the parent rect's edge in the direction
    /// of the target and rotates to point at it.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveIndicator : MonoBehaviour
    {
        [Header("Objective")]
        [Tooltip("Component implementing IObjectiveProvider that supplies the world target to point at.")]
        [SerializeField, RequireInterface(typeof(IObjectiveProvider))]
        Object provider;

        [Header("Visual")]
        [Tooltip("RectTransform of the icon that gets repositioned and rotated. Required.")]
        [SerializeField] RectTransform icon;

        [Tooltip("Optional CanvasGroup on the icon root — fades in/out instead of toggling active.")]
        [SerializeField] CanvasGroup canvasGroup;

        [Tooltip("Optional distance label (TextMeshPro). Hidden when null.")]
        [SerializeField] TMP_Text distanceText;

        [Tooltip("Distance unit suffix appended to the number (e.g. \"m\").")]
        [SerializeField] string distanceSuffix = "m";

        [Header("Layout")]
        [Tooltip("Pixels of inset from the parent rect edge.")]
        [SerializeField] float edgePadding = 60f;

        [Tooltip("Sprite art that points UP by default needs -90; sprite that points RIGHT needs 0.")]
        [SerializeField] float spriteRotationOffset = -90f;

        [Header("Lifecycle")]
        [Tooltip("Hide while local vessel/camera is unavailable. Recommended.")]
        [SerializeField] bool hideWhenNoCamera = true;

        IObjectiveProvider _providerCached;
        RectTransform _parentRect;
        Canvas _canvas;
        bool? _visible;

        IObjectiveProvider Provider =>
            _providerCached ??= provider as IObjectiveProvider;

        /// <summary>
        /// Wires a provider at runtime. Use when constructing the indicator
        /// programmatically rather than wiring via the inspector.
        /// </summary>
        public void Configure(IObjectiveProvider providerInstance)
        {
            _providerCached = providerInstance;
        }

        void Awake()
        {
            _parentRect = icon != null ? icon.parent as RectTransform : null;
            _canvas = GetComponentInParent<Canvas>();

            // Force consistent anchoring so anchoredPosition is "offset from
            // parent's centre, in parent's local pixels" — independent of the
            // parent's pivot or the icon's original anchor settings.
            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
            }

            SetVisible(false);
        }

        void OnValidate()
        {
            if (icon != null && canvasGroup == null)
                canvasGroup = icon.GetComponent<CanvasGroup>();
        }

        void LateUpdate()
        {
            if (icon == null || Provider == null || _parentRect == null)
            {
                SetVisible(false);
                return;
            }

            if (!Provider.TryGetObjective(out var target) || target == null)
            {
                SetVisible(false);
                return;
            }

            var cam = ResolveCamera();
            if (cam == null)
            {
                if (hideWhenNoCamera) SetVisible(false);
                return;
            }

            var targetPos = target.position;
            var screenPos = cam.WorldToScreenPoint(targetPos);
            bool inFront = screenPos.z > 0f;

            if (inFront &&
                screenPos.x >= 0f && screenPos.x <= Screen.width &&
                screenPos.y >= 0f && screenPos.y <= Screen.height)
            {
                SetVisible(false);
                return;
            }

            // Behind the camera: WorldToScreenPoint produces a flipped value.
            // Mirror through the screen centre so direction-from-centre still
            // points roughly toward the target.
            if (!inFront)
                screenPos = new Vector3(Screen.width - screenPos.x, Screen.height - screenPos.y, screenPos.z);

            PositionAtEdge(screenPos);
            UpdateDistance(cam.transform.position, targetPos);
            SetVisible(true);
        }

        void PositionAtEdge(Vector3 screenPos)
        {
            var canvasCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;

            // Parent rect's centre in screen space.
            Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(canvasCam, _parentRect.position);
            Vector2 dirScreen = (Vector2)screenPos - centerScreen;
            if (dirScreen.sqrMagnitude < 1e-4f) dirScreen = Vector2.up;

            // Convert the screen-space direction into parent-rect local pixels
            // so canvas scaling / rotation is handled correctly.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, centerScreen, canvasCam, out Vector2 localCenter);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _parentRect, centerScreen + dirScreen, canvasCam, out Vector2 localTarget);
            Vector2 dirLocal = localTarget - localCenter;
            if (dirLocal.sqrMagnitude < 1e-4f) dirLocal = Vector2.up;

            // Clamp to the parent rect's edge, inset by edgePadding.
            Rect rect = _parentRect.rect;
            float halfW = rect.width * 0.5f - edgePadding;
            float halfH = rect.height * 0.5f - edgePadding;
            if (halfW < 0f) halfW = 0f;
            if (halfH < 0f) halfH = 0f;

            Vector2 unit = dirLocal.normalized;
            float tx = Mathf.Abs(unit.x) > 1e-4f ? halfW / Mathf.Abs(unit.x) : float.PositiveInfinity;
            float ty = Mathf.Abs(unit.y) > 1e-4f ? halfH / Mathf.Abs(unit.y) : float.PositiveInfinity;
            float t = Mathf.Min(tx, ty);

            // Icon is anchored at parent's centre (forced in Awake), so
            // anchoredPosition is offset from that centre in parent pixels.
            icon.anchoredPosition = unit * t;

            float angleDeg = Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg + spriteRotationOffset;
            icon.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
        }

        void UpdateDistance(Vector3 cameraPos, Vector3 targetPos)
        {
            if (distanceText == null) return;
            float dist = Vector3.Distance(cameraPos, targetPos);
            distanceText.text = $"{Mathf.RoundToInt(dist)}{distanceSuffix}";
        }

        Camera ResolveCamera()
        {
            if (CameraManager.Instance != null
                && CameraManager.Instance.GetActiveController() is CustomCameraController active
                && active.Camera != null)
                return active.Camera;
            return Camera.main;
        }

        void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            else if (icon != null)
            {
                icon.gameObject.SetActive(visible);
            }
        }

        // ── Runtime construction ─────────────────────────────────────────

        /// <summary>
        /// Builds a fully configured indicator at runtime: a full-screen
        /// container under <paramref name="parent"/> with an arrow icon child.
        /// Use when no editor-wired indicator exists in the scene.
        /// </summary>
        public static ObjectiveIndicator CreateRuntime(Transform parent, IObjectiveProvider providerInstance)
        {
            var rootGo = new GameObject("ObjectiveIndicator", typeof(RectTransform));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.SetAsLastSibling();

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasGroup));
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.SetParent(rootRect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(80f, 80f);

            var iconCg = iconGo.GetComponent<CanvasGroup>();
            iconCg.alpha = 0f;
            iconCg.interactable = false;
            iconCg.blocksRaycasts = false;

            var arrowText = iconGo.AddComponent<TextMeshProUGUI>();
            arrowText.text = "▲"; // ▲
            arrowText.fontSize = 60f;
            arrowText.alignment = TextAlignmentOptions.Center;
            arrowText.raycastTarget = false;
            arrowText.color = new Color(1f, 0.85f, 0.2f, 1f);

            var indicator = rootGo.AddComponent<ObjectiveIndicator>();
            indicator.icon = iconRect;
            indicator.canvasGroup = iconCg;
            indicator.Configure(providerInstance);
            return indicator;
        }
    }
}
