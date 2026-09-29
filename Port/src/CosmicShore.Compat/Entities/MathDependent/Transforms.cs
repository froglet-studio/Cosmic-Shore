// Needs the Unity.Mathematics shim (float3 / float4x4 / quaternion), which lands separately.
// Compiled only when COSMICSHORE_COMPAT_MATH is defined: once the Mathematics/Collections shims are
// merged, add <DefineConstants>$(DefineConstants);COSMICSHORE_COMPAT_MATH</DefineConstants> to
// CosmicShore.Compat.csproj (or delete this guard). Verified to compile against the standard API shapes.
#if COSMICSHORE_COMPAT_MATH
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// Local position/rotation/uniform scale (Entities 1.x transform). Data only: the port runs
    /// no TransformSystemGroup, so nothing derives <see cref="LocalToWorld"/> from it — code that
    /// writes <see cref="LocalToWorld"/> directly (PrismRenderService) is unaffected.
    /// </summary>
    public struct LocalTransform : IComponentData
    {
        public float3 Position;
        public float Scale;
        public quaternion Rotation;

        public static readonly LocalTransform Identity = new LocalTransform { Scale = 1f, Rotation = quaternion.identity };

        public static LocalTransform FromPosition(float3 position) => new LocalTransform { Position = position, Scale = 1f, Rotation = quaternion.identity };
        public static LocalTransform FromPosition(float x, float y, float z) => FromPosition(new float3(x, y, z));
        public static LocalTransform FromRotation(quaternion rotation) => new LocalTransform { Scale = 1f, Rotation = rotation };
        public static LocalTransform FromScale(float scale) => new LocalTransform { Scale = scale, Rotation = quaternion.identity };
        public static LocalTransform FromPositionRotation(float3 position, quaternion rotation) => new LocalTransform { Position = position, Scale = 1f, Rotation = rotation };
        public static LocalTransform FromPositionRotationScale(float3 position, quaternion rotation, float scale) => new LocalTransform { Position = position, Scale = scale, Rotation = rotation };

        public LocalTransform WithPosition(float3 position) => new LocalTransform { Position = position, Scale = Scale, Rotation = Rotation };
        public LocalTransform WithRotation(quaternion rotation) => new LocalTransform { Position = Position, Scale = Scale, Rotation = rotation };
        public LocalTransform WithScale(float scale) => new LocalTransform { Position = Position, Scale = scale, Rotation = Rotation };
        public LocalTransform Translate(float3 translation) => WithPosition(Position + translation);

        public float4x4 ToMatrix() => float4x4.TRS(Position, Rotation, new float3(Scale));
    }

    /// <summary>The world matrix Entities Graphics draws with.</summary>
    public struct LocalToWorld : IComponentData
    {
        public float4x4 Value;

        public float3 Right => new float3(Value.c0.x, Value.c0.y, Value.c0.z);
        public float3 Up => new float3(Value.c1.x, Value.c1.y, Value.c1.z);
        public float3 Forward => new float3(Value.c2.x, Value.c2.y, Value.c2.z);
        public float3 Position => new float3(Value.c3.x, Value.c3.y, Value.c3.z);
    }

    /// <summary>A non-uniform scale/shear applied after <see cref="LocalTransform"/>.</summary>
    public struct PostTransformMatrix : IComponentData
    {
        public float4x4 Value;
    }
}
#endif
