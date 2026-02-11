using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public sealed class SquirrelVesselHUDView : VesselHUDView
    {
        [Header("Boost")]
        [SerializeField] private Image boostFill;
        [SerializeField] private Color boostNormalColor = Color.white;
        [SerializeField] private Color boostFullColor = Color.yellow;

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

        public override void Initialize()
        {
            if (!boostFill) return;
            boostFill.fillAmount = 0f;
            boostFill.color = boostNormalColor;
            boostFill.enabled = false;

            if (driftButtonIcon)
            {
                driftButtonIcon.sprite = normalSprite;
            }

            if (shieldIcon)
                shieldIcon.color = shieldNormalColor;
        }

        public void SetBoostState(float boost01, bool isBoosted, bool isFull)
        {
            if (!boostFill) return;

            boostFill.enabled = isBoosted;
            boostFill.fillAmount = isBoosted ? Mathf.Clamp01(boost01) : 0f;
            boostFill.color = isFull ? boostFullColor : boostNormalColor;
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