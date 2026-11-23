namespace CosmicShore.UI
{
    /// <summary>
    /// The WHAT'S NEW window's one rule (Docs/MENU_REDESIGN_B.md section 216): when it opens by itself. Pure, so the
    /// edit-mode tests hold it (<c>WhatsNewRulesTests</c>).
    /// </summary>
    public static class WhatsNewRules
    {
        /// <summary>The device's record of the newest update the player has been shown.</summary>
        public const string SeenPrefKey = "WhatsNew.SeenId";

        /// <summary>
        /// The window opens by itself once per update: when the newest entry's id is not the one last shown, and only on
        /// the menu at rest, never over a guided path (the first-time flow), a flight, another window or the way back
        /// to a launched card.
        /// </summary>
        public static bool ShouldOpenByItself(string newestId, string seenId, bool guided, bool inFreestyle,
            bool windowOpen, bool returningToCard)
        {
            if (string.IsNullOrWhiteSpace(newestId)) return false;
            if (newestId == seenId) return false;
            return !guided && !inFreestyle && !windowOpen && !returningToCard;
        }
    }
}
