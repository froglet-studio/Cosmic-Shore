namespace CosmicShore.UI
{
    /// <summary>
    /// The Episodes tab as the Sea table (Rev 90, Docs/MENU_REDESIGN_B.md sections 171-172): the galaxy on the table,
    /// dimmed, the next episode's name over it with "Coming soon", one line on the front face, and one quiet button back
    /// to the Voyages tab. Episodes are locked (Shombith, 2026-11-02: "episodes shown coming soon or locked"), so the
    /// page has no chips and no call to action.
    /// </summary>
    public class VoyageEpisodeSlab : VoyageSlabPage
    {
        protected override void BuildPanel() => BuildBack();

        protected override void Draw() =>
            ShowLocked(Lines?.SlabEpisodesKicker, Lines?.SlabEpisodesName, Lines?.SlabEpisodesLine, Lines?.SlabEpisodesFace, null);
    }
}
