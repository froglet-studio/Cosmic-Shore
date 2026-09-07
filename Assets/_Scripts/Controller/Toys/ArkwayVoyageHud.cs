using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Arkway voyage's one screen-space surface: a single line of text near the top of the
    /// screen with two channels — a persistent COUNTDOWN ("RETURN TO THE ARK — 3", held while
    /// the leash is breached) and a timed BANNER ("STAY WITH THE ARK", "THE ARK HAS FALLEN").
    /// Built programmatically like <see cref="EnvironmentLoadVeil"/>'s overlay, because a leash
    /// telegraph has to reach a player who is by definition FAR from every world-space label the
    /// toy owns. It never blocks input and it fades rather than popping — continuity of
    /// existence applies to UI too.
    /// </summary>
    public sealed class ArkwayVoyageHud : MonoBehaviour
    {
        const float FadeSeconds = 0.25f;

        CanvasGroup _group;
        TMP_Text _text;
        bool _countdownShown;
        float _bannerUntil = -1f;
        float _targetAlpha;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000; // above gameplay HUD, below the load veil (30000)
            // No GraphicRaycaster: this surface is a readout and must never eat a tap.

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var textGo = new GameObject("Line", typeof(RectTransform));
            textGo.transform.SetParent(transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -90f);
            rect.sizeDelta = new Vector2(900f, 80f);

            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = 34f;
            _text.color = new Color(1f, 0.92f, 0.6f);
            _text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset) _text.font = TMP_Settings.defaultFontAsset;
        }

        /// <summary>Hold a countdown line on screen until <see cref="HideCountdown"/>.</summary>
        public void ShowCountdown(string line)
        {
            _countdownShown = true;
            _text.text = line;
            _targetAlpha = 1f;
        }

        public void HideCountdown()
        {
            if (!_countdownShown) return;
            _countdownShown = false;
            if (Time.unscaledTime >= _bannerUntil) _targetAlpha = 0f;
        }

        /// <summary>Show a line for <paramref name="seconds"/>, then fade it away. A live
        /// countdown outranks it (the leash is the line the player must not miss).</summary>
        public void ShowBanner(string line, float seconds)
        {
            _bannerUntil = Time.unscaledTime + Mathf.Max(0.5f, seconds);
            if (!_countdownShown) _text.text = line;
            _targetAlpha = 1f;
        }

        void Update()
        {
            if (!_countdownShown && _bannerUntil >= 0f && Time.unscaledTime >= _bannerUntil)
            {
                _bannerUntil = -1f;
                _targetAlpha = 0f;
            }

            float step = Time.unscaledDeltaTime / FadeSeconds;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, step);
        }
    }
}
