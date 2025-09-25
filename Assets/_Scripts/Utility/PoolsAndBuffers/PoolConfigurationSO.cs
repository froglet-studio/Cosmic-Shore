// File: Assets/Scripts/Game/PoolConfigurationSO.cs
using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "Pool Configuration", menuName = "CosmicShore/Pool Configuration")]
    public class PoolConfigurationSO : ScriptableObject
    {
        [Header("Pool Identity"), Tooltip("Used as the pool key")]
        public string poolName;         

        [Header("Pool Settings")]
        public GameObject basePrefab;
        public int poolSize = 10;

        [Header("Material Configuration")]
        public MaterialPropertyMapping[] materialProperties;

        [Header("Component Requirements")]
        public ComponentRequirement[] requiredComponents;

        [Header("Custom Setup")]
        [SerializeField] private PoolSetupBehaviour customSetup;

        /// <summary>
        /// Apply visual/material customizations and optional custom setup.
        /// Component add/remove/configuration is handled by the pool manager.
        /// </summary>
        public void ConfigurePoolObject(GameObject obj, SO_MaterialSet materialSet)
        {
            if (obj == null) return;

            // Apply per-instance material overrides via MPB
            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null && materialProperties != null && materialProperties.Length > 0)
            {
                foreach (var mapping in materialProperties)
                {
                    if (mapping == null) continue;
                    mapping.ApplyToRenderer(renderer, materialSet);
                }
            }

            // Custom one-off setup
            if (customSetup != null)
                customSetup.SetupPoolObject(obj, materialSet);
        }
    }

    [Serializable]
    public class MaterialPropertyMapping
    {
        [Header("Source Material")]
        public MaterialSource sourceMaterial;

        [Header("Property Mapping")]
        public PropertyMapping[] propertyMappings;

        public void ApplyToRenderer(Renderer renderer, SO_MaterialSet materialSet)
        {
            if (renderer == null || materialSet == null) return;

            var src = GetSourceMaterial(materialSet);
            if (src == null) return;

            // Use MPB to avoid instantiating unique materials per object
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            if (propertyMappings != null)
            {
                foreach (var m in propertyMappings)
                {
                    if (m == null) continue;
                    m.ApplyProperty(block, src);
                }
            }

            renderer.SetPropertyBlock(block);
        }

        private Material GetSourceMaterial(SO_MaterialSet set)
        {
            return sourceMaterial switch
            {
                MaterialSource.BlockMaterial => set.BlockMaterial,
                MaterialSource.ExplodingBlockMaterial => set.ExplodingBlockMaterial,
                MaterialSource.ShieldedBlockMaterial => set.ShieldedBlockMaterial,
                MaterialSource.SuperShieldedBlockMaterial => set.SuperShieldedBlockMaterial,
                MaterialSource.DangerousBlockMaterial => set.DangerousBlockMaterial,
                MaterialSource.CrystalMaterial => set.CrystalMaterial,
                MaterialSource.ShipMaterial => set.ShipMaterial,
                MaterialSource.AOEExplosionMaterial => set.AOEExplosionMaterial,
                MaterialSource.AOEConicExplosionMaterial => set.AOEConicExplosionMaterial,
                MaterialSource.SpikeMaterial => set.SpikeMaterial,
                MaterialSource.SkimmerMaterial => set.SkimmerMaterial,
                _ => null
            };
        }
    }

    [Serializable]
    public class PropertyMapping
    {
        public string sourcePropertyName;
        public string targetPropertyName;
        public PropertyType propertyType;

        public void ApplyProperty(MaterialPropertyBlock block, Material sourceMaterial)
        {
            if (block == null || sourceMaterial == null) return;
            if (string.IsNullOrEmpty(sourcePropertyName) || string.IsNullOrEmpty(targetPropertyName)) return;

            if (!sourceMaterial.HasProperty(sourcePropertyName)) return;

            switch (propertyType)
            {
                case PropertyType.Color:
                    block.SetColor(targetPropertyName, sourceMaterial.GetColor(sourcePropertyName));
                    break;
                case PropertyType.Float:
                    block.SetFloat(targetPropertyName, sourceMaterial.GetFloat(sourcePropertyName));
                    break;
                case PropertyType.Vector:
                    block.SetVector(targetPropertyName, sourceMaterial.GetVector(sourcePropertyName));
                    break;
                case PropertyType.Texture:
                    block.SetTexture(targetPropertyName, sourceMaterial.GetTexture(sourcePropertyName));
                    break;
            }
        }
    }

    [Serializable]
    public class ComponentRequirement
    {
        [Header("Component Settings")]
        public string componentTypeName;   // e.g. "Namespace.MyBehaviour, AssemblyName" OR "Namespace.MyBehaviour"
        public bool removeIfExists = false;

        [Header("Component Configuration")]
        [SerializeField] private ComponentConfiguration configuration;

        // Cache, not serialized
        [NonSerialized] private Type cachedComponentType;

        public Type GetComponentType()
        {
            if (cachedComponentType != null) return cachedComponentType;

            // Try fast path
            var t = Type.GetType(componentTypeName);
            if (t != null) { cachedComponentType = t; return t; }

            // Fallback: scan assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(componentTypeName);
                if (t != null) { cachedComponentType = t; return t; }
            }
            return null;
        }

        public bool ShouldRemoveExisting() => removeIfExists;
        public ComponentConfiguration GetConfiguration() => configuration;

        public bool IsValid() => GetComponentType() != null;
    }

    public enum MaterialSource
    {
        BlockMaterial,
        ExplodingBlockMaterial,
        ShieldedBlockMaterial,
        SuperShieldedBlockMaterial,
        DangerousBlockMaterial,
        CrystalMaterial,
        ShipMaterial,
        AOEExplosionMaterial,
        AOEConicExplosionMaterial,
        SpikeMaterial,
        SkimmerMaterial
    }

    public enum PropertyType { Color, Float, Vector, Texture }

    /// <summary>Base class for custom component configuration payloads.</summary>
    [Serializable]
    public abstract class ComponentConfiguration
    {
        public abstract void ConfigureComponent(Component component);
    }

    /// <summary>Optional custom pool setup hook invoked after material mapping.</summary>
    public abstract class PoolSetupBehaviour : ScriptableObject
    {
        public abstract void SetupPoolObject(GameObject obj, SO_MaterialSet materialSet);
    }
}
