using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fades a renderer in by driving its shader's _opacity through a
    /// MaterialPropertyBlock — no material clone, no per-frame GetComponent.
    /// The override is cleared once the fade completes so material swaps
    /// (crystal activation, domain changes) always show their authored opacity.
    /// </summary>
    public class FadeIn : MonoBehaviour
    {
        static readonly int OpacityID = Shader.PropertyToID("_opacity");

        [SerializeField] float fadeInRate;

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        Coroutine fadeInCoroutine;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

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

        public void StartFadeIn()
        {
            // Zero the opacity before starting the coroutine so there is no
            // one-frame flash at full opacity.
            _mpb.SetFloat(OpacityID, 0f);
            _renderer.SetPropertyBlock(_mpb);

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
                _mpb.SetFloat(OpacityID, opacity);
                _renderer.SetPropertyBlock(_mpb);
            }

            // Drop the override so the material's own opacity wins from here on.
            _mpb.Clear();
            _renderer.SetPropertyBlock(_mpb);
            fadeInCoroutine = null;
        }
    }
}
