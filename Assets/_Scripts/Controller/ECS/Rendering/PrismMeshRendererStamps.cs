using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.ECS
{
    /// <summary>
    /// The clock-material stamps written to a prism's <see cref="MeshRenderer"/> instead of its companion
    /// render entity, on a device where Entities Graphics cannot run (Docs/PRISM_ANIMATION.md §4.11).
    ///
    /// <para><b>Why it exists.</b> A WebGL2 browser has no compute shaders, so Entities Graphics is unsupported,
    /// <see cref="PrismRenderService"/> never creates a companion entity and every prism draws through its own
    /// MeshRenderer. Strict mode (§4.4) then had nothing to stamp, so on those devices NOTHING animated: no grow-in,
    /// no colour lerp, no sway, no shield bloom, no flight, no death. The prism graphs declare every clock property
    /// Hybrid Per Instance, and in a variant without <c>DOTS_INSTANCING_ON</c> (the only variant a no-compute device
    /// has) those properties live in the <c>UnityPerMaterial</c> cbuffer, which a MaterialPropertyBlock overrides.
    /// So the SAME initial conditions, written once into a property block, make the GPU run the SAME course off the
    /// same <c>_PrismClock</c>.</para>
    ///
    /// <para><b>The law still holds.</b> One write at the stamp, nothing per frame, one write at the analytic end.
    /// Every method here is a twin of a <c>PrismRenderService.Stamp*</c> / <c>Clear*</c>, writes the same shader
    /// reference names with the same packing (float stays float, a float3 rides a Vector4 with w = 0, a colour goes
    /// through <see cref="PrismRenderService.ApplyColorSpace"/> exactly as the entity write does), and does nothing
    /// unless <see cref="PrismRenderService.UsesMeshRendererStamps"/> — so a device with Entities Graphics behaves
    /// exactly as before, and every other strict-mode case (tool scenes, pre-first-show, a misconfigured config on a
    /// desktop) still screams through <see cref="PrismClockDiagnostics.WarnNoRenderEntity"/>.</para>
    ///
    /// <para><b>Clearing is removal, not a zero write.</b> A property block on a renderer drops it out of the SRP
    /// Batcher, so a settled prism must end with NO block. Unity has no per-property remove, so a clear rebuilds the
    /// block from the families that are still live and sets <c>null</c> when none are. The timed families (grow,
    /// colour, flight, shield morph, jiggle) schedule their own clear at the analytic end through
    /// <see cref="PrismTimerManager"/>, guarded by their stamp time so a re-stamp or a pooled reuse makes a stale
    /// clear a no-op (the <c>PrismSuperShieldJiggle</c> guard shape). Sway is permanent while the lifeform lives and
    /// suction ends in a pool return, so both are cleared by their owners, as on the entity path.</para>
    ///
    /// <para><b>Ownership.</b> On these devices this class owns the prism renderer's block. The one other writer of a
    /// live prism's block, the super-shield mark, is a family here too, so a rebuild keeps it.</para>
    /// </summary>
    public static class PrismMeshRendererStamps
    {
        /// <summary>The stamp families, in the order a rebuild walks them.</summary>
        internal enum Family : byte
        {
            Grow = 0,
            Color = 1,
            Flight = 2,
            ShieldMorph = 3,
            Jiggle = 4,
            Suction = 5,
            Sway = 6,
            SuperShieldMark = 7,
        }

        /// <summary>The fraction of a grow's FINAL size the visual may still be short of when its block is
        /// cleared. 0.5% of the size is below a pixel at any distance a prism is legible at, and it is a
        /// relative bound, so a 60-unit flora leaf and a 0.5-unit trail chip settle equally invisibly.</summary>
        internal const float GrowClearFraction = 0.005f;

        // ── Shader reference names. Each MUST equal the [MaterialProperty] name of the component the
        //    entity stamp writes (PrismRenderProperties.cs) — PrismMeshRendererStampsTests asserts it. ──

        internal const string GrowStartTimeName = "_GrowStartTime";
        internal const string GrowRateName = "_GrowRate";
        internal const string GrowStartFracName = "_GrowStartFrac";
        internal const string ColorStartTimeName = "_ColorStartTime";
        internal const string ColorDurationName = "_ColorDuration";
        internal const string StartBrightColorName = "_StartBrightColor";
        internal const string StartDarkColorName = "_StartDarkColor";
        internal const string StartSpreadName = "_StartSpread";
        internal const string FlightStartTimeName = "_FlightStartTime";
        internal const string FlightDurationName = "_FlightDuration";
        internal const string FlightVelocityName = "_FlightVelocity";
        internal const string ShieldMorphStartTimeName = "_ShieldMorphStartTime";
        internal const string ShieldMorphDurationName = "_ShieldMorphDuration";
        internal const string ShieldMorphDirectionName = "_ShieldMorphDirection";
        internal const string ShieldMorphOffsetName = "_ShieldMorphOffset";
        internal const string JiggleStartTimeName = "_JiggleStartTime";
        internal const string JiggleDurationName = "_JiggleDuration";
        internal const string JiggleParamsName = "_JiggleParams";
        internal const string SuctionStartTimeName = "_SuctionStartTime";
        internal const string SuctionDurationName = "_SuctionDuration";
        internal const string SuctionDirectionName = "_SuctionDirection";
        internal const string SuctionGrowDelayName = "_SuctionGrowDelay";
        internal const string LocationName = "_Location";
        internal const string SwaySpanXName = "_SwaySpanX";
        internal const string SwaySpanYName = "_SwaySpanY";
        internal const string SwayAxisName = "_SwayAxis";
        internal const string SwayTimingName = "_SwayTiming";
        internal const string SuperShieldedName = "_PrismSuperShielded";

        /// <summary>(reference name, is a vector) per family, the first entry being the family's presence key.
        /// Exposed to the tests so a family can never list a name the entity path does not write.</summary>
        internal static readonly (string Name, bool Vector)[][] FamilyProperties =
        {
            new[] { (GrowStartTimeName, false), (GrowRateName, false), (GrowStartFracName, true) },
            new[] { (ColorStartTimeName, false), (ColorDurationName, false), (StartBrightColorName, true),
                    (StartDarkColorName, true), (StartSpreadName, true) },
            new[] { (FlightStartTimeName, false), (FlightDurationName, false), (FlightVelocityName, true) },
            new[] { (ShieldMorphStartTimeName, false), (ShieldMorphDurationName, false),
                    (ShieldMorphDirectionName, false), (ShieldMorphOffsetName, false) },
            new[] { (JiggleStartTimeName, false), (JiggleDurationName, false), (JiggleParamsName, true) },
            new[] { (SuctionStartTimeName, false), (SuctionDurationName, false), (SuctionDirectionName, false),
                    (SuctionGrowDelayName, false), (LocationName, true) },
            new[] { (SwaySpanXName, true), (SwaySpanYName, true), (SwayAxisName, true), (SwayTimingName, true) },
            new[] { (SuperShieldedName, false) },
        };

        static int[][] s_ids;
        static MaterialPropertyBlock s_block;
        static MaterialPropertyBlock s_rebuild;
        static bool s_noted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_noted = false;
            RenderersWithBlock = 0;
        }

        static int[][] Ids
        {
            get
            {
                if (s_ids != null) return s_ids;
                var ids = new int[FamilyProperties.Length][];
                for (int f = 0; f < FamilyProperties.Length; f++)
                {
                    ids[f] = new int[FamilyProperties[f].Length];
                    for (int p = 0; p < FamilyProperties[f].Length; p++)
                        ids[f][p] = Shader.PropertyToID(FamilyProperties[f][p].Name);
                }
                s_ids = ids;
                return ids;
            }
        }

        static int Id(Family family, int index) => Ids[(int)family][index];

        /// <summary>True when stamps on this device go to the MeshRenderer: play mode on a device where
        /// <see cref="PrismRenderService.UsesMeshRendererStamps"/>. Every method below is a no-op returning false (or
        /// doing nothing) when this is false. Edit mode is excluded on purpose: edit-mode tooling has no clock
        /// publisher and must keep settling to the end state, exactly as strict mode always made it.</summary>
        public static bool Active => Application.isPlaying && PrismRenderService.UsesMeshRendererStamps;

        /// <summary>The one line a session gets when the path first engages — the replacement, on these devices,
        /// for the strict-mode scream every stamp site used to raise.</summary>
        internal static void NoteFirstUse()
        {
            if (s_noted) return;
            s_noted = true;
            CSDebug.LogVerbose(CSLogChannel.PrismRuntime,
                "[PrismClock] MeshRenderer stamp path (no Entities Graphics): clock-material stamps go to each " +
                $"prism's MaterialPropertyBlock [service: {PrismRenderService.StatusLine()}]. " +
                "Docs/PRISM_ANIMATION.md §4.11.");
        }

        static MaterialPropertyBlock Begin(Renderer renderer)
        {
            s_block ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(s_block);
            s_beganEmpty = s_block.isEmpty;
            return s_block;
        }

        // Whether the block Begin last read was empty — so Commit knows a renderer just LEFT the SRP Batcher.
        static bool s_beganEmpty;

        static void Commit(Renderer renderer, MaterialPropertyBlock block)
        {
            renderer.SetPropertyBlock(block);
            if (s_beganEmpty) RenderersWithBlock++;
        }

        static void CommitNull(Renderer renderer)
        {
            renderer.SetPropertyBlock(null);
            RenderersWithBlock = Mathf.Max(0, RenderersWithBlock - 1);
        }

        /// <summary>
        /// Live readout: prism renderers currently carrying a stamp block on this path — each one is outside the
        /// SRP Batcher, so this is the number to watch (the <c>prismpath</c> console command prints it). Counted at
        /// the empty↔non-empty transitions this class makes; a prism DESTROYED while stamped is not subtracted, so
        /// treat it as an upper bound across scene changes.
        /// </summary>
        public static int RenderersWithBlock { get; private set; }

        static Vector4 V(in float3 v) => new(v.x, v.y, v.z, 0f);

        // ── Pure value computation (shared with the tests) ───────────────────────────────────────

        /// <summary>
        /// Seconds after a grow stamp at which its block can be cleared without a visible snap: when the largest
        /// per-axis remainder <c>|1 - startFrac|·exp(-rate·t)</c> falls under <see cref="GrowClearFraction"/> of
        /// the final size. 0 means the stamp is already settled and need not be written at all.
        /// </summary>
        internal static float GrowClearDelay(float rate, in float3 startFrac)
        {
            float deviation = math.cmax(math.abs(1f - startFrac));
            if (rate <= 0f || deviation <= GrowClearFraction) return 0f;
            return math.log(deviation / GrowClearFraction) / rate;
        }

        // ── Stamps (twins of PrismRenderService.Stamp*) ──────────────────────────────────────────

        /// <summary>Twin of <see cref="PrismRenderService.StampGrow"/>. Schedules its own clear once the bloom is
        /// within <see cref="GrowClearFraction"/> of final.</summary>
        public static bool StampGrow(Renderer renderer, float startTime, float rate, in float3 startFrac)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            float clearDelay = GrowClearDelay(rate, in startFrac);
            if (clearDelay <= 0f)
            {
                // Already within the clear tolerance: the settled material IS this stamp's whole course.
                Remove(renderer, Family.Grow);
                return true;
            }
            var b = Begin(renderer);
            b.SetFloat(Id(Family.Grow, 0), startTime);
            b.SetFloat(Id(Family.Grow, 1), rate);
            b.SetVector(Id(Family.Grow, 2), V(in startFrac));
            Commit(renderer, b);
            ScheduleClear(renderer, Family.Grow, startTime, clearDelay);
            return true;
        }

        public static void ClearGrowStamp(Renderer renderer) => ClearIfActive(renderer, Family.Grow);

        /// <summary>Twin of <see cref="PrismRenderService.StampColorTransition"/>: start colours in authored
        /// space, converted here exactly as the entity write converts them. Clears itself at the end, where the
        /// shader lerp already equals the bound material.</summary>
        public static bool StampColorTransition(Renderer renderer, float startTime, float duration,
            in float4 startBright, in float4 startDark, in float3 startSpread)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetFloat(Id(Family.Color, 0), startTime);
            b.SetFloat(Id(Family.Color, 1), duration);
            b.SetVector(Id(Family.Color, 2), (Vector4)PrismRenderService.ApplyColorSpace(in startBright));
            b.SetVector(Id(Family.Color, 3), (Vector4)PrismRenderService.ApplyColorSpace(in startDark));
            b.SetVector(Id(Family.Color, 4), V(in startSpread));
            Commit(renderer, b);
            ScheduleClear(renderer, Family.Color, startTime, duration);
            return true;
        }

        public static void ClearColorTransitionStamp(Renderer renderer) => ClearIfActive(renderer, Family.Color);

        /// <summary>Twin of <see cref="PrismRenderService.StampFlight"/>. Clears itself at arrival, where the
        /// shader's offset is already zero; an early stop clears it explicitly.</summary>
        public static bool StampFlight(Renderer renderer, float startTime, float duration, in float3 velocity)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetFloat(Id(Family.Flight, 0), startTime);
            b.SetFloat(Id(Family.Flight, 1), duration);
            b.SetVector(Id(Family.Flight, 2), V(in velocity));
            Commit(renderer, b);
            ScheduleClear(renderer, Family.Flight, startTime, duration);
            return true;
        }

        public static void ClearFlightStamp(Renderer renderer) => ClearIfActive(renderer, Family.Flight);

        /// <summary>Twin of <see cref="PrismRenderService.StampShieldMorph"/>. Clears itself at t = 1, which IS the
        /// authored shield mesh (the shader clamps there).</summary>
        public static bool StampShieldMorph(Renderer renderer, float startTime, float duration,
            float direction, float offset)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetFloat(Id(Family.ShieldMorph, 0), startTime);
            b.SetFloat(Id(Family.ShieldMorph, 1), duration);
            b.SetFloat(Id(Family.ShieldMorph, 2), direction);
            b.SetFloat(Id(Family.ShieldMorph, 3), offset);
            Commit(renderer, b);
            ScheduleClear(renderer, Family.ShieldMorph, startTime, duration);
            return true;
        }

        public static void ClearShieldMorphStamp(Renderer renderer) => ClearIfActive(renderer, Family.ShieldMorph);

        /// <summary>Twin of <see cref="PrismRenderService.StampJiggle"/>. Clears itself at the end, where the
        /// envelope is exactly zero.</summary>
        public static bool StampJiggle(Renderer renderer, float startTime, float duration, in float3 params_)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetFloat(Id(Family.Jiggle, 0), startTime);
            b.SetFloat(Id(Family.Jiggle, 1), duration);
            b.SetVector(Id(Family.Jiggle, 2), V(in params_));
            Commit(renderer, b);
            ScheduleClear(renderer, Family.Jiggle, startTime, duration);
            return true;
        }

        public static void ClearJiggleStamp(Renderer renderer) => ClearIfActive(renderer, Family.Jiggle);

        /// <summary>Twin of <see cref="PrismRenderService.StampSuctionClock"/> on a LIVE prism (the cell-swap
        /// suction). Not self-clearing: the prism ends fully collapsed and its owner returns it to the pool, whose
        /// <see cref="ClearPrismStamps"/> removes it — clearing at the end would pop it back to full size.</summary>
        public static bool StampSuctionClock(Renderer renderer, float startTime, float duration,
            float direction, float growDelay, in float3 location)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetFloat(Id(Family.Suction, 0), startTime);
            b.SetFloat(Id(Family.Suction, 1), duration);
            b.SetFloat(Id(Family.Suction, 2), direction);
            b.SetFloat(Id(Family.Suction, 3), growDelay);
            b.SetVector(Id(Family.Suction, 4), V(in location));
            Commit(renderer, b);
            return true;
        }

        public static void ClearSuctionClockStamp(Renderer renderer) => ClearIfActive(renderer, Family.Suction);

        /// <summary>Twin of <see cref="PrismRenderService.StampSway"/>. Permanent while the lifeform lives;
        /// <see cref="ClearSwayStamp"/> is its death (and pool hygiene).</summary>
        public static bool StampSway(Renderer renderer, in float3 spanX, in float3 spanY, in float3 axis,
            in float3 timing)
        {
            if (!Active || renderer == null) return false;
            NoteFirstUse();
            var b = Begin(renderer);
            b.SetVector(Id(Family.Sway, 0), V(in spanX));
            b.SetVector(Id(Family.Sway, 1), V(in spanY));
            b.SetVector(Id(Family.Sway, 2), V(in axis));
            b.SetVector(Id(Family.Sway, 3), V(in timing));
            Commit(renderer, b);
            return true;
        }

        public static void ClearSwayStamp(Renderer renderer) => ClearIfActive(renderer, Family.Sway);

        /// <summary>The super-shield MARK on this path: present while marked, REMOVED (not written 0) when not,
        /// so an unmarked prism carries no block. False when the path is inactive — the caller then keeps its
        /// own MeshRenderer write.</summary>
        public static bool SetSuperShieldMark(Renderer renderer, bool superShielded)
        {
            if (!Active || renderer == null) return false;
            if (!superShielded)
            {
                Remove(renderer, Family.SuperShieldMark);
                return true;
            }
            var b = Begin(renderer);
            b.SetFloat(Id(Family.SuperShieldMark, 0), 1f);
            Commit(renderer, b);
            return true;
        }

        /// <summary>Twin of <see cref="PrismRenderService.ClearPrismStamps"/> (pool reuse): every animation family
        /// goes, the super-shield mark stays (its owner re-asserts it), and the culling envelope returns to the
        /// mesh.</summary>
        public static void ClearPrismStamps(Renderer renderer)
        {
            if (!Active || renderer == null) return;
            var b = Begin(renderer);
            if (b.isEmpty) return;
            s_rebuild ??= new MaterialPropertyBlock();
            bool any = RebuildKeeping(b, s_rebuild, Family.SuperShieldMark);
            if (any) renderer.SetPropertyBlock(s_rebuild);
            else CommitNull(renderer);
            renderer.ResetLocalBounds();
        }

        // ── Effect carriers (debris, the pooled Grow) ────────────────────────────────────────────
        // A debris carrier OWNS its whole block (it is cleared on retirement), so these write straight into a
        // caller's block instead of rebuilding a renderer's. Same names and packing as the entity spawns
        // (PrismRenderService.SpawnExplosionDebrisBatch / SpawnImplosionDebrisBatch).

        internal const string BrightColorName = "_BrightColor";
        internal const string DarkColorName = "_DarkColor";
        internal const string ExplodeStartTimeName = "_ExplodeStartTime";
        internal const string ExplodeSpeedName = "_ExplodeSpeed";
        internal const string ExplodeDurationName = "_ExplodeDuration";
        internal const string VelocityName = "_Velocity";
        internal const string FacePivotFromCentroidName = "_FacePivotFromCentroid";
        internal const string ImplosionStateName = "_State";

        static readonly int BrightColorId = Shader.PropertyToID(BrightColorName);
        static readonly int DarkColorId = Shader.PropertyToID(DarkColorName);
        static readonly int ExplodeStartTimeId = Shader.PropertyToID(ExplodeStartTimeName);
        static readonly int ExplodeSpeedId = Shader.PropertyToID(ExplodeSpeedName);
        static readonly int ExplodeDurationId = Shader.PropertyToID(ExplodeDurationName);
        static readonly int VelocityId = Shader.PropertyToID(VelocityName);
        static readonly int FacePivotFromCentroidId = Shader.PropertyToID(FacePivotFromCentroidName);
        static readonly int ImplosionStateId = Shader.PropertyToID(ImplosionStateName);

        /// <summary>Team colours, converted as <see cref="PrismRenderService.SetTeamColors"/> converts them.</summary>
        internal static void WriteTeamColors(MaterialPropertyBlock block, in float4 bright, in float4 dark)
        {
            block.SetVector(BrightColorId, (Vector4)PrismRenderService.ApplyColorSpace(in bright));
            block.SetVector(DarkColorId, (Vector4)PrismRenderService.ApplyColorSpace(in dark));
        }

        /// <summary>The explosion clock, exactly as <c>SpawnExplosionDebrisBatch</c> stamps a debris entity.</summary>
        internal static void WriteExplosionClock(MaterialPropertyBlock block, float startTime, float speed,
            float duration, in float3 velocity, float facePivotFromCentroid)
        {
            block.SetFloat(ExplodeStartTimeId, startTime);
            block.SetFloat(ExplodeSpeedId, speed);
            block.SetFloat(ExplodeDurationId, duration);
            block.SetVector(VelocityId, V(in velocity));
            block.SetFloat(FacePivotFromCentroidId, facePivotFromCentroid);
        }

        /// <summary>The suction clock, exactly as <c>SpawnImplosionDebrisBatch</c> stamps a suction entity
        /// (including the legacy <c>_State</c> rest value the shader reads only while unstamped).</summary>
        internal static void WriteSuctionClock(MaterialPropertyBlock block, float startTime, float duration,
            float direction, float growDelay, in float3 location)
        {
            block.SetFloat(ImplosionStateId, direction < 0f ? 1f : 0f);
            block.SetFloat(Id(Family.Suction, 0), startTime);
            block.SetFloat(Id(Family.Suction, 1), duration);
            block.SetFloat(Id(Family.Suction, 2), direction);
            block.SetFloat(Id(Family.Suction, 3), growDelay);
            block.SetVector(Id(Family.Suction, 4), V(in location));
        }

        /// <summary>The §1 moving-target exception on this path: one float3 per live suction per frame.</summary>
        internal static void WriteSuctionLocation(MaterialPropertyBlock block, in float3 location) =>
            block.SetVector(Id(Family.Suction, 4), V(in location));

        // ── Culling envelope (twins of the RenderBounds writes) ──────────────────────────────────

        /// <summary>Twin of <c>ResetBoundsToMesh</c> + <c>ExpandBoundsForClockAnimation</c>: the renderer's local
        /// bounds become <paramref name="mesh"/>'s, displaced by half the object-space flight and padded. A
        /// MeshRenderer culls against its bounds on the CPU exactly as an entity does against RenderBounds.</summary>
        public static void ExpandBounds(Renderer renderer, Mesh mesh, in float3 objectDisplacement, float padding)
        {
            if (!Active || renderer == null || mesh == null) return;
            renderer.localBounds = ExpandedBounds(mesh.bounds, in objectDisplacement, padding);
        }

        /// <summary>The pure half of <see cref="ExpandBounds"/>: the same arithmetic as
        /// <c>PrismRenderService.ExpandBoundsForClockAnimation</c> applied to a reset box.</summary>
        internal static Bounds ExpandedBounds(Bounds mesh, in float3 objectDisplacement, float padding)
        {
            float3 half = objectDisplacement * 0.5f;
            float3 c = (float3)(Vector3)mesh.center + half;
            float3 e = (float3)(Vector3)mesh.extents + math.abs(half) + new float3(math.max(0f, padding));
            return new Bounds(new Vector3(c.x, c.y, c.z), new Vector3(e.x, e.y, e.z) * 2f);
        }

        /// <summary>Twin of <c>EncapsulateBoundsPoint</c>: grows the local bounds minimally to hold an OBJECT-space
        /// point (+ padding). No write while the point is already inside.</summary>
        public static void EncapsulateBoundsPoint(Renderer renderer, in float3 objectPoint, float padding)
        {
            if (!Active || renderer == null) return;
            var b = renderer.localBounds;
            float3 pad = new float3(math.max(0f, padding));
            float3 min = (float3)(Vector3)b.min, max = (float3)(Vector3)b.max;
            float3 pmin = objectPoint - pad, pmax = objectPoint + pad;
            if (math.all(pmin >= min) && math.all(pmax <= max)) return;
            b.SetMinMax((Vector3)math.min(min, pmin), (Vector3)math.max(max, pmax));
            renderer.localBounds = b;
        }

        /// <summary>Twin of <c>ResetBoundsToMesh</c>.</summary>
        public static void ResetBounds(Renderer renderer)
        {
            if (!Active || renderer == null) return;
            renderer.ResetLocalBounds();
        }

        // ── Removal ──────────────────────────────────────────────────────────────────────────────

        static void ClearIfActive(Renderer renderer, Family family)
        {
            if (!Active || renderer == null) return;
            Remove(renderer, family);
        }

        /// <summary>
        /// Drops one family from the renderer's block by rebuilding the block from the families still present.
        /// No-op when the family is absent (the common clear: nothing was ever stamped). When nothing remains the
        /// block is set to null — the renderer rejoins the SRP Batcher — and the culling envelope goes back to the
        /// mesh, since every envelope was sized for an animation that is now over.
        /// </summary>
        static void Remove(Renderer renderer, Family family)
        {
            var b = Begin(renderer);
            if (b.isEmpty || !IsPresent(b, family)) return;

            s_rebuild ??= new MaterialPropertyBlock();
            bool any = RebuildWithout(b, s_rebuild, family);

            if (any)
            {
                renderer.SetPropertyBlock(s_rebuild);
                return;
            }
            CommitNull(renderer);
            renderer.ResetLocalBounds();
        }

        /// <summary>The block-level half of a clear: <paramref name="to"/> becomes <paramref name="from"/> minus
        /// <paramref name="family"/>. Returns false when nothing is left — the caller then sets a null block.
        /// Pure over two blocks, so the tests can run it without a renderer or play mode.</summary>
        internal static bool RebuildWithout(MaterialPropertyBlock from, MaterialPropertyBlock to, Family family)
        {
            to.Clear();
            bool any = false;
            for (int f = 0; f < FamilyProperties.Length; f++)
            {
                if (f == (int)family) continue;
                any |= CopyIfPresent(from, to, (Family)f);
            }
            return any;
        }

        /// <summary>The block-level half of <see cref="ClearPrismStamps"/>: keeps ONLY <paramref name="family"/>.</summary>
        internal static bool RebuildKeeping(MaterialPropertyBlock from, MaterialPropertyBlock to, Family family)
        {
            to.Clear();
            return CopyIfPresent(from, to, family);
        }

        internal static bool IsPresent(MaterialPropertyBlock block, Family family)
        {
            int id = Id(family, 0);
            return FamilyProperties[(int)family][0].Vector ? block.HasVector(id) : block.HasFloat(id);
        }

        /// <summary>Copies a family's raw stored values (GetVector/SetVector never colour-convert, so a colour
        /// already converted at its stamp is copied exactly).</summary>
        static bool CopyIfPresent(MaterialPropertyBlock from, MaterialPropertyBlock to, Family family)
        {
            if (!IsPresent(from, family)) return false;
            var props = FamilyProperties[(int)family];
            for (int p = 0; p < props.Length; p++)
            {
                int id = Id(family, p);
                if (props[p].Vector) to.SetVector(id, from.GetVector(id));
                else to.SetFloat(id, from.GetFloat(id));
            }
            return true;
        }

        /// <summary>
        /// Touchpoint 3 for a timed family: ONE scheduled clear at the analytic end. Guarded by the stamp time it
        /// was scheduled for, so a re-stamp (interruption) or a pooled reuse that cleared the block leaves this
        /// callback owning nothing — it then expires as a no-op instead of being cancelled (cancelling per stamp is
        /// a scan per owner, and the owner here is the renderer, shared by every family).
        /// </summary>
        static void ScheduleClear(Renderer renderer, Family family, float stampTime, float delay)
        {
            var timers = PrismTimerManager.EnsureInstance();
            if (timers == null) return;
            timers.ScheduleAction(renderer, Mathf.Max(0f, delay), () =>
            {
                if (renderer == null || !Active) return;
                var b = Begin(renderer);
                int startId = Id(family, 0);
                if (!b.HasFloat(startId) || b.GetFloat(startId) != stampTime) return;
                Remove(renderer, family);
            });
        }
    }
}
