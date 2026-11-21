namespace CosmicShore.UI
{
    /// <summary>
    /// Fills an authored line's tokens ("{domain} victory"). The sentence belongs to whoever authors the field; code only
    /// supplies the facts. An empty template stays empty - that is how a line is deleted - so there is no fallback.
    /// </summary>
    public static class TemplateText
    {
        /// <summary>Replaces each "{token}" with its value; <paramref name="pairs"/> is token, value, token, value...</summary>
        public static string Fill(string template, params string[] pairs)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                template = template.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            return template;
        }
    }
}
