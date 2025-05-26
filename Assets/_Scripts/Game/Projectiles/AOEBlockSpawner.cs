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

            yield return new WaitForSeconds(ExplosionDelay);

            spawnable.Spawn(position, rotation);

            yield return new WaitForEndOfFrame();
        }
    }
}