using System;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared runtime references handed to every <see cref="Toy"/> at creation. Toys are
    /// built at runtime via <c>new GameObject</c>, so Reflex <c>[Inject]</c> does not run on
    /// them - the <see cref="ToyboxController"/> owns the injected dependencies and passes
    /// them down through this context instead.
    /// </summary>
    public sealed class ToyContext
    {
        /// <summary>Shared game state (local player, domains, etc.).</summary>
        public GameDataSO GameData;

        /// <summary>Menu vessel spawner used to request networked vessel swaps.</summary>
        public MenuServerPlayerVesselInitializer VesselInitializer;

        /// <summary>Vessel-prefab registry, for building mini display models of a target vessel.</summary>
        public VesselPrefabContainer VesselPrefabContainer;

        /// <summary>
        /// True only while the local player is flying freestyle (not autopilot/menu). Toys are
        /// visible in the lava lamp but stay inert until the player takes control, so a drifting
        /// autopilot vessel never trips them.
        /// </summary>
        public Func<bool> IsFreestyleActive;
    }

    /// <summary>Where and how big a toy is placed in the world.</summary>
    public readonly struct ToyPlacement
    {
        public readonly Vector3 Position;
        public readonly Vector3 LookTarget;
        public readonly float BodyRadius;
        public readonly float TriggerRadius;

        public ToyPlacement(Vector3 position, Vector3 lookTarget, float bodyRadius, float triggerRadius)
        {
            Position = position;
            LookTarget = lookTarget;
            BodyRadius = bodyRadius;
            TriggerRadius = triggerRadius;
        }
    }
}
