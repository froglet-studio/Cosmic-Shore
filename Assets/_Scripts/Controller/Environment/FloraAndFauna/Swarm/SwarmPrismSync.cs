// Round 11a (Docs/SWARM_FAUNA.md §19): ONE prism system. A swarm member's body is no longer found through a
// hand-copied member grid and drawn by its own shader - it is an ordinary PrismSpatialIndex VIRTUAL entry
// (every prism query, the AOE pass, the cell's volume sum and the phase ladder see it exactly as they see any
// prism) and an ordinary PrismRenderService entity (so it wears the platform's materials, colours and spread).
//
// This file is the half of that which is plain C#: no UnityEngine, so the SAME file compiles and RUNS in
// Tools/Build/swarm_core_harness (R11a-R11d):
//
//   * SwarmBodyPose - the member body's pose as arithmetic: the point the index stores for it each tick
//     (the body prism's centre at the middle of the published step) and the per-frame world matrix the
//     render entity gets (the SwarmMemberInstanced shader's body pose, term for term: interpolated root,
//     LookRotation(face, up), seat PrismZ behind the heart, the body's Scale, the newborn's bloom);
//   * SwarmEntryLedger - which members the index must hold, decided from the published frame: one live
//     entry per living member, re-registered when a slot is reused by a newborn, SUSPENDED while the member
//     has a real body prism of its own (a proxy whose body finished creation - then THAT prism is the
//     member's one entry), released on death. The glue hands it an ISwarmEntrySink over PrismSpatialIndex;
//     the harness hands it a model of the index's virtual-entry contract and checks count-once;
//   * SwarmEntityLedger - which members have a render entity and which are shown: created in one batch the
//     first time a slot holds a member, shown while it lives, hidden on death, and the compact list of shown
//     slots the per-frame matrix pass walks.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The member body's pose (Docs/SWARM_FAUNA.md §19.2). Pure arithmetic over a <see cref="SwarmInstance"/>.</summary>
    public static class SwarmBodyPose
    {
        /// <summary>A body's volume as the cell counts a prism's: |x·y·z| (a plan may author a negative extent - the whale).</summary>
        public static double BodyVolume(Vector3 scale) => Math.Abs((double)scale.X * scale.Y * scale.Z);

        /// <summary>Half the body's scale diagonal - a real prism's <c>0.5 * lossyScale.magnitude</c>, the contact allowance a
        /// swept weapon adds.</summary>
        public static float BoundingRadius(Vector3 scale) => 0.5f * scale.Length();

        /// <summary>The display alpha the index's stored point is taken at: the middle of the published step, so the point is
        /// never more than half a tick's travel from where the member is drawn (§19.1).</summary>
        public const float IndexAlpha = 0.5f;

        /// <summary>SwarmTickJob.FaceAt: the interpolated facing, normalised; the current facing for a half-turn.</summary>
        public static Vector3 Face(in SwarmInstance s, float alpha)
        {
            var f = Vector3.Lerp(s.PrevFace, s.CurFace, alpha);
            float l = f.Length();
            return l > 1e-5f ? f / l : s.CurFace;
        }

        /// <summary>World centre of the BODY PRISM at display <paramref name="alpha"/>: the interpolated root plus PrismZ along
        /// the face - where the proxy's body sits (SwarmTickJob.BodyAt), and what the index stores for the member.</summary>
        public static Vector3 Body(in SwarmInstance s, float alpha) =>
            Vector3.Lerp(s.PrevPos, s.CurPos, alpha) + Face(s, alpha) * s.PrismZ;

        /// <summary>SwarmMemberInstanced.hlsl SwarmSmooth01 over the bloom: a newborn grows from 0.001.</summary>
        public static float Bloom(float clock, float birthTick, float bloomTicks)
        {
            // scalar comparisons only (Burst-compiled by SwarmPoseJob through PoseMatrix)
            float x = (clock - birthTick) / (bloomTicks > 1e-3f ? bloomTicks : 1e-3f);
            x = x < 0f ? 0f : (x > 1f ? 1f : x);
            float y = x * x * (3f - 2f * x);
            return y > 0.001f ? y : 0.001f;
        }

        /// <summary>
        /// THE body pose (Docs/SWARM_FAUNA.md §19.2, round 11a-2): the world matrix of member <paramref name="s"/>'s body at
        /// display <paramref name="alpha"/>, COLUMN-MAJOR (the layout of Unity.Mathematics.float4x4 and so of the entity's
        /// LocalToWorld). The shader's body pose, term for term: face = lerp(Prev, Cur) (Cur for a half-turn), basis =
        /// SwarmBasis(face, up, upAlt), and a mesh vertex v lands at <c>p + (bx·v.x·Sx + by·v.y·Sy + bz·(v.z·Sz + PrismZ))·bloom</c>.
        /// A dead slot is the zero matrix (drawn as a point, never as a member).
        ///
        /// Written ONCE, for two callers: the per-frame Burst job (<c>SwarmPoseJob</c>, the glue) and the managed
        /// <see cref="Matrix"/>/<see cref="Matrices"/> the harness runs (R11d). So it is Burst-compilable plain C#: scalar
        /// floats, field reads of the System.Numerics structs (no Vector3 method or operator), <see cref="KernelMath"/> only
        /// (never MathF: Burst cannot find its internal calls), no managed reference, no allocation.
        /// </summary>
        public static void PoseMatrix(in SwarmInstance s, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt,
                                      out SwarmPoseMatrix m)
        {
            m = default;
            if ((s.Flags & 1u) == 0u) return;
            float px = s.PrevPos.X + (s.CurPos.X - s.PrevPos.X) * alpha;
            float py = s.PrevPos.Y + (s.CurPos.Y - s.PrevPos.Y) * alpha;
            float pz = s.PrevPos.Z + (s.CurPos.Z - s.PrevPos.Z) * alpha;
            float fx = s.PrevFace.X + (s.CurFace.X - s.PrevFace.X) * alpha;
            float fy = s.PrevFace.Y + (s.CurFace.Y - s.PrevFace.Y) * alpha;
            float fz = s.PrevFace.Z + (s.CurFace.Z - s.PrevFace.Z) * alpha;
            float fl = KernelMath.Sqrt(fx * fx + fy * fy + fz * fz);
            if (fl < 1e-5f) { fx = s.CurFace.X; fy = s.CurFace.Y; fz = s.CurFace.Z; fl = KernelMath.Sqrt(fx * fx + fy * fy + fz * fz); }

            // SwarmBasis: bz = face, bx = up x bz (upAlt when face ~ up), by = bz x bx
            float bzx, bzy, bzz;
            if (fl > 1e-5f) { bzx = fx / fl; bzy = fy / fl; bzz = fz / fl; } else { bzx = 0f; bzy = 0f; bzz = 1f; }
            float ux = up.X, uy = up.Y, uz = up.Z;
            if (KernelMath.Abs(bzx * ux + bzy * uy + bzz * uz) > 0.98f) { ux = upAlt.X; uy = upAlt.Y; uz = upAlt.Z; }
            float bxx = uy * bzz - uz * bzy, bxy = uz * bzx - ux * bzz, bxz = ux * bzy - uy * bzx;
            float lx = KernelMath.Sqrt(bxx * bxx + bxy * bxy + bxz * bxz);
            if (lx > 1e-6f) { bxx /= lx; bxy /= lx; bxz /= lx; } else { bxx = 1f; bxy = 0f; bxz = 0f; }
            float byx = bzy * bxz - bzz * bxy, byy = bzz * bxx - bzx * bxz, byz = bzx * bxy - bzy * bxx;

            float b = Bloom(clock, s.BirthTick, bloomTicks);
            float sx = s.Scale.X * b, sy = s.Scale.Y * b, sz = s.Scale.Z * b, seat = s.PrismZ * b;
            m.C0X = bxx * sx; m.C0Y = bxy * sx; m.C0Z = bxz * sx;
            m.C1X = byx * sy; m.C1Y = byy * sy; m.C1Z = byz * sy;
            m.C2X = bzx * sz; m.C2Y = bzy * sz; m.C2Z = bzz * sz;
            m.C3X = px + bzx * seat; m.C3Y = py + bzy * seat; m.C3Z = pz + bzz * seat; m.C3W = 1f;
        }

        /// <summary><see cref="PoseMatrix"/> into <paramref name="m"/>[<paramref name="o"/> .. o+15], column-major - the
        /// managed caller (harness, entity creation).</summary>
        public static void Matrix(in SwarmInstance s, float alpha, float clock, float bloomTicks, Vector3 up, Vector3 upAlt,
                                  float[] m, int o)
        {
            PoseMatrix(s, alpha, clock, bloomTicks, up, upAlt, out var r);
            m[o + 0] = r.C0X; m[o + 1] = r.C0Y; m[o + 2] = r.C0Z; m[o + 3] = r.C0W;
            m[o + 4] = r.C1X; m[o + 5] = r.C1Y; m[o + 6] = r.C1Z; m[o + 7] = r.C1W;
            m[o + 8] = r.C2X; m[o + 9] = r.C2Y; m[o + 10] = r.C2Z; m[o + 11] = r.C2W;
            m[o + 12] = r.C3X; m[o + 13] = r.C3Y; m[o + 14] = r.C3Z; m[o + 15] = r.C3W;
        }

        /// <summary>
        /// The per-frame pass, managed: one <see cref="PoseMatrix"/> per shown slot, in <paramref name="slots"/> order, into
        /// <paramref name="m"/> (16 floats each). The game runs the SAME function per slot in a Burst job (SwarmPoseJob); this
        /// is the harness's (R11d) and the inline fallback's.
        /// </summary>
        public static void Matrices(SwarmInstance[] inst, int[] slots, int count, float alpha, float clock, float bloomTicks,
                                    Vector3 up, Vector3 upAlt, float[] m)
        {
            for (int k = 0; k < count; k++) Matrix(inst[slots[k]], alpha, clock, bloomTicks, up, upAlt, m, 16 * k);
        }
    }

    /// <summary>A body matrix, column-major, 64 bytes - field for field the layout of Unity.Mathematics.float4x4 (c0..c3,
    /// x..w), so the Burst job converts it with no shuffling. Blittable.</summary>
    public struct SwarmPoseMatrix
    {
        public float C0X, C0Y, C0Z, C0W;
        public float C1X, C1Y, C1Z, C1W;
        public float C2X, C2Y, C2Z, C2W;
        public float C3X, C3Y, C3Z, C3W;
    }

    /// <summary>What <see cref="SwarmEntryLedger"/> asks of the spatial index - PrismSpatialIndex's virtual-entry API in the
    /// glue, a model of its contract in the harness.</summary>
    public interface ISwarmEntrySink
    {
        /// <summary>RegisterVirtual + the cell's volume binding. Returns the entry id (or -1).</summary>
        int Register(int slot, Vector3 point, int domainSlot, float volume, bool shielded, float radius);
        /// <summary>Unregister.</summary>
        void Release(int id);
        /// <summary>SetVirtualSuspended.</summary>
        void SetSuspended(int id, bool suspended);
        /// <summary>UpdateCellVolume + UpdateVolume + SetVirtualRadius.</summary>
        void SetShape(int id, float volume, float radius);
        /// <summary>UpdateShieldState.</summary>
        void SetShielded(int id, bool shielded);
        /// <summary>UpdateDomain + the cell's volume binding's domain slot.</summary>
        void SetDomainSlot(int id, int domainSlot);
    }

    /// <summary>
    /// One swarm's members as spatial-index entries (Docs/SWARM_FAUNA.md §19.1). The invariant the harness asserts
    /// (R11b): after every <see cref="Sync"/>, every living member is seen by the index EXACTLY ONCE - by its live
    /// virtual entry, or (while <c>realBody[i]</c>) by its own real body prism with the virtual entry suspended - and a
    /// dead member not at all.
    /// </summary>
    public sealed class SwarmEntryLedger
    {
        public readonly int[] Ids;            // per slot: the index entry id, -1 when none (UpdatePositionsBatch's index array)
        readonly float[] _birth;              // BirthTick the entry was registered for (a reused slot is a new creature)
        readonly bool[] _suspended, _shield;
        readonly float[] _volume;
        readonly sbyte[] _dom;
        readonly int _cap;

        public int Registered { get; private set; }
        public int Suspended { get; private set; }

        public SwarmEntryLedger(int cap)
        {
            _cap = cap;
            Ids = new int[cap]; _birth = new float[cap]; _suspended = new bool[cap]; _shield = new bool[cap];
            _volume = new float[cap]; _dom = new sbyte[cap];
            for (int i = 0; i < cap; i++) Ids[i] = -1;
        }

        public bool IsSuspended(int i) => i >= 0 && i < _cap && Ids[i] >= 0 && _suspended[i];

        /// <summary>
        /// Once per published tick, AFTER the stored points of existing entries were pushed (so a resume re-files the
        /// entry where it is now). <paramref name="realBody"/>[i]: member i's own body prism is a registered prism
        /// (its proxy's body finished creation) - the virtual entry steps aside. <paramref name="points"/>: the tick's
        /// stored point per slot (<see cref="SwarmTickJob.IndexPoint"/>).
        /// </summary>
        public void Sync(SwarmInstance[] inst, bool[] realBody, Vector3[] points, ISwarmEntrySink sink)
        {
            for (int i = 0; i < _cap; i++)
            {
                ref var s = ref inst[i];
                int id = Ids[i];
                if (!s.Alive)
                {
                    if (id >= 0) Drop(i, sink);
                    continue;
                }
                if (id >= 0 && _birth[i] != s.BirthTick) { Drop(i, sink); id = -1; }   // a newborn in a reused slot

                float vol = (float)SwarmBodyPose.BodyVolume(s.Scale);
                bool shield = s.Shielded;   // not Tier == 2: a puffed shield member shows danger and stays shielded
                int dom = Math.Clamp(s.DomainSlot, 0, 2);
                if (id < 0)
                {
                    id = sink.Register(i, points[i], dom, vol, shield, SwarmBodyPose.BoundingRadius(s.Scale));
                    if (id < 0) continue;
                    Ids[i] = id; _birth[i] = s.BirthTick; _suspended[i] = false;
                    _volume[i] = vol; _shield[i] = shield; _dom[i] = (sbyte)dom;
                    Registered++;
                }
                else
                {
                    if (_volume[i] != vol) { sink.SetShape(id, vol, SwarmBodyPose.BoundingRadius(s.Scale)); _volume[i] = vol; }
                    if (_shield[i] != shield) { sink.SetShielded(id, shield); _shield[i] = shield; }
                    if (_dom[i] != dom) { sink.SetDomainSlot(id, dom); _dom[i] = (sbyte)dom; }
                }

                bool want = realBody != null && realBody[i];
                if (want != _suspended[i])
                {
                    sink.SetSuspended(id, want);
                    _suspended[i] = want;
                    Suspended += want ? 1 : -1;
                }
            }
        }

        /// <summary>Member i died (between ticks): its entry goes now - the skeleton or the eater holds its mass.</summary>
        public void Release(int i, ISwarmEntrySink sink)
        {
            if (i >= 0 && i < _cap && Ids[i] >= 0) Drop(i, sink);
        }

        /// <summary>The index materialised member i itself (an AOE hit, a ResolvePrism): it suspended the entry already.</summary>
        public void NoteSuspendedByIndex(int i)
        {
            if (i < 0 || i >= _cap || Ids[i] < 0 || _suspended[i]) return;
            _suspended[i] = true;
            Suspended++;
        }

        /// <summary>Member i's real body left the index without the member dying (its proxy retired): the entry resumes.
        /// The caller pushed a fresh stored point first.</summary>
        public void Resume(int i, ISwarmEntrySink sink)
        {
            if (i < 0 || i >= _cap || Ids[i] < 0 || !_suspended[i]) return;
            sink.SetSuspended(Ids[i], false);
            _suspended[i] = false;
            Suspended--;
        }

        /// <summary>The swarm's slot table changed (a recolour): every entry re-states its domain.</summary>
        public void RestateDomains(ISwarmEntrySink sink)
        {
            for (int i = 0; i < _cap; i++) if (Ids[i] >= 0) sink.SetDomainSlot(Ids[i], _dom[i]);
        }

        /// <summary>Every entry, released (the swarm is going away).</summary>
        public void ReleaseAll(ISwarmEntrySink sink)
        {
            for (int i = 0; i < _cap; i++) if (Ids[i] >= 0) Drop(i, sink);
        }

        void Drop(int i, ISwarmEntrySink sink)
        {
            sink.Release(Ids[i]);
            if (_suspended[i]) Suspended--;
            Ids[i] = -1; _suspended[i] = false;
            Registered--;
        }
    }

    /// <summary>
    /// One swarm's members as render entities (Docs/SWARM_FAUNA.md §19.2). Per tick, from the published frame: the slots
    /// that hold a member for the FIRST time and need an entity (<see cref="Create"/>, made in one batch), the ones to
    /// show and to hide, and the compact list of shown slots (<see cref="Shown"/>) the per-frame matrix pass walks.
    /// An entity is never destroyed while the swarm lives - a slot's next member reuses it.
    /// </summary>
    public sealed class SwarmEntityLedger
    {
        public readonly bool[] HasEntity, Visible;
        public readonly List<int> Create = new(), Show = new(), Hide = new();
        public int[] Shown;
        public int ShownCount;
        readonly int _cap;

        public SwarmEntityLedger(int cap)
        {
            _cap = cap;
            HasEntity = new bool[cap]; Visible = new bool[cap]; Shown = new int[cap];
        }

        /// <summary>Plans this tick's entity work. The caller creates <see cref="Create"/> (then <see cref="Created"/>),
        /// applies <see cref="Show"/> / <see cref="Hide"/>, and writes matrices for <see cref="Shown"/>.</summary>
        public void Sync(SwarmInstance[] inst)
        {
            Create.Clear(); Show.Clear(); Hide.Clear();
            ShownCount = 0;
            for (int i = 0; i < _cap; i++)
            {
                bool alive = inst[i].Alive;
                if (alive && !HasEntity[i]) Create.Add(i);
                if (alive != Visible[i])
                {
                    if (alive) Show.Add(i); else Hide.Add(i);
                    Visible[i] = alive;
                }
                if (alive) Shown[ShownCount++] = i;
            }
        }

        /// <summary>The caller made entities for <see cref="Create"/> (all or none: a failed batch leaves the slots to
        /// try next tick, and the caller falls back).</summary>
        public void Created(bool ok)
        {
            if (!ok) return;
            for (int k = 0; k < Create.Count; k++) HasEntity[Create[k]] = true;
        }

        /// <summary>Member i died between ticks: hide it now. Its matrix keeps being written until the next tick
        /// (the zero matrix - its Flags were cleared), which is harmless on a hidden entity.</summary>
        public bool HideNow(int i)
        {
            if (i < 0 || i >= _cap || !Visible[i]) return false;
            Visible[i] = false;
            return true;
        }
    }
}
