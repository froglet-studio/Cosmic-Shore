using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Column-aligned numerals for a face without tabular digits (Docs/STYLE_FOUNDATION.md §4).
    ///
    /// Neither Aldrich nor Chakra Petch has tabular figures, so a live score, timer or balance
    /// reflows every time a "1" becomes an "8". §4's fix is TMP <c>&lt;mspace&gt;</c>, sized to the
    /// face's WIDEST digit, so there is one value per face rather than one global: measured from
    /// the TTFs, Aldrich is <b>0.730 em</b> (its "0") and Chakra Petch SemiBold <b>0.644 em</b> (its
    /// "0") - the two figures task T5 asked for. The value is read from the font asset itself at
    /// runtime (so a re-generated asset can never disagree with it) and cached per asset.
    ///
    /// Only DIGIT runs are wrapped. Punctuation keeps its own advance, which is constant, so a
    /// "00:13:37" timer still never reflows and its colons do not balloon to digit width.
    /// Static numerals that never change after layout do not need this (§4).
    /// </summary>
    public static class TabularText
    {
        /// <summary>Widest digit advance of Aldrich, from ALDRICH-REGULAR.TTF (1495 / 2048 em).</summary>
        public const float AldrichDigitEm = 0.730f;
        /// <summary>Widest digit advance of Chakra Petch SemiBold, from its TTF (644 / 1000 em).</summary>
        public const float ChakraPetchSemiBoldDigitEm = 0.644f;

        private const string Digits = "0123456789";
        private static readonly Dictionary<TMP_FontAsset, float> Cache = new();
        private static readonly StringBuilder Builder = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Cache.Clear();

        /// <summary>
        /// <paramref name="text"/> with every run of digits wrapped in <c>&lt;mspace&gt;</c> sized for
        /// <paramref name="face"/>. A null face falls back to Aldrich's measured advance.
        /// </summary>
        public static string Wrap(string text, TMP_FontAsset face)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string em = DigitAdvanceEm(face).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

            Builder.Clear();
            bool inRun = false;
            foreach (char c in text)
            {
                bool digit = c >= '0' && c <= '9';
                if (digit && !inRun) Builder.Append("<mspace=").Append(em).Append("em>");
                if (!digit && inRun) Builder.Append("</mspace>");
                inRun = digit;
                Builder.Append(c);
            }
            if (inRun) Builder.Append("</mspace>");
            return Builder.ToString();
        }

        /// <summary>The widest of <paramref name="face"/>'s ten digit advances, in em.</summary>
        public static float DigitAdvanceEm(TMP_FontAsset face)
        {
            if (!face) return AldrichDigitEm;
            if (Cache.TryGetValue(face, out float cached)) return cached;

            // A dynamic face (Chakra Petch SemiBold) only knows a glyph once it has been asked for.
            face.HasCharacters(Digits, out _, false, true);

            float widest = 0f;
            float pointSize = face.faceInfo.pointSize;
            foreach (char c in Digits)
                if (face.characterLookupTable != null &&
                    face.characterLookupTable.TryGetValue(c, out var character) && character.glyph != null)
                    widest = Mathf.Max(widest, character.glyph.metrics.horizontalAdvance);

            float em = pointSize > 0f && widest > 0f
                ? widest / pointSize
                : (face.faceInfo.styleName == "SemiBold" ? ChakraPetchSemiBoldDigitEm : AldrichDigitEm);
            Cache[face] = em;
            return em;
        }
    }
}
