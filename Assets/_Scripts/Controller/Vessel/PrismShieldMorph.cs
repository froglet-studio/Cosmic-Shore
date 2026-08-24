using CosmicShore.ECS;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CPU half of the GPU shield morph (Docs/PRISM_ANIMATION.md §5 B4) — shared by
    /// <see cref="PrismOctahedronShield"/> and <see cref="PrismStellatedOctahedronShield"/>
    /// so the two tiers cannot drift apart.
    ///
    /// Everything here is ONE-SHOT by construction: a stamp writes the animation's
    /// initial conditions once and the GPU runs the course off the shader clock
    /// (PrismShieldMorph_float). There is no tick, no progress poll, and no end
    /// callback — the shader clamps at t = 1, which IS the settled shield, so a
    /// completed bloom needs nothing done to it. The stamp is only ever CLEARED, and
    /// only where it must be: at disengage (the entity has gone back to the prism's own
    /// box mesh, which carries no face centroids) and on pool reuse
    /// (Prism.Initialize → PrismRenderService.ClearPrismStamps).
    /// </summary>
    internal static class PrismShieldMorph
    {
        // Directions live on PrismRenderService — the batched shatter spawner writes one
        // of them directly, so a second definition here could drift from the shipped
        // meaning of the property.
        const float BloomDirection = PrismRenderService.ShieldMorphBloom;

        static readonly int StartTimeId = Shader.PropertyToID("_ShieldMorphStartTime");
        static readonly int DurationId = Shader.PropertyToID("_ShieldMorphDuration");
        static readonly int DirectionId = Shader.PropertyToID("_ShieldMorphDirection");
        static readonly int OffsetId = Shader.PropertyToID("_ShieldMorphOffset");

        static MaterialPropertyBlock s_rigBlock;

        /// <summary>
        /// Stamps an engage bloom on the prism's companion entity. The caller must have
        /// already put the entity on the SETTLED shield mesh (SetRenderMeshOverride) and
        /// committed every piece of gameplay state — the morph animates photons only.
        /// </summary>
        public static void StampBloom(Prism prism, MeshRenderer renderer, Object context,
            float duration, string reasonKey)
        {
            Stamp(prism, renderer, context, duration, BloomDirection, 0f, reasonKey);
        }

        static void Stamp(Prism prism, MeshRenderer renderer, Object context,
            float duration, float direction, float offset, string reasonKey)
        {
            if (duration <= 0f) return;
            float now = PrismClock.Now;

            if (prism == null)
            {
                // NO PRISM AT ALL — a standalone shield rig (OctahedronShieldTest.prefab
                // + PrismOctahedronShieldTester), which has no companion entity because
                // it never enters the prism lifecycle. This is NOT the strict-mode
                // fallback §4.4 forbids: that rule governs prisms, and it forbids a CPU
                // ANIMATION tier. This is the same single initial-conditions write on the
                // only property sink a plain MeshRenderer has, after which the GPU runs
                // the identical course. Never reachable from a real prism.
                StampRig(renderer, now, duration, direction, offset);
                return;
            }

            // STRICT MODE (no legacy fallback): stamp, and scream if the visual cannot
            // ride the clock — the shield state itself is already final either way.
            bool stamped = PrismRenderService.StampShieldMorph(
                in prism.RenderHandle, now, duration, direction, offset);

            // One self-heal before screaming: the stamp is a ONE-SHOT write, so a prism
            // that reaches this instant without a companion entity loses its morph for
            // good. Creation is idempotent and no-ops when an entity already exists.
            if (!stamped && prism.TryEnsureRenderEntityForStamp())
                stamped = PrismRenderService.StampShieldMorph(
                    in prism.RenderHandle, now, duration, direction, offset);

            if (!stamped)
                PrismClockDiagnostics.WarnNoRenderEntity(reasonKey, context,
                    prism.DescribeRenderEntityState());
            else if (renderer != null && renderer.sharedMaterial != null &&
                     !renderer.sharedMaterial.HasProperty(DurationId))
                PrismClockDiagnostics.WarnUnwiredMaterial(renderer.sharedMaterial,
                    "_ShieldMorphDuration", context);
        }

        /// <summary>
        /// Settles the morph — REQUIRED before the entity goes back to the prism's own
        /// box mesh, which carries no per-face centroids in TEXCOORD1: a stale stamp
        /// would collapse that box toward the object origin.
        /// </summary>
        public static void Clear(Prism prism, MeshRenderer renderer)
        {
            if (prism != null)
            {
                PrismRenderService.ClearShieldMorphStamp(in prism.RenderHandle);
                return;
            }
            ClearRig(renderer);
        }

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");

        /// <summary>
        /// Queues the disengage overlay: the shield's faces, shed as ORDINARY PRISM
        /// EXPLOSION DEBRIS (Docs/PRISM_ANIMATION.md §4.8.1) — the same entities, stamps,
        /// per-face rotation, erosion and fade a dying prism's pieces get, on the shield's
        /// own mesh. Fire-and-forget — <see cref="PrismShieldShatter"/> owns the entities
        /// and retires them on a flat time sweep.
        /// </summary>
        /// <param name="breakVelocity">
        /// RAW impact vector of whatever broke the shield (Prism.Damage's vector, the
        /// Rhino sword's contact velocity). Clamped by the debris pipeline's own band,
        /// exactly as a prism death clamps it; zero (a shield timer expiring, an arena
        /// teardown, a herbivore stripping armour) degrades to the same up·minSpeed puff
        /// an impactless prism death gets.
        /// </param>
        /// <param name="debrisSpeedLimit">
        /// Per-impact ceiling for TRUE-velocity impacts, forwarded like
        /// <see cref="Prism.Damage"/>'s own; 0 keeps the authored band.
        /// </param>
        public static void RequestShatter(GameObject host, MeshRenderer renderer, Mesh sharedShieldMesh,
            Vector3 breakVelocity = default, float debrisSpeedLimit = 0f,
            Color? shedBright = null, Color? shedDark = null)
        {
            if (sharedShieldMesh == null || renderer == null) return;

            // The shards wear the tier the SHIELD was showing, carried the same way death
            // debris carries a dying prism's tier colors: per-entity overrides on the shared
            // debris material. PrismStateManager passes the pair from the SO_ColorSet tier
            // lookup (the renderer cannot answer it there — the state change has already
            // bound the incoming tier's material by the time the shield disengages); with
            // no pair supplied (the standalone rig, the ContextMenu toggles, where nothing
            // repaints) the renderer's own material is the correct read.
            Color bright, dark;
            if (shedBright.HasValue && shedDark.HasValue)
            {
                bright = shedBright.Value;
                dark = shedDark.Value;
            }
            else
            {
                var material = renderer.sharedMaterial;
                bright = material != null && material.HasProperty(BrightColorId)
                    ? material.GetColor(BrightColorId) : Color.white;
                dark = material != null && material.HasProperty(DarkColorId)
                    ? material.GetColor(DarkColorId) : Color.white;
            }

            bool queued = PrismShieldShatter.TryRequest(sharedShieldMesh, host.layer,
                host.transform, bright, dark, breakVelocity, debrisSpeedLimit);

            // Strict mode is silent about nothing: the shards ride the instanced path, so
            // if there is none the disengage simply has no overlay and that must be said
            // once. Own reason key — a disengage can happen with no preceding bloom (a
            // birth-engaged shield dropped later), so the engage warning may never fire.
            if (!queued)
                PrismClockDiagnostics.WarnNoRenderEntity("shieldShatter", host,
                    $"shield debris was refused [service: {PrismRenderService.StatusLine()}]");
        }

        // -- standalone rig only (see Stamp) ----------------------------------

        static void StampRig(MeshRenderer renderer, float startTime, float duration,
            float direction, float offset)
        {
            if (renderer == null) return;
            s_rigBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(s_rigBlock);
            s_rigBlock.SetFloat(StartTimeId, startTime);
            s_rigBlock.SetFloat(DurationId, duration);
            s_rigBlock.SetFloat(DirectionId, direction);
            s_rigBlock.SetFloat(OffsetId, offset);
            renderer.SetPropertyBlock(s_rigBlock);
        }

        static void ClearRig(MeshRenderer renderer)
        {
            if (renderer == null) return;
            s_rigBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(s_rigBlock);
            s_rigBlock.SetFloat(StartTimeId, 0f);
            s_rigBlock.SetFloat(DurationId, 0f);
            s_rigBlock.SetFloat(DirectionId, BloomDirection);
            s_rigBlock.SetFloat(OffsetId, 0f);
            renderer.SetPropertyBlock(s_rigBlock);
        }
    }
}
