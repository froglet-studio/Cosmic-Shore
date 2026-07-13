using System;
using System.Collections.Generic;
using CosmicShore.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public abstract class VesselHUDView : MonoBehaviour
    {
        [Serializable]
        public struct HighlightBinding
        {
            public InputEvents input;
            public Image image;
        }

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();

        /// <summary>
        /// Four-icon contract: return the Image that ALREADY presents ability slot
        /// <paramref name="slotIndex"/> (matching the vessel's <c>VesselAbilitySetSO</c> slot
        /// order), or null when this view has no icon for that slot. Bound slots are presented and
        /// animated entirely by the view/controller's own logic — the <c>VesselAbilityBar</c>
        /// renders its obvious placeholder ONLY for unbound slots, so adopting the contract is a
        /// pure refactor for a view that already shows four icons (the player sees no change).
        /// </summary>
        public virtual Image GetAbilitySlotImage(int slotIndex) => null;

        /// <summary>Finds the highlight overlay Image bound to <paramref name="input"/>, if any.</summary>
        protected Image GetHighlightImage(InputEvents input)
        {
            for (var i = 0; i < highlights.Count; i++)
                if (highlights[i].input == input)
                    return highlights[i].image;
            return null;
        }

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Tween _fadeTween;

        public abstract void Initialize();

        internal GameObject TrailBlockPrefab;

        public void Show()
        {
            gameObject.SetActive(true);

            EnsureCanvasGroup();
            _fadeTween?.Kill();

            float duration = animSettings ? animSettings.vesselHudFadeDuration : 0.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _canvasGroup.alpha = 0f;
            _fadeTween = _canvasGroup.DOFade(1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }

        public void Hide()
        {
            EnsureCanvasGroup();
            _fadeTween?.Kill();

            float duration = animSettings ? animSettings.vesselHudFadeDuration : 0.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _fadeTween = _canvasGroup.DOFade(0f, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(unscaled)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup) return;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        protected virtual void OnDestroy()
        {
            _fadeTween?.Kill();
        }
    }
}
