using CosmicShore.Engine;
using Matrix4x4 = CosmicShore.Engine.Rendering.Matrix4x4;

namespace Unity.Mathematics
{
    // Unity declares these conversions on the UnityEngine side (Vector3 <-> float3 etc.). The
    // port engine's types are owned elsewhere, so the SAME implicit operators are declared here,
    // on the math side — C# resolves a user-defined conversion from either operand type, so call
    // sites (`float3 p = transform.position;`, `transform.position = p;`) compile unchanged.

    public partial struct float2
    {
        public static implicit operator Vector2(float2 v) => new Vector2(v.x, v.y);
        public static implicit operator float2(Vector2 v) => new float2(v.x, v.y);
    }

    public partial struct float3
    {
        public static implicit operator Vector3(float3 v) => new Vector3(v.x, v.y, v.z);
        public static implicit operator float3(Vector3 v) => new float3(v.x, v.y, v.z);
    }

    public partial struct float4
    {
        public static implicit operator Vector4(float4 v) => new Vector4(v.x, v.y, v.z, v.w);
        public static implicit operator float4(Vector4 v) => new float4(v.x, v.y, v.z, v.w);
    }

    public partial struct quaternion
    {
        public static implicit operator Quaternion(quaternion q) => new Quaternion(q.value.x, q.value.y, q.value.z, q.value.w);
        public static implicit operator quaternion(Quaternion q) => new quaternion(q.x, q.y, q.z, q.w);
    }

    public partial struct float4x4
    {
        public static implicit operator Matrix4x4(float4x4 m)
        {
            Matrix4x4 r = default;
            r.m00 = m.c0.x; r.m10 = m.c0.y; r.m20 = m.c0.z; r.m30 = m.c0.w;
            r.m01 = m.c1.x; r.m11 = m.c1.y; r.m21 = m.c1.z; r.m31 = m.c1.w;
            r.m02 = m.c2.x; r.m12 = m.c2.y; r.m22 = m.c2.z; r.m32 = m.c2.w;
            r.m03 = m.c3.x; r.m13 = m.c3.y; r.m23 = m.c3.z; r.m33 = m.c3.w;
            return r;
        }

        public static implicit operator float4x4(Matrix4x4 m) => new float4x4(
            new float4(m.m00, m.m10, m.m20, m.m30),
            new float4(m.m01, m.m11, m.m21, m.m31),
            new float4(m.m02, m.m12, m.m22, m.m32),
            new float4(m.m03, m.m13, m.m23, m.m33));
    }
}
