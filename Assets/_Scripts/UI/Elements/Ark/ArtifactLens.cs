using System;
using System.Collections.Generic;
using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Artifacts tab's lens (Rev 105 B, Docs/MENU_REDESIGN_B.md section 193; Shombith, 2026-11-13: "B is good"): a
    /// round hologram, like the domes the captains lean over in the pilot scenes inside the Ark (Jugger, Scales), in which
    /// the picked artifact's payload plays out over a field of prisms, with the artifact's four tracks as arcs round the
    /// rim. <see cref="ArkArtifactsPage"/> tells it what to show; the lens only draws.
    ///
    /// <para><b>What plays.</b> Every kind's payload is one of four shapes (<see cref="Payload"/>), and the NUMBER in it
    /// (heads that fan out, cracks along a line, shocks in one place, re-raised shells) is the artifact's own count, so
    /// feeding the artifact changes what the lens shows. The heads and bursts are a handful of pooled UI images moved in
    /// <see cref="Update"/> only while the lens is open; the prisms are placed once per artifact (from its seed) and never
    /// move. A live 3D scene in a render texture can replace the field later without touching the page.</para>
    ///
    /// <para><b>Colours</b> come from the authored templates (themed graphics, so the worn colour set paints them); the
    /// lens copies them, it never writes a colour of its own.</para>
    /// </summary>
    public class ArtifactLens : MonoBehaviour
    {
        /// <summary>The payload's shape in the lens.</summary>
        public enum Payload
        {
            /// <summary>Heads fan out across the heading, each bursting where it lands (Hydra).</summary>
            Fan = 0,
            /// <summary>One throw down the heading, cracking along its line (Javelin).</summary>
            Line = 1,
            /// <summary>One throw, then the same place cracks again and again (Aftershock).</summary>
            Repeat = 2,
            /// <summary>A shell raised over the hull, re-raised (Aegis).</summary>
            Shell = 3,
        }

        [Serializable]
        public class Arc
        {
            [Tooltip("The fill while this is the artifact's attuned track (the element it is fed).")]
            public Image attunedFill;
            [Tooltip("The fill while this track is one of the three fixed ones.")]
            public Image fixedFill;
            public KitElementFlower flower;
            public TMP_Text valueText;
            public TMP_Text labelText;
            [Tooltip("The arc's words: full on the attuned track, dimmed on the fixed ones.")]
            public CanvasGroup wordsGroup;
        }

        [SerializeField] private CanvasGroup group;
        [Tooltip("Scaled from small to full as the lens opens.")]
        [SerializeField] private RectTransform body;
        [Tooltip("The hologram's area, inside the rim and masked to it; its centre is the lens's centre.")]
        [SerializeField] private RectTransform field;

        [Header("Templates (hidden; the lens clones them)")]
        [SerializeField] private Image prismTemplate;
        [SerializeField] private Image headTemplate;
        [SerializeField] private Image burstTemplate;

        [Header("The tracks round the rim: Mass, Space, Time, Charge")]
        [SerializeField] private Arc[] arcs = new Arc[4];
        [Tooltip("How much of the circle one arc covers at level 10.")]
        [Range(0.05f, 0.25f)]
        [SerializeField] private float arcSpan = 80f / 360f;
        [Range(0f, 1f)]
        [SerializeField] private float fixedWordsAlpha = 0.6f;

        [Header("Motion")]
        [Min(0)] [SerializeField] private int prismCount = 40;
        [Tooltip("Seconds of one throw, from release to the last burst; it then plays again.")]
        [Min(0.5f)] [SerializeField] private float cycleSeconds = 2.4f;
        [Tooltip("The share of a cycle a head spends flying; the rest is its burst.")]
        [Range(0.2f, 0.9f)] [SerializeField] private float flightShare = 0.55f;
        [Tooltip("Seconds the lens takes to open.")]
        [Min(0f)] [SerializeField] private float openSeconds = 0.6f;
        [Tooltip("The lens's scale when it starts to open.")]
        [Range(0.05f, 1f)] [SerializeField] private float closedScale = 0.18f;
        [Tooltip("A burst's size at its widest, as a share of the field's radius.")]
        [Range(0.02f, 0.5f)] [SerializeField] private float burstSize = 0.16f;
        [Tooltip("The most heads or bursts one throw shows.")]
        [Range(1, 12)] [SerializeField] private int maxCount = 8;

        struct Shot { public Vector2 From, Ctrl, To; public float Delay; }
        struct Burst { public Vector2 At; public float Delay, Size; }

        readonly List<Shot> _shots = new();
        readonly List<Burst> _bursts = new();
        readonly List<Image> _headPool = new();
        readonly List<Image> _burstPool = new();
        readonly List<Image> _prismPool = new();

        bool _open;
        float _openedAt;
        Payload _payload;
        int _count;
        int _seed = int.MinValue;

        public bool IsOpen => _open;

        void Awake()
        {
            if (prismTemplate) prismTemplate.gameObject.SetActive(false);
            if (headTemplate) headTemplate.gameObject.SetActive(false);
            if (burstTemplate) burstTemplate.gameObject.SetActive(false);
            if (group) KitVisibility.SetShown(group, false);
        }

        /// <summary>Opens the lens (or keeps it open) on a payload: its shape, its count (heads, cracks, shocks,
        /// re-raises) and the seed its field of prisms is laid from. <paramref name="replayOpen"/> plays the opening.</summary>
        public void Show(Payload payload, int count, int seed, bool replayOpen)
        {
            count = Mathf.Clamp(count, 1, maxCount);
            if (seed != _seed) LayPrisms(seed);
            if (payload != _payload || count != _count || seed != _seed || !_open) Plan(payload, count);
            _payload = payload;
            _count = count;
            _seed = seed;
            if (!_open || replayOpen) _openedAt = Time.unscaledTime;
            _open = true;
            if (group) KitVisibility.SetShown(group, true);
            Tick();
        }

        public void Hide()
        {
            _open = false;
            if (group) KitVisibility.SetShown(group, false);
            if (body) body.localScale = Vector3.one;
        }

        /// <summary>Draws track <paramref name="index"/> (Mass, Space, Time, Charge): its level on the 0-10 band, its value
        /// and label, and whether it is the attuned track.</summary>
        public void SetArc(int index, Element element, float level, string value, string label, bool attuned)
        {
            if (index < 0 || index >= arcs.Length || arcs[index] == null) return;
            var a = arcs[index];
            float fill = arcSpan * Mathf.Clamp01(level / ArtifactLevels.MaxLevel);
            if (a.attunedFill)
            {
                a.attunedFill.enabled = attuned;
                a.attunedFill.fillAmount = fill;
            }
            if (a.fixedFill)
            {
                a.fixedFill.enabled = !attuned;
                a.fixedFill.fillAmount = fill;
            }
            if (a.flower) a.flower.Set(element, ElementalFlowerLevel);
            if (a.valueText) a.valueText.text = value;
            if (a.labelText) a.labelText.text = label;
            if (a.wordsGroup) a.wordsGroup.alpha = attuned ? 1f : fixedWordsAlpha;
        }

        /// <summary>A flower drawn as a mark, not a level: all five petals lit white (the HUD's level 5).</summary>
        const int ElementalFlowerLevel = 5;

        void Update()
        {
            if (_open) Tick();
        }

        void Tick()
        {
            float since = Time.unscaledTime - _openedAt;
            if (body)
            {
                float k = openSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(since / openSeconds));
                body.localScale = Vector3.one * Mathf.Lerp(closedScale, 1f, k);
                if (group) group.alpha = k;
            }
            if (!field) return;
            float r = Radius();
            float t = Mathf.Repeat(since, cycleSeconds) / cycleSeconds;

            for (int i = 0; i < _shots.Count; i++)
            {
                var head = Pooled(_headPool, headTemplate, i);
                if (!head) break;
                var s = _shots[i];
                float p = (t - s.Delay) / flightShare;
                bool flying = p > 0f && p < 1f;
                head.enabled = flying;
                if (!flying) continue;
                float e = 1f - (1f - p) * (1f - p);       // fast out, slowing as it lands
                head.rectTransform.anchoredPosition = Bezier(s.From, s.Ctrl, s.To, e) * r;
            }
            for (int i = _shots.Count; i < _headPool.Count; i++) _headPool[i].enabled = false;

            for (int i = 0; i < _bursts.Count; i++)
            {
                var img = Pooled(_burstPool, burstTemplate, i);
                if (!img) break;
                var b = _bursts[i];
                float p = (t - b.Delay - flightShare) / (1f - flightShare);
                bool on = p > 0f && p < 1f;
                img.enabled = on;
                if (!on) continue;
                var rt = img.rectTransform;
                rt.anchoredPosition = b.At * r;
                float size = Mathf.Lerp(0.2f, 1f, p) * b.Size * r * 2f;
                rt.sizeDelta = new Vector2(size, size);
                img.canvasRenderer.SetAlpha(1f - p);
            }
            for (int i = _bursts.Count; i < _burstPool.Count; i++) _burstPool[i].enabled = false;
        }

        float Radius() => field ? Mathf.Min(field.rect.width, field.rect.height) * 0.5f : 1f;

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        /// <summary>The throw's shape, in units of the field's radius (centre 0,0; the release point low in the lens).</summary>
        void Plan(Payload payload, int count)
        {
            _shots.Clear();
            _bursts.Clear();
            var origin = new Vector2(0f, -0.75f);
            float stagger = 0.08f;
            switch (payload)
            {
                case Payload.Fan:
                    for (int i = 0; i < count; i++)
                    {
                        float deg = count == 1 ? 90f : Mathf.Lerp(140f, 40f, i / (float)(count - 1));
                        var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
                        var to = origin + dir * 1.05f;
                        _shots.Add(new Shot { From = origin, Ctrl = origin + dir * 0.4f + Vector2.up * 0.1f, To = to, Delay = i * stagger * 0.5f });
                        _bursts.Add(new Burst { At = to, Delay = i * stagger * 0.5f, Size = burstSize });
                    }
                    break;
                case Payload.Line:
                {
                    var to = new Vector2(0f, 0.8f);
                    _shots.Add(new Shot { From = origin, Ctrl = (origin + to) * 0.5f, To = to, Delay = 0f });
                    for (int i = 0; i < count; i++)
                    {
                        float k = (i + 1) / (float)count;
                        _bursts.Add(new Burst { At = Vector2.Lerp(origin, to, k), Delay = (k - 1f) * flightShare * 0.6f, Size = burstSize * (i == count - 1 ? 1.4f : 0.7f) });
                    }
                    break;
                }
                case Payload.Repeat:
                {
                    var to = new Vector2(0f, 0.15f);
                    _shots.Add(new Shot { From = origin, Ctrl = new Vector2(0f, -0.2f), To = to, Delay = 0f });
                    float gap = (1f - flightShare) / (count + 1);
                    for (int i = 0; i < count; i++)
                        _bursts.Add(new Burst { At = to, Delay = i * gap, Size = burstSize * (1f + 0.35f * i) });
                    break;
                }
                default:
                {
                    var hull = new Vector2(0f, -0.35f);
                    float gap = (1f - flightShare) / (count + 1);
                    for (int i = 0; i < count; i++)
                        _bursts.Add(new Burst { At = hull, Delay = i * gap - flightShare * 0.5f, Size = 0.55f + 0.08f * i });
                    break;
                }
            }
        }

        void LayPrisms(int seed)
        {
            if (!field || !prismTemplate) return;
            var rng = new System.Random(seed);
            float r = Radius();
            for (int i = 0; i < prismCount; i++)
            {
                var img = Pooled(_prismPool, prismTemplate, i);
                if (!img) break;
                // Uniform over the disc, kept off the rim.
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = Mathf.Sqrt((float)rng.NextDouble()) * 0.88f;
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d * r;
                rt.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 180f);
                img.enabled = true;
            }
            for (int i = prismCount; i < _prismPool.Count; i++) _prismPool[i].enabled = false;
        }

        Image Pooled(List<Image> pool, Image template, int index)
        {
            if (!template || !field) return null;
            while (pool.Count <= index)
            {
                var img = Instantiate(template, field);
                img.gameObject.SetActive(true);
                img.enabled = false;
                pool.Add(img);
            }
            return pool[index];
        }
    }
}
