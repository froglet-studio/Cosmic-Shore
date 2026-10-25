using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hijack's replicated prism-state table: who owns every prism in the Switchyard and whether
    /// it is shielded, as the SERVER says, mirrored on every peer. The networking (the RPCs)
    /// lives on <see cref="HijackController"/>, which is the mode's only NetworkBehaviour; this
    /// class is the bookkeeping, kept plain C# so the controller stays readable.
    ///
    /// <para><b>Why the mode needs it.</b> Platform prism ownership
    /// (<see cref="PrismTeamManager"/>) and shield state (<see cref="PrismStateManager"/>) are
    /// local: a steal or a shield mutates fields on whichever machine ran it. Every peer builds a
    /// byte-identical yard (the generator is closed form) and then diverged from the first steal,
    /// so ride speed, the objective arrow and the AI's rail choice - all three of which read
    /// ownership - disagreed between machines. Shield state decides what a steal DOES
    /// (<c>PrismTeamManager.Steal</c> drops a shield instead of flipping a shielded prism, and
    /// refuses a super-shielded one outright), so a shield that existed on one machine only made
    /// the same pass a steal there and a shield-break elsewhere. This table is SCOPED TO THE
    /// YARD: it watches only the Switchyard's own trails (18 burrs + 24 rails) and changes nothing
    /// about how a prism changes hands or armours anywhere else.</para>
    ///
    /// <para><b>Who is believed.</b> A change seen on a machine is AUTHORITATIVE there when that
    /// machine simulates the pilot who could have caused it - the same "each machine accounts for
    /// the players it simulates" rule <c>StatsManager.OwnsAttacker</c> applies to the score,
    /// applied to the prism instead of the stat:
    /// <list type="bullet">
    /// <item>a FLIP is caused by a pilot of the NEW domain;</item>
    /// <item>a SHIELD (or super-shield) GAINED with no flip is caused by the OWNER - the Urchin's
    /// Mass-5 ride armours only its own mass (<c>GunVesselTransformer.ApplyPrismscapePayoff</c>);</item>
    /// <item>a shield DROPPED with no flip is caused by a RIVAL of the owner - a steal that met a
    /// shield.</item>
    /// </list>
    /// On the server that means the host's own pilot and every AI; on a client, its own pilot.
    /// The server records its own authoritative changes straight into the table; a client reports
    /// its own and the server accepts a report only when the SENDER (identity from RPC ownership,
    /// never a name) could have caused it. Every other change - a remote pilot's proxy grinding a
    /// rail on this machine - is provisional: it is left on screen for <c>graceSeconds</c> so the
    /// real change has time to arrive, then put back to whatever the table says. The table always
    /// wins.</para>
    ///
    /// <para><b>The steal-against-shield race.</b> A client can flip a prism it has not yet
    /// heard is shielded. The server applies the shield rule to the TABLE, not to the client's
    /// screen: a reported flip of a table-shielded prism lands as a shield-break (owner kept,
    /// shield gone), a reported flip of a table-super-shielded prism is refused, and either way
    /// the sender is answered with the table's value and converges. A client that DID see the
    /// shield and broke it first says so (<see cref="ViaBreakBit"/>), so its legitimate
    /// break-then-steal is not mistaken for the race when both land in one flush.</para>
    ///
    /// <para>Known approximation: a domain is the unit of trust, not a pilot, so a peer that
    /// simulates a pilot of domain D also believes its PROXY of a remote teammate in D, and a
    /// peer that simulates any rival of D believes a proxy's break of a D shield. Each proxy
    /// replays its owner's real ride, so this can make a real change land early, never hand a
    /// prism to the wrong team.</para>
    ///
    /// <para><b>Bandwidth.</b> Nothing is sent per frame. A change is one packed int
    /// (state | slot | index, the state byte carrying domain AND shield), coalesced per key into
    /// an outbox the controller flushes on a short cadence, so a 100-prism spike cascade is one
    /// ~400-byte message. A late joiner pulls one snapshot of every prism whose state has EVER
    /// changed - the rest are still what the closed-form generator laid, which its own copy of
    /// the yard already shows.</para>
    ///
    /// <para><b>Detection.</b> A flip raises <see cref="PrismTeamManager.OnTeamChanged"/> and is
    /// seen the instant it happens. A shield change raises nothing, so the ledger SWEEPS the
    /// yard's discovered prisms for it, <c>shieldSweepBudget</c> prisms a frame round-robin -
    /// two bool reads each, so a full 9,930-prism yard is ~3 frames at the default budget, well
    /// inside the flush cadence the change waits for anyway.</para>
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
        /// <summary>A prism's armour, as the table carries it. Values are the wire encoding -
        /// never renumber.</summary>
        public enum Shield : byte
        {
            None = 0,
            Shielded = 1,
            SuperShielded = 2,
        }

        // Packed entry:  bits 0-15 index | 16-23 slot | 24-31 state byte.
        // State byte:    bits 0-3 domain | 4-5 shield | 6 via-break (client report only) | 7 zero,
        //                so a packed entry is never negative.
        const int SlotShift = 16;
        const int StateShift = 24;
        const int IndexMask = 0xFFFF;
        const int SlotMask = 0xFF;
        const int KeyMask = 0xFFFFFF;

        const int DomainMask = 0x0F;
        const int ShieldShift = 4;
        const int ShieldMask = 0x03;
        /// <summary>Client report flag: "I saw this prism shielded and broke the shield before
        /// I took it". Never stored in the table and never broadcast.</summary>
        internal const int ViaBreakBit = 0x40;
        const int StoredStateMask = 0x3F;

        public const int MaxSlots = SlotMask + 1;
        public const int MaxIndex = IndexMask + 1;

        static readonly ProfilerMarker s_sweepMarker = new("HijackOwnershipLedger.SweepShields");

        sealed class Slot
        {
            public Trail Trail;
            public readonly List<PrismTeamManager> Teams = new();
            public readonly List<Prism> Prisms = new();
            public readonly List<Action<Domains, Domains>> Handlers = new();
            /// <summary>The table's answer for each discovered prism (a state byte): as laid
            /// until the server says otherwise.</summary>
            public readonly List<byte> Authoritative = new();
            /// <summary>The shield this ledger last saw on the LOCAL prism - the sweep's
            /// baseline, so it reports a change once, not every frame.</summary>
            public readonly List<Shield> SeenShield = new();
        }

        readonly bool _isServer;
        readonly Func<Domains, bool> _simulatesPilotOf;
        readonly Func<Domains, bool> _simulatesRivalOf;
        readonly float _graceSeconds;
        readonly int _sweepBudget;

        /// <summary>Every key the server has ever set. Server: the late-join snapshot. Client:
        /// holds keys for prisms not laid yet.</summary>
        readonly Dictionary<int, byte> _table = new();
        /// <summary>Provisional changes waiting to be put back, key → deadline.</summary>
        readonly Dictionary<int, float> _pendingReverts = new();
        /// <summary>Coalesced changes to send: server → broadcast, client → report.</summary>
        readonly Dictionary<int, byte> _outbox = new();
        readonly Dictionary<Trail, int> _slotOfTrail = new();
        readonly List<int> _scratch = new();

        Slot[] _slots = Array.Empty<Slot>();
        int _discovered;
        int _sweepSlot;
        int _sweepIndex;
        float _now;
        bool _applying;
        bool _warnedShrink;

        public HijackYard Yard { get; private set; }
        public bool IsBound => _slots.Length > 0;

        /// <param name="simulatesPilotOf">Does this machine simulate a pilot of the domain?</param>
        /// <param name="simulatesRivalOf">Does this machine simulate a pilot of any OTHER
        /// playable domain - someone who could have broken a shield the domain's owner laid?</param>
        /// <param name="shieldSweepBudget">Prisms checked for a shield change per tick.</param>
        public HijackOwnershipLedger(bool isServer, Func<Domains, bool> simulatesPilotOf,
                                     Func<Domains, bool> simulatesRivalOf, float graceSeconds,
                                     int shieldSweepBudget)
        {
            _isServer = isServer;
            _simulatesPilotOf = simulatesPilotOf;
            _simulatesRivalOf = simulatesRivalOf;
            _graceSeconds = Mathf.Max(0f, graceSeconds);
            _sweepBudget = Mathf.Max(1, shieldSweepBudget);
        }

        // ── Packing ──────────────────────────────────────────────────────────

        static int KeyOf(int slot, int index) => (slot << SlotShift) | index;

        internal static byte StateOf(Domains domain, Shield shield) =>
            (byte)(((int)domain & DomainMask) | (((int)shield & ShieldMask) << ShieldShift));

        internal static Domains DomainOfState(byte state) => (Domains)(state & DomainMask);
        internal static Shield ShieldOfState(byte state) => (Shield)((state >> ShieldShift) & ShieldMask);

        /// <summary>One wire entry. <paramref name="state"/> is a state byte, optionally with
        /// <see cref="ViaBreakBit"/>.</summary>
        internal static int Pack(int slot, int index, byte state) =>
            ((state & 0x7F) << StateShift) | ((slot & SlotMask) << SlotShift) | (index & IndexMask);

        internal static void Unpack(int packed, out int slot, out int index, out byte state)
        {
            index = packed & IndexMask;
            slot = (packed >> SlotShift) & SlotMask;
            state = (byte)((packed >> StateShift) & 0x7F);
        }

        static bool IsPlayableDomain(Domains d) =>
            d == Domains.Jade || d == Domains.Ruby || d == Domains.Gold;

        /// <summary>A state byte a peer may store: a playable domain, a defined shield tier, no
        /// stray bits.</summary>
        static bool IsValidState(byte state) =>
            (state & ~StoredStateMask) == 0
            && IsPlayableDomain(DomainOfState(state))
            && ShieldOfState(state) <= Shield.SuperShielded;

        /// <summary>The armour a prism shows on THIS machine.</summary>
        internal static Shield LocalShield(Prism prism)
        {
            // Reference check, not Unity's null: the sweep runs this ~4k times a frame, and a
            // pooled prism's managed properties outlive its native side harmlessly.
            if (prism is null) return Shield.None;
            var props = prism.prismProperties;
            if (props is null) return Shield.None;
            if (props.IsSuperShielded) return Shield.SuperShielded;
            return props.IsShielded ? Shield.Shielded : Shield.None;
        }

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
            _discovered = 0;
            _sweepSlot = 0;
            _sweepIndex = 0;
            Yard = null;
        }

        /// <summary>Slot of a burr / rail by its index in the yard's lists.</summary>
        public static int BurrSlot(int burr) => burr;
        public static int RailSlot(HijackYard yard, int rail) => yard.Burrs.Count + rail;

        public int SlotOf(Trail trail) =>
            trail != null && _slotOfTrail.TryGetValue(trail, out var s) ? s : -1;

        /// <summary>The table's owner of one prism, if this peer has discovered it.</summary>
        public bool TryRead(int slot, int index, out Domains domain) =>
            TryReadState(slot, index, out domain, out _);

        /// <summary>The table's owner AND armour of one prism, if this peer has discovered it.</summary>
        public bool TryReadState(int slot, int index, out Domains domain, out Shield shield)
        {
            if (TryReadByte(slot, index, out var state))
            {
                domain = DomainOfState(state);
                shield = ShieldOfState(state);
                return true;
            }
            domain = Domains.Blue;
            shield = Shield.None;
            return false;
        }

        bool TryReadByte(int slot, int index, out byte state)
        {
            if ((uint)slot < (uint)_slots.Length)
            {
                var list = _slots[slot].Authoritative;
                if ((uint)index < (uint)list.Count)
                {
                    state = list[index];
                    return true;
                }
            }
            state = 0;
            return false;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        public void Tick(float now)
        {
            _now = now;
            Discover();
            SweepShields();
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
                if (TryReadByte(slot, index, out var state)) ApplyLocal(slot, index, state);
            }
        }

        /// <summary>Pick up prisms laid since the last frame: record their laid state, apply
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
                    var laidShield = LocalShield(prism);
                    byte laid = StateOf(team ? team.Domain : Domains.Blue, laidShield);
                    int key = KeyOf(s, i);
                    byte owner = _table.TryGetValue(key, out var held) ? held : laid;

                    int slotIndex = s, prismIndex = i;
                    Action<Domains, Domains> handler = (_, to) => OnLocalFlip(slotIndex, prismIndex, to);

                    slot.Teams.Add(team);
                    slot.Prisms.Add(prism);
                    slot.Handlers.Add(handler);
                    slot.Authoritative.Add(owner);
                    slot.SeenShield.Add(laidShield);
                    _discovered++;

                    if (team)
                    {
                        team.OnTeamChanged += handler;
                        if (owner != laid) ApplyLocal(s, i, owner);
                    }
                }
            }
        }

        /// <summary>
        /// Shield changes raise no event, so look for them: <c>_sweepBudget</c> discovered prisms
        /// per tick, round-robin, comparing each prism's local armour to the last one seen.
        /// </summary>
        void SweepShields()
        {
            if (_discovered == 0 || _slots.Length == 0) return;

            using (s_sweepMarker.Auto())
            {
                int budget = Math.Min(_sweepBudget, _discovered);
                // Bounded so a pass of empty slots cannot spin: at most one wrap past each slot.
                int emptySteps = _slots.Length + 1;
                while (budget > 0 && emptySteps > 0)
                {
                    if (_sweepSlot >= _slots.Length) { _sweepSlot = 0; _sweepIndex = 0; }
                    var slot = _slots[_sweepSlot];
                    if (_sweepIndex >= slot.Prisms.Count)
                    {
                        _sweepSlot++;
                        _sweepIndex = 0;
                        emptySteps--;
                        continue;
                    }
                    emptySteps = _slots.Length + 1;

                    int i = _sweepIndex++;
                    budget--;
                    var current = LocalShield(slot.Prisms[i]);
                    if (current == slot.SeenShield[i]) continue;

                    slot.SeenShield[i] = current;
                    var team = slot.Teams[i];
                    if (!team) continue;
                    OnLocalChange(_sweepSlot, i, StateOf(team.Domain, current));
                }
            }
        }

        void OnLocalFlip(int slot, int index, Domains to)
        {
            if (_applying) return;
            if ((uint)slot >= (uint)_slots.Length) return;
            var s = _slots[slot];
            if ((uint)index >= (uint)s.Prisms.Count) return;

            // The flip carries the prism's armour as it stands now (a super-steal keeps it), and
            // the sweep must not report the same change a second time.
            var shield = LocalShield(s.Prisms[index]);
            s.SeenShield[index] = shield;
            OnLocalChange(slot, index, StateOf(to, shield));
        }

        /// <summary>The local prism now shows <paramref name="next"/>. Believe it, report it, or
        /// schedule it to be put back.</summary>
        void OnLocalChange(int slot, int index, byte next)
        {
            if (_applying) return;

            // A torn-down yard returns its prisms to the environment pool before this ledger is
            // unbound; a reused prism is no longer this table's business.
            if ((uint)slot >= (uint)_slots.Length) return;
            var list = _slots[slot].Trail?.TrailList;
            if (list == null || index >= list.Count) return;
            if (!TryReadByte(slot, index, out var prev)) return;

            int key = KeyOf(slot, index);
            if (next == prev)
            {
                // Back in step with the table (a provisional change undone by the game itself).
                _pendingReverts.Remove(key);
                return;
            }

            if (IsAuthoritativeHere(prev, next))
            {
                _pendingReverts.Remove(key);
                if (_isServer)
                {
                    SetAuthoritative(slot, index, next);
                    _outbox[key] = next;
                }
                else
                {
                    // This client only ever flips a prism its TABLE says is shielded by
                    // breaking that shield first - the ledger keeps the prism showing the table.
                    bool viaBreak = ShieldOfState(prev) != Shield.None
                                    && DomainOfState(next) != DomainOfState(prev);
                    _outbox[key] = viaBreak ? (byte)(next | ViaBreakBit) : next;
                }
            }
            else if (!_pendingReverts.ContainsKey(key))
            {
                _pendingReverts[key] = _now + _graceSeconds;
            }
        }

        /// <summary>Could a pilot this machine simulates have turned <paramref name="prev"/> into
        /// <paramref name="next"/>?</summary>
        bool IsAuthoritativeHere(byte prev, byte next)
        {
            var nextDomain = DomainOfState(next);
            if (nextDomain != DomainOfState(prev)) return _simulatesPilotOf(nextDomain);
            if (ShieldOfState(next) != Shield.None) return _simulatesPilotOf(nextDomain);
            return _simulatesRivalOf(nextDomain);
        }

        void SetAuthoritative(int slot, int index, byte state)
        {
            _table[KeyOf(slot, index)] = state;
            if ((uint)slot < (uint)_slots.Length && (uint)index < (uint)_slots[slot].Authoritative.Count)
                _slots[slot].Authoritative[index] = state;
        }

        /// <summary>Make the local prism show <paramref name="state"/> - owner first, then
        /// armour, so a shield change binds the new owner's material - without re-reporting it.</summary>
        void ApplyLocal(int slot, int index, byte state)
        {
            if ((uint)slot >= (uint)_slots.Length) return;
            var s = _slots[slot];
            if ((uint)index >= (uint)s.Teams.Count) return;
            var team = s.Teams[index];
            if (!team) return;

            var domain = DomainOfState(state);
            var shield = ShieldOfState(state);
            var prism = s.Prisms[index];

            _applying = true;
            try
            {
                if (team.Domain != domain) team.ChangeTeam(domain);

                if (prism && LocalShield(prism) != shield)
                {
                    switch (shield)
                    {
                        case Shield.Shielded: prism.ActivateShield(); break;
                        case Shield.SuperShielded: prism.ActivateSuperShield(); break;
                        default: prism.DeactivateShields(); break;
                    }
                }
            }
            finally
            {
                _applying = false;
                // Whatever the prism ACTUALLY shows now is the sweep's baseline - including the
                // case where it could not take the armour - so this apply is never re-reported.
                s.SeenShield[index] = LocalShield(prism);
            }
        }

        // ── Wire ─────────────────────────────────────────────────────────────

        /// <summary>Drains the outbox as packed entries, or null when there is nothing to send.</summary>
        public int[] TakeOutbox()
        {
            if (_outbox.Count == 0) return null;
            var packed = new int[_outbox.Count];
            int n = 0;
            foreach (var kv in _outbox)
                packed[n++] = Pack((kv.Key >> SlotShift) & SlotMask, kv.Key & IndexMask, kv.Value);
            _outbox.Clear();
            return packed;
        }

        /// <summary>CLIENT: the server's word - a broadcast delta or a late-join snapshot chunk.</summary>
        public void ApplyAuthoritative(int[] packed)
        {
            if (packed == null) return;
            for (int n = 0; n < packed.Length; n++)
            {
                Unpack(packed[n], out int slot, out int index, out byte state);
                state &= StoredStateMask;
                if (!IsValidState(state)) continue;
                _pendingReverts.Remove(KeyOf(slot, index));
                SetAuthoritative(slot, index, state);
                ApplyLocal(slot, index, state);
            }
        }

        /// <summary>
        /// SERVER: a client's own changes. Each is accepted only if the SENDER could have caused
        /// it (a flip into its own domain, a shield on its own mass, a shield broken on a
        /// rival's), and the shield rule is applied against the TABLE: a flip of a prism the
        /// table holds shielded lands as a shield-break unless the sender says it broke that
        /// shield first, and a flip or break of a super-shielded prism is refused. Anything not
        /// taken as sent is answered with the table's value so the sender converges.
        /// </summary>
        public void AcceptClientReports(int[] packed, Domains senderDomain)
        {
            if (packed == null) return;
            for (int n = 0; n < packed.Length; n++)
            {
                Unpack(packed[n], out int slot, out int index, out byte raw);
                if ((uint)slot >= (uint)_slots.Length) continue;
                int key = KeyOf(slot, index);
                bool viaBreak = (raw & ViaBreakBit) != 0;
                byte reported = (byte)(raw & StoredStateMask);

                bool known = TryReadByte(slot, index, out var prev);
                if (!known) prev = _table.TryGetValue(key, out var held) ? held : (byte)0;

                if (!IsValidState(reported) || !TryResolveReport(prev, reported, viaBreak, senderDomain, out var next))
                {
                    if (known) _outbox[key] = prev;
                    continue;
                }

                _pendingReverts.Remove(key);
                SetAuthoritative(slot, index, next);
                ApplyLocal(slot, index, next);
                // Broadcast what landed - which is also the sender's correction when the shield
                // rule turned its steal into a break.
                _outbox[key] = next;
            }
        }

        /// <summary>
        /// The table's verdict on one client report: <paramref name="next"/> is what the prism
        /// becomes, or false when nothing changes. Pure, so the harness can pin every case.
        /// </summary>
        internal static bool TryResolveReport(byte prev, byte reported, bool viaBreak,
                                              Domains sender, out byte next)
        {
            next = prev;
            var prevDomain = DomainOfState(prev);
            var prevShield = ShieldOfState(prev);
            var domain = DomainOfState(reported);
            var shield = ShieldOfState(reported);

            if (domain != prevDomain)
            {
                // A steal - only ever INTO the sender's own colour.
                if (domain != sender) return false;
                // Nothing a pilot carries breaks a super-shield (PrismTeamManager.Steal returns).
                if (prevShield == Shield.SuperShielded) return false;
                // The sender flipped a prism it had not heard was shielded: the shield takes the
                // hit, exactly as Steal would have done had the sender known.
                if (prevShield == Shield.Shielded && !viaBreak)
                {
                    next = StateOf(prevDomain, Shield.None);
                    return true;
                }
                next = reported;
                return true;
            }

            if (shield == prevShield) return false;

            if (shield != Shield.None)
            {
                // Armour is laid by the owner, on the owner's own mass.
                if (domain != sender) return false;
                next = reported;
                return true;
            }

            // A break: a rival's steal met the shield. A super-shield is never broken this way.
            if (domain == sender || prevShield == Shield.SuperShielded) return false;
            next = reported;
            return true;
        }

        /// <summary>SERVER: every prism whose state has ever changed, for a late joiner.</summary>
        public int[] BuildSnapshot()
        {
            var packed = new int[_table.Count];
            int n = 0;
            foreach (var kv in _table)
            {
                int key = kv.Key & KeyMask;
                packed[n++] = Pack((key >> SlotShift) & SlotMask, key & IndexMask, kv.Value);
            }
            return packed;
        }
    }
}
