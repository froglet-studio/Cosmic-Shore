using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking.Components;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The game's own opt-in to unreliable transforms reaches the engine: NetworkTransform.UseUnreliableDeltas
    /// loads from the real prefabs (the four fauna set it; the vessels do not), so NetDriver sends exactly
    /// the poses Unity's Netcode would send unreliably (docs/MULTIPLAYER.md §6.6).
    /// </summary>
    public class UnreliableDeltasPrefabTests
    {
        static AssetDatabase Db => ContentYamlTests.Db;

        static NetworkTransform[] Transforms(string prefab)
        {
            var loader = new AssetLoader(Db, new ScriptTypeMap(Db, new[] { typeof(GameObject).Assembly }));
            var scene = new SceneInstantiator(loader, new InstantiateOptions { IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true, Activate = false })
                .Instantiate(PrefabGraph.Build(Db, Db.LoadPath(prefab)));
            return scene.Roots.SelectMany(r => r.GetComponentsInChildren<NetworkTransform>(true)).ToArray();
        }

        [Theory]
        [InlineData("Assets/_Prefabs/FloraAndFauna/QuadFish.prefab")]
        [InlineData("Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab")]
        public void Fauna_ThatOptsIn_LoadsUnreliable(string prefab)
        {
            using var loop = new GameLoop(nameof(Fauna_ThatOptsIn_LoadsUnreliable));
            var nts = Transforms(prefab);
            Assert.NotEmpty(nts);
            Assert.Contains(nts, nt => nt.UseUnreliableDeltas);
        }

        [Fact]
        public void NoVessel_OptsIn()
        {
            // A vessel's NetworkTransform is a game subclass (the engine-only loader skips it), so read the
            // authored value: every vessel prefab that carries the field keeps it at 0 - vessels stay reliable.
            var dir = System.IO.Path.Combine(ContentYamlTests.ProjectRoot, "Assets", "_Prefabs", "Spacevessels");
            var prefabs = System.IO.Directory.GetFiles(dir, "*.prefab");
            Assert.Contains(prefabs, f => System.IO.File.ReadAllText(f).Contains("UseUnreliableDeltas: 0"));
            Assert.DoesNotContain(prefabs, f => System.IO.File.ReadAllText(f).Contains("UseUnreliableDeltas: 1"));
        }
    }
}
