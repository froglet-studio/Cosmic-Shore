using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public class HealthPrism : Prism
    {
        
        public LifeForm LifeForm;
        [Header("Optional Components")]
        [SerializeField] Spindle spindle;
        
        public override void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            base.Initialize(playerName);
            if (LifeForm) LifeForm.AddHealthBlock(this);
            spindle ??= transform.parent.GetComponent<Spindle>(); // Every healthPrism requires a spindle parent
            spindle.AddHealthBlock(this);
        }

        public void Reparent(Transform newParent)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            spindle.RemoveHealthBlock(this);

            transform.parent = newParent;
            
            LifeForm.RemoveHealthBlock(this);
            spindle.CheckForLife();
        }

        protected override void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            spindle.RemoveHealthBlock(this);
            
            base.Explode(impactVector, domain, playerName, devastate);
            
            LifeForm.RemoveHealthBlock(this);
            spindle.CheckForLife();
        }
    }
}