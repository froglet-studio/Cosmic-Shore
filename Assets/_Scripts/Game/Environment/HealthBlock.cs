using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public class HealthBlock : TrailBlock
    {
        
        public LifeForm LifeForm;
        [Header("Optional Components")]
        [SerializeField] Spindle spindle;
        
        protected override void Start()
        {
            base.Start();
            if (LifeForm) LifeForm.AddHealthBlock(this);
            spindle ??= transform.parent.GetComponent<Spindle>(); // Every healthBlock requires a spindle parent
        }

        public void Reparent(Transform newParent)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            transform.parent = newParent;
            LifeForm.RemoveHealthBlock(this);
            spindle.CheckForLife();
        }

        protected override void Explode(Vector3 impactVector, Teams team, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            base.Explode(impactVector, team, playerName, devastate); 
            LifeForm.RemoveHealthBlock(this);
            spindle.CheckForLife();           
        }
    }
}
