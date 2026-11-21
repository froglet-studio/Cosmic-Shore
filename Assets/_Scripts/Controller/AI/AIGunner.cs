using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;


namespace CosmicShore.Gameplay
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        [SerializeField, FormerlySerializedAs("Team")] Domains domain;
        public Domains Domain => domain;

        /*[RequireInterface(typeof(IVessel))]
        public MonoBehaviour Vessel;*/
        
        /*void Start()
        {
            gun.Team = Team;
            gun.Vessel = Vessel as IVessel;
        }*/
    }
}