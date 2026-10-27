using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The nanobot swap on a squad card (Docs/MENU_REDESIGN_B.md section 93; Shombith, 2026-10-27: "the nanobot transition
    /// ... we can use that to change domains for the squad vessels"). When the squad's domain changes, the hull dissolves into
    /// a swarm of small prism-bots that scatter, swirl and rebuild it in the new domain's colour.
    ///
    /// <para>Runs on UNSCALED time: ScreenSwitcher pauses the game on every screen but Home. The bots are clones of
    /// <see cref="botTemplate"/>, made on the first play and kept; between plays they are switched off and the component does
    /// no per-frame work beyond one early return.</para>
    /// </summary>
    public class SquadNanobotSwap : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The hull art: it dissolves, and takes the domain's tint.")]
        [SerializeField] private Graphic hull;
        [Tooltip("Dissolves with the hull (the pilot standing beside it). Optional.")]
        [SerializeField] private Graphic companion;
        [Tooltip("One bot: a small diamond, inactive. Cloned into the swarm, inside the hull's rect.")]
        [SerializeField] private Image botTemplate;

        [Header("Swarm")]
        [SerializeField, Range(4, 64)] private int botCount = 34;
        [Tooltip("Seconds from the first bot to the hull standing again.")]
        [SerializeField, Min(0.1f)] private float seconds = 1.1f;
        [Tooltip("How far a bot flies out, in canvas units, at most.")]
        [SerializeField, Min(0f)] private float scatter = 130f;
        [Tooltip("Share of bots that stay white-hot instead of taking the domain colour.")]
        [SerializeField, Range(0f, 1f)] private float sparkShare = 0.25f;
        [SerializeField] private Color sparkColor = Color.white;

        [Header("Tint")]
        [Tooltip("How far the hull's colour moves toward the domain's (0 keeps the art's own colours).")]
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.45f;

        readonly List<RectTransform> _bots = new();
        readonly List<Image> _botImages = new();
        Vector2[] _home, _away;
        float[] _offset;
        bool[] _spark;
        float _clock = -1f, _delay;
        bool _tinted;
        Color _domain = Color.white;

        /// <summary>Tints the hull to <paramref name="domain"/> at once (a redraw, not a change).</summary>
        public void SetColor(Color domain)
        {
            _domain = domain;
            if (_clock < 0f) ApplyTint(1f);
        }

        /// <summary>Plays the swap to <paramref name="domain"/>, starting after <paramref name="delay"/> seconds.</summary>
        public void Play(Color domain, float delay)
        {
            _domain = domain;
            _delay = Mathf.Max(0f, delay);
            _clock = 0f;
            _tinted = false;
            EnsureSwarm();
            for (int i = 0; i < _bots.Count; i++)
            {
                _botImages[i].color = _spark[i] ? sparkColor : domain;
                _bots[i].gameObject.SetActive(true);
                SetBot(i, _home[i], 0f, 0f);
            }
        }

        void OnDisable() => Finish();

        void Update()
        {
            if (_clock < 0f) return;
            _clock += Time.unscaledDeltaTime;
            float t = _clock - _delay;
            if (t < 0f) return;
            float u = t / seconds;

            // The hull: gone by 30 %, back from 62 %; it takes the new colour while it is gone.
            float alpha = u < 0.3f ? 1f - u / 0.3f : u < 0.62f ? 0f : Mathf.Clamp01((u - 0.62f) / 0.38f);
            if (!_tinted && u >= 0.45f)
            {
                _tinted = true;
                ApplyTint(alpha);
            }
            SetAlpha(hull, alpha);
            SetAlpha(companion, alpha);

            for (int i = 0; i < _bots.Count; i++)
            {
                float b = Mathf.Clamp01((t - _offset[i]) / seconds);
                Vector2 pos;
                float a;
                if (b < 0.22f) { pos = _home[i]; a = b / 0.22f; }
                else if (b < 0.55f) { pos = Vector2.Lerp(_home[i], _away[i], Ease((b - 0.22f) / 0.33f)); a = 1f; }
                else if (b < 0.85f) { pos = Vector2.Lerp(_away[i], _home[i], Ease((b - 0.55f) / 0.3f)); a = 1f; }
                else { pos = _home[i]; a = 1f - (b - 0.85f) / 0.15f; }
                SetBot(i, pos, a, 45f + 360f * b);
            }

            if (u >= 1f && t >= seconds + MaxOffset()) Finish();
        }

        void Finish()
        {
            if (_clock < 0f) return;
            _clock = -1f;
            foreach (var bot in _bots) if (bot) bot.gameObject.SetActive(false);
            ApplyTint(1f);
            SetAlpha(companion, 1f);
        }

        void ApplyTint(float alpha)
        {
            if (!hull) return;
            var c = Color.Lerp(Color.white, _domain, tintStrength);
            c.a = alpha;
            hull.color = c;
        }

        void EnsureSwarm()
        {
            if (_bots.Count > 0 || !botTemplate) return;
            var area = hull ? hull.rectTransform.rect.size : new Vector2(300f, 100f);
            var rng = new System.Random(7);   // seeded: every swap is the same swarm
            _home = new Vector2[botCount];
            _away = new Vector2[botCount];
            _offset = new float[botCount];
            _spark = new bool[botCount];
            for (int i = 0; i < botCount; i++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f), r = Mathf.Sqrt((float)rng.NextDouble());
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _home[i] = new Vector2(dir.x * r * area.x * 0.44f, dir.y * r * area.y * 0.34f);
                float far = scatter * (0.3f + 0.7f * (float)rng.NextDouble());
                _away[i] = _home[i] + new Vector2(dir.x * far, dir.y * far * 0.8f + 30f + 40f * (float)rng.NextDouble());
                _offset[i] = 0.16f * (float)rng.NextDouble();
                _spark[i] = rng.NextDouble() < sparkShare;
                float size = 3f + 5f * (float)rng.NextDouble();
                var bot = Instantiate(botTemplate, botTemplate.transform.parent);
                bot.raycastTarget = false;
                bot.rectTransform.sizeDelta = new Vector2(size, size);
                _bots.Add(bot.rectTransform);
                _botImages.Add(bot);
            }
        }

        float MaxOffset()
        {
            float m = 0f;
            if (_offset != null) foreach (var o in _offset) m = Mathf.Max(m, o);
            return m;
        }

        void SetBot(int i, Vector2 pos, float alpha, float degrees)
        {
            var rt = _bots[i];
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, degrees);
            var c = _botImages[i].color;
            c.a = alpha;
            _botImages[i].color = c;
        }

        static void SetAlpha(Graphic g, float alpha)
        {
            if (!g) return;
            var c = g.color;
            c.a = alpha;
            g.color = c;
        }

        static float Ease(float x) => x * x * (3f - 2f * x);
    }
}
