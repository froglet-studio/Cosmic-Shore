using System;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    public class RoundStats : NetworkBehaviour, IRoundStats
    {
        //–––––––––––––––––––––––––––––––––––––––––
        // Events
        public event Action OnScoreChanged;
        public event Action<IRoundStats> OnVolumeCreatedChanged;
        public event Action<IRoundStats> OnVolumeDestroyedChanged;

        //–––––––––––––––––––––––––––––––––––––––––
        // Local fallback fields (offline / not spawned)
        string _nameLocal;
        Domains _domainLocal;
        float _scoreLocal, _volumeCreatedLocal;

        int _blocksCreatedLocal, _blocksDestroyedLocal, _blocksRestoredLocal;
        int _prismStolenLocal, _prismsRemainingLocal;
        int _friendlyPrismsDestroyedLocal, _hostilePrismsDestroyedLocal;
        float _volumeDestroyedLocal, _volumeRestoredLocal, _volumeStolenLocal, _volumeRemainingLocal;
        float _friendlyVolumeDestroyedLocal, _hostileVolumeDestroyedLocal;
        int _crystalsCollectedLocal, _omniCrystalsCollectedLocal, _elementalCrystalsCollectedLocal;
        float _chargeCrystalValueLocal, _massCrystalValueLocal, _spaceCrystalValueLocal, _timeCrystalValueLocal;
        int _skimmerShipCollisionsLocal;
        float _fullSpeedStraightAbilityActiveTimeLocal, _rightStickAbilityActiveTimeLocal, _leftStickAbilityActiveTimeLocal;
        float _flipAbilityActiveTimeLocal, _button1AbilityActiveTimeLocal, _button2AbilityActiveTimeLocal, _button3AbilityActiveTimeLocal;

        //–––––––––––––––––––––––––––––––––––––––––
        // NetworkVariables
        readonly NetworkVariable<FixedString64Bytes> n_Name = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Domains> n_Domain = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Score = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_VolumeCreated = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_BlocksCreated = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_BlocksDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_BlocksRestored = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_PrismStolen = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_PrismsRemaining = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_FriendlyPrismsDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_HostilePrismsDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_VolumeDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_VolumeRestored = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_VolumeStolen = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_VolumeRemaining = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_FriendlyVolumeDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_HostileVolumeDestroyed = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_CrystalsCollected = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_OmniCrystalsCollected = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_ElementalCrystalsCollected = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_ChargeCrystalValue = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_MassCrystalValue = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_SpaceCrystalValue = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_TimeCrystalValue = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_SkimmerShipCollisions = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_FullSpeedStraightAbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_RightStickAbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_LeftStickAbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_FlipAbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button1AbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button2AbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button3AbilityActiveTime = new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        //–––––––––––––––––––––––––––––––––––––––––
        // Properties (switch between offline/local and online/networked)

        public string Name
        {
            get => IsSpawned ? n_Name.Value.ToString() : _nameLocal;
            set { if (IsSpawned && IsServer) n_Name.Value = value; else _nameLocal = value; }
        }

        public Domains Domain
        {
            get => IsSpawned ? n_Domain.Value : _domainLocal;
            set { if (IsSpawned && IsServer) n_Domain.Value = value; else _domainLocal = value; }
        }

        public float Score
        {
            get => IsSpawned ? n_Score.Value : _scoreLocal;
            set
            {
                if (IsSpawned && IsServer)
                    n_Score.Value = value;
                else
                {
                    _scoreLocal = value;
                    OnScoreChanged?.Invoke();
                }
            }
        }

        public float VolumeCreated
        {
            get => IsSpawned ? n_VolumeCreated.Value : _volumeCreatedLocal;
            set
            {
                if (IsSpawned && IsServer)
                    n_VolumeCreated.Value = value;
                else
                {
                    _volumeCreatedLocal = value;
                    OnVolumeCreatedChanged?.Invoke(this);
                }
            }
        }

        public int BlocksCreated { get => IsSpawned ? n_BlocksCreated.Value : _blocksCreatedLocal; set { if (IsSpawned && IsServer) n_BlocksCreated.Value = value; else _blocksCreatedLocal = value; } }
        public int BlocksDestroyed { get => IsSpawned ? n_BlocksDestroyed.Value : _blocksDestroyedLocal; set { if (IsSpawned && IsServer) n_BlocksDestroyed.Value = value; else _blocksDestroyedLocal = value; } }
        public int BlocksRestored { get => IsSpawned ? n_BlocksRestored.Value : _blocksRestoredLocal; set { if (IsSpawned && IsServer) n_BlocksRestored.Value = value; else _blocksRestoredLocal = value; } }

        public int PrismStolen { get => IsSpawned ? n_PrismStolen.Value : _prismStolenLocal; set { if (IsSpawned && IsServer) n_PrismStolen.Value = value; else _prismStolenLocal = value; } }
        public int PrismsRemaining { get => IsSpawned ? n_PrismsRemaining.Value : _prismsRemainingLocal; set { if (IsSpawned && IsServer) n_PrismsRemaining.Value = value; else _prismsRemainingLocal = value; } }

        public int FriendlyPrismsDestroyed { get => IsSpawned ? n_FriendlyPrismsDestroyed.Value : _friendlyPrismsDestroyedLocal; set { if (IsSpawned && IsServer) n_FriendlyPrismsDestroyed.Value = value; else _friendlyPrismsDestroyedLocal = value; } }
        public int HostilePrismsDestroyed { get => IsSpawned ? n_HostilePrismsDestroyed.Value : _hostilePrismsDestroyedLocal; set { if (IsSpawned && IsServer) n_HostilePrismsDestroyed.Value = value; else _hostilePrismsDestroyedLocal = value; } }

        public float VolumeDestroyed
        {
            get => IsSpawned ? n_VolumeDestroyed.Value : _volumeDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) 
                    n_VolumeDestroyed.Value = value;
                else
                {
                    _volumeDestroyedLocal = value;
                    OnVolumeDestroyedChanged?.Invoke(this);
                }
            }
        }
        public float VolumeRestored { get => IsSpawned ? n_VolumeRestored.Value : _volumeRestoredLocal; set { if (IsSpawned && IsServer) n_VolumeRestored.Value = value; else _volumeRestoredLocal = value; } }
        public float VolumeStolen { get => IsSpawned ? n_VolumeStolen.Value : _volumeStolenLocal; set { if (IsSpawned && IsServer) n_VolumeStolen.Value = value; else _volumeStolenLocal = value; } }
        public float VolumeRemaining { get => IsSpawned ? n_VolumeRemaining.Value : _volumeRemainingLocal; set { if (IsSpawned && IsServer) n_VolumeRemaining.Value = value; else _volumeRemainingLocal = value; } }

        public float FriendlyVolumeDestroyed { get => IsSpawned ? n_FriendlyVolumeDestroyed.Value : _friendlyVolumeDestroyedLocal; set { if (IsSpawned && IsServer) n_FriendlyVolumeDestroyed.Value = value; else _friendlyVolumeDestroyedLocal = value; } }
        public float HostileVolumeDestroyed { get => IsSpawned ? n_HostileVolumeDestroyed.Value : _hostileVolumeDestroyedLocal; set { if (IsSpawned && IsServer) n_HostileVolumeDestroyed.Value = value; else _hostileVolumeDestroyedLocal = value; } }

        public int CrystalsCollected { get => IsSpawned ? n_CrystalsCollected.Value : _crystalsCollectedLocal; set { if (IsSpawned && IsServer) n_CrystalsCollected.Value = value; else _crystalsCollectedLocal = value; } }
        public int OmniCrystalsCollected { get => IsSpawned ? n_OmniCrystalsCollected.Value : _omniCrystalsCollectedLocal; set { if (IsSpawned && IsServer) n_OmniCrystalsCollected.Value = value; else _omniCrystalsCollectedLocal = value; } }
        public int ElementalCrystalsCollected { get => IsSpawned ? n_ElementalCrystalsCollected.Value : _elementalCrystalsCollectedLocal; set { if (IsSpawned && IsServer) n_ElementalCrystalsCollected.Value = value; else _elementalCrystalsCollectedLocal = value; } }

        public float ChargeCrystalValue { get => IsSpawned ? n_ChargeCrystalValue.Value : _chargeCrystalValueLocal; set { if (IsSpawned && IsServer) n_ChargeCrystalValue.Value = value; else _chargeCrystalValueLocal = value; } }
        public float MassCrystalValue { get => IsSpawned ? n_MassCrystalValue.Value : _massCrystalValueLocal; set { if (IsSpawned && IsServer) n_MassCrystalValue.Value = value; else _massCrystalValueLocal = value; } }
        public float SpaceCrystalValue { get => IsSpawned ? n_SpaceCrystalValue.Value : _spaceCrystalValueLocal; set { if (IsSpawned && IsServer) n_SpaceCrystalValue.Value = value; else _spaceCrystalValueLocal = value; } }
        public float TimeCrystalValue { get => IsSpawned ? n_TimeCrystalValue.Value : _timeCrystalValueLocal; set { if (IsSpawned && IsServer) n_TimeCrystalValue.Value = value; else _timeCrystalValueLocal = value; } }

        public int SkimmerShipCollisions { get => IsSpawned ? n_SkimmerShipCollisions.Value : _skimmerShipCollisionsLocal; set { if (IsSpawned && IsServer) n_SkimmerShipCollisions.Value = value; else _skimmerShipCollisionsLocal = value; } }

        public float FullSpeedStraightAbilityActiveTime { get => IsSpawned ? n_FullSpeedStraightAbilityActiveTime.Value : _fullSpeedStraightAbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_FullSpeedStraightAbilityActiveTime.Value = value; else _fullSpeedStraightAbilityActiveTimeLocal = value; } }
        public float RightStickAbilityActiveTime { get => IsSpawned ? n_RightStickAbilityActiveTime.Value : _rightStickAbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_RightStickAbilityActiveTime.Value = value; else _rightStickAbilityActiveTimeLocal = value; } }
        public float LeftStickAbilityActiveTime { get => IsSpawned ? n_LeftStickAbilityActiveTime.Value : _leftStickAbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_LeftStickAbilityActiveTime.Value = value; else _leftStickAbilityActiveTimeLocal = value; } }
        public float FlipAbilityActiveTime { get => IsSpawned ? n_FlipAbilityActiveTime.Value : _flipAbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_FlipAbilityActiveTime.Value = value; else _flipAbilityActiveTimeLocal = value; } }
        public float Button1AbilityActiveTime { get => IsSpawned ? n_Button1AbilityActiveTime.Value : _button1AbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_Button1AbilityActiveTime.Value = value; else _button1AbilityActiveTimeLocal = value; } }
        public float Button2AbilityActiveTime { get => IsSpawned ? n_Button2AbilityActiveTime.Value : _button2AbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_Button2AbilityActiveTime.Value = value; else _button2AbilityActiveTimeLocal = value; } }
        public float Button3AbilityActiveTime { get => IsSpawned ? n_Button3AbilityActiveTime.Value : _button3AbilityActiveTimeLocal; set { if (IsSpawned && IsServer) n_Button3AbilityActiveTime.Value = value; else _button3AbilityActiveTimeLocal = value; } }

        //–––––––––––––––––––––––––––––––––––––––––
        // Network Event Hooks
        public override void OnNetworkSpawn()
        {
            n_Score.OnValueChanged += OnNetworkScoreChanged;
            n_VolumeCreated.OnValueChanged += OnNetworkVolumeCreated;
            n_VolumeDestroyed.OnValueChanged += OnNetworkVolumeDestroyed;
        }

        public override void OnNetworkDespawn()
        {
            n_Score.OnValueChanged -= OnNetworkScoreChanged;
            n_VolumeCreated.OnValueChanged -= OnNetworkVolumeCreated;
            n_VolumeDestroyed.OnValueChanged -= OnNetworkVolumeDestroyed;
        }

        void OnNetworkScoreChanged(float oldVal, float newVal) => OnScoreChanged?.Invoke();
        void OnNetworkVolumeCreated(float oldVal, float newVal) => OnVolumeCreatedChanged?.Invoke(this);
        void OnNetworkVolumeDestroyed(float oldVal, float newVal) => OnVolumeDestroyedChanged?.Invoke(this);

        //–––––––––––––––––––––––––––––––––––––––––
        // Reset Method (same pattern as InputStatus)
        public void ResetForReplay()
        {
            if (IsSpawned && !IsServer)
                return;

            Score = 0f;
            VolumeCreated = 0f;
            VolumeDestroyed = 0;

            BlocksCreated = 0;
            BlocksDestroyed = 0;
            BlocksRestored = 0;
            PrismStolen = 0;
            PrismsRemaining = 0;
            FriendlyPrismsDestroyed = 0;
            HostilePrismsDestroyed = 0;
            VolumeRestored = 0;
            VolumeStolen = 0;
            VolumeRemaining = 0;
            FriendlyVolumeDestroyed = 0;
            HostileVolumeDestroyed = 0;
            CrystalsCollected = 0;
            OmniCrystalsCollected = 0;
            ElementalCrystalsCollected = 0;
            ChargeCrystalValue = 0;
            MassCrystalValue = 0;
            SpaceCrystalValue = 0;
            TimeCrystalValue = 0;
            SkimmerShipCollisions = 0;
            FullSpeedStraightAbilityActiveTime = 0;
            RightStickAbilityActiveTime = 0;
            LeftStickAbilityActiveTime = 0;
            FlipAbilityActiveTime = 0;
            Button1AbilityActiveTime = 0;
            Button2AbilityActiveTime = 0;
            Button3AbilityActiveTime = 0;
        }
    }
}
