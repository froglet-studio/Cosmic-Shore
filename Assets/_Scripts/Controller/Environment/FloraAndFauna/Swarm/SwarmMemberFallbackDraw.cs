using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The member draw for a device WITHOUT vertex-stage structured buffers: WebGL2, OpenGL ES 3.0, anything below
    /// SM 4.5 (Docs/SWARM_FAUNA.md §14.8, Docs/SUBSTRATE_FAUNA.md §5.1). <see cref="SwarmMemberRenderer"/> and
    /// <see cref="SubstrateMemberRenderer"/> own one when <see cref="SwarmMemberRenderer.Supported"/> is false, and
    /// keep their GraphicsBuffer path untouched where it is true. The simulation, the proxies, the counts and every
    /// rule are the same either way: only the draw's carrier differs.
    ///
    /// <para>
    /// The picture is the SM 4.5 path's. SwarmMemberInstancedFallback.shader runs the same <c>SwarmMemberPoseOf</c>
    /// and <c>SwarmMemberShade</c> (SwarmMemberInstanced.hlsl) over the same 80-byte <see cref="SwarmInstance"/>, so the
    /// interpolated pose, the newborn bloom, the heart molt, the tier and domain palette, the open-with-distance spread
    /// and the prism occlusion corridor are identical. What changes is how the instance reaches the vertex shader:
    /// once per TICK each member is packed into five float4 rows (<see cref="Pack"/>): the bodies compacted to the
    /// living slots, the hearts gathered per element from the tick's heart lists. Once per FRAME each list is drawn
    /// with <c>Graphics.RenderMeshInstanced</c> in batches of <see cref="Batch"/>, the rows travelling as
    /// MaterialPropertyBlock arrays in Unity's instancing constant buffer.
    /// </para>
    ///
    /// <para>
    /// Cost. Per tick: ~20 floats copied per living member, plus one array re-set per (part, batch) whose rows
    /// changed. Per frame: one <c>RenderMeshInstanced</c> per (part, batch) and a handful of block writes per batch;
    /// nothing per member. A batch is 128 instances x 80 bytes = 10 KB, inside the 16 KB uniform block every
    /// WebGL2 / GLES 3.0 device must offer, so each call is ONE draw on every device this path runs on and its
    /// correctness never depends on Unity splitting a batch.
    /// </para>
    ///
    /// <para>
    /// One MaterialPropertyBlock per (part, batch), never shared between draws: the GraphicsBuffer path's rule
    /// (Docs/SWARM_FAUNA.md §14.5), so no draw depends on when a shared block is read.
    /// </para>
    /// </summary>
    public sealed class SwarmMemberFallbackDraw
    {
        /// <summary>Assets/Resources/SwarmMemberInstancedFallback.mat: SwarmMemberInstancedFallback.shader with GPU
        /// instancing ON. The asset (not a runtime <c>new Material</c>) is what keeps the shader and its INSTANCING_ON
        /// variant in a build: Graphics settings strip instancing variants no material asks for.</summary>
        public const string MaterialResource = "SwarmMemberInstancedFallback";

        /// <summary>Instances per <c>RenderMeshInstanced</c> call: 128 x 80 bytes fits the 16 KB minimum uniform block.</summary>
        public const int Batch = 128;

        const int RowCount = 5;

        static readonly int[] RowIds =
        {
            Shader.PropertyToID("_SwarmRow0"), Shader.PropertyToID("_SwarmRow1"), Shader.PropertyToID("_SwarmRow2"),
            Shader.PropertyToID("_SwarmRow3"), Shader.PropertyToID("_SwarmRow4"),
        };
        static readonly int PartId = Shader.PropertyToID("_SwarmPart");
        static readonly int HeartElementId = Shader.PropertyToID("_SwarmHeartElement");
        static readonly int MeshLocalId = Shader.PropertyToID("_SwarmMeshLocal");
        static readonly int AlphaId = Shader.PropertyToID("_SwarmAlpha");
        static readonly int ClockId = Shader.PropertyToID("_SwarmClock");
        static readonly int BloomId = Shader.PropertyToID("_SwarmBloomTicks");
        static readonly int UpId = Shader.PropertyToID("_SwarmUp");
        static readonly int UpAltId = Shader.PropertyToID("_SwarmUpAlt");

        // RenderMeshInstanced wants a matrix per instance; the shader never reads it (the pose is the rows')
        static readonly Matrix4x4[] s_identity = BuildIdentity();

        static Material s_material;
        static bool s_materialLoaded;
        static readonly HashSet<string> s_announced = new();

        /// <summary>One list of instances, drawn by one or more parts: the bodies, or one element's hearts.</summary>
        sealed class InstanceList
        {
            public int Count;
            public readonly Vector4[][] Rows;   // [batch * RowCount + row], each Batch long
            public readonly int[] Version;      // per batch: bumped on every write, so a block knows to re-read it

            public InstanceList(int batches)
            {
                Rows = new Vector4[batches * RowCount][];
                for (int i = 0; i < Rows.Length; i++) Rows[i] = new Vector4[Batch];
                Version = new int[batches];
            }

            public void Touch(int count)
            {
                for (int b = 0, n = (count + Batch - 1) / Batch; b < n; b++) Version[b]++;
            }
        }

        sealed class DrawPart
        {
            public Mesh Mesh;
            public int Submesh;
            public int List;                       // 0 = bodies, 1 + e = the hearts of element e
            public MaterialPropertyBlock[] Blocks; // one per batch
            public int[] Seen;                     // the list version each block last read
        }

        readonly Material _mat;
        readonly int _cap, _batches;
        readonly InstanceList[] _lists = new InstanceList[5];
        readonly List<DrawPart> _parts = new();
        readonly List<MaterialPropertyBlock> _blocks = new();
        readonly int[] _bodyAt;   // slot -> index in the body list, -1 = not drawn
        readonly int[] _bodySlot; // body index -> slot (ascending: the list is built in slot order)
        readonly int[] _heartAt;  // slot * 2 + k -> element * cap + index in that element's list, -1 = none

        /// <summary>Every block this draw owns, so the owner can write the per-swarm palette and spread once, at bind,
        /// exactly as it writes its GraphicsBuffer blocks.</summary>
        public IReadOnlyList<MaterialPropertyBlock> Blocks => _blocks;

        /// <summary>
        /// The fallback's material, or null with the reason this device cannot draw members without compute either
        /// (no GPU instancing, the material or its shader missing from the build, instancing switched off on it).
        /// </summary>
        public static Material LoadMaterial(out string whyNot)
        {
            whyNot = null;
            if (!SystemInfo.supportsInstancing)
            {
                whyNot = "it has no GPU instancing either";
                return null;
            }
            if (!s_materialLoaded)
            {
                s_materialLoaded = true;
                s_material = Resources.Load<Material>(MaterialResource);
            }
            if (!s_material) whyNot = $"Resources/{MaterialResource}.mat is missing from the build";
            else if (!s_material.shader || !s_material.shader.isSupported)
                whyNot = $"the {MaterialResource} shader is not supported on {SystemInfo.graphicsDeviceType}";
            else if (!s_material.enableInstancing) whyNot = $"GPU instancing is off on Resources/{MaterialResource}.mat";
            return whyNot == null ? s_material : null;
        }

        /// <summary>Once per kind of owner, on the Ecology channel: which path this device draws members with. A state,
        /// not a fault (a WebGL build always takes it), so it is verbose, not a warning.</summary>
        public static void Announce(string owner, bool forced)
        {
            if (!s_announced.Add(owner)) return;
            string why = forced ? "forced by the memberdraw console command"
                                : $"no vertex-stage structured buffers ({SystemInfo.graphicsDeviceType})";
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[{owner}] {why}: members are drawn through the SM 3.5 instanced path ({MaterialResource}, " +
                $"{Batch} per batch) - Docs/SWARM_FAUNA.md §14.8.");
        }

        /// <param name="material">From <see cref="LoadMaterial"/>; shared, never modified (every value is a block's).</param>
        /// <param name="cap">Member slots: the most bodies, and the most hearts of any one element, a tick can list.</param>
        public SwarmMemberFallbackDraw(Material material, int cap)
        {
            _mat = material;
            _cap = Mathf.Max(1, cap);
            _batches = (_cap + Batch - 1) / Batch;
            for (int l = 0; l < _lists.Length; l++) _lists[l] = new InstanceList(_batches);
            _bodyAt = new int[_cap];
            _bodySlot = new int[_cap];
            _heartAt = new int[2 * _cap];
            System.Array.Fill(_bodyAt, -1);
            System.Array.Fill(_heartAt, -1);
        }

        /// <summary>The body prism (drawn over the body list).</summary>
        public void AddBody(Mesh mesh, int submesh, Matrix4x4 local) => AddPart(mesh, submesh, local, 0, 0f, 0f);

        /// <summary>One crystal model of one heart element (drawn over that element's heart list).</summary>
        public void AddHeart(Mesh mesh, int submesh, Matrix4x4 local, int element) =>
            AddPart(mesh, submesh, local, 1 + Mathf.Clamp(element, 0, 3), 1f, element);

        void AddPart(Mesh mesh, int submesh, Matrix4x4 local, int list, float part, float element)
        {
            var p = new DrawPart
            {
                Mesh = mesh, Submesh = submesh, List = list,
                Blocks = new MaterialPropertyBlock[_batches], Seen = new int[_batches],
            };
            for (int b = 0; b < _batches; b++)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat(PartId, part);
                block.SetFloat(HeartElementId, element);
                block.SetMatrix(MeshLocalId, local);
                p.Blocks[b] = block;
                _blocks.Add(block);
            }
            _parts.Add(p);
        }

        /// <summary>Once per TICK: the living members of slots [<paramref name="start"/>, start + cap), in slot order.</summary>
        public void UploadBodies(SwarmInstance[] instances, int start)
        {
            var list = _lists[0];
            int n = 0, slots = Mathf.Min(_cap, instances.Length - start);
            for (int i = 0; i < _cap; i++)
            {
                if (i >= slots || (instances[start + i].Flags & 1u) == 0u) { _bodyAt[i] = -1; continue; }
                _bodyAt[i] = n;
                _bodySlot[n] = i;
                Pack(in instances[start + i], list.Rows, n);
                n++;
            }
            list.Count = n;
            list.Touch(n);
        }

        /// <summary>Once per TICK, before the <see cref="UploadHearts"/> calls: empties every element's heart list.</summary>
        public void BeginHearts()
        {
            for (int e = 0; e < 4; e++) _lists[1 + e].Count = 0;
            System.Array.Fill(_heartAt, -1);
        }

        /// <summary>
        /// Once per TICK per element: <paramref name="count"/> slot indices from
        /// <paramref name="slots"/>[<paramref name="slotsStart"/>..], each relative to <paramref name="start"/> in
        /// <paramref name="instances"/> - the same list the GraphicsBuffer path uploads as _SwarmHeartIdx. A molting
        /// heart is in two elements' lists; the shader shows it in the one the molt has reached.
        /// </summary>
        public void UploadHearts(int element, SwarmInstance[] instances, int start, uint[] slots, int slotsStart, int count)
        {
            if (element < 0 || element > 3) return;
            var list = _lists[1 + element];
            int n = Mathf.Clamp(count, 0, _cap);
            for (int k = 0; k < n; k++)
            {
                uint slot = slots[slotsStart + k];
                if (slot >= (uint)_cap) { Pack(default, list.Rows, k); continue; }   // never drawn: Flags 0
                Pack(in instances[start + (int)slot], list.Rows, k);
                int at = (int)slot * 2;
                _heartAt[_heartAt[at] < 0 ? at : at + 1] = element * _cap + k;
            }
            list.Count = n;
            list.Touch(n);
        }

        /// <summary>A member died between uploads: hide every row that draws <paramref name="slot"/> NOW (the owner
        /// has already zeroed its Flags in the frame). Event-driven, a few writes.</summary>
        public void Hide(int slot)
        {
            if (slot < 0 || slot >= _cap) return;
            int b = _bodyAt[slot];
            if (b >= 0) Kill(_lists[0], b);
            for (int k = 0; k < 2; k++)
            {
                int h = _heartAt[slot * 2 + k];
                if (h >= 0) Kill(_lists[1 + h / _cap], h % _cap);
            }
        }

        static void Kill(InstanceList list, int index)
        {
            int batch = index / Batch;
            list.Rows[batch * RowCount + 1][index % Batch].w = 0f;   // row 1 .w is Flags: 0 = dead, collapsed
            list.Version[batch]++;
        }

        /// <summary>
        /// Once per FRAME. <paramref name="bodySlotLimit"/> is the GraphicsBuffer path's instance count: bodies of
        /// slots at or past it are not drawn (every caller passes the cap today).
        /// </summary>
        public void Draw(Bounds bounds, int layer, bool drawBodies, int bodySlotLimit, float alpha, float clock,
                         float bloomTicks, Vector3 up, Vector3 upAlt)
        {
            var rp = new RenderParams(_mat)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = layer,
            };
            for (int q = 0; q < _parts.Count; q++)
            {
                var p = _parts[q];
                if (p.List == 0 && !drawBodies) continue;
                var list = _lists[p.List];
                int n = p.List == 0 ? BodiesBelow(bodySlotLimit) : list.Count;
                for (int b = 0; b * Batch < n; b++)
                {
                    var block = p.Blocks[b];
                    if (p.Seen[b] != list.Version[b])
                    {
                        for (int r = 0; r < RowCount; r++) block.SetVectorArray(RowIds[r], list.Rows[b * RowCount + r]);
                        p.Seen[b] = list.Version[b];
                    }
                    block.SetFloat(AlphaId, alpha);
                    block.SetFloat(ClockId, clock);
                    block.SetFloat(BloomId, bloomTicks);
                    block.SetVector(UpId, up);
                    block.SetVector(UpAltId, upAlt);
                    rp.matProps = block;
                    Graphics.RenderMeshInstanced(rp, p.Mesh, p.Submesh, s_identity, Mathf.Min(Batch, n - b * Batch));
                }
            }
        }

        /// <summary>How many listed bodies have a slot below <paramref name="limit"/> (the list is in slot order).</summary>
        int BodiesBelow(int limit)
        {
            int n = _lists[0].Count;
            if (limit >= _cap) return n;
            if (limit <= 0) return 0;
            int lo = 0, hi = n;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_bodySlot[mid] < limit) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// One <see cref="SwarmInstance"/> as the shader's five rows (SwarmInstanceFromRows in SwarmMemberInstanced.hlsl
        /// unpacks them): (PrevPos, BirthTick) (CurPos, Flags) (PrevFace, PrevMolt) (CurFace, CurMolt) (Scale, PrismZ).
        /// Flags travel as a float VALUE - every bit is below 2^10, exact in a float - never as raw bits, which a GPU
        /// may flush to zero as a denormal.
        /// </summary>
        static void Pack(in SwarmInstance s, Vector4[][] rows, int index)
        {
            int b = (index / Batch) * RowCount, i = index % Batch;
            rows[b][i] = new Vector4(s.PrevPos.X, s.PrevPos.Y, s.PrevPos.Z, s.BirthTick);
            rows[b + 1][i] = new Vector4(s.CurPos.X, s.CurPos.Y, s.CurPos.Z, s.Flags);
            rows[b + 2][i] = new Vector4(s.PrevFace.X, s.PrevFace.Y, s.PrevFace.Z, s.PrevMolt);
            rows[b + 3][i] = new Vector4(s.CurFace.X, s.CurFace.Y, s.CurFace.Z, s.CurMolt);
            rows[b + 4][i] = new Vector4(s.Scale.X, s.Scale.Y, s.Scale.Z, s.PrismZ);
        }

        static Matrix4x4[] BuildIdentity()
        {
            var m = new Matrix4x4[Batch];
            for (int i = 0; i < m.Length; i++) m[i] = Matrix4x4.identity;
            return m;
        }
    }
}
