#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pilot SCENES in the game (Docs/Pilots/PORTRAITS.md §8, "The scenes in the game"; Docs/MENU_REDESIGN_B.md
    /// §92.1): the cut a profile page makes of a scene on any screen shape (<see cref="PilotSceneImage.Frame"/>), and
    /// the SHIPPED assets as <c>Tools/Build/author_pilot_scene_assets.py</c>, <c>author_menu_pilot_profile.py</c> and
    /// <c>author_menu_ark.py</c> author them, read the way Unity reads them.
    ///
    /// <para>The generators check their own text; these check what only the editor can: that a scene loads by its
    /// resource name as a picture of the scene's shape, that a close-up is a sprite, that the page is found where
    /// <see cref="ScreenSwitcher"/> looks for it, and that the Ark has a cell for every callsign in the enum.</para>
    /// </summary>
    [TestFixture]
    public class PilotSceneAssetTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Pilots/PilotSceneCatalog.asset";
        const string ArkViewPath = "Assets/_Prefabs/UI Elements/Main Menu Screens/Ark/ArkView.prefab";
        const string ResourcesRoot = "Assets/Resources/";
        const float SceneAspect = 16f / 9f;
        const float Eps = 1e-4f;

        // The scene kit's own numbers (Tools/Build/pilot_art/scene.py crop_box) for one scene, 1536 x 864 with the
        // close-up at [296, 0, 606, 310] from the top left: the page must make the same crops.
        static readonly Rect KitFace = new(296f / 1536f, 1f - 310f / 864f, 310f / 1536f, 310f / 864f);
        // A pilot who stands right of the middle, with room on both sides of the face.
        static readonly Rect RightFace = new(640f / 1536f, 1f - 550f / 864f, 550f / 1536f, 550f / 864f);

        static PilotSceneCatalogSO LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PilotSceneCatalogSO>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing; run Tools/Build/author_pilot_scene_assets.py");
            return catalog;
        }

        static void AssertRect(Rect expected, Rect actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, Eps, what + " (x)");
            Assert.AreEqual(expected.y, actual.y, Eps, what + " (y)");
            Assert.AreEqual(expected.width, actual.width, Eps, what + " (width)");
            Assert.AreEqual(expected.height, actual.height, Eps, what + " (height)");
        }

        // ---------------------------------------------------------------------------------------------------------
        // The cut
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void Frame_AViewOfTheScenesShape_ShowsTheWholeScene()
        {
            AssertRect(new Rect(0f, 0f, 1f, 1f), PilotSceneImage.Frame(SceneAspect, SceneAspect, KitFace),
                "a 16:9 view shows all of a 16:9 scene");
            AssertRect(new Rect(0f, 0f, 1f, 1f),
                PilotSceneImage.Frame(SceneAspect, SceneAspect, KitFace, new Rect(0f, 0f, 0.3f, 1f), 0.03f),
                "with no scene outside the view there is nowhere to move the cut");
        }

        // view width : height, then the crop's left edge and its width in scene pixels, as the kit recorded them
        // (Docs/Pilots/art/scenes/scenes.json, ENSIGN's crops).
        [TestCase(4f, 3f, 0f, 1152f)]
        [TestCase(1f, 1f, 19f, 864f)]
        [TestCase(4f, 5f, 106f, 691f)]
        [TestCase(9f, 16f, 208f, 486f)]
        public void Frame_ANarrowerView_IsTheSceneKitsCrop(float viewW, float viewH, float left, float width)
        {
            Rect cut = PilotSceneImage.Frame(viewW / viewH, SceneAspect, KitFace);
            // The kit rounds to whole pixels; the page does not, so the two agree to within a pixel.
            Assert.AreEqual(left / 1536f, cut.x, 1f / 1536f, "the crop's left edge");
            Assert.AreEqual(width / 1536f, cut.width, 1f / 1536f, "the crop's width");
            Assert.AreEqual(0f, cut.y, Eps, "a narrower view shows every row");
            Assert.AreEqual(1f, cut.height, Eps, "a narrower view shows every row");
        }

        [Test]
        public void Frame_AWiderView_SetsTheFaceALittleAboveTheMiddle()
        {
            const float view = 21f / 9f;
            var face = new Rect(0.4f, 0.4f, 0.2f, 0.3f);          // room above and below
            Rect cut = PilotSceneImage.Frame(view, SceneAspect, face);
            Assert.AreEqual(SceneAspect / view, cut.height, Eps, "a wider view shows fewer rows");
            Assert.AreEqual(0f, cut.x, Eps);
            Assert.AreEqual(1f, cut.width, Eps, "a wider view shows every column");
            Assert.AreEqual(1f - PilotSceneImage.FaceFromTop, (face.center.y - cut.y) / cut.height, Eps,
                "the face's centre sits FaceFromTop down the view");

            // A face at the scene's top edge cannot sit that low: the cut stops at the top of the scene.
            Rect top = PilotSceneImage.Frame(view, SceneAspect, KitFace);
            Assert.AreEqual(1f, top.yMax, Eps, "the cut never leaves the scene");
        }

        [Test]
        public void Frame_AWiderView_ShowsATallCloseUpWhole()
        {
            const float view = 21f / 9f;
            // A close-up that fits the view, but with more above its centre than FaceFromTop of the view leaves.
            var tall = new Rect(0.4f, 0.3f, 0.3f, 0.62f);
            Rect cut = PilotSceneImage.Frame(view, SceneAspect, tall);
            Assert.Less(tall.height, cut.height, "the test's own premise: the close-up fits the view");
            Assert.Greater(tall.height * 0.5f, cut.height * PilotSceneImage.FaceFromTop,
                "the test's own premise: with its centre FaceFromTop down, the close-up's top is outside the view");
            Assert.AreEqual(tall.yMax, cut.yMax, Eps, "the view moves up just until the close-up's top shows");
            Assert.GreaterOrEqual(tall.yMin, cut.yMin - Eps, "and its bottom still shows");
        }

        [Test]
        public void Frame_WordsBesideTheFace_MoveTheCutUntilTheFaceIsClear()
        {
            const float view = 4f / 3f, margin = 0.03f;
            var words = new Rect(0.025f, 0.1f, 0.29f, 0.52f);     // the panel on the left of a 4:3 view
            Rect plain = PilotSceneImage.Frame(view, SceneAspect, RightFace);
            Rect cut = PilotSceneImage.Frame(view, SceneAspect, RightFace, words, margin);

            float before = (RightFace.xMin - plain.x) / plain.width;
            float after = (RightFace.xMin - cut.x) / cut.width;
            Assert.Less(before, words.xMax + margin, "the test's own premise: the face starts under the words");
            Assert.GreaterOrEqual(after, words.xMax + margin - Eps, "the face clears the words and the margin");
            Assert.LessOrEqual((RightFace.xMax - cut.x) / cut.width, 1f - margin + Eps, "and stays inside the view");
            Assert.AreEqual(words.xMax + margin, after, Eps, "the cut moves no further than it must");
            Assert.GreaterOrEqual(cut.x, 0f);
            Assert.LessOrEqual(cut.xMax, 1f + Eps, "the cut never leaves the scene");
            Assert.AreEqual(plain.width, cut.width, Eps, "moving the cut never changes its size");
        }

        [Test]
        public void Frame_WordsUnderTheFace_DoNotMoveTheCut()
        {
            const float view = 9f / 16f;
            var words = new Rect(0.05f, 0.05f, 0.9f, 0.3f);       // across the bottom of a tall view
            Rect plain = PilotSceneImage.Frame(view, SceneAspect, KitFace);
            Assert.Greater((KitFace.yMin - plain.y) / plain.height, words.yMax,
                "the test's own premise: the face is above the words");
            AssertRect(plain, PilotSceneImage.Frame(view, SceneAspect, KitFace, words, 0.03f),
                "words under the face leave the cut where it was");
        }

        [Test]
        public void Frame_AFaceWiderThanTheClearView_IsCentredInIt()
        {
            const float view = 9f / 16f;
            var face = new Rect(0.4f, 0.3f, 0.3f, 0.3f);
            var words = new Rect(0f, 0f, 0.5f, 1f);               // the left half, the whole height
            Rect cut = PilotSceneImage.Frame(view, SceneAspect, face, words);
            Assert.Greater(face.width / cut.width, 0.5f,
                "the test's own premise: the face is wider than the clear half");
            Assert.AreEqual(0.75f, (face.center.x - cut.x) / cut.width, Eps,
                "the face's centre is the centre of the clear half");
        }

        [Test]
        public void Frame_WithNoFaceOrNoShape_FallsBackToTheMiddle()
        {
            AssertRect(new Rect(0.125f, 0f, 0.75f, 1f), PilotSceneImage.Frame(4f / 3f, SceneAspect, default),
                "a scene with no close-up is cut about its middle");
            AssertRect(new Rect(0f, 0f, 1f, 1f), PilotSceneImage.Frame(0f, SceneAspect, KitFace),
                "a view with no shape yet shows the whole scene");
            AssertRect(new Rect(0f, 0f, 1f, 1f), PilotSceneImage.Frame(SceneAspect, 0f, KitFace),
                "a picture with no shape shows whole");
        }

        // ---------------------------------------------------------------------------------------------------------
        // The shipped scenes
        // ---------------------------------------------------------------------------------------------------------

        [Test]
        public void EveryScene_NamesItsCallsignOnce_WithAPictureACloseUpAndAFace()
        {
            var seen = new HashSet<PilotVision>();
            var scenes = LoadCatalog().Scenes;
            Assert.Greater(scenes.Count, 0, "the catalog holds no scene; run Tools/Build/author_pilot_scene_assets.py");
            foreach (var scene in scenes)
            {
                string who = scene.callsign.ToString();
                Assert.AreNotEqual(PilotVision.None, scene.callsign, "a scene belongs to a callsign");
                Assert.IsTrue(Enum.IsDefined(typeof(PilotVision), scene.callsign), $"{who} is not a callsign");
                Assert.IsTrue(seen.Add(scene.callsign), $"{who} has two scenes");
                Assert.IsFalse(string.IsNullOrWhiteSpace(scene.painted), $"{who}: the painted pilot has no name");
                Assert.IsNotNull(scene.closeUp, $"{who}: the close-up is not a sprite");
                Assert.IsTrue(File.Exists(ResourcesRoot + scene.resource + ".jpg"),
                    $"{who}: no picture at {ResourcesRoot}{scene.resource}.jpg");
                Assert.IsTrue(Enum.IsDefined(typeof(PilotSceneSide), scene.side), $"{who}: an unknown side");

                Rect f = scene.face;
                Assert.IsTrue(f.width > 0f && f.height > 0f, $"{who}: the face has no area");
                Assert.IsTrue(f.xMin >= -Eps && f.yMin >= -Eps && f.xMax <= 1f + Eps && f.yMax <= 1f + Eps,
                    $"{who}: the face {f} leaves the scene");
                // The side says where the pilot stands; the words go to the other one.
                if (scene.side == PilotSceneSide.Left)
                    Assert.Less(f.center.x, 0.5f, $"{who} stands left of the middle");
                if (scene.side == PilotSceneSide.Right)
                    Assert.Greater(f.center.x, 0.5f, $"{who} stands right of the middle");
            }
        }

        [Test]
        public void EveryScene_LoadsByItsResourceName_AsAPictureOfTheScenesShape()
        {
            foreach (var scene in LoadCatalog().Scenes)
            {
                var picture = Resources.Load<Texture2D>(scene.resource);
                Assert.IsNotNull(picture, $"{scene.callsign}: Resources.Load finds no Texture2D at {scene.resource}");
                float aspect = (float)picture.width / picture.height;
                Resources.UnloadAsset(picture);
                // 2%: the browser stand-in is 32 x 18.
                Assert.AreEqual(SceneAspect, aspect, SceneAspect * 0.02f, $"{scene.callsign}: the picture is not 16:9");
            }
        }

        [Test]
        public void TheCatalog_AnswersQuietly_ForACallsignWithNoScene()
        {
            var empty = ScriptableObject.CreateInstance<PilotSceneCatalogSO>();
            try
            {
                Assert.IsFalse(empty.TryGet(PilotVision.Ensign, out var none), "an empty catalog has no scene");
                Assert.IsNull(none);
                Assert.IsFalse(LoadCatalog().TryGet(PilotVision.None, out _), "no callsign, no scene");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // The page and the Ark's tab
        // ---------------------------------------------------------------------------------------------------------

        static PilotProfileModal LoadPage()
        {
            var window = Resources.Load<ModalWindowManager>(ScreenSwitcher.PilotProfileWindowResource);
            Assert.IsNotNull(window, $"no page at Resources/{ScreenSwitcher.PilotProfileWindowResource}; " +
                                     "run Tools/Build/author_menu_pilot_profile.py");
            Assert.IsInstanceOf<PilotProfileModal>(window, "the page's root is not a PilotProfileModal");
            return (PilotProfileModal)window;
        }

        [Test]
        public void ThePage_IsWhereTheMenuLooksForIt_AsThePilotProfileWindow()
        {
            var page = LoadPage();
            Assert.AreEqual(ScreenSwitcher.ModalWindows.PILOT_PROFILE, page.ModalType);
            Assert.IsNotNull(page.GetComponentInChildren<PilotSceneImage>(true), "the page has no scene picture");
            Assert.IsTrue(page.TryGetComponent<CanvasGroup>(out _), "the page fades through its CanvasGroup");

            var so = new SerializedObject(page);
            Assert.AreSame(LoadCatalog(), so.FindProperty("scenes").objectReferenceValue,
                "the page reads the scene catalog");
        }

        [Test]
        public void ThePagesWords_NameOnlyTokensThePageFills()
        {
            var known = new HashSet<string>(PilotProfileModal.Tokens);
            var so = new SerializedObject(LoadPage());
            var field = so.GetIterator();
            int templates = 0;
            while (field.NextVisible(true))
            {
                if (field.propertyType != SerializedPropertyType.String || !field.name.EndsWith("Template")) continue;
                templates++;
                foreach (Match token in Regex.Matches(field.stringValue, @"\{(\w+)\}"))
                    Assert.IsTrue(known.Contains(token.Groups[1].Value),
                        $"{field.name} names {{{token.Groups[1].Value}}}, which PilotProfileModal does not fill");
            }
            Assert.Greater(templates, 0, "the page carries no template: its words would be empty");
        }

        [Test]
        public void TheArk_HasACrewCellForEveryCallsign()
        {
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(ArkViewPath);
            Assert.IsNotNull(view, $"{ArkViewPath} is missing; run Tools/Build/author_menu_ark.py");
            var page = view.GetComponentInChildren<ArkPilotsPage>(true);
            Assert.IsNotNull(page, "the Ark has no Pilots page; run Tools/Build/author_menu_ark.py");

            int callsigns = 0;
            foreach (PilotVision vision in Enum.GetValues(typeof(PilotVision)))
                if (vision != PilotVision.None) callsigns++;

            var so = new SerializedObject(page);
            var cells = so.FindProperty("cells");
            // A crew holds at most one pilot a callsign (Docs/Pilots/INVENTORY.md §3.1), so the tab needs that many.
            Assert.AreEqual(callsigns, cells.arraySize, "one crew cell a callsign");
            for (int i = 0; i < cells.arraySize; i++)
                Assert.IsNotNull(cells.GetArrayElementAtIndex(i).objectReferenceValue, $"crew cell {i} is not wired");
            Assert.AreSame(LoadCatalog(), so.FindProperty("scenes").objectReferenceValue,
                "the tab reads the scene catalog");
        }

        // ---------------------------------------------------------------------------------------------------------
        // The browser build
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Everything under Resources ships in every build. The browser Toybox never opens the Ark, so its copy of
        /// each scene is a stand-in a few pixels wide (author_pilot_scene_assets.py, WEBGL_STAND_IN). The two
        /// decisions hang together: whoever lets the page open in the browser lifts the stand-ins in the same change.
        /// </summary>
        [Test]
        public void WhileTheBrowserKeepsStandIns_ThePageStaysOutOfTheBrowser()
        {
            const int standIn = 64;     // anything this small is no picture to show full screen
            var standIns = new List<string>();
            foreach (var scene in LoadCatalog().Scenes)
            {
                string path = ResourcesRoot + scene.resource + ".jpg";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"{path} is not imported as a texture");
                var web = importer.GetPlatformTextureSettings("WebGL");
                if (web.overridden && web.maxTextureSize <= standIn) standIns.Add(scene.callsign.ToString());
            }

            if (ScreenSwitcher.IsModalInWebToybox(ScreenSwitcher.ModalWindows.PILOT_PROFILE))
                CollectionAssert.IsEmpty(standIns,
                    "the profile page opens in the browser Toybox, but these scenes are stand-ins there: lift " +
                    "WEBGL_STAND_IN in Tools/Build/author_pilot_scene_assets.py");
        }
    }
}
#endif
