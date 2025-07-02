using UnityEngine;

namespace CosmicShore
{
    public class ToggleProjectileActionWrapper :ShipAction
    {
        [SerializeField] FullAutoAction wrappedAction;
        [SerializeField] float projectileTime1 = 0.3f;
        [SerializeField] float projectileTime2 = 0.1f;

        public override void StartAction()
        {
            wrappedAction.Energy += 1;
            wrappedAction.Energy %= 2;
            wrappedAction.projectileTime = wrappedAction.Energy == 0 ? projectileTime1 : projectileTime2;
        }

        public override void StopAction()
        {

        }
    }
}
