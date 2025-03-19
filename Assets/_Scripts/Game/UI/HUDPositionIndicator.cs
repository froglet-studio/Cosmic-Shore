using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attaches to a UI Image that serves as an indicator pointing towards a target object.
/// The indicator stays at the edge of the screen when the target is off-screen.
/// </summary>
public class HUDPositionIndicator : MonoBehaviour
{
    [Tooltip("The target object to track (must have a Renderer component)")]
    [SerializeField] private GameObject targetObject;

    [Tooltip("Padding from the edge of the screen")]
    [SerializeField] private float screenEdgePadding = 30f;

    [Tooltip("Whether to show the indicator when the target is on screen")]
    [SerializeField] private bool showWhenVisible = true;

    [Tooltip("The color of the indicator when the target is close")]
    [SerializeField] private Color closeColor = new Color(.3f, 1, .1f,.01f);

    [Tooltip("The color of the indicator when the target is far")]
    [SerializeField] private Color farColor = new Color(.15f, .5f, .05f,.5f);

    [Tooltip("The distance at which the color begins to transition")]
    [SerializeField] private float colorTransitionDistance = 100f;

    private Camera mainCamera;
    private RectTransform rectTransform;
    private Image image;
    private Canvas canvas;
    private RectTransform canvasRectTransform;
    private Renderer targetRenderer;
    private Transform targetTransform;

    private void Awake()
    {
        mainCamera = Camera.main;
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        canvas = GetComponentInParent<Canvas>();
        canvasRectTransform = canvas.GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("Target object is not assigned to the ObjectTracker script!");
            gameObject.SetActive(false);
            return;
        }

        // Get the renderer component
        targetRenderer = targetObject.GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogError("Target object must have a Renderer component!");
            gameObject.SetActive(false);
            return;
        }

        targetTransform = targetRenderer.transform;
    }

    private void Update()
    {
        if (targetObject == null || targetRenderer == null) return;

        UpdateIndicatorVisibility();
        UpdateIndicatorPosition();
        UpdateIndicatorColor();
    }

    /// <summary>
    /// Updates the visibility of the indicator based on whether the target is visible on screen
    /// </summary>
    private void UpdateIndicatorVisibility()
    {
        bool isVisible = IsTargetVisible();
        image.enabled = isVisible ? showWhenVisible : true;
    }

    /// <summary>
    /// Checks if the target is visible on screen using the renderer's IsVisible property
    /// </summary>
    private bool IsTargetVisible()
    {
        // Use the built-in renderer visibility check
        if (targetRenderer.isVisible)
        {
            // Double-check that it's in front of the camera (not behind)
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(targetTransform.position);
            return viewportPos.z > 0;
        }
        return false;
    }

    /// <summary>
    /// Updates the position of the indicator to point towards the target
    /// </summary>
    private void UpdateIndicatorPosition()
    {
        // Convert target position to viewport coordinates (0, 0) to (1, 1)
        Vector3 targetViewportPos = mainCamera.WorldToViewportPoint(targetTransform.position);

        // Check if target is behind the camera
        if (targetViewportPos.z < 0)
        {
            // If target is behind, invert the position
            targetViewportPos.x = 1.0f - targetViewportPos.x;
            targetViewportPos.y = 1.0f - targetViewportPos.y;
            targetViewportPos.z *= -1;
        }

        // Determine if the target is on screen
        bool isOnScreen = IsTargetVisible();

        if (!isOnScreen)
        {
            // Calculate the screen boundaries accounting for padding
            Vector2 canvasSize = canvasRectTransform.rect.size;
            float minX = screenEdgePadding;
            float maxX = canvasSize.x - screenEdgePadding;
            float minY = screenEdgePadding;
            float maxY = canvasSize.y - screenEdgePadding;

            // Convert viewport position to canvas position
            Vector2 canvasPosition = new Vector2(
                targetViewportPos.x * canvasSize.x,
                targetViewportPos.y * canvasSize.y
            );

            // Calculate direction from screen center to target
            Vector2 direction = canvasPosition - (canvasSize * 0.5f);

            // Find the intersection point with the screen edge
            Vector2 screenEdgePosition = FindScreenEdgePoint(canvasSize * 0.5f, direction, minX, maxX, minY, maxY);

            // Set position (anchored position in the Canvas)
            rectTransform.anchoredPosition = screenEdgePosition;

            // Rotate the indicator to point towards the target
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rectTransform.rotation = Quaternion.Euler(0, 0, angle);
        }
        else
        {
            // Convert viewport position to canvas position
            Vector2 canvasSize = canvasRectTransform.rect.size;
            rectTransform.anchoredPosition = new Vector2(
                (targetViewportPos.x * canvasSize.x) - (canvasSize.x * 0.5f),
                (targetViewportPos.y * canvasSize.y) - (canvasSize.y * 0.5f)
            );

            // Reset rotation when visible
            rectTransform.rotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Finds the point on the screen edge in the direction of the target
    /// </summary>
    private Vector2 FindScreenEdgePoint(Vector2 center, Vector2 direction, float minX, float maxX, float minY, float maxY)
    {
        Vector2 screenEdgePosition = center;

        // Normalize direction
        direction.Normalize();

        // Find screen edge intersection by calculating the scaling factor
        float scaleX, scaleY;

        if (Mathf.Approximately(direction.x, 0))
        {
            // Vertical direction
            scaleY = direction.y > 0 ? (maxY - center.y) / direction.y : (minY - center.y) / direction.y;
            screenEdgePosition = center + direction * scaleY;
        }
        else if (Mathf.Approximately(direction.y, 0))
        {
            // Horizontal direction
            scaleX = direction.x > 0 ? (maxX - center.x) / direction.x : (minX - center.x) / direction.x;
            screenEdgePosition = center + direction * scaleX;
        }
        else
        {
            // Diagonal direction
            scaleX = direction.x > 0 ? (maxX - center.x) / direction.x : (minX - center.x) / direction.x;
            scaleY = direction.y > 0 ? (maxY - center.y) / direction.y : (minY - center.y) / direction.y;

            // Use the smaller scale to find the edge intersection
            float scale = Mathf.Min(scaleX, scaleY);
            screenEdgePosition = center + direction * scale;
        }

        // Adjust for canvas anchoring
        screenEdgePosition -= center;

        return screenEdgePosition;
    }

    /// <summary>
    /// Updates the indicator color based on distance to the target
    /// </summary>
    private void UpdateIndicatorColor()
    {
        // Calculate distance to target
        float distance = Vector3.Distance(mainCamera.transform.position, targetTransform.position);

        // Lerp between farColor and closeColor based on distance
        float t = Mathf.Clamp01(1 - (distance / colorTransitionDistance));
        image.color = Color.Lerp(farColor, closeColor, t);
    }

    /// <summary>
    /// Sets a new target object to track
    /// </summary>
    public void SetTarget(GameObject newTarget)
    {
        targetObject = newTarget;

        if (targetObject != null)
        {
            targetRenderer = targetObject.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                targetTransform = targetRenderer.transform;
            }
            else
            {
                Debug.LogError("New target object must have a Renderer component!");
            }
        }
    }
}