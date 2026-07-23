using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino energy sword's brain. Owns the sword transform's SCALE and all energy-sword
    /// state, and implements <see cref="IRhinoSwordState"/> so the shared impact-effect SOs can
    /// read/drive it via <c>Skimmer.SwordState</c>.
    ///
    /// ENERGY is the Rhino's Shield resource (<see cref="shieldIndex"/>, normalized 0..1). It is
    /// gained when a slash destroys a prism and spent two ways:
    ///  • CRYSTAL HIT (<see cref="TriggerCrystalBurst"/>): the blade bursts in all three dimensions
    ///    scaled by the energy at the moment of the hit, then ALL energy is consumed. Full energy
    ///    reaches the authored max scale; less energy scales the burst down.
    ///  • ENERGIZE (hold the lower/chop stance &gt; <c>EnergizeHoldSeconds</c>): costs
    ///    <c>EnergizeCostFraction</c> (1/10) of max energy and lights the blade up (white + tracers),
    ///    lets it pop super-shielded prisms, and drops the slash cooldown to 0. It stays energized
    ///    for <c>EnergizedTailSeconds</c> after leaving the stance, then locks out for
    ///    <c>EnergizeCooldownSeconds</c>.
    ///
    /// While NOT bursting the blade's resting length reflects stored energy (Y-only elongation, the
    /// same energy meter the Rhino HUD reads through <see cref="OnScaleChanged"/>). Sets
    /// <c>Skimmer.HasExternalScaleDriver</c> so the Skimmer's own elemental scale write stands down.
    /// The swipe pose (rotation/position) is owned by <see cref="ShieldSwipeActionExecutor"/> — only
    /// scale is ours. See <c>RHINO_ENERGY_SWORD.md</c>.
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
        readonly RhinoSwordVisualizer _visual = new();

        Vector3 _authoredShape; // local X/Y/Z silhouette captured from the Skimmer

        // ── crystal burst ──
        enum BurstPhase { None, Growing, Holding, Returning }
        BurstPhase _burst = BurstPhase.None;
        Vector3 _burstTargetLocal;
        float _burstHoldEnd;

        // ── energize state machine ──
        enum EnergizeState { Idle, Charging, Energized, Cooldown }
        EnergizeState _energize = EnergizeState.Idle;
        float _chargeStart, _tailEnd, _cooldownEnd;
        bool _tailCounting;
        bool _inStance;

        // ── slashing ──
        bool _isSlashing;
        float _nextSlashTime;

        // Sword capsules (Skimmer.ElongateYOnly): the resting length grows only local Y and
        // preserves the authored X/Z silhouette; the crystal burst overrides all three dims.
        bool YOnly => _skimmer && _skimmer.ElongateYOnly;

        // Resting size is the skimmer's live elemental (Space-driven) scale — element levels
        // lengthen the sword and energy growth composes on top. Falls back to the config value.
        float BaseScale => _skimmer ? Mathf.Max(0.01f, _skimmer.LiveElementalScale) : config.BaseScale;
        float MaxScale  => config.MaxScale;
        float Range     => Mathf.Max(0.0001f, MaxScale - BaseScale);

        public float MinScale => BaseScale;
        public float CurrentScale => skimmerRoot
            ? (YOnly ? skimmerRoot.lossyScale.y : skimmerRoot.lossyScale.x)
            : BaseScale;

        // ── IRhinoSwordState ──
        public bool IsEnergized => _energize == EnergizeState.Energized;
        public float Energy01 => GetShield01();

        public bool CanSlashDamage => _isSlashing && (IsEnergized || Time.time >= _nextSlashTime);

        public void NotifySlashLanded() =>
            _nextSlashTime = Time.time + (IsEnergized ? 0f : config.SlashCooldownSeconds);

        public void AddEnergy(float amount01)
        {
            if (amount01 == 0f) return;
            SetShield01(GetShield01() + amount01);
        }

        public void TriggerCrystalBurst()
        {
            if (config == null) return;
            float energy = Mathf.Clamp01(GetShield01());
            float factor = Mathf.Lerp(1f, config.CrystalBurstFactorAtFullEnergy, energy);

            _burstTargetLocal = new Vector3(
                _authoredShape.x * factor,
                _authoredShape.y * factor,
                _authoredShape.z * factor);
            _burst = BurstPhase.Growing;

            SetShield01(0f); // hitting a crystal consumes ALL energy
        }

        public void SetInStance(bool inStance) => _inStance = inStance;

        public void SetSlashing(bool slashing)
        {
            bool rising = slashing && !_isSlashing;
            _isSlashing = slashing;

            // A slash that begins with a prism already inside the blade sees no fresh
            // OnTriggerEnter, so re-apply the prism effects to whatever is overlapping right now.
            // The effects self-gate on CanSlashDamage (which is true this frame), so this only
            // bites when a slash actually opens.
            if (rising && _skimmerImpactor) _skimmerImpactor.ReapplyPrismEffectsToOverlapping();
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

            // reset runtime state so a pooled/re-enabled vessel starts clean
            _burst = BurstPhase.None;
            _energize = EnergizeState.Idle;
            _isSlashing = false;
            _inStance = false;
            _tailCounting = false;
            _nextSlashTime = 0f;

            _visual.Setup(skimmerRoot, config);
        }

        void OnDisable()
        {
            if (_skimmer) _skimmer.HasExternalScaleDriver = false;
            _visual.Teardown();
        }

        void Update()
        {
            if (!skimmerRoot || config == null) return;
            float dt = Time.deltaTime;

            UpdateEnergize();
            UpdateScale(dt);
            _visual.Tick(IsEnergized, dt);
        }

        // ── energize ──────────────────────────────────────────────────────────
        void UpdateEnergize()
        {
            float now = Time.time;
            float cost = config.EnergizeCostFraction;

            switch (_energize)
            {
                case EnergizeState.Idle:
                    if (_inStance && now >= _cooldownEnd && GetShield01() >= cost)
                    {
                        _energize = EnergizeState.Charging;
                        _chargeStart = now;
                    }
                    break;

                case EnergizeState.Charging:
                    if (!_inStance || GetShield01() < cost)
                        _energize = EnergizeState.Idle;
                    else if (now - _chargeStart >= config.EnergizeHoldSeconds)
                    {
                        SetShield01(GetShield01() - cost); // spend 1/10 of max energy
                        _energize = EnergizeState.Energized;
                        _tailCounting = false;
                    }
                    break;

                case EnergizeState.Energized:
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
                        _energize = EnergizeState.Cooldown;
                        _cooldownEnd = now + config.EnergizeCooldownSeconds;
                    }
                    break;

                case EnergizeState.Cooldown:
                    if (now >= _cooldownEnd) _energize = EnergizeState.Idle;
                    break;
            }
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

            OnScaleChanged?.Invoke(skimmerRoot.lossyScale.y, BaseScale, MaxScale);
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
