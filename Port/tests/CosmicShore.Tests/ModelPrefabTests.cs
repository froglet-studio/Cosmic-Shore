using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Models;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;
using EngineObject = CosmicShore.Engine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Arc E: FBX model files as nested prefab sources and as asset references, over the REAL
    /// project — the Squirrel nests <c>SquirrelVessel_CosmicShoresTest1.fbx</c> and overrides
    /// its hull renderer, its Animator and a bone; projectiles reference model meshes directly.
    /// </summary>
    public class ModelPrefabTests
    {
        const string SquirrelPrefab = "Assets/_Prefabs/Spacevessels/Squirrel.prefab";
        const string SquirrelModelGuid = "5db69771d02b4c24ca00998adf318137";
        // Squirrel.prefab's PrefabInstance of the model, and the model objects it targets by fileID.
        const long ModelInstanceId = 8410834229753696920;
        const long HullRendererId = 2144674613060260969;   // SkinnedMeshRenderer on //RootNode/root/a_SquirrelShipMesh_nearfinal
        const long HullGameObjectId = -2358995200592801505;
        const long CockpitBoneGameObjectId = -5228419308709608677;
        const long HullMeshId = -8802436679497846652;      // Mesh "a_SquirrelShipMesh_nearfinal"
        const string RedAccentMaterialGuid = "5e4677e2359056046839d737e79d18cc"; // override on m_Materials[0]

        static AssetDatabase Db => ContentYamlTests.Db;

        static AssetLoader EngineOnlyLoader()
            => new(Db, new ScriptTypeMap(Db, new[] { typeof(GameObject).Assembly }));

        static InstantiateOptions EngineOnly(bool activate = true) => new()
        {
            IncludeScript = t => t.Namespace?.StartsWith("CosmicShore.Engine") == true,
            Activate = activate,
        };

        [Fact]
        public void ModelImportGivesUnityIdsAndOneSubmeshPerMaterial()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var model = Db.LoadModel(SquirrelModelGuid);
            Assert.NotNull(model);
            Assert.Same(model, Db.LoadModel(SquirrelModelGuid)); // imported once, cached by guid

            Assert.Equal(919132149155446097, model.Root.GameObjectId);
            Assert.Equal(-8679921383154817045, model.Root.ComponentId("Transform"));
            Assert.Equal(5866666021909216657, model.Root.ComponentId("Animator"));

            var hull = model.FindNode("a_SquirrelShipMesh_nearfinal");
            Assert.True(hull.Skinned);
            Assert.Equal(HullGameObjectId, hull.GameObjectId);
            Assert.Equal(HullRendererId, hull.ComponentId("SkinnedMeshRenderer"));
            Assert.Equal(HullMeshId, hull.Mesh.FileId);
            // The per-polygon material layer (IndexToDirect with no index array) splits the hull
            // into one submesh per FBX material, as the prefab's m_Materials[3] override implies.
            Assert.Equal(new[] { "Domain", "Body", "Window", "Engine" }, hull.Materials);
            Assert.Equal(4, hull.Mesh.Mesh.subMeshCount);
        }

        [Fact]
        public void FbxSourceExpandsIntoTheModelPrefabWithOverrides()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var graph = PrefabGraph.Build(Db, Db.LoadPath(SquirrelPrefab));
            Assert.DoesNotContain(graph.Warnings, w => w.Contains(SquirrelModelGuid));

            var hull = graph.Get(PrefabGraph.Xor(ModelInstanceId, HullRendererId));
            Assert.NotNull(hull);
            Assert.Equal(137, hull.ClassId);
            var mats = hull.Body["m_Materials"].Items;
            Assert.Equal(4, mats.Count);
            Assert.Equal(RedAccentMaterialGuid, ObjRef.From(mats[0]).Guid);          // instance override
            Assert.Equal("923d366546007394ea6c8aaff6a06863", ObjRef.From(mats[2]).Guid); // meta remap "Window"

            var hullGo = graph.Get(PrefabGraph.Xor(ModelInstanceId, HullGameObjectId));
            Assert.Equal("a_SquirrelShipMesh", hullGo.Body.Str("m_Name"));           // m_Name override
            var cockpit = graph.Get(PrefabGraph.Xor(ModelInstanceId, CockpitBoneGameObjectId));
            Assert.Equal(8, cockpit.Body.Int("m_Layer"));                            // m_Layer override

            // The nesting prefab keeps its own root.
            Assert.Equal("Squirrel", graph.FindPrefabRoot().Body.Str("m_Name"));
        }

        [Fact]
        public void SquirrelInstantiatesItsModelHullSkinnedAndAnimated()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            using var loop = new GameLoop("ModelPrefabTests");
            var scene = new SceneInstantiator(EngineOnlyLoader(), EngineOnly())
                .Instantiate(PrefabGraph.Build(Db, Db.LoadPath(SquirrelPrefab)));
            var squirrel = Assert.Single(scene.Roots);

            var skinned = squirrel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.NotEmpty(skinned);
            Assert.All(skinned, r => Assert.True(r.sharedMesh != null && r.sharedMesh.vertexCount > 0, $"{r.name} has no mesh"));

            var hull = (SkinnedMeshRenderer)scene.ById[PrefabGraph.Xor(ModelInstanceId, HullRendererId)];
            Assert.Equal("a_SquirrelShipMesh", hull.gameObject.name);
            Assert.Equal("a_SquirrelShipMesh_nearfinal", hull.sharedMesh.name);
            Assert.Equal(4, hull.sharedMaterials.Length);
            Assert.Equal("RedAccentShipMaterial", hull.sharedMaterials[0].name); // prefab override
            Assert.Equal("ScreenShipMaterial", hull.sharedMaterials[2].name);    // meta remap
            Assert.Equal(42, hull.bones.Length);
            Assert.All(hull.bones, b => Assert.NotNull(b));
            Assert.Equal("a_Squirrel_Armature", hull.rootBone.name);             // m_RootBone override
            Assert.Equal(4, hull.GetBlendShapeCount());

            // The model root carries the Animator the vessel's puppetry drives.
            var modelRoot = hull.transform.parent.gameObject;
            Assert.Equal("SquirrelShip_CosmicShoresTest1", modelRoot.name);
            Assert.NotNull(modelRoot.GetComponent<Animator>());

            // A component the prefab ADDS to a model bone attaches through its stripped alias.
            var cockpit = modelRoot.transform.Find("a_Squirrel_Armature/abone_MAIN_Cockpit");
            Assert.NotNull(cockpit);
            Assert.Equal(8, cockpit.gameObject.layer);
            Assert.NotNull(cockpit.GetComponent<BoxCollider>());
        }

        [Fact]
        public void DirectMeshReferencesIntoModelsResolve()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var loader = EngineOnlyLoader();
            var mesh = loader.Load<Mesh>(new ObjRef(HullMeshId, SquirrelModelGuid, 3));
            Assert.NotNull(mesh);
            Assert.True(mesh.vertexCount > 0);
            Assert.Same(mesh, loader.Load<Mesh>(new ObjRef(HullMeshId, SquirrelModelGuid, 3)));

            // A MeshFilter in a plain prefab: m_Mesh {fileID, guid: <fbx>, type: 3}.
            using var loop = new GameLoop("ModelPrefabTests");
            var scene = new SceneInstantiator(loader, EngineOnly())
                .Instantiate(PrefabGraph.Build(Db, Db.LoadPath("Assets/_Prefabs/Projectile/UrchinSpikeProjectile.prefab")));
            var filter = scene.Roots.SelectMany(r => r.GetComponentsInChildren<MeshFilter>(true)).First();
            Assert.NotNull(filter.sharedMesh);
            Assert.True(filter.sharedMesh.vertexCount > 0);
            Assert.Same(filter.sharedMesh, loader.Load<Mesh>(new ObjRef(-5493726671624683310, "73e4860aaa536f24e9bee9d6accb9b43", 3)));
        }

        [Fact]
        public void ModelMaterialsResolveThroughTheMetaRemap()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            var loader = EngineOnlyLoader();
            var body = loader.Load<Material>(new ObjRef(ModelFileIds.Material("Body"), SquirrelModelGuid, 3));
            Assert.Equal("BlueBaseShipMaterial", body.name);
            // "Engine" has no remap: a default material named after the FBX material.
            var engine = loader.Load<Material>(new ObjRef(ModelFileIds.Material("Engine"), SquirrelModelGuid, 3));
            Assert.NotNull(engine);
            Assert.Equal("Engine", engine.name);
            // Sub-assets the port does not model yet stay null rather than resolving wrongly.
            Assert.Null(loader.Load<Avatar>(new ObjRef(ModelPrefabGraph.AvatarFileId, SquirrelModelGuid, 3)));
        }

        [Fact]
        public void ModelPrefabLoadsAsATemplateAndInstantiates()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            using var loop = new GameLoop("ModelPrefabTests");
            var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });

            // A mesh ref into the model must not materialize the model prefab.
            Assert.NotNull(runtime.Assets.Load(new ObjRef(HullMeshId, SquirrelModelGuid, 3), typeof(EngineObject)));
            Assert.Equal(0, runtime.PrefabTemplateCount);

            var template = runtime.Assets.Load<GameObject>(new ObjRef(ModelFileIds.PrefabAsset, SquirrelModelGuid, 3));
            Assert.NotNull(template);
            Assert.Equal("SquirrelVessel_CosmicShoresTest1", template.name);
            Assert.Equal(1, runtime.PrefabTemplateCount);
            // A GameObject fileID names that node of the same template.
            var hullGo = runtime.Assets.Load<GameObject>(new ObjRef(HullGameObjectId, SquirrelModelGuid, 3));
            Assert.Equal("a_SquirrelShipMesh_nearfinal", hullGo.name);
            Assert.Same(template.transform, hullGo.transform.parent);

            var clone = EngineObject.Instantiate(template);
            Assert.NotNull(clone.GetComponent<Animator>());
            var smr = clone.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Assert.NotNull(smr.sharedMesh);
            Assert.Equal("BlueBaseShipMaterial", smr.sharedMaterials[1].name);
            Assert.Equal(42, smr.bones.Length);
            Assert.True(smr.bones.All(b => b.IsChildOf(clone.transform)), "bones re-map into the clone");
        }

        [Fact]
        public void BuiltinDefaultLine_ResolvesToOneSharedMaterial()
        {
            if (ContentYamlTests.ProjectRoot == null) return;
            using var loop = new GameLoop("ModelPrefabTests");
            var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });
            var line = runtime.Assets.Load<Material>(new ObjRef(10306, AssetLoader.BuiltinExtraGuid, 0));
            Assert.NotNull(line);
            Assert.Equal("Default-Line", line.name);
            Assert.Same(line, runtime.Assets.Load<Material>(new ObjRef(10306, AssetLoader.BuiltinExtraGuid, 0)));
            Assert.Null(runtime.Assets.Load<Material>(new ObjRef(10999, AssetLoader.BuiltinExtraGuid, 0)));
        }
    }
}
