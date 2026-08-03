using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A display-only model of an ELEMENT's crystal - the element's canonical in-world SHAPE
    /// signature (elements are told apart by shape; colour belongs to domains).
    ///
    /// Like <see cref="VesselModelBuilder"/>, this is a thin filter-free wrapper over
    /// <see cref="ToyModelBuilder"/>: it harvests meshes off the crystal PREFAB ASSET and never
    /// instantiates it, so no <c>Crystal</c> behaviour runs, no fade-in coroutine starts, and no
    /// transparent gameplay material comes along (all three of which an
    /// <c>Instantiate</c>-the-prefab icon drags in).
    /// </summary>
    public static class ElementCrystalModelBuilder
    {
        public static bool TryBuild(Element element, float radius, Material sharedMaterial, out GameObject model)
        {
            model = null;

            var set = ElementalCrystalSetSO.Load();
            var prefab = set ? set.GetPrefab(element) : null;
            var models = prefab ? prefab.CrystalModels : null;
            var source = models is { Count: > 0 } ? models[0]?.model : null;
            if (!source) return false;

            if (!ToyModelBuilder.TryBuild(source.transform, radius, sharedMaterial, out model)) return false;
            model.name = $"Crystal_{element}";
            return true;
        }
    }
}
