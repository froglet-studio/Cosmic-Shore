using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] SpawnableAbstractBase spawnable;

        protected override IEnumerator ExplodeCoroutine()
        {
            var position = Vessel.Transform.position;
            var rotation = Vessel.Transform.rotation;
            var team = Vessel.VesselStatus.Domain;

            yield return new WaitForSeconds(ExplosionDelay);

            spawnable.Spawn(position, rotation, team, (int)(Vessel.VesselStatus.ResourceSystem.Resources[0].CurrentAmount*10));

            yield return new WaitForEndOfFrame();
        }
    }
}