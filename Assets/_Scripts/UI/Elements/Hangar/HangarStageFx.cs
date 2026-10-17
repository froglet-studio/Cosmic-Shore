using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar stage's effects, drawn in UI over the 3D vessel (Docs/MENU_REDESIGN_B.md section 41):
    /// ability bolts with a muzzle flash in the element's colour (the owner: "the sparrow doesn't shoot"),
    /// and the UPGRADE moment - crystals flying from the button to the hull, a ring bursting, and a
    /// "LEVEL N" banner.
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
        float _fireT = -1f, _ringT = -1f, _flyT = -1f, _bannerT = -1f;
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
            if (_fireT < 0f && _ringT < 0f && _flyT < 0f && _bannerT < 0f) return;
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
    }
}
