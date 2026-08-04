using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    public class FullAutoBlockShootActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        /// <summary>Static event: each time a block prism is shot. Param = player name.</summary>
        public static event Action<string> OnBlockShot;

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
                CSDebug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
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

                        // Stationary blocks bypass Projectile.LaunchProjectile (movement is
                        // driven by MoveAndAnchorAsync), so play the launch SFX here — the
                        // flying-mode guns get theirs from LaunchProjectile.
                        audioSystem.PlayGameplaySFX(GameplaySFXCategory.ProjectileLaunch, m.position);

                        prism.transform.SetParent(null, true);

                        // MASS → turret prism stretch: the long z-axis scales with the vessel's
                        // live Mass level (ElementalAbilityMapSO). Volume = x·y·z of lossyScale,
                        // so the stretch feeds Cell.LiveVolume — "volume is the spine".
                        var blockScale = so.BlockScale;
                        blockScale.z *= _status?.ElementalAbilityHandler.Multiplier(Element.Mass) ?? 1f;

                        // Route sizing through the scale animator instead of a raw
                        // localScale write: TargetScale stays truthful (a later
                        // Grow/ChangeSize no longer snaps the prism back to its authored
                        // size), live volume tracks, and the block blooms in from zero
                        // during flight instead of popping in (continuity law).
                        var scaleAnimator = prism.GetComponent<PrismScaleAnimator>();
                        if (scaleAnimator)
                        {
                            prism.transform.localScale = Vector3.zero;
                            scaleAnimator.SetTargetScale(blockScale);
                            scaleAnimator.BeginGrowthAnimation();
                        }
                        else
                        {
                            prism.transform.localScale = blockScale;
                        }

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
                            childProjectile.Velocity = m.forward * so.BlockSpeed; // m.forward is already unit

                            if (childProjectile.TryGetComponent<Collider>(out var projCol))
                                projCol.enabled = true;

                            if (childProjectile.TryGetComponent<Rigidbody>(out var rb))
                            {
                                rb.isKinematic = false;
                            }
                        }

                        float travelDistance = UnityEngine.Random.Range(so.MinStopDistance, so.MaxStopDistance);

                        OnBlockShot?.Invoke(_status?.PlayerName);

                        // MASS level-5 'Shielded Prisms': snapshot at fire time, applied at
                        // anchor. Regular shield only — one-hit ablative armor that fauna can
                        // still eat via devastate, preserving the food-web sink (SuperShield
                        // would create mass with no active sink — an ecosystem freeze vector).
                        bool shieldOnAnchor =
                            _status?.ElementalAbilityHandler.IsUpgradeActive(Element.Mass) == true;

                        var movementToken = this.GetCancellationTokenOnDestroy();
                        MoveAndAnchorAsync(
                            prism.transform,
                            m.forward,
                            so.BlockSpeed,
                            travelDistance,
                            so.DisableCollidersOnLaunch,
                            prism,
                            childProjectile,
                            shieldOnAnchor,
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
                CSDebug.LogError($"[FullAutoBlockShoot] loop error: {e}");
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
                CSDebug.LogError($"[FullAutoBlockShoot] SetupPrismVisual error: {e}");
            }
        }
        #endregion

        #region Movement / Anchor
        private async UniTaskVoid MoveAndAnchorAsync(Transform block, Vector3 dir, float speed, float distance, bool reactivateCollidersAtEnd, Prism prism, Projectile childProjectile, bool shieldOnAnchor, CancellationToken token)
        {
            Vector3 start  = block.position;
            Vector3 target = start + dir * distance; // dir is m.forward (already unit)

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

                // Anchored: the block is now permanent world mass. Register it with the
                // spatial index (the one registration lifecycle) so it participates in
                // Burst AOE damage, growth occupancy, fauna density queries, and the
                // containing cell's LiveVolume — unregistered turret prisms were
                // invisible to the entire ecosystem. Registered at rest, not at the
                // muzzle, so the bucket grid files it at its true position.
                if (prism && prism.gameObject.activeInHierarchy && prism.SpatialIndexId < 0)
                {
                    prism.prismProperties.position = prism.transform.position;
                    var spatialIndex = PrismSpatialIndex.EnsureInstance();
                    if (spatialIndex != null && spatialIndex.IsAvailable)
                        prism.SpatialIndexId = spatialIndex.Register(prism);
                }

                // MASS level-5: engage the shield AFTER the collider re-enable and the index
                // registration — the shield's Box→Mesh collider swap must run last so the
                // reactivation loop can't re-enable the disabled BoxCollider, and the state
                // manager's index flag sync needs SpatialIndexId ≥ 0. Collider budget: the
                // swap is 1:1 (count-neutral); note shield MeshColliders are LOD-exempt today.
                if (shieldOnAnchor && prism && prism.gameObject.activeInHierarchy)
                    prism.ActivateShield();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[FullAutoBlockShoot] MoveAndAnchor error: {e}");
            }
        }
        #endregion
    }
}