using CosmicShore.Core;
using CosmicShore.Game.Ship;
using UnityEngine;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Game.Ship.ShipActions;
namespace CosmicShore.Game.Ship.R_ShipActions.DataContainers
{
    public abstract class ShipActionSO : ScriptableObject
    {
        protected IVesselStatus vesselStatus { get; private set; }
    
        public virtual void Initialize(IVesselStatus vs) => vesselStatus = vs;
    
        /*{ TODO : Not sure what it does, was inside virtual Initialize method.
            // ElementalFloatBinder.BindAndClone(this, ship, GetType().Name);
        }*/

        /// <summary>
        /// Stateless: vessel context is passed in each call.
        /// </summary>
        public abstract void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
        public abstract void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus);
    }
}
