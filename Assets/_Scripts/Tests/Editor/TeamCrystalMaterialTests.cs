#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A domain-owned crystal wears the team clone of its OWN material, looked up by the material, never by its
    /// model's slot index (refactor row F1, Docs/Studios/OMNI_SHEPARD_LAB.md). The base material set's slots are
    /// the omni crystal's, so by index a Mass crystal given a domain would have worn the omni body.
    /// </summary>
    public class TeamCrystalMaterialTests
    {
        const string BaseSetPath = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset";
        const string OmniPrefabPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        static readonly string[] MassPrefabPaths =
        {
            "Assets/_Prefabs/Environment/CrystalMass.prefab",
            "Assets/_Prefabs/Environment/ActiveCrystalMass.prefab",
        };

        [Test]
        public void TeamMaterial_IsKeyedByTheModelsOwnMaterial_NotItsSlot()
        {
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var body = new Material(shader) { name = "body" };
            var prisms = new Material(shader) { name = "prisms" };
            var mass = new Material(shader) { name = "mass prisms" };
            var baseSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            var jade = ScriptableObject.CreateInstance<SO_MaterialSet>();
            var container = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
            try
            {
                baseSet.CrystalMaterial = body;
                baseSet.CrystalMaterial1 = prisms;
                jade.CrystalMaterial = new Material(body);
                jade.CrystalMaterial1 = new Material(prisms);
                container.BaseMaterialSet = baseSet;
                container.TeamMaterialSets = new Dictionary<Domains, SO_MaterialSet> { [Domains.Jade] = jade };

                Assert.AreSame(jade.CrystalMaterial, container.GetTeamCrystalMaterial(Domains.Jade, body));
                Assert.AreSame(jade.CrystalMaterial1, container.GetTeamCrystalMaterial(Domains.Jade, prisms));
                // The Mass crystal's single model sits at slot 0, where the index lookup handed it the omni body.
                Assert.IsNull(container.GetTeamCrystalMaterial(Domains.Jade, mass), "a material the base set does not hold must keep itself");
                Assert.IsNull(container.GetTeamCrystalMaterial(Domains.Jade, null));
            }
            finally
            {
                foreach (var m in new[] { body, prisms, mass, jade.CrystalMaterial, jade.CrystalMaterial1 }) if (m) Object.DestroyImmediate(m);
                Object.DestroyImmediate(baseSet);
                Object.DestroyImmediate(jade);
                Object.DestroyImmediate(container);
            }
        }

        [Test]
        public void ShippedCrystals_OmniModelsHaveTheirSlots_MassModelsKeepTheirOwn()
        {
            var baseSet = AssetDatabase.LoadAssetAtPath<SO_MaterialSet>(BaseSetPath);
            Assert.IsNotNull(baseSet, BaseSetPath);

            var omni = AssetDatabase.LoadAssetAtPath<GameObject>(OmniPrefabPath).GetComponent<Crystal>();
            for (int i = 0; i < omni.CrystalModels.Count; i++)
                Assert.AreEqual(i, ThemeManagerDataContainerSO.CrystalSlotOf(baseSet, omni.CrystalModels[i].defaultMaterial),
                    $"omni model {i} ('{omni.CrystalModels[i].defaultMaterial?.name}') is not in base slot {i}: a team omni would lose its colour there");

            foreach (var path in MassPrefabPaths)
            {
                var mass = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Crystal>();
                foreach (var model in mass.CrystalModels)
                    Assert.AreEqual(-1, ThemeManagerDataContainerSO.CrystalSlotOf(baseSet, model.defaultMaterial),
                        $"{path}: '{model.defaultMaterial?.name}' is in the omni's base set, so a team Mass crystal would swap shaders");
            }
        }
    }
}
#endif
