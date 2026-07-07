using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    public class FadeIn : MonoBehaviour
    {
        [SerializeField] float fadeInRate;

        Renderer _renderer;
        Material material;
        Coroutine fadeInCoroutine;

        void Start()
        {
            // One explicit instance material, created once. The previous version cloned twice —
            // `.material` (implicit clone) then `new Material(...)` of that clone — leaking the
            // intermediate per renderer per mint (conveyor crystals mint continuously), and the
            // fade loop did a GetComponent + `.material` every frame.
            _renderer = GetComponent<Renderer>();
            material = new Material(_renderer.sharedMaterial);
            _renderer.material = material;

            StartFadeIn();
        }

        void OnDestroy()
        {
            if (material) Destroy(material);
        }

        public void StartFadeIn()
        {
            if (!material) return;

            // Set the opacity to zero before starting the coroutine so there is no delay in the start of the effect
            material.SetFloat("_opacity", 0f);

            if (fadeInCoroutine != null)
                StopCoroutine(fadeInCoroutine);

            fadeInCoroutine = StartCoroutine(FadeInCoroutine());
        }

        IEnumerator FadeInCoroutine()
        {
            fadeInRate = .001f;
            var opacity = 0f;
            while (opacity < 1)
            {
                yield return null;
                fadeInRate *= 1.00f + Time.deltaTime;
                opacity += fadeInRate;
                material.SetFloat("_opacity", opacity);
            }
        }
    }
}
