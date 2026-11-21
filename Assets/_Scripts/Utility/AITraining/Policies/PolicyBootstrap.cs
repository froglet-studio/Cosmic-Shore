using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Single entry point that wires every built-in policy and fitness component into
    /// the gene registry. Splitting this out makes it obvious where to add a new
    /// policy: drop the file in the Policies folder and append it here.
    ///
    /// Registration runs in both edit and play mode so the editor window can show the
    /// full search space before pressing Play.
    /// </summary>
    public static class PolicyBootstrap
    {
        static bool s_Initialized;
        static readonly List<IDecisionPolicy> s_Policies = new();

        public static IReadOnlyList<IDecisionPolicy> RegisteredPolicies => s_Policies;

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        static void EditorInit() => EnsureInitialized();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RuntimeInit() => EnsureInitialized();

        public static void EnsureInitialized()
        {
            if (s_Initialized) return;
            s_Initialized = true;

            Register(new TargetSeekingPolicy());
            Register(new ObstacleAvoidancePolicy());
            Register(new ThrottleControlPolicy());
            Register(new DriftPolicy());
            Register(new SkimPolicy());
            Register(new BoostManagementPolicy());
            Register(new AbilitySchedulerPolicy());
            Register(new ThreatEngagementPolicy());
        }

        public static void Register(IDecisionPolicy policy)
        {
            if (policy == null) return;
            policy.RegisterGenes();
            s_Policies.Add(policy);
        }
    }
}
