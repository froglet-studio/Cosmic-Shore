using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Voyage window as the Sea table (Rev 89 A and Rev 90, Docs/MENU_REDESIGN_B.md sections 170-172): the tilt's
    /// arithmetic, the style asset, the authored words, and the Voyages board building and showing voyages with no squad
    /// book loaded.
    /// </summary>
    public class VoyageBoardTests
    {
        [Test]
        public void StyleAsset_LoadsFromResources_WithEverySpriteAndColumnsInOrder()
        {
            var style = Resources.Load<VoyageBoardStyleSO>(VoyageBoardStyleSO.ResourcePath);
            Assert.IsNotNull(style, "Resources/VoyageBoardStyle is missing (Tools/Build/author_voyage_board.py writes it)");
            Assert.IsNotNull(style.Sliver10Fill);
            Assert.IsNotNull(style.Sliver10Line);
            Assert.Less(style.ListRight, style.CourseLeft);
            Assert.Less(style.CourseLeft, style.CourseRight);
            Assert.Less(style.CourseRight, style.PanelLeft);
            // The Sea table: the table is the widest of the three columns.
            Assert.Greater(style.CourseRight - style.CourseLeft, 0.5f);
        }

        [Test]
        public void TableLines_AreAuthored()
        {
            var lines = VoyageContentSO.Load()?.Lines;
            Assert.IsNotNull(lines);
            foreach (var line in new[]
                     {
                         lines.BoardRowTitle, lines.BoardRowMeta, lines.BoardSquadLabel, lines.BoardSeatWho, lines.BoardSeatKit,
                         lines.BoardSeatNotYours, lines.BoardKicker, lines.BoardKickerAlone, lines.BoardStats, lines.BoardStepBack,
                         lines.BoardStepOn, lines.SlabChip, lines.SlabSagaPart, lines.SlabSagaKicker, lines.SlabSagaForm,
                         lines.SlabQuestsKicker, lines.SlabQuestsName, lines.SlabQuestsProgress, lines.SlabQuestKicker,
                         lines.SlabQuestClaimed, lines.SlabQuestReady, lines.SlabQuestNext, lines.SlabQuestLocked,
                         lines.SlabQuestClaim, lines.SlabQuestPlay, lines.SlabEpisodesKicker, lines.SlabEpisodesName,
                         lines.SlabEpisodesSoon, lines.SlabEpisodesLine, lines.SlabEpisodesFace, lines.SlabEpisodesBack,
                         lines.SlabSagaLockedKicker, lines.SlabSagaLockedLine, lines.SlabSagaLockedFace,
                     })
                Assert.IsFalse(string.IsNullOrEmpty(line));
        }

        [Test]
        public void Tilt_KeepsTheNearEdge_NarrowsTheFarEdge_AndKeepsLinesStraight()
        {
            var r = new Rect(-510f, -100f, 1020f, 1020f / VoyageSlab.TopAspect);
            // The near edge does not move, and is drawn at full size.
            var nearLeft = VoyageSlab.Warp(r, new Vector2(r.xMin, r.yMin), out float nearScale);
            Assert.AreEqual(r.xMin, nearLeft.x, 1e-3f);
            Assert.AreEqual(r.yMin, nearLeft.y, 1e-3f);
            Assert.AreEqual(1f, nearScale, 1e-4f);
            // The far edge is narrower, lower than the flat picture's top, and still above the near edge.
            var farLeft = VoyageSlab.Warp(r, new Vector2(r.xMin, r.yMax), out float farScale);
            var farRight = VoyageSlab.Warp(r, new Vector2(r.xMax, r.yMax), out _);
            Assert.Less(farScale, 0.8f);
            Assert.Greater(farLeft.x, r.xMin);
            Assert.AreEqual(-farLeft.x, farRight.x, 1e-2f, "the table is symmetric about its middle");
            Assert.Less(farLeft.y, r.yMax);
            Assert.Greater(farLeft.y, r.yMin);
            // The board's table: 1020 wide, its top seen about 416 high.
            Assert.AreEqual(416f, VoyageSlab.SeenHeight(1020f), 6f);
            // A projection keeps a straight line straight: the middle of an edge lies on the line between its ends.
            var mid = VoyageSlab.Warp(r, new Vector2(r.xMin, r.center.y), out _);
            float cross = (farLeft.x - nearLeft.x) * (mid.y - nearLeft.y) - (farLeft.y - nearLeft.y) * (mid.x - nearLeft.x);
            Assert.AreEqual(0f, cross, 0.5f);
        }

        [Test]
        public void Tilt_FadesOnlyTheFarPart()
        {
            var r = new Rect(0f, 0f, 1000f, 600f);
            Assert.AreEqual(1f, VoyageSlab.Fade(r, new Vector2(500f, 0f)), 1e-4f);
            Assert.AreEqual(1f, VoyageSlab.Fade(r, new Vector2(500f, 600f * VoyageSlab.FadeFrom)), 1e-4f);
            Assert.AreEqual(0f, VoyageSlab.Fade(r, new Vector2(500f, 600f)), 1e-4f);
            float half = VoyageSlab.Fade(r, new Vector2(500f, 600f * (VoyageSlab.FadeFrom + VoyageSlab.FadeTo) * 0.5f));
            Assert.AreEqual(0.5f, half, 1e-3f);
        }

        [Test]
        public void Grid_CoversItsRect_WithItsTextureEndToEnd()
        {
            using var vh = new VertexHelper();
            var r = new Rect(10f, 20f, 300f, 200f);
            VoyageSlab.Grid(vh, r, Color.white, new Vector2(0.1f, 0.2f), new Vector2(0.6f, 0.9f), 6, 4);
            Assert.AreEqual(7 * 5, vh.currentVertCount);
            Assert.AreEqual(6 * 4 * 6, vh.currentIndexCount);
            var first = new UIVertex();
            var last = new UIVertex();
            vh.PopulateUIVertex(ref first, 0);
            vh.PopulateUIVertex(ref last, vh.currentVertCount - 1);
            Assert.AreEqual(new Vector2(10f, 20f), (Vector2)first.position);
            Assert.AreEqual(new Vector2(310f, 220f), (Vector2)last.position);
            Assert.AreEqual(0.1f, first.uv0.x, 1e-5f);
            Assert.AreEqual(0.9f, last.uv0.y, 1e-5f);
        }

        [Test]
        public void Picture_IsCroppedToItsRectsShape_AboutItsMiddle()
        {
            var whole = new Rect(0f, 0f, 1f, 1f);
            // A wider rect than the picture: full width, the middle band of its height.
            var wide = VoyageSlabPicture.Crop(whole, 1.5f, 3f, 1f);
            Assert.AreEqual(1f, wide.width, 1e-5f);
            Assert.AreEqual(0.5f, wide.height, 1e-5f);
            Assert.AreEqual(0.25f, wide.y, 1e-5f);
            // Twice as close: half of each, still about the middle.
            var close = VoyageSlabPicture.Crop(whole, 1.5f, 1.5f, 2f);
            Assert.AreEqual(0.5f, close.width, 1e-5f);
            Assert.AreEqual(0.25f, close.x, 1e-5f);
        }

        [Test]
        public void Stepper_GoesRoundTheEnds_AndStartsFromNothing()
        {
            Assert.AreEqual(1, VoyageSquadColumn.StepIndex(0, +1, 3));
            Assert.AreEqual(0, VoyageSquadColumn.StepIndex(2, +1, 3));
            Assert.AreEqual(2, VoyageSquadColumn.StepIndex(0, -1, 3));
            Assert.AreEqual(0, VoyageSquadColumn.StepIndex(-1, +1, 3));
            Assert.AreEqual(2, VoyageSquadColumn.StepIndex(-1, -1, 3));
            Assert.AreEqual(-1, VoyageSquadColumn.StepIndex(0, +1, 0));
        }

        [Test]
        public void Board_ShowsTheVoyagesAndTheOpenVoyagesName_WithNoSquadBook()
        {
            var host = new GameObject("Page", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(1840f, 796f);
                var lines = new VoyageLines();
                var board = VoyageBoard.Create((RectTransform)host.transform, null, null, lines);
                var rows = new List<VoyageBoard.Row>
                {
                    new() { Title = "01  Tollmoor", Meta = "540 ly - 27 min - Intensity 1" },
                    new() { Title = "02  Lantern Reach", Meta = "580 ly - 29 min - Intensity 2" },
                };
                var voyage = new VoyageBoard.Voyage
                {
                    Kicker = "Voyage 02 of 02 - brought by Oriel Vance", Place = "Lantern Reach",
                    Title = "Open the vaults at Lantern Reach", Distance = "580 ly", Sail = "29 min", Intensity = "2",
                };
                board.Show(rows, 1, _ => { }, voyage, null, null);

                var texts = new List<string>();
                foreach (var t in host.GetComponentsInChildren<TMP_Text>(true)) texts.Add(t.text);
                CollectionAssert.Contains(texts, "02  Lantern Reach");
                CollectionAssert.Contains(texts, "Lantern Reach");
                CollectionAssert.Contains(texts, "Open the vaults at Lantern Reach");
                CollectionAssert.Contains(texts, TemplateText.Fill(lines.BoardStats, "distance", "580 ly", "sail", "29 min", "intensity", "2"));
                CollectionAssert.Contains(texts, lines.CardSetSail);

                Assert.IsNull(host.transform.Find("VoyageBoard/SeatEditor"), "a seat changes where it stands: there is no editor screen");
                Assert.IsNotNull(board.CourseColumn);
                Assert.AreEqual(-1, board.Squad.OpenSeat, "no squad, no open seat");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Board_ShapesTheTable_TheFaceUnderTheMap()
        {
            var host = new GameObject("Page", typeof(RectTransform));
            try
            {
                ((RectTransform)host.transform).sizeDelta = new Vector2(1840f, 796f);
                var board = VoyageBoard.Create((RectTransform)host.transform, null, null, new VoyageLines());
                var map = new GameObject("Map", typeof(RectTransform)).GetComponent<RectTransform>();
                var edge = new GameObject("Edge", typeof(RectTransform)).GetComponent<RectTransform>();
                map.SetParent(board.CourseColumn, false);
                edge.SetParent(board.CourseColumn, false);
                board.FitSlab(map, edge);
                float w = board.CourseColumn.rect.width;
                Assert.Greater(w, 900f);
                Assert.AreEqual(w / VoyageSlab.FaceAspect, edge.rect.height, 0.5f);
                Assert.AreEqual(w / VoyageSlab.TopAspect, map.rect.height, 0.5f);
                // The map's near edge stands on the face's top edge.
                Assert.AreEqual(edge.anchoredPosition.y + edge.rect.height, map.anchoredPosition.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
