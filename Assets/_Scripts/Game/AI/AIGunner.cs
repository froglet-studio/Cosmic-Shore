using CosmicShore.Game.Projectiles;
using UnityEngine;


namespace CosmicShore.Game.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        public Teams Team;

        /*[RequireInterface(typeof(IVessel))]
        public MonoBehaviour Vessel;*/
        
        /*void Start()
        {
            gun.Team = Team;
            gun.Vessel = Vessel as IVessel;
        }*/
    }
}