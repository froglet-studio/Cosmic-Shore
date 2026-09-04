#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// Shared pastel styling for the Performance Benchmark window - centralizes the palette
    /// and reusable IMGUI factories (section headers, badges, stat rows, score bars) so the
    /// tabs look consistent. Styles are created lazily inside OnGUI.
    /// </summary>
    public static class EditorUIStyles
    {
        // ── Pastel palette ──────────────────────────────
        public static readonly Color Mint = new(0.62f, 0.90f, 0.76f);
        public static readonly Color Sky = new(0.66f, 0.82f, 0.96f);
        public static readonly Color Lavender = new(0.78f, 0.74f, 0.95f);
        public static readonly Color Peach = new(0.99f, 0.82f, 0.66f);
        public static readonly Color Rose = new(0.97f, 0.71f, 0.74f);
        public static readonly Color Amber = new(0.98f, 0.88f, 0.58f);
        public static readonly Color Slate = new(0.62f, 0.66f, 0.74f);

        static GUIStyle s_header;
        static GUIStyle s_badge;
        static GUIStyle s_cardLabel;
        static GUIStyle s_cardValue;
        static GUIStyle s_wrap;

        public static GUIStyle Header => s_header ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            padding = new RectOffset(8, 8, 4, 4)
        };

        public static GUIStyle BadgeStyle => s_badge ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        public static GUIStyle CardLabel => s_cardLabel ??= new GUIStyle(EditorStyles.label)
        {
            wordWrap = false
        };

        public static GUIStyle CardValue => s_cardValue ??= new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        public static GUIStyle Wrap => s_wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true };

        // ── Color mappings ──────────────────────────────

        public static Color ForScore(int score)
        {
            if (score >= 80) return Mint;
            if (score >= 60) return new Color(0.78f, 0.90f, 0.62f); // light green
            if (score >= 40) return Amber;
            if (score >= 20) return Peach;
            return Rose;
        }

        public static Color ForGrade(string grade) => grade switch
        {
            "A" => Mint,
            "B" => new Color(0.78f, 0.90f, 0.62f),
            "C" => Amber,
            "D" => Peach,
            "F" => Rose,
            _ => Slate
        };

        public static Color ForSeverity(HintSeverity severity) => severity switch
        {
            HintSeverity.Blocker => Rose,
            HintSeverity.Warning => Amber,
            _ => Sky
        };

        // ── Drawing helpers ─────────────────────────────

        /// <summary>A tinted section header bar.</summary>
        public static void SectionHeader(string text, Color tint)
        {
            var rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(tint.r, tint.g, tint.b, 0.25f));
            var labelRect = new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height);
            GUI.Label(labelRect, text, Header);
        }

        /// <summary>A coloured pill/badge using a background tint.</summary>
        public static void Badge(string text, Color color, float width = 0f)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (width > 0f) GUILayout.Label(text, BadgeStyle, GUILayout.Width(width));
            else GUILayout.Label(text, BadgeStyle);
            GUI.backgroundColor = prev;
        }

        /// <summary>Label + right-aligned value row with optional tooltip.</summary>
        public static void StatRow(string label, string value, string tooltip = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), CardLabel, GUILayout.Width(170));
            EditorGUILayout.LabelField(value, CardValue);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>A 0-100 score bar with the colour mapped to the value.</summary>
        public static void ScoreBar(int score, string caption)
        {
            var rect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.17f, 1f));

            float pct = Mathf.Clamp01(score / 100f);
            var fill = new Rect(rect.x, rect.y, rect.width * pct, rect.height);
            var c = ForScore(score);
            EditorGUI.DrawRect(fill, new Color(c.r, c.g, c.b, 0.85f));

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, caption, style);
        }

        /// <summary>Tints the most recent layout rect (call right after BeginVertical(helpBox)).</summary>
        public static void TintLastRect(Rect rect, Color color, float alpha = 0.12f)
        {
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, alpha));
        }
    }
}
#endif
