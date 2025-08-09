using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "DrainHalfAmmoImpactEffect", menuName = "ScriptableObjects/Impact Effects/DrainHalfAmmoImpactEffectSO")]
    public class DrainHalfAmmoEffectSO : ImpactEffectSO
    {
        const int DIVIDE_BY = 2;

        [SerializeField]
        int _ammoResourceIndex;

        /*public void Execute(ImpactEffectData context)
        {
            context.ThisShipStatus.ResourceSystem.ChangeResourceAmount(_ammoResourceIndex,
                                - context.ThisShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount / DIVIDE_BY);
        }*/
        public override void Execute(R_IImpactor impactor, R_IImpactor impactee)
        {
            throw new System.NotImplementedException();
        }
    }
}
