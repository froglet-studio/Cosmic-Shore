using System;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Reads a RectTransform's layout inputs by querying its <see cref="ILayoutElement"/>
    /// components (original contract): the highest layoutPriority wins; among elements at
    /// that priority the MAXIMUM value is taken; disabled behaviours are skipped; a negative
    /// value means "no opinion". Preferred sizes are never below min.
    /// </summary>
    public static class LayoutUtility
    {
        public static float GetMinSize(RectTransform rect, int axis)
            => axis == 0 ? GetMinWidth(rect) : GetMinHeight(rect);

        public static float GetPreferredSize(RectTransform rect, int axis)
            => axis == 0 ? GetPreferredWidth(rect) : GetPreferredHeight(rect);

        public static float GetFlexibleSize(RectTransform rect, int axis)
            => axis == 0 ? GetFlexibleWidth(rect) : GetFlexibleHeight(rect);

        public static float GetMinWidth(RectTransform rect)
            => GetLayoutProperty(rect, static e => e.minWidth, 0f);

        public static float GetPreferredWidth(RectTransform rect)
            => Mathf.Max(GetLayoutProperty(rect, static e => e.minWidth, 0f),
                         GetLayoutProperty(rect, static e => e.preferredWidth, 0f));

        public static float GetFlexibleWidth(RectTransform rect)
            => GetLayoutProperty(rect, static e => e.flexibleWidth, 0f);

        public static float GetMinHeight(RectTransform rect)
            => GetLayoutProperty(rect, static e => e.minHeight, 0f);

        public static float GetPreferredHeight(RectTransform rect)
            => Mathf.Max(GetLayoutProperty(rect, static e => e.minHeight, 0f),
                         GetLayoutProperty(rect, static e => e.preferredHeight, 0f));

        public static float GetFlexibleHeight(RectTransform rect)
            => GetLayoutProperty(rect, static e => e.flexibleHeight, 0f);

        /// <summary>The priority-resolved value of one layout property across the rect's elements.</summary>
        public static float GetLayoutProperty(RectTransform rect, Func<ILayoutElement, float> property, float defaultValue)
        {
            if (rect == null) return 0f;

            float value = defaultValue;
            int maxPriority = int.MinValue;
            foreach (var element in rect.gameObject.GetComponents<ILayoutElement>())
            {
                if (element is Behaviour { isActiveAndEnabled: false }) continue;

                int priority = element.layoutPriority;
                if (priority < maxPriority) continue;

                float candidate = property(element);
                if (candidate < 0f) continue; // negative = no opinion

                if (priority > maxPriority)
                {
                    value = candidate;
                    maxPriority = priority;
                }
                else if (candidate > value)
                {
                    value = candidate;
                }
            }
            return value;
        }
    }
}
