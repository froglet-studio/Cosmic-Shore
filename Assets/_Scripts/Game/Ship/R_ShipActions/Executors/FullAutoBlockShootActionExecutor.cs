using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
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

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        private IVesselStatus _status;
        private CancellationTokenSource _cts;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            if (muzzles == null || muzzles.Length == 0)
                muzzles = new[] { _status.ShipTransform };
        }

        public void Begin(FullAutoBlockShootActionSO so)
        {
            if (_cts != null) return;
            // Fire loop cancels on End() or Destroy
            _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            FireLoopAsync(so, _cts.Token).Forget();
        }

        public void End()
        {
            if (_cts == null) return;
            try { _cts.Cancel(); } catch { /* no-op */ }
            _cts.Dispose();
            _cts = null;
        }

        void OnTurnEndOfMiniGame() => End();

        private async UniTaskVoid FireLoopAsync(FullAutoBlockShootActionSO so, CancellationToken token)
        {
            if (!blockFactory)
            {
                Debug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
                return;
            }

            float interval = 1f / Mathf.Max(0.1f, so.FireRate);
            var rotOffset = Quaternion.Euler(so.RotationOffsetEuler);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var m in muzzles)
                    {
                        if (!m) continue;

                        var domainAtShot = _status.Domain;

                        var prism = blockFactory.GetBlock(so.PrismType, m.position, m.rotation * rotOffset, null);
                        if (!prism) continue;

                        prism.transform.SetParent(null, true);
                        prism.transform.localScale = so.BlockScale;

                        prism.Domain = domainAtShot;

                        if (so.DisableCollidersOnLaunch)
                        {
                            if (prism.TryGetComponent<Collider>(out var c)) c.enabled = false;
                            foreach (var col in prism.GetComponentsInChildren<Collider>()) col.enabled = false;
                        }

                        var movementToken = this.GetCancellationTokenOnDestroy();

                        MoveAndAnchorAsync(
                            prism.transform,
                            m.forward,
                            so.BlockSpeed,
                            UnityEngine.Random.Range(so.MinStopDistance, so.MaxStopDistance),
                            so.DisableCollidersOnLaunch,
                            prism,
                            movementToken
                        ).Forget();
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(interval), DelayType.DeltaTime, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[FullAutoBlockShoot] loop error: {e}"); }
        }

        private async UniTaskVoid MoveAndAnchorAsync(
            Transform block,
            Vector3 dir,
            float speed,
            float distance,
            bool enableCollidersAtEnd,
            Prism prism,
            CancellationToken token)
        {
            Vector3 start  = block.position;
            Vector3 target = start + dir.normalized * distance;

            try
            {
                while ((block.position - target).sqrMagnitude > 0.01f)
                {
                    token.ThrowIfCancellationRequested();
                    block.position = Vector3.MoveTowards(block.position, target, speed * Time.deltaTime);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // Domain already assigned at launch; keep it as-shot.
                if (!enableCollidersAtEnd) return;

                if (block.TryGetComponent<Collider>(out var c)) c.enabled = true;
                foreach (var col in block.GetComponentsInChildren<Collider>()) col.enabled = true;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Debug.LogError($"[FullAutoBlockShoot] MoveAndAnchor error: {e}"); }
        }
    }
}
