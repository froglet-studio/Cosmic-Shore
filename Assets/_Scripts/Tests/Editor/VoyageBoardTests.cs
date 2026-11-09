using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Voyages tab as the board (Rev 81 B5 with the Rev 83 S2 seat editor, Docs/MENU_REDESIGN_B.md section 164): its
    /// style asset is whole, its words are authored, and it builds and shows voyages with no squad book loaded.
    /// </summary>
    public class VoyageBoardTests
    {
        [Test]
        public void StyleAsset_LoadsFromResources_WithEverySpriteAndColumnsInOrder()
        {
            var style = Resources.Load<VoyageBoardStyleSO>(VoyageBoardStyleSO.ResourcePath);
            Assert.IsNotNull(style, "Resources/VoyageBoardStyle is missing (Tools/Build/author_voyage_board.py writes it)");
            Assert.IsNotNull(style.Sliver14Fill);
            Assert.IsNotNull(style.Sliver14Line);
            Assert.IsNotNull(style.Sliver14Line2);
            Assert.IsNotNull(style.Sliver10Fill);
            Assert.IsNotNull(style.Sliver10Line);
            Assert.IsNotNull(style.FadeV);
            Assert.IsNotNull(style.Pool);
            Assert.Less(style.ListRight, style.CourseLeft);
            Assert.Less(style.CourseLeft, style.CourseRight);
            Assert.Less(style.CourseRight, style.PanelLeft);
        }

        [Test]
        public void BoardLines_AreAuthored()
        {
            var lines = VoyageContentSO.Load()?.Lines;
            Assert.IsNotNull(lines);
            foreach (var line in new[]
                     {
                         lines.BoardRowTitle, lines.BoardRowBy, lines.BoardRowMeta, lines.BoardDistanceLabel, lines.BoardSailLabel,
                         lines.BoardIntensityLabel, lines.BoardIntensityValue, lines.BoardSquadLabel, lines.BoardSeatWho,
                         lines.BoardSeatKit, lines.BoardSeatNotYours, lines.BoardEditorTitle, lines.BoardDone,
                     })
                Assert.IsFalse(string.IsNullOrEmpty(line));
        }

        [Test]
        public void Board_ShowsRowsAndTheOpenVoyage_WithTheEditorClosed()
        {
            var host = new GameObject("Page", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(1760f, 780f);
                var lines = new VoyageLines();
                var board = VoyageBoard.Create((RectTransform)host.transform, null, null, null, lines);
                var rows = new List<VoyageBoard.Row>
                {
                    new() { Title = "01  Tollmoor", By = "Brought by Merrin Tal", Meta = "540 ly - 27 min - Intensity 1" },
                    new() { Title = "02  Lantern Reach", By = "Brought by Oriel Vance", Meta = "580 ly - 29 min - Intensity 2" },
                };
                var voyage = new VoyageBoard.Voyage { Title = "Open the vaults at Lantern Reach", Distance = "580 ly", Sail = "29 min", Intensity = "2" };
                board.Show(rows, 1, _ => { }, voyage, null, null);

                var texts = new List<string>();
                foreach (var t in host.GetComponentsInChildren<TMP_Text>(true)) texts.Add(t.text);
                CollectionAssert.Contains(texts, "02  Lantern Reach");
                CollectionAssert.Contains(texts, "Open the vaults at Lantern Reach");
                CollectionAssert.Contains(texts, lines.BoardDistanceLabel);
                CollectionAssert.Contains(texts, lines.CardSetSail);

                var editor = host.transform.Find("VoyageBoard/SeatEditor").GetComponent<CanvasGroup>();
                Assert.IsFalse(editor.blocksRaycasts, "the seat editor opens only on a seat card's press");
                Assert.IsNotNull(board.CourseColumn);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
