using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Draws every LIVING member of one swarm from one GPU buffer (Docs/SWARM_FAUNA.md §14). Per TICK the
    /// swarm hands it the tick job's instance array and heart lists (two <c>GraphicsBuffer.SetData</c>);
    /// per FRAME it sets ~a dozen per-swarm values and issues one body draw plus one draw per crystal
    /// model per element. Nothing here is per member: the shader (SwarmMemberInstanced) interpolates
    /// the pose, blooms newborns, re-forms molting hearts and shows the tier from the instance data.
    ///
    /// Meshes are the member's OWN: the body prism's mesh off the tadpole prefab, and each element's
    /// crystal models off the elemental crystal set, so a member looks the same whether the swarm draws
    /// it or the platform does (after death). Colours are the palette's (plain / danger / shielded tier
    /// pairs of the swarm's domain, and the living-heart neutral pair) - read once, at bind.
    /// </summary>
    public sealed class SwarmMemberRenderer : System.IDisposable
    {
        struct Part { public Mesh Mesh; public int Submesh; public Matrix4x4 Local; public int Element; }

        static readonly int InstancesId = Shader.PropertyToID("_SwarmInstances");
        static readonly int HeartIdxId = Shader.PropertyToID("_SwarmHeartIdx");
        static readonly int PartId = Shader.PropertyToID("_SwarmPart");
        static readonly int BaseId = Shader.PropertyToID("_SwarmBase");
        static readonly int HeartElementId = Shader.PropertyToID("_SwarmHeartElement");
        static readonly int MeshLocalId = Shader.PropertyToID("_SwarmMeshLocal");
        static readonly int AlphaId = Shader.PropertyToID("_SwarmAlpha");
        static readonly int ClockId = Shader.PropertyToID("_SwarmClock");
        static readonly int BloomId = Shader.PropertyToID("_SwarmBloomTicks");
        static readonly int UpId = Shader.PropertyToID("_SwarmUp");
        static readonly int UpAltId = Shader.PropertyToID("_SwarmUpAlt");
        static readonly int HeartScaleId = Shader.PropertyToID("_SwarmHeartScale");
        static readonly int RimPowerId = Shader.PropertyToID("_SwarmRimPower");
        // [tier * 3 + domain slot] (Docs/SWARM_FAUNA.md §16.4) - new names: an array property's length is pinned by name
        static readonly int TierDarkId = Shader.PropertyToID("_SwarmTierDark"), TierBrightId = Shader.PropertyToID("_SwarmTierBright");
        static readonly int HeartDullId = Shader.PropertyToID("_SwarmHeartDull"), HeartBrightId = Shader.PropertyToID("_SwarmHeartBright");
        static readonly int SpreadPlainId = Shader.PropertyToID("_SwarmSpreadPlain");
        static readonly int SpreadDangerId = Shader.PropertyToID("_SwarmSpreadDanger");
        static readonly int SpreadShieldId = Shader.PropertyToID("_SwarmSpreadShield");

        readonly Material _mat;
        readonly GraphicsBuffer _inst, _heart;
        readonly Part _body;
        readonly List<Part> _hearts = new();
        // one block per DRAW (body + each heart part): the per-part values are set once, the per-frame
        // ones on every block, so no draw ever depends on when a shared block is read
        readonly List<MaterialPropertyBlock> _mpbs = new();
        readonly int _cap;
        int _heartTotal;
        readonly int[] _heartStart = new int[4], _heartCount = new int[4];
        int _layer;
        static bool s_warned;

        public bool Valid { get; }

        /// <summary>Instanced drawing needs structured buffers in the VERTEX stage (SM 4.5).</summary>
        public static bool Supported =>
            SystemInfo.supportsComputeShaders && SystemInfo.maxComputeBufferInputsVertex > 0;

        public SwarmMemberRenderer(Shader shader, SwarmTadpoleFauna prefab, int cap, int layer)
        {
            _cap = cap;
            _layer = layer;
            if (!shader || !Supported || !prefab)
            {
                Warn(!shader ? "SwarmFaunaConfigSO.MemberShader is not assigned"
                     : !Supported ? $"this device has no vertex-stage structured buffers ({SystemInfo.graphicsDeviceType})"
                     : "the tadpole prefab is missing");
                return;
            }

            var bodyPrism = prefab.GetComponentInChildren<HealthPrism>(true);
            var bodyFilter = bodyPrism ? bodyPrism.GetComponent<MeshFilter>() : null;
            if (!bodyFilter || !bodyFilter.sharedMesh) { Warn("the tadpole prefab's body prism has no mesh"); return; }
            _body = new Part { Mesh = bodyFilter.sharedMesh, Submesh = 0, Local = Matrix4x4.identity, Element = -1 };

            var set = ElementalCrystalSetSO.Load();
            for (int e = 0; e < 4; e++)
            {
                var crystal = set ? set.GetPrefab(SwarmFaunaConfigSO.ToElement(e)) : null;
                if (!crystal) continue;
                var root = crystal.transform;
                // the heart's ROOT is sized at runtime (LifeFormCrystal.SetWorldScale), so the mesh's own
                // transform is taken under a unit-scale root that keeps the prefab's root rotation
                var rootBasis = Matrix4x4.Rotate(root.localRotation);
                var toRoot = root.worldToLocalMatrix;
                foreach (var r in crystal.GetComponentsInChildren<Renderer>(true))
                {
                    Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh
                              : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                    if (!mesh) continue;
                    var local = rootBasis * (toRoot * r.transform.localToWorldMatrix);
                    for (int sm = 0; sm < mesh.subMeshCount; sm++)
                        _hearts.Add(new Part { Mesh = mesh, Submesh = sm, Local = local, Element = e });
                }
            }
            if (_hearts.Count == 0) Warn("no elemental crystal model was found - hearts will not be drawn");

            _mat = new Material(shader) { name = "SwarmMemberInstanced (runtime)", hideFlags = HideFlags.DontSave };
            var bodyBlock = new MaterialPropertyBlock();
            bodyBlock.SetFloat(PartId, 0f); bodyBlock.SetFloat(BaseId, 0f); bodyBlock.SetMatrix(MeshLocalId, _body.Local);
            _mpbs.Add(bodyBlock);
            foreach (var part in _hearts)
            {
                var b = new MaterialPropertyBlock();
                b.SetFloat(PartId, 1f); b.SetFloat(HeartElementId, part.Element); b.SetMatrix(MeshLocalId, part.Local);
                _mpbs.Add(b);
            }
            _inst = new GraphicsBuffer(GraphicsBuffer.Target.Structured, cap, SwarmInstance.Stride);
            _heart = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, 2 * cap), sizeof(uint));
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
        /// domain in all three slots; a MultiDomain swarm gives each slot its own (§16.4).
        /// </summary>
        public void SetColours(Vector4[] tierDark, Vector4[] tierBright, Color heartDull, Color heartBright,
                               Vector4 heartScale, float rimPower)
        {
            foreach (var b in _mpbs)
            {
                b.SetVectorArray(TierDarkId, tierDark); b.SetVectorArray(TierBrightId, tierBright);
                b.SetColor(HeartDullId, heartDull); b.SetColor(HeartBrightId, heartBright);
                b.SetVector(HeartScaleId, heartScale);
                b.SetFloat(RimPowerId, rimPower);
            }
        }

        /// <summary>Once per TICK: the published frame of the swarm's tick job.</summary>
        public void Upload(SwarmTickJob job)
        {
            if (!Valid) return;
            _inst.SetData(job.Instances, 0, 0, _cap);
            _heartTotal = 0;
            for (int e = 0; e < 4; e++)
            {
                _heartStart[e] = job.HeartStart[e]; _heartCount[e] = job.HeartCount[e];
                _heartTotal = Mathf.Max(_heartTotal, job.HeartStart[e] + job.HeartCount[e]);
            }
            if (_heartTotal > 0) _heart.SetData(job.HeartIdx, 0, 0, _heartTotal);
        }

        /// <summary>A member died between uploads: hide its slot NOW (one 80-byte write, event-driven).</summary>
        public void HideSlot(SwarmTickJob job, int i)
        {
            if (!Valid || i < 0 || i >= _cap) return;
            job.Instances[i].Flags = 0u;
            _inst.SetData(job.Instances, i, i, 1);
        }

        /// <summary>Once per FRAME. Nothing here scales with the member count.</summary>
        public void Draw(Bounds bounds, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt, int count)
        {
            if (!Valid || count <= 0) return;
            for (int q = 0; q < _mpbs.Count; q++)
            {
                var b = _mpbs[q];
                b.SetBuffer(InstancesId, _inst);
                b.SetBuffer(HeartIdxId, _heart);
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

            for (int q = 0; q < _hearts.Count; q++)
            {
                var part = _hearts[q];
                int n = _heartCount[part.Element];
                if (n <= 0) continue;
                var b = _mpbs[q + 1];
                b.SetFloat(BaseId, _heartStart[part.Element]);
                rp.matProps = b;
                Graphics.RenderMeshPrimitives(rp, part.Mesh, part.Submesh, n);
            }
        }

        public void Dispose()
        {
            _inst?.Release();
            _heart?.Release();
            if (_mat) Object.Destroy(_mat);
        }
    }
}
