using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Collects unique impacted objects while active on the local client.
    /// Each Execute() from the skimmer effects should call RegisterImpact(impactee).
    /// When count >= maxCollectedThreshold, triggers an "explosion" (disable/log).
    /// </summary>
    public class OverchargeAction : ShipAction
    {
        [Header("Overcharge Settings")]
        [SerializeField] int  maxCollectedThreshold = 30;
        [SerializeField] bool localOnly = true;
        [Tooltip("If true, disables impacted objects on trigger; else only Debug.Log.")]
        [SerializeField] bool disableOnExplode = false;

        [Header("Debug")]
        [SerializeField] bool verbose = true;

        public event Action<int,int> OnCountChanged;        // (current, max)
        public event Action<int>     OnThresholdTriggered;  // final count

        readonly HashSet<int> _collectedIds = new();
        readonly List<Component> _collectedObjs = new();

        // Required by ShipAction but unused here (collision-driven via impact effect)
        public override void StartAction() { /* no-op */ }
        public override void StopAction()  { /* no-op */ }

        /// <summary>Called once per unique impactee (from the impact effect).</summary>
        public void RegisterImpact(Component impactee)
        {
            if (!IsEligibleClient()) { Log("RegisterImpact ignored: not eligible client."); return; }
            if (!impactee) return;

            int id = impactee.GetInstanceID();
            if (_collectedIds.Contains(id)) return; // already counted

            _collectedIds.Add(id);
            _collectedObjs.Add(impactee);

            int count = _collectedIds.Count;
            OnCountChanged?.Invoke(count, maxCollectedThreshold);
            Log($"Collected {impactee.name} ({count}/{maxCollectedThreshold})");

            if (count >= maxCollectedThreshold)
                TriggerExplosion();
        }

        void TriggerExplosion()
        {
            Log($"OVERCHARGE TRIGGERED — affecting {_collectedObjs.Count} objects.");
            OnThresholdTriggered?.Invoke(_collectedObjs.Count);

            foreach (var c in _collectedObjs)
            {
                if (!c) continue;
                if (disableOnExplode) c.gameObject.SetActive(false);
                else Debug.Log($"[Overcharge] Explode -> {c.name}", c);
            }

            _collectedIds.Clear();
            _collectedObjs.Clear();
        }

        bool IsEligibleClient()
        {
            // Swap to your real local-player/authority check if you have one.
            return ShipStatus != null && !ShipStatus.AutoPilotEnabled && ShipStatus.Player != null;
        }

        void Log(string msg) { if (verbose) Debug.Log($"[OverchargeAction] {msg}", this); }
    }
}
