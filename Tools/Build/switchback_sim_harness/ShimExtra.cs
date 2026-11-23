// The few UnityEngine types the Switchback code uses that the shared Skim Race shim
// (Tools/Build/skimrace_sim_harness/UnityShim.cs) does not carry.
namespace UnityEngine
{
    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => default;
    }
}
