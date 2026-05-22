using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Overtake impact effect for the Squirrel's skimmer: when a slower vessel collides with the
    /// Squirrel's skimmer (the Squirrel is overtaking it), the slower vessel's elements are affected.
    /// - Opponent (different domain): all elements are debuffed below baseline (into the first 5
    ///   pips) with haptics, recovering over time back to 0 (baseline).
    /// - Ally (same domain): all elements are buffed up additively instead.
    /// Nothing happens to the faster (Squirrel) vessel.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselOvertakeBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselOvertakeBySkimmerEffectSO")]
    public class VesselOvertakeBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Penalty")]
        [Tooltip("Normalized level to slam all elements to on overtake (-0.5 = level -5)")]
        [SerializeField] private float penaltyLevel = -0.5f;

        [Tooltip("Seconds to recover from penalty back to baseline (0)")]
        [SerializeField] private float recoveryDuration = 3f;

        [Header("Ally Buff")]
        [Tooltip("Amount each element is raised when the Squirrel overtakes a same-domain ally (additive, normalized — 0.5 = +5 levels)")]
        [SerializeField] private float allyBuffAmount = 0.5f;

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.8f;
        [SerializeField] private float hapticFrequency = 0.7f;
        [SerializeField] private float hapticDuration = 0.25f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between overtake penalties on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        // Per-vessel tracking: last overtake-effect time (cooldown) and active debuff recovery state
        private static readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();
        private static readonly Dictionary<ResourceSystem, OvertakeRecovery> _activeRecoveries = new();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null) return;
            if (impactee == null || impactee.Skimmer?.VesselStatus?.Vessel == null) return;

            var impactorVessel = impactor.Vessel;
            var impacteeVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Don't trigger on self-collision
            if (impactorVessel == impacteeVessel) return;

            // Determine who is slower
            float impactorSpeed = impactorVessel.VesselStatus.Speed;
            float impacteeSpeed = impacteeVessel.VesselStatus.Speed;

            // Only the slower vessel — the one being overtaken — is affected
            if (impactorSpeed >= impacteeSpeed) return;

            // The impactor (vessel that hit the skimmer) is the slower, overtaken one
            var slowerStatus = impactorVessel.VesselStatus;
            var rs = slowerStatus.ResourceSystem;
            if (rs == null) return;

            // Cooldown check — anti-spam per overtaken vessel
            var now = Time.time;
            if (_lastEffectTime.TryGetValue(rs, out var lastTime))
            {
                if (now - lastTime < cooldown)
                    return;
            }

            _lastEffectTime[rs] = now;

            // Haptic feedback
            HapticController.PlayConstant(hapticAmplitude, hapticFrequency, hapticDuration);

            // Allies (same domain) get their elements buffed; opponents get debuffed.
            if (slowerStatus.Domain == impacteeVessel.VesselStatus.Domain)
                BuffAlly(rs);
            else
                DebuffOpponent(rs, slowerStatus.Silhouette?.ElementBars);
        }

        /// <summary>
        /// Buffs an allied (same-domain) vessel the Squirrel overtook: every element is nudged up
        /// additively. Any in-progress overtake debuff recovery is cleared first so the buff isn't
        /// immediately lerped away by the recovery ticker.
        /// </summary>
        void BuffAlly(ResourceSystem rs)
        {
            if (_activeRecoveries.TryGetValue(rs, out var recovery))
            {
                recovery.ElementBars?.EndOvertake();
                _activeRecoveries.Remove(rs);
            }

            for (int i = 0; i < AllElements.Length; i++)
                rs.AdjustLevel(AllElements[i], allyBuffAmount);
        }

        /// <summary>
        /// Debuffs an opposing (different-domain) vessel the Squirrel overtook: all elements are
        /// slammed below baseline and recover back to 0 (baseline) over <see cref="recoveryDuration"/>.
        /// </summary>
        void DebuffOpponent(ResourceSystem rs, ElementalBarsView elementBars)
        {
            var recovery = new OvertakeRecovery
            {
                ResourceSystem = rs,
                ElementBars = elementBars,
                PenaltyLevel = penaltyLevel,
                RecoveryDuration = recoveryDuration,
                ElapsedTime = 0f,
            };

            // Begin overtake on the element bars so pips can go below baseline
            elementBars?.BeginOvertake();

            // Slam all elements
            for (int i = 0; i < AllElements.Length; i++)
                rs.SetElementLevel(AllElements[i], penaltyLevel);

            // Juice the bars
            elementBars?.JuiceOvertakePenalty();

            _activeRecoveries[rs] = recovery;

            // Ensure the recovery ticker is running
            OvertakeRecoveryTicker.EnsureExists();
        }

        /// <summary>
        /// Ticks all active recoveries. Called by OvertakeRecoveryTicker every frame.
        /// </summary>
        internal static void TickRecoveries()
        {
            if (_activeRecoveries.Count == 0) return;

            List<ResourceSystem> completed = null;

            foreach (var kvp in _activeRecoveries)
            {
                var recovery = kvp.Value;
                recovery.ElapsedTime += Time.deltaTime;

                float t = Mathf.Clamp01(recovery.ElapsedTime / recovery.RecoveryDuration);
                float currentLevel = Mathf.Lerp(recovery.PenaltyLevel, 0f, t);

                for (int i = 0; i < AllElements.Length; i++)
                    recovery.ResourceSystem.SetElementLevel(AllElements[i], currentLevel);

                if (t >= 1f)
                {
                    completed ??= new List<ResourceSystem>();
                    completed.Add(kvp.Key);
                    recovery.ElementBars?.EndOvertake();
                }
            }

            if (completed != null)
            {
                foreach (var rs in completed)
                    _activeRecoveries.Remove(rs);
            }
        }

        private class OvertakeRecovery
        {
            public ResourceSystem ResourceSystem;
            public ElementalBarsView ElementBars;
            public float PenaltyLevel;
            public float RecoveryDuration;
            public float ElapsedTime;
        }
    }

    /// <summary>
    /// Auto-created singleton MonoBehaviour that ticks overtake recovery every frame.
    /// ScriptableObjects can't run Update, so this bridges the gap.
    /// </summary>
    internal class OvertakeRecoveryTicker : MonoBehaviour
    {
        private static OvertakeRecoveryTicker _instance;

        internal static void EnsureExists()
        {
            if (_instance != null) return;
            var go = new GameObject("[OvertakeRecoveryTicker]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<OvertakeRecoveryTicker>();
        }

        void Update()
        {
            VesselOvertakeBySkimmerEffectSO.TickRecoveries();
        }
    }
}
