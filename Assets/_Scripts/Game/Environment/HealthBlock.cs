using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class HealthBlock : TrailBlock
    {
        public LifeForm LifeForm;
        Spindle spindle;

        public override void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            spindle = transform.parent.GetComponent<Spindle>(); // Every healthBlock required a spindle parent
            base.Explode(impactVector, team, playerName, devastate);
            LifeForm.RemoveHealthBlock(this);
            spindle.CheckForLife();
        }
    }
}
