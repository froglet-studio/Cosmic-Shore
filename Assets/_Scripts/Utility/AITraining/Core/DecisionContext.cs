using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Per-frame snapshot of everything a behavior policy is allowed to read.
    /// Built once per Update by TrainingPilot, then handed to every active policy.
    /// Keeping this immutable-after-build (we just clear-and-refill per frame) prevents
    /// policies from interfering with each other through shared state.
    ///
    /// Note: only WORLD INFORMATION is exposed here. Policies do not get to mutate
    /// the vessel — they emit a DecisionOutput, which the pilot writes to InputStatus.
    /// </summary>
    public class DecisionContext
    {
        // Identity
        public IVessel Vessel;
        public IVesselStatus VesselStatus;
        public Domains MyDomain;
        public string PlayerName;

        // Self pose & motion
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Up;
        public Vector3 Right;
        public Vector3 Velocity;
        public float Speed;
        public bool IsBoosting;
        public bool IsDrifting;
        public bool IsStationary;
        public bool IsAttached;
        public bool GunsActive;
        public bool HasLiveProjectiles;

        // Resources
        public float ChargedBoostCharge;
        public bool IsChargedBoostDischarging;

        // Targeting
        public bool HasTarget;
        public Vector3 TargetPosition;
        public Vector3 TargetVelocity;
        public TargetKind TargetKind;
        public float TargetRange;

        // Threats (other vessels, prisms, mines)
        public readonly List<ThreatInfo> Threats = new(16);
        public readonly List<PrismInfo> NearbyPrisms = new(32);

        // Navigation
        public Vector3 ObjectiveDirection;   // 0 if HasTarget == false
        public float DotForwardObjective;    // -1..1
        public float TimeSinceLastDamage;
        public float TimeSinceLastObjectiveProgress;

        // Episode bookkeeping
        public float EpisodeTime;
        public int EpisodeFrame;
        public TrainingGenome Genome;        // Read-only handle for the current rollout

        public void Clear()
        {
            Threats.Clear();
            NearbyPrisms.Clear();
            HasTarget = false;
        }
    }

    public enum TargetKind
    {
        None = 0,
        Crystal = 1,        // Crystal/objective collection
        EnemyVessel = 2,    // Joust / shooter
        EnemyTerritory = 3, // Cellular / capture
        Waypoint = 4,       // Race
        Friendly = 5        // Co-op partner
    }

    public struct ThreatInfo
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Range;
        public float Severity;   // 0..1, fitness-component-defined
        public Domains Domain;
    }

    public struct PrismInfo
    {
        public Vector3 Position;
        public Vector3 Forward;
        public float Range;
        public Domains Domain;
        public bool IsHostile;
    }
}
