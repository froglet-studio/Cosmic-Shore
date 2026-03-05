using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using CosmicShore.App.Systems.Audio;
using CosmicShore.App.UI.Views;
using CosmicShore.Utility;

namespace CosmicShore.App.UI
{
    /// <summary>
    /// Nav tab with crossfade, DOTween scale/position bounce, and gamepad select support.
    /// </summary>
    public class NavLink : MonoBehaviour, ISelectHandler
    {
        [SerializeField] public View view;
        [SerializeField] List<Image> activeImageElements;
        [SerializeField] List<Image> inactiveImageElements;
        [SerializeField] List<TMP_Text> activeTextElements;
        [SerializeField] List<TMP_Text> inactiveTextElements;

        [SerializeField] bool dynamicSize = false;
        [SerializeField] Vector2 activeDimensions;
        [SerializeField] Vector2 inactiveDimensions;
        [SerializeField] float crossfadeDuration = 0.15f;

        [Header("Activation Animation")]
        [SerializeField] private float bounceScale = 1.15f;
        [SerializeField] private float bounceDuration = 0.25f;
        [SerializeField] private float activeYOffset = 4f;
        [SerializeField] private float positionDuration = 0.2f;

        [HideInInspector]public NavGroup navGroup;
        public int Index;

        Coroutine currentCrossfade;
        bool isActive;

        private Tween _scaleTween;
        private Tween _positionTween;
        private Vector3 _originalLocalPosition;
        private Vector3 _originalScale;

        List<Color> activeImageStartColors = new();
        List<Color> inactiveImageStartColors = new();
        List<Color> activeTextStartColors = new();
        List<Color> inactiveTextStartColors = new();

        void Awake()
        {
            _originalLocalPosition = transform.localPosition;
            _originalScale = transform.localScale;

            if (activeImageElements.Count != inactiveImageElements.Count)
                CSDebug.LogError($"NavLink Configuration Error: activeImageElements.Count != inactiveImageElements.Count  --- for: {gameObject.name}");

            if (activeTextElements.Count != inactiveTextElements.Count)
                CSDebug.LogError($"NavLink Configuration Error: activeTextElements.Count != inactiveTextElements.Count  --- for: {gameObject.name}");

            // Save off initial text an image colors
            for (int i = 0; i < activeImageElements.Count; i++)
            {
                activeImageStartColors.Add(activeImageElements[i].color);
                inactiveImageStartColors.Add(inactiveImageElements[i].color);
            }
            for (int i = 0; i < activeTextElements.Count; i++)
            {
                activeTextStartColors.Add(activeTextElements[i].color);
                inactiveTextStartColors.Add(inactiveTextElements[i].color);
            }
        }

        public void OnClick()
        {
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.SwitchView);
            navGroup.ActivateLink(this);
        }

        public void OnSelect(BaseEventData eventData)
        {
            // When selected via gamepad DPad, activate this link
            if (!isActive)
                OnClick();
        }

        public virtual void SetActive(bool isActive)
        {
            if (this.isActive == isActive)
                return;

            this.isActive = isActive;

            if (currentCrossfade != null)
                StopCoroutine(currentCrossfade);

            currentCrossfade = StartCoroutine(CrossfadeImages(isActive));

            // Scale bounce + Y-offset animation
            AnimateActivation(isActive);
        }

        private void AnimateActivation(bool active)
        {
            _scaleTween?.Kill();
            _positionTween?.Kill();

            if (active)
            {
                // Bounce up: scale overshoot then settle
                transform.localScale = _originalScale;
                _scaleTween = transform.DOScale(_originalScale * bounceScale, bounceDuration * 0.4f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _scaleTween = transform.DOScale(_originalScale, bounceDuration * 0.6f)
                            .SetEase(Ease.OutBack)
                            .SetUpdate(true);
                    });

                // Slide up
                var targetPos = _originalLocalPosition + new Vector3(0, activeYOffset, 0);
                _positionTween = transform.DOLocalMove(targetPos, positionDuration)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
            else
            {
                // Return to original
                _scaleTween = transform.DOScale(_originalScale, positionDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);

                _positionTween = transform.DOLocalMove(_originalLocalPosition, positionDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
        }

        void OnDisable()
        {
            _scaleTween?.Kill();
            _positionTween?.Kill();
        }

        IEnumerator CrossfadeImages(bool isActive)
        {
            for (int i = 0; i < activeImageElements.Count; i++)
            {
                activeImageElements[i].enabled = true;
                inactiveImageElements[i].enabled = true;
            }

            float time = 0;
            while (time <= crossfadeDuration)
            {
                time += Time.unscaledDeltaTime;
                float normalizedTime = time / crossfadeDuration;

                for (int i = 0; i < activeImageElements.Count; i++)
                {
                    CrossfadeImage(isActive, normalizedTime, activeImageElements[i], inactiveImageElements[i], activeImageStartColors[i], inactiveImageStartColors[i]);
                }

                yield return null;
            }

            for (int i = 0; i < activeImageElements.Count; i++)
            {
                CrossfadeImage(isActive, 1, activeImageElements[i], inactiveImageElements[i], activeImageStartColors[i], inactiveImageStartColors[i]);
            }

            for (int i = 0; i < activeImageElements.Count; i++)
            {
                inactiveImageElements[i].enabled = !isActive;
                activeImageElements[i].enabled = isActive;
            }

            if (dynamicSize)
            {
                var rectTransform = GetComponent<RectTransform>();

                rectTransform.sizeDelta = isActive ? activeDimensions : inactiveDimensions;

                navGroup.UpdateLayout();
            }

            currentCrossfade = null;
        }

        void CrossfadeImage(bool isActive, float normalizedTime, Image activeImage, Image inactiveImage, Color initialActiveColor, Color initialInactiveColor)
        {
            activeImage.color = new Color(initialActiveColor.r, initialActiveColor.g, initialActiveColor.b, isActive ? initialActiveColor.a * normalizedTime : initialActiveColor.a * (1 - normalizedTime));
            inactiveImage.color = new Color(initialInactiveColor.r, initialInactiveColor.g, initialInactiveColor.b, isActive ? initialInactiveColor.a * (1 - normalizedTime) : initialInactiveColor.a * normalizedTime);
        }
    }
}