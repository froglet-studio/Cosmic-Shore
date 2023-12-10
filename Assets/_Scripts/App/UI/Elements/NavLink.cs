using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace CosmicShore.App.UI
{
    public class NavLink : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] Image activeImage;
        [SerializeField] Image inactiveImage;
        [SerializeField] float crossfadeDuration = 0.5f;
        public NavLinkGroup navLinkGroup;

        Coroutine currentCrossfade;

        public void OnClick()
        {
            navLinkGroup.ActivateLink(this);
        }

        public void SetActive(bool isActive)
        {
            if (currentCrossfade != null)
                StopCoroutine(currentCrossfade);

            currentCrossfade = StartCoroutine(CrossfadeImage(isActive));
            panel.SetActive(isActive);
        }

        IEnumerator CrossfadeImage(bool isActive)
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