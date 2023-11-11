using CosmicShore._Core.Ship.Projectiles;
using UnityEngine;

namespace CosmicShore.Core.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        public Teams Team;
        public Ship Ship;
        
        void Start()
        {
            gun.Team = Team;
            gun.Ship = Ship;
        }
    }
}