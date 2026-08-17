using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A placed Scarab switch (design: R_VesselActions/SCARAB.md §5) — a low-poly ring in the
    /// toy shape language (the same <see cref="ToyFactory"/> ring the connect-the-dots gates
    /// wear) with its interior filled by prisms, which pays out when a ball threads it.
    ///
    /// The fill and the payout are ONE pattern, not two. Prism positions come from a single
    /// Vogel (sunflower) spiral: indices <c>[0, interiorCount)</c> land inside the ring and are
    /// laid at placement; indices <c>[interiorCount, interiorCount + burstCount)</c> continue the
    /// same spiral OUTWARD and are laid when the switch is struck. So the burst is literally the
    /// pattern carrying on past the ring's edge rather than a second effect that happens to look
    /// similar — same spiral, same spacing, same phase.
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
        int _interiorCount, _burstCount;
        float _burstRadiusMultiplier;
        float _clearRadius;

        bool _shieldPrisms;
        GameObject _ring;
        readonly List<Prism> _interior = new();
        readonly Dictionary<AstroLeagueBall, Vector3> _lastBallPos = new();
        bool _spent;

        /// <summary>Lay the ring and its interior fill. Call immediately after AddComponent.</summary>
        public void Build(PrismEventChannelWithReturnSO spawnChannel, IVesselStatus status,
                          Vector3 center, Vector3 axis, float ringRadius, Vector3 brickScale,
                          float growthRate, int interiorCount, int burstCount,
                          float burstRadiusMultiplier)
        {
            _spawnChannel = spawnChannel;
            _domain = status.Domain;
            _playerName = status.PlayerName;
            // MASS 5 — "Armored Switch": the switch's prisms arrive SHIELDED (regular shield, the
            // sanctioned primitive; never SuperShield). Snapshotted at placement, so a switch
            // keeps the armour it was built with even if the level drops later. Note the
            // interplay it creates with the ball rules: an OPPOSING ball now caroms off this
            // switch and sheds one shield per prism instead of eating straight through it.
            _shieldPrisms = status.ElementalAbilityHandler != null
                            && status.ElementalAbilityHandler.IsUpgradeActive(Element.Mass);
            _ringRadius = Mathf.Max(1f, ringRadius);
            _brickScale = brickScale;
            _growthRate = growthRate;
            _interiorCount = Mathf.Max(0, interiorCount);
            _burstCount = Mathf.Max(0, burstCount);
            _burstRadiusMultiplier = Mathf.Max(1f, burstRadiusMultiplier);
            _clearRadius = Mathf.Max(1f, 0.5f * Mathf.Max(brickScale.x, Mathf.Max(brickScale.y, brickScale.z)));

            transform.position = center;
            _axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            BuildBasis();

            // The ring reads as a portal you thread, so it faces along the placement axis.
            if (SafeLookRotation.TryGet(_axis, _basisU, out var rot, this))
                transform.rotation = rot;

            _ring = ToyFactory.AddRingBody(transform, _ringRadius, DomainAccent(_domain));

            // Interior: the first arc of the spiral, inside the ring.
            for (int i = 0; i < _interiorCount; i++)
            {
                if (TryLay(SpiralPoint(i, _interiorCount, 0f, _ringRadius), out var prism))
                    _interior.Add(prism);
            }
        }

        void BuildBasis()
        {
            _basisU = Vector3.ProjectOnPlane(Vector3.up, _axis);
            if (_basisU.sqrMagnitude < 1e-4f) _basisU = Vector3.ProjectOnPlane(Vector3.right, _axis);
            _basisU.Normalize();
            _basisV = Vector3.Cross(_axis, _basisU).normalized;
        }

        /// <summary>
        /// Point <paramref name="i"/> of the shared spiral, mapped into the annulus
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

        bool TryLay(Vector3 pos, out Prism prism)
        {
            prism = null;
            if (_spawnChannel == null) return false;

            var index = PrismSpatialIndex.EnsureInstance();
            if (index != null && !index.TryReserve(pos, _clearRadius)) return false;

            // Prism forward runs along the ring axis so the fill reads as a membrane across the
            // mouth rather than a scatter of loose blocks.
            if (!SafeLookRotation.TryGet(_axis, _basisU, out var rotation, this)) return false;

            var ret = _spawnChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = _domain,
                Rotation = rotation,
                SpawnPosition = pos,
                Scale = _brickScale,
                Velocity = Vector3.zero,
                PrismType = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out prism)) return false;

            prism.ownerID = $"{_playerName}::Switch::{GetInstanceID()}";
            prism.Domain = _domain;
            // Flag BEFORE Initialize so the prism blooms in already armoured, rather than
            // popping a shield on after it has settled (Docs/ECOSYSTEM.md's birth-transition rule).
            if (_shieldPrisms) prism.prismProperties.IsShielded = true;
            // The one growth engine (Docs/PRISM_ANIMATION.md) — bloom in on the clock.
            prism.TargetScale = _brickScale;
            prism.SetGrowthRate(_growthRate);
            prism.Initialize(_playerName);
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

        /// <summary>Struck: the pattern continues outward, and the switch is spent.</summary>
        void Trigger(AstroLeagueBall ball)
        {
            _spent = true;

            // The SAME spiral, continued past the ring's edge into the annulus beyond it.
            int placed = 0;
            for (int i = 0; i < _burstCount; i++)
            {
                if (TryLay(SpiralPoint(_interiorCount + i, _burstCount,
                                       _ringRadius, _ringRadius * _burstRadiusMultiplier), out _))
                    placed++;
            }

            CSDebug.Log($"[ScarabSwitch] Threaded by a {ball.LastHitDomain} ball — " +
                        $"pattern extended outward with {placed}/{_burstCount} prisms.");

            // The ring is the switch; the interior fill it laid stays as world mass (conserved —
            // it is removed only by an active force, like any other prism).
            if (_ring) Destroy(_ring);
            Destroy(this);
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
