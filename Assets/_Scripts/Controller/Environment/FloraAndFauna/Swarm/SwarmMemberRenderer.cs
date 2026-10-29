using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11a (§19.2): with the unified prism path on, the bodies are prism render entities and this draws only the
    /// HEARTS. A heart is not a prism (several crystal models per element, a molt that shrinks one element away and
    /// re-forms another between ticks, none of the platform's prism effects), so it is drawn as the CRYSTAL it is:
    /// <see cref="CrystalHeartDraw"/> draws each listed heart with the elemental crystal's own models and materials,
    /// posed per frame (Docs/ECOSYSTEM.md §65.7). Until 2026-10-28 the member shader drew hearts too, with a look of
    /// its own that the elemental crystals had moved on from.
    ///
    /// Draws every LIVING member of one swarm from one GPU buffer (Docs/SWARM_FAUNA.md §14). Per TICK the
    /// swarm hands it the tick job's instance array and heart lists (one <c>GraphicsBuffer.SetData</c>, one copy
    /// for the hearts); per FRAME it sets ~a dozen per-swarm values and issues one body draw, then the hearts'.
    /// Nothing in the body draw is per member: the shader (SwarmMemberInstanced) interpolates the pose, blooms
    /// newborns and shows the tier from the instance data.
    ///
    /// Meshes are the member's OWN: the body prism's mesh off the tadpole prefab, and each element's crystal
    /// models off the elemental crystal set, so a member looks the same whether the swarm draws it or the platform
    /// does (after death). Colours are the palette's (plain / danger / shielded tier pairs of the swarm's domain, and
    /// the living-heart pair the crystals' own tint rule takes) - read once, at bind.
    ///
    /// Two carriers, one picture (Docs/SWARM_FAUNA.md §14.8): where the device has vertex-stage structured
    /// buffers (<see cref="Supported"/>) the instances live in a GraphicsBuffer as above; where it does not
    /// (WebGL2, GLES 3.0) the same instances are drawn by <see cref="SwarmMemberFallbackDraw"/> - per-instance
    /// constants, SM 3.5, the same pose and look code. Either way <see cref="Valid"/> is true and the swarm keeps
    /// its members as data; only a device with neither path falls back to a GameObject per member.
    /// </summary>
    public sealed class SwarmMemberRenderer : System.IDisposable
    {
        struct Part { public Mesh Mesh; public int Submesh; public Matrix4x4 Local; }

        static readonly int InstancesId = Shader.PropertyToID("_SwarmInstances");
        static readonly int MeshLocalId = Shader.PropertyToID("_SwarmMeshLocal");
        static readonly int AlphaId = Shader.PropertyToID("_SwarmAlpha");
        static readonly int ClockId = Shader.PropertyToID("_SwarmClock");
        static readonly int BloomId = Shader.PropertyToID("_SwarmBloomTicks");
        static readonly int UpId = Shader.PropertyToID("_SwarmUp");
        static readonly int UpAltId = Shader.PropertyToID("_SwarmUpAlt");
        static readonly int RimPowerId = Shader.PropertyToID("_SwarmRimPower");
        // [tier * 3 + domain slot] (Docs/SWARM_FAUNA.md §16.4) - new names: an array property's length is pinned by name
        static readonly int TierDarkId = Shader.PropertyToID("_SwarmTierDark"), TierBrightId = Shader.PropertyToID("_SwarmTierBright");
        static readonly int SpreadPlainId = Shader.PropertyToID("_SwarmSpreadPlain");
        static readonly int SpreadDangerId = Shader.PropertyToID("_SwarmSpreadDanger");
        static readonly int SpreadShieldId = Shader.PropertyToID("_SwarmSpreadShield");

        readonly Material _mat;
        readonly GraphicsBuffer _inst;
        readonly Part _body;
        // the body draw's blocks (one; the fallback's per batch): the per-part values are set once, the per-frame
        // ones on every block, so no draw ever depends on when a shared block is read
        readonly List<MaterialPropertyBlock> _mpbs = new();
        readonly int _cap;
        int _layer;
        // the hearts, as the crystals they are (Docs/ECOSYSTEM.md §65.7)
        readonly CrystalHeartDraw _hearts;
        // non-null on a device without vertex-stage structured buffers: the SM 3.5 instanced draw (§14.8)
        readonly SwarmMemberFallbackDraw _fallback;
        static bool s_warned;

        public bool Valid { get; }

        /// <summary>True when this renderer draws through the SM 3.5 instanced path (no compute on this device).</summary>
        public bool UsesFallback => _fallback != null;

        /// <summary>
        /// Round 11a (Docs/SWARM_FAUNA.md §19.2): false while the member BODIES are drawn as ordinary prism render
        /// entities (PrismRenderService) - this renderer then draws only the hearts. True is the round-7 body draw, the
        /// automatic fallback when the ECS render path is unavailable and the <c>UnifiedPrismBodies</c> off switch.
        /// </summary>
        public bool DrawBodies { get; set; } = true;

        /// <summary>The GraphicsBuffer path needs structured buffers in the VERTEX stage (SM 4.5). Without them the
        /// SM 3.5 path (<see cref="SwarmMemberFallbackDraw"/>) draws the same members.</summary>
        public static bool Supported =>
            SystemInfo.supportsComputeShaders && SystemInfo.maxComputeBufferInputsVertex > 0;

        /// <summary>
        /// Dev A/B only (the <c>memberdraw</c> console command, editor and development builds): renderers built while
        /// this is on take the SM 3.5 path even where the GraphicsBuffer path is supported, so the WebGL draw can be
        /// compared with the desktop one in the editor. Read once, at construction; never set by game code.
        /// </summary>
        public static bool ForceFallback { get; set; }

        /// <summary>Whether a renderer built now draws through <see cref="SwarmMemberFallbackDraw"/>.</summary>
        public static bool UseFallback => !Supported || ForceFallback;

        public SwarmMemberRenderer(Shader shader, SwarmTadpoleFauna prefab, int cap, int layer)
        {
            _cap = cap;
            _layer = layer;
            if (!shader || !prefab)
            {
                Warn(!shader ? "SwarmFaunaConfigSO.MemberShader is not assigned" : "the tadpole prefab is missing");
                return;
            }
            Material fallbackMaterial = null;
            if (UseFallback)
            {
                fallbackMaterial = SwarmMemberFallbackDraw.LoadMaterial(out var whyNot);
                if (!fallbackMaterial)
                {
                    Warn((Supported ? "the SM 3.5 path was forced (memberdraw)" :
                          $"this device has no vertex-stage structured buffers ({SystemInfo.graphicsDeviceType})") + $" and {whyNot}");
                    return;
                }
            }

            var bodyPrism = prefab.GetComponentInChildren<HealthPrism>(true);
            var bodyFilter = bodyPrism ? bodyPrism.GetComponent<MeshFilter>() : null;
            if (!bodyFilter || !bodyFilter.sharedMesh) { Warn("the tadpole prefab's body prism has no mesh"); return; }
            _body = new Part { Mesh = bodyFilter.sharedMesh, Submesh = 0, Local = Matrix4x4.identity };
            _hearts = new CrystalHeartDraw(cap, layer, 0xF);

            if (fallbackMaterial)
            {
                _fallback = new SwarmMemberFallbackDraw(fallbackMaterial, cap);
                _fallback.AddBody(_body.Mesh, _body.Submesh, _body.Local);
                _mpbs.AddRange(_fallback.Blocks);   // the palette / spread setters below write every block
                SwarmMemberFallbackDraw.Announce("Swarm", forced: Supported);
                Valid = true;
                return;
            }

            _mat = new Material(shader) { name = "SwarmMemberInstanced (runtime)", hideFlags = HideFlags.DontSave };
            var bodyBlock = new MaterialPropertyBlock();
            bodyBlock.SetMatrix(MeshLocalId, _body.Local);
            _mpbs.Add(bodyBlock);
            _inst = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cap, SwarmInstance.Stride);
            Valid = true;
        }

        static void Warn(string why)
        {
            if (s_warned) return;
            s_warned = true;
            CSDebug.LogWarning($"[Swarm] GPU member drawing is OFF: {why}. Every member falls back to a real " +
                               "GameObject (the pre-round-7 cost) - Docs/SWARM_FAUNA.md §14.");
        }

        /// <summary>Each body tier's prism spread, read once at bind: xyz = the tier material's <c>_Spread</c>,
        /// w = its <c>_SqrDistance</c>. Zero spread draws a closed prism (the pre-fix look).</summary>
        public void SetSpread(Vector4 plain, Vector4 danger, Vector4 shield)
        {
            foreach (var b in _mpbs)
            {
                b.SetVector(SpreadPlainId, plain);
                b.SetVector(SpreadDangerId, danger);
                b.SetVector(SpreadShieldId, shield);
            }
        }

        /// <summary>
        /// Palette, read once at bind: the body's base face and rim per TIER and per DOMAIN SLOT,
        /// <c>[tier * 3 + slot]</c> (tier 0 plain, 1 danger, 2 shielded). A one-colour swarm repeats its one
        /// domain in all three slots; a MultiDomain swarm gives each slot its own (§16.4). The living-heart pair
        /// and each element's heart world scale go to the hearts (<see cref="CrystalHeartDraw.SetLivingTint"/>).
        /// </summary>
        public void SetColours(Vector4[] tierDark, Vector4[] tierBright, Color heartDull, Color heartBright,
                               Vector4 heartScale, float rimPower)
        {
            foreach (var b in _mpbs)
            {
                b.SetVectorArray(TierDarkId, tierDark); b.SetVectorArray(TierBrightId, tierBright);
                b.SetFloat(RimPowerId, rimPower);
            }
            _hearts?.SetLivingTint(heartDull, heartBright, heartScale);
        }

        /// <summary>Once per TICK: the published frame of the swarm's tick job.</summary>
        public void Upload(SwarmTickJob job) => Upload(job.Instances, job.HeartIdx, job.HeartStart, job.HeartCount);

        /// <summary>Once per TICK, for any population that fills its own frame (a builder colony,
        /// Docs/BUILDERS_AND_THIEVES.md): the instance array and the per-element heart lists.</summary>
        public void Upload(SwarmInstance[] instances, uint[] heartIdx, int[] heartStart, int[] heartCount)
        {
            if (!Valid) return;
            _hearts.Upload(instances, 0, heartIdx, heartStart, heartCount);
            if (_fallback != null) _fallback.UploadBodies(instances, 0);
            else _inst.SetData(instances, 0, 0, _cap);
        }

        /// <summary>A member died between uploads: hide its slot NOW (one 80-byte write, event-driven).</summary>
        public void HideSlot(SwarmTickJob job, int i) => HideSlot(job.Instances, i);

        public void HideSlot(SwarmInstance[] instances, int i)
        {
            if (!Valid || i < 0 || i >= _cap) return;
            instances[i].Flags = 0u;
            _hearts.Hide(i);
            if (_fallback != null) _fallback.Hide(i);
            else _inst.SetData(instances, i, i, 1);
        }

        /// <summary>Once per FRAME. Nothing here scales with the member count.</summary>
        public void Draw(Bounds bounds, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt, int count)
        {
            if (!Valid || count <= 0) return;
            _hearts.Draw(bounds, alpha, clock, bloomTicks, up, upAlt);
            if (!DrawBodies) return;
            if (_fallback != null)
            {
                _fallback.Draw(bounds, _layer, count, alpha, clock, bloomTicks, up, upAlt);
                return;
            }
            for (int q = 0; q < _mpbs.Count; q++)
            {
                var b = _mpbs[q];
                b.SetBuffer(InstancesId, _inst);
                b.SetFloat(AlphaId, alpha);
                b.SetFloat(ClockId, clock);
                b.SetFloat(BloomId, bloomTicks);
                b.SetVector(UpId, up);
                b.SetVector(UpAltId, upAlt);
            }

            var rp = new RenderParams(_mat)
            {
                worldBounds = bounds,
                matProps = _mpbs[0],
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = _layer,
            };
            Graphics.RenderMeshPrimitives(rp, _body.Mesh, _body.Submesh, count);
        }

        public void Dispose()
        {
            _inst?.Release();
            _hearts?.Dispose();
            if (_mat) Object.Destroy(_mat);
        }
    }
}
