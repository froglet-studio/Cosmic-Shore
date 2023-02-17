using UnityEngine;

namespace StarWriter.Core
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public Teams Team;
        public Ship Ship;


        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (trailBlock.Team == Team)
                    return;

                trailBlock.Explode(Velocity, Team, Ship.Player.PlayerName); // TODO: need to attribute the explosion color to the team that made the explosion
            }
        }
    }
}