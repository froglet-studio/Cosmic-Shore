using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    public class FullAutoBlockShootActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")]
        [SerializeField] private Transform[] muzzles;
        [SerializeField] private BlockProjectileFactory blockFactory;

        [Header("Visual")]
        [SerializeField] private float spawnVisibilityDelay = 0.1f;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        private IVesselStatus _status;
        private CancellationTokenSource _cts;

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        private void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }
        #endregion

        #region ShipActionExecutorBase
        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            if (muzzles == null || muzzles.Length == 0)
                muzzles = new[] { _status.ShipTransform };
        }
        #endregion

        #region Public API
        public void Begin(FullAutoBlockShootActionSO so)
        {
            if (_cts != null) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            FireLoopAsync(so, _cts.Token).Forget();
        }

        public void End()
        {
            if (_cts == null) return;

            try
            {
                _cts.Cancel();
            }
            catch
            {
                // no-op
            }

            _cts.Dispose();
            _cts = null;
        }

        private void OnTurnEndOfMiniGame() => End();
        #endregion

        #region Core Loop
        private async UniTaskVoid FireLoopAsync(FullAutoBlockShootActionSO so, CancellationToken token)
        {
            if (!blockFactory)
            {
                Debug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
                return;
            }

            var interval  = 1f / Mathf.Max(0.1f, so.FireRate);
            var rotOffset = Quaternion.Euler(so.RotationOffsetEuler);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    foreach (var m in muzzles)
                    {
                        if (!m) continue;

                        var domainAtShot = _status.Domain;
                        var prism = blockFactory.GetBlock(
                            so.PrismType,
                            m.position,
                            m.rotation * rotOffset,
                            null);

                        if (!prism) continue;

                        prism.transform.SetParent(null, true);
                        prism.transform.localScale = so.BlockScale;
                        //prism.ownerID = _status.PlayerName; 
                        prism.ChangeTeam(domainAtShot); 
                        prism.RegisterProjectileCreated(_status.PlayerName); 

                        SetupPrismVisualAsync(prism, domainAtShot, spawnVisibilityDelay,
                            this.GetCancellationTokenOnDestroy()).Forget();

                        if (so.DisableCollidersOnLaunch)
                        {
                            var rootColliders = prism.GetComponents<Collider>();
                            foreach (var col in rootColliders)
                                col.enabled = false;
                        }
                        var childProjectile = prism.GetComponentInChildren<Projectile>();
                        if (childProjectile)
                        {
                            childProjectile.Velocity = m.forward.normalized * so.BlockSpeed;

                            if (childProjectile.TryGetComponent<Collider>(out var projCol))
                                projCol.enabled = true;

                            if (childProjectile.TryGetComponent<Rigidbody>(out var rb))
                            {
                                rb.isKinematic = false;
                            }
                        }

                        float travelDistance = UnityEngine.Random.Range(so.MinStopDistance, so.MaxStopDistance);

                        var movementToken = this.GetCancellationTokenOnDestroy();
                        MoveAndAnchorAsync(
                            prism.transform,
                            m.forward,
                            so.BlockSpeed,
                            travelDistance,
                            so.DisableCollidersOnLaunch,
                            prism,
                            childProjectile,
                            movementToken
                        ).Forget();
                    }

                    await UniTask.Delay(
                        TimeSpan.FromSeconds(interval),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[FullAutoBlockShoot] loop error: {e}");
            }
        }
        #endregion

        #region Visual Setup
        private async UniTaskVoid SetupPrismVisualAsync(
            Prism prism,
            Domains domain,
            float delaySeconds,
            CancellationToken token)
        {
            try
            {
                if (!prism) return;

                var matAnim = prism.GetComponent<MaterialPropertyAnimator>();
                if (!matAnim || !matAnim.MeshRenderer)
                {
                    prism.Domain = domain;
                    return;
                }

                var mr = matAnim.MeshRenderer;
                mr.enabled = false;
                prism.Domain = domain;
                matAnim.IsAnimating        = false;
                matAnim.AnimationProgress  = 1f;
                matAnim.OnAnimationComplete = null;
                matAnim.MarkMaterialsDirty();

                matAnim.SetTransparency(false);

                if (delaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(delaySeconds),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }
                else
                {
    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!prism || !prism.gameObject.activeInHierarchy)
                    return;

                mr.enabled = true;
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                Debug.LogError($"[FullAutoBlockShoot] SetupPrismVisual error: {e}");
            }
        }
        #endregion

        #region Movement / Anchor
        private async UniTaskVoid MoveAndAnchorAsync(Transform block, Vector3 dir, float speed, float distance, bool reactivateCollidersAtEnd, Prism prism, Projectile childProjectile, CancellationToken token)
        {
            Vector3 start  = block.position;
            Vector3 target = start + dir.normalized * distance;

            try
            {
                while ((block.position - target).sqrMagnitude > 0.01f)
                {
                    token.ThrowIfCancellationRequested();

                    if (!block || !block.gameObject.activeInHierarchy)
                        return;

                    block.position = Vector3.MoveTowards(
                        block.position,
                        target,
                        speed * Time.deltaTime);

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                if (reactivateCollidersAtEnd && prism && prism.gameObject.activeInHierarchy)
                {
                    var rootColliders = prism.GetComponents<Collider>();
                    foreach (var col in rootColliders)
                        col.enabled = true;
                }

                if (childProjectile && childProjectile.gameObject.activeInHierarchy)
                {
                    if (childProjectile.TryGetComponent<Collider>(out var projCol))
                        projCol.gameObject.SetActive(false);

                    if (childProjectile.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.isKinematic     = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[FullAutoBlockShoot] MoveAndAnchor error: {e}");
            }
        }
        #endregion
    }
}