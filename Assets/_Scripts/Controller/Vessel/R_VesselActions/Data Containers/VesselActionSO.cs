using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public abstract class ShipActionSO : ScriptableObject
    {
        protected IVesselStatus vesselStatus { get; private set; }
    
        public virtual void Initialize(IVesselStatus vs) => vesselStatus = vs;

        /// <summary>
        /// Stateless: vessel context is passed in each call.
        /// </summary>
        public abstract void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
        public abstract void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);

        /// <summary>
        /// An AUTOPILOT's press of this action has to reach every peer. A human press always does: it
        /// round-trips through <see cref="R_VesselActionHandler"/> and re-executes everywhere. An AI pilot runs
        /// on the server only and starts its actions directly (<see cref="VesselActionTape.Start"/>), which is
        /// local, and right for an action whose whole effect is MOTION (the transform replicates). It is wrong
        /// for one whose effect is PRISMS, which are simulated on every peer and never sent: the Serpent's
        /// wall stood on the host and nowhere else. True sends the press through
        /// <see cref="R_VesselActionHandler.TrySendAutopilotAction"/> so it runs on every peer, as a human's does.
        /// </summary>
        public virtual bool AutopilotPressTravels => false;
    }
}
