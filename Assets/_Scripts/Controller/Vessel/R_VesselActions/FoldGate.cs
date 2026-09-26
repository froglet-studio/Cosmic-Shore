using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One end of a Butterfly's <b>fold gate</b> pair — a standing portal the Fold leaves behind
    /// (design: <c>R_VesselActions/BUTTERFLY_FOLD.md</c> §6). Every fold opens TWO: one where the
    /// vessel left and one where it arrived. Thread either one and you are at the other.
    ///
    /// <para><b>It is a SWITCH, and it is a DOMAIN switch.</b> A ring you thread is the platform's
    /// one word for "this activates something", so the gate is the ordinary
    /// <see cref="ToyFactory.AddSwitchRing"/> ring drawn at its own trigger radius — the ring IS
    /// the volume, never an advertisement for a bigger one. It wears the placer's DOMAIN because
    /// the colour says who may use it, the second wearer outside the toybox after
    /// <see cref="ScarabSwitch"/> and for the same reason: it BELONGS to a domain rather than
    /// handing you one. Nothing about a gate changes a pilot's domain, so the two readings of a
    /// domain-coloured ring never share a screen.</para>
    ///
    /// <para><b>The Butterfly is the only hull that can place one, and every hull of its domain can
    /// use one.</b> That is the point: the fleet's slowest ship cannot out-fly anybody, and what it
    /// can do instead is leave a shortcut standing that its whole team keeps. A gate is therefore
    /// the Butterfly's contribution to a TEAM rather than to its own lap.</para>
    ///
    /// <para><b>Nothing removes a gate but the pilot who placed it.</b> A pair stands until that
    /// Butterfly folds again and the new pair replaces it — an ACTIVE, explicit player act, the
    /// same class of removal as a cell swap or a Scarab standing one switch too many. There is no
    /// lifespan, no decay and no idle culler here, and there must never be one: that is the timed
    /// culler the platform rejects, wearing a portal's costume.</para>
    ///
    /// <para><b>Who detects, who moves.</b> Each machine tests only the vessels it OWNS, and the
    /// owner writes its own pose — which replicates. So a transit needs no new networking, cannot
    /// double-fire across peers, and cannot be decided for you by somebody else's frame. The
    /// GEOMETRY agrees across machines for free, because neither end is placed from a stick
    /// reading: the origin gate is laid at a vessel that has been stopped and replicated for the
    /// whole hold, and the destination gate at the vessel's replicated pose once it has arrived.</para>
    ///
    /// <para><b>A transit is a TELEPORT and every watcher already knows it</b>, because
    /// <c>IVessel.SetPose</c> bumps <c>VesselTransformer.TeleportCount</c>. So a gate cannot thread
    /// a race ring (<c>GateRaceController</c> declines any step containing a teleport) — the rule
    /// Waystation needed for the Fold covers the gates with nothing added.</para>
    ///
    /// <para><b>Stated judgement: the VESSEL is not withered on a transit, the GATES flare.</b> The
    /// Fold itself withers and blooms the hull because a teleport out of open space is a
    /// disappearance with nothing to explain it. A gate transit is not that — the pilot flies INTO
    /// a visible ring and OUT of a visible ring, and the rings are the continuity. Withering the
    /// hull at both ends would also put a quarter-second of dead time on a movement option meant to
    /// be flown through at speed. If a playtest reads it as a pop, the fix is to route the transit
    /// through the same wither/bloom the Fold already owns rather than to add a second one.</para>
    /// </summary>
    public class FoldGate : MonoBehaviour
    {
        /// <summary>
        /// Every standing gate, oldest first — the <see cref="ScarabSwitch.Live"/> shape, and for
        /// the same reason: an AI, a HUD marker or a mode wanting "the nearest gate of my domain"
        /// must not be running <c>FindObjectsByType</c> to get it. A gate joins on
        /// <see cref="Build"/> rather than Awake, so no reader can ever see one before it knows its
        /// domain, and leaves ahead of its own destruction.
        /// </summary>
        public static readonly List<FoldGate> Live = new();

        Domains _domain;
        string _placerName = string.Empty;
        Vector3 _axis = Vector3.forward;
        float _radius = 1f;
        float _exitClearance;
        float _bloomSeconds = 0.35f;

        FoldGate _partner;
        GameObject _ring;
        IReadOnlyList<IPlayer> _players;

        bool _retiring;
        float _bloom;

        readonly Dictionary<IVessel, Vector3> _lastPos = new();
        readonly HashSet<IVessel> _armed = new();
        readonly List<IVessel> _scratchDead = new();

        /// <summary>The pilot who folded this pair into being.</summary>
        public string PlacerName => _placerName;

        /// <summary>The domain that may thread it — and the colour its ring is painted in.</summary>
        public Domains PlacerDomain => _domain;

        /// <summary>Mouth radius in world units. The ring is drawn at exactly this.</summary>
        public float RingRadius => _radius;

        /// <summary>The other end. Null until <see cref="Pair"/> runs, and a gate with no partner
        /// is inert rather than broken: the origin gate stands alone for the length of the fold's
        /// wither and arrival, and threading it in that window must do nothing rather than send a
        /// pilot to a destination that does not exist yet.</summary>
        public FoldGate Partner => _partner;

        /// <summary>Lay the ring. Call immediately after AddComponent, as ScarabSwitch does.</summary>
        public void Build(IVesselStatus placer, IReadOnlyList<IPlayer> players,
                          Vector3 centre, Vector3 axis, float radius,
                          float exitClearance, float bloomSeconds,
                          ThemeManagerDataContainerSO theme)
        {
            _domain = placer != null ? placer.Domain : Domains.Blue;
            _placerName = placer != null ? placer.PlayerName : string.Empty;
            _players = players;
            _radius = Mathf.Max(1f, radius);
            _exitClearance = Mathf.Max(0f, exitClearance);
            _bloomSeconds = Mathf.Max(0.01f, bloomSeconds);

            transform.position = centre;
            _axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;

            // A ring faces along its axis; the antipode guard is why this is SafeLookRotation and
            // not LookRotation, which is undefined when up is parallel to forward and invents a
            // pose rather than saying so.
            if (SafeLookRotation.TryGet(_axis, out var rot, this)) transform.rotation = rot;

            _ring = ToyFactory.AddSwitchRing(transform, _radius, theme,
                                             ToySwitchSignal.Domain, _domain);
            _bloom = 0f;
            ApplyBloom();

            if (!Live.Contains(this)) Live.Add(this);
        }

        /// <summary>Join the two ends. Symmetric, so one call wires both.</summary>
        public static void Pair(FoldGate a, FoldGate b)
        {
            if (!a || !b || a == b) return;
            a._partner = b;
            b._partner = a;
        }

        // A scene unload destroys gates without retiring them; a stale entry would outlive the
        // scene and be handed to the next match's readers.
        void OnDestroy() => Live.Remove(this);

        /// <summary>
        /// Wither this gate away over <paramref name="seconds"/> and destroy it. The ONLY removal
        /// path, and it is always caused by its placer folding again — never by a clock. It leaves
        /// the roster immediately so nothing is steered at a gate that is already closing, and it
        /// unpairs its partner so a half-retired pair can never send a pilot into a gate that is
        /// mid-wither.
        /// </summary>
        public void Retire(float seconds)
        {
            if (_retiring) return;
            _retiring = true;
            Live.Remove(this);
            if (_partner && _partner._partner == this) _partner._partner = null;
            _partner = null;
            RetireAsync(Mathf.Max(0.05f, seconds), this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RetireAsync(float seconds, CancellationToken ct)
        {
            float from = _bloom;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _bloom = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / seconds));
                ApplyBloom();
                // Sequencing only, never thread marshaling (Docs/THREADING.md).
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            CSDebug.LogVerbose(CSLogChannel.ButterflyFold,
                $"[FoldGate] {_placerName}'s gate closed — replaced by a new fold.");
            Destroy(gameObject);
        }

        void Update()
        {
            // Settles toward 1 from BOTH directions - the bloom rises into it and a flare's
            // overshoot falls back to it. One clause, so a flare can never leave a ring stuck
            // oversized the way a rise-only settle would.
            if (!_retiring && !Mathf.Approximately(_bloom, 1f))
            {
                _bloom = Mathf.MoveTowards(_bloom, 1f, Time.deltaTime / _bloomSeconds);
                ApplyBloom();
            }

            if (_retiring || _partner == null || _players == null) return;

            for (int i = 0; i < _players.Count; i++)
            {
                var vessel = _players[i]?.Vessel;
                if (vessel == null) continue;

                // Only the machine that OWNS a vessel decides that vessel moved: the pose write
                // replicates, so a peer acting too would be two machines teleporting one ship.
                if (!vessel.IsNetworkOwner) continue;

                var status = vessel.VesselStatus;
                if (status?.Player == null || status.Domain != _domain) continue;

                Vector3 cur = vessel.Transform.position;
                bool first = !_lastPos.TryGetValue(vessel, out var prev);
                _lastPos[vessel] = cur;

                // ARMING. A vessel may only be taken by a gate it has been clear of. Without
                // this the very first thing every fold does is teleport the pilot back: the
                // destination gate is laid AROUND them, so flying out of their own arrival ring
                // crosses its plane and sends them home. It is a geometric latch rather than a
                // timer - "you got clear of this gate" is exactly the fact that matters, it has
                // no number of its own, and a system whose whole rule is that nothing runs on a
                // clock should not gate its own detector on one.
                if (!_armed.Contains(vessel))
                {
                    if (!InNearZone(cur)) _armed.Add(vessel);
                    continue;
                }
                if (first) continue;               // two samples are needed to test a crossing
                if (!CrossedMouth(prev, cur, out Vector3 hit)) continue;

                Transit(vessel, status, prev, cur, hit);
                return;                            // one transit per gate per frame
            }

            // A despawned vessel leaves its samples behind, and a gate outlives several of them.
            if (_lastPos.Count > _players.Count) PruneDead();
        }

        void PruneDead()
        {
            _scratchDead.Clear();
            foreach (var key in _lastPos.Keys)
            {
                bool alive = false;
                for (int i = 0; i < _players.Count && !alive; i++)
                    alive = _players[i] != null && ReferenceEquals(_players[i].Vessel, key);
                if (!alive) _scratchDead.Add(key);
            }
            for (int i = 0; i < _scratchDead.Count; i++)
            {
                _lastPos.Remove(_scratchDead[i]);
                _armed.Remove(_scratchDead[i]);
            }
        }

        bool InNearZone(Vector3 p) => FoldGateGeometry.InNearZone(
            p, transform.position, _axis, _radius, _exitClearance);

        bool CrossedMouth(Vector3 prev, Vector3 cur, out Vector3 hit) =>
            FoldGateGeometry.CrossedMouth(prev, cur, transform.position, _axis, _radius, out hit);

        /// <summary>
        /// Put the pilot through. Two properties are preserved on purpose, because together they
        /// are what makes a portal predictable rather than a shuffle:
        ///
        /// <list type="bullet">
        /// <item><b>Where in the mouth you entered is where you leave.</b> The lateral offset
        /// inside this ring is re-applied inside the partner's, so threading near the rim comes out
        /// near the rim.</item>
        /// <item><b>The side you were heading for is the side you come out on.</b> The exit is one
        /// clearance along the shared axis in the SENSE you were travelling, so momentum reads
        /// through the gate and a transit never spits a pilot backwards.</item>
        /// </list>
        ///
        /// <para>The pilot's ROTATION and SPEED are untouched. A gate moves you; it does not fly
        /// you.</para>
        ///
        /// <para>Both ends are then locked out for this vessel and re-seeded at the exit. The
        /// re-seed is the load-bearing half: without it the partner's very next sample is a segment
        /// running from the pilot's old position to the exit, which crosses its plane and would
        /// bounce them straight back. That is a detector debounce, not a lifespan — nothing is
        /// removed by it.</para>
        /// </summary>
        void Transit(IVessel vessel, IVesselStatus status, Vector3 prev, Vector3 cur, Vector3 hit)
        {
            var partner = _partner;
            if (!partner) return;

            Vector3 exit = FoldGateGeometry.Exit(hit, cur - prev,
                                                 transform.position, _axis,
                                                 partner.transform.position, partner._axis,
                                                 _exitClearance);

            vessel.SetPose(new Pose(exit, vessel.Transform.rotation));

            // Both ends are re-seeded at the exit and DISARMED. The re-seed alone is not enough:
            // the far gate deposits the pilot one clearance from its own plane, so it has to
            // treat them as somebody standing in its mouth - which is what disarming says - until
            // they have flown clear of it.
            _lastPos[vessel] = exit;
            partner._lastPos[vessel] = exit;
            _armed.Remove(vessel);
            partner._armed.Remove(vessel);

            Flare();
            partner.Flare();

            CSDebug.LogVerbose(CSLogChannel.ButterflyFold,
                $"[FoldGate] {status.PlayerName} threaded {_placerName}'s gate " +
                $"({Vector3.Distance(transform.position, partner.transform.position):F0}u).");
        }

        /// <summary>A visible acknowledgement at BOTH ends — the ring punches and settles. It is
        /// what says a transit happened to everyone except the pilot, who was already looking
        /// somewhere else by then.</summary>
        void Flare()
        {
            if (!_ring || _retiring) return;
            _bloom = 1.25f;
            ApplyBloom();
        }

        void ApplyBloom()
        {
            if (!_ring) return;
            _ring.transform.localScale = Vector3.one * (_radius * 2f * Mathf.Max(0f, _bloom));
        }
    }
}
