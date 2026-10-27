using System;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Records the prism world of a match into a <see cref="TheaterWorld"/> (Theater P1) by watching
    /// <see cref="PrismSpatialIndex"/>, the one place every real prism registers, dies, comes back
    /// and moves. Nothing is hooked into <see cref="Prism"/> itself.
    ///
    /// <para><b>Ids, not slots.</b> The index reuses a slot as soon as its prism is gone, so each
    /// birth takes a fresh recording id and the slot only maps to it until the prism is gone.
    /// A prism that is destroyed and later restored keeps its id. The index can report a death twice
    /// (destroyed, then unregistered when it returns to the pool); the second is dropped here.</para>
    ///
    /// <para><b>Moves are thinned.</b> Swarm fauna move their body prisms every tick; recording every
    /// one would grow the file by megabytes a minute. A move is kept when the prism has gone
    /// <see cref="MoveMinSeconds"/> and <see cref="MoveMinDistance"/> since its last kept pose, or
    /// jumped <see cref="MoveJumpDistance"/> at once; playback holds the last kept pose between.</para>
    ///
    /// <para><b>Bounded.</b> Past <see cref="MaxEvents"/> the recorder stops taking births and moves
    /// (deaths are still kept, so nothing recorded lingers forever) and says so once.</para>
    ///
    /// <para>Costs nothing while not attached: the index pays one null check per call. Attached, it
    /// pays an array write per event and nothing per frame.</para>
    /// </summary>
    public sealed class TheaterWorldRecorder : IPrismLifeObserver
    {
        public float MoveMinSeconds = 0.2f;
        public float MoveMinDistance = 1f;
        public float MoveJumpDistance = 8f;
        public bool RecordMoves = true;
        public int MaxEvents = 250000;

        struct SlotLife
        {
            public int Id;          // recording id + 1; 0 = no live prism recorded in this slot
            public bool Gone;       // destroyed, waiting for restore or unregister
            public float LastTime;
            public Vector3 LastPosition;
            public Quaternion LastRotation;
            public Vector3 Scale;
            public byte Domain;
            public byte Flags;
        }

        /// <summary>A virtual member's box edge from its bounding radius (a cube's half-diagonal is edge * sqrt(3) / 2).</summary>
        const float RadiusToEdge = 1.1547f;

        /// <summary>A virtual member with no reach recorded is drawn as a box this big.</summary>
        const float VirtualDefaultEdge = 1f;

        readonly TheaterWorld _world = new();
        SlotLife[] _slots = new SlotLife[1024];
        Prism[] _prisms = new Prism[1024];
        int _nextId;
        bool _capped;

        public TheaterWorld World => _world;
        public bool IsAttached { get; private set; }

        /// <summary>
        /// Start recording: take every prism standing now as a birth, then watch the index.
        /// False when there is no prism index in this scene (nothing to record) or another observer
        /// already holds it.
        /// </summary>
        public bool Attach()
        {
            if (IsAttached) return true;
            var index = PrismSpatialIndex.Instance;
            if (!index) return false;
            if (PrismSpatialIndex.LifeObserver != null) return false;

            _world.Events.Clear();
            Array.Clear(_slots, 0, _slots.Length);
            Array.Clear(_prisms, 0, _prisms.Length);
            _nextId = 0;
            _capped = false;

            index.VisitLive(this);
            PrismSpatialIndex.LifeObserver = this;
            IsAttached = true;
            return true;
        }

        public void Detach()
        {
            if (!IsAttached) return;
            if (PrismSpatialIndex.LifeObserver == this) PrismSpatialIndex.LifeObserver = null;
            IsAttached = false;
            // The world keeps its events; only the live references go.
            Array.Clear(_prisms, 0, _prisms.Length);
        }

        public void OnPrismBorn(int slot, Prism prism)
        {
            if (!prism || slot < 0 || !Room()) return;
            Ensure(slot);
            // A birth on a slot still holding a recorded prism means its death was never reported:
            // close that life first so the playback does not keep a prism that left.
            if (_slots[slot].Id != 0 && !_slots[slot].Gone) Add(new TheaterWorldEvent { Kind = TheaterWorldEventKind.Gone, Time = Time.time, Id = _slots[slot].Id - 1 });

            int id = _nextId++;
            _slots[slot] = new SlotLife { Id = id + 1 };
            _prisms[slot] = prism;
            AddPose(TheaterWorldEventKind.Born, slot, id, prism);
        }

        public void OnPrismGone(int slot)
        {
            if (slot < 0 || slot >= _slots.Length) return;
            ref var life = ref _slots[slot];
            if (life.Id == 0) return;
            if (!life.Gone)
                Add(new TheaterWorldEvent { Kind = TheaterWorldEventKind.Gone, Time = Time.time, Id = life.Id - 1 });
            life.Gone = true;

            // Destroyed prisms stay in their slot and can be restored; an unregistered one cannot.
            // Both arrive here, so the slot is only freed when the index frees it - a later birth on
            // the same slot (OnPrismBorn) takes a new id either way.
        }

        public void OnPrismRestored(int slot, Prism prism)
        {
            if (slot < 0 || slot >= _slots.Length) return;
            ref var life = ref _slots[slot];
            if (life.Id == 0 || !life.Gone) return;
            if (!Room()) return;
            life.Gone = false;
            if (prism)
            {
                _prisms[slot] = prism;
                AddPose(TheaterWorldEventKind.Restored, slot, life.Id - 1, prism);
                return;
            }

            // A virtual member: back where it was last recorded, the next move puts it right.
            life.LastTime = Time.time;
            Add(new TheaterWorldEvent
            {
                Kind = TheaterWorldEventKind.Restored, Time = Time.time, Id = life.Id - 1,
                Position = life.LastPosition, Rotation = life.LastRotation, Scale = life.Scale,
                Domain = life.Domain, Flags = life.Flags
            });
        }

        public void OnVirtualBorn(int slot, Vector3 position, Domains domain, float radius, bool shielded)
        {
            if (slot < 0 || !Room()) return;
            Ensure(slot);
            if (_slots[slot].Id != 0 && !_slots[slot].Gone) Add(new TheaterWorldEvent { Kind = TheaterWorldEventKind.Gone, Time = Time.time, Id = _slots[slot].Id - 1 });

            int id = _nextId++;
            float edge = radius > 0f ? radius * RadiusToEdge : VirtualDefaultEdge;
            var e = new TheaterWorldEvent
            {
                Kind = TheaterWorldEventKind.Born, Time = Time.time, Id = id,
                Position = position, Rotation = Quaternion.identity, Scale = new Vector3(edge, edge, edge),
                Domain = (byte)domain, Flags = shielded ? TheaterWorld.FlagShielded : (byte)0
            };
            _slots[slot] = new SlotLife
            {
                Id = id + 1, LastTime = e.Time, LastPosition = position, LastRotation = e.Rotation,
                Scale = e.Scale, Domain = e.Domain, Flags = e.Flags
            };
            _prisms[slot] = null;
            Add(e);
        }

        public void OnPrismMoved(int slot, Vector3 position)
        {
            if (!RecordMoves || slot < 0 || slot >= _slots.Length) return;
            ref var life = ref _slots[slot];
            if (life.Id == 0 || life.Gone) return;

            float now = Time.time;
            float sq = (position - life.LastPosition).sqrMagnitude;
            bool jumped = sq >= MoveJumpDistance * MoveJumpDistance;
            bool drifted = now - life.LastTime >= MoveMinSeconds && sq >= MoveMinDistance * MoveMinDistance;
            if (!jumped && !drifted) return;
            if (!Room()) return;

            var prism = _prisms[slot];
            life.LastTime = now;
            life.LastPosition = position;
            if (prism) life.LastRotation = prism.transform.rotation;
            Add(new TheaterWorldEvent
            {
                Kind = TheaterWorldEventKind.Moved, Time = now, Id = life.Id - 1,
                Position = position, Rotation = life.LastRotation
            });
        }

        void AddPose(TheaterWorldEventKind kind, int slot, int id, Prism prism)
        {
            var t = prism.transform;
            var e = new TheaterWorldEvent
            {
                Kind = kind, Time = Time.time, Id = id,
                Position = t.position, Rotation = t.rotation, Scale = Size(prism),
                Domain = (byte)prism.Domain, Flags = FlagsOf(prism)
            };
            ref var life = ref _slots[slot];
            life.LastTime = e.Time;
            life.LastPosition = e.Position;
            life.LastRotation = e.Rotation;
            life.Scale = e.Scale;
            life.Domain = e.Domain;
            life.Flags = e.Flags;
            Add(e);
        }

        /// <summary>The prism's world-space box: its scale times its mesh's size, so playback draws a unit cube.</summary>
        static Vector3 Size(Prism prism)
        {
            Vector3 scale = prism.transform.lossyScale;
            if (prism.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                scale = Vector3.Scale(scale, filter.sharedMesh.bounds.size);
            return scale;
        }

        static byte FlagsOf(Prism prism)
        {
            var p = prism.prismProperties;
            if (p == null) return 0;
            byte flags = 0;
            if (p.IsShielded) flags |= TheaterWorld.FlagShielded;
            if (p.IsSuperShielded) flags |= TheaterWorld.FlagSuperShielded;
            if (p.IsDangerous) flags |= TheaterWorld.FlagDangerous;
            return flags;
        }

        bool Room()
        {
            if (_world.Events.Count < MaxEvents) return true;
            if (!_capped)
            {
                _capped = true;
                CSDebug.LogWarning($"[Theater] World recording reached its {MaxEvents} event cap; new prisms are no longer recorded this match.");
            }
            return false;
        }

        void Add(TheaterWorldEvent e) => _world.Events.Add(e);

        void Ensure(int slot)
        {
            if (slot < _slots.Length) return;
            int size = _slots.Length;
            while (size <= slot) size *= 2;
            Array.Resize(ref _slots, size);
            Array.Resize(ref _prisms, size);
        }
    }
}
