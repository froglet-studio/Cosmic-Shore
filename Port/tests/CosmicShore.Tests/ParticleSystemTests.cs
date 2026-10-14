using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// C2: particle systems simulate and load. The game loop ticks them (they are not
    /// MonoBehaviours): emission over time and bursts, lifetime, the stop action, and the modules a
    /// prefab serializes reach the engine's modules.
    /// </summary>
    public class ParticleSystemTests
    {
        const float Dt = 1f / 60f;

        static ParticleSystem Make(GameLoop loop, bool loopIt = false)
        {
            var go = new GameObject("ps");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = loopIt;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 2f;
            main.playOnAwake = true;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            return ps;
        }

        [Fact]
        public void ABurst_EmitsItsCount_AndTheParticlesDieAtTheirLifetime()
        {
            using var loop = new GameLoop(nameof(ParticleSystemTests));
            var ps = Make(loop);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12f) });
            loop.Tick(Dt);
            Assert.Equal(12, ps.particleCount);
            Assert.True(ps.isPlaying);
            // Moving at startSpeed away from the shape.
            var buffer = new ParticleSystem.RenderParticle[4];
            Assert.Equal(12, ps.GetRenderParticles(ref buffer));
            for (int i = 0; i < 20; i++) loop.Tick(Dt);
            Assert.All(buffer.Take(12), p => Assert.True(p.Velocity.magnitude > 1.9f && p.Velocity.magnitude < 2.1f, $"speed {p.Velocity.magnitude}"));
            for (int i = 0; i < 20; i++) loop.Tick(Dt);
            Assert.Equal(0, ps.particleCount); // 0.5 s lifetime is over
        }

        [Fact]
        public void RateOverTime_EmitsAboutRateTimesSeconds_AndADestroyStopActionRemovesTheObject()
        {
            using var loop = new GameLoop(nameof(ParticleSystemTests));
            var ps = Make(loop);
            var emission = ps.emission;
            emission.rateOverTime = 60f;
            var main = ps.main;
            main.startLifetime = 5f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            for (int i = 0; i < 30; i++) loop.Tick(Dt); // half a second
            Assert.InRange(ps.particleCount, 27, 31);
            var go = ps.gameObject;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            loop.Tick(Dt);
            Assert.True(go == null || go.destroyedFlag, "the Destroy stop action destroys the GameObject");
        }

        [Fact]
        public void APrefabsSerializedModules_ReachTheEngine()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            using var loop = new GameLoop(nameof(ParticleSystemTests));
            var db = ContentYamlTests.Db;
            var loader = new AssetLoader(db, new ScriptTypeMap(db, new[] { typeof(GameObject).Assembly }));
            var scene = new SceneInstantiator(loader, new InstantiateOptions
            {
                IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true,
                Activate = true,
            }).Instantiate(PrefabGraph.Build(db, db.LoadPath("Assets/_Prefabs/Environment/Materials/FX/FX_skimmer_greenblock.prefab")));
            var systems = scene.Roots.SelectMany(r => r.GetComponentsInChildren<ParticleSystem>(true)).ToList();
            Assert.True(systems.Count >= 2, $"{systems.Count} systems");
            // fx_sparks_box: lengthInSec 0.5, looping, maxNumParticles 360, a box-edge shape, startLifetime 0.08.
            var ps = systems.First(p => p.gameObject.name == "fx_sparks_box");
            Assert.Equal(360, ps.main.maxParticles);
            Assert.Equal(0.5f, ps.main.duration);
            Assert.True(ps.main.loop);
            Assert.Equal(ParticleSystemShapeType.BoxEdge, ps.shape.shapeType);
            Assert.Equal(0.08f, ps.main.startLifetime.constant, 3);
            Assert.True(ps.GetComponent<ParticleSystemRenderer>().sharedMaterials.Length >= 1);
        }

        /// <summary>
        /// VFX Graph (approximate): VfxArcLightning's VisualEffect loads its asset name and property
        /// sheet, and while awake draws its two Bezier arcs through ProceduralLines every frame.
        /// </summary>
        [Fact]
        public void TheArcLightningVfx_LoadsItsPropertySheet_AndDrawsTwoArcsAFrame()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            using var loop = new GameLoop(nameof(ParticleSystemTests));
            var db = ContentYamlTests.Db;
            var loader = new AssetLoader(db, new ScriptTypeMap(db, new[] { typeof(GameObject).Assembly, typeof(CosmicShore.Engine.VFX.VisualEffect).Assembly }));
            var scene = new SceneInstantiator(loader, new InstantiateOptions
            {
                IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true,
                Activate = true,
            }).Instantiate(PrefabGraph.Build(db, db.LoadPath("Assets/_Prefabs/Environment/Materials/FX/VfxArcLightning.prefab")));
            var ve = scene.Roots.SelectMany(r => r.GetComponentsInChildren<CosmicShore.Engine.VFX.VisualEffect>(true)).Single();
            Assert.Equal("vfxgraph_arclightning", ve.visualEffectAsset.name);
            Assert.True(ve.HasFloat("Thickness"));
            Assert.Equal(0.1f, ve.GetFloat("Thickness"), 3);
            Assert.Equal(-9.11f, ve.GetVector3("Pos2").x, 2);
            loop.Tick(Dt);
            Assert.Equal(2, ProceduralLines.Current.Count);
            ve.Stop();
            loop.Tick(Dt);
            Assert.Empty(ProceduralLines.Current);
        }
    }
}
