using System;
using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fades a renderer in by driving its shader's _opacity through a
    /// MaterialPropertyBlock — no material clone, no per-frame GetComponent.
    /// The override is cleared once the fade completes so material swaps
    /// (crystal activation, domain changes) always show their authored opacity.
    ///
    /// This component owns exactly ONE property (_opacity), but a property block belongs to the
    /// RENDERER, not to the component that writes it — so it must never assume the block is its
    /// own. It merges into whatever is already there on every write, and raises
    /// <see cref="FadeCompleted"/> after the clear so a co-owner can put its overrides back
    /// (MaterialPropertyBlock cannot drop a single key, so Clear takes the whole block with it).
    ///
    /// Crystal.ApplyColorSetTint is the co-owner that made this necessary. Before the merge, the
    /// bloom wiped every crystal's collectability tint — a living lifeform's heart is blue, a free
    /// pickup is the lime CTA — and the crystal fell back to its authored material colour, which
    /// is why Charge and Time hearts read lime while their lifeform was still alive.
    /// </summary>
    public class FadeIn : MonoBehaviour
    {
        static readonly int OpacityID = Shader.PropertyToID("_opacity");

        [SerializeField] float fadeInRate;

        Renderer _renderer;
        MaterialPropertyBlock _mpb;
        Coroutine fadeInCoroutine;

        /// <summary>
        /// Raised on the frame the bloom finishes, immediately after the _opacity override is
        /// dropped. Anything that keeps its OWN override on this renderer must subscribe and
        /// re-apply here — the clear that ends the fade cannot spare it.
        /// </summary>
        public event Action FadeCompleted;

        /// <summary>
        /// True when something else also writes this renderer's property block — it announced
        /// itself by subscribing to <see cref="FadeCompleted"/>. Only then does the bloom pay for
        /// a read-back per frame; a renderer this component owns outright keeps the cheap path,
        /// which matters because a single prefab can carry thousands of these blooming at once.
        /// </summary>
        bool HasBlockCoOwner => FadeCompleted != null;

        void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            StartFadeIn();
        }

        public void StartFadeIn()
        {
            // Zero the opacity before starting the coroutine so there is no
            // one-frame flash at full opacity.
            WriteOpacity(0f);

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
                WriteOpacity(opacity);
            }

            // Drop the override so the material's own opacity wins from here on.
            _mpb.Clear();
            _renderer.SetPropertyBlock(_mpb);
            fadeInCoroutine = null;

            // ...and hand the block back to its other owners, since the clear above took
            // their overrides with it.
            FadeCompleted?.Invoke();
        }

        /// <summary>
        /// Read-modify-write of the shared per-renderer block. The read-back is what keeps a
        /// co-owner's overrides alive across the whole bloom rather than only until this
        /// component's next frame.
        /// </summary>
        void WriteOpacity(float opacity)
        {
            if (HasBlockCoOwner)
                _renderer.GetPropertyBlock(_mpb);

            _mpb.SetFloat(OpacityID, opacity);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
