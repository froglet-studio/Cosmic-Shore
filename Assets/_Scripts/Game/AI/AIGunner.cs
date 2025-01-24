using CosmicShore.Game.Projectiles;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        public Teams Team;
        public IShip Ship;
        
        void Start()
        {
            gun.Team = Team;
            gun.Ship = Ship;
        }
    }
}