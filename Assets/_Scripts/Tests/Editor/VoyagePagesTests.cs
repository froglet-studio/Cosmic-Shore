using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The rules under the three Voyages pages (Frogma Rev 100, 108 and 111; Docs/MENU_REDESIGN_B.md sections 201-204): the
    /// difficulty 1-9 and the pay that follows it, the table's tilt and its inverse, the route drawing itself, the seat
    /// popup's quick kit and Sees groups, and the pages' authored words.
    /// </summary>
    public class VoyagePagesTests
    {
        [Test]
        public void Difficulty_PilotsRecommendTwiceTheIntensity_AndEveryPickMapsBackToACourse()
        {
            Assert.AreEqual(2, VoyageDifficulty.Recommended(1));
            Assert.AreEqual(4, VoyageDifficulty.Recommended(2));
            Assert.AreEqual(6, VoyageDifficulty.Recommended(3));
            Assert.AreEqual(8, VoyageDifficulty.Recommended(4));
            Assert.AreEqual(2, VoyageDifficulty.Recommended(0), "a job with no intensity is an intensity 1 course");
            for (int i = 1; i <= 4; i++)
                Assert.AreEqual(i, VoyageDifficulty.IntensityOf(VoyageDifficulty.Recommended(i)), "the recommendation sails its own course");
            Assert.AreEqual(1, VoyageDifficulty.IntensityOf(1));
            Assert.AreEqual(4, VoyageDifficulty.IntensityOf(9));
            Assert.AreEqual(1, VoyageDifficulty.Clamp(-3));
            Assert.AreEqual(9, VoyageDifficulty.Clamp(12));
        }

        [Test]
        public void Difficulty_PaysWhatTheBoardPricedAtTheRecommendation_AndMoreAboveIt()
        {
            Assert.AreEqual(40, VoyageDifficulty.PayAt(40, 4, 4));
            Assert.Greater(VoyageDifficulty.PayAt(40, 4, 9), 40);
            Assert.Less(VoyageDifficulty.PayAt(40, 4, 1), 40);
            Assert.AreEqual(1, VoyageDifficulty.PayAt(1, 8, 1), "a job that pays pays at least one");
            Assert.AreEqual(0, VoyageDifficulty.PayAt(0, 4, 9));
            int last = 0;
            for (int d = 1; d <= 9; d++)
            {
                int pay = VoyageDifficulty.PayAt(100, 6, d);
                Assert.GreaterOrEqual(pay, last, "pay never falls as the difficulty rises");
                last = pay;
            }
        }

        [Test]
        public void Difficulty_SailsACopy_TheBoardsJobIsUntouched()
        {
            var job = new VoyageJob { TemplateId = "t", Title = "Meet the Gallows crew", Intensity = 2, Pay = 40 };
            job.Warnings.Add("Rivals near");
            var copy = VoyageDifficulty.At(job, 9);
            Assert.AreNotSame(job, copy);
            Assert.AreEqual(4, copy.Intensity);
            Assert.AreEqual(9, copy.Difficulty);
            Assert.AreEqual(VoyageDifficulty.PayAt(40, 4, 9), copy.Pay);
            Assert.AreEqual("Meet the Gallows crew", copy.Title);
            Assert.AreEqual(2, job.Intensity);
            Assert.AreEqual(40, job.Pay);
            Assert.AreEqual(0, job.Difficulty);
            Assert.IsNull(VoyageDifficulty.At(null, 3));
        }

        [Test]
        public void TableTilt_KeepsTheNearEdge_NarrowsTheFar_AndTakesAPressBackToTheFlatMap()
        {
            var r = new Rect(-500f, 0f, 1000f, 900f);
            var near = VoyageTableTilt.Seen(r, new Vector2(r.xMin, r.yMin), VoyageTableTilt.TableAngle, out float nearScale);
            Assert.AreEqual(r.xMin, near.x, 1e-3f);
            Assert.AreEqual(r.yMin, near.y, 1e-3f);
            Assert.AreEqual(1f, nearScale, 1e-4f);
            var far = VoyageTableTilt.Seen(r, new Vector2(r.xMin, r.yMax), VoyageTableTilt.TableAngle, out float farScale);
            Assert.Less(farScale, 1f);
            Assert.Greater(far.x, r.xMin);
            Assert.Less(far.y, r.yMax);
            // Flat is Seen's inverse anywhere on the table.
            foreach (var p in new[] { new Vector2(0f, 0f), new Vector2(300f, 450f), new Vector2(-480f, 880f), new Vector2(120f, 30f) })
            {
                var seen = VoyageTableTilt.Seen(r, p, VoyageTableTilt.TableAngle, out _);
                var back = VoyageTableTilt.Flat(r, seen, VoyageTableTilt.TableAngle);
                Assert.AreEqual(p.x, back.x, 0.05f);
                Assert.AreEqual(p.y, back.y, 0.05f);
            }
            // Flat at 0 degrees is the point itself.
            var flat = VoyageTableTilt.Seen(r, new Vector2(10f, 20f), 0f, out float one);
            Assert.AreEqual(new Vector2(10f, 20f), flat);
            Assert.AreEqual(1f, one, 1e-5f);
        }

        [Test]
        public void TableTilt_FlatHeight_PutsTheFarEdgeAtTheSeenHeight()
        {
            float flat = VoyageTableTilt.FlatHeight(1000f, 420f, VoyageTableTilt.TableAngle);
            Assert.Greater(flat, 420f, "a table lying back is longer than it is seen");
            var r = new Rect(0f, 0f, 1000f, flat);
            var far = VoyageTableTilt.Seen(r, new Vector2(500f, flat), VoyageTableTilt.TableAngle, out _);
            Assert.AreEqual(420f, far.y, 0.5f);
        }

        [Test]
        public void TableTilt_Subdivide_KeepsTheAreaAndCutsLongEdges()
        {
            using var vh = new UnityEngine.UI.VertexHelper();
            vh.AddVert(new Vector3(0f, 0f), Color.white, Vector2.zero);
            vh.AddVert(new Vector3(0f, 200f), Color.white, new Vector2(0f, 1f));
            vh.AddVert(new Vector3(200f, 200f), Color.white, Vector2.one);
            vh.AddTriangle(0, 1, 2);
            VoyageTableTilt.Subdivide(vh, 48f);
            var stream = new List<UIVertex>();
            vh.GetUIVertexStream(stream);
            Assert.Greater(stream.Count, 3);
            float area = 0f;
            for (int i = 0; i + 2 < stream.Count; i += 3)
            {
                Vector2 a = stream[i].position, b = stream[i + 1].position, c = stream[i + 2].position;
                area += Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                Assert.LessOrEqual((a - b).magnitude, 48.01f);
            }
            Assert.AreEqual(200f * 200f * 0.5f, area, 0.5f);
        }

        [Test]
        public void Route_MeasuresItsLength_AndWhereAStopLiesAlongIt()
        {
            var course = new List<Vector2> { new(0f, 0f), new(10f, 0f), new(10f, 10f) };
            Assert.AreEqual(20f, VoyageCardRules.Length(course), 1e-4f);
            Assert.AreEqual(0f, VoyageCardRules.ShareAlong(course, new Vector2(0f, 0f)), 1e-4f);
            Assert.AreEqual(0.5f, VoyageCardRules.ShareAlong(course, new Vector2(10f, 0f)), 1e-4f);
            Assert.AreEqual(1f, VoyageCardRules.ShareAlong(course, new Vector2(10f, 10f)), 1e-4f);
            Assert.AreEqual(0.75f, VoyageCardRules.ShareAlong(course, new Vector2(12f, 5f)), 1e-4f, "a stop off the line takes its nearest point");
        }

        [Test]
        public void SeatPicker_KitsRoundTrip_AndBrokenLinesReadAsNone()
        {
            var kit = new VoyageSeatPicker.Kit
            {
                Vessel = VesselClassType.Manta, PilotId = "pilot.callsign.citadel#7", Element = Element.Time, ArtifactId = null,
            };
            var back = VoyageSeatPicker.Kit.Read(VoyageSeatPicker.Kit.Write(kit));
            Assert.IsTrue(back.HasValue);
            Assert.AreEqual(VesselClassType.Manta, back.Value.Vessel);
            Assert.AreEqual("pilot.callsign.citadel#7", back.Value.PilotId);
            Assert.AreEqual(Element.Time, back.Value.Element);
            Assert.IsNull(back.Value.ArtifactId);
            Assert.IsNull(VoyageSeatPicker.Kit.Read(null));
            Assert.IsNull(VoyageSeatPicker.Kit.Read("not a kit"));
            Assert.IsNull(VoyageSeatPicker.Kit.Read("x|y|z|w"));
        }

        [Test]
        public void SeatPicker_EverySeenThingBelongsToOneSeesGroup()
        {
            foreach (PilotVision v in System.Enum.GetValues(typeof(PilotVision)))
            {
                int group = VoyageSeatPicker.SeesGroup(v);
                if (v == PilotVision.None) Assert.AreEqual(0, group);
                else Assert.That(group, Is.InRange(1, 5), v.ToString());
            }
            Assert.AreEqual(1, VoyageSeatPicker.SeesGroup(PilotVision.Volt));
            Assert.AreEqual(2, VoyageSeatPicker.SeesGroup(PilotVision.Citadel));
            Assert.AreEqual(3, VoyageSeatPicker.SeesGroup(PilotVision.Talon));
            Assert.AreEqual(4, VoyageSeatPicker.SeesGroup(PilotVision.Bandit));
            Assert.AreEqual(5, VoyageSeatPicker.SeesGroup(PilotVision.Flume));
        }

        [Test]
        public void Cover_ShowsThePictureWhole_InsideTheBox()
        {
            // A tall 100 x 300 picture in a 100 x 150 box at the board's face focus (50% 28%): never trimmed.
            var tex = new Texture2D(100, 300);
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, 100f, 300f), new Vector2(0.5f, 0.5f));
            var box = new GameObject("Box", typeof(RectTransform));
            try
            {
                var image = VoyageKit.CoverBox((RectTransform)box.transform);
                VoyageKit.ShowCover(image, sprite, new Vector2(100f, 150f), VoyageKit.FaceFocus);
                var rt = image.rectTransform;
                Assert.IsTrue(image.enabled);
                Assert.AreEqual(150f, rt.sizeDelta.y, 1e-3f, "as tall as the box");
                Assert.AreEqual(50f, rt.sizeDelta.x, 1e-3f, "its own shape kept");
                Assert.AreEqual(25f, rt.anchoredPosition.x, 1e-3f, "the spare width shared out about the middle");
                Assert.AreEqual(0f, rt.anchoredPosition.y, 1e-3f, "no spare height");
                VoyageKit.ShowCover(image, null, new Vector2(100f, 150f), VoyageKit.FaceFocus);
                Assert.IsFalse(image.enabled, "no picture hides it");
            }
            finally
            {
                Object.DestroyImmediate(box);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void PagesLines_AreAuthored()
        {
            var lines = VoyageContentSO.Load()?.Lines;
            Assert.IsNotNull(lines);
            foreach (var line in new[]
                     {
                         lines.StepSea, lines.StepVoyage, lines.StepArk, lines.PageBack, lines.PageBackKey, lines.SeaFullScreen,
                         lines.SeaFullScreenBack, lines.SeaCrew, lines.SeaCrewHide, lines.SeaTierWidth, lines.SeaOpenVoyage,
                         lines.SeaOfferKicker, lines.SeaOfferPay, lines.SeaCrewRecommends, lines.SeaCrewDrawer, lines.BriefKicker, lines.BriefStats,
                         lines.BriefDifficulty, lines.BriefRecommended, lines.BriefRewards, lines.BriefCrystals, lines.BriefCrystalsBy,
                         lines.BriefStanding, lines.BriefStandingBy, lines.BriefRole, lines.BriefStop, lines.BriefReady, lines.ArkLobby,
                         lines.ArkLobbyWith, lines.ArkSquadHead, lines.ArkSquadLine, lines.ArkSquadLineLobby, lines.ArkYou,
                         lines.ArkWingmate, lines.ArkInLobby, lines.ArkFriendLine, lines.ArkArtifact, lines.ArkFlySquad,
                         lines.ArkFlySquadKey, lines.ArkBackToLobby, lines.ArkVoyageLine, lines.ArkSeatAiShort, lines.ArkSquadSeparator,
                         lines.PickTitle, lines.PickTitleSeat,
                         lines.PickKits, lines.PickKitVoyage, lines.PickKitLast, lines.PickKitWho, lines.PickSees, lines.PickSeesAll,
                         lines.PickSeesCrystals, lines.PickSeesPrisms, lines.PickSeesLife, lines.PickSeesVessels, lines.PickSeesPaths,
                         lines.PickCount, lines.PickForVoyage, lines.PickNote, lines.PickClose, lines.KindCourier, lines.KindHunt,
                         lines.KindSalvage, lines.KindDefence, lines.KindSurvey, lines.KindHarvest, lines.KindStudy, lines.KindRescue,
                         lines.KindLiberate, lines.KindBuild, lines.KindLift, lines.KindBounty, lines.KindRite, lines.KindParley,
                         lines.KindRaid, lines.KindTend, lines.KindExhibition, lines.KindConvoy, lines.KindCull, lines.KindHome,
                         lines.KindEscort,
                     })
                Assert.IsFalse(string.IsNullOrEmpty(line));
        }
    }
}
