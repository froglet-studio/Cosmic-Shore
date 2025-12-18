using System;
using System.Collections;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ShieldSkimmerScaleDriver : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ResourceSystem resourceSystem;
        [SerializeField] private Transform skimmerRoot;

        [Header("Config")]
        [SerializeField] private ShieldSkimmerScaleConfigSO config;

        [Header("Shield Resource Index (normalized 0..1)")]
        [SerializeField] private int shieldIndex = 0;

        public event Action<float, float, float> OnScaleChanged;

        private enum CrystalState { None, Armed, Holding }

        CrystalState _crystalState = CrystalState.None;
        float _crystalHoldEndTime;

        float _targetWorld;
        float _prevShield01;
        float _noDecayUntil;     // time until which no decay is allowed (reset delay / crystal hold)

        Coroutine _tickLoop;

        float BaseScale  => config.BaseScale;
        float MaxScale   =>config.MaxScale;
        float PrismMax   => config.PrismMaxScale;
        float StepUnits  => config.StepScaleUnits;
        float TickSecs   => config.TickSeconds;
        float ResetDelay => config.ResetDelaySeconds;
        float HoldSecs   => config.CrystalHoldSeconds;
        float PrismGrow  => config.PrismGrowSpeed;
        float CrystalGrow=> config.CrystalGrowSpeed;
        float Shrink     => config.ShrinkSpeed;
        float HoldEps    => config.MaxHoldEpsilon;

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

            if (cur01 >= 0.999f && _prevShield01 < 0.999f)
            {
                _crystalState = CrystalState.Armed;
                _crystalHoldEndTime = 0f;

                _noDecayUntil = float.PositiveInfinity;
            }

            switch (_crystalState)
            {
                case CrystalState.Armed or CrystalState.Holding when cur01 < 0.999f:
                    SetShield01(1f);
                    cur01 = 1f;
                    break;
                case CrystalState.None:
                {
                    bool increased = cur01 > _prevShield01 + 0.0001f;
                    if (increased)
                    {
                        _noDecayUntil = now + ResetDelay;
                    }

                    break;
                }
            }

            _prevShield01 = cur01;
            
            if (_crystalState != CrystalState.Holding)
                UpdateTargetFromShield(cur01);
        }

        void Update()
        {
            if (!skimmerRoot) return;

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
                speed = (_crystalState == CrystalState.Armed) ? CrystalGrow : PrismGrow;
            }
            else
            {
                speed = Shrink;
            }
            
            _targetWorld = Mathf.Clamp(_targetWorld, BaseScale, MaxScale);

            float nextWorld = Mathf.MoveTowards(nowWorld, _targetWorld, speed * Time.deltaTime);

            if (_crystalState == CrystalState.Armed &&
                Mathf.Abs(nextWorld - MaxScale) <= HoldEps)
            {
                _crystalState = CrystalState.Holding;
                _crystalHoldEndTime = Time.time + HoldSecs;

                _noDecayUntil = _crystalHoldEndTime;

                nextWorld = MaxScale;
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

                // If holding from crystal, do not decay until hold time is over.
                if (_crystalState == CrystalState.Holding)
                {
                    if (now < _crystalHoldEndTime)
                        continue;

                    // Hold finished – go back to normal and allow decay.
                    _crystalState = CrystalState.None;
                }

                // Respect reset delay (or +∞ while in Armed state).
                if (now < _noDecayUntil)
                    continue;

                float cur01 = GetShield01();
                if (cur01 <= 0f)
                    continue;

                // Decay Shield by same step size as prism growth.
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
