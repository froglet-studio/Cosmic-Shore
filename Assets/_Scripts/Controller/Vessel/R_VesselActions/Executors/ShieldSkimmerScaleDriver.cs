using System;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino energy sword's brain. Owns the sword transform's SCALE and the sword's
    /// energy/energize state, and implements <see cref="IRhinoSwordState"/> so the shared
    /// impact-effect SOs can read/drive it via <c>Skimmer.SwordState</c>.
    ///
    /// The sword's ORDINARY cutting is UNGATED — it always damages prisms on contact
    /// (that logic lives in <c>RhinoSkimmerDamagePrismEffectSO</c>). Two things live here:
    ///
    /// ENERGY — the Rhino's Shield resource (<see cref="shieldIndex"/>, normalized 0..1,
    /// no passive decay: a meter you fill and spend):
    ///  • GAIN: the prism effect banks energy per prism the sword destroys, scaled by CHARGE
    ///    (<c>RhinoSkimmerDamagePrismEffectSO.chargeEnergyGainMultiplier</c>).
    ///  • SPEND: energizing costs <see cref="ShieldSkimmerScaleConfigSO.EnergizeCostFraction"/>.
    ///  • SURGE: an omni crystal (<see cref="Surge"/>) fills it, bursts the blade in all three
    ///    dimensions and holds it energized with no stance for the surge's length.
    ///
    /// AUTHORITY — energy and the energize phase are decided on the machine that OWNS the vessel
    /// and replicated by <see cref="RhinoSwordNetworkRelay"/>; every other peer mirrors them. Each
    /// peer's local copy used to run its own state machine off its own kills and its own clock,
    /// and an energized blade pops super-shielded mass, so that was a divergent prismscape.
    ///
    /// ENERGIZE — the supershield key. Holding the both-triggers lower/chop stance
    /// (<see cref="SetInStance"/>, fed by <see cref="ShieldSwipeActionExecutor"/>) for
    /// <c>EnergizeHoldSeconds</c> spends the cost and ignites the blade: while
    /// <see cref="IsEnergized"/> the damage effect POPS super-shielded prisms instead of
    /// binding. Leaving the stance starts the <c>EnergizedTailSeconds</c> countdown, then
    /// <c>EnergizeCooldownSeconds</c> locks re-charging. The tail runs out once started - taking
    /// the stance again does not re-light it - UNLESS the CHARGE level-5 upgrade, <b>Live Wire</b>,
    /// is active: then every return to the stance holds the blade lit again (until 2026-10-16 that
    /// refresh was ungated, so a chop every few seconds kept any blade energized forever for one
    /// payment). On the ignition edge the standing
    /// blade contacts are RE-DISPATCHED (both the box-trigger overlaps and the shell-tier
    /// pairs) so a super-shielded prism already resting against the blade pops without
    /// needing a fresh OnTriggerEnter.
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
    /// The resting length reflects stored energy (Y-only elongation from the Space-driven
    /// elemental base — the same meter the Rhino HUD draws through <see cref="OnScaleChanged"/>).
    /// All LOOK is delegated to the prefab-authored <see cref="RhinoSwordFXController"/> on the
    /// blade root. Sets <c>Skimmer.HasExternalScaleDriver</c> so the Skimmer's own elemental
    /// scale write stands down. The swipe pose (rotation/position) is owned by
    /// <see cref="ShieldSwipeActionExecutor"/> — only scale is ours. See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public class ShieldSkimmerScaleDriver : MonoBehaviour, IRhinoSwordState
    {
        [Header("Refs")]
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private Transform skimmerRoot;

        [Header("Config")]
        [SerializeField] private ShieldSkimmerScaleConfigSO config;

        [Header("Energy Resource Index (Shield, normalized 0..1)")]
        [SerializeField] private int shieldIndex = 1;

        [Header("Network")]
        [Tooltip("Replicates energy + energize phase from the vessel's owner. On the Rhino root. " +
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

        /// <summary>Fires each frame with (currentWorldLength, restingLength, maxLength) — the Rhino
        /// HUD reads it as a 0..1 energy meter.</summary>
        public event Action<float, float, float> OnScaleChanged;

        Skimmer _skimmer;
        SkimmerImpactor _skimmerImpactor;
        RhinoSwordFXController _fx;
        bool _fxWarned;

        Vector3 _authoredShape; // local X/Y/Z silhouette captured from the Skimmer

        // ── crystal burst ──
        enum BurstPhase { None, Growing, Holding, Returning }
        BurstPhase _burst = BurstPhase.None;
        Vector3 _burstTargetLocal;
        float _burstHoldEnd;

        // ── energize (the supershield key) ──
        RhinoSwordEnergizePhase _energize = RhinoSwordEnergizePhase.Idle;
        float _chargeStart, _tailEnd, _cooldownEnd;
        bool _tailCounting;
        bool _inStance;
        bool _releasedSinceIgnition;   // the stance has been let go at least once since ignition

        // ── surge (omni crystal) ──
        bool _surging;
        float _surgeEnd;

        // ── binding (a non-energized blade inside super-shielded mass) ──
        readonly List<Prism> _bound = new(4);
        float _bindTurnMultiplier = 1f;
        VesselTransformer _turnOwner;   // the transformer we are currently slowing, if any
        bool _wasBound;
        float _nextBindJiggle, _nextBindHaptic;

        // Sword capsules (Skimmer.ElongateYOnly): the resting length grows only local Y and
        // preserves the authored X/Z silhouette; the crystal burst overrides all three dims.
        bool YOnly => _skimmer && _skimmer.ElongateYOnly;

        // Resting size is the skimmer's live elemental (Space-driven) scale — element levels
        // lengthen the sword and energy growth composes on top. Falls back to the config value.
        float BaseScale => _skimmer ? Mathf.Max(0.01f, _skimmer.LiveElementalScale) : config.BaseScale;
        // Keep max ≥ base: the two come from different sources (base = skimmer's live elemental
        // scale, max = config), so a large elemental scale must never invert the resting clamp.
        float MaxScale  => Mathf.Max(config.MaxScale, BaseScale);
        float Range     => Mathf.Max(0.0001f, MaxScale - BaseScale);

        public float MinScale => BaseScale;
        public float CurrentScale => skimmerRoot
            ? (YOnly ? skimmerRoot.lossyScale.y : skimmerRoot.lossyScale.x)
            : BaseScale;

        /// <summary>Current energize phase (for FX and any future HUD readout).</summary>
        public RhinoSwordEnergizePhase EnergizePhase => _energize;

        /// <summary>How much of the energize COOLDOWN is still to run, 1 the instant it starts and 0
        /// once the blade can be charged again (0 outside the cooldown). The HUD's recharge veil.</summary>
        public float EnergizeCooldownRemaining01 =>
            _energize == RhinoSwordEnergizePhase.Cooldown && config != null && config.EnergizeCooldownSeconds > 0f
                ? Mathf.Clamp01((_cooldownEnd - Time.time) / config.EnergizeCooldownSeconds)
                : 0f;

        /// <summary>Charge progress 0..1 while Charging (0 otherwise).</summary>
        public float Charge01 => _energize == RhinoSwordEnergizePhase.Charging && config != null
            ? Mathf.Clamp01((Time.time - _chargeStart) / config.EnergizeHoldSeconds)
            : 0f;

        /// <summary>The sword's own Skimmer (blade root), for the components that share its GameObject.</summary>
        public Skimmer Skimmer => _skimmer;

        /// <summary>The blade transform this driver scales (the skimmer root).</summary>
        public Transform BladeRoot => skimmerRoot;

        /// <summary>True where this sword's state is decided (see <see cref="RhinoSwordNetworkRelay"/>).</summary>
        bool IsAuthority => !relay || relay.IsAuthority;

        // ── IRhinoSwordState ──
        public bool IsEnergized => _energize == RhinoSwordEnergizePhase.Energized;
        public float Energy01 => GetShield01();
        public bool IsSurging => _surging;

        public void AddEnergy(float amount01)
        {
            if (amount01 == 0f || !IsAuthority) return;
            SetShield01(GetShield01() + amount01);
        }

        public bool IsOwnAfterimage(Prism prism) => afterimage && afterimage.IsMine(prism);

        public void Surge(float seconds, float energyFill01)
        {
            if (!IsAuthority || config == null || seconds <= 0f) return;
            float now = Time.time;

            SetShield01(Mathf.Max(GetShield01(), Mathf.Clamp01(energyFill01)));
            if (_energize != RhinoSwordEnergizePhase.Energized)
                SetEnergizePhase(RhinoSwordEnergizePhase.Energized, now);

            _surgeEnd = _surging ? Mathf.Max(_surgeEnd, now + seconds) : now + seconds;
            _surging = true;
            // A surge is not held by the stance, so the tail rules start from "already let go".
            _releasedSinceIgnition = true;
            _tailCounting = false;

            StartBurst();
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

        /// <summary>The surge's look: the blade swells in all three dimensions, scaled by the energy at
        /// that instant, holds, and returns. Photons and reach only - it spends nothing (the burst
        /// used to drain the whole meter on EVERY elemental crystal the sword touched, which is
        /// every petal a Rhino collects).</summary>
        void StartBurst()
        {
            if (config == null || !skimmerRoot) return;
            float energy = Mathf.Clamp01(GetShield01());
            float factor = Mathf.Lerp(1f, config.CrystalBurstFactorAtFullEnergy, energy);

            // A burst only ever GROWS the blade: the length target starts from the authored
            // silhouette × factor but never drops below the current length (a Space-lengthened
            // blade at low energy must not contract), and never exceeds MaxScale unless the blade
            // is already longer.
            float parentY = skimmerRoot.parent ? skimmerRoot.parent.lossyScale.y : 1f;
            float currentWorldY = skimmerRoot.lossyScale.y;
            float burstWorldY = Mathf.Max(currentWorldY, _authoredShape.y * parentY * factor);
            burstWorldY = Mathf.Min(burstWorldY, Mathf.Max(MaxScale, currentWorldY));

            _burstTargetLocal = new Vector3(
                _authoredShape.x * factor,
                burstWorldY / Mathf.Max(0.0001f, parentY),
                _authoredShape.z * factor);
            _burst = BurstPhase.Growing;

            Fx?.NotifyCrystalBurst(energy);
        }

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
            _authoredShape = skimmerRoot.localScale;
            if (!relay) relay = GetComponentInParent<RhinoSwordNetworkRelay>();
            if (!afterimage) TryGetComponent(out afterimage);
        }

        void OnEnable()
        {
            if (_skimmer)
            {
                _skimmer.HasExternalScaleDriver = true;
                _skimmer.SwordState = this;
                // Skimmer.Awake has run (all Awakes precede all OnEnables on a fresh instance),
                // so the authored silhouette is captured and valid here.
                _authoredShape = _skimmer.AuthoredShape;
            }

            // Pooled/re-enabled vessels start clean.
            _burst = BurstPhase.None;
            _energize = RhinoSwordEnergizePhase.Idle;
            _inStance = false;
            _tailCounting = false;
            _releasedSinceIgnition = false;
            _surging = false;
            _surgeEnd = 0f;
            _cooldownEnd = 0f;
            _bindTurnMultiplier = 1f;
            _wasBound = false;
        }

        void OnDisable()
        {
            if (_skimmer) _skimmer.HasExternalScaleDriver = false;
            ReleaseTurn();
        }

        void Update()
        {
            if (!skimmerRoot || config == null) return;
            float dt = Time.deltaTime;

            if (IsAuthority)
            {
                UpdateEnergize();
                if (relay) relay.Publish(GetShield01(), _energize, _surging);
            }
            else
            {
                MirrorAuthority();
            }

            UpdateBind(dt);
            UpdateScale(dt);
            Fx?.Tick(Mathf.Clamp01(GetShield01()), _energize, Charge01, dt);
        }

        // ── energize ──────────────────────────────────────────────────────────
        void UpdateEnergize()
        {
            float now = Time.time;
            float cost = config.EnergizeCostFraction;

            switch (_energize)
            {
                case RhinoSwordEnergizePhase.Idle:
                    if (_inStance && now >= _cooldownEnd && GetShield01() >= cost)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Charging, now);
                    break;

                case RhinoSwordEnergizePhase.Charging:
                    if (!_inStance || GetShield01() < cost)
                        SetEnergizePhase(RhinoSwordEnergizePhase.Idle, now);
                    else if (now - _chargeStart >= config.EnergizeHoldSeconds)
                    {
                        SetShield01(GetShield01() - cost); // ignition spends the fraction
                        SetEnergizePhase(RhinoSwordEnergizePhase.Energized, now);
                    }
                    break;

                case RhinoSwordEnergizePhase.Energized:
                    if (_surging)
                    {
                        // A surge holds the blade whatever the fingers do. When it ends it hands
                        // over to the ordinary rules with the tail already spent, so the blade
                        // goes out next frame unless Live Wire lets the stance hold it.
                        if (now < _surgeEnd) break;
                        _surging = false;
                        _tailCounting = true;
                        _tailEnd = now;
                        break;
                    }

                    if (_inStance && (!_releasedSinceIgnition || LiveWireActive()))
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

        /// <summary>CHARGE level-5, <b>Live Wire</b>: a return to the stance re-lights the tail.
        /// Through the replicated unlock bit, though only the owner evaluates it - the phase it
        /// decides is what replicates.</summary>
        bool LiveWireActive() => config.LiveWireActive(_skimmer ? _skimmer.VesselStatus : null);

        /// <summary>A non-owning peer: take energy, phase and surge from the owner's relay. The phase
        /// goes through <see cref="SetEnergizePhase"/>, so the ignition re-dispatch, the FX and the
        /// afterimage window fire here exactly as they do on the owner.</summary>
        void MirrorAuthority()
        {
            if (resourceSystem && (uint)shieldIndex < (uint)resourceSystem.Resources.Count
                && !Mathf.Approximately(resourceSystem.Resources[shieldIndex].CurrentAmount, relay.Energy01))
                resourceSystem.SetResourceAmount(shieldIndex, relay.Energy01);

            bool surging = relay.IsSurging;
            if (surging && !_surging) StartBurst();
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

        // ── scale ─────────────────────────────────────────────────────────────
        void UpdateScale(float dt)
        {
            if (_burst != BurstPhase.None)
            {
                UpdateBurst(dt);
                return;
            }

            // Resting length reflects stored energy (Y-only, world units).
            float energy = Mathf.Clamp01(GetShield01());
            float targetWorldY = Mathf.Clamp(BaseScale + energy * Range, BaseScale, MaxScale);
            float nowWorldY = skimmerRoot.lossyScale.y;
            float speed = (targetWorldY >= nowWorldY) ? config.PrismGrowSpeed : config.ShrinkSpeed;
            float nextWorldY = Mathf.MoveTowards(nowWorldY, targetWorldY, speed * dt);

            SetLengthWorldY(nextWorldY);
            OnScaleChanged?.Invoke(nextWorldY, BaseScale, MaxScale);
        }

        void UpdateBurst(float dt)
        {
            Vector3 cur = skimmerRoot.localScale;

            switch (_burst)
            {
                case BurstPhase.Growing:
                {
                    var next = Vector3.MoveTowards(cur, _burstTargetLocal, config.CrystalBurstGrowSpeed * dt);
                    skimmerRoot.localScale = next;
                    if ((next - _burstTargetLocal).sqrMagnitude < 0.0025f)
                    {
                        skimmerRoot.localScale = _burstTargetLocal;
                        _burst = BurstPhase.Holding;
                        _burstHoldEnd = Time.time + config.CrystalBurstHoldSeconds;
                    }
                    break;
                }
                case BurstPhase.Holding:
                    skimmerRoot.localScale = _burstTargetLocal;
                    if (Time.time >= _burstHoldEnd) _burst = BurstPhase.Returning;
                    break;

                case BurstPhase.Returning:
                {
                    Vector3 rest = RestLocalScale();
                    var back = Vector3.MoveTowards(cur, rest, config.CrystalBurstReturnSpeed * dt);
                    skimmerRoot.localScale = back;
                    if ((back - rest).sqrMagnitude < 0.0025f) _burst = BurstPhase.None;
                    break;
                }
            }

            // The HUD reads this as the ENERGY meter, so during a burst report the energy-based
            // resting length (energy just drained to 0), not the transient ballooned Y — otherwise
            // the meter pegs full for the whole hold while the tank is actually empty.
            float energyNow = Mathf.Clamp01(GetShield01());
            float restWorldY = Mathf.Clamp(BaseScale + energyNow * Range, BaseScale, MaxScale);
            OnScaleChanged?.Invoke(restWorldY, BaseScale, MaxScale);
        }

        Vector3 RestLocalScale()
        {
            float energy = Mathf.Clamp01(GetShield01());
            float targetWorldY = Mathf.Clamp(BaseScale + energy * Range, BaseScale, MaxScale);
            float parentY = skimmerRoot.parent ? skimmerRoot.parent.lossyScale.y : 1f;
            return new Vector3(_authoredShape.x, targetWorldY / Mathf.Max(0.0001f, parentY), _authoredShape.z);
        }

        void SetLengthWorldY(float worldY)
        {
            if (YOnly)
            {
                float parentY = skimmerRoot.parent ? skimmerRoot.parent.lossyScale.y : 1f;
                float localY = worldY / Mathf.Max(0.0001f, parentY);
                skimmerRoot.localScale = new Vector3(_authoredShape.x, localY, _authoredShape.z);
                return;
            }

            float parent = skimmerRoot.parent ? skimmerRoot.parent.lossyScale.x : 1f;
            float local = worldY / Mathf.Max(0.0001f, parent);
            skimmerRoot.localScale = new Vector3(local, local, local);
        }

        // ── energy resource ───────────────────────────────────────────────────
        float GetShield01()
        {
            if (!resourceSystem) return 0f;
            if ((uint)shieldIndex >= (uint)resourceSystem.Resources.Count) return 0f;
            return Mathf.Clamp01(resourceSystem.Resources[shieldIndex].CurrentAmount);
        }

        void SetShield01(float v)
        {
            // A mirror never writes its own verdict: the owner's value arrives through the relay.
            if (!resourceSystem || !IsAuthority) return;
            if ((uint)shieldIndex >= (uint)resourceSystem.Resources.Count) return;
            resourceSystem.SetResourceAmount(shieldIndex, Mathf.Clamp01(v));
        }
    }
}
