using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockSpawner : AOEBlockCreation
    {
        [SerializeField] SpawnableAbstractBase spawnable;

        protected override IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            spawnable.Spawn().transform.SetPositionAndRotation(Ship.Transform.position,Ship.Transform.rotation);

            yield return new WaitForEndOfFrame();
        }
    }
}