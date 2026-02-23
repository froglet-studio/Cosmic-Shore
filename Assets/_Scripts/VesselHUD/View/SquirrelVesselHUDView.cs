using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private float colorLerpSpeed = 4f;
        [SerializeField] private float crystalFlashDuration = 0.35f;
        [SerializeField, Range(0f, 1f)] private float fullBoostWhiteMix = 0.3f;

        [Header("Drift")]
        [SerializeField] private Image driftButtonIcon;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite driftingSprite;
        [SerializeField] private Sprite doubleDriftingSprite;

        [Header("Danger")]
        [SerializeField] private Image dangerRingIcon;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color dangerColor = Color.red;

        [Header("Shield")]
        [SerializeField] private Image shieldIcon;
        [SerializeField] private Color shieldNormalColor = Color.white;
        [SerializeField] private Color shieldActiveColor = Color.green;

        private Color _playerDomainColor = Color.white;
        private Color _currentBoostColor = Color.white;
        private Color _targetBoostColor = Color.white;
        private float _flashTimer;

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = _playerDomainColor;
            boostFill.enabled = false;

            if (driftButtonIcon)
                driftButtonIcon.sprite = normalSprite;

            if (shieldIcon)
                shieldIcon.color = shieldNormalColor;
        }

        public void SetPlayerDomainColor(Color color)
        {
            _playerDomainColor = color;
            _currentBoostColor = color;
            _targetBoostColor = color;

            if (boostFill)
                boostFill.color = color;
        }

        public void SetBoostState(float boost01, bool isBoosted, bool isFull,
            Color sourceColor, bool hasSourceDomain)
        {
            if (!boostFill) return;

            boostFill.enabled = isBoosted;
            boostFill.fillAmount = isBoosted ? Mathf.Clamp01(boost01) : 0f;

            if (!isBoosted)
            {
                _targetBoostColor = _playerDomainColor;
                return;
            }

            if (hasSourceDomain)
            {
                // Blend between player domain color and the stolen block's domain color
                // Higher boost = more of the source color, giving a gradient feel
                _targetBoostColor = Color.Lerp(_playerDomainColor, sourceColor, boost01);
            }
            else
            {
                // Decay: ease back toward the player's own domain color
                _targetBoostColor = _playerDomainColor;
            }

            if (isFull)
            {
                // Full boost gets a vivid white mix for punch
                _targetBoostColor = Color.Lerp(_targetBoostColor, Color.white, fullBoostWhiteMix);
            }
        }

        public void FlashCrystalSurge()
        {
            _flashTimer = crystalFlashDuration;
        }

        private void Update()
        {
            if (!boostFill || !boostFill.enabled) return;

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                float flashT = Mathf.Clamp01(_flashTimer / crystalFlashDuration);
                // Bright flash that decays: white → target color
                _currentBoostColor = Color.Lerp(_targetBoostColor, Color.white, flashT * 0.6f);
            }
            else
            {
                _currentBoostColor = Color.Lerp(
                    _currentBoostColor, _targetBoostColor,
                    colorLerpSpeed * Time.deltaTime);
            }

            boostFill.color = _currentBoostColor;
        }

        public void UpdateDriftIcon(bool isDrifting, bool isDoubleDrifting)
        {
            if (!driftButtonIcon) return;

            if (isDrifting && isDoubleDrifting)
                driftButtonIcon.sprite = doubleDriftingSprite;
            else if(isDrifting)
                driftButtonIcon.sprite = driftingSprite;
            else
                driftButtonIcon.sprite = normalSprite;
        }

        public void UpdateDangerIcon(bool inDanger)
        {
            if (!dangerRingIcon) return;

            dangerRingIcon.color = inDanger ? dangerColor : normalColor;
        }

        public void UpdateShieldColor(bool active)
        {
            if (!shieldIcon) return;

            shieldIcon.color = active ? shieldActiveColor : shieldNormalColor;
        }
    }
}
