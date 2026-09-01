using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The SWORDFISH - the flagship apex predator (Docs/ECOSYSTEM.md §42). An ordinary
    /// <see cref="LightFauna"/> predator in every respect the ecology cares about (it hunts
    /// herbivores at the mouth, holds a territory, rests between hunt pulses, starves, breeds,
    /// withers extremity-first to a skeleton and drops its heart) with ONE addition layered on
    /// top: inside a hunt window it also charges PILOTS.
    ///
    /// The charge is the worm colony's souls-like grammar, on a body built for it:
    ///   Cruise -> (opposing pilot inside aggroRadius) Pursue -> (inside strikeRange)
    ///   Telegraph (coils, backs off, nose on you) -> Lunge (a straight line through the point
    ///   it locked at the END of the telegraph - dodge by moving) -> Recover (spent, slow,
    ///   fins flared: the punish window) -> Cruise.
    ///
    /// The bill is the whole weapon: three danger prisms laid end to end, so the hit is the
    /// ordinary danger-prism contact (the full-stop slow, the elemental debuff, the boost
    /// reset - whatever the struck vessel's own impact container wires; CLAUDE.md records that
    /// Rhino and Serpent take no slow). Nothing here damages anything directly, replicates
    /// anything, or writes anyone's domain.
    ///
    /// Per-element identity comes from <see cref="SwordfishStrikeDataSO.ProfileFor"/>: the
    /// element this individual hatched with scales its lunge, wind-up, reach and cooldown, so the
    /// four variants read as four animals with one prefab (§40).
    /// </summary>
    public class SwordfishFauna : LightFauna
    {
        public enum StrikeState { Cruise = 0, Pursue = 1, Telegraph = 2, Lunge = 3, Recover = 4 }

        [Header("Swordfish")]
        [Tooltip("The vessel strike - sensing, wind-up, lunge, recover, cooldown, per-element profiles.")]
        [SerializeField] SwordfishStrikeDataSO strikeData;

        /// <summary>Where the strike is - read by the presentation driver (animator, audio).</summary>
        public StrikeState State { get; private set; } = StrikeState.Cruise;

        /// <summary>True from the wind-up through the lunge: the body is committed to a strike.</summary>
        public bool IsCharging => State == StrikeState.Telegraph || State == StrikeState.Lunge;

        /// <summary>True while it is chasing a pilot but has not yet committed.</summary>
        public bool IsPursuingVessel => State == StrikeState.Pursue;

        Transform _threat;             // the pilot, acquired on the behavior tick
        Vector3 _lungePoint;           // locked at the end of the telegraph
        float _stateSince;
        float _nextStrikeAt;           // cooldown gate
        ElementStrikeProfile _profile;

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);
            _nextStrikeAt = Time.time + Effective(strikeData ? strikeData.strikeCooldownSeconds : 0f, p => p.cooldownMultiplier);
            SetState(StrikeState.Cruise);
        }

        // Steering belongs to the strike whenever one is in progress; the base predator
        // steering (territory patrol, prey pursuit) resumes the moment it ends.
        protected override bool SubclassOwnsSteering => State != StrikeState.Cruise;

        ElementStrikeProfile Profile
        {
            get
            {
                if (_profile == null && strikeData)
                {
                    var pick = VariantPick;
                    _profile = strikeData.ProfileFor(pick.HasValue ? pick.Value.Element : Element.None);
                }
                return _profile;
            }
        }

        float Effective(float value, System.Func<ElementStrikeProfile, float> multiplier)
        {
            var p = Profile;
            return p == null ? value : value * multiplier(p);
        }

        void SetState(StrikeState state)
        {
            State = state;
            _stateSince = Time.time;
        }

        /// <summary>
        /// Threat acquisition rides the behavior tick like prey acquisition does: cheap, and
        /// never inside a committed strike (a locked lunge plays out). Outside a hunt window the
        /// swordfish carries no threat at all - it is resting, like every predator.
        /// </summary>
        protected override void OnBehaviorTick()
        {
            if (!strikeData) return;
            if (State == StrikeState.Lunge || State == StrikeState.Recover) return;

            if (!IsHuntWindow || Time.time < _nextStrikeAt)
            {
                _threat = null;
                return;
            }

            float aggro = Effective(strikeData.aggroRadius, p => p.rangeMultiplier);
            _threat = FindNearestVessel(transform.position, aggro, strikeData.opposingDomainsOnly);
        }

        /// <summary>
        /// The strike state machine, per frame on the simulating peer. Returns true while it owns
        /// the body (velocity + facing written here); the base then only keeps the mouth working,
        /// so a lunge that runs through a herbivore still eats it.
        /// </summary>
        protected override bool TickSubclassMovement()
        {
            if (!strikeData) return false;
            float elapsed = Time.time - _stateSince;
            var data = Data;

            switch (State)
            {
                case StrikeState.Cruise:
                    if (_threat && IsHuntWindow) SetState(StrikeState.Pursue);
                    return false;

                case StrikeState.Pursue:
                {
                    if (!_threat || !IsHuntWindow)
                    {
                        SetState(StrikeState.Cruise);
                        return false;
                    }
                    Vector3 toThreat = _threat.position - transform.position;
                    float range = Effective(strikeData.strikeRange, p => p.rangeMultiplier);
                    if (toThreat.sqrMagnitude <= range * range)
                    {
                        SetState(StrikeState.Telegraph);
                        return true;
                    }
                    float speed = Mathf.Max(0f, data.maxSpeed) * Mathf.Max(1f, data.pursuitSpeedMultiplier);
                    Drive(toThreat, speed, data.pursuitAgility);
                    return true;
                }

                case StrikeState.Telegraph:
                {
                    if (!_threat)
                    {
                        SetState(StrikeState.Cruise); // the pilot left mid-wind-up
                        return false;
                    }
                    Vector3 toThreat = _threat.position - transform.position;
                    // Coil: back away slowly with the nose held on the target. The velocity runs
                    // AGAINST the facing here on purpose - that is what reads as a wind-up.
                    Face(toThreat);
                    CurrentVelocity = Vector3.Lerp(CurrentVelocity, -toThreat.normalized * strikeData.telegraphRetreatSpeed,
                        Mathf.Clamp01(Time.deltaTime * 4f));
                    if (elapsed >= Effective(strikeData.telegraphSeconds, p => p.telegraphMultiplier))
                    {
                        // The strike point locks HERE. Aim through the pilot, not at them.
                        _lungePoint = _threat.position + toThreat.normalized * strikeData.lungeOvershoot;
                        SetState(StrikeState.Lunge);
                    }
                    return true;
                }

                case StrikeState.Lunge:
                {
                    Vector3 toPoint = _lungePoint - transform.position;
                    float speed = Effective(strikeData.lungeSpeed, p => p.lungeSpeedMultiplier);
                    if (elapsed >= strikeData.lungeMaxSeconds ||
                        toPoint.sqrMagnitude <= strikeData.lungeArriveRadius * strikeData.lungeArriveRadius)
                    {
                        SetState(StrikeState.Recover);
                        return true;
                    }
                    // Arrow-straight: the direction was decided at the lock, the speed is the sword's.
                    CurrentVelocity = toPoint.normalized * speed;
                    Face(toPoint);
                    return true;
                }

                case StrikeState.Recover:
                {
                    float cruise = Mathf.Max(0f, data.minSpeed) * strikeData.recoverSpeedFraction;
                    CurrentVelocity = Vector3.Lerp(CurrentVelocity, transform.forward * cruise,
                        Mathf.Clamp01(Time.deltaTime * 2f));
                    if (elapsed >= strikeData.recoverSeconds)
                    {
                        _threat = null;
                        _nextStrikeAt = Time.time + Effective(strikeData.strikeCooldownSeconds, p => p.cooldownMultiplier);
                        SetState(StrikeState.Cruise);
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        void Drive(Vector3 toward, float speed, float agility)
        {
            Vector3 heading = CurrentVelocity.sqrMagnitude > DegenerateSteeringSqr
                ? CurrentVelocity.normalized
                : transform.forward;
            heading = Vector3.Slerp(heading, toward.normalized, Mathf.Clamp01(Time.deltaTime * agility));
            CurrentVelocity = heading * speed;
            Face(CurrentVelocity);
        }

        void Face(Vector3 direction)
        {
            if (SafeLookRotation.TryGet(direction, out var rotation, this))
                DesiredRotation = rotation;
        }

        protected override void OnDeath(string killerName = "")
        {
            _threat = null;
            SetState(StrikeState.Cruise);
            base.OnDeath(killerName);
        }

        // A puppet never decides a strike; the replicated transform carries whatever the server
        // did. (The pose it shows is the swim - see the driver's note on replication.)
        protected override void OnEnteredPuppetMode()
        {
            base.OnEnteredPuppetMode();
            _threat = null;
            SetState(StrikeState.Cruise);
        }
    }
}
