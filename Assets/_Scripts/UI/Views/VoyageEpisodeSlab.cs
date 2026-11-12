using CosmicShore.ScriptableObjects;
using UnityEngine;

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
        // The dimmed galaxy: the picture multiplied down, as the board drew it (saturate .25, brightness .9).
        static readonly Color DimAuthored = new(0.5f, 0.52f, 0.6f, 1f);

        protected override void BuildPanel()
        {
            var back = VoyageKit.Node("Back", Panel);
            back.anchorMin = new Vector2(1f, 0f);
            back.anchorMax = new Vector2(1f, 0f);
            back.pivot = new Vector2(1f, 0f);
            back.sizeDelta = new Vector2(340f, 56f);
            back.anchoredPosition = Vector2.zero;
            Kit.Quiet(back, Lines?.SlabEpisodesBackKey, Lines?.SlabEpisodesBack, () => { if (Tabs) Tabs.Show(0); });
        }

        protected override void Draw()
        {
            ShowChips(null, -1, null);
            ShowName(Lines?.SlabEpisodesKicker, Lines?.SlabEpisodesName, Lines?.SlabEpisodesLine, Lines?.SlabEpisodesSoon);
            Picture.color = ColorTrace.Retrace(ColorTrace.Ui, DimAuthored);
            Picture.Zoom = 1.9f;
            Picture.Show(VoyageGalaxyArt.Texture(), new Rect(0f, 0f, 1f, 1f), VoyageGalaxyArt.Aspect);
            ShowFaceWords(Lines?.SlabEpisodesFace);
        }
    }
}
