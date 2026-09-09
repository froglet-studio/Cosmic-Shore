using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A placed Scarab switch (design: R_VesselActions/SCARAB.md §5) — a low-poly ring in the
    /// toy shape language (the same <see cref="ToyFactory"/> ring the connect-the-dots gates
    /// wear), empty at the mouth, which pays out when a ball threads it.
    ///
    /// <para><b>The ring itself carries no fill.</b> An earlier revision laid a Vogel-spiral
    /// disc of prisms across the mouth at placement (and a MASS-5 "Armored Switch" upgrade built
    /// that disc from shielded prisms); both are retired (2026-08-24) — the switch now blooms in
    /// as a bare ring, and the payout below is the only prism mass a switch ever carries. See the
    /// "Superseded" note in SCARAB.md §5.1.</para>
    ///
    /// <para><b>The payout is a scarab-wing DAIS.</b> A struck switch does not scatter prisms —
    /// it lays a <see cref="ScarabWingDais"/>: super-shielded SUN CORES ringing the spent switch,
    /// each WRAPPED by a mirrored pair of wings and each aiming one of its spikes back at the
    /// switch. The wings BEGIN at this ring — their first blades are long spars running back to it
    /// — and sweep out and around their sun, closing into a C that opens away from the switch.
    /// Nothing overlaps and nothing clips, by construction. The rosette draws itself outward over
    /// several frames — every wing's first blade, then every wing's second — with the suns
    /// igniting last, so the payout READS as a monument being raised rather than as mass
    /// appearing. The mouth the switch struck through is already clear (there is no fill left
    /// to blow out), so the rosette rises around it cleanly.</para>
    ///
    /// <para><b>The tiers are gameplay AND geometry.</b> Blades alternate plain → danger, with
    /// SHIELDED octahedra capping both ends of every wing and recurring as its hinges — and that
    /// is not decoration: a plain blade's widest point is its root corner, so it stands its
    /// neighbour off by a couple of degrees however long it is, while an octahedron presents a
    /// root POINT with faces sloping away from its axis, so a flush neighbour has to stand off by
    /// that whole angle and the fan visibly opens there. To a ball the three are three different things
    /// (SCARAB.md §4.1b): a PLAIN blade is food it eats and pays speed for; a SHIELDED blade
    /// costs no speed at all but sheds its shield and, for a forged ball, turns the shot — armour
    /// buys a redirect, not a brake; a DANGER blade is identical to a plain one from the ball's
    /// point of view and exists to punish PILOTS who fly the rosette. The sun cores are inert to
    /// the match ball and are a one-shot trade against a forged one, which dies on them and
    /// strips their super-shield. Nothing here is removed by a clock: the dais is conserved mass
    /// that only the food web, an ability, or a ball takes away.</para>
    ///
    /// Detection is plane-crossing math against <see cref="AstroLeagueBall.Live"/> (a ball passing
    /// through the ring's mouth in either direction), the shape <see cref="AstroLeagueGoal"/>
    /// already uses: no trigger collider, and no per-frame FindObjectsByType. The mouth is open to
    /// any ball, owner's or opponent's — there is no fill in the way, so threading it costs
    /// nothing but the crossing.
    /// </summary>
    public class ScarabSwitch : MonoBehaviour
    {
        PrismEventChannelWithReturnSO _spawnChannel;

        Domains _domain;
        string _playerName;
        Vector3 _axis, _basisU, _basisV;
        float _ringRadius;
        float _growthRate;

        ScarabWingDaisSettings _dais;
        int _daisPrismsPerFrame;

        GameObject _ring;
        readonly List<ScarabWingDais.Element> _daisElements = new();
        readonly Dictionary<AstroLeagueBall, Vector3> _lastBallPos = new();
        readonly List<AstroLeagueBall> _scratchDead = new();
        bool _spent;

        /// <summary>The pilot who placed this switch. Empty only if Build was never called.</summary>
        public string PlacerName => _playerName;

        /// <summary>The domain this switch belongs to — the colour its ring is painted in, and
        /// (SCARAB.md §5) the side a threading pays, whoever's ball threaded it.</summary>
        public Domains PlacerDomain => _domain;

        /// <summary>Mouth radius in world units, as placed (MASS-scaled at placement time).</summary>
        public float RingRadius => _ringRadius;

        /// <summary>
        /// A ball threaded a switch's mouth. SCARAB.md §5 has always said a switch does two jobs —
        /// it deflects, and it PAYS its placer — and until now only the first half existed in code:
        /// a threading raised the dais and told nobody, so nothing outside this class could observe
        /// the event the whole ability is built around, and no mode could score it.
        ///
        /// <para>Raised on EVERY peer, because detection is per-peer (each machine runs its own
        /// crossing test against its own copy of the replicated ball) — the same reason the dais is
        /// laid on every peer rather than replicated. A subscriber that must be authoritative
        /// (anything that SCORES) has to gate on <c>IsServer</c> itself; a subscriber that is
        /// presentation (a toast, a flare) wants exactly this and should not.</para>
        ///
        /// <para>The ball argument is the one that threaded it and MAY be null on a stray
        /// crossing; the switch argument is never null and carries
        /// <see cref="PlacerName"/>/<see cref="PlacerDomain"/> — read the payer off the SWITCH,
        /// never off the ball, because "any ball pays the ring's owner" is the rule.</para>
        /// </summary>
        public static event System.Action<ScarabSwitch, AstroLeagueBall> OnThreaded;

        /// <summary>
        /// Every standing (unspent) switch in the scene, oldest first — the <c>AstroLeagueBall.Live</c>
        /// shape, and for the same reason: a mode, an AI and a HUD marker all need "the nearest ring
        /// of my domain" and none of them should be running <c>FindObjectsByType</c> to get it.
        /// A switch joins on <see cref="Build"/> (not Awake, so its domain is already known and no
        /// reader can ever see a Blue one) and leaves the instant it is spent or retired, ahead of
        /// its own destruction, so nothing is ever steered at a ring that has already paid out.
        /// </summary>
        public static readonly List<ScarabSwitch> Live = new();

        /// <summary>Lay the ring. Call immediately after AddComponent.</summary>
        public void Build(PrismEventChannelWithReturnSO spawnChannel, IVesselStatus status,
                          Vector3 center, Vector3 axis, float ringRadius,
                          float growthRate, in ScarabWingDaisSettings dais, int daisPrismsPerFrame,
                          ThemeManagerDataContainerSO theme = null)
        {
            _spawnChannel = spawnChannel;
            _domain = status.Domain;
            _playerName = status.PlayerName;
            _ringRadius = Mathf.Max(1f, ringRadius);
            _growthRate = growthRate;
            _dais = dais;
            _daisPrismsPerFrame = Mathf.Max(1, daisPrismsPerFrame);

            transform.position = center;
            _axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            BuildBasis();

            // The ring reads as a portal you thread, so it faces along the placement axis.
            if (SafeLookRotation.TryGet(_axis, _basisU, out var rot, this))
                transform.rotation = rot;

            // The same builder every freestyle toy's ring comes from: this IS the switch the toy
            // rings borrow their meaning from, and CrossedMouth tests exactly _ringRadius, so the
            // ring is its trigger volume drawn at its own radius (Docs/ToySystem/ARCHITECTURE.md
            // § "The switch"). The mouth is left empty — no interior fill (see the class doc
            // comment) — so the ring blooms in on its own.
            //
            // Painted in the PRISM shader like every switch, wearing this switch's DOMAIN — whose
            // colour it is decides who it pays (SCARAB.md §5), and it is the one domain-coloured
            // switch that does not hand you a domain. Nothing in this mode changes a pilot's
            // domain, so the two readings never share a screen; see ToySwitchSignal.Domain.
            // The theme comes from the executor (which is DI-injected on the vessel), so the ring
            // is the SAME live material asset the dais prisms below are laid in — this class
            // therefore carries no per-domain palette of its own.
            _ring = ToyFactory.AddSwitchRing(transform, _ringRadius, theme,
                                             ToySwitchSignal.Domain, _domain);

            // Joins the roster only now: everything above is what a reader would ask about.
            if (!Live.Contains(this)) Live.Add(this);
        }

        // A scene unload destroys switches without spending them; a stale entry would outlive
        // the scene and be handed to the next match's readers.
        void OnDestroy() => Live.Remove(this);

        void BuildBasis()
        {
            _basisU = Vector3.ProjectOnPlane(Vector3.up, _axis);
            if (_basisU.sqrMagnitude < 1e-4f) _basisU = Vector3.ProjectOnPlane(Vector3.right, _axis);
            _basisU.Normalize();
            // Right-handed with the axis: basisU x basisV == axis, which ScarabWingDais assumes.
            _basisV = Vector3.Cross(_axis, _basisU).normalized;
        }

        /// <summary>
        /// Lay one prism of this switch. The ordering below is the whole contract and every step
        /// of it is load-bearing:
        ///
        /// <list type="number">
        /// <item><b>No occupancy reservation.</b> <c>PrismSpatialIndex.TryReserve</c> exists for a
        /// GROWTH decision ("may I grow here?"), and it answers from each peer's OWN live prism
        /// set. A switch is re-built independently on every peer from a replicated input event,
        /// so a reservation is a per-peer VETO on an authored structure: peers with different
        /// local mass around the ring end up with different prisms in different places, forever,
        /// because nothing here is replicated. <c>BoostRingBuilder</c> — the reference structure
        /// builder — deliberately does not reserve either.</item>
        /// <item><b><c>ChangeTeam</c>, never the <c>Domain</c> setter.</b> The setter routes to
        /// <c>SetInitialTeam</c>, which is a NO-OP on a prism whose team is already non-Blue —
        /// and a pooled prism arrives carrying its previous life's colour.</item>
        /// <item><b>Kind flags cleared before <c>Initialize</c>.</b> The Interactive pool path
        /// does not reset them (the Boost path does), and <c>Initialize</c> re-engages whatever
        /// it finds — so a recycled prism would arrive wearing its last life's tier.</item>
        /// <item><b><c>AdmitTargetScale</c> AFTER <c>Initialize</c>, not before.</b>
        /// <c>Initialize</c> → <c>ResetState</c> → <c>RestoreAuthoredScaleWindow()</c> undoes any
        /// widening and then re-clamps the target against the restored window, so a size stated
        /// before <c>Initialize</c> is silently trimmed. The interactive pool's window is
        /// (0.5,0.5,0.5)..(40,10,10) and the dais states blades ~38 long and 0.33 thin, so this
        /// is the difference between the authored rosette and a pile of 10-unit stubs.</item>
        /// <item><b>Tier applied AFTER <c>Initialize</c></b> via <c>PrismKinds</c>, the one
        /// helper that owns the state machine. During the birth window it snaps rather than
        /// animating, which is the continuity law's reading of a spawn.</item>
        /// </list>
        /// </summary>
        bool TryLay(Vector3 pos, Quaternion rotation, Vector3 scale, PrismKind kind, out Prism prism)
        {
            prism = null;
            if (_spawnChannel == null) return false;

            var ret = _spawnChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = _domain,
                Rotation = rotation,
                SpawnPosition = pos,
                Scale = scale,
                Velocity = Vector3.zero,
                PrismType = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out prism)) return false;

            prism.ChangeTeam(_domain);
            prism.ownerID = $"{_playerName}::Switch::{GetInstanceID()}";

            // Pool reuse: start from a known-plain prism so Initialize cannot re-engage a stale tier.
            if (prism.prismProperties != null)
            {
                prism.prismProperties.IsShielded = false;
                prism.prismProperties.IsDangerous = false;
                prism.prismProperties.speedDebuffAmount = 0f;
            }

            prism.Initialize(_playerName);

            // AUTHORED size — widen the window first, then state it (see the doc comment above).
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            // The one growth engine (Docs/PRISM_ANIMATION.md) — bloom in on the clock.
            prism.SetGrowthRate(_growthRate);

            PrismKinds.Apply(prism, kind);
            return true;
        }

        void Update()
        {
            if (_spent) return;

            var live = AstroLeagueBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (ball == null) continue;

                Vector3 cur = ball.transform.position;
                if (!_lastBallPos.TryGetValue(ball, out var prev))
                {
                    _lastBallPos[ball] = cur;
                    continue;   // need two samples to test a crossing
                }
                _lastBallPos[ball] = cur;

                if (!CrossedMouth(prev, cur)) continue;

                Trigger(ball);
                return;
            }

            // A ball that died leaves its last sample behind. An unstruck switch lives for the
            // whole match, so prune when the book is bigger than the world it describes.
            if (_lastBallPos.Count > live.Count) PruneDeadBalls(live);
        }

        void PruneDeadBalls(IReadOnlyList<AstroLeagueBall> live)
        {
            _scratchDead.Clear();
            foreach (var key in _lastBallPos.Keys)
            {
                bool alive = false;
                for (int i = 0; i < live.Count && !alive; i++) alive = ReferenceEquals(live[i], key);
                if (!alive) _scratchDead.Add(key);
            }
            for (int i = 0; i < _scratchDead.Count; i++) _lastBallPos.Remove(_scratchDead[i]);
        }

        /// <summary>Did the segment prev→cur cross the ring's plane INSIDE the mouth? Direction
        /// agnostic: threading a switch backwards is still threading it.</summary>
        bool CrossedMouth(Vector3 prev, Vector3 cur)
        {
            Vector3 c = transform.position;
            float dPrev = Vector3.Dot(prev - c, _axis);
            float dCur = Vector3.Dot(cur - c, _axis);
            if (dPrev * dCur > 0f) return false;         // same side of the plane — no crossing
            if (Mathf.Approximately(dPrev, dCur)) return false;

            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            Vector3 hit = Vector3.Lerp(prev, cur, t);
            Vector3 rel = hit - c;
            Vector3 lateral = rel - Vector3.Dot(rel, _axis) * _axis;
            return lateral.sqrMagnitude <= _ringRadius * _ringRadius;
        }

        /// <summary>Struck: the switch is spent, it pays, and its dais is raised.</summary>
        void Trigger(AstroLeagueBall ball)
        {
            _spent = true;
            Live.Remove(this);

            // The ring is the switch, and the switch has been used.
            if (_ring) Destroy(_ring);

            // Announce BEFORE the dais goes up and before this GameObject is destroyed, so a
            // subscriber can still read the switch. Raised inside a try so one throwing listener
            // cannot cost this switch its payout — the dais below is conserved mass the player
            // earned, and a mode's scoring bug must not silently eat it.
            try { OnThreaded?.Invoke(this, ball); }
            catch (System.Exception e)
            {
                CSDebug.LogError($"[ScarabSwitch] A ScarabSwitch.OnThreaded listener threw; the " +
                                 $"dais is still being raised. {e}");
            }

            RaiseDaisAsync(ball != null ? ball.LastHitDomain : Domains.Blue,
                           this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Retire this switch UNSPENT — no ball threaded it, so it pays no dais. Called when its
        /// placer puts one switch too many into the world (<see cref="PlaceSwitchActionExecutor"/>
        /// enforces a per-pilot ceiling): the removal is caused by that placement, never by a
        /// clock, which is the same shape as the ball's cell overload. Nothing conserved is lost —
        /// a standing switch is a generated ring mesh, not prisms — but continuity of existence
        /// still applies to anything a player can see, so the ring SHRINKS away rather than
        /// blinking out.
        /// </summary>
        public void Retire(float seconds)
        {
            if (_spent) return;      // already threaded, or already retiring
            _spent = true;
            Live.Remove(this);
            RetireAsync(Mathf.Max(0.05f, seconds), this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RetireAsync(float seconds, CancellationToken ct)
        {
            var ring = _ring ? _ring.transform : null;
            Vector3 from = ring ? ring.localScale : Vector3.one;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (!ring) break;
                ring.localScale = Vector3.Lerp(from, Vector3.zero, Mathf.Clamp01(t / seconds));
                // Sequencing only, never thread marshaling (Docs/THREADING.md).
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                        "[ScarabSwitch] Retired unspent — its placer stood one switch too many.");
            Destroy(gameObject);
        }

        /// <summary>
        /// Raise the rosette over several frames. Budgeted because the dais is an order of
        /// magnitude more prisms than the old outward burst (255 at the shipped shape), and
        /// because a structure that draws itself outward from the spent ring reads as a monument
        /// going up — the continuity law's preferred reading of a spawn, applied to the whole
        /// structure rather than to each prism separately.
        /// </summary>
        async UniTaskVoid RaiseDaisAsync(Domains struckBy, CancellationToken ct)
        {
            ScarabWingDais.Generate(_dais, transform.position, _axis, _basisU, _basisV,
                                    _ringRadius, _daisElements);

            int placed = 0;
            for (int i = 0; i < _daisElements.Count; i++)
            {
                var e = _daisElements[i];
                if (TryLay(e.Position, e.Rotation, e.Scale, e.Kind, out _)) placed++;

                if ((i + 1) % _daisPrismsPerFrame != 0) continue;
                // Sequencing only, never thread marshaling (Docs/THREADING.md). Cancellation is
                // the switch being destroyed, so the teardown below is moot when it fires.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                        $"[ScarabSwitch] Threaded by a {struckBy} ball — dais raised with " +
                        $"{placed}/{_daisElements.Count} prisms " +
                        $"({_dais.PairCount} wing pairs, reach " +
                        $"{ScarabWingDais.OuterReach(_dais, _ringRadius):F0}u).");

            // The switch itself is done. Destroying the GameObject (not just the component) is
            // what keeps a spent switch from leaving an empty transform behind for the match.
            Destroy(gameObject);
        }
    }
}
