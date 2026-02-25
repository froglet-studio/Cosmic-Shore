using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CosmicShore.Game;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Hybrid bridge between MonoBehaviour Prism and ECS companion entity.
    ///
    /// Phase 0 of the ECS migration: each prism GameObject gets a companion entity
    /// with AOESpatial + AOEDamage + AOEManagedRef components. The MonoBehaviour
    /// pushes state changes to the entity; PrismAOERegistry reads from EntityQuery
    /// for batch processing when UseECSQuery is enabled.
    ///
    /// Lifecycle:
    ///   CreateCompanionEntity()  — called after AOE registry registration
    ///   UpdatePosition()         — called when prism position is known (post-spawn)
    ///   UpdateFlags()            — called on shield/destroy state changes
    ///   UpdateDamageData()       — called on domain/volume changes
    ///   DestroyCompanionEntity() — called on pool return or destroy
    /// </summary>
    public class PrismEntityBridge : MonoBehaviour
    {
        /// <summary>
        /// Master toggle for the ECS hybrid path. When false, the bridge is inert
        /// and PrismAOERegistry uses its legacy NativeArray path.
        /// </summary>
        public static bool UseECS { get; set; }

        public Entity CompanionEntity { get; private set; }
        public bool HasEntity { get; private set; }

        private EntityManager _entityManager;
        private static EntityArchetype _prismArchetype;
        private static bool _archetypeInitialized;

        private static EntityArchetype GetOrCreateArchetype(EntityManager em)
        {
            if (!_archetypeInitialized)
            {
                _prismArchetype = em.CreateArchetype(
                    typeof(AOESpatial),
                    typeof(AOEDamage),
                    typeof(AOEManagedRef)
                );
                _archetypeInitialized = true;
            }
            return _prismArchetype;
        }

        /// <summary>
        /// Creates the companion entity with initial spatial, damage, and managed ref data.
        /// Called from Prism after AOE registry registration so the managed index is known.
        /// </summary>
        public void CreateCompanionEntity(
            float3 position,
            byte flags,
            float volume,
            int domain,
            int managedIndex)
        {
            if (!UseECS) return;
            if (HasEntity) return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            _entityManager = world.EntityManager;
            var archetype = GetOrCreateArchetype(_entityManager);
            CompanionEntity = _entityManager.CreateEntity(archetype);

            _entityManager.SetComponentData(CompanionEntity, new AOESpatial
            {
                Position = position,
                Flags = flags
            });

            _entityManager.SetComponentData(CompanionEntity, new AOEDamage
            {
                Volume = volume,
                Domain = domain
            });

            _entityManager.SetComponentData(CompanionEntity, new AOEManagedRef
            {
                ManagedIndex = managedIndex
            });

            HasEntity = true;
        }

        /// <summary>
        /// Destroys the companion entity. Called on pool return or MonoBehaviour destroy.
        /// Safe to call multiple times.
        /// </summary>
        public void DestroyCompanionEntity()
        {
            if (!HasEntity) return;

            if (_entityManager.World != null && _entityManager.World.IsCreated &&
                _entityManager.Exists(CompanionEntity))
            {
                _entityManager.DestroyEntity(CompanionEntity);
            }

            CompanionEntity = Entity.Null;
            HasEntity = false;
        }

        /// <summary>
        /// Updates the spatial position on the companion entity.
        /// Called when the prism's world position needs to sync to ECS.
        /// </summary>
        public void UpdatePosition(float3 position)
        {
            if (!HasEntity) return;
            if (!_entityManager.Exists(CompanionEntity)) return;

            var spatial = _entityManager.GetComponentData<AOESpatial>(CompanionEntity);
            spatial.Position = position;
            _entityManager.SetComponentData(CompanionEntity, spatial);
        }

        /// <summary>
        /// Updates the flags byte on the companion entity.
        /// Called from PrismStateManager on shield/destroy state changes.
        /// </summary>
        public void UpdateFlags(byte flags)
        {
            if (!HasEntity) return;
            if (!_entityManager.Exists(CompanionEntity)) return;

            var spatial = _entityManager.GetComponentData<AOESpatial>(CompanionEntity);
            spatial.Flags = flags;
            _entityManager.SetComponentData(CompanionEntity, spatial);
        }

        /// <summary>
        /// Updates the damage data (volume, domain) on the companion entity.
        /// Called on domain change or volume update after growth completes.
        /// </summary>
        public void UpdateDamageData(float volume, int domain)
        {
            if (!HasEntity) return;
            if (!_entityManager.Exists(CompanionEntity)) return;

            _entityManager.SetComponentData(CompanionEntity, new AOEDamage
            {
                Volume = volume,
                Domain = domain
            });
        }

        /// <summary>
        /// Updates the managed index when the prism's registry slot changes.
        /// </summary>
        public void UpdateManagedIndex(int managedIndex)
        {
            if (!HasEntity) return;
            if (!_entityManager.Exists(CompanionEntity)) return;

            _entityManager.SetComponentData(CompanionEntity, new AOEManagedRef
            {
                ManagedIndex = managedIndex
            });
        }

        /// <summary>
        /// Sets the destroyed flag on the companion entity.
        /// </summary>
        public void MarkDestroyed()
        {
            if (!HasEntity) return;
            if (!_entityManager.Exists(CompanionEntity)) return;

            var spatial = _entityManager.GetComponentData<AOESpatial>(CompanionEntity);
            spatial.Flags |= PrismFlags.Destroyed;
            _entityManager.SetComponentData(CompanionEntity, spatial);
        }

        private void OnDestroy()
        {
            DestroyCompanionEntity();
        }
    }
}
