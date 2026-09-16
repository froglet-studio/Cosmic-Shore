using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A LIVING health prism rides the sway of the limb it is part of
    /// (Docs/ECOSYSTEM.md §47).
    ///
    /// §44 gave every spindle a GPU bend and left the conserved mass bolted to it rigid,
    /// so a Clawfish's fin bent away from the four ribs lying on it and a plant's leaves
    /// stood still while its branches moved. The lockup that reads as ONE creature came
    /// apart the moment the sway became visible.
    ///
    /// THE SWAY IS A PROPERTY OF THE LIMB, NOT OF THE MESH. <c>SpindleSway</c> displaces a
    /// vertex by a pure shear along the limb's own +z, and the offset is a function of z
    /// ALONE — every point at the same height on the limb moves identically, whatever its
    /// x and y. So a prism does not need to know where around the limb it sits; it needs
    /// its own height and the limb's axes. Evaluate the SAME field at the prism's own
    /// vertices and the prism and the limb surface move together exactly. That exactness
    /// is the point: an approximation here reads as the rib sliding on the fin, which is
    /// the defect being fixed.
    ///
    /// EVERYTHING STAMPED IS A CONSTANT OF THE ATTACHMENT, so this is a creation stamp in
    /// the purest sense of Docs/PRISM_ANIMATION.md's clock-material law: a prism does not
    /// move relative to the limb it is part of, so nothing is ever re-computed and there
    /// is no per-frame CPU at any population. Unlike every other stamp on the service
    /// there is no start time and no duration — the sway does not end, it is what being
    /// alive looks like.
    ///
    /// A ZERO SPAN IS AN EXACT NO-OP AND IS THE DEFAULT, which is the feature rather than
    /// just a safe default: a vessel's trail, a cell's authored environment and the
    /// SKELETON a dead lifeform leaves behind (§26) all stay perfectly still, so **living
    /// mass is the mass that moves** and a player can tell a plant that is alive from the
    /// husk of one that is not without being told.
    /// </summary>
    public static class PrismSway
    {
        /// <summary>
        /// Bakes a prism's attachment to its limb and stamps it. Safe to call repeatedly —
        /// every value is a pure function of the two transforms and the limb's material, so
        /// a re-stamp writes the same numbers.
        /// </summary>
        /// <returns>true when the sway landed on the GPU.</returns>
        /// <summary>
        /// The whole of the geometry, as a pure function of two transforms — extracted so the
        /// one thing that can be silently WRONG here is testable without a render service, an
        /// entity world or a play mode.
        ///
        /// `spanX` / `spanY` carry a unit limb-space displacement back into the prism's own
        /// object space (where the vertex shader adds it), premultiplied by the amplitude.
        /// `axis` is ROW 3 of the prism->limb change of basis: the linear FUNCTIONAL giving a
        /// prism-space point's height up the limb. A row rather than a normalized direction,
        /// because under the non-uniform leaf scale a prism routinely carries, a normalized
        /// axis is simply a different — wrong — number. `z0` is the prism ORIGIN's height.
        /// </summary>
        public static void BakeAttachment(Transform limb, Transform prism, float amplitude,
            out float3 spanX, out float3 spanY, out float3 axis, out float z0)
        {
            Matrix4x4 toLimb = limb.worldToLocalMatrix * prism.localToWorldMatrix;
            Matrix4x4 toPrism = prism.worldToLocalMatrix * limb.localToWorldMatrix;

            axis = new float3(toLimb.m20, toLimb.m21, toLimb.m22);
            z0 = toLimb.m23;

            Vector3 x = toPrism.MultiplyVector(Vector3.right) * amplitude;
            Vector3 y = toPrism.MultiplyVector(Vector3.up) * amplitude;
            spanX = new float3(x.x, x.y, x.z);
            spanY = new float3(y.x, y.y, y.z);
        }

        public static bool TryStamp(HealthPrism prism, Spindle limb)
        {
            if (prism == null || prism.destroyed || limb == null) return false;

            // The birth rule (Docs/PRISM_ANIMATION.md §4.5b): a prism that has not finished
            // creating has no companion entity to stamp yet, and the grow bloom already owns
            // its vertex stage. Spindle.Start re-stamps, so this is a deferral, not a loss.
            if (!prism.IsCreationComplete) return false;

            // An exotic visual draws geometry this field was not measured against.
            if (prism.ExoticVisualActive) return false;

            if (!limb.TryGetSwayConstants(out float amplitude, out float frequency, out float phase))
                return false;

            var limbT = limb.SwayFrame;
            if (limbT == null) return false;

            BakeAttachment(limbT, prism.transform, amplitude,
                out float3 spanX, out float3 spanY, out float3 axis, out float z0);
            var timing = new float3(frequency, phase, z0);

            bool stamped = PrismRenderService.StampSway(in prism.RenderHandle, spanX, spanY, axis, timing);
            if (!stamped && prism.TryEnsureRenderEntityForStamp())
                stamped = PrismRenderService.StampSway(in prism.RenderHandle, spanX, spanY, axis, timing);

            if (!stamped)
            {
                PrismClockDiagnostics.WarnNoRenderEntity("prismSway", prism,
                    prism.DescribeRenderEntityState());
                return false;
            }

            // Ground truth: a material whose graph has not been re-imported since the splice
            // would take the stamp silently and render nothing. STRICT mode has no fallback,
            // so the only honest response is to say so, once.
            var sharedMat = prism.TryGetComponent<MeshRenderer>(out var mr) ? mr.sharedMaterial : null;
            if (sharedMat && !sharedMat.HasProperty(SwaySpanXId))
                PrismClockDiagnostics.WarnUnwiredMaterial(sharedMat, "_SwaySpanX", prism);

            ExpandCullingEnvelope(prism, amplitude, z0);
            return true;
        }

        /// <summary>
        /// Stops a prism swaying. This is a STATE CHANGE, not a settle: it is what a
        /// lifeform's death does to the skeleton it leaves behind, so the conserved mass the
        /// food web then grazes is visibly no longer alive.
        /// </summary>
        public static void Clear(Prism prism)
        {
            if (prism == null) return;
            PrismRenderService.ClearSwayStamp(in prism.RenderHandle);
            var mesh = prism.EffectiveRenderMesh();
            if (mesh != null) PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);
        }

        static readonly int SwaySpanXId = Shader.PropertyToID("_SwaySpanX");

        /// The peak of the shared two-wave pair, measured off the SHIPPED HLSL by
        /// Tools/Shaders/verify_prism_sway.py (T8) rather than derived here, because the
        /// secondary weight and ratio live in SpindleSway.hlsl and a copy of them would be
        /// a second source of truth for the same number.
        const float PeakWaveMagnitude = 1.0964f;

        /// A swaying prism leaves its resting box, and the renderer culls against that box.
        /// The sweep is `peak * amplitude * limbHeight` in LIMB units; converting it to the
        /// prism's own object units is what the axis row already encodes, so the padding is
        /// taken in WORLD units off the limb height and the prism's own scale.
        static void ExpandCullingEnvelope(HealthPrism prism, float amplitude, float z0)
        {
            var mesh = prism.EffectiveRenderMesh();
            if (mesh == null) return;
            PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);

            var scale = prism.transform.lossyScale;
            float smallest = Mathf.Max(1e-4f,
                Mathf.Min(Mathf.Abs(scale.x), Mathf.Min(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            // Object-space padding: the world sweep divided by the SMALLEST axis scale, so a
            // thin prism's box still covers the sweep on every axis.
            float padding = PeakWaveMagnitude * Mathf.Abs(amplitude * z0) / smallest;
            PrismRenderService.ExpandBoundsForClockAnimation(in prism.RenderHandle, float3.zero, padding);
        }
    }
}
