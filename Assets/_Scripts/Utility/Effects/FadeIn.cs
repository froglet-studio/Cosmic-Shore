using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fades a renderer in by driving its shader's _opacity through a
    /// MaterialPropertyBlock — no material clone, no per-frame GetComponent.
    ///
    /// Every write COMPOSES with the block already on the renderer instead of replacing it.
    /// This is load-bearing, not tidiness: <see cref="CosmicShore.Gameplay.Crystal"/> paints the
    /// free-pickup lime through a property block on these same renderers, and this component used
    /// to own a private block it pushed wholesale (wiping the tint at the START of the fade) and
    /// then Clear() at the end (wiping it again, permanently). Every crystal in the game therefore
    /// settled back to its authored material colour and the CTA lime was never seen. Composing also
    /// makes the two writers order-independent, so it no longer matters whether Crystal.Start or
    /// FadeIn.Start runs first.
    ///
    /// The fade override is retired by restoring the MATERIAL's own authored opacity rather than by
    /// clearing the block — same end state for _opacity, without discarding anyone else's overrides.
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
            SetOpacity(0f);

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
                SetOpacity(opacity);
            }

            // Retire the fade by handing _opacity back to the material's authored value, rather
            // than Clear()ing the block — a Clear would also discard the crystal's colour tint.
            var mat = _renderer.sharedMaterial;
            SetOpacity(mat && mat.HasProperty(OpacityID) ? mat.GetFloat(OpacityID) : 1f);
            fadeInCoroutine = null;
        }

        /// <summary>Writes _opacity ON TOP of whatever block the renderer already carries.</summary>
        void SetOpacity(float opacity)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OpacityID, opacity);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
