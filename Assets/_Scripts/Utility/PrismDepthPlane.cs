using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of <c>PrismDepthPlane.hlsl</c>: for ONE camera's render, the prism look measures depth as the
    /// distance out of a plane instead of the distance from the lens. The prisms' face spread (faces part as the
    /// prism gets farther away, Docs/PRISM_ANIMATION.md) then reads "on the plane" as near and "far out of it" as
    /// far, which is what a top-down orthographic view of a plane needs: the Kite's navigator board
    /// (Controller/Vessel/Crew/KiteNavigatorView.cs) is the caller.
    ///
    /// <para>Two globals, set in <c>RenderPipelineManager.beginCameraRendering</c> for that camera and zeroed in
    /// its <c>endCameraRendering</c>, so every other camera reads the all-zero default (off) and renders
    /// bit-identically. Globals survive play-mode exit, so they are zeroed on load as well.</para>
    /// </summary>
    public static class PrismDepthPlane
    {
        static readonly int PlaneId = Shader.PropertyToID("_PrismDepthPlane");
        static readonly int ScaleId = Shader.PropertyToID("_PrismDepthPlaneScale");

        /// <summary>The squared distance at which a BlockGraph prism's faces are fully parted (its materials'
        /// <c>_SqrDistance</c>, 100000 on every shipped prism material).</summary>
        public const float FullSpreadSqrDistance = 100000f;

        /// <summary>
        /// Publish the plane through <paramref name="point"/> with normal <paramref name="normal"/>. A prism
        /// <paramref name="fullSpreadAt"/> world units out of it reads as far as the pilot's view's most distant
        /// mass; one on the plane reads as near.
        /// </summary>
        public static void Begin(Vector3 point, Vector3 normal, float fullSpreadAt)
        {
            var n = normal.normalized;
            Shader.SetGlobalVector(PlaneId, new Vector4(n.x, n.y, n.z, Vector3.Dot(n, point)));
            Shader.SetGlobalFloat(ScaleId, Mathf.Sqrt(FullSpreadSqrDistance) / Mathf.Max(0.01f, fullSpreadAt));
        }

        /// <summary>Back to the camera's own distance.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void End()
        {
            Shader.SetGlobalVector(PlaneId, Vector4.zero);
            Shader.SetGlobalFloat(ScaleId, 0f);
        }
    }
}
