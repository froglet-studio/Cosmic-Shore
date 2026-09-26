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
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[VESSEL] OnDestroy '{gameObject.name}' - IsSpawned={IsSpawned}, IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}");

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
            VesselRearView.ClearTarget(transform);
            VesselPlacementView.ClearTarget(transform);
            OnBeforeDestroyed?.Invoke();

            // The base is what tears down this behaviour's NetworkVariables. An override that
            // never calls it suppresses that teardown exactly as a hiding method would - and
            // without the CS0114 that catches the hiding case, which is why this one survived
            // while ArcadeConfigSyncManager's was reported.
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[VESSEL] OnNetworkSpawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}");
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
            CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[VESSEL] OnNetworkDespawn '{gameObject.name}' - IsServer={IsServer}, IsOwner={IsOwner}, NetObjId={NetworkObjectId}");
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
            // occlusion corridor (Docs/PRISM_ANIMATION.md §4.7), the speed tunnel
            // (Docs/SPEED_TUNNEL.md), and the vessel vision band's local-pilot exclusion
            // (Docs/VESSEL_VISION.md — the band marks every OTHER ship, never the one you are
            // flying). Initialize is the one method every vessel must call to
            // become a player's vessel: single-player spawn, multiplayer spawn, the menu
            // autopilot, and every runtime vessel swap all route through it. Binding here is
            // what makes it impossible to author a vessel or a minigame in which either is
            // off. IsLocalPilot (not IsLocalUser) so the non-networked single-player spawn
            // path is covered too. Do not move these onto a prefab, a camera, or a mode.
            if (player.IsLocalPilot)
            {
                PrismOcclusionCorridor.SetTarget(transform);
                VesselSpeedTunnel.SetTarget(VesselStatus, transform);
                VesselVisionShading.SetLocalVessel(transform);
                VesselRearView.SetTarget(transform);
                VesselPlacementView.SetTarget(transform);
            }

            // NO HIGH-POLY PRISM MORPH IS GRANTED HERE, and that is the design rather than an
            // omission. The family (.claude/skills/prism-morph, Docs/PRISM_ANIMATION.md §4.7.2)
            // deforms the surface of a handful of prisms out of a SHARED residency budget, so a
            // per-vessel grant does not add morphs, it DIVIDES the one that mattered until nothing
            // is smooth — and an effect strong enough to be an EVENT stops being one the moment
            // every hull in the match wears it. Its one member is the Urchin's cradle, granted by
            // the hull that can ride (GunVesselTransformer ensures PrismCradleSource) rather than
            // by every vessel's Initialize. Adding a second is a design call, not a wiring one, so
            // do not add an ensure here.

            // Pip is NOT granted here any more. The picture-in-picture rear view is retired in
            // favour of the look-back camera above (Docs/REAR_VIEW.md), which shows the same
            // thing full-screen, at the vessel's own follow distance, on the rig every camera
            // platform law is already bound to - instead of a second camera pass into a shared
            // render texture behind a frame whose art no longer exists. Pip.cs is deliberately
            // KEPT and deliberately never told it is the local pilot: its Awake default-off is
            // now the only thing standing PipCamera down on the eight hulls that carry one.

            if (gameData != null)
                ShipHelper.SetShipProperties(gameData.ThemeManagerData, this);
            else
                CSDebug.LogError($"[VesselController] GameDataSO is not assigned on {name}. Ship properties will not be set.");

            VesselStatus.Customization.Initialize(VesselStatus);
            VesselStatus.ResetForPlay();
            ApplyStartingElements();
            OnInitialized?.Invoke();
        }

        /// <summary>
        /// Seed this hull's element levels from the current card's per-hull table
        /// (<c>SO_ArcadeGame.StartingElements</c>, published into <c>GameDataSO</c>). Bound HERE
        /// for the reason the platform laws above are: Initialize is the one method every vessel
        /// passes through on every spawn path, on every machine, human and AI alike - so a card's
        /// handicap cannot be escaped by choosing a spawn path, and a guest's own vessel is seeded
        /// exactly as the host's replica of it. A hull with no row is left at rest: this never
        /// writes zeros over a seed some other path made. After ResetForPlay, which resets the
        /// named resources and leaves element levels alone.
        /// </summary>
        void ApplyStartingElements()
        {
            if (gameData == null || VesselStatus == null) return;
            if (!gameData.TryGetStartingElements(VesselStatus.VesselType, out var levels)) return;
            SetResourceLevels(levels);
        }
        
        public Transform Transform => transform;

        public void Teleport(Transform targetTransform)
        {
            // Counted like a SetPose: this writes the transform directly, and anything watching
            // the vessel's motion has to be able to tell a jump from a fast frame.
            VesselStatus?.VesselTransformer?.NotifyTeleported();
            ShipHelper.Teleport(transform, targetTransform);
        }

        public void SetResourceLevels(ResourceCollection resources) =>
            VesselStatus.ResourceSystem.InitializeElementLevels(resources);

        /// <summary>
        /// Roll the visible ship about its own forward axis — the mobile device-orientation path.
        ///
        /// <para>The handle is an AUTHORED child (<c>VesselStatus.orientationHandle</c>) and a
        /// vessel can ship without one, so this used to throw for such a hull — on a phone only,
        /// on the frame the device was flipped, from a call site that has nothing to say about
        /// vessel wiring. A hull with no handle simply has nothing to roll.</para>
        /// </summary>
        public void SetShipUp(float angle)
        {
            var handle = VesselStatus.OrientationHandle;
            if (handle) handle.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

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

        /// <summary>
        /// Put this vessel somewhere. The write travels to every peer, because a teleport that
        /// only happened on one machine is a vessel in two places.
        ///
        /// <para><b>A CLIENT may move its OWN vessel</b>, and that route is the reason this is
        /// three branches rather than one. <c>SetPose_ClientRpc</c> is a ClientRpc, which only a
        /// server may send — so every client-owned teleport (the Butterfly's Fold, a fold gate
        /// transit, the Wanderway's return) reached this method on a party guest, hit the ClientRpc
        /// and did nothing but log. The owner now asks the server, which broadcasts exactly as
        /// before.</para>
        ///
        /// <para>The SERVER branch is deliberately kept as it was rather than folded into the
        /// ServerRpc the way the slowed-transform pair is: a ServerRpc invoked on the server is
        /// still dispatched through the network layer, and every existing caller here is a
        /// host-side teleport that should not pay a tick for a route it does not need.</para>
        ///
        /// <para>A peer that is neither the server nor the owner writes nothing. It is not that
        /// machine's vessel to move, and it will receive the pose like everybody else.</para>
        /// </summary>
        public void SetPose(Pose pose)
        {
            if (!IsSpawned) { SetPose_Local(pose); return; }
            if (IsServer)   { SetPose_ClientRpc(pose); return; }
            if (IsOwner)      SetPose_ServerRpc(pose);
        }

        public void ChangePlayer(IPlayer player)
        {
            // The pause subscription belongs to the PILOT, so it has to be moved across the
            // pointer change: detached from the outgoing pilot while this vessel can still reach
            // them, re-attached to the incoming one (only if they are the local user).
            VesselStatus.ActionHandler.DetachInputPause();
            VesselStatus.Player = player;
            VesselStatus.ActionHandler.AttachInputPause();

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
                VesselVisionShading.SetLocalVessel(transform);
                VesselRearView.SetTarget(transform);
                VesselPlacementView.SetTarget(transform);
            }
            else
            {
                PrismOcclusionCorridor.ClearTarget(transform);
                VesselSpeedTunnel.ClearTarget(transform);
                VesselVisionShading.ClearLocalVessel(transform);
                VesselRearView.ClearTarget(transform);
                VesselPlacementView.ClearTarget(transform);
            }

            // The HUD is OPTIONAL on a hull, exactly as Initialize treats it: the Urchin ships
            // with no HUD controller at all. Dereferencing it unguarded here threw on the Urchin
            // halfway through a pilot swap - after the hull's Player had changed and before the
            // other hull's had - so the Urchin went on reading the AI's stick (it looked like
            // "autopilot switched on") while the human never reached the teammate's hull. A
            // hull-handover path must tolerate every optional component Initialize tolerates.
            var hud = VesselStatus.VesselHUDController;

            // If the player is AI in general, or if it is a network client
            if (player.IsInitializedAsAI || player.IsNetworkClient)
            {
                hud?.UnsubscribeFromEvents();
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
                hud?.HideHUD();

                return;
            }
            
            UnsubscribeFromNetworkVariables();

            hud?.SubscribeToEvents();
            hud?.ShowHUD();

                
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

        // RequireOwnership is left ON: this is the "I am moving MY OWN vessel" route, and the
        // server already has its own direct branch for moving anybody's.
        [ServerRpc]
        void SetPose_ServerRpc(Pose pose) => SetPose_ClientRpc(pose);

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
        
        // Guarded so a subscribe/unsubscribe is IDEMPOTENT. A vessel's replica subscription used
        // to be decided once, at spawn, from its ownership then - and ChangePlayer added a second
        // unguarded += on every hand-over, so a hull that changed hands mid-match (the Cellular
        // Duel swap, the arena pilot swap) either ran every replica callback twice or, having
        // BECOME the owner, kept overwriting its own simulation from its own echoed writes.
        // Subscription now follows OWNERSHIP (OnGainedOwnership / OnLostOwnership below), and
        // ChangePlayer's calls are no-ops when the state is already right.
        bool _netVarsSubscribed;

        /// <summary>
        /// A hull handed to another machine mid-match (<c>PilotSwap</c>, the Cellular Duel swap)
        /// stops reading its kinematics off the network the moment it is this machine's to
        /// simulate - whichever of the ownership message and the swap RPC lands first.
        /// </summary>
        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
            UnsubscribeFromNetworkVariables();
        }

        /// <summary>The mirror: a hull this machine no longer owns is driven by its new owner's
        /// replicated kinematics from here on.</summary>
        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
            SubscribeToNetworkVariables();
        }

        void SubscribeToNetworkVariables()
        {
            if (_netVarsSubscribed) return;
            _netVarsSubscribed = true;
            n_Speed.OnValueChanged += OnSpeedChanged;
            n_Course.OnValueChanged += OnCourseChanged;
            n_BlockRotation.OnValueChanged += OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged += OnIsTranslationRestrictedValueChanged;
        }
        
        void UnsubscribeFromNetworkVariables()
        {
            if (!_netVarsSubscribed) return;
            _netVarsSubscribed = false;
            n_Speed.OnValueChanged -= OnSpeedChanged;
            n_Course.OnValueChanged -= OnCourseChanged;
            n_BlockRotation.OnValueChanged -= OnBlockRotationChanged;
            n_IsTranslationRestricted.OnValueChanged -= OnIsTranslationRestrictedValueChanged;
        }
        
        void ToggleStationaryMode(bool enable) =>
            VesselStatus.IsStationary = enable;
    }
}
