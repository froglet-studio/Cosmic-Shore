using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws every LIVING agent of one substrate population from one GPU buffer (Docs/SUBSTRATE_FAUNA.md §5), through
    /// the swarm's member shader (SwarmMemberInstanced, Docs/SWARM_FAUNA.md §14): the tick job publishes the same
    /// 80-byte <see cref="SwarmInstance"/> per agent, so the shader interpolates the pose, blooms newborns and shows the
    /// DANGER tier exactly as it does a tadpole. Per tick: one <c>GraphicsBuffer.SetData</c> of this population's slice
    /// of the cell's frame, and one copy for the hearts. Per frame: one body draw, then the hearts - drawn as the
    /// crystal they are, with the elemental crystal's own models and materials (<see cref="CrystalHeartDraw"/>,
    /// Docs/ECOSYSTEM.md §65.7), as the swarm's are.
    ///
    /// The shared prism path (round 11a, Docs/SWARM_FAUNA.md §19.2): when PrismRenderService is on, every agent's BODY
    /// is an ordinary prism entity (SubstrateFauna.SyncEntities / PoseBodies, the swarm's code over this population's
    /// slice) and this draws the HEARTS only (<see cref="DrawBodies"/> false) - the render service draws no heart
    /// crystal. With the service off (its default) this draws both, as the swarm does.
    ///
    /// On a device without vertex-stage structured buffers (WebGL2, GLES 3.0) the same instances are drawn by
    /// <see cref="SwarmMemberFallbackDraw"/> (SM 3.5, per-instance constants, the same pose and look code;
    /// Docs/SUBSTRATE_FAUNA.md §5.1) instead of not at all.
    /// </summary>
    public sealed class SubstrateMemberRenderer : System.IDisposable
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
        static readonly int TierDarkId = Shader.PropertyToID("_SwarmTierDark"), TierBrightId = Shader.PropertyToID("_SwarmTierBright");
        static readonly int SpreadPlainId = Shader.PropertyToID("_SwarmSpreadPlain");
        static readonly int SpreadDangerId = Shader.PropertyToID("_SwarmSpreadDanger");
        static readonly int SpreadShieldId = Shader.PropertyToID("_SwarmSpreadShield");
        static readonly int SpreadPropId = Shader.PropertyToID("_Spread");
        static readonly int SqrDistancePropId = Shader.PropertyToID("_SqrDistance");

        readonly Material _mat;
        readonly GraphicsBuffer _inst;
        readonly Part _body;
        readonly List<MaterialPropertyBlock> _mpbs = new();
        readonly int _cap, _layer, _element;
        // the hearts, as the crystals they are (Docs/ECOSYSTEM.md §65.7)
        readonly CrystalHeartDraw _hearts;
        // non-null on a device without vertex-stage structured buffers: the SM 3.5 instanced draw (§5.1)
        readonly SwarmMemberFallbackDraw _fallback;
        static bool s_warned;

        public bool Valid { get; }

        /// <summary>True when this renderer draws through the SM 3.5 instanced path (no compute on this device).</summary>
        public bool UsesFallback => _fallback != null;

        /// <summary>False while the bodies are PrismRenderService entities (the unified path): this then draws hearts only.</summary>
        public bool DrawBodies { get; set; } = true;

        /// <summary>The GraphicsBuffer path's requirement (<see cref="SwarmMemberRenderer.Supported"/>); without it the
        /// SM 3.5 path draws the same agents.</summary>
        public static bool Supported => SwarmMemberRenderer.Supported;

        /// <param name="element">Research element index (0 Charge, 1 Mass, 2 Space, 3 Time) every agent's heart is.</param>
        public SubstrateMemberRenderer(Shader shader, SubstrateAgentFauna prefab, int cap, int element, int layer)
        {
            _cap = Mathf.Max(1, cap);
            _layer = layer;
            _element = Mathf.Clamp(element, 0, 3);
            if (!shader || !prefab)
            {
                Warn(!shader ? "SubstrateSpeciesSO.MemberShader is not assigned" : "the agent prefab is missing");
                return;
            }
            Material fallbackMaterial = null;
            if (SwarmMemberRenderer.UseFallback)
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
            if (!bodyFilter || !bodyFilter.sharedMesh) { Warn("the agent prefab's body prism has no mesh"); return; }
            _body = new Part { Mesh = bodyFilter.sharedMesh, Submesh = 0, Local = Matrix4x4.identity };
            _hearts = new CrystalHeartDraw(_cap, layer, 1 << _element);

            if (fallbackMaterial)
            {
                _fallback = new SwarmMemberFallbackDraw(fallbackMaterial, _cap);
                _fallback.AddBody(_body.Mesh, _body.Submesh, _body.Local);
                _mpbs.AddRange(_fallback.Blocks);   // SetPalette writes every block
                SwarmMemberFallbackDraw.Announce("Substrate", forced: Supported);
                Valid = true;
                return;
            }

            _mat = new Material(shader) { name = "SwarmMemberInstanced (substrate, runtime)", hideFlags = HideFlags.DontSave };
            var bodyBlock = new MaterialPropertyBlock();
            bodyBlock.SetMatrix(MeshLocalId, _body.Local);
            _mpbs.Add(bodyBlock);
            _inst = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _cap, SwarmInstance.Stride);
            Valid = true;
        }

        static void Warn(string why)
        {
            if (s_warned) return;
            s_warned = true;
            CSDebug.LogWarning($"[Substrate] GPU agent drawing is OFF: {why}. Agents are only visible while they have " +
                               "a proxy - Docs/SUBSTRATE_FAUNA.md §5.");
        }

        /// <summary>Palette, read once at bind: the body's base face and rim per tier for the population's domain
        /// (all three domain slots the same - one population is one colour) and the living-heart pair the crystals' own
        /// tint rule takes (<see cref="CrystalHeartDraw.SetLivingTint"/>).</summary>
        public void SetPalette(ThemeManagerDataContainerSO theme, Domains domain, Vector4 heartScale)
        {
            if (!Valid) return;
            var colors = theme ? theme.ColorSet : null;
            Color bd = new(0.05f, 0.2f, 0.4f), bb = new(0.4f, 0.8f, 1.2f);
            Color dd = bd, db = new(1.5f, 0.3f, 0.1f), sd = bd, sb = bb;
            Color hd = new(0.1f, 0.2f, 0.5f), hb = new(0.8f, 0.9f, 1.4f);
            if (colors != null)
            {
                colors.TryGetPrismKindColors(domain, PrismKind.Plain, out bb, out bd);
                colors.TryGetPrismKindColors(domain, PrismKind.Danger, out db, out dd);
                colors.TryGetPrismKindColors(domain, PrismKind.Shielded, out sb, out sd);
                if (colors.TryGetColorSetByDomain(Domains.Blue, out var neutral) && neutral != null)
                { hd = neutral.DullCrystalColor; hb = neutral.BrightCrystalColor; }
            }
            var dark = new Vector4[9];
            var bright = new Vector4[9];
            for (int slot = 0; slot < 3; slot++)
            {
                dark[slot] = bd; bright[slot] = bb;
                dark[3 + slot] = dd; bright[3 + slot] = db;
                dark[6 + slot] = sd; bright[6 + slot] = sb;
            }
            var set = theme ? theme.BaseMaterialSet : null;
            var spreadPlain = TierSpread(set ? set.BlockMaterial : null);
            var spreadDanger = TierSpread(set ? set.DangerousBlockMaterial : null);
            var spreadShield = TierSpread(set ? set.ShieldedBlockMaterial : null);
            foreach (var b in _mpbs)
            {
                b.SetVectorArray(TierDarkId, dark); b.SetVectorArray(TierBrightId, bright);
                b.SetFloat(RimPowerId, 2f);
                b.SetVector(SpreadPlainId, spreadPlain);
                b.SetVector(SpreadDangerId, spreadDanger);
                b.SetVector(SpreadShieldId, spreadShield);
            }
            _hearts.SetLivingTint(hd, hb, heartScale);
        }

        static Vector4 TierSpread(Material m)
        {
            if (!m || !m.HasProperty(SpreadPropId)) return new Vector4(0f, 0f, 0f, 100000f);
            Vector4 s = m.GetVector(SpreadPropId);
            s.w = m.HasProperty(SqrDistancePropId) ? m.GetFloat(SqrDistancePropId) : 100000f;
            return s;
        }

        /// <summary>Once per TICK: this population's slice [start, start + cap) of the published frame, and its hearts.</summary>
        public void Upload(SubstrateTickJob job, int start, int pop)
        {
            if (!Valid) return;
            _hearts.Upload(job.Instances, start, _element, job.HeartIdx, start, Mathf.Min(job.HeartCount(pop), _cap));
            if (_fallback != null) _fallback.UploadBodies(job.Instances, start);
            else _inst.SetData(job.Instances, start, 0, _cap);
        }

        /// <summary>An agent died between uploads: hide its slot NOW (one 80-byte write). <paramref name="local"/> is the
        /// slot's index inside the population's slice.</summary>
        public void HideSlot(SubstrateTickJob job, int start, int local)
        {
            if (!Valid || local < 0 || local >= _cap) return;
            job.Instances[start + local].Flags = 0u;
            _hearts.Hide(local);
            if (_fallback != null) _fallback.Hide(local);
            else _inst.SetData(job.Instances, start + local, local, 1);
        }

        /// <summary>Once per FRAME. Nothing here scales with the agent count.</summary>
        public void Draw(Bounds bounds, float alpha, float clock, float bloomTicks)
        {
            if (!Valid) return;
            _hearts.Draw(bounds, alpha, clock, bloomTicks, Vector3.up, Vector3.forward);
            if (!DrawBodies) return;
            if (_fallback != null)
            {
                _fallback.Draw(bounds, _layer, _cap, alpha, clock, bloomTicks, Vector3.up, Vector3.forward);
                return;
            }
            for (int q = 0; q < _mpbs.Count; q++)
            {
                var b = _mpbs[q];
                b.SetBuffer(InstancesId, _inst);
                b.SetFloat(AlphaId, alpha);
                b.SetFloat(ClockId, clock);
                b.SetFloat(BloomId, bloomTicks);
                b.SetVector(UpId, Vector3.up);
                b.SetVector(UpAltId, Vector3.forward);
            }
            var rp = new RenderParams(_mat)
            {
                worldBounds = bounds,
                matProps = _mpbs[0],
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = _layer,
            };
            Graphics.RenderMeshPrimitives(rp, _body.Mesh, _body.Submesh, _cap);
        }

        public void Dispose()
        {
            _inst?.Release();
            _hearts?.Dispose();
            if (_mat) Object.Destroy(_mat);
        }
    }
}
