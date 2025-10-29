using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    public sealed class FullAutoBlockShootActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] private Transform[] muzzles;
        [SerializeField] private BlockProjectileFactory blockFactory;

        private IVesselStatus _status;
        private Coroutine _loop;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            if (muzzles == null || muzzles.Length == 0)
                muzzles = new[] { _status.ShipTransform };
        }

        public void Begin(FullAutoBlockShootActionSO so)
        {
            if (_loop != null) return;
            _loop = StartCoroutine(FireLoop(so));
        }

        public void End()
        {
            if (_loop == null) return;
            StopCoroutine(_loop);
            _loop = null;
        }

        private IEnumerator FireLoop(FullAutoBlockShootActionSO so)
        {
            if (!blockFactory)
            {
                Debug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
                yield break;
            }

            float interval = 1f / Mathf.Max(0.1f, so.FireRate);
            var rotOffset = Quaternion.Euler(so.RotationOffsetEuler);

            while (true)
            {
                // fire ALL muzzles in the same frame
                for (int i = 0; i < muzzles.Length; i++)
                {
                    var m = muzzles[i];
                    var prism = blockFactory.GetBlock(so.PrismType, m.position, m.rotation * rotOffset, null);
                    if (!prism) continue;

                    prism.transform.SetParent(null, true);
                    prism.transform.localScale = so.BlockScale;

                    if (so.DisableCollidersOnLaunch)
                    {
                        if (prism.TryGetComponent<Collider>(out var c)) c.enabled = false;
                        foreach (var col in prism.GetComponentsInChildren<Collider>()) col.enabled = false;
                    }

                    // move forward then anchor
                    StartCoroutine(MoveAndAnchor(prism.transform, m.forward, so.BlockSpeed, Random.Range(so.MinStopDistance, so.MaxStopDistance), so.DisableCollidersOnLaunch, prism));
                }

                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator MoveAndAnchor(Transform block, Vector3 dir, float speed, float distance, bool enableCollidersAtEnd, Prism prism )
        {
            Vector3 start  = block.position;
            Vector3 target = start + dir.normalized * distance;

            while (Vector3.SqrMagnitude(block.position - target) > 0.01f)
            {
                block.position = Vector3.MoveTowards(block.position, target, speed * Time.deltaTime);
                yield return null;
            }

            prism.Domain = _status.Domain;
            
            if (enableCollidersAtEnd == false)
                yield break;

            if (block.TryGetComponent<Collider>(out var c)) c.enabled = true;
            foreach (var col in block.GetComponentsInChildren<Collider>()) col.enabled = true;
        }
    }
}
