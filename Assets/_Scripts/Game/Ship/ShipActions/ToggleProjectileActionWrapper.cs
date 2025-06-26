using UnityEngine;

namespace CosmicShore
{
    public class ToggleProjectileActionWrapper :ShipAction
    {
        [SerializeField] FullAutoAction wrappedAction;

        public override void StartAction()
        {
            wrappedAction.Energy += 1;
            wrappedAction.Energy %= 2;
        }

        public override void StopAction()
        {

        }
    }
}
