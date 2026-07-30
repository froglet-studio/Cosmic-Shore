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

        [Serializable]
        public struct AbilityIconBinding
        {
            public Element element;
            public Image icon;
        }

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();

        [Header("Ability icons (elemental upgrade highlight)")]
        [Tooltip("One entry per ability icon, keyed by the element that upgrades it (per the " +
                 "vessel's ElementalAbilityMapSO). The icon glows while that element's level-5 " +
                 "upgrade is active. Shared system - every vessel HUD wires its own four icons.")]
        public List<AbilityIconBinding> abilityIcons = new();

        [Tooltip("Tint applied to an ability icon while its elemental upgrade is active. Views " +
                 "that repaint icon colors per-frame still show the persistent scale bump below.")]
        [SerializeField] private Color upgradeHighlightColor = new(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Persistent scale an upgraded ability icon rests at while the upgrade is active.")]
        [SerializeField] private float upgradeHighlightScale = 1.15f;
        [Tooltip("Scale punch played when an ability upgrade unlocks.")]
        [SerializeField] private float upgradePunchScale = 1.35f;
        [SerializeField] private float upgradePunchDuration = 0.35f;

        readonly Dictionary<Element, Color> _abilityIconRestColors = new();
        readonly Dictionary<Element, Tween> _abilityIconTweens = new();

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Tween _fadeTween;

        public abstract void Initialize();

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

        /// <summary>
        /// Highlights (or rests) the ability icon bound to this element - called by the base
        /// VesselHUDController from the ElementalAbilityHandler's OnUpgradeStateChanged event
        /// and once at init to seed already-active upgrades. Safe no-op for unbound elements.
        /// </summary>
        public void SetAbilityUpgraded(Element element, bool upgraded)
        {
            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.icon) continue;

                if (!_abilityIconRestColors.ContainsKey(element))
                    _abilityIconRestColors[element] = binding.icon.color;

                if (_abilityIconTweens.TryGetValue(element, out var tween))
                    tween?.Kill();

                if (upgraded)
                {
                    binding.icon.color = upgradeHighlightColor;
                    // Rest at the highlight scale (survives views that repaint colors per-frame),
                    // with a one-shot punch around it to telegraph the unlock.
                    binding.icon.rectTransform.localScale = Vector3.one * upgradeHighlightScale;
                    _abilityIconTweens[element] = binding.icon.rectTransform
                        .DOPunchScale(Vector3.one * (upgradePunchScale - upgradeHighlightScale),
                            upgradePunchDuration, 1, 0.5f)
                        .SetUpdate(true)
                        .SetLink(binding.icon.gameObject);
                }
                else
                {
                    binding.icon.color = _abilityIconRestColors[element];
                    binding.icon.rectTransform.localScale = Vector3.one;
                }
            }
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
            foreach (var tween in _abilityIconTweens.Values)
                tween?.Kill();
            _abilityIconTweens.Clear();
        }
    }
}
