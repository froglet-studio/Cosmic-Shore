using System.Runtime.CompilerServices;

namespace CosmicShore.Engine.UI
{
    /// <summary>
    /// Legacy uGUI <see cref="Text"/> is laid out and drawn through the TextMeshPro path: each
    /// legacy component gets a dormant <see cref="TextMeshProUGUI"/> twin (on a hidden, inactive,
    /// never-queried holder) whose properties mirror it — font size, colour, alignment, style,
    /// wrapping, overflow, line spacing, rich text, best fit. The twin carries no font, so it
    /// resolves to the project's default TMP font (Liberation Sans — metric-compatible with
    /// Unity's legacy Arial/LegacyRuntime runtime font). Nothing about the legacy component's
    /// own GameObject changes.
    /// </summary>
    public static class LegacyTextProxy
    {
        static readonly ConditionalWeakTable<Text, TextMeshProUGUI> s_twins = new();
        static GameObject s_holder;

        /// <summary>The configured TMP twin of <paramref name="legacy"/>, synced to its current state.</summary>
        public static TMP_Text For(Text legacy)
        {
            if (!s_twins.TryGetValue(legacy, out var twin) || twin == null)
            {
                if (s_holder == null)
                {
                    s_holder = new GameObject("__legacy_text_twins");
                    s_holder.SetActive(false);
                    s_holder.MarkAsPrefabAsset();
                    Object.DontDestroyOnLoad(s_holder);
                }
                var go = new GameObject("twin");
                go.transform.SetParent(s_holder.transform, false);
                twin = go.AddComponent<TextMeshProUGUI>();
                s_twins.AddOrUpdate(legacy, twin);
            }
            Sync(legacy, twin);
            return twin;
        }

        static void Sync(Text l, TextMeshProUGUI t)
        {
            if (t.text != l.text) t.text = l.text;
            t.color = l.color;
            t.richText = l.supportRichText;
            t.alignment = Alignment(l.alignment);
            t.fontStyle = l.fontStyle switch
            {
                FontStyle.Bold => FontStyles.Bold,
                FontStyle.Italic => FontStyles.Italic,
                FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
                _ => FontStyles.Normal,
            };
            t.textWrappingMode = l.horizontalOverflow == HorizontalWrapMode.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = l.verticalOverflow == VerticalWrapMode.Truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
            // Legacy line spacing is a multiplier; TMP's is additive in hundredths of an em.
            t.lineSpacing = (l.lineSpacing - 1f) * 100f;
            t.enableAutoSizing = l.resizeTextForBestFit;
            if (l.resizeTextForBestFit)
            {
                t.fontSizeMin = l.resizeTextMinSize;
                t.fontSizeMax = l.resizeTextMaxSize;
            }
            t.fontSize = l.fontSize > 0 ? l.fontSize : 14;
            if (l.transform is RectTransform src && t.transform is RectTransform dst)
            {
                var r = src.rect;
                dst.sizeDelta = new Vector2(r.width, r.height);
                dst.pivot = src.pivot;
            }
        }

        static TextAlignmentOptions Alignment(TextAnchor a) => a switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            _ => TextAlignmentOptions.BottomRight,
        };
    }
}
