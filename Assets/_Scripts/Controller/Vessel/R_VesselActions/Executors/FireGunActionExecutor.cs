using System;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    public class FireGunActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;
        /// <summary>Static event: each time a gun fires a single shot. Param = player name.</summary>
        public static event Action<string> OnShotFired;

        public event Action OnGunFired;
        public event Action<float> OnAmmoChanged;

        /// <summary>
        /// Raised at PRESS time, before the (optional) launch delay, so the missile-bay
        /// animation opens in step with the input. Param = true when the RIGHT bay fires
        /// this shot ("Missile Launch 1"), false for the LEFT bay ("Missile Launch 2").
        /// The projectile spawn uses the SAME side, so the animated missile and the live
        /// projectile always agree on which bay opened.
        /// </summary>
        public event Action<bool> OnMissileFired;

        [Header("Scene Refs")]
        [SerializeField] Gun gun;

        [SerializeField] Transform projectileContainer;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Ammo")]
        [SerializeField] private int defaultAmmoIndex = 2;

        [Tooltip("The weapon asset this executor fires. Optional, and it changes nothing about " +
                 "firing — a shot always uses the SO it was handed. It exists because the HUD " +
                 "has to describe the tank BEFORE the first shot (and again after a turn-end " +
                 "clear), when there is no live SO to read: without it the shot COST would have " +
                 "to be authored a second time on the HUD, where it could drift from the number " +
                 "the gun actually spends. Same reasoning as VesselRearmOnPrismDestruction " +
                 "reading its ammo index off the weapon asset rather than re-authoring it.")]
        [SerializeField] private FireGunActionSO defaultAction;

        // The Sparrow's rig exposes one bone per missile bay; the projectile spawns at the
        // live bone pose so it emerges exactly where the bay animation ejects the missile.
        // Resolved BY NAME (the art-swap-resilient pattern VesselAnimation.ResolvePart uses)
        // and lazily, because executor Initialize can run before the model hierarchy is live.
        const string RightBayBoneName = "b_Missile.R";
        const string LeftBayBoneName = "b_Missile.L";

        IVesselStatus _status;
        ResourceSystem _resources;
        FireGunActionSO _soRef;
        readonly bool _detachFromContainer = true;

        Transform _worldMuzzleAnchor;
        Transform _rightBayBone;
        Transform _leftBayBone;
        bool _bayBonesResolved;
        bool _warnedMissingBayBones;

        CancellationTokenSource _pendingLaunchCts;

        void Awake()
        {
            var go = new GameObject("[MuzzleWorldAnchor]")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _worldMuzzleAnchor = go.transform;
            _worldMuzzleAnchor.SetParent(null, true);
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;

            CancelPendingLaunches();
        }

        void OnDestroy()
        {
            if (_worldMuzzleAnchor)
                Destroy(_worldMuzzleAnchor.gameObject);

            if (_resources != null)
                _resources.OnResourceChanged -= HandleResourceChanged;

            CancelPendingLaunches();
        }

        /// <summary>The weapon this executor is currently describing: the SO of the shot in
        /// flight when there is one, else the authored asset, else nothing.</summary>
        FireGunActionSO ActiveAction => _soRef ? _soRef : defaultAction;

        int ActiveAmmoIndex
        {
            get
            {
                var action = ActiveAction;
                return action ? action.AmmoIndex : defaultAmmoIndex;
            }
        }

        public float Ammo01
        {
            get
            {
                var index = ActiveAmmoIndex;

                if (index < 0 || index >= _resources.Resources.Count)
                    return 0f;

                var res = _resources.Resources[index];
                if (res == null || res.MaxAmount <= 0f)
                    return 0f;

                return Mathf.Clamp01(res.CurrentAmount / res.MaxAmount);
            }
        }

        /// <summary>
        /// One shot's cost as a fraction of the FULL tank — 0.5 for the skyburst, whose bay
        /// holds two. 0 when there is no weapon to ask, or when a shot is free.
        /// </summary>
        public float ShotCost01
        {
            get
            {
                var action = ActiveAction;
                if (!action) return 0f;

                var index = ActiveAmmoIndex;
                if (index < 0 || index >= _resources.Resources.Count) return 0f;

                var res = _resources.Resources[index];
                if (res == null || res.MaxAmount <= 0f) return 0f;

                return Mathf.Clamp01(action.AmmoCost / res.MaxAmount);
            }
        }

        /// <summary>
        /// How far the tank has come toward the NEXT shot, 0..1 — the quantity a charge gauge
        /// shows, as distinct from <see cref="Ammo01"/>, which is how much the tank HOLDS.
        ///
        /// <para>They are different questions because a tank holds several shots: the Sparrow's
        /// missile bay is 0..1 with a rocket costing 0.5, so a half-full tank is ONE ROCKET
        /// READY and ZERO progress toward the second. The icon ladder already says how many you
        /// hold; this says how close the next one is, and it resets each time one is earned —
        /// which is exactly the ask ("it should fill … until a missile is created, then it
        /// resets as the energy for the second missile is acquired").</para>
        ///
        /// <para>A FULL tank reads FULL rather than empty. <c>frac</c> of a full rack is 0, and
        /// a gauge that empties the moment the rack fills says the opposite of the truth; there
        /// is simply nothing further to earn, which is what a full bar means.</para>
        ///
        /// <para>A weapon that spends the whole tank per shot (cost >= 1) degenerates correctly:
        /// the gauge is then just the tank.</para>
        /// </summary>
        public float ChargeToNextShot01 => ChargeToNextShot(Ammo01, ShotCost01);

        /// <summary>
        /// The arithmetic of <see cref="ChargeToNextShot01"/>, as a pure function so it can be
        /// tested without a vessel, a resource system or a live weapon.
        /// </summary>
        /// <param name="ammo01">How full the tank is, 0..1.</param>
        /// <param name="cost01">One shot's cost as a fraction of the full tank; 0 = unknown or
        /// free, in which case the gauge degenerates to the tank itself.</param>
        public static float ChargeToNextShot(float ammo01, float cost01)
        {
            ammo01 = Mathf.Clamp01(ammo01);
            if (ammo01 >= 1f) return 1f;
            if (cost01 <= 0f) return ammo01;

            float shots = ammo01 / cost01;
            return Mathf.Clamp01(shots - Mathf.Floor(shots));
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status    = shipStatus;
            _resources = shipStatus.ResourceSystem;

            _resources.OnResourceChanged -= HandleResourceChanged;
            _resources.OnResourceChanged += HandleResourceChanged;

            // Re-resolve on every Initialize: vessel swaps re-run Initialize on live
            // components and may hand this executor a different model hierarchy.
            _bayBonesResolved = false;
            _rightBayBone = null;
            _leftBayBone = null;

            if (gun != null)
                gun.Initialize(shipStatus);
        }

        void HandleResourceChanged(int index, float current, float max)
        {
            if (index != ActiveAmmoIndex)
                return;

            OnAmmoChanged?.Invoke(Ammo01);
        }

        public void Fire(FireGunActionSO so, IVesselStatus status)
        {
            var ammoBefore = _resources.Resources[so.AmmoIndex].CurrentAmount;
            if (ammoBefore < so.AmmoCost)
                return;

            _soRef = so;
            _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);

            OnAmmoChanged?.Invoke(Ammo01);

            // ONE side predicate for animation AND spawn: the first missile of a full pair
            // leaves the right bay, the last one the left bay (mirrors the alternating
            // "Missile Launch 1"/"Missile Launch 2" clips).
            var useRightBay = ammoBefore >= 2f * so.AmmoCost;
            OnMissileFired?.Invoke(useRightBay);

            var delay = so.LaunchDelaySeconds;
            if (delay <= 0f)
            {
                SpawnProjectile(so, status, useRightBay);
                return;
            }

            SpawnAfterBayOpensAsync(so, status, useRightBay, delay, PendingLaunchToken()).Forget();
        }

        async UniTaskVoid SpawnAfterBayOpensAsync(
            FireGunActionSO so, IVesselStatus status, bool useRightBay, float delay, CancellationToken token)
        {
            // Scaled time on purpose: the bay animation runs on the Animator's scaled clock,
            // so a paused/slowed game holds the missile in the bay rather than desyncing.
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
            SpawnProjectile(so, status, useRightBay);
        }

        void SpawnProjectile(FireGunActionSO so, IVesselStatus status, bool useRightBay)
        {
            audioSystem.PlayGameplaySFX(GameplaySFXCategory.GunFire);

            // Muzzle pose is snapshotted at SPAWN time (the vessel keeps flying through the
            // launch delay): position from the live bay bone so the projectile emerges where
            // the animated missile just left the hull, aim from the gun so flight matches
            // the vessel's current course.
            var gunTf = gun ? gun.transform : transform;
            var bay = BayBone(useRightBay);
            var spawnPos = bay ? bay.position : gunTf.position;
            _worldMuzzleAnchor.SetPositionAndRotation(spawnPos, gunTf.rotation);

            var inheritedVelocityWS = status.Course * status.Speed;

            OnGunFired?.Invoke();
            OnShotFired?.Invoke(_status?.PlayerName);

            // CHARGE → blast radius: ProjectileDetonatorSO lerps each effect asset's
            // MinScale..MaxScale by Projectile.Charge, so pass the live normalized Charge
            // level (0 at resting → authored minimum; 1 at level 10 → authored maximum).
            var charge01 = Mathf.Clamp01(ElementalScaling.Level01(status, Element.Charge));

            // CHARGE level-5 'Domain-Safe Skybursts': the direct-hit damage spares the
            // shooter's own domain. Per-shot snapshot at the moment the missile leaves.
            var spareOwnDomain = status.ElementalAbilityHandler.IsUpgradeActive(Element.Charge);

            // MASS → in-flight growth, exactly as the full-auto bullets do it: the missile
            // leaves the bay at the size of the one the bay animation just ejected and swells
            // as it travels. Live level, read at the moment the missile actually leaves (the
            // vessel keeps playing through the launch delay), never at press.
            var growth = so.ResolveGrowthFactor(status);

            gun.FireGun(
                _worldMuzzleAnchor,
                so.Speed,
                inheritedVelocityWS,
                so.ProjectileScale,
                true,
                so.ProjectileTime.Value,
                charge01,
                FiringPatterns.Default,
                so.Energy,
                detachAfterSpawn: _detachFromContainer,
                spareOwnDomain: spareOwnDomain,
                flightGrowthFactor: growth
            );
        }

        Transform BayBone(bool useRightBay)
        {
            ResolveBayBones();
            return useRightBay ? _rightBayBone : _leftBayBone;
        }

        void ResolveBayBones()
        {
            if (_bayBonesResolved) return;

            // ShipTransform delegates to Vessel.Transform, and Vessel is wired during the
            // vessel init chain - guard so an early shot degrades to the muzzle, not an NRE.
            var root = _status != null && _status.Vessel != null ? _status.ShipTransform : null;
            if (!root) return; // model not live yet - retry on the next shot

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!_rightBayBone && string.Equals(t.name, RightBayBoneName, StringComparison.OrdinalIgnoreCase))
                    _rightBayBone = t;
                else if (!_leftBayBone && string.Equals(t.name, LeftBayBoneName, StringComparison.OrdinalIgnoreCase))
                    _leftBayBone = t;
            }

            _bayBonesResolved = true;

            if ((!_rightBayBone || !_leftBayBone) && !_warnedMissingBayBones)
            {
                _warnedMissingBayBones = true;
                CSDebug.LogWarning($"[{nameof(FireGunActionExecutor)}] '{name}' could not find missile bay " +
                                   $"bone(s) '{RightBayBoneName}'/'{LeftBayBoneName}' under '{root.name}' - " +
                                   "projectiles will spawn at the gun muzzle instead of the bay. Check the " +
                                   "vessel model's rig names.");
            }
        }

        CancellationToken PendingLaunchToken()
        {
            _pendingLaunchCts ??= CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            return _pendingLaunchCts.Token;
        }

        // A pending launch must never outlive the flight it belongs to: cancelled on
        // disable, on turn end, and (via the linked token) on destroy. Ammo stays spent -
        // the missile was committed at the press.
        void CancelPendingLaunches()
        {
            if (_pendingLaunchCts == null) return;
            _pendingLaunchCts.Cancel();
            _pendingLaunchCts.Dispose();
            _pendingLaunchCts = null;
        }

        void OnTurnEndOfMiniGame()
        {
            CancelPendingLaunches();
            _soRef = null;
            OnAmmoChanged?.Invoke(Ammo01);
        }
    }
}
