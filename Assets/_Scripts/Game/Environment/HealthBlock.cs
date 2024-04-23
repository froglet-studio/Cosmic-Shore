using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class HealthBlock : TrailBlock
    {
        public LifeForm LifeForm;


        public override void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            base.Explode(impactVector, team, playerName, devastate);
            LifeForm.RemoveHealthBlock(this);
        }
    }
}
