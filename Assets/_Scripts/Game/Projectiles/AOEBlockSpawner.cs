using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] SpawnableAbstractBase spawnable;

        protected override IEnumerator ExplodeCoroutine()
        {
            var position = Ship.Transform.position;
            var rotation = Ship.Transform.rotation;
            var team = Ship.ShipStatus.Team;

            yield return new WaitForSeconds(ExplosionDelay);

            spawnable.Spawn(position, rotation, team, (int)(Ship.ShipStatus.ResourceSystem.Resources[0].CurrentAmount*10));

            yield return new WaitForEndOfFrame();
        }
    }
}