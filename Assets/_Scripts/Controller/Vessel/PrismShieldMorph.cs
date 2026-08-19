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

        /// <summary>
        /// Queues the disengage overlay: the shards of the shield just dropped, flying
        /// out along their own face normals — and, when the shield was BROKEN by something,
        /// drifting and tumbling along that impulse (Docs/PRISM_ANIMATION.md §4.8.1).
        /// Fire-and-forget — <see cref="PrismShieldShatter"/> owns the entities and retires
        /// them on a flat time sweep.
        /// </summary>
        /// <param name="breakVelocity">
        /// WORLD-space velocity of the breaking force, ALREADY CLAMPED by the shield
        /// component (which owns the speed cap). Zero is the identity: every disengage with
        /// no direction to speak of — a shield timer expiring, an arena teardown, a domain
        /// change, a herbivore stripping armour — renders the symmetric puff it always did.
        /// </param>
        public static void RequestShatter(GameObject host, MeshRenderer renderer, Mesh sharedShieldMesh,
            float duration, float maxOffset, Vector3 breakVelocity = default)
        {
            if (duration <= 0f || sharedShieldMesh == null || renderer == null) return;

            // The culling envelope wants the drift in OBJECT space. Do it here, where the
            // Transform is already in hand — the batch spawner would otherwise have to
            // invert a matrix per shard to recover what this one call gives it for free.
            Vector3 objectDrift = breakVelocity == Vector3.zero
                ? Vector3.zero
                : host.transform.InverseTransformVector(breakVelocity) * duration;

            bool queued = PrismShieldShatter.TryRequest(sharedShieldMesh, renderer.sharedMaterial,
                host.layer, host.transform.localToWorldMatrix, duration, maxOffset,
                breakVelocity, objectDrift);

            // Strict mode is silent about nothing: the shards ride the instanced path, so
            // if there is none the disengage simply has no overlay and that must be said
            // once. Own reason key — a disengage can happen with no preceding bloom (a
            // birth-engaged shield dropped later), so the engage warning may never fire.
            if (!queued)
                PrismClockDiagnostics.WarnNoRenderEntity("shieldShatter", host,
                    $"batched shield-shatter debris was refused [service: {PrismRenderService.StatusLine()}]");
        }

        /// <summary>
        /// The ONE rule for turning a breaking force into shatter drift, shared by both
        /// shield tiers so they cannot drift apart.
        ///
        /// A shield is broken by whatever impact vector the damage site happened to carry,
        /// and those magnitudes are not comparable: the legacy inertia gain and the
        /// true-velocity (proportional) path differ by orders of magnitude for the same
        /// blow (see PrismExplosion.TriggerExplosion's clamp note, which exists for exactly
        /// this reason). Only the DIRECTION is reliably meaningful, so the magnitude is
        /// clamped to the shield's authored cap — which is therefore the single dial for
        /// how violently a shield comes apart, since the tumble angle rides the same speed.
        ///
        /// Clamped on the CPU, once, so the GPU stays a pure function of what it is handed.
        /// A zero or non-finite vector returns zero: the symmetric puff, never a fabricated
        /// direction.
        /// </summary>
        public static Vector3 ClampBreakVelocity(Vector3 velocity, float speedCap)
        {
            if (speedCap <= 0f) return Vector3.zero;
            float sqr = velocity.sqrMagnitude;
            // Negated finite test: every comparison against NaN is false, so NaN falls out.
            if (!(sqr > 1e-6f) || !(sqr < 1e12f)) return Vector3.zero;

            float speed = Mathf.Sqrt(sqr);
            return speed > speedCap ? velocity * (speedCap / speed) : velocity;
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
