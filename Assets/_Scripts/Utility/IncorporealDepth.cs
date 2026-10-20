using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Publishes the incorporeal depth effect's two globals (<c>IncorporealDepth.hlsl</c>) from
    /// <see cref="IncorporealDepthSO"/>. Any system that draws an incorporeal part calls
    /// <see cref="Publish"/> when it starts and on its own cadence; the values are a READ of the
    /// danger prism material, so re-publishing picks up a retune. Two global writes, no allocation.
    /// </summary>
    public static class IncorporealDepth
    {
        static readonly int SpreadId = Shader.PropertyToID("_IncorporealSpread");
        static readonly int FlowId = Shader.PropertyToID("_IncorporealFlow");

        /// <summary>The spread (xyz) and ramp (w) last published; zero spread = the effect is off.</summary>
        public static Vector4 Spread { get; private set; }

        /// <summary>The farthest any axis of an incorporeal part opens, in world units, including the
        /// wave's breathing - what a culling volume around an opened part must allow for.</summary>
        public static float MaxOpening { get; private set; }

        public static void Publish()
        {
            var config = IncorporealDepthSO.Instance;
            Vector4 spread = config ? config.TierSpread() : new Vector4(0f, 0f, 0f, 100000f);
            Vector4 flow = config ? config.Flow : Vector4.zero;
            Spread = spread;
            // The prism rule's full opening beyond rest: spread x (50, 35, 20) - spread, per axis.
            var full = new Vector3(spread.x * 49f, spread.y * 34f, spread.z * 19f);
            MaxOpening = Mathf.Max(full.x, Mathf.Max(full.y, full.z)) * (1f + Mathf.Max(flow.x, 0f));
            Shader.SetGlobalVector(SpreadId, spread);
            Shader.SetGlobalVector(FlowId, flow);
        }

        /// <summary>
        /// The normalised squared distance at which a part STARTS to open: the smallest, over the
        /// three axes, of where the prisms' lerp(-7, far, norm) rises past the resting spread. Below
        /// it every incorporeal part is exactly its authored self. 1 when nothing is published.
        /// </summary>
        public static float OpeningStartNorm(Vector4 spread)
        {
            // Component by component, not by indexer: Amoebius's Vector4 has none.
            float start = 1f;
            start = AxisStart(start, spread.x, 50f);
            start = AxisStart(start, spread.y, 35f);
            start = AxisStart(start, spread.z, 20f);
            return start;
        }

        static float AxisStart(float start, float spread, float gain) =>
            spread > 0f ? Mathf.Min(start, (spread + 7f) / (spread * gain + 7f)) : start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Spread = Vector4.zero;
            MaxOpening = 0f;
        }
    }
}
