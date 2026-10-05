using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// What a single behavior policy contributes for one frame. Multiple policies
    /// can run in parallel and their outputs are blended by the pilot before being
    /// written to InputStatus.
    ///
    /// All steering values are in the vessel's local frame. Throttle is 0..1.
    /// Booleans are sticky-per-frame requests: any policy voting true triggers the action.
    /// </summary>
    public struct DecisionOutput
    {
        public Vector2 SteerLocal;      // (yaw, pitch) in -1..1
        public float SteerWeight;       // 0..1 — how much this contribution should count
        public float Throttle;          // 0..1, additive
        public float ThrottleWeight;
        public float Roll;              // -1..1
        public float RollWeight;
        public bool RequestDrift;
        public bool RequestRam;
        public bool RequestFire;
        public List<InputEvents> RequestActionsStart;
        public List<InputEvents> RequestActionsStop;

        public static DecisionOutput Zero => new()
        {
            SteerLocal = Vector2.zero,
            SteerWeight = 0f,
            Throttle = 0f,
            ThrottleWeight = 0f,
            Roll = 0f,
            RollWeight = 0f
        };

        public DecisionOutput RequestStart(InputEvents ev)
        {
            RequestActionsStart ??= new List<InputEvents>(2);
            RequestActionsStart.Add(ev);
            return this;
        }

        public DecisionOutput RequestStop(InputEvents ev)
        {
            RequestActionsStop ??= new List<InputEvents>(2);
            RequestActionsStop.Add(ev);
            return this;
        }
    }
}
