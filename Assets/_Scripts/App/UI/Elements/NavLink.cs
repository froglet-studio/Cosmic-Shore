using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using CosmicShore.App.UI.Views;

namespace CosmicShore.App.UI
{
    /// <summary>
    /// 
    /// </summary>
    public class NavLink : MonoBehaviour
    {
        [SerializeField] public View view;
        [SerializeField] List<Image> activeImageElements;
        [SerializeField] List<Image> inactiveImageElements;
        [SerializeField] List<TMP_Text> activeTextElements;
        [SerializeField] List<TMP_Text> inactiveTextElements;

        [SerializeField] float crossfadeDuration = 0.15f;
        [HideInInspector] public NavGroup navGroup;
        public int Index;

        Coroutine currentCrossfade;
        bool isActive;

        List<Color> activeImageStartColors = new();
        List<Color> inactiveImageStartColors = new();
        List<Color> activeTextStartColors = new();
        List<Color> inactiveTextStartColors = new();

        void Awake()
        {
            if (activeImageElements.Count != inactiveImageElements.Count)
                Debug.LogError($"NavLink Configuration Error: activeImageElements.Count != inactiveImageElements.Count  --- for: {gameObject.name}");

            if (activeTextElements.Count != inactiveTextElements.Count)
                Debug.LogError($"NavLink Configuration Error: activeTextElements.Count != inactiveTextElements.Count  --- for: {gameObject.name}");

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
            navGroup.ActivateLink(this);
        }

        public virtual void SetActive(bool isActive)
        {
            if (this.isActive == isActive)
                return;

            this.isActive = isActive;

            if (currentCrossfade != null)
                StopCoroutine(currentCrossfade);

            currentCrossfade = StartCoroutine(CrossfadeImages(isActive));
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

            currentCrossfade = null;
        }

        void CrossfadeImage(bool isActive, float normalizedTime, Image activeImage, Image inactiveImage, Color initialActiveColor, Color initialInactiveColor)
        {
            activeImage.color = new Color(initialActiveColor.r, initialActiveColor.g, initialActiveColor.b, isActive ? initialActiveColor.a * normalizedTime : initialActiveColor.a * (1 - normalizedTime));
            inactiveImage.color = new Color(initialInactiveColor.r, initialInactiveColor.g, initialInactiveColor.b, isActive ? initialInactiveColor.a * (1 - normalizedTime) : initialInactiveColor.a * normalizedTime);
        }
    }
}