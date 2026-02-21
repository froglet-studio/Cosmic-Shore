using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// UI indicator that points toward the ball when it's off-screen.
    /// Attach to a UI arrow image on the player's HUD canvas.
    /// </summary>
    public class AstroLeagueBallIndicator : MonoBehaviour
    {
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] RectTransform arrowIndicator;
        [SerializeField] float edgeBuffer = 50f;
        [SerializeField] float hideDistance = 30f;

        Camera mainCam;

        void Start()
        {
            mainCam = Camera.main;
        }

        void LateUpdate()
        {
            if (ball == null || arrowIndicator == null || mainCam == null) return;

            Vector3 viewportPos = mainCam.WorldToViewportPoint(ball.transform.position);

            bool isOnScreen = viewportPos.z > 0 &&
                              viewportPos.x > 0 && viewportPos.x < 1 &&
                              viewportPos.y > 0 && viewportPos.y < 1;

            float distToBall = Vector3.Distance(mainCam.transform.position, ball.transform.position);

            if (isOnScreen && distToBall < hideDistance)
            {
                arrowIndicator.gameObject.SetActive(false);
                return;
            }

            arrowIndicator.gameObject.SetActive(true);

            // If behind camera, flip the direction
            if (viewportPos.z < 0)
            {
                viewportPos.x = 1f - viewportPos.x;
                viewportPos.y = 1f - viewportPos.y;
            }

            // Clamp to screen edges
            Vector2 screenCenter = new Vector2(0.5f, 0.5f);
            Vector2 dir = ((Vector2)viewportPos - screenCenter).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            arrowIndicator.localRotation = Quaternion.Euler(0, 0, angle);

            // Position at screen edge
            Vector2 screenPos = new Vector2(
                Mathf.Clamp(viewportPos.x, edgeBuffer / Screen.width, 1f - edgeBuffer / Screen.width),
                Mathf.Clamp(viewportPos.y, edgeBuffer / Screen.height, 1f - edgeBuffer / Screen.height));

            arrowIndicator.anchorMin = screenPos;
            arrowIndicator.anchorMax = screenPos;
            arrowIndicator.anchoredPosition = Vector2.zero;
        }
    }
}
