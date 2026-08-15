using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The CPU half of the SUPER-SHIELD DEFLECTION JIGGLE (Docs/PRISM_ANIMATION.md §5 C14):
    /// one stamp, per hit, on a super-shielded prism that absorbed damage without being
    /// destroyed. The GPU (<c>PrismJiggleClock</c> in <c>PrismClockAnimation.hlsl</c>) runs
    /// the whole wobble off the shader clock; nothing here touches the prism again until the
    /// scheduled clear.
    ///
    /// Why it exists: super-shielded mass is fully invulnerable, so every hit on it was
    /// visually silent — the impactor's sparks and SFX fired, the mass did not move, and a
    /// deflection read as the shot having missed. This makes the deflection legible.
    ///
    /// What it does NOT do: anything to gameplay. The prism stays invulnerable; collider,
    /// volume, spatial registration, shell state, domain and state flags are untouched. Only
    /// photons move, which is why this is safe to fire from inside an invulnerability gate.
    ///
    /// This is §1 ANIMATION — a pure function of the clock and what was known at the hit — so
    /// it is a per-instance STAMP. It is deliberately NOT the §4.7 global-uniform shape used
    /// by <c>PrismOcclusionCorridor</c> and <c>PrismDestructionSight</c>: those are
    /// view-dependent (they change every frame for every prism as the camera moves), a jiggle
    /// is not.
    /// </summary>
    public static class PrismSuperShieldJiggle
    {
        const string ConfigResourcePath = "PrismSuperShieldJiggleConfig";

        /// Sizing slack on the culling envelope. The shipped HLSL was measured (clang harness,
        /// /asset-surgery §4.5c) to displace a vertex by at most 0.991 x radius x tilt, so 1.25
        /// covers the worst case with headroom. Bounds are gameplay-free — conservative
        /// overdraw is the accepted cost, an under-sized envelope is a prism that vanishes.
        const float BoundsSafety = 1.25f;

        static PrismSuperShieldJiggleConfigSO _config;
        static bool _configResolved;

        static PrismSuperShieldJiggleConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<PrismSuperShieldJiggleConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<PrismSuperShieldJiggleConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>Editor/domain-reload safety: a cached ScriptableObject reference does not
        /// survive a domain reload intact, and a stale one silently disables the effect.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetConfigCache()
        {
            _config = null;
            _configResolved = false;
        }

        /// <summary>
        /// Stamp a deflection wobble onto <paramref name="prism"/>.
        ///
        /// <paramref name="lastStampTime"/> is the caller's own per-prism rate-limit slot,
        /// passed by reference so the state costs one float on the prism instead of a
        /// dictionary keyed by instance id. It is updated in place when a stamp lands.
        ///
        /// Returns true if a stamp landed. A false is normal and silent in every gated case
        /// (disabled, mid-birth, rate-limited, exotic visual owns the renderer); only a
        /// genuinely broken render path or an unwired graph is loud.
        /// </summary>
        public static bool TryStamp(Prism prism, float impactSpeed, ref float lastStampTime)
        {
            if (prism == null || prism.destroyed) return false;

            var config = Config;
            if (config == null || !config.Enabled) return false;

            // Birth rule (Docs/PRISM_ANIMATION.md §4.5b): state applied while a prism is still
            // being created is part of its BIRTH, not a transition on live mass. The grow-in
            // bloom already owns the vertex stage there, and a wobble laid over it is invisible
            // while still costing a stamp.
            if (!prism.IsCreationComplete) return false;

            float now = PrismClock.Now;

            // Spam gate. A swept piercing projectile re-dispatches the same prism every frame it
            // overlaps (Projectile.SweepPrismsAlong keeps no cross-frame hit set), a drone swarm
            // re-queues its meal every behaviour tick, and N concurrent blasts each resolve
            // independently. Without this the envelope restarts before it can visibly move and a
            // prism under sustained fire reads as FROZEN rather than wobbling — the opposite of
            // the intent. Re-stamping is otherwise legal and cheap (interruption = re-stamp).
            float cooldown = config.MinSecondsBetweenStamps;
            if (cooldown > 0f && now - lastStampTime < cooldown) return false;

            // While an exotic visual owns the renderer (the shield engage bloom or the shatter
            // overlay) the companion entity is hidden, so a ONE-SHOT stamp would be spent
            // invisibly. Those morphs are already motion; let them carry the frame.
            if (!prism.UsesEntityColorSink && !prism.TryEnsureRenderEntityForStamp())
                return false;

            float duration = config.Duration;
            var packed = config.PackParams(impactSpeed);
            var p = new float3(packed.x, packed.y, packed.z);

            bool stamped = PrismRenderService.StampJiggle(in prism.RenderHandle, now, duration, in p);

            // One self-heal before screaming: the stamp is a ONE-SHOT write, so a prism that
            // reaches this instant without a companion entity loses its deflection for good.
            // Creation is idempotent and no-ops on the happy path.
            if (!stamped && prism.TryEnsureRenderEntityForStamp())
                stamped = PrismRenderService.StampJiggle(in prism.RenderHandle, now, duration, in p);

            if (!stamped)
            {
                // STRICT MODE: no CPU fallback. The prism simply absorbs the hit without moving,
                // which is exactly the silent deflection this feature exists to fix — so say so.
                PrismClockDiagnostics.WarnNoRenderEntity("superShieldJiggle", prism,
                    prism.DescribeRenderEntityState());
                return false;
            }

            // The stamp landing on the ENTITY proves nothing about the SHADER: if the graph
            // wiring regressed (or was never imported), the per-instance values upload into a
            // property no shader reads and the deflection is silently back to doing nothing.
            // Scream, once per material.
            var sharedMat = prism.TryGetComponent<MeshRenderer>(out var mr) ? mr.sharedMaterial : null;
            if (sharedMat && !sharedMat.HasProperty(JiggleStartTimeId))
                PrismClockDiagnostics.WarnUnwiredMaterial(sharedMat, "_JiggleStartTime", prism);

            lastStampTime = now;
            ExpandCullingEnvelope(prism, config);
            ScheduleSettle(prism, duration);
            return true;
        }

        static readonly int JiggleStartTimeId = Shader.PropertyToID("_JiggleStartTime");

        /// <summary>
        /// The entity matrix never moves during a clock animation, so without this a wobbling
        /// prism frustum-culls against its RESTING box and pops in and out at the screen edge.
        /// Reset-then-expand, because pooled reuse must not compound envelopes run over run.
        ///
        /// The mesh is <c>EffectiveRenderMesh</c>, never the authored box: a super-shielded
        /// prism is drawn as the stellated octahedron, and sizing the envelope off the box
        /// would leave every spike outside it.
        /// </summary>
        static void ExpandCullingEnvelope(Prism prism, PrismSuperShieldJiggleConfigSO config)
        {
            var mesh = prism.EffectiveRenderMesh();
            if (mesh == null) return;

            PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);

            // Rotation is about the prism's object ORIGIN, so the farthest-travelling vertex is
            // the farthest one from that origin, and it moves at most radius x tilt.
            var b = mesh.bounds;
            float radius = b.center.magnitude + b.extents.magnitude;
            float padding = radius * config.MaxTiltRadians * BoundsSafety;
            PrismRenderService.ExpandBoundsForClockAnimation(in prism.RenderHandle, float3.zero, padding);
        }

        /// <summary>
        /// Touchpoint 3: ONE scheduled settle at the analytically-known end. The clear itself is
        /// invisible — the shader's envelope is exactly zero at t = Duration — so this only
        /// stops the GPU evaluating a finished wobble and keeps the envelope from staying
        /// inflated. A re-stamp cancels the pending settle first, so a second hit extends the
        /// wobble instead of being cut short by the first hit's timer.
        /// </summary>
        static void ScheduleSettle(Prism prism, float duration)
        {
            var timers = PrismTimerManager.EnsureInstance();
            if (timers == null) return;

            timers.CancelScheduledActions(prism);
            timers.ScheduleAction(prism, duration, () =>
            {
                if (prism == null) return;
                PrismRenderService.ClearJiggleStamp(in prism.RenderHandle);
                var mesh = prism.EffectiveRenderMesh();
                if (mesh != null)
                    PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);
            });
        }
    }
}
