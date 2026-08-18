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
    /// wear) with its interior filled by prisms, which pays out when a ball threads it.
    ///
    /// <para><b>The payout is a scarab-wing DAIS.</b> A struck switch does not scatter prisms —
    /// it lays a <see cref="ScarabWingDais"/>: ten mirrored pairs of wing fans meeting all the
    /// way around the spent ring, blades growing in size along each wing and cycling
    /// base → shielded → danger along its length, with a super-shielded cube on every pair's axis
    /// standing in for the scarab's sun disc (the stellation renders it as an eight-pointed star).
    /// The rosette blooms outward over several frames — every wing's first blade, then every
    /// wing's second, and the ten suns igniting last — so the payout READS as a monument being
    /// raised rather than as mass appearing.</para>
    ///
    /// <para><b>The tiers are gameplay, not decoration</b>, and they are three different things
    /// (SCARAB.md §4.1b): a PLAIN blade is food the ball eats and pays speed for; a SHIELDED
    /// blade costs the ball no speed at all but sheds its shield and, for a forged ball, turns
    /// the shot — armour buys a redirect, not a brake; a DANGER blade is identical to a plain one
    /// from the ball's point of view and exists to punish PILOTS who fly the rosette. The sun
    /// cores are inert to the match ball and are a one-shot trade against a forged one, which
    /// dies on them and strips their super-shield. Nothing here is removed by a clock: the dais
    /// is conserved mass that only the food web, an ability, or a ball takes away.</para>
    ///
    /// Detection is plane-crossing math against <see cref="AstroLeagueBall.Live"/> (a ball passing
    /// through the ring's mouth in either direction), the shape <see cref="AstroLeagueGoal"/>
    /// already uses: no trigger collider, and no per-frame FindObjectsByType. The ring's own
    /// interior prisms are the Scarab's domain, so its OWNER's ball shields them and sails
    /// through, while an opposing ball has to eat its way in — which is what makes a switch worth
    /// placing in front of someone else's shot.
    /// </summary>
    public class ScarabSwitch : MonoBehaviour
    {
        // Vogel spiral: the golden angle is what keeps successive points from lining up into
        // spokes, so the disc fills evenly at any count.
        const float GoldenAngleRadians = 2.39996323f;

        PrismEventChannelWithReturnSO _spawnChannel;
        Domains _domain;
        string _playerName;
        Vector3 _axis, _basisU, _basisV;
        float _ringRadius;
        Vector3 _brickScale;
        float _growthRate;
        int _interiorCount;

        ScarabWingDaisSettings _dais;
        int _daisPrismsPerFrame;

        bool _shieldPrisms;
        GameObject _ring;
        readonly List<Prism> _interior = new();
        readonly List<ScarabWingDais.Element> _daisElements = new();
        readonly Dictionary<AstroLeagueBall, Vector3> _lastBallPos = new();
        readonly List<AstroLeagueBall> _scratchDead = new();
        bool _spent;

        /// <summary>Lay the ring and its interior fill. Call immediately after AddComponent.</summary>
        public void Build(PrismEventChannelWithReturnSO spawnChannel, IVesselStatus status,
                          Vector3 center, Vector3 axis, float ringRadius, Vector3 brickScale,
                          float growthRate, int interiorCount,
                          in ScarabWingDaisSettings dais, int daisPrismsPerFrame)
        {
            _spawnChannel = spawnChannel;
            _domain = status.Domain;
            _playerName = status.PlayerName;
            // MASS 5 — "Armored Switch": the switch's BODY arrives SHIELDED (regular shield, the
            // sanctioned primitive; never SuperShield). Snapshotted at placement, so a switch
            // keeps the armour it was built with even if the level drops later. Note the
            // interplay it creates with the ball rules: an OPPOSING ball now caroms off this
            // switch and sheds one shield per prism instead of eating straight through it.
            //
            // It deliberately does NOT reach the dais. The upgrade armours the switch you PLACE;
            // the rosette it pays out wears its authored base/shielded/danger cycle, because that
            // pattern IS the read — an upgrade that silently re-tiered two thirds of it would
            // make the same structure mean different things at different element levels.
            _shieldPrisms = status.ElementalAbilityHandler != null
                            && status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            _ringRadius = Mathf.Max(1f, ringRadius);
            _brickScale = brickScale;
            _growthRate = growthRate;
            _interiorCount = Mathf.Max(0, interiorCount);
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
            // § "The switch").
            _ring = ToyFactory.AddSwitchRing(transform, _ringRadius, DomainAccent(_domain));

            // Interior: a Vogel spiral inside the ring — the switch's own body.
            for (int i = 0; i < _interiorCount; i++)
            {
                var kind = _shieldPrisms ? PrismKind.Shielded : PrismKind.Plain;
                if (TryLay(SpiralPoint(i, _interiorCount, 0f, _ringRadius), InteriorRotation(),
                           _brickScale, kind, out var prism))
                    _interior.Add(prism);
            }
        }

        void BuildBasis()
        {
            _basisU = Vector3.ProjectOnPlane(Vector3.up, _axis);
            if (_basisU.sqrMagnitude < 1e-4f) _basisU = Vector3.ProjectOnPlane(Vector3.right, _axis);
            _basisU.Normalize();
            // Right-handed with the axis: basisU x basisV == axis, which ScarabWingDais assumes.
            _basisV = Vector3.Cross(_axis, _basisU).normalized;
        }

        /// <summary>
        /// Point <paramref name="i"/> of the interior spiral, mapped into the annulus
        /// [<paramref name="rInner"/>, <paramref name="rOuter"/>]. sqrt() on the normalized index
        /// is what makes the AREA density uniform — a linear radius would crowd the centre.
        /// </summary>
        Vector3 SpiralPoint(int i, int count, float rInner, float rOuter)
        {
            float t = count <= 1 ? 1f : (i + 0.5f) / count;
            float r = Mathf.Lerp(rInner * rInner, rOuter * rOuter, t);
            r = Mathf.Sqrt(r);
            float a = i * GoldenAngleRadians;
            return transform.position + (_basisU * Mathf.Cos(a) + _basisV * Mathf.Sin(a)) * r;
        }

        /// <summary>Interior bricks face along the ring axis so the fill reads as a membrane
        /// across the mouth rather than a scatter of loose blocks.</summary>
        Quaternion InteriorRotation() =>
            SafeLookRotation.TryGet(_axis, _basisU, out var rotation, this) ? rotation : Quaternion.identity;

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

        /// <summary>Struck: the switch is spent and its dais is raised.</summary>
        void Trigger(AstroLeagueBall ball)
        {
            _spent = true;

            // The ring is the switch, and the switch has been used. The interior fill it laid
            // stays as world mass (conserved — it is removed only by an active force, like any
            // other prism).
            if (_ring) Destroy(_ring);

            RaiseDaisAsync(ball != null ? ball.LastHitDomain : Domains.Blue,
                           this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Raise the rosette over several frames. Budgeted because the dais is an order of
        /// magnitude more prisms than the old outward burst (~190 at the shipped shape), and
        /// because a structure that blooms outward from the spent ring reads as a monument going
        /// up — the continuity law's preferred reading of a spawn, applied to the whole structure
        /// rather than to each prism separately.
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

            CSDebug.Log($"[ScarabSwitch] Threaded by a {struckBy} ball — dais raised with " +
                        $"{placed}/{_daisElements.Count} prisms " +
                        $"({_dais.PairCount} wing pairs, reach " +
                        $"{ScarabWingDais.OuterReach(_dais, _ringRadius):F0}u).");

            // The switch itself is done. Destroying the GameObject (not just the component) is
            // what keeps a spent switch from leaving an empty transform behind for the match.
            Destroy(gameObject);
        }

        /// <summary>Ring tint. The domain PRISM material is the eventual right answer (the toy
        /// gates take one); until that is plumbed through the action SO the ring wears a neutral
        /// accent and the interior prisms carry the domain colour.</summary>
        static Color DomainAccent(Domains domain) => domain switch
        {
            Domains.Jade => new Color(0.19f, 0.82f, 0.86f),
            Domains.Ruby => new Color(0.91f, 0.15f, 0.67f),
            Domains.Gold => new Color(0.95f, 0.75f, 0.2f),
            _ => new Color(0.8f, 0.85f, 0.9f)
        };
    }
}
