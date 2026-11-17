using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Keeps the boot poster's centre group (the bands, the hulls, the captains) inside a narrow screen: the
    /// group is authored at a fixed width for a 16:9 canvas, and on a canvas narrower than that (a phone held
    /// upright, a 4:3 tablet) it is scaled down about its pivot until it fits. It never scales up: a wider
    /// screen shows more of the sea either side. It fits again only when the frame's size changes.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI/Poster Fit")]
    public class PosterFit : MonoBehaviour
    {
        [Tooltip("The parent whose width the group must fit in. Its size changes drive the fit.")]
        [SerializeField] RectTransform frame;
        [Tooltip("The width the group's content was authored for, in canvas units.")]
        [SerializeField, Min(1f)] float authoredWidth = 1920f;

        Vector2 _fittedTo = new(-1f, -1f);

        /// <summary>The group's scale in a frame this wide: 1 while it fits, smaller on a narrower frame.</summary>
        public static float ScaleFor(float frameWidth, float authoredWidth) =>
            authoredWidth <= 0f || frameWidth <= 0f ? 1f : Mathf.Min(1f, frameWidth / authoredWidth);

        void OnEnable() => Fit();

        // The frame is a stretched parent, so its size is read here; a canvas resize reaches this object as a
        // dimensions change only when its own rect is stretched, so the frame's size is compared each frame.
        void LateUpdate()
        {
            if (frame && frame.rect.size != _fittedTo) Fit();
        }

        void Fit()
        {
            if (!frame) return;
            _fittedTo = frame.rect.size;
            float scale = ScaleFor(_fittedTo.x, authoredWidth);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
