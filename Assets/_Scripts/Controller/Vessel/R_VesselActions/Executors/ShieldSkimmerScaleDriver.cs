using System;
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
    ///  • GAIN: the prism effect banks energy per prism the sword destroys (an omni-crystal
    ///    pickup also sets it to full via the Rhino's vessel crystal effect).
    ///  • SPEND: energizing costs <see cref="ShieldSkimmerScaleConfigSO.EnergizeCostFraction"/>;
    ///    an elemental-crystal hit (<see cref="TriggerCrystalBurst"/>) bursts the blade in all
    ///    three dimensions scaled by the energy at that instant, then drains it all.
    ///
    /// ENERGIZE — the supershield key. Holding the both-triggers lower/chop stance
    /// (<see cref="SetInStance"/>, fed by <see cref="ShieldSwipeActionExecutor"/>) for
    /// <c>EnergizeHoldSeconds</c> spends the cost and ignites the blade: while
    /// <see cref="IsEnergized"/> the damage effect POPS super-shielded prisms instead of
    /// bouncing. Leaving the stance starts the <c>EnergizedTailSeconds</c> countdown, then
    /// <c>EnergizeCooldownSeconds</c> locks re-charging. On the ignition edge the standing
    /// blade contacts are RE-DISPATCHED (both the box-trigger overlaps and the shell-tier
    /// pairs) so a super-shielded prism already resting against the blade pops without
    /// needing a fresh OnTriggerEnter.
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

        /// <summary>Charge progress 0..1 while Charging (0 otherwise).</summary>
        public float Charge01 => _energize == RhinoSwordEnergizePhase.Charging && config != null
            ? Mathf.Clamp01((Time.time - _chargeStart) / config.EnergizeHoldSeconds)
            : 0f;

        // ── IRhinoSwordState ──
        public bool IsEnergized => _energize == RhinoSwordEnergizePhase.Energized;
        public float Energy01 => GetShield01();

        public void AddEnergy(float amount01)
        {
            if (amount01 == 0f) return;
            SetShield01(GetShield01() + amount01);
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

        public void TriggerCrystalBurst()
        {
            if (config == null || !skimmerRoot) return;
            float energy = Mathf.Clamp01(GetShield01());
            float factor = Mathf.Lerp(1f, config.CrystalBurstFactorAtFullEnergy, energy);

            // A burst only ever GROWS the blade: the length target starts from the authored
            // silhouette × factor but never drops below the current length (a Space-lengthened
            // blade at low energy must not contract), and never exceeds the (debuff-aware)
            // MaxScale unless the blade is already longer.
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

            SetShield01(0f); // hitting a crystal consumes ALL energy
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
            _cooldownEnd = 0f;
        }

        void OnDisable()
        {
            if (_skimmer) _skimmer.HasExternalScaleDriver = false;
        }

        void Update()
        {
            if (!skimmerRoot || config == null) return;
            float dt = Time.deltaTime;

            UpdateEnergize();
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
                    if (_inStance)
                    {
                        _tailCounting = false; // holding the stance keeps it lit
                    }
                    else if (!_tailCounting)
                    {
                        _tailCounting = true;
                        _tailEnd = now + config.EnergizedTailSeconds;
                    }
                    else if (now >= _tailEnd)
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

        void SetEnergizePhase(RhinoSwordEnergizePhase phase, float now)
        {
            if (_energize == phase) return;
            _energize = phase;

            switch (phase)
            {
                case RhinoSwordEnergizePhase.Charging:
                    _chargeStart = now;
                    break;
                case RhinoSwordEnergizePhase.Energized:
                    _tailCounting = false;
                    // A super-shielded prism already RESTING against the blade gets no fresh
                    // OnTriggerEnter (and its shell-tier pair was dispatched once on entry),
                    // so the ignition edge re-runs the standing contacts through the same
                    // effect chains — the pop the player just paid for lands immediately.
                    RedispatchStandingBladeContacts();
                    break;
            }

            Fx?.NotifyEnergizePhaseChanged(phase);
        }

        void RedispatchStandingBladeContacts()
        {
            if (!_skimmerImpactor) return;
            _skimmerImpactor.ReapplyPrismEffectsToOverlapping();
            PrismShellContactManager.RedispatchPairsForOwner(_skimmerImpactor);
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
            if (!resourceSystem) return;
            if ((uint)shieldIndex >= (uint)resourceSystem.Resources.Count) return;
            resourceSystem.SetResourceAmount(shieldIndex, Mathf.Clamp01(v));
        }
    }
}
