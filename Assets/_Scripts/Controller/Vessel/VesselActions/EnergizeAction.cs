using CosmicShore.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lifts a set of guns' output while held. It states a FLOOR on each gun rather than writing
    /// the gun's authored fields, which is what lets <see cref="FireGunAction.ProjectileTime"/>
    /// stay the element's parameter -- see the note on FireGunAction's floors for the three
    /// defects the write-and-restore shape carried.
    /// </summary>
    public class EnergizeAction : ShipAction
    {
        [SerializeField] List<FireGunAction> fireActions;

        [SerializeField] float Speed = 70;
        [SerializeField] float ProjectileTime = 6;
        [SerializeField] int Energy = 1;

        public override void StartAction()
        {
            foreach (FireGunAction fireaction in fireActions)
                fireaction.RaiseOutputFloors(Speed, ProjectileTime, Energy);
        }

        public override void StopAction()
        {
            foreach (FireGunAction fireaction in fireActions)
                fireaction.ClearOutputFloors();
        }
    }
}
