using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hijack's replicated prism-ownership table: who owns every prism in the Switchyard, as the
    /// SERVER says, mirrored on every peer. The networking (the RPCs) lives on
    /// <see cref="HijackController"/>, which is the mode's only NetworkBehaviour; this class is
    /// the bookkeeping, kept plain C# so the controller stays readable.
    ///
    /// <para><b>Why the mode needs it.</b> Platform prism ownership
    /// (<see cref="PrismTeamManager"/>) is local: a steal mutates a field on whichever machine
    /// ran the steal. Every peer builds a byte-identical yard (the generator is closed form) and
    /// then diverged from the first steal, so ride speed, the objective arrow and the AI's rail
    /// choice - all three of which read ownership - disagreed between machines. This table is
    /// SCOPED TO THE YARD: it watches only the Switchyard's own trails (18 burrs + 24 rails) and
    /// changes nothing about how a prism changes hands anywhere else.</para>
    ///
    /// <para><b>Who is believed.</b> A flip seen on a machine is AUTHORITATIVE there when that
    /// machine simulates a pilot of the flip's NEW domain - on the server, the host's own pilot
    /// and every AI; on a client, its own pilot. That is the same "each machine accounts for the
    /// players it simulates" rule <c>StatsManager.OwnsAttacker</c> applies to the score, applied
    /// to the prism instead of the stat. The server records its own authoritative flips straight
    /// into the table; a client reports its own and the server accepts a report only in the
    /// SENDER's domain (identity from RPC ownership, never a name). Every other flip - a remote
    /// pilot's proxy grinding a rail on this machine - is provisional: it is left on screen for
    /// <c>graceSeconds</c> so the real change has time to arrive, then put back to whatever the
    /// table says. The table always wins.</para>
    ///
    /// <para>Known approximation: a domain is the unit of trust, not a pilot, so a peer that
    /// simulates a pilot of domain D also believes its PROXY of a remote teammate in D. Both
    /// would flip the prism the same way, so it can only make a teammate's steal land early,
    /// never hand a prism to the wrong team.</para>
    ///
    /// <para><b>Bandwidth.</b> Nothing is sent per frame. A change is one packed int
    /// (domain | slot | index), coalesced per key into an outbox the controller flushes on a
    /// short cadence, so a 100-prism spike cascade is one ~400-byte message. A late joiner pulls
    /// one snapshot of every prism that has EVER changed hands - the rest are still the colour
    /// the closed-form generator painted them, which its own copy of the yard already shows.</para>
    ///
    /// <para><b>Addressing.</b> A prism is (slot, index): slot is the trail - every burr in
    /// <see cref="HijackYard.Burrs"/> order, then every rail in <see cref="HijackYard.Rails"/>
    /// order - and index is its position in that trail's <c>TrailList</c>, which is append-only
    /// for an environment trail and laid in the same order on every peer. A key can arrive
    /// before this peer has laid the prism it names (a late joiner's yard is still building);
    /// it waits in the table and is applied the moment the prism appears.</para>
    /// </summary>
    internal sealed class HijackOwnershipLedger
    {
        const int SlotShift = 16;
        const int DomainShift = 24;
        const int IndexMask = 0xFFFF;
        const int SlotMask = 0xFF;
        const int KeyMask = 0xFFFFFF;

        public const int MaxSlots = SlotMask + 1;
        public const int MaxIndex = IndexMask + 1;

        sealed class Slot
        {
            public Trail Trail;
            public readonly List<PrismTeamManager> Teams = new();
            public readonly List<Action<Domains, Domains>> Handlers = new();
            /// <summary>The table's answer for each discovered prism: painted colour until the
            /// server says otherwise.</summary>
            public readonly List<byte> Authoritative = new();
        }

        readonly bool _isServer;
        readonly Func<Domains, bool> _isLocallyAuthoritative;
        readonly float _graceSeconds;

        /// <summary>Every key the server has ever set. Server: the late-join snapshot. Client:
        /// holds keys for prisms not laid yet.</summary>
        readonly Dictionary<int, byte> _table = new();
        /// <summary>Provisional flips waiting to be put back, key → deadline.</summary>
        readonly Dictionary<int, float> _pendingReverts = new();
        /// <summary>Coalesced changes to send: server → broadcast, client → report.</summary>
        readonly Dictionary<int, byte> _outbox = new();
        readonly Dictionary<Trail, int> _slotOfTrail = new();
        readonly List<int> _scratch = new();

        Slot[] _slots = Array.Empty<Slot>();
        bool _applying;
        bool _warnedShrink;

        public HijackYard Yard { get; private set; }
        public bool IsBound => _slots.Length > 0;

        public HijackOwnershipLedger(bool isServer, Func<Domains, bool> isLocallyAuthoritative,
                                     float graceSeconds)
        {
            _isServer = isServer;
            _isLocallyAuthoritative = isLocallyAuthoritative;
            _graceSeconds = Mathf.Max(0f, graceSeconds);
        }

        // ── Packing ──────────────────────────────────────────────────────────

        static int KeyOf(int slot, int index) => (slot << SlotShift) | index;
        static int Pack(int key, byte domain) => (domain << DomainShift) | key;

        static void Unpack(int packed, out int slot, out int index, out byte domain)
        {
            index = packed & IndexMask;
            slot = (packed >> SlotShift) & SlotMask;
            domain = (byte)((packed >> DomainShift) & 0x7F);
        }

        static bool IsPlayableDomain(byte d) =>
            d == (byte)Domains.Jade || d == (byte)Domains.Ruby || d == (byte)Domains.Gold;

        // ── Binding to the yard ──────────────────────────────────────────────

        public void Bind(HijackYard yard)
        {
            Unbind();
            Yard = yard;
            if (yard == null) return;

            int burrs = yard.Burrs.Count;
            int count = burrs + yard.Rails.Count;
            if (count > MaxSlots)
            {
                CSDebug.LogError($"[Hijack] Yard has {count} trails; the ownership table addresses " +
                                 $"at most {MaxSlots}. Ownership will not replicate.", yard);
                return;
            }

            _slots = new Slot[count];
            for (int s = 0; s < count; s++)
            {
                var trail = s < burrs ? yard.Burrs[s].Trail : yard.Rails[s - burrs].Trail;
                _slots[s] = new Slot { Trail = trail };
                if (trail != null) _slotOfTrail[trail] = s;
            }
        }

        public void Unbind()
        {
            for (int s = 0; s < _slots.Length; s++)
            {
                var slot = _slots[s];
                for (int i = 0; i < slot.Teams.Count; i++)
                    if (slot.Teams[i]) slot.Teams[i].OnTeamChanged -= slot.Handlers[i];
            }
            _slots = Array.Empty<Slot>();
            _slotOfTrail.Clear();
            _pendingReverts.Clear();
            Yard = null;
        }

        /// <summary>Slot of a burr / rail by its index in the yard's lists.</summary>
        public static int BurrSlot(int burr) => burr;
        public static int RailSlot(HijackYard yard, int rail) => yard.Burrs.Count + rail;

        public int SlotOf(Trail trail) =>
            trail != null && _slotOfTrail.TryGetValue(trail, out var s) ? s : -1;

        /// <summary>The table's owner of one prism, if this peer has discovered it.</summary>
        public bool TryRead(int slot, int index, out Domains domain)
        {
            if ((uint)slot < (uint)_slots.Length)
            {
                var list = _slots[slot].Authoritative;
                if ((uint)index < (uint)list.Count)
                {
                    domain = (Domains)list[index];
                    return true;
                }
            }
            domain = Domains.Blue;
            return false;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        public void Tick(float now)
        {
            Discover();
            if (_pendingReverts.Count == 0) return;

            _scratch.Clear();
            foreach (var kv in _pendingReverts)
                if (kv.Value <= now) _scratch.Add(kv.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                int key = _scratch[i];
                _pendingReverts.Remove(key);
                int slot = (key >> SlotShift) & SlotMask;
                int index = key & IndexMask;
                if (TryRead(slot, index, out var owner)) ApplyLocal(slot, index, (byte)owner);
            }
        }

        /// <summary>Pick up prisms laid since the last frame: record their painted colour, apply
        /// anything the table already holds for them, and start watching them. A trail laid
        /// over several frames is picked up as it fills.</summary>
        void Discover()
        {
            for (int s = 0; s < _slots.Length; s++)
            {
                var slot = _slots[s];
                var list = slot.Trail?.TrailList;
                if (list == null) continue;

                if (list.Count < slot.Teams.Count)
                {
                    // An environment trail never shrinks during a match; if one ever does, the
                    // indices no longer address the same prisms on every peer. Say so once.
                    if (!_warnedShrink)
                    {
                        _warnedShrink = true;
                        CSDebug.LogWarning("[Hijack] A yard trail shrank mid-match; prism " +
                                           "ownership indices for it are no longer reliable.");
                    }
                    continue;
                }

                for (int i = slot.Teams.Count; i < list.Count && i < MaxIndex; i++)
                {
                    var prism = list[i];
                    var team = prism ? prism.GetComponent<PrismTeamManager>() : null;
                    byte painted = (byte)(team ? team.Domain : Domains.Blue);
                    int key = KeyOf(s, i);
                    byte owner = _table.TryGetValue(key, out var held) ? held : painted;

                    int slotIndex = s, prismIndex = i;
                    Action<Domains, Domains> handler = (_, to) => OnLocalFlip(slotIndex, prismIndex, to);

                    slot.Teams.Add(team);
                    slot.Handlers.Add(handler);
                    slot.Authoritative.Add(owner);

                    if (team)
                    {
                        team.OnTeamChanged += handler;
                        if (owner != painted) ApplyLocal(s, i, owner);
                    }
                }
            }
        }

        void OnLocalFlip(int slot, int index, Domains to)
        {
            if (_applying) return;

            // A torn-down yard returns its prisms to the environment pool before this ledger is
            // unbound; a reused prism is no longer this table's business.
            if ((uint)slot >= (uint)_slots.Length) return;
            var list = _slots[slot].Trail?.TrailList;
            if (list == null || index >= list.Count) return;

            int key = KeyOf(slot, index);
            if (_isLocallyAuthoritative(to))
            {
                _pendingReverts.Remove(key);
                if (_isServer) SetAuthoritative(slot, index, (byte)to);
                _outbox[key] = (byte)to;
            }
            else if (!_pendingReverts.ContainsKey(key))
            {
                _pendingReverts[key] = Time.time + _graceSeconds;
            }
        }

        void SetAuthoritative(int slot, int index, byte domain)
        {
            _table[KeyOf(slot, index)] = domain;
            if ((uint)slot < (uint)_slots.Length && (uint)index < (uint)_slots[slot].Authoritative.Count)
                _slots[slot].Authoritative[index] = domain;
        }

        /// <summary>Make the local prism show <paramref name="domain"/> without re-reporting it.</summary>
        void ApplyLocal(int slot, int index, byte domain)
        {
            if ((uint)slot >= (uint)_slots.Length) return;
            var teams = _slots[slot].Teams;
            if ((uint)index >= (uint)teams.Count) return;
            var team = teams[index];
            if (!team || (byte)team.Domain == domain) return;

            _applying = true;
            try { team.ChangeTeam((Domains)domain); }
            finally { _applying = false; }
        }

        // ── Wire ─────────────────────────────────────────────────────────────

        /// <summary>Drains the outbox as packed entries, or null when there is nothing to send.</summary>
        public int[] TakeOutbox()
        {
            if (_outbox.Count == 0) return null;
            var packed = new int[_outbox.Count];
            int n = 0;
            foreach (var kv in _outbox) packed[n++] = Pack(kv.Key, kv.Value);
            _outbox.Clear();
            return packed;
        }

        /// <summary>CLIENT: the server's word - a broadcast delta or a late-join snapshot chunk.</summary>
        public void ApplyAuthoritative(int[] packed)
        {
            if (packed == null) return;
            for (int n = 0; n < packed.Length; n++)
            {
                Unpack(packed[n], out int slot, out int index, out byte domain);
                if (!IsPlayableDomain(domain)) continue;
                _pendingReverts.Remove(KeyOf(slot, index));
                SetAuthoritative(slot, index, domain);
                ApplyLocal(slot, index, domain);
            }
        }

        /// <summary>
        /// SERVER: a client's own steals. Accepted only in the sender's domain - a client can
        /// only ever have stolen INTO its own colour - and a rejected entry is answered with the
        /// table's current value so the sender converges instead of staying wrong.
        /// </summary>
        public void AcceptClientReports(int[] packed, Domains senderDomain)
        {
            if (packed == null) return;
            for (int n = 0; n < packed.Length; n++)
            {
                Unpack(packed[n], out int slot, out int index, out byte domain);
                if ((uint)slot >= (uint)_slots.Length) continue;
                int key = KeyOf(slot, index);

                if (domain != (byte)senderDomain || !IsPlayableDomain(domain))
                {
                    if (TryRead(slot, index, out var current)) _outbox[key] = (byte)current;
                    continue;
                }

                _pendingReverts.Remove(key);
                SetAuthoritative(slot, index, domain);
                ApplyLocal(slot, index, domain);
                _outbox[key] = domain;
            }
        }

        /// <summary>SERVER: every prism that has ever changed hands, for a late joiner.</summary>
        public int[] BuildSnapshot()
        {
            var packed = new int[_table.Count];
            int n = 0;
            foreach (var kv in _table) packed[n++] = Pack(kv.Key & KeyMask, kv.Value);
            return packed;
        }
    }
}
