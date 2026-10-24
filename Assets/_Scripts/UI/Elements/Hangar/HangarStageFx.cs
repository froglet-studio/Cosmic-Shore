using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar stage's effects, drawn in UI over the 3D vessel (Docs/MENU_REDESIGN_B.md section 41):
    /// each ability's own effect in the element's colour - bolts for a shot, exhaust for a boost, blocks laid
    /// for a build, radar pings for a sense and so on (<see cref="Play"/>, one per <see cref="HangarAbilityKind"/>,
    /// section 46) - and the UPGRADE moment: crystals flying from the button to the hull, a ring bursting,
    /// and a "LEVEL N" banner.
    ///
    /// <para>Only works while something plays; otherwise one bool check a frame. It lives under its own
    /// nested Canvas, so its per-frame moves re-batch only itself.</para>
    /// </summary>
    public class HangarStageFx : MonoBehaviour
    {
        [Header("Fire")]
        [SerializeField] private Image[] bolts = new Image[6];
        [SerializeField] private Image flash;
        [SerializeField, Min(0.05f)] private float boltSeconds = 0.35f;
        [SerializeField, Min(0f)] private float boltStagger = 0.11f;
        [SerializeField, Tooltip("How far a bolt travels, as a fraction of the stage's width.")]
        private float boltTravel = 0.55f;

        [Header("Ability kinds (section 46)")]
        [SerializeField, Min(0.1f), Tooltip("Seconds each kind's effect lasts (Burst, Boost, Phase, Build, Stance, Sense, Pull, Strike, Passive).")]
        private float kindSeconds = 1f;

        [Header("Upgrade")]
        [SerializeField] private Image ring;
        [SerializeField] private Image[] crystals = new Image[6];
        [SerializeField] private CanvasGroup banner;
        [SerializeField] private TMP_Text bannerEyebrow;
        [SerializeField] private TMP_Text bannerTitle;
        [SerializeField] private TMP_Text bannerLine;
        [SerializeField, Min(0.1f)] private float flySeconds = 0.55f;
        [SerializeField, Min(0.1f)] private float ringSeconds = 0.6f;
        [SerializeField, Min(0.1f)] private float bannerSeconds = 1.6f;

        RectTransform _rt;
        float _fireT = -1f, _ringT = -1f, _flyT = -1f, _bannerT = -1f, _kindT = -1f;
        HangarAbilityKind _kind;
        Color _kindColour;
        Vector2 _kindFrom, _kindDir;
        Color _fireColour, _ringColour;
        Vector2 _from, _dir;
        readonly Vector2[] _flyFrom = new Vector2[6];

        /// <summary>Seconds the crystals take to reach the hull.</summary>
        public float FlySeconds => flySeconds;

        void Awake()
        {
            _rt = (RectTransform)transform;
            HideAll();
        }

        void HideAll()
        {
            foreach (var b in bolts) if (b) b.enabled = false;
            foreach (var c in crystals) if (c) c.enabled = false;
            if (flash) flash.enabled = false;
            if (ring) ring.enabled = false;
            KitVisibility.SetShown(banner, false);
        }

        /// <summary>Bolts and a muzzle flash from <paramref name="fromViewport"/> along <paramref name="dirViewport"/>
        /// (0..1 across the stage).</summary>
        public void Fire(Color colour, Vector2 fromViewport, Vector2 dirViewport)
        {
            _fireColour = colour;
            _from = fromViewport;
            _dir = dirViewport.sqrMagnitude > 1e-6f ? dirViewport.normalized : Vector2.right;
            _fireT = 0f;
        }

        /// <summary>
        /// The effect of an ability of <paramref name="kind"/>, from the nose at <paramref name="fromViewport"/>
        /// along <paramref name="dirViewport"/> (0..1 across the stage). A shot fires bolts; every other kind
        /// plays its own program on the same images.
        /// </summary>
        public void Play(HangarAbilityKind kind, Color colour, Vector2 fromViewport, Vector2 dirViewport)
        {
            HideAll();
            _fireT = _ringT = _flyT = _kindT = -1f;
            if (kind == HangarAbilityKind.Shot)
            {
                Fire(colour, fromViewport, dirViewport);
                return;
            }
            _kind = kind;
            _kindColour = colour;
            _kindFrom = fromViewport;
            _kindDir = dirViewport.sqrMagnitude > 1e-6f ? dirViewport.normalized : Vector2.right;
            _kindT = 0f;
        }

        /// <summary>The upgrade ring bursting from the hull.</summary>
        public void Ring(Color colour)
        {
            _ringColour = colour;
            _ringT = 0f;
        }

        /// <summary>Crystals fly from <paramref name="fromWorld"/> (the UPGRADE button) to the hull.</summary>
        public void FlyCrystals(Vector3 fromWorld)
        {
            if (!_rt) _rt = (RectTransform)transform;
            var local = (Vector2)_rt.InverseTransformPoint(fromWorld);
            for (int i = 0; i < _flyFrom.Length; i++)
                _flyFrom[i] = local + new Vector2((i % 3 - 1) * 14f, (i / 3) * 12f);
            _flyT = 0f;
        }

        /// <summary>"LEVEL N": the eyebrow, title and line, held for a beat.</summary>
        public void ShowBanner(string eyebrow, string title, string line, Color colour)
        {
            if (bannerEyebrow) { bannerEyebrow.text = eyebrow; bannerEyebrow.color = colour; }
            if (bannerTitle) bannerTitle.text = title;
            if (bannerLine) bannerLine.text = line;
            _bannerT = 0f;
        }

        void Update()
        {
            if (_fireT < 0f && _ringT < 0f && _flyT < 0f && _bannerT < 0f && _kindT < 0f) return;
            if (!_rt) _rt = (RectTransform)transform;
            float dt = Time.unscaledDeltaTime;
            var size = _rt.rect.size;
            Vector2 ToLocal(Vector2 vp) => new((vp.x - _rt.pivot.x) * size.x, (vp.y - _rt.pivot.y) * size.y);
            var centre = ToLocal(new Vector2(0.5f, 0.48f));

            if (_fireT >= 0f)
            {
                _fireT += dt;
                var start = ToLocal(_from);
                var dirLocal = new Vector2(_dir.x * size.x, _dir.y * size.y).normalized;
                float angle = Mathf.Atan2(dirLocal.y, dirLocal.x) * Mathf.Rad2Deg;
                bool any = false;
                for (int i = 0; i < bolts.Length; i++)
                {
                    var b = bolts[i];
                    if (!b) continue;
                    float k = (_fireT - i * boltStagger) / boltSeconds;
                    bool on = k >= 0f && k <= 1f;
                    b.enabled = on;
                    if (!on) { any |= k < 0f; continue; }
                    any = true;
                    var r = b.rectTransform;
                    r.anchoredPosition = start + dirLocal * (boltTravel * size.x * k) + new Vector2(-dirLocal.y, dirLocal.x) * ((i % 2) * 2f - 1f) * 6f;
                    r.localRotation = Quaternion.Euler(0f, 0f, angle);
                    r.localScale = new Vector3(0.4f + 0.8f * k, 1f, 1f);
                    b.color = new Color(_fireColour.r, _fireColour.g, _fireColour.b, 1f - k * k);
                }
                if (flash)
                {
                    float f = Mathf.Repeat(_fireT, boltStagger * 2f) / (boltStagger * 2f);
                    flash.enabled = _fireT < bolts.Length * boltStagger;
                    flash.rectTransform.anchoredPosition = start;
                    flash.rectTransform.localScale = Vector3.one * (0.6f + 0.6f * f);
                    flash.color = new Color(_fireColour.r, _fireColour.g, _fireColour.b, 1f - f);
                }
                if (!any) { _fireT = -1f; if (flash) flash.enabled = false; }
            }

            if (_kindT >= 0f)
            {
                _kindT += dt / kindSeconds;
                var nose = ToLocal(_kindFrom);
                var dir = new Vector2(_kindDir.x * size.x, _kindDir.y * size.y).normalized;
                PlayKind(Mathf.Clamp01(_kindT), centre, nose, dir, size.x);
                if (_kindT >= 1f) { _kindT = -1f; HideAll(); }
            }

            if (_flyT >= 0f)
            {
                _flyT += dt;
                bool any = false;
                for (int i = 0; i < crystals.Length; i++)
                {
                    var c = crystals[i];
                    if (!c) continue;
                    float k = (_flyT - i * 0.05f) / flySeconds;
                    bool on = k >= 0f && k <= 1f;
                    c.enabled = on;
                    if (!on) { any |= k < 0f; continue; }
                    any = true;
                    float e = k * k;                                   // ease in: they accelerate into the hull
                    var p = Vector2.Lerp(_flyFrom[i], centre, e) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 60f;
                    c.rectTransform.anchoredPosition = p;
                    c.rectTransform.localScale = Vector3.one * (1f - 0.5f * e);
                }
                if (!any) _flyT = -1f;
            }

            if (_ringT >= 0f && ring)
            {
                _ringT += dt;
                float k = _ringT / ringSeconds;
                ring.enabled = k <= 1f;
                ring.rectTransform.anchoredPosition = centre;
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, 1f - (1f - k) * (1f - k));
                ring.color = new Color(_ringColour.r, _ringColour.g, _ringColour.b, Mathf.Clamp01(1f - k));
                if (k > 1f) _ringT = -1f;
            }

            if (_bannerT >= 0f && banner)
            {
                _bannerT += dt;
                float k = _bannerT / bannerSeconds;
                float a = k < 0.15f ? k / 0.15f : k > 0.8f ? Mathf.Clamp01((1f - k) / 0.2f) : 1f;
                banner.alpha = a;
                banner.interactable = banner.blocksRaycasts = false;
                if (k > 1f) { _bannerT = -1f; banner.alpha = 0f; }
            }
        }

        // ───────────────────────── one program per ability kind ─────────────────────────
        // k: 0..1 through the effect. centre: the hull; nose and dir: where it points; w: the stage's width.

        void PlayKind(float k, Vector2 centre, Vector2 nose, Vector2 dir, float w)
        {
            var side = new Vector2(-dir.y, dir.x);
            var tail = centre - (nose - centre);
            switch (_kind)
            {
                case HangarAbilityKind.Boost:
                    // Exhaust streaming back from the tail, the flash flickering at the nozzle.
                    for (int i = 0; i < bolts.Length; i++)
                    {
                        float b = Mathf.Repeat(k * 2.2f - i * 0.17f, 1f);
                        Place(bolts[i], k < 0.85f, tail - dir * (b * 0.32f * w) + side * ((i % 3 - 1) * 9f), dir,
                              1.2f + 1.6f * b, 1f - b);
                    }
                    Glow(flash, tail, 0.8f + 0.5f * Mathf.Repeat(k * 9f, 1f), k < 0.85f ? 0.9f : 0f);
                    break;

                case HangarAbilityKind.Burst:
                    // A shock ring and shards flung out all round the hull.
                    Ring(ring, centre, Mathf.Lerp(0.3f, 3.2f, Ease(k)), 1f - k);
                    Glow(flash, centre, 1.4f + 1.2f * k, 1f - k * 1.6f);
                    for (int i = 0; i < bolts.Length; i++)
                    {
                        float a = (i / (float)bolts.Length) * Mathf.PI * 2f;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Place(bolts[i], true, centre + d * (Ease(k) * 0.3f * w), d, 0.6f + k, 1f - k);
                    }
                    break;

                case HangarAbilityKind.Phase:
                    // A dotted path ahead, then a ghost left where the hull was.
                    for (int i = 0; i < bolts.Length; i++)
                    {
                        float at = (i + 1) / (float)(bolts.Length + 1);
                        Place(bolts[i], k > at * 0.4f, nose + dir * (at * 0.45f * w), dir, 0.35f, (1f - k) * 0.9f);
                    }
                    Glow(flash, centre, 2.2f, k < 0.5f ? 0.15f : (1f - k) * 1.2f);
                    Ring(ring, nose + dir * (0.45f * w), 0.5f + k, k < 0.5f ? 0f : 1f - k);
                    break;

                case HangarAbilityKind.Build:
                    // Blocks set down one by one behind the tail, then fading together.
                    for (int i = 0; i < crystals.Length; i++)
                    {
                        float pop = Mathf.Clamp01((k - i * 0.1f) / 0.12f);
                        var c = crystals[i];
                        if (!c) continue;
                        c.enabled = pop > 0f && k < 0.98f;
                        c.rectTransform.anchoredPosition = tail - dir * (24f + i * 30f);
                        c.rectTransform.localScale = Vector3.one * (0.4f + 0.6f * pop);
                        c.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                        c.color = Tint(k > 0.75f ? (1f - k) / 0.25f : 1f);
                    }
                    break;

                case HangarAbilityKind.Stance:
                    // A ring closes onto the hull and locks with a flash.
                    Ring(ring, centre, Mathf.Lerp(2.8f, 0.85f, Ease(Mathf.Clamp01(k / 0.6f))), k < 0.6f ? k / 0.6f : 1f - (k - 0.6f) / 0.4f);
                    Glow(flash, centre, 1.6f, k > 0.55f && k < 0.8f ? 1f - (k - 0.55f) / 0.25f : 0f);
                    break;

                case HangarAbilityKind.Sense:
                    // Three radar pings spreading from the hull.
                    {
                        float ping = Mathf.Repeat(k * 3f, 1f);
                        Ring(ring, centre, Mathf.Lerp(0.4f, 3f, ping), (1f - ping) * (k < 0.98f ? 1f : 0f));
                        Glow(flash, centre, 0.9f, (1f - ping) * 0.6f);
                    }
                    break;

                case HangarAbilityKind.Pull:
                    // Motes spiral in to a point ahead of the nose and wink out there.
                    {
                        var target = nose + dir * (0.12f * w);
                        for (int i = 0; i < crystals.Length; i++)
                        {
                            var c = crystals[i];
                            if (!c) continue;
                            float a = i / (float)crystals.Length * Mathf.PI * 2f + k * 4f;
                            float r = (1f - Ease(k)) * 0.32f * w;
                            c.enabled = k < 0.95f;
                            c.rectTransform.anchoredPosition = target + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                            c.rectTransform.localScale = Vector3.one * (0.9f - 0.5f * k);
                            c.rectTransform.localRotation = Quaternion.identity;
                            c.color = Tint(1f);
                        }
                        Glow(flash, target, 0.6f + k, k * k);
                    }
                    break;

                case HangarAbilityKind.Strike:
                    // A slash arc sweeping across the front of the hull.
                    {
                        float baseA = Mathf.Atan2(dir.y, dir.x);
                        for (int i = 0; i < bolts.Length; i++)
                        {
                            float at = i / (float)(bolts.Length - 1);
                            float a = baseA + Mathf.Lerp(1.2f, -1.2f, at);
                            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            float lit = Mathf.Clamp01(1f - Mathf.Abs(k * 1.4f - at) * 3f);
                            Place(bolts[i], lit > 0f, centre + d * (0.2f * w), new Vector2(-d.y, d.x), 1.1f, lit);
                        }
                    }
                    break;

                default:   // Passive: the hull glows twice
                    Ring(ring, centre, 1f + 0.6f * Mathf.Repeat(k * 2f, 1f), 0.6f * (1f - Mathf.Repeat(k * 2f, 1f)));
                    break;
            }
        }

        static float Ease(float k) => 1f - (1f - k) * (1f - k);

        Color Tint(float alpha) => new(_kindColour.r, _kindColour.g, _kindColour.b, Mathf.Clamp01(alpha));

        void Place(Image img, bool on, Vector2 at, Vector2 along, float stretch, float alpha)
        {
            if (!img) return;
            img.enabled = on && alpha > 0f;
            if (!img.enabled) return;
            var r = img.rectTransform;
            r.anchoredPosition = at;
            r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            r.localScale = new Vector3(stretch, 1f, 1f);
            img.color = Tint(alpha);
        }

        void Glow(Image img, Vector2 at, float scale, float alpha)
        {
            if (!img) return;
            img.enabled = alpha > 0f;
            if (!img.enabled) return;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.localScale = Vector3.one * scale;
            img.color = Tint(alpha);
        }

        void Ring(Image img, Vector2 at, float scale, float alpha)
        {
            if (!img) return;
            img.enabled = alpha > 0f;
            if (!img.enabled) return;
            img.rectTransform.anchoredPosition = at;
            img.rectTransform.localScale = Vector3.one * scale;
            img.color = Tint(alpha);
        }
    }
}
