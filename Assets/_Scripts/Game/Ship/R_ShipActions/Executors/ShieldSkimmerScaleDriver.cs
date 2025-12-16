using System;
using System.Collections;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Reads Shield resource (0..1, MaxAmount=1) and drives skimmer uniform XYZ scale.
    /// Handles tick-based decay, reset delay, and crystal 5s hold.
    ///
    /// Prism/Crystal impact effects must ONLY change Shield resource (ResourceSystem),
    /// and any skimmer-size debuff is applied by modifying ShieldSkimmerScaleConfigSO.
    /// </summary>
    public sealed class ShieldSkimmerScaleDriver : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private Transform skimmerRoot;

        [Header("Config")]
        [SerializeField] private ShieldSkimmerScaleConfigSO config;

        [Header("Shield Resource Index (normalized 0..1)")]
        [SerializeField] private int shieldIndex = 0;

        public event Action<float, float, float> OnScaleChanged;

        enum CrystalState { None, Armed, Holding }

        CrystalState _crystalState = CrystalState.None;
        float _crystalHoldEndTime;

        float _targetWorld;
        float _prevShield01;
        float _noDecayUntil;

        Coroutine _tickLoop;

        // Convenience accessors
        float BaseScale  => config ? config.BaseScale        : 30f;
        float MaxScale   => config ? config.MaxScale         : 120f;
        float PrismMax   => config ? config.PrismMaxScale    : 100f;
        float StepUnits  => config ? config.StepScaleUnits   : 5f;
        float TickSecs   => config ? config.TickSeconds      : 0.25f;
        float ResetDelay => config ? config.ResetDelaySeconds: 1f;
        float HoldSecs   => config ? config.CrystalHoldSeconds : 5f;
        float PrismGrow  => config ? config.PrismGrowSpeed   : 300f;
        float CrystalGrow=> config ? config.CrystalGrowSpeed : 800f;
        float Shrink     => config ? config.ShrinkSpeed      : 400f;
        float HoldEps    => config ? config.MaxHoldEpsilon   : 0.05f;

        float Range => Mathf.Max(0.0001f, MaxScale - BaseScale);

        float Step01 => StepUnits / Range;

        float PrismCap01
        {
            get
            {
                float cap = (PrismMax - BaseScale) / Range;
                return Mathf.Clamp01(cap);
            }
        }

        public float MinScale => BaseScale;
        public float CurrentScale => skimmerRoot ? skimmerRoot.lossyScale.x : BaseScale;

        void Awake()
        {
            if (!skimmerRoot) skimmerRoot = transform;
            _targetWorld = CurrentScale;
        }

        void OnEnable()
        {
            if (!resourceSystem) return;

            _prevShield01 = GetShield01();
            UpdateTargetFromShield(_prevShield01);

            resourceSystem.OnResourceChanged += OnResourceChanged;

            if (_tickLoop == null)
                _tickLoop = StartCoroutine(TickLoop());
        }

        void OnDisable()
        {
            if (resourceSystem)
                resourceSystem.OnResourceChanged -= OnResourceChanged;

            if (_tickLoop != null) StopCoroutine(_tickLoop);
            _tickLoop = null;
        }

        void OnResourceChanged(int index, float current, float max)
        {
            if (index != shieldIndex) return;

            float now   = Time.time;
            float cur01 = Mathf.Clamp01(current);

            // Detect CRYSTAL hit: Shield set to 1
            if (cur01 >= 0.999f && _prevShield01 < 0.999f)
            {
                _crystalState = CrystalState.Armed;
                _crystalHoldEndTime = 0f;

                // While ARMED, block decay until we actually reach MaxScale and start hold.
                _noDecayUntil = float.PositiveInfinity;
            }

            // Prism-like reset:
            bool increased = cur01 > _prevShield01 + 0.0001f;
            bool crystalCancelledByPrism = (_prevShield01 >= 0.999f && cur01 <= PrismCap01 + 0.0001f);

            if (increased || crystalCancelledByPrism)
            {
                if (crystalCancelledByPrism)
                {
                    _crystalState = CrystalState.None;
                    _crystalHoldEndTime = 0f;
                }

                _noDecayUntil = now + ResetDelay;
            }

            _prevShield01 = cur01;

            if (_crystalState != CrystalState.Holding)
                UpdateTargetFromShield(cur01);
        }

        void Update()
        {
            if (!skimmerRoot) return;

            // If currently holding from crystal, pin at MaxScale.
            if (_crystalState == CrystalState.Holding)
            {
                SetWorldUniform(MaxScale);
                OnScaleChanged?.Invoke(MaxScale, BaseScale, MaxScale);
                return;
            }

            float nowWorld = CurrentScale;

            float speed;
            if (_targetWorld >= nowWorld)
            {
                // Growing
                speed = (_crystalState == CrystalState.Armed) ? CrystalGrow : PrismGrow;
            }
            else
            {
                // Shrinking
                speed = Shrink;
            }

            // Clamp target in case MaxScale changed due to debuff.
            _targetWorld = Mathf.Clamp(_targetWorld, BaseScale, MaxScale);

            float nextWorld = Mathf.MoveTowards(nowWorld, _targetWorld, speed * Time.deltaTime);

            // Crystal: start HOLD once visually near MaxScale
            if (_crystalState == CrystalState.Armed &&
                Mathf.Abs(nextWorld - MaxScale) <= HoldEps)
            {
                _crystalState = CrystalState.Holding;
                _crystalHoldEndTime = Time.time + HoldSecs;

                _noDecayUntil = _crystalHoldEndTime;
                nextWorld = MaxScale; // snap
            }

            SetWorldUniform(nextWorld);
            OnScaleChanged?.Invoke(nextWorld, BaseScale, MaxScale);
        }

        IEnumerator TickLoop()
        {
            var wait = new WaitForSeconds(TickSecs);

            while (true)
            {
                yield return wait;

                float now = Time.time;

                // While holding from crystal, don't decay until hold ends
                if (_crystalState == CrystalState.Holding)
                {
                    if (now < _crystalHoldEndTime)
                        continue;

                    _crystalState = CrystalState.None;
                }

                // Respect reset delay (or +∞ while ARMED)
                if (now < _noDecayUntil)
                    continue;

                float cur01 = GetShield01();
                if (cur01 <= 0f)
                    continue;

                float next01 = Mathf.Max(0f, cur01 - Step01);
                SetShield01(next01);

                _prevShield01 = next01;

                if (_crystalState != CrystalState.Holding)
                    UpdateTargetFromShield(next01);
            }
        }

        void UpdateTargetFromShield(float shield01)
        {
            _targetWorld = BaseScale + Mathf.Clamp01(shield01) * Range;
        }

        void SetWorldUniform(float world)
        {
            float parent = (skimmerRoot.parent) ? skimmerRoot.parent.lossyScale.x : 1f;
            float local  = world / Mathf.Max(0.0001f, parent);
            skimmerRoot.localScale = new Vector3(local, local, local);
        }

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
