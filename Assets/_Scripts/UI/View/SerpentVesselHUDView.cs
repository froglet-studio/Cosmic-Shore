using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
{
    public class SerpentVesselHUDView : VesselHUDView
    {
        [Header("SEED WALL")]
        [SerializeField] private Sprite[] shieldIconsByCount;
        [SerializeField] private Image   shieldIcon;

        [Header("Fuel pellet pips (TIME card)")]
        [Tooltip("The four lit pellets drawn over the TIME icon's slots, top to bottom. Pip n " +
                 "is full once the tank holds n pellets; the refilling pellet shows its partial fill.")]
        [SerializeField] private Image boostPip1;
        [SerializeField] private Image boostPip2;
        [SerializeField] private Image boostPip3;
        [SerializeField] private Image boostPip4;

        [Header("Pip Colors")]
        [SerializeField] private Color pipFullColor      = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color pipEmptyColor     = new Color(1f, 1f, 1f, 0.25f);

        [Tooltip("How fast the pips ease toward the tank's real level, in pellets per second. " +
                 "Fuel refills in one-second steps; this turns each step into a visible pour. " +
                 "A burn drops the pips at once - spending is never eased.")]
        [SerializeField, Min(0.01f)] private float pipFillRate = 1.5f;

        Image[] _boostPips;
        float _pelletsShown = -1f;
        float _pelletsTarget;

        public override void Initialize()
        {
            BuildBoostPipCache();
            InitializeShieldHUD();
            InitializeBoostPips();
        }

        void BuildBoostPipCache()
        {
            if (_boostPips != null) return;

            _boostPips = new[] { boostPip1, boostPip2, boostPip3, boostPip4 };
        }

        // ---------- Public API for controller ----------

        public void InitializeHUD()
        {
            InitializeShieldHUD();
            InitializeBoostPips();
        }

        void InitializeShieldHUD()
        {
            if (!shieldIcon) return;

            shieldIcon.enabled = true;
            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);
        }

        public void SetShieldCount(int shields)
        {
            if (!shieldIcon || shieldIconsByCount == null || shieldIconsByCount.Length < 5)
                return;

            shields = Mathf.Clamp(shields, 0, 4);
            var sprite = shieldIconsByCount[shields];
            if (!sprite) return;

            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);

            shieldIcon.enabled = true;
            shieldIcon.sprite  = sprite;
        }

        void InitializeBoostPips()
        {
            BuildBoostPipCache();
            _pelletsShown = -1f;
            foreach (var pip in _boostPips)
            {
                if (!pip) continue;
                pip.gameObject.SetActive(true);
                pip.enabled = true;
                pip.type = Image.Type.Filled;
            }
            PaintPips(_pelletsTarget);
        }

        /// <summary>
        /// The tank's level in pellets - 2.6 is two ready and a third 60% refilled. A drop (a burn)
        /// lands at once; a rise is poured in over <see cref="pipFillRate"/> so the one-second
        /// refill ticks read as fuel flowing rather than as a counter jumping.
        /// </summary>
        public void SetPelletFuel(float pellets)
        {
            BuildBoostPipCache();
            _pelletsTarget = Mathf.Max(0f, pellets);

            // Spending snaps; an inactive HUD (every hull but the local pilot's) never ticks
            // Update, so it settles too rather than holding a stale level.
            if (_pelletsShown < 0f || _pelletsTarget < _pelletsShown || !isActiveAndEnabled)
                PaintPips(_pelletsTarget);
        }

        void Update()
        {
            if (_pelletsShown < 0f || Mathf.Approximately(_pelletsShown, _pelletsTarget)) return;
            PaintPips(Mathf.MoveTowards(_pelletsShown, _pelletsTarget, pipFillRate * Time.deltaTime));
        }

        void PaintPips(float pellets)
        {
            _pelletsShown = pellets;
            if (_boostPips == null) return;

            for (int i = 0; i < _boostPips.Length; i++)
            {
                var pip = _boostPips[i];
                if (!pip) continue;

                float fill = Mathf.Clamp01(pellets - i);
                pip.fillAmount = fill;
                pip.color = fill >= 1f ? pipFullColor
                          : fill > 0f  ? Color.Lerp(pipEmptyColor, pipFullColor, 0.5f)   // refilling: lit, not yet burnable
                          : pipEmptyColor;
            }
        }
    }
}
