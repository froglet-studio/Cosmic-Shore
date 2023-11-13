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

            spawnable.Spawn().transform.SetPositionAndRotation(Ship.transform.position,Ship.transform.rotation);

            yield return new WaitForEndOfFrame();
        }
    }
}