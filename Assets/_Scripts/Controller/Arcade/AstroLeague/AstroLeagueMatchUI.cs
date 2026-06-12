using System.Collections;
using CosmicShore.Data;
using Obvious.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Self-contained match overlay for Astro League, built at runtime so no
    /// prefab surgery is required: big center-top score, announcer banner
    /// (GOAL! / kickoff count-in / OVERTIME / winner), and an edge-clamped
    /// arrow pointing at the ball when it is off screen.
    /// Listens to the match controller's announcer events plus the shared
    /// score ScriptableEvent channel.
    /// </summary>
    public class AstroLeagueMatchUI : MonoBehaviour
    {
        [SerializeField] AstroLeagueMatchController matchController;
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] ScriptableEventString onScoreUpdated;

        [Header("Style")]
        [SerializeField] Color jadeColor = new(0.15f, 1f, 0.55f, 1f);
        [SerializeField] Color rubyColor = new(1f, 0.22f, 0.35f, 1f);
        [SerializeField] float bannerSeconds = 1.6f;
        [SerializeField] float ballArrowHideDistance = 60f;

        Canvas canvas;
        TMP_Text scoreText;
        TMP_Text bannerText;
        RectTransform ballArrow;
        Image ballArrowImage;
        Coroutine bannerRoutine;
        Camera mainCam;

        void Awake()
        {
            BuildCanvas();
        }

        void OnEnable()
        {
            if (onScoreUpdated != null) onScoreUpdated.OnRaised += HandleScore;
            if (matchController != null)
            {
                matchController.OnKickoffCount += HandleKickoffCount;
                matchController.OnKickoffGo += HandleKickoffGo;
                matchController.OnGoalAnnounced += HandleGoal;
                matchController.OnOvertimeAnnounced += HandleOvertime;
                matchController.OnMatchFinished += HandleMatchFinished;
            }
        }

        void OnDisable()
        {
            if (onScoreUpdated != null) onScoreUpdated.OnRaised -= HandleScore;
            if (matchController != null)
            {
                matchController.OnKickoffCount -= HandleKickoffCount;
                matchController.OnKickoffGo -= HandleKickoffGo;
                matchController.OnGoalAnnounced -= HandleGoal;
                matchController.OnOvertimeAnnounced -= HandleOvertime;
                matchController.OnMatchFinished -= HandleMatchFinished;
            }
        }

        void BuildCanvas()
        {
            var canvasGO = new GameObject("AstroLeagueMatchCanvas");
            canvasGO.transform.SetParent(transform, false);
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50; // Above the shared MiniGameHUD
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            scoreText = CreateText("Score", new Vector2(0.5f, 1f), new Vector2(0f, -70f),
                new Vector2(600f, 90f), 64f, FontStyles.Bold);
            scoreText.text = "0 - 0";

            bannerText = CreateText("Banner", new Vector2(0.5f, 0.62f), Vector2.zero,
                new Vector2(1400f, 160f), 110f, FontStyles.Bold | FontStyles.Italic);
            bannerText.gameObject.SetActive(false);

            var arrowGO = new GameObject("BallArrow");
            arrowGO.transform.SetParent(canvas.transform, false);
            ballArrow = arrowGO.AddComponent<RectTransform>();
            ballArrow.sizeDelta = new Vector2(46f, 46f);
            ballArrowImage = arrowGO.AddComponent<Image>();
            ballArrowImage.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            ballArrow.gameObject.SetActive(false);
        }

        TMP_Text CreateText(string name, Vector2 anchor, Vector2 anchoredPos,
            Vector2 size, float fontSize, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            return text;
        }

        // ── Event handlers ───────────────────────────────────────────────────

        void HandleScore(string score)
        {
            if (scoreText != null) scoreText.text = score;
        }

        void HandleKickoffCount(int count) => FlashBanner(count.ToString(), Color.white, 0.9f);

        void HandleKickoffGo() => FlashBanner("GO!", new Color(1f, 0.85f, 0.2f, 1f), 0.7f);

        void HandleGoal(Domains scoringDomain, int jade, int ruby)
        {
            var color = scoringDomain == Domains.Jade ? jadeColor : rubyColor;
            FlashBanner($"{DomainLabel(scoringDomain)} GOAL!", color, bannerSeconds);
        }

        void HandleOvertime() =>
            FlashBanner("OVERTIME — GOLDEN GOAL", new Color(1f, 0.85f, 0.2f, 1f), bannerSeconds * 1.4f);

        void HandleMatchFinished(Domains winner, int jade, int ruby)
        {
            var color = winner == Domains.Jade ? jadeColor : rubyColor;
            FlashBanner($"{DomainLabel(winner)} WINS {Mathf.Max(jade, ruby)} - {Mathf.Min(jade, ruby)}",
                color, 4f);
        }

        static string DomainLabel(Domains domain) => domain == Domains.Jade ? "JADE" : "RUBY";

        void FlashBanner(string message, Color color, float seconds)
        {
            if (bannerText == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(message, color, seconds));
        }

        IEnumerator BannerRoutine(string message, Color color, float seconds)
        {
            bannerText.text = message;
            bannerText.color = color;
            bannerText.gameObject.SetActive(true);

            // Punch-in scale, hold, fade — unscaled so slow-mo doesn't stall it.
            var rect = bannerText.rectTransform;
            float t = 0f;
            const float punchSeconds = 0.18f;
            while (t < punchSeconds)
            {
                t += Time.unscaledDeltaTime;
                rect.localScale = Vector3.one * Mathf.SmoothStep(1.6f, 1f, t / punchSeconds);
                yield return null;
            }
            rect.localScale = Vector3.one;

            yield return new WaitForSecondsRealtime(seconds);

            const float fadeSeconds = 0.25f;
            t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                bannerText.alpha = 1f - t / fadeSeconds;
                yield return null;
            }
            bannerText.alpha = 1f;
            bannerText.gameObject.SetActive(false);
            bannerRoutine = null;
        }

        // ── Ball arrow ───────────────────────────────────────────────────────

        void LateUpdate()
        {
            if (ball == null || ballArrow == null) return;
            if (mainCam == null)
            {
                mainCam = Camera.main;
                if (mainCam == null) return;
            }

            Vector3 viewportPos = mainCam.WorldToViewportPoint(ball.transform.position);
            bool onScreen = viewportPos.z > 0f &&
                            viewportPos.x is > 0f and < 1f &&
                            viewportPos.y is > 0f and < 1f;

            float distance = Vector3.Distance(mainCam.transform.position, ball.transform.position);
            if (onScreen && distance < ballArrowHideDistance)
            {
                ballArrow.gameObject.SetActive(false);
                return;
            }

            ballArrow.gameObject.SetActive(true);

            if (viewportPos.z < 0f)
            {
                viewportPos.x = 1f - viewportPos.x;
                viewportPos.y = 1f - viewportPos.y;
            }

            Vector2 fromCenter = new Vector2(viewportPos.x - 0.5f, viewportPos.y - 0.5f);
            float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
            ballArrow.localRotation = Quaternion.Euler(0f, 0f, angle);

            var canvasRect = (RectTransform)canvas.transform;
            Vector2 halfSize = canvasRect.sizeDelta * 0.5f;
            const float edgePadding = 60f;
            Vector2 clamped = new(
                Mathf.Clamp((viewportPos.x - 0.5f) * canvasRect.sizeDelta.x, -halfSize.x + edgePadding, halfSize.x - edgePadding),
                Mathf.Clamp((viewportPos.y - 0.5f) * canvasRect.sizeDelta.y, -halfSize.y + edgePadding, halfSize.y - edgePadding));
            ballArrow.anchoredPosition = clamped;
        }
    }
}
