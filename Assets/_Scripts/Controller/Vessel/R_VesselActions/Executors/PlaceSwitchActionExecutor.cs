using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Executes <see cref="PlaceSwitchActionSO"/>: gates on switch charges, spends one, and
    /// hands off to a <see cref="ScarabSwitch"/>, which owns the ring visual, the pass-through
    /// detection and the scarab-wing dais it pays out. Per-vessel state lives here (the SO is
    /// shared and stateless). Rides the normal R_VesselActionHandler ServerRpc→ClientRpc
    /// re-execution, so every peer builds the same switch from its replicated transform.
    /// </summary>
    public class PlaceSwitchActionExecutor : ShipActionExecutorBase
    {
        [Tooltip("The standard pooled prism spawn channel " +
                 "(Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset).")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnEvent;

        // The float-ulp epsilon from SCARAB.md §3.3: a full 1.0 meter minus two exact 1/3
        // spends lands one ulp BELOW 1/3f, so the gate must sit a hair under the cost.
        const float CostEpsilon = 0.001f;

        IVesselStatus _status;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        public void PlaceSwitch(PlaceSwitchActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            _status = status;

            var resources = status.ResourceSystem;
            if (!resources) return;

            if (!prismSpawnEvent)
            {
                CSDebug.LogError("[PlaceSwitch] Prism spawn event channel is not assigned on the executor.");
                return;
            }

            float cost = so.ComputeCost(resources);
            if (cost <= 0f) return;
            var meter = resources.Resources[so.ResourceIndex];
            if (meter.CurrentAmount < cost - CostEpsilon)
            {
                // Refusal: no charge, nothing spawns. (HUD pips already show the count;
                // a refusal SFX is a follow-up alongside the Scarab HUD pass.)
                CSDebug.LogVerbose(CSLogChannel.ScarabSwitch, "[PlaceSwitch] Refused — no switch charge banked.");
                return;
            }

            var ship = status.ShipTransform ? status.ShipTransform : transform;
            Vector3 course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : ship.forward;

            float distance = so.placementDistance.EvaluateLive(status); // SPACE, live at use time
            float radius = so.RingRadius * so.switchScale.EvaluateLive(status); // MASS, live
            Vector3 center = ship.position + course * distance;

            // The switch owns its own ring visual and pass-through detection.
            var go = new GameObject($"ScarabSwitch::{status.PlayerName}");
            var sw = go.AddComponent<ScarabSwitch>();
            sw.Build(prismSpawnEvent, status, center, course, radius, so.GrowthRate,
                     so.Dais, so.DaisPrismsPerFrame);

            resources.ChangeResourceAmount(so.ResourceIndex, -cost);
            CSDebug.LogVerbose(CSLogChannel.ScarabSwitch,
                $"[PlaceSwitch] Switch ring r={radius:F0} placed {distance:F0}u ahead.");
        }

    }
}
