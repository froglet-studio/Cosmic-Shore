using System.Text;
using CosmicShore.Gameplay;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Edge-of-screen pointer that highlights an off-screen objective.
    ///
    /// While the objective is on-screen the icon is hidden; while it is
    /// off-screen the icon clamps to the parent rect's edge in the direction
    /// of the target and rotates to point at it.
    ///
    /// Set <see cref="debugAlwaysVisible"/> to true to force the icon to stay
    /// on the right edge at all times - useful for verifying that the UI
    /// element renders at all before wiring up a provider.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjectiveIndicator : MonoBehaviour
    {
        [Header("Objective")]
        [Tooltip("Component implementing IObjectiveProvider that supplies the world target to point at. Optional when debugAlwaysVisible is on.")]
        [SerializeField, RequireInterface(typeof(IObjectiveProvider))]
        Object provider;

        [Header("Visual")]
        [Tooltip("RectTransform of the icon that gets repositioned and rotated. Required.")]
        [SerializeField] RectTransform icon;

        [Tooltip("Optional CanvasGroup on the icon root - fades in/out instead of toggling active.")]
        [SerializeField] CanvasGroup canvasGroup;

        [Tooltip("Optional distance label (TextMeshPro). Hidden when null.")]
        [SerializeField] TMP_Text distanceText;

        [Tooltip("Distance unit suffix appended to the number (e.g. \"m\").")]
        [SerializeField] string distanceSuffix = "m";

        [Header("Layout")]
        [Tooltip("Pixels of inset from the parent rect edge.")]
        [SerializeField] float edgePadding = 55f;

        [Tooltip("Sprite art that points UP by default needs -90; sprite that points RIGHT needs 0.")]
        [SerializeField] float spriteRotationOffset = 0f;

        [Header("Debug")]
        [Tooltip("Always show the icon at a fixed edge position, ignoring providers and on-screen checks. Use to verify the UI renders.")]
        [SerializeField] bool debugAlwaysVisible = false;

        /// <summary>
        /// The on-screen hide rule's reach. While the target is inside the screen rect the
        /// arrow hides — but only if the target is also within this distance of the camera.
        /// Default: no limit (the legacy rule). A provider whose objective is a small body that
        /// can be on screen and yet unreadable (the Arkway's Ark, thousands of units down its
        /// corridor axis) lowers it so the arrow keeps pointing at a speck.
        /// </summary>
        public float HideOnScreenWithin { get; set; } = float.PositiveInfinity;

        IObjectiveProvider _providerCached;
        RectTransform _parentRect;
        Canvas _canvas;
        Camera _canvasCamera;
        Camera _cachedCamera;
        float _cameraCacheRefreshAt;
        int _lastDistanceInt = int.MinValue;
        string _lastDistanceSuffix;
        readonly StringBuilder _distanceTextBuilder = new(16);
        bool? _visible;

        const float CameraCacheTtlSeconds = 0.5f;

        static readonly ProfilerMarker s_LateUpdateMarker =
            new("ObjectiveIndicator.LateUpdate");
        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("ObjectiveIndicator.TryGetObjective");
        static readonly ProfilerMarker s_WorldToScreenMarker =
            new("ObjectiveIndicator.WorldToScreen");
        static readonly ProfilerMarker s_ResolveCameraMarker =
            new("ObjectiveIndicator.ResolveCamera");
        static readonly ProfilerMarker s_PositionAtEdgeMarker =
            new("ObjectiveIndicator.PositionAtEdge");
        static readonly ProfilerMarker s_UpdateDistanceMarker =
            new("ObjectiveIndicator.UpdateDistance");

        IObjectiveProvider Provider =>
            _providerCached ??= provider as IObjectiveProvider;

        /// <summary>Wires a provider at runtime (alternative to inspector wiring).</summary>
        public void Configure(IObjectiveProvider providerInstance)
        {
            _providerCached = providerInstance;
        }

        void Awake()
        {
            _parentRect = icon != null ? icon.parent as RectTransform : null;
            _canvas = GetComponentInParent<Canvas>();
            _canvasCamera = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera
                : null;

            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.pivot = new Vector2(0.5f, 0.5f);
            }

            // Force-visible on Awake when in debug mode so the user sees something
            // immediately even before LateUpdate runs.
            if (debugAlwaysVisible)
            {
                ForceVisible(true);
                PlaceAtFixedEdge();
            }
            else
            {
                SetVisible(false);
            }
        }

        void OnValidate()
        {
            if (icon != null && canvasGroup == null)
                canvasGroup = icon.GetComponent<CanvasGroup>();
        }

        void LateUpdate()
        {
            using (s_LateUpdateMarker.Auto())
            {
                if (icon == null || _parentRect == null) return;

                if (debugAlwaysVisible)
                {
                    ForceVisible(true);
                    PlaceAtFixedEdge();
                    return;
                }

                Transform target;
                using (s_TryGetObjectiveMarker.Auto())
                {
                    if (Provider == null || !Provider.TryGetObjective(out target) || target == null)
                    {
                        SetVisible(false);
                        return;
                    }
                }

                var cam = ResolveCamera();
                if (cam == null)
                {
                    SetVisible(false);
                    return;
                }

                float screenW = Screen.width;
                float screenH = Screen.height;

                var targetPos = target.position;
                Vector3 screenPos;
                using (s_WorldToScreenMarker.Auto())
                    screenPos = cam.WorldToScreenPoint(targetPos);
                bool inFront = screenPos.z > 0f;

                if (inFront &&
                    screenPos.x >= 0f && screenPos.x <= screenW &&
                    screenPos.y >= 0f && screenPos.y <= screenH &&
                    (float.IsPositiveInfinity(HideOnScreenWithin) ||
                     (targetPos - cam.transform.position).sqrMagnitude <= HideOnScreenWithin * HideOnScreenWithin))
                {
                    SetVisible(false);
                    return;
                }

                if (!inFront)
                    screenPos = new Vector3(screenW - screenPos.x, screenH - screenPos.y, screenPos.z);

                PositionAtEdge(screenPos);
                UpdateDistance(cam.transform.position, targetPos);
                SetVisible(true);
            }
        }

        /// <summary>Fixed-position debug placement: hugs the right edge, mid-height.</summary>
        void PlaceAtFixedEdge()
        {
            Rect rect = _parentRect.rect;
            float halfW = Mathf.Max(0f, rect.width * 0.5f - edgePadding);
            icon.anchoredPosition = new Vector2(halfW, 0f);
            icon.localRotation = Quaternion.identity;
        }

        void PositionAtEdge(Vector3 screenPos)
        {
            using (s_PositionAtEdgeMarker.Auto())
            {
                var canvasCam = _canvasCamera;

                Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(canvasCam, _parentRect.position);
                Vector2 dirScreen = (Vector2)screenPos - centerScreen;
                if (dirScreen.sqrMagnitude < 1e-4f) dirScreen = Vector2.up;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, centerScreen, canvasCam, out Vector2 localCenter);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, centerScreen + dirScreen, canvasCam, out Vector2 localTarget);
                Vector2 dirLocal = localTarget - localCenter;
                if (dirLocal.sqrMagnitude < 1e-4f) dirLocal = Vector2.up;

                Rect rect = _parentRect.rect;
                float halfW = Mathf.Max(0f, rect.width * 0.5f - edgePadding);
                float halfH = Mathf.Max(0f, rect.height * 0.5f - edgePadding);

                Vector2 unit = dirLocal.normalized;
                float tx = Mathf.Abs(unit.x) > 1e-4f ? halfW / Mathf.Abs(unit.x) : float.PositiveInfinity;
                float ty = Mathf.Abs(unit.y) > 1e-4f ? halfH / Mathf.Abs(unit.y) : float.PositiveInfinity;
                float t = Mathf.Min(tx, ty);

                icon.anchoredPosition = unit * t;

                float angleDeg = Mathf.Atan2(unit.y, unit.x) * Mathf.Rad2Deg + spriteRotationOffset;
                icon.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            }
        }

        void UpdateDistance(Vector3 cameraPos, Vector3 targetPos)
        {
            using (s_UpdateDistanceMarker.Auto())
            {
                if (distanceText == null) return;
                float dist = Vector3.Distance(cameraPos, targetPos);
                int distInt = Mathf.RoundToInt(dist);
                if (distInt == _lastDistanceInt
                    && ReferenceEquals(distanceSuffix, _lastDistanceSuffix))
                    return;

                _lastDistanceInt = distInt;
                _lastDistanceSuffix = distanceSuffix;

                _distanceTextBuilder.Clear();
                _distanceTextBuilder.Append(distInt);
                if (!string.IsNullOrEmpty(distanceSuffix))
                    _distanceTextBuilder.Append(distanceSuffix);
                distanceText.SetText(_distanceTextBuilder);
            }
        }

        Camera ResolveCamera()
        {
            using (s_ResolveCameraMarker.Auto())
            {
                float now = Time.unscaledTime;
                if (_cachedCamera != null && now < _cameraCacheRefreshAt)
                    return _cachedCamera;

                _cameraCacheRefreshAt = now + CameraCacheTtlSeconds;

                if (CameraManager.Instance != null
                    && CameraManager.Instance.GetActiveController() is CustomCameraController active
                    && active.Camera != null)
                {
                    _cachedCamera = active.Camera;
                    return _cachedCamera;
                }
                _cachedCamera = Camera.main;
                return _cachedCamera;
            }
        }

        void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            ApplyVisibility(visible);
        }

        /// <summary>Bypasses the cache so the visible state is reasserted every frame in debug mode.</summary>
        void ForceVisible(bool visible)
        {
            _visible = visible;
            ApplyVisibility(visible);
        }

        void ApplyVisibility(bool visible)
        {
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
        /// container under <paramref name="parent"/> with a procedural arrow
        /// icon (<see cref="ObjectiveArrowGraphic"/>). Use when no
        /// editor-wired indicator exists in the scene.
        /// </summary>
        public static ObjectiveIndicator CreateRuntime(Transform parent, IObjectiveProvider providerInstance)
        {
            // Sub-canvas isolation: a dedicated Canvas component on the
            // indicator root means any Graphic rebuild under the indicator
            // (e.g. ObjectiveArrowGraphic vertices) only rebuilds the
            // indicator's batched mesh, not the parent HUD canvas with all
            // its score/timer/lifeform/notification/player-card graphics.
            // This is the single biggest perf win for this widget on a
            // shared HUD canvas.
            var rootGo = new GameObject("ObjectiveIndicator",
                typeof(RectTransform), typeof(Canvas));
            // Deactivate while we wire things up so Awake doesn't fire with
            // unassigned serialized references (AddComponent runs Awake
            // immediately on active GameObjects).
            rootGo.SetActive(false);

            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.SetAsLastSibling();

            // overrideSorting=false so the sub-canvas inherits the parent's
            // sort order - the indicator renders on top by virtue of being
            // last sibling, not by overriding sort.
            var subCanvas = rootGo.GetComponent<Canvas>();
            subCanvas.overrideSorting = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasGroup));
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.SetParent(rootRect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(60f, 60f);

            var iconCg = iconGo.GetComponent<CanvasGroup>();
            iconCg.alpha = 1f;
            iconCg.interactable = false;
            iconCg.blocksRaycasts = false;

            // Procedural arrow Graphic - three layered triangles (halo, stroke,
            // core) with pulse animation. Points right at rotation 0; no
            // sprite/font dependency.
            var arrow = iconGo.AddComponent<ObjectiveArrowGraphic>();
            arrow.raycastTarget = false;

            var indicator = rootGo.AddComponent<ObjectiveIndicator>();
            indicator.icon = iconRect;
            indicator.canvasGroup = iconCg;
            indicator.Configure(providerInstance);

            // Now safe to activate - Awake runs with fields populated.
            rootGo.SetActive(true);

            return indicator;
        }
    }
}
