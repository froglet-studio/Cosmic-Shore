using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Models;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// FBX takes as AnimationClips and blend-tree controllers, over the REAL project: the Manta
    /// family's controller is a Direct tree over two 2D freeform trees whose motions are clips
    /// inside <c>mantis_shapekey_with_animations.fbx</c>.
    /// </summary>
    public class ModelAnimationTests
    {
        const string MantisGuid = "7c324ef5b6621f84b894d9474c05ec3b";
        const string MantaControllerGuid = "f922b4ddeaa211f498ff3a539e6ce230";
        const long CutClip = 2905659382556172114, PitchDownClip = 6212661447819486376;

        static AssetDatabase Db => ContentYamlTests.Db;

        [Fact]
        public void TakeClip_IsCutAtItsFrames_AndMatchesTheStaticPoseItWasExportedFrom()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var model = Db.LoadModel(MantisGuid);
            var cut = FbxAnimationImporter.ImportClip(model, CutClip);
            Assert.NotNull(cut);
            Assert.Equal(1f / 24f, cut.length, 5);   // firstFrame 19 → lastFrame 20 at 24 fps

            const string wing = "Armature.002/root/chassis.001/Winghold.L/Wing.L";
            var node = model.Nodes.Single(n => n.Path.EndsWith("/" + wing));
            var rz = cut.Bindings.Single(b => b.Path == wing && b.Attribute == "m_LocalRotation.z");
            Assert.Equal(node.LocalRotation.z, rz.Curve.Evaluate(0f), 4);

            var down = FbxAnimationImporter.ImportClip(model, PitchDownClip);
            var dz = down.Bindings.Single(b => b.Path == wing && b.Attribute == "m_LocalRotation.z");
            Assert.True(System.Math.Abs(dz.Curve.Evaluate(0f) - node.LocalRotation.z) > 0.05f);
        }

        [Fact]
        public void MantaController_LoadsItsBlendTreesWithModelClips()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var loader = new AssetLoader(Db, new ScriptTypeMap(Db, new[] { typeof(GameObject).Assembly }));
            var controller = loader.Load<RuntimeAnimatorController>(new ObjRef(9100000, MantaControllerGuid, 2)) as AnimatorController;
            Assert.NotNull(controller);
            var state = controller.Layers[0].States.Single();
            Assert.Equal("Blend", state.TimeParameter);
            Assert.Equal(BlendTreeType.Direct, state.Tree.Type);
            Assert.All(state.Tree.Children, c => Assert.Equal(BlendTreeType.FreeformCartesian2D, c.Tree.Type));
            var leaves = state.Tree.Children.SelectMany(c => c.Tree.Children).ToList();
            Assert.Equal(8, leaves.Count);
            Assert.All(leaves, l => Assert.NotNull(l.Clip));
            Assert.Equal(new[] { "Yaw", "Roll" }, state.Tree.Children.Select(c => c.Tree.ParameterX));
        }
    }
}
