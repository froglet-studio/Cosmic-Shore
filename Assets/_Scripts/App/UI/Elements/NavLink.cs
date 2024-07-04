using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Serialization;
using System.Collections.Generic;
using TMPro;

namespace CosmicShore.App.UI
{
    public class NavLink : MonoBehaviour
    {
        [FormerlySerializedAs("panel")]
        [FormerlySerializedAs("view")]
        [FormerlySerializedAs("selectView")]
        [Tooltip("Set this to the view to activate for Select/Toggle Views")]
        [SerializeField] public GameObject toggleView;
        [Tooltip("Set this to the view to update for Update Views")]
        [SerializeField] public View updateView;
        [SerializeField] Image activeImage;
        [SerializeField] Image inactiveImage;
        [SerializeField] List<Image> activeImageElements;
        [SerializeField] List<Image> inactiveImageElements;
        [SerializeField] List<TMP_Text> activeTextElements;
        [SerializeField] List<TMP_Text> inactiveTextElements;

        [SerializeField] float crossfadeDuration = 0.5f;
        [HideInInspector] public NavGroup navGroup;

        Coroutine currentCrossfade;

        List<Color> activeImageStartColors = new();
        List<Color> inactiveImageStartColors = new();
        List<Color> activeTextStartColors = new();
        List<Color> inactiveTextStartColors = new();

        void Start()
        {
            if (activeImageElements.Count != inactiveImageElements.Count)
                Debug.LogError($"NavLink Configuration Error: activeImageElements.Count != inactiveImageElements.Count  --- for: {gameObject.name}");

            if (activeTextElements.Count != inactiveTextElements.Count)
                Debug.LogError($"NavLink Configuration Error: activeTextElements.Count != inactiveTextElements.Count  --- for: {gameObject.name}");

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
            //Debug.LogError($"NavLink - OnClick");
            navGroup.ActivateLink(this);
        }

        public virtual void SetActive(bool isActive)
        {
            if (currentCrossfade != null)
                StopCoroutine(currentCrossfade);

            currentCrossfade = StartCoroutine(CrossfadeImages(isActive));
        }

        IEnumerator CrossfadeImages(bool isActive, Image activeImage, Image inactiveImage)
        {
            float time = 0;


            for (int i = 0; i < activeImageElements.Count; i++)
            {


                //activeStartColors[i].a = isActive ? 1 : 0;
                //inactiveStartColors[i].a = isActive ? 0 : 1;
            }


                Color activeStartColor = activeImage.color;
            Color inactiveStartColor = inactiveImage.color;

            activeStartColor.a = isActive ? 1 : 0;
            inactiveStartColor.a = isActive ? 0 : 1;

            activeImage.enabled = true;
            inactiveImage.enabled = true;

            while (time < crossfadeDuration)
            {
                time += Time.deltaTime;
                float normalizedTime = time / crossfadeDuration;

                activeImage.color = new Color(activeStartColor.r, activeStartColor.g, activeStartColor.b, Mathf.Lerp(activeStartColor.a, isActive ? 1 : 0, normalizedTime));
                inactiveImage.color = new Color(inactiveStartColor.r, inactiveStartColor.g, inactiveStartColor.b, Mathf.Lerp(inactiveStartColor.a, isActive ? 0 : 1, normalizedTime));

                yield return null;
            }

            if (!isActive)
            {
                inactiveImage.enabled = true;
                activeImage.enabled = false;
            }
            else
            {
                inactiveImage.enabled = false;
                activeImage.enabled = true;
            }

            currentCrossfade = null;
        }

        IEnumerator CrossfadeImages(bool isActive)
        {
            float time = 0;

            Color activeStartColor = activeImage.color;
            Color inactiveStartColor = inactiveImage.color;

            activeStartColor.a = isActive ? 1 : 0;
            inactiveStartColor.a = isActive ? 0 : 1;

            activeImage.enabled = true;
            inactiveImage.enabled = true;

            while (time < crossfadeDuration)
            {
                time += Time.deltaTime;
                float normalizedTime = time / crossfadeDuration;

                activeImage.color = new Color(activeStartColor.r, activeStartColor.g, activeStartColor.b, Mathf.Lerp(activeStartColor.a, isActive ? 1 : 0, normalizedTime));
                inactiveImage.color = new Color(inactiveStartColor.r, inactiveStartColor.g, inactiveStartColor.b, Mathf.Lerp(inactiveStartColor.a, isActive ? 0 : 1, normalizedTime));

                yield return null;
            }

            if (!isActive)
            {
                inactiveImage.enabled = true;
                activeImage.enabled = false;
            }
            else
            {
                inactiveImage.enabled = false;
                activeImage.enabled = true;
            }

            currentCrossfade = null;
        }
    }
}