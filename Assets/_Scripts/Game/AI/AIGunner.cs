using CosmicShore.Game.ImpactEffects;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Models.Enums;


namespace CosmicShore.Game.AI
{
    public class AIGunner : MonoBehaviour
    {
        [SerializeField] Gun gun;
        [SerializeField] GameObject gunMount;

        [FormerlySerializedAs("Team")] public Domains domain;

        /*[RequireInterface(typeof(IVessel))]
        public MonoBehaviour Vessel;*/
        
        /*void Start()
        {
            gun.Team = Team;
            gun.Vessel = Vessel as IVessel;
        }*/
    }
}