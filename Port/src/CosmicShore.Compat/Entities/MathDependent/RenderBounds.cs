// Needs the Unity.Mathematics shim (float3), which lands separately.
// Compiled only when COSMICSHORE_COMPAT_MATH is defined: once the Mathematics/Collections shims are
// merged, add <DefineConstants>$(DefineConstants);COSMICSHORE_COMPAT_MATH</DefineConstants> to
// CosmicShore.Compat.csproj (or delete this guard). Verified to compile against the standard API shapes.
#if COSMICSHORE_COMPAT_MATH
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace Unity.Rendering
{
    /// <summary>Object-space bounds used for culling.</summary>
    public struct RenderBounds : IComponentData
    {
        public AABB Value;
    }

    /// <summary>World-space bounds (derived by a system in the original; data only here).</summary>
    public struct WorldRenderBounds : IComponentData
    {
        public AABB Value;
    }

    /// <summary>
    /// Adds the component set an entity needs to be drawn by Entities Graphics: filter settings
    /// (+ the mesh array when given), <see cref="MaterialMeshInfo"/>, <see cref="LocalToWorld"/>,
    /// <see cref="RenderBounds"/>, <see cref="WorldRenderBounds"/>, <see cref="PerInstanceCullingTag"/>.
    /// Values are left at their defaults except the ones passed in, as in the original's batch-ID path.
    /// </summary>
    public static class RenderMeshUtility
    {
        public static void AddComponents(Entity entity, EntityManager entityManager, in RenderMeshDescription renderMeshDescription,
                                         MaterialMeshInfo materialMeshInfo = default)
        {
            AddCommon(entity, entityManager, in renderMeshDescription, materialMeshInfo);
        }

        public static void AddComponents(Entity entity, EntityManager entityManager, in RenderMeshDescription renderMeshDescription,
                                         RenderMeshArray renderMeshArray, MaterialMeshInfo materialMeshInfo = default)
        {
            entityManager.AddSharedComponentManaged(entity, renderMeshArray);
            AddCommon(entity, entityManager, in renderMeshDescription, materialMeshInfo);

            var mesh = renderMeshArray.GetMesh(materialMeshInfo);
            if (mesh != null)
            {
                var b = mesh.bounds;
                entityManager.SetComponentData(entity, new RenderBounds
                {
                    Value = new AABB
                    {
                        Center = new float3(b.center.x, b.center.y, b.center.z),
                        Extents = new float3(b.extents.x, b.extents.y, b.extents.z),
                    }
                });
            }
        }

        static void AddCommon(Entity entity, EntityManager em, in RenderMeshDescription desc, MaterialMeshInfo mmi)
        {
            em.AddSharedComponentManaged(entity, desc.FilterSettings);
            em.AddComponentData(entity, mmi);
            em.AddComponent<LocalToWorld>(entity);
            em.AddComponent<RenderBounds>(entity);
            em.AddComponent<WorldRenderBounds>(entity);
            em.AddComponent<PerInstanceCullingTag>(entity);
        }
    }
}
#endif
