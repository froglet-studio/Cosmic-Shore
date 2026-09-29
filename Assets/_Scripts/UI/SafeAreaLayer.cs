using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Builds the CONTENT layer a <see cref="SafeAreaFitter"/> is meant to sit on.
    ///
    /// <para>The fitter's contract (<c>Docs/UI_ARCHITECTURE_AUDIT.md</c> §1.3) splits a canvas into
    /// a FULL-BLEED layer — whose job is to cover the screen: a fade, a scrim, a dimmer, branded
    /// splash art — and a CONTENT layer, which is everything readable or tappable. This helper is
    /// the content layer, for the canvases that are built in code (and for the one authored case
    /// where a component on the host would be stomped; see <see cref="Wrap"/>).</para>
    ///
    /// <para>Two constraints from that section are encoded here rather than restated per call site:
    /// the layer is a DIRECT CHILD of the canvas root (<see cref="SafeAreaFitter.ComputeAnchors"/>
    /// normalises against the screen, so a rect nested under something smaller than the canvas
    /// resolves its anchors against the wrong rect), and it is authored full-stretch with zero
    /// offsets, so on a display whose safe area IS the screen it is exactly the canvas rect and the
    /// whole thing is a no-op.</para>
    /// </summary>
    public static class SafeAreaLayer
    {
        /// <summary>Name every layer this helper creates carries, and the name <see cref="Wrap"/>
        /// recognises so a second pass adopts the existing layer instead of nesting another.</summary>
        public const string LayerName = "Safe Area";

        /// <summary>
        /// Creates a full-stretch, zero-offset child of <paramref name="parent"/> carrying a
        /// <see cref="SafeAreaFitter"/>. Content the caller wants constrained is parented to the
        /// returned rect; anything that must bleed stays a sibling.
        /// </summary>
        /// <param name="parent">Must be the canvas root, or a rect that exactly matches it.</param>
        /// <param name="siblingIndex">Draw order within the parent. Negative leaves it last.</param>
        public static RectTransform Create(Transform parent, int siblingIndex = -1)
        {
            var go = new GameObject(LayerName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            if (siblingIndex >= 0) rt.SetSiblingIndex(siblingIndex);
            go.AddComponent<SafeAreaFitter>();
            return rt;
        }

        /// <summary>
        /// Inserts a fitted layer between <paramref name="content"/> and its current parent, and
        /// moves <paramref name="content"/> into it — keeping its sibling index, so draw order is
        /// unchanged.
        ///
        /// <para>This is the shape to reach for when something ELSE writes the content rect's
        /// anchors. A <see cref="SafeAreaFitter"/> on such a rect is silently defeated: it caches
        /// the safe area it last applied against, so an overwrite it did not cause never looks like
        /// a change and is never re-applied. The vessel HUDs are that case —
        /// <c>AbilityLockupView.NormaliseHudRoot</c> stamps <c>anchorMin 0</c> / <c>anchorMax 1</c>
        /// onto the HUD root once at initialization. Wrapped, the two compose: the HUD root
        /// stretches to fill the SAFE rect instead of the screen.</para>
        ///
        /// <para>Idempotent — a second call returns the existing layer.</para>
        /// </summary>
        /// <para>The content's parent must be a CANVAS root — constraint 1 above, checked rather
        /// than assumed, because the failure it prevents is silent: a layer inserted under a rect
        /// smaller than the canvas would resolve screen-normalised anchors against the wrong rect
        /// and mis-place the content on every device instead of none.</para>
        ///
        /// <returns>The fitted layer, or null when there is nothing safe to insert under — no
        /// parent (the content is already a canvas root, which must never carry the fitter), or a
        /// parent that is not one.</returns>
        public static RectTransform Wrap(RectTransform content)
        {
            if (!content) return null;

            var parent = content.parent as RectTransform;
            if (!parent) return null;

            // Already wrapped, by us or by an authored layer.
            if (parent.GetComponent<SafeAreaFitter>()) return parent;

            if (!parent.GetComponent<Canvas>()) return null;

            int index = content.GetSiblingIndex();
            var layer = Create(parent, index);
            content.SetParent(layer, false);
            return layer;
        }

        /// <summary>Full-stretch, zero offsets — the authored state the fitter reads as "no insets".</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }
    }
}
