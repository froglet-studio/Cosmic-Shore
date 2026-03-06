using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Overtake impact effect: when a vessel collides with an opponent's skimmer,
    /// the slower vessel gets all its elements debuffed below baseline (into the first 5 pips)
    /// with haptics. The debuff recovers over time back to 0 (baseline).
    /// Nothing happens to the faster vessel.
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

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.8f;
        [SerializeField] private float hapticFrequency = 0.7f;
        [SerializeField] private float hapticDuration = 0.25f;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between overtake penalties on the same vessel")]
        [SerializeField] private float cooldown = 1f;

        static readonly Element[] AllElements =
            { Element.Mass, Element.Charge, Element.Space, Element.Time };

        // Per-vessel tracking: last penalty time and active recovery state
        private static readonly Dictionary<ResourceSystem, float> _lastPenaltyTime = new();
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

            // Only the slower vessel gets penalized
            if (impactorSpeed >= impacteeSpeed) return;

            // The impactor (vessel that hit the skimmer) is the slower one — penalize them
            var slowerStatus = impactorVessel.VesselStatus;
            var rs = slowerStatus.ResourceSystem;
            if (rs == null) return;

            // Cooldown check
            var now = Time.time;
            if (_lastPenaltyTime.TryGetValue(rs, out var lastTime))
            {
                if (now - lastTime < cooldown)
                    return;
            }

            _lastPenaltyTime[rs] = now;

            // Haptic feedback
            HapticController.PlayConstant(hapticAmplitude, hapticFrequency, hapticDuration);

            // Slam all elements to penalty level and start recovery
            var recovery = new OvertakeRecovery
            {
                ResourceSystem = rs,
                PenaltyLevel = penaltyLevel,
                RecoveryDuration = recoveryDuration,
                ElapsedTime = 0f,
            };

            // Begin overtake on the element bars so pips can go below baseline
            var elementBars = slowerStatus.Silhouette?.ElementBars;
            recovery.ElementBars = elementBars;

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
