using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino sword's brain. Owns the energize state, the blade's LENGTH (Space alone) and the
    /// camera distance that follows it, and the omni-crystal surge's sword ring; implements
    /// <see cref="IRhinoSwordState"/> so the shared impact-effect SOs can read/drive it via
    /// <c>Skimmer.SwordState</c>.
    ///
    /// The sword's ORDINARY cutting is UNGATED — it always damages prisms on contact
    /// (that logic lives in <c>RhinoSkimmerDamagePrismEffectSO</c>). There is NO energy: the meter,
    /// the energy banked per cut and the energize cost were discarded on 2026-10-26 (Garrett:
    /// "lets discard the whole notion of energy as a resource on the rhino").
    ///
    /// AUTHORITY — the energize phase and the surge are decided on the machine that OWNS the vessel
    /// and replicated by <see cref="RhinoSwordNetworkRelay"/>; every other peer mirrors them. An
    /// energized blade pops super-shielded mass, so a phase run on each peer's own clock would be a
    /// divergent prismscape.
    ///
    /// ENERGIZE — the supershield key. Holding the both-triggers lower/chop stance
    /// (<see cref="SetInStance"/>, fed by <see cref="ShieldSwipeActionExecutor"/>) for
    /// <c>EnergizeHoldSeconds</c> (divided by <c>touchEnergizeRateMultiplier</c> for a human on a phone)
    /// ignites the blade (no cost; the cooldown is the only limit): while
    /// <see cref="IsEnergized"/> the damage effect POPS super-shielded prisms instead of
    /// binding, once the SPACE level-5 upgrade is active (RhinoSkimmerDamagePrismEffectSO). Leaving
    /// the stance starts the <c>EnergizedTailSeconds</c> countdown, then
    /// <c>EnergizeCooldownSeconds</c> locks re-charging. The tail runs out once started - taking
    /// the stance again does not re-light it. On the ignition edge
    /// the standing blade contacts are RE-DISPATCHED (both the box-trigger overlaps and the
    /// shell-tier pairs) so a super-shielded prism already resting against the blade pops without
    /// needing a fresh OnTriggerEnter.
    ///
    /// SURGE — an omni crystal (<see cref="Surge"/>) holds the blade energized with no stance for the
    /// surge's length and raises a ring of trigger-driven sword copies round the hull with the
    /// camera pulled back onto it (<see cref="RhinoSurgeSwordRing"/>).
    ///
    /// BINDING — a NON-energized blade inside super-shielded mass it cannot cut does not recoil;
    /// the armour DRAGS on it. While the shell tier reports the blade inside one or more live
    /// super-shielded prisms (<see cref="PrismShellContactManager.CollectSuperShieldedContactsForOwner"/>)
    /// the vessel's rotation rate (pitch, yaw AND roll) eases down to <c>BindTurnRateMultiplier</c>
    /// (<see cref="VesselTransformer.ExternalTurnRateMultiplier"/>), every bound prism keeps
    /// shuddering (the deflection jiggle, re-stamped through <see cref="Prism.AbsorbSuperShieldHit"/>
    /// — photons only, the prism stays invulnerable), and the local human pilot feels a grind
    /// (<see cref="HapticController.PlayBind"/>). Leaving the prism eases the turn rate back.
    /// The ENTRY beat (first jiggle + punish thud) is the damage effect's. See
    /// <c>RHINO_ENERGY_SWORD.md</c> § "Binding".
    ///
    /// LENGTH — SPACE ALONE (Garrett, 2026-10-26: "Space alone can grow the sword, but move the camera
    /// accordingly"). The blade's world length is the Skimmer's live Space scale
    /// (<c>Skimmer.LiveElementalScale</c>, 24 at rest → 40 at Space 10, on Rhino.prefab), eased at
    /// <c>SpaceLengthEaseSpeed</c>, along local Y only so the authored X/Z silhouette holds. Nothing
    /// else resizes it - no energy, no surge burst. The driver sets <c>Skimmer.HasExternalScaleDriver</c>
    /// so the Skimmer's own (un-eased) write stands down: one writer. The local human pilot's camera
    /// pulls back with the blade (<see cref="RhinoSwordCamera"/>). All LOOK is delegated to the
    /// prefab-authored <see cref="RhinoSwordFXController"/> on the blade root. The swipe pose
    /// (rotation/position) is owned by <see cref="ShieldSwipeActionExecutor"/>. See
    /// <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public class ShieldSkimmerScaleDriver : MonoBehaviour, IRhinoSwordState
    {
        [Header("Refs")]
        [SerializeField] private Transform skimmerRoot;

        [Header("Config")]
        [SerializeField] private ShieldSkimmerScaleConfigSO config;

        [Header("Network")]
        [Tooltip("Replicates the energize phase and the surge from the vessel's owner. On the Rhino root. " +
                 "Empty = this sword decides its own state locally (an un-networked vessel).")]
        [SerializeField] private RhinoSwordNetworkRelay relay;

        [Header("Mass - Afterimage")]
        [Tooltip("The Mass ability on this GameObject: lays the blade's afterimages once the energize " +
                 "ends (and throughout a surge). It also says which prisms are the sword's own, so the AI " +
                 "aims elsewhere first; the blade itself cuts them like any prism.")]
        [SerializeField] private RhinoSwordAfterimage afterimage;

        /// <summary>Raised on every energize phase change, on every peer (from the replicated phase
        /// off the owner): (from, to). The Mass afterimage opens its window on the way out of
        /// Energized.</summary>
        public event Action<RhinoSwordEnergizePhase, RhinoSwordEnergizePhase> EnergizePhaseChanged;

        Skimmer _skimmer;
        SkimmerImpactor _skimmerImpactor;
        RhinoSwordFXController _fx;
        bool _fxWarned;

        // ── energize (the supershield key) ──
        RhinoSwordEnergizePhase _energize = RhinoSwordEnergizePhase.Idle;
        float _chargeStart, _tailEnd, _cooldownEnd;
        bool _tailCounting;
        bool _inStance;
        bool _releasedSinceIgnition;   // the stance has been let go at least once since ignition

        // ── surge (omni crystal) ──
        bool _surging;
        float _surgeEnd;
        RhinoSurgeSwordRing _ring;
        readonly RhinoSwordCamera _camera = new();

        // ── length (Space alone) ──
        float _lengthWorld = -1f;   // the blade's current world length; < 0 until first written

        // ── binding (a non-energized blade inside super-shielded mass) ──
        readonly List<Prism> _bound = new(4);
        float _bindTurnMultiplier = 1f;
        VesselTransformer _turnOwner;   // the transformer we are currently slowing, if any
        bool _wasBound;
        float _nextBindJiggle, _nextBindHaptic;

        /// <summary>Current energize phase (for FX and any future HUD readout).</summary>
        public RhinoSwordEnergizePhase EnergizePhase => _energize;

        /// <summary>How much of the energize COOLDOWN is still to run, 1 the instant it starts and 0
        /// once the blade can be charged again (0 outside the cooldown). The HUD's recharge veil.</summary>
        public float EnergizeCooldownRemaining01 =>
            _energize == RhinoSwordEnergizePhase.Cooldown && config != null && config.EnergizeCooldownSeconds > 0f
                ? Mathf.Clamp01((_cooldownEnd - Time.time) / config.EnergizeCooldownSeconds)
                : 0f;

        /// <summary>The Charge card's gauge: the stance filling while Charging, full while lit (held, Live
        /// Wire or a surge), draining over the let-go tail, empty otherwise. Owner-side clock; the HUD
        /// only draws for the local pilot, who is the owner.</summary>
        public float EnergizeGauge01
        {
            get
            {
                switch (_energize)
                {
                    case RhinoSwordEnergizePhase.Charging: return Charge01;
                    case RhinoSwordEnergizePhase.Energized:
                        if (_surging || !_tailCounting || config == null || config.EnergizedTailSeconds <= 0f) return 1f;
                        return Mathf.Clamp01((_tailEnd - Time.time) / config.EnergizedTailSeconds);
                    default: return 0f;
                }
            }
        }

        /// <summary>Charge progress 0..1 while Charging (0 otherwise).</summary>
        public float Charge01 => _energize == RhinoSwordEnergizePhase.Charging && config != null
            ? Mathf.Clamp01((Time.time - _chargeStart) / EnergizeHoldSeconds)
            : 0f;

        /// <summary>The stance hold that ignites THIS blade: the config's, shortened by its touch rate for a
        /// human on a phone (Garrett 2026-10-21: a phone pilot energizes by lifting both thumbs, which also
        /// lets go of the steering, "so we can compensate for lack of control by having it go quicker").
        /// Read from the REPLICATED device, so a replica's charge ramp (FX) runs the owner's clock; the
        /// ignition itself is decided on the owner and mirrored through the relay.</summary>
        float EnergizeHoldSeconds => config.EnergizeHoldSecondsFor(IsHumanTouchPilot);

        /// <summary>A human flies this Rhino on a phone. AI and autopilot keep the pad's timing (an AI's
        /// input status can read Touch, the enum's zero, without any thumb on any glass).</summary>
        bool IsHumanTouchPilot
        {
            get
            {
                var status = _skimmer ? _skimmer.VesselStatus : null;
                if (status?.Player == null) return false;
                var input = status.InputStatus;
                if (input == null || input.ActiveInputDevice != InputDeviceType.Touch) return false;
                if (status.IsInitializedAsAI) return false;
                return status.AIPilot == null || !status.AIPilot.AutoPilotEnabled;
            }
        }

        /// <summary>The sword's own Skimmer (blade root), for the components that share its GameObject.</summary>
        public Skimmer Skimmer => _skimmer;

        /// <summary>The blade transform (the skimmer root). Its length is Space's (see the class notes).</summary>
        public Transform BladeRoot => skimmerRoot;

        /// <summary>True where this sword's state is decided (see <see cref="RhinoSwordNetworkRelay"/>).</summary>
        bool IsAuthority => !relay || relay.IsAuthority;

        // ── IRhinoSwordState ──
        public bool IsEnergized => _energize == RhinoSwordEnergizePhase.Energized;
        public bool IsSurging => _surging;

        public bool IsOwnAfterimage(Prism prism) => afterimage && afterimage.IsMine(prism);

        public void Surge(float seconds)
        {
            if (!IsAuthority || config == null || seconds <= 0f) return;
            float now = Time.time;

            if (_energize != RhinoSwordEnergizePhase.Energized)
                SetEnergizePhase(RhinoSwordEnergizePhase.Energized, now);

            bool started = !_surging;
            _surgeEnd = _surging ? Mathf.Max(_surgeEnd, now + seconds) : now + seconds;
            _surging = true;
            // A surge is not held by the stance, so the tail rules start from "already let go".
            _releasedSinceIgnition = true;
            _tailCounting = false;

            if (started) PlaySurgeStart();
        }

        public void SetInStance(bool inStance) => _inStance = inStance;

        public void NotifyPrismDestroyed(bool superShielded, Vector3 prismWorldPosition)
        {
            Fx?.NotifyKill(superShielded, prismWorldPosition);
        }

        public void NotifyPopDenied(Vector3 prismWorldPosition)
        {
            Fx?.NotifyPopDenied(prismWorldPosition);
        }

        /// <summary>The surge's opening beat: a full flash, a whole-blade crackle and a camera shake.
        /// The ring itself fans out from <see cref="LateUpdate"/>.</summary>
        void PlaySurgeStart() => Fx?.NotifySurge();

        RhinoSwordFXController Fx
        {
            get
            {
                if (_fx) return _fx;
                if (skimmerRoot) skimmerRoot.TryGetComponent(out _fx);
                if (!_fx && !_fxWarned)
                {
                    _fxWarned = true;
                    CSDebug.LogWarning($"[{nameof(ShieldSkimmerScaleDriver)}] No {nameof(RhinoSwordFXController)} " +
                                       $"on '{(skimmerRoot ? skimmerRoot.name : "<null>")}' — the sword runs with no " +
                                       "blade FX (heat ramp, ignition, sparks). Author one on the blade root prefab.");
                }
                return _fx;
            }
        }

        void Awake()
        {
            if (!skimmerRoot) skimmerRoot = transform;
            skimmerRoot.TryGetComponent(out _skimmer);
            skimmerRoot.TryGetComponent(out _skimmerImpactor);
            if (_skimmer) _skimmer.SwordState = this;
            if (!relay) relay = GetComponentInParent<RhinoSwordNetworkRelay>();
            if (!afterimage) TryGetComponent(out afterimage);
        }

        void OnEnable()
        {
            if (_skimmer)
            {
                // One writer of the blade's scale: this driver eases the Space length itself.
                _skimmer.HasExternalScaleDriver = true;
                _skimmer.SwordState = this;
            }

            // Pooled/re-enabled vessels start clean.
            _energize = RhinoSwordEnergizePhase.Idle;
            _inStance = false;
            _tailCounting = false;
            _releasedSinceIgnition = false;
            _surging = false;
            _surgeEnd = 0f;
            _cooldownEnd = 0f;
            _bindTurnMultiplier = 1f;
            _wasBound = false;
            _lengthWorld = -1f;
        }

        void OnDisable()
        {
            if (_skimmer) _skimmer.HasExternalScaleDriver = false;
            ReleaseTurn();
            _ring?.Collapse();
            _camera.Release();
        }

        void OnDestroy()
        {
            _ring?.Dispose();
            _camera.Release();
        }

        void Update()
        {
            if (!skimmerRoot || config == null) return;
            float dt = Time.deltaTime;

            if (IsAuthority)
            {
                UpdateEnergize();
                if (relay) relay.Publish(_energize, _surging);
            }
            else
            {
                MirrorAuthority();
            }

            UpdateBind(dt);
            UpdateLength(dt);
            Fx?.Tick(_energize, Charge01, dt);
        }

        // After the swipe executor (Update) has posed the blade, so every ring copy takes this
        // frame's pose and this frame's colour.
        void LateUpdate()
        {
            if (!skimmerRoot || config == null) return;
            var status = _skimmer ? _skimmer.VesselStatus : null;
            float dt = Time.deltaTime;

            float rest = _skimmer ? Mathf.Max(0.01f, _skimmer.RestElementalScale) : 1f;
            float lengthRatio = _lengthWorld > 0f ? _lengthWorld / rest : 1f;
            _camera.Tick(status, _surging, lengthRatio, config, dt);

            if (_ring == null)
            {
                if (!_surging) return;
                _ring = new RhinoSurgeSwordRing(skimmerRoot, this);
            }
            _ring.Tick(_surging, config, status, Fx ? Fx.BodyRenderer : null, dt);
        }

        // ── length (Space alone) ──────────────────────────────────────────────
        // The Skimmer's live Space scale, eased, along local Y only (the authored X/Z silhouette holds).
        // The first write snaps, so a fresh or re-enabled blade never grows in from its prefab size.
        void UpdateLength(float dt)
        {
            if (!_skimmer || !_skimmer.IsInitialized) return;
            float target = Mathf.Max(0.01f, _skimmer.LiveElementalScale);
            _lengthWorld = _lengthWorld < 0f
                ? target
                : Mathf.MoveTowards(_lengthWorld, target, config.SpaceLengthEaseSpeed * dt);

            var shape = _skimmer.AuthoredShape;
            float parentY = skimmerRoot.parent ? skimmerRoot.parent.lossyScale.y : 1f;
            float localY = _lengthWorld / Mathf.Max(0.0001f, parentY);
            var current = skimmerRoot.localScale;
            if (Mathf.Abs(current.y - localY) > 1e-4f || current.x != shape.x || current.z != shape.z)
                skimmerRoot.localScale = new Vector3(shape.x, localY, shape.z);
        }

        // ── energize ──────────────────────────────────────────────────────────
        void UpdateEnergize()
        {
            float now = Time.time;

            switch (_energize)
            {
                case RhinoSwordEnergizePhase.Idle:
                    if (_inStance && now >= _cooldownEnd)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Charging, now);
                    break;

                case RhinoSwordEnergizePhase.Charging:
                    if (!_inStance)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Idle, now);
                    else if (now - _chargeStart >= EnergizeHoldSeconds)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Energized, now);
                    break;

                case RhinoSwordEnergizePhase.Energized:
                    if (_surging)
                    {
                        // A surge holds the blade whatever the fingers do. When it ends it hands
                        // over to the ordinary rules with the tail already spent, so the blade
                        // goes out next frame.
                        if (now < _surgeEnd) break;
                        _surging = false;
                        _tailCounting = true;
                        _tailEnd = now;
                        break;
                    }

                    if (_inStance && !_releasedSinceIgnition)
                    {
                        _tailCounting = false; // holding the stance keeps it lit
                    }
                    else if (!_inStance && !_tailCounting)
                    {
                        _tailCounting = true;
                        _releasedSinceIgnition = true;
                        _tailEnd = now + config.EnergizedTailSeconds;
                    }
                    else if (_tailCounting && now >= _tailEnd)
                    {
                        _cooldownEnd = now + config.EnergizeCooldownSeconds;
                        SetEnergizePhase(RhinoSwordEnergizePhase.Cooldown, now);
                    }
                    break;

                case RhinoSwordEnergizePhase.Cooldown:
                    if (now >= _cooldownEnd)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Idle, now);
                    break;
            }
        }

        /// <summary>A non-owning peer: take the phase and the surge from the owner's relay. The phase
        /// goes through <see cref="SetEnergizePhase"/>, so the ignition re-dispatch, the FX and the
        /// afterimage window fire here exactly as they do on the owner.</summary>
        void MirrorAuthority()
        {
            bool surging = relay.IsSurging;
            if (surging && !_surging) PlaySurgeStart();
            _surging = surging;

            if (relay.Phase != _energize)
                SetEnergizePhase(relay.Phase, Time.time);
        }

        void SetEnergizePhase(RhinoSwordEnergizePhase phase, float now)
        {
            if (_energize == phase) return;
            var from = _energize;
            _energize = phase;

            switch (phase)
            {
                case RhinoSwordEnergizePhase.Charging:
                    _chargeStart = now;
                    break;
                case RhinoSwordEnergizePhase.Energized:
                    _tailCounting = false;
                    _releasedSinceIgnition = false;
                    // A super-shielded prism already RESTING against the blade gets no fresh
                    // OnTriggerEnter (and its shell-tier pair was dispatched once on entry),
                    // so the ignition edge re-runs the standing contacts through the same
                    // effect chains — the pop the player just paid for lands immediately.
                    RedispatchStandingBladeContacts();
                    break;
            }

            Fx?.NotifyEnergizePhaseChanged(phase);
            EnergizePhaseChanged?.Invoke(from, phase);
        }

        void RedispatchStandingBladeContacts()
        {
            if (!_skimmerImpactor) return;
            // Super-shielded only: the ignition buys the POP. Replaying every standing contact also
            // cut the core of any shielded prism resting in the blade - a free Breaker.
            _skimmerImpactor.ReapplyPrismEffectsToOverlapping(superShieldedOnly: true);
            PrismShellContactManager.RedispatchPairsForOwner(_skimmerImpactor, superShieldedOnly: true);
        }

        // ── binding ───────────────────────────────────────────────────────────
        void UpdateBind(float dt)
        {
            var status = _skimmer ? _skimmer.VesselStatus : null;

            _bound.Clear();
            // An energized blade POPS super-shields on contact (and the ignition edge re-dispatches
            // standing contacts), so it is never bound — anything left in the pair set this frame
            // is about to pop.
            bool bound = !IsEnergized && _skimmerImpactor && _skimmer && _skimmer.IsInitialized
                         && PrismShellContactManager.CollectSuperShieldedContactsForOwner(_skimmerImpactor, _bound) > 0;

            float target = bound ? config.BindTurnRateMultiplier : 1f;
            float rate = target < _bindTurnMultiplier ? config.BindEngageRate : config.BindReleaseRate;
            _bindTurnMultiplier = Mathf.MoveTowards(_bindTurnMultiplier, target, rate * dt);
            ApplyTurn(status);

            if (!bound)
            {
                _wasBound = false;
                return;
            }

            float now = Time.time;
            if (!_wasBound)
            {
                // The damage effect already played the ENTRY beat (first jiggle + punish thud), so
                // the texture starts one interval later rather than stacking on top of it.
                _wasBound = true;
                _nextBindJiggle = now + config.BindJiggleIntervalSeconds;
                _nextBindHaptic = now + config.BindHapticIntervalSeconds;
            }

            if (now >= _nextBindJiggle)
            {
                _nextBindJiggle = now + config.BindJiggleIntervalSeconds;
                float speed = config.BindJiggleImpactSpeed;
                for (int i = 0; i < _bound.Count; i++)
                    _bound[i].AbsorbSuperShieldHit(speed);
            }

            if (now >= _nextBindHaptic)
            {
                _nextBindHaptic = now + config.BindHapticIntervalSeconds;
                if (status != null && status.IsLocalUser && !status.AutoPilotEnabled)
                    HapticController.PlayBind(config.BindHapticStrength);
            }
        }

        // Single writer of VesselTransformer.ExternalTurnRateMultiplier for this vessel: writes
        // while the drag is non-trivial and hands it back at exactly 1 once it has eased out.
        void ApplyTurn(IVesselStatus status)
        {
            var transformer = status?.VesselTransformer;
            if (!transformer || _bindTurnMultiplier >= 0.9999f)
            {
                ReleaseTurn();
                return;
            }

            if (_turnOwner && _turnOwner != transformer) ReleaseTurn();
            transformer.ExternalTurnRateMultiplier = _bindTurnMultiplier;
            _turnOwner = transformer;
        }

        void ReleaseTurn()
        {
            if (_turnOwner) _turnOwner.ExternalTurnRateMultiplier = 1f;
            _turnOwner = null;
        }
    }
}
