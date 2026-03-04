using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselElementDrainBySkimmerEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Skimmer/VesselElementDrainBySkimmerEffectSO")]
    public sealed class VesselElementDrainBySkimmerEffectSO : VesselSkimmerEffectsSO
    {
        [Header("Element Drain")]
        [SerializeField, Tooltip("Number of element ticks to drain from each element.")]
        private int drainTicks = 5;

        [SerializeField, Tooltip("Seconds before drained elements are restored.")]
        private float restoreDelaySeconds = 4f;

        [Header("Anti-Spam")]
        [SerializeField, Tooltip("Minimum seconds between drains on the same vessel.")]
        private float cooldownSeconds = 0.5f;

        private static readonly Dictionary<VesselImpactor, float> _lastDrainTimeByImpactor = new();
        private static readonly Dictionary<ResourceSystem, Coroutine> _restoreByTarget = new();

        public override void Execute(VesselImpactor impactor, SkimmerImpactor impactee)
        {
            if (impactor == null || impactor.Vessel == null)
                return;
            if (impactee == null || impactee.Skimmer.VesselStatus.Vessel == null)
                return;

            var impactorVessel = impactor.Vessel;
            var skimmerVessel = impactee.Skimmer.VesselStatus.Vessel;

            // Only trigger when the skimmer's vessel is faster (nerf the slower player)
            if (skimmerVessel.VesselStatus.Speed <= impactorVessel.VesselStatus.Speed)
                return;

            // Anti-spam cooldown per impactor
            var now = Time.time;
            if (_lastDrainTimeByImpactor.TryGetValue(impactor, out var lastTime)
                && now - lastTime < cooldownSeconds)
                return;

            _lastDrainTimeByImpactor[impactor] = now;

            // Drain elements on the slower (impactor) vessel
            var resourceSystem = impactorVessel.VesselStatus.ResourceSystem;
            float drainAmount = drainTicks * -0.1f;

            // Snapshot current levels so we can restore exactly what was drained
            float chargeBefore = resourceSystem.GetNormalizedLevel(Element.Charge);
            float massBefore = resourceSystem.GetNormalizedLevel(Element.Mass);
            float spaceBefore = resourceSystem.GetNormalizedLevel(Element.Space);
            float timeBefore = resourceSystem.GetNormalizedLevel(Element.Time);

            resourceSystem.AdjustLevel(Element.Charge, drainAmount);
            resourceSystem.AdjustLevel(Element.Mass, drainAmount);
            resourceSystem.AdjustLevel(Element.Space, drainAmount);
            resourceSystem.AdjustLevel(Element.Time, drainAmount);

            float chargeActualDrain = chargeBefore - resourceSystem.GetNormalizedLevel(Element.Charge);
            float massActualDrain = massBefore - resourceSystem.GetNormalizedLevel(Element.Mass);
            float spaceActualDrain = spaceBefore - resourceSystem.GetNormalizedLevel(Element.Space);
            float timeActualDrain = timeBefore - resourceSystem.GetNormalizedLevel(Element.Time);

            // Cancel any pending restore on this target before scheduling a new one
            if (_restoreByTarget.TryGetValue(resourceSystem, out var running) && running != null)
                impactee.Skimmer.StopCoroutine(running);

            _restoreByTarget[resourceSystem] = impactee.Skimmer.StartCoroutine(
                RestoreAfterDelay(resourceSystem, chargeActualDrain, massActualDrain, spaceActualDrain, timeActualDrain));
        }

        private IEnumerator RestoreAfterDelay(
            ResourceSystem resourceSystem,
            float chargeDrain, float massDrain, float spaceDrain, float timeDrain)
        {
            yield return new WaitForSeconds(restoreDelaySeconds);

            if (resourceSystem == null)
                yield break;

            resourceSystem.AdjustLevel(Element.Charge, chargeDrain);
            resourceSystem.AdjustLevel(Element.Mass, massDrain);
            resourceSystem.AdjustLevel(Element.Space, spaceDrain);
            resourceSystem.AdjustLevel(Element.Time, timeDrain);

            _restoreByTarget.Remove(resourceSystem);
        }
    }
}
