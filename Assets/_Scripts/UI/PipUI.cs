using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;
using System.Collections;

namespace CosmicShore.UI
{
    public class PipUI : MonoBehaviour
    {

        bool isSmall = true;
        public bool mirrored = true;

        [SerializeField] Vector3 smallScale = new Vector3(1, 1, 1);
        [SerializeField] Vector3 largeScale = new Vector3(2, 2, 1);
        [SerializeField] Vector3 smallPosition = new Vector3(0, 445, 0);
        [SerializeField] Vector3 largePosition = new Vector3(0, 375, 0);


        private void Start()
        {
            SetMirrored(mirrored);
            SilenceUntexturedGraphics();
        }

        /// <summary>
        /// A <see cref="RawImage"/> whose texture is missing does not draw nothing - it draws a
        /// SOLID QUAD in its own tint, at whatever size its rect happens to be. So a frame
        /// graphic whose art was deleted from the project stops being a frame and becomes an
        /// opaque panel, and the only evidence anywhere is the panel itself.
        ///
        /// <para>That is what shipped here: this prefab's <c>border</c> points at texture guid
        /// <c>24ca4c74937a9ed4d9251952057575ab</c>, which no asset in the project carries any
        /// more, and its tint is (0.14, 0.29, 0.52) - so the moment a vessel switched the Pip
        /// on, a 780x400 navy rectangle covering over half the screen appeared over the game
        /// with the actual 300x150 view sitting inside it. It went unseen for as long as it did
        /// because only eight of eleven hulls carry a <c>Pip</c>, and no arcade mode had ever
        /// locked to one of them until Hijack locked to the Urchin.</para>
        ///
        /// <para>The fix is a statement rather than an asset edit, and deliberately: it NAMES
        /// the offender in the console (an asset edit cannot), it covers the next textureless
        /// graphic as well as this one, and it undoes itself the day the art is restored - where
        /// switching the component off in the prefab would keep the frame dark forever with
        /// nothing to say why. Silence is not a fallback hiding a fault: the warning is the
        /// fault, stated once, and what is suppressed is a lie about the UI.</para>
        /// </summary>
        void SilenceUntexturedGraphics()
        {
            var images = GetComponentsInChildren<RawImage>(includeInactive: true);
            for (int i = 0; i < images.Length; i++)
            {
                var image = images[i];
                if (image == null || !image.enabled || image.texture != null) continue;

                image.enabled = false;
                CSDebug.LogWarning(
                    $"[PipUI] '{image.name}' is a RawImage with no texture, so it was about to " +
                    "paint a solid " +
                    $"{image.rectTransform.rect.width:0}x{image.rectTransform.rect.height:0} " +
                    $"rectangle in {image.color}. Its art is missing - restore the texture (or " +
                    "delete the graphic); it is switched off for this session.");
            }
        }

        /// <summary>
        /// Point the panel the right way round and return it to its resting size.
        ///
        /// <para>Mirroring is a NEGATIVE x scale rather than a flipped texture, because that
        /// keeps the raycast target - so the sign has to live in the two scale fields that
        /// <see cref="ToggleSizeAndPosition"/> lerps toward, not just on the transform.</para>
        ///
        /// <para><b>It is written from the absolute value on purpose.</b> This used to negate
        /// whatever was there, which made it correct exactly once: called a second time with
        /// <c>mirrored</c> true it flipped the panel back, and the panel is now told which way
        /// to face on every ownership bind rather than once at Start. An idempotent setter is
        /// what makes "tell it again" a safe thing for a caller to do.</para>
        /// </summary>
        public void SetMirrored(bool mirrored)
        {
            this.mirrored = mirrored;

            float sign = mirrored ? -1f : 1f;
            smallScale = new Vector3(sign * Mathf.Abs(smallScale.x), smallScale.y, smallScale.z);
            largeScale = new Vector3(sign * Mathf.Abs(largeScale.x), largeScale.y, largeScale.z);

            isSmall = true;
            ((RectTransform)transform).localScale = smallScale;
            ((RectTransform)transform).localPosition = smallPosition;
        }

        public void ToggleSizeAndPosition()
        {
            CSDebug.Log("pip button pressed");
            isSmall = !isSmall;

            // negative x is to get the mirror image without loosing the raycast target. the y values are whack and we don't know why.
            if (isSmall)
            {
                StartCoroutine(LerpUtilities.LerpingCoroutine(((RectTransform)transform).localScale, smallScale, .5f, (i) => { ((RectTransform)transform).localScale = i; }));
                StartCoroutine(LerpUtilities.LerpingCoroutine(((RectTransform)transform).localPosition, smallPosition, .5f, (i) => { ((RectTransform)transform).localPosition = i; }));
            }
            else
            {
                StartCoroutine(LerpUtilities.LerpingCoroutine(((RectTransform)transform).localScale, largeScale, .5f, (i) => { ((RectTransform)transform).localScale = i; }));
                StartCoroutine(LerpUtilities.LerpingCoroutine(((RectTransform)transform).localPosition, largePosition, .5f, (i) => { ((RectTransform)transform).localPosition = i; }));
            }

        }
    }
}