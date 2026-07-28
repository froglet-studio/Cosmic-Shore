using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino energy sword's brain. Owns the sword transform's SCALE and the sword's
    /// energy/FX state, and implements <see cref="IRhinoSwordState"/> so the shared
    /// impact-effect SOs can read/drive it via <c>Skimmer.SwordState</c>.
    ///
    /// The sword itself is UNGATED — it always damages prisms on contact and always pops
    /// super-shields (that logic lives in <c>RhinoSkimmerDamagePrismEffectSO</c>). This class
    /// only tracks ENERGY — the Rhino's Shield resource (<see cref="shieldIndex"/>, normalized
    /// 0..1, no passive decay: a meter you fill and spend):
    ///  • GAIN: the prism effect banks energy per prism the sword destroys (an omni-crystal
    ///    pickup also sets it to full via the Rhino's vessel crystal effect).
    ///  • SPEND: an elemental-crystal hit (<see cref="TriggerCrystalBurst"/>) bursts the blade
    ///    in all three dimensions scaled by the energy at that instant, then drains it all.
    ///
    /// The resting length reflects stored energy (Y-only elongation from the Space-driven
    /// elemental base — the same meter the Rhino HUD draws through <see cref="OnScaleChanged"/>),
    /// and the blade's look heats with energy (<see cref="RhinoSwordVisualizer"/>). Sets
    /// <c>Skimmer.HasExternalScaleDriver</c> so the Skimmer's own elemental scale write stands
    /// down. The swipe pose (rotation/position) is owned by <see cref="ShieldSwipeActionExecutor"/>
    /// — only scale is ours. See <c>RHINO_ENERGY_SWORD.md</c>.
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
        readonly RhinoSwordVisualizer _visual = new();

        Vector3 _authoredShape; // local X/Y/Z silhouette captured from the Skimmer

        // ── crystal burst ──
        enum BurstPhase { None, Growing, Holding, Returning }
        BurstPhase _burst = BurstPhase.None;
        Vector3 _burstTargetLocal;
        float _burstHoldEnd;

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

        // ── IRhinoSwordState ──
        public float Energy01 => GetShield01();

        public void AddEnergy(float amount01)
        {
            if (amount01 == 0f) return;
            SetShield01(GetShield01() + amount01);
        }

        public void NotifyPrismDestroyed(bool superShielded)
        {
            if (config == null) return;
            _visual.Flash(superShielded ? config.PopFlashAmount : config.HitFlashAmount);
            if (superShielded)
                TryShakeCamera(config.PopShakeIntensity, config.PopShakeDuration);
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

            _visual.Flash(config.PopFlashAmount);
            TryShakeCamera(config.BurstShakeMaxIntensity * energy, config.BurstShakeDuration);

            SetShield01(0f); // hitting a crystal consumes ALL energy
        }

        void Awake()
        {
            if (!skimmerRoot) skimmerRoot = transform;
            skimmerRoot.TryGetComponent(out _skimmer);
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

            _burst = BurstPhase.None; // pooled/re-enabled vessels start clean

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

            UpdateScale(dt);
            _visual.Tick(Mathf.Clamp01(GetShield01()), dt);
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

        // ── juice ─────────────────────────────────────────────────────────────
        // Local human pilot only (and not while autopiloting, e.g. the Menu_Main lava lamp) —
        // remote/AI Rhinos must not rattle this client's camera.
        void TryShakeCamera(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            var status = _skimmer ? _skimmer.VesselStatus : null;
            if (status == null || !status.IsLocalUser || status.AutoPilotEnabled) return;

            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(intensity, duration);
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
