using System;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Combines behaviour of R_LocalVessel and R_NetworkVessel. Behaviour is
    /// selected at runtime based on <see cref="IsSpawned"/> in multiplayer mode.
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class VesselController : NetworkBehaviour, IVessel
    {
        [SerializeField]
        GameDataSO gameData;
        
        public event Action OnInitialized;
        public event Action OnBeforeDestroyed;
        
        IVesselStatus vesselStatus;
        public IVesselStatus VesselStatus
        {
            get
            {
                vesselStatus ??= GetComponent<IVesselStatus>();
                return vesselStatus;
            }
        }

        public bool IsNetworkOwner => IsSpawned && IsOwner;
        public bool IsNetworkClient => IsSpawned && !IsOwner;
        
        readonly NetworkVariable<float> n_Speed = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Vector3> n_Course = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<Quaternion> n_BlockRotation = new(writePerm: NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> n_IsTranslationRestricted =
            new(writePerm: NetworkVariableWritePermission.Owner);
        
        public ulong PlayerNetId { get; private set; }
        public ulong VesselNetId => NetworkObjectId;
        public ulong OwnerClientNetId => OwnerClientId;
        
        public override void OnDestroy()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnDestroy '{gameObject.name}' - IsSpawned={IsSpawned}, IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");

            // Leave the roster we joined in OnNetworkSpawn. Without this a destroyed vessel stays
            // in gameData.Vessels forever, and every consumer that iterates it is exposed to a
            // MissingReferenceException: the list is List<IVessel>, so `vessel == null` is a plain
            // INTERFACE reference comparison that never reaches UnityEngine.Object's overload — a
            // destroyed hull sails through the guard and throws on the first member access.
            // The despawn path (ServerPlayerVesselInitializer) already removes; this covers every
            // other way a vessel dies, including the freestyle vessel-changer swap.
            if (gameData != null) gameData.Vessels.Remove(this);

            // Both clear only if THIS vessel is still the one in force, so a vessel swap whose
            // outgoing hull is destroyed after the incoming one initializes cannot cancel the
            // new binding.
            PrismOcclusionCorridor.ClearTarget(transform);
            VesselSpeedTunnel.ClearTarget(transform);
            OnBeforeDestroyed?.Invoke();
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnNetworkSpawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
            // Cache it to game data early, so that later,
            // ClientInitializer can find the player and vessels with their Ids
            gameData.Vessels.Add(this);
            gameData.InvokeVesselNetworkSpawned();

            if (IsOwner)
                return;

            SubscribeToNetworkVariables();
        }

        public override void OnNetworkDespawn()
        {
            Debug.Log($"<color=#FFFF00>[VESSEL] OnNetworkDespawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}</color>");
            if (IsOwner)
                return;

            UnsubscribeFromNetworkVariables();
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;

            // Per-frame owner→server kinematic replication - the hottest netcode write path.
            using (CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Serialize.Auto())
            {
                n_Speed.Value = VesselStatus.Speed;
                n_Course.Value = VesselStatus.Course;
                n_BlockRotation.Value = VesselStatus.blockRotation;
                CosmicShore.Utility.PerformanceBenchmark.NetMarkers.CountNetVarDirty(3);
            }
        }

        public void Initialize(IPlayer player)
        {
            if (VesselStatus.Player != null)
            {
                CSDebug.LogError("Double initialization not allowed!");
                return;
            }

            VesselStatus.Player = player;
            VesselStatus.VesselAnimation.Initialize(VesselStatus);
            VesselStatus.VesselPrismController.Initialize(VesselStatus);

            if (!VesselStatus.CameraFollowTarget)
                VesselStatus.CameraFollowTarget = transform;

            VesselStatus.ActionHandler.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.Initialize(this);
            VesselStatus.AIPilot.Initialize(this);

            var hudController = VesselStatus.VesselHUDController;
            if (hudController != null)
            {
                hudController.Initialize(VesselStatus);
                hudController.HideHUD();
            }
            else
            {
                CSDebug.LogWarning($"[VesselController] VesselHUDController is null on {name}. HUD will not function.");
            }

            if (VesselStatus.NearFieldSkimmer)
                VesselStatus.NearFieldSkimmer.Initialize(VesselStatus);

            if (VesselStatus.FarFieldSkimmer)
                VesselStatus.FarFieldSkimmer.Initialize(VesselStatus);

            VesselStatus.ElementalBarsController.Initialize(VesselStatus);
            VesselStatus.VesselTransformer.ToggleActive(true);

            if (player.IsLocalUser)
            {
                VesselStatus.ActionHandler.ToggleSubscription(true);
                VesselStatus.VesselCameraCustomizer.Initialize(this);
                hudController?.SubscribeToEvents();
            }

            // PLATFORM LAWS — bound HERE, not per vessel and not per game mode: the prism
            // occlusion corridor (Docs/PRISM_ANIMATION.md §4.7) and the speed tunnel
            // (Docs/SPEED_TUNNEL.md). Initialize is the one method every vessel must call to
            // become a player's vessel: single-player spawn, multiplayer spawn, the menu
            // autopilot, and every runtime vessel swap all route through it. Binding here is
            // what makes it impossible to author a vessel or a minigame in which either is
            // off. IsLocalPilot (not IsLocalUser) so the non-networked single-player spawn
            // path is covered too. Do not move these onto a prefab, a camera, or a mode.
            if (player.IsLocalPilot)
            {
                PrismOcclusionCorridor.SetTarget(transform);
                VesselSpeedTunnel.SetTarget(VesselStatus, transform);
            }

            // TAIL + JETS (Docs/VESSEL_TAIL_AND_JETS.md) — bound here for the same reason as the
            // laws above: Initialize is the one method every vessel calls on every spawn path, so
            // a hull cannot be authored, nor a mode written, in which a pilot sees somebody else's
            // engine plumes. NOT gated on IsLocalPilot — the call is made on every machine and the
            // flag is the argument, because a remote replica must be told to HIDE its jets just as
            // deliberately as the local one is told to show them. Ordered BEFORE SetShipProperties
            // so the domain paint below lands on the set this pass just settled.
            TailAndJets?.SetViewerIsOwnPilot(player.IsLocalPilot);

            if (gameData != null)
                ShipHelper.SetShipProperties(gameData.ThemeManagerData, this);
            else
                CSDebug.LogError($"[VesselController] GameDataSO is not assigned on {name}. Ship properties will not be set.");

            VesselStatus.Customization.Initialize(VesselStatus);
            VesselStatus.ResetForPlay();
            OnInitialized?.Invoke();
        }
        
        public Transform Transform => transform;

        public void Teleport(Transform targetTransform) =>
            ShipHelper.Teleport(transform, targetTransform);

        public void SetResourceLevels(ResourceCollection resources) =>
            VesselStatus.ResourceSystem.InitializeElementLevels(resources);

        public void SetShipUp(float angle) =>
            VesselStatus.OrientationHandle.transform.localRotation = Quaternion.Euler(0, 0, angle);

        public void DisableSkimmer()
        {
            VesselStatus.NearFieldSkimmer?.gameObject.SetActive(false);
            VesselStatus.FarFieldSkimmer?.gameObject.SetActive(false);
        }

        public void SetBoostMultiplier(float multiplier) => VesselStatus.BoostMultiplier = multiplier;
        
        public void SetShipMaterial(Material material) =>
            VesselStatus.ShipMaterial = material;

        public void SetAOEExplosionMaterial(Material material) =>
            VesselStatus.AOEExplosionMaterial = material;

        public virtual void SetAOEConicExplosionMaterial(Material material) =>
                VesselStatus.AOEConicExplosionMaterial = material;

        public virtual void SetSkimmerMaterial(Material material) =>
                VesselStatus.SkimmerMaterial = material;

        VesselTailAndJets _tailAndJets;

        /// <summary>
        /// This vessel's TAIL and JETS (Docs/VESSEL_TAIL_AND_JETS.md). Resolved lazily and cached:
        /// the component is optional today because the fleet is still being migrated onto the
        /// standard, so a vessel without one simply has no tail or jets to paint or hide.
        /// </summary>
        VesselTailAndJets TailAndJets =>
            _tailAndJets != null
                ? _tailAndJets
                : _tailAndJets = GetComponentInChildren<VesselTailAndJets>(includeInactive: true);

        public virtual void SetTailAndJetColors(Color highlightColor, Color coreColor) =>
            TailAndJets?.SetColors(highlightColor, coreColor);

        public virtual void BindElementalFloat(string name, Element element) =>
            VesselStatus.ElementalStatsHandler.BindElementalFloat(name, element);

        public void PerformShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.PerformShipControllerActions(controlType);

        public void StopShipControllerActions(InputEvents controlType) =>
                VesselStatus.ActionHandler.StopShipControllerActions(controlType);

        public void ToggleAIPilot(bool toggle)
        {
            if (toggle)
                VesselStatus.AIPilot.StartAIPilot();
            else
                VesselStatus.AIPilot.StopAIPilot();
        }

        public void DestroyVessel()
        {
            if (IsSpawned)
            {
                if (IsServer)
                    NetworkObject.Despawn(true);
                return;
            }
            Destroy(gameObject);
        }

        public void StartVessel()
        {
            ToggleStationaryMode(false);
            VesselStatus.VesselPrismController.StartSpawn();
        }

        public void ResetForPlay()
        {
            if (IsSpawned && IsOwner)
            {
                VesselStatus.Speed = 0f;
                VesselStatus.Course = transform.forward;
                VesselStatus.blockRotation = Quaternion.identity;
            }
            VesselStatus.ResetForPlay();
        }

        public void SetPose(Pose pose)
        {
            if (IsSpawned)
                SetPose_ClientRpc(pose);
            else
                SetPose_Local(pose);
        }

        public void ChangePlayer(IPlayer player)
        {
            VesselStatus.Player = player;

            // Re-evaluate BOTH platform laws: ChangePlayer hands a LIVE vessel to a different
            // player (the Cellular Duel round-boundary ownership swap), which Initialize never
            // sees. Without this the tunnel would keep driving the local camera from a vessel
            // the local player no longer flies, and the occlusion corridor would keep cutting
            // its hole around the hull the AI inherited — leaving the local pilot's own ship
            // hidden behind prism mass for the whole next round, the exact condition the
            // corridor exists to prevent. Both clears are identity-guarded, so the losing
            // vessel's release cannot cancel the winning vessel's bind whatever the call order.
            if (player.IsLocalPilot)
            {
                PrismOcclusionCorridor.SetTarget(transform);
                VesselSpeedTunnel.SetTarget(VesselStatus, transform);
            }
            else
            {
                PrismOcclusionCorridor.ClearTarget(transform);
                VesselSpeedTunnel.ClearTarget(transform);
            }

            // Same handover, same reason: the vessel the local pilot just gave up must stop
            // drawing its jets on this screen, and the one they just took up must start.
            TailAndJets?.SetViewerIsOwnPilot(player.IsLocalPilot);

            // If the player is AI in general, or if it is a network client
            if (player.IsInitializedAsAI || player.IsNetworkClient)
            {
                VesselStatus.VesselHUDController.UnsubscribeFromEvents();
                if (player.IsInitializedAsAI)
                {
                    VesselStatus.VesselTransformer.ToggleActive(true);
                }
                if (player.IsNetworkClient)
                {
                    VesselStatus.VesselTransformer.ToggleActive(false);
                    SubscribeToNetworkVariables();
                }
                VesselStatus.ActionHandler.ToggleSubscription(false);
                VesselStatus.VesselHUDController.HideHUD();

                return;
            }
            
            UnsubscribeFromNetworkVariables();

            VesselStatus.VesselHUDController.SubscribeToEvents();
            VesselStatus.VesselHUDController.ShowHUD();

                
            VesselStatus.VesselTransformer.ToggleActive(true);
            VesselStatus.ActionHandler.ToggleSubscription(true);
            VesselStatus.VesselCameraCustomizer.RetargetAndApply(this);
        }
        
        public void SetTranslationRestricted(bool value)
        {
            if (IsNetworkOwner)
                n_IsTranslationRestricted.Value = value;

            VesselStatus.IsTranslationRestricted = value; 
        }

        public void ModifyThrottle(float amount, float duration) =>
            VesselStatus.VesselTransformer.ModifyThrottle(amount, duration);
        
        public void AddSlowedShipTransformToGameData()
        {
            if (IsSpawned)
                AddSlowedShipTransformToGameData_ServerRpc();
            else
                AddSlowedShipTransformToGameData_Local();
        }
        
        public void RemoveSlowedShipTransformFromGameData()
        {
            if (IsSpawned)
                RemoveSlowedShipTransformFromGameData_ServerRpc();
            else
                RemoveSlowedShipTransformFromGameData_Local();
        }

        [ServerRpc(RequireOwnership = false)]
        void RemoveSlowedShipTransformFromGameData_ServerRpc() =>
            RemoveSlowedShipTransformFromGameData_ClientRpc();

        [ClientRpc]
        void RemoveSlowedShipTransformFromGameData_ClientRpc() =>
            RemoveSlowedShipTransformFromGameData_Local();
        void RemoveSlowedShipTransformFromGameData_Local() =>
            gameData?.SlowedShipTransforms.Remove(transform);
        
        [ServerRpc(RequireOwnership = false)]
        void AddSlowedShipTransformToGameData_ServerRpc() =>
            AddSlowedShipTransformToGameData_ClientRpc();

        [ClientRpc]
        void AddSlowedShipTransformToGameData_ClientRpc() =>
            AddSlowedShipTransformToGameData_Local();
        void AddSlowedShipTransformToGameData_Local() =>
            gameData?.SlowedShipTransforms.Add(transform);

        [ClientRpc]
        void SetPose_ClientRpc(Pose pose) => SetPose_Local(pose);

        void SetPose_Local(Pose pose) => VesselStatus.VesselTransformer.SetPose(pose);

        // Route to the owner (like SetPose) so a party client's own swapped vessel also inherits
        // the previous ship's speed, not just the host's.
        public void SetInitialSpeed(float initialSpeed)
        {
            if (IsSpawned)
                SetInitialSpeed_ClientRpc(initialSpeed);
            else
                SetInitialSpeed_Local(initialSpeed);
        }

        [ClientRpc]
        void SetInitialSpeed_ClientRpc(float initialSpeed) => SetInitialSpeed_Local(initialSpeed);

        void SetInitialSpeed_Local(float initialSpeed) => VesselStatus.VesselTransformer.SetInitialSpeed(initialSpeed);
        
        void OnSpeedChanged(float previousValue, float newValue) => VesselStatus.Speed = newValue;
        void OnCourseChanged(Vector3 previousValue, Vector3 newValue) => VesselStatus.Course = newValue;
        void OnBlockRotationChanged(Quaternion previousValue, Quaternion newValue) => VesselStatus.blockRotation = newValue;
        void OnIsTranslationRestrictedValueChanged(bool previousValue, bool newValue) => VesselStatus.IsTranslationRestricted = newValue;
        
        void SubscribeToNetworkVariables()
        {
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged += OnIsTranslationRestrictedValueChanged;
        }
        
        void UnsubscribeFromNetworkVariables()
        {
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged -= OnIsTranslationRestrictedValueChanged;
        }
        
        void ToggleStationaryMode(bool enable) =>
            VesselStatus.IsStationary = enable;
    }
}
