using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class BrittleStarHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private Color boostNormalColor = Color.cyan;
        [SerializeField] private Color boostActiveColor = Color.yellow;
        [SerializeField] private float boostFillTweenDuration = 0.15f;

        [Header("Fire Mode")]
        [SerializeField] private Image fireModeIcon;
        [SerializeField] private Sprite[] fireModeIcons;

        private Tween _boostFillTween;

        public override void Initialize()
        {
            if (boostFill)
            {
                boostFill.fillAmount = 0f;
                boostFill.color = boostNormalColor;
            }

            if (fireModeIcon)
                fireModeIcon.enabled = false;
        }

        public void SetBoostState(float fill01, bool isBoosting)
        {
            if (!boostFill) return;

            _boostFillTween?.Kill();
            _boostFillTween = boostFill.DOFillAmount(Mathf.Clamp01(fill01), boostFillTweenDuration)
                .SetEase(Ease.Linear);

            boostFill.color = isBoosting ? boostActiveColor : boostNormalColor;
        }

        public void SetFireMode(int modeIndex)
        {
            if (!fireModeIcon || fireModeIcons == null || fireModeIcons.Length == 0)
                return;

            var idx = Mathf.Clamp(modeIndex, 0, fireModeIcons.Length - 1);
            var sprite = fireModeIcons[idx];
            if (!sprite) return;

            fireModeIcon.sprite = sprite;
            fireModeIcon.enabled = true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _boostFillTween?.Kill();
        }
    }
}
