using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselShrinkSkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselShrinkSkimmerEffectSO")]
    public sealed class VesselShrinkSkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Temporary Shrink")]
        [SerializeField, Tooltip("Units to subtract from skimmer scale on each hit.")]
        private float shrinkUnits = 5f;

        [SerializeField, Tooltip("Seconds to hold the shrunken size before restoring.")]
        private float restoreDelaySeconds = 2f;

        [SerializeField, Tooltip("Absolute minimum scale value to clamp at.")]
        private float minScale = 0.1f;

        [Header("Smoothing")]
        [SerializeField, Tooltip("Seconds to lerp from current → shrunken size.")]
        private float shrinkLerpSeconds = 0.15f;

        [SerializeField, Tooltip("Seconds to lerp from current → baseline when restoring.")]
        private float restoreLerpSeconds = 0.25f;

        [Header("Per-attacker cooldown")]
        [SerializeField, Tooltip("After triggering a shrink, the same attacker must wait this many seconds before it can trigger again.")]
        private float attackerCooldownSeconds = 1.5f;

        private readonly Dictionary<Skimmer, float>     _baselineBySkimmer   = new();
        private readonly Dictionary<Skimmer, Coroutine> _restoreBySkimmer    = new();
        private readonly Dictionary<Skimmer, Coroutine> _activeLerpBySkimmer = new();
        private readonly Dictionary<IVessel, float> _attackerCooldownUntil = new();

        public override void Execute(VesselImpactor vesselImpactor, SkimmerImpactor skimmerImpactee)
        {
            var attacker = vesselImpactor?.Vessel;
            var skimmer  = skimmerImpactee?.Skimmer;

            if (_attackerCooldownUntil.TryGetValue(attacker, out var until) && Time.time < until)
            {
                // on cooldown
                return;
            }
            _attackerCooldownUntil[attacker] = Time.time + Mathf.Max(0f, attackerCooldownSeconds);

            if (skimmer == null) return;
            
            var currentScale = skimmer.transform.localScale.x;
            _baselineBySkimmer.TryAdd(skimmer, currentScale);
            
            var baseline = _baselineBySkimmer[skimmer];
            var target = Mathf.Max(minScale, currentScale - Mathf.Abs(shrinkUnits));
            RestartLerp(skimmer, currentScale, target, shrinkLerpSeconds);

            if (_restoreBySkimmer.TryGetValue(skimmer, out var runningRestore) && runningRestore != null)
                skimmer.StopCoroutine(runningRestore);

            _restoreBySkimmer[skimmer] = skimmer.StartCoroutine(RestoreAfterDelay(skimmer, baseline));
        }

        private IEnumerator RestoreAfterDelay(Skimmer skimmer, float baseline)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, restoreDelaySeconds));
            if (!skimmer) yield break;

            float current = skimmer.transform.localScale.x;

            RestartLerp(skimmer, current, baseline, restoreLerpSeconds);
            yield return new WaitForSeconds(Mathf.Max(0f, restoreLerpSeconds));
            _restoreBySkimmer.Remove(skimmer);
            _baselineBySkimmer.Remove(skimmer);
        }

        private void RestartLerp(Skimmer skimmer, float from, float to, float seconds)
        {
            if (_activeLerpBySkimmer.TryGetValue(skimmer, out var running) && running != null)
                skimmer.StopCoroutine(running);

            _activeLerpBySkimmer[skimmer] = skimmer.StartCoroutine(LerpScale(skimmer, from, to, Mathf.Max(0f, seconds)));
        }

        private IEnumerator LerpScale(Skimmer skimmer, float from, float to, float seconds)
        {
            if (!skimmer) yield break;

            if (seconds <= 0f)
            {
                SetSkimmerScale(skimmer, to);
                yield break;
            }

            float t = 0f;
            while (t < seconds && skimmer)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / seconds);
                float v = Mathf.Lerp(from, to, a);
                SetSkimmerScale(skimmer, v);
                yield return null;
            }

            if (skimmer) SetSkimmerScale(skimmer, to);
            _activeLerpBySkimmer.Remove(skimmer);
        }

        private void SetSkimmerScale(Skimmer skimmer, float value)
        {
            try
            {
                var field = typeof(Skimmer).GetField("Scale",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    var elem = field.GetValue(skimmer);
                    var prop = elem.GetType().GetProperty("Value");
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(elem, value);
                        field.SetValue(skimmer, elem);
                        // ensure immediate feedback
                        skimmer.transform.localScale = Vector3.one * value;
                        return;
                    }
                }
            }
            catch { /* ignore and fallback */ }

            skimmer.transform.localScale = Vector3.one * value;
        }
    }
}
