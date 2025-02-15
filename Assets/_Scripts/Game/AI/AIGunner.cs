using CosmicShore.Game.Projectiles;
using UnityEngine;


namespace CosmicShore.Game.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        public Teams Team;

        /*[RequireInterface(typeof(IShip))]
        public MonoBehaviour Ship;*/
        
        /*void Start()
        {
            gun.Team = Team;
            gun.Ship = Ship as IShip;
        }*/
    }
}