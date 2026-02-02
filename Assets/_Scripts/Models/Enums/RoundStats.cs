using System;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

namespace CosmicShore.Game
{
    public class RoundStats : NetworkBehaviour, IRoundStats
    {
        //–––––––––––––––––––––––––––––––––––––––––
        // EVENTS
        //–––––––––––––––––––––––––––––––––––––––––

        public event Action<IRoundStats> OnAnyStatChanged;

        public event Action OnScoreChanged;

        public event Action<IRoundStats> OnBlocksCreatedChanged;
        public event Action<IRoundStats> OnBlocksDestroyedChanged;
        public event Action<IRoundStats> OnBlocksRestoredChanged;
        public event Action<IRoundStats> OnPrismsStolenChanged;
        public event Action<IRoundStats> OnPrismsRemainingChanged;
        public event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
        public event Action<IRoundStats> OnHostilePrismsDestroyedChanged;

        public event Action<IRoundStats> OnVolumeCreatedChanged;
        public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
        public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
        public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
        public event Action<IRoundStats> OnVolumeRestoredChanged;
        public event Action<IRoundStats> OnVolumeStolenChanged;
        public event Action<IRoundStats> OnVolumeRemainingChanged;

        public event Action<IRoundStats> OnCrystalsCollectedChanged;
        public event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
        public event Action<IRoundStats> OnElementalCrystalsCollectedChanged;

        public event Action<IRoundStats> OnChargeCrystalValueChanged;
        public event Action<IRoundStats> OnMassCrystalValueChanged;
        public event Action<IRoundStats> OnSpaceCrystalValueChanged;
        public event Action<IRoundStats> OnTimeCrystalValueChanged;

        public event Action<IRoundStats> OnSkimmerShipCollisionsChanged;

        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

        //–––––––––––––––––––––––––––––––––––––––––
        // LOCAL OFFLINE STORAGE
        //–––––––––––––––––––––––––––––––––––––––––

        string _nameLocal;
        Domains _domainLocal;

        float _scoreLocal, _volumeCreatedLocal;
        int _blocksCreatedLocal, _blocksDestroyedLocal, _blocksRestoredLocal;
        int _prismStolenLocal, _prismsRemainingLocal;
        int _friendlyPrismsDestroyedLocal, _hostilePrismsDestroyedLocal;
        float _totalVolumeDestroyedLocal, _volumeRestoredLocal, _volumeStolenLocal, _volumeRemainingLocal;
        float _friendlyVolumeDestroyedLocal, _hostileVolumeDestroyedLocal;

        int _crystalsCollectedLocal, _omniCrystalsCollectedLocal, _elementalCrystalsCollectedLocal;
        float _chargeCrystalValueLocal, _massCrystalValueLocal, _spaceCrystalValueLocal, _timeCrystalValueLocal;
        int _skimmerShipCollisionsLocal;
        float _fullSpeedStraightAbilityActiveTimeLocal, _rightStickAbilityActiveTimeLocal, _leftStickAbilityActiveTimeLocal;
        float _flipAbilityActiveTimeLocal, _button1AbilityActiveTimeLocal, _button2AbilityActiveTimeLocal, _button3AbilityActiveTimeLocal;

        //–––––––––––––––––––––––––––––––––––––––––
        // NETWORK VARIABLES
        //–––––––––––––––––––––––––––––––––––––––––

        readonly NetworkVariable<FixedString64Bytes> n_Name =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<Domains> n_Domain =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_Score =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_VolumeCreated =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_BlocksCreated =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_BlocksDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_BlocksRestored =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_PrismStolen =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_PrismsRemaining =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_FriendlyPrismsDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_HostilePrismsDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_TotalVolumeDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_VolumeRestored =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_VolumeStolen =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_VolumeRemaining =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_FriendlyVolumeDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_HostileVolumeDestroyed =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_CrystalsCollected =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_OmniCrystalsCollected =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> n_ElementalCrystalsCollected =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_ChargeCrystalValue =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_MassCrystalValue =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_SpaceCrystalValue =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_TimeCrystalValue =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<int> n_SkimmerShipCollisions =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        readonly NetworkVariable<float> n_FullSpeedStraightAbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_RightStickAbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_LeftStickAbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_FlipAbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button1AbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button2AbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> n_Button3AbilityActiveTime =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        //–––––––––––––––––––––––––––––––––––––––––
        // HELPERS
        //–––––––––––––––––––––––––––––––––––––––––

        void RaiseSpecific(Action<IRoundStats> evt)
        {
            evt?.Invoke(this);
            OnAnyStatChanged?.Invoke(this);
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // PROPERTIES
        //–––––––––––––––––––––––––––––––––––––––––

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
                    _scoreLocal = value;

                if (IsSpawned) return;
                OnScoreChanged?.Invoke();
                OnAnyStatChanged?.Invoke(this);
            }
        }

        public float VolumeCreated
        {
            get => IsSpawned ? n_VolumeCreated.Value : _volumeCreatedLocal;
            set
            {
                if (IsSpawned && IsServer) n_VolumeCreated.Value = value;
                else _volumeCreatedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnVolumeCreatedChanged);
            }
        }

        public int BlocksCreated
        {
            get => IsSpawned ? n_BlocksCreated.Value : _blocksCreatedLocal;
            set
            {
                if (IsSpawned && IsServer) n_BlocksCreated.Value = value;
                else _blocksCreatedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnBlocksCreatedChanged);
            }
        }

        public int BlocksDestroyed
        {
            get => IsSpawned ? n_BlocksDestroyed.Value : _blocksDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_BlocksDestroyed.Value = value;
                else _blocksDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnBlocksDestroyedChanged);
            }
        }

        public int BlocksRestored
        {
            get => IsSpawned ? n_BlocksRestored.Value : _blocksRestoredLocal;
            set
            {
                if (IsSpawned && IsServer) n_BlocksRestored.Value = value;
                else _blocksRestoredLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnBlocksRestoredChanged);
            }
        }

        public int PrismStolen
        {
            get => IsSpawned ? n_PrismStolen.Value : _prismStolenLocal;
            set
            {
                if (IsSpawned && IsServer) n_PrismStolen.Value = value;
                else _prismStolenLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnPrismsStolenChanged);
            }
        }

        public int PrismsRemaining
        {
            get => IsSpawned ? n_PrismsRemaining.Value : _prismsRemainingLocal;
            set
            {
                if (IsSpawned && IsServer) n_PrismsRemaining.Value = value;
                else _prismsRemainingLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnPrismsRemainingChanged);
            }
        }

        public int FriendlyPrismsDestroyed
        {
            get => IsSpawned ? n_FriendlyPrismsDestroyed.Value : _friendlyPrismsDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_FriendlyPrismsDestroyed.Value = value;
                else _friendlyPrismsDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnFriendlyPrismsDestroyedChanged);
            }
        }

        public int HostilePrismsDestroyed
        {
            get => IsSpawned ? n_HostilePrismsDestroyed.Value : _hostilePrismsDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_HostilePrismsDestroyed.Value = value;
                else _hostilePrismsDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnHostilePrismsDestroyedChanged);
            }
        }

        public float TotalVolumeDestroyed
        {
            get => IsSpawned ? n_TotalVolumeDestroyed.Value : _totalVolumeDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_TotalVolumeDestroyed.Value = value;
                else _totalVolumeDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnTotalVolumeDestroyedChanged);
            }
        }

        public float VolumeRestored
        {
            get => IsSpawned ? n_VolumeRestored.Value : _volumeRestoredLocal;
            set
            {
                if (IsSpawned && IsServer) n_VolumeRestored.Value = value;
                else _volumeRestoredLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnVolumeRestoredChanged);
            }
        }

        public float VolumeStolen
        {
            get => IsSpawned ? n_VolumeStolen.Value : _volumeStolenLocal;
            set
            {
                if (IsSpawned && IsServer) n_VolumeStolen.Value = value;
                else _volumeStolenLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnVolumeStolenChanged);
            }
        }

        public float VolumeRemaining
        {
            get => IsSpawned ? n_VolumeRemaining.Value : _volumeRemainingLocal;
            set
            {
                if (IsSpawned && IsServer) n_VolumeRemaining.Value = value;
                else _volumeRemainingLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnVolumeRemainingChanged);
            }
        }

        public float FriendlyVolumeDestroyed
        {
            get => IsSpawned ? n_FriendlyVolumeDestroyed.Value : _friendlyVolumeDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_FriendlyVolumeDestroyed.Value = value;
                else _friendlyVolumeDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnFriendlyVolumeDestroyedChanged);
            }
        }

        public float HostileVolumeDestroyed
        {
            get => IsSpawned ? n_HostileVolumeDestroyed.Value : _hostileVolumeDestroyedLocal;
            set
            {
                if (IsSpawned && IsServer) n_HostileVolumeDestroyed.Value = value;
                else _hostileVolumeDestroyedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnHostileVolumeDestroyedChanged);
            }
        }

        //──────────────────────────────────────────
        // CRYSTALS & OTHER STATS
        //──────────────────────────────────────────

        public int CrystalsCollected
        {
            get => IsSpawned ? n_CrystalsCollected.Value : _crystalsCollectedLocal;
            set
            {
                if (IsSpawned && IsServer) n_CrystalsCollected.Value = value;
                else _crystalsCollectedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnCrystalsCollectedChanged);
            }
        }

        public int OmniCrystalsCollected
        {
            get => IsSpawned ? n_OmniCrystalsCollected.Value : _omniCrystalsCollectedLocal;
            set
            {
                if (IsSpawned && IsServer) n_OmniCrystalsCollected.Value = value;
                else _omniCrystalsCollectedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnOmniCrystalsCollectedChanged);
            }
        }

        public int ElementalCrystalsCollected
        {
            get => IsSpawned ? n_ElementalCrystalsCollected.Value : _elementalCrystalsCollectedLocal;
            set
            {
                if (IsSpawned && IsServer) n_ElementalCrystalsCollected.Value = value;
                else _elementalCrystalsCollectedLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnElementalCrystalsCollectedChanged);
            }
        }

        public float ChargeCrystalValue
        {
            get => IsSpawned ? n_ChargeCrystalValue.Value : _chargeCrystalValueLocal;
            set
            {
                if (IsSpawned && IsServer) n_ChargeCrystalValue.Value = value;
                else _chargeCrystalValueLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnChargeCrystalValueChanged);
            }
        }

        public float MassCrystalValue
        {
            get => IsSpawned ? n_MassCrystalValue.Value : _massCrystalValueLocal;
            set
            {
                if (IsSpawned && IsServer) n_MassCrystalValue.Value = value;
                else _massCrystalValueLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnMassCrystalValueChanged);
            }
        }

        public float SpaceCrystalValue
        {
            get => IsSpawned ? n_SpaceCrystalValue.Value : _spaceCrystalValueLocal;
            set
            {
                if (IsSpawned && IsServer) n_SpaceCrystalValue.Value = value;
                else _spaceCrystalValueLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnSpaceCrystalValueChanged);
            }
        }

        public float TimeCrystalValue
        {
            get => IsSpawned ? n_TimeCrystalValue.Value : _timeCrystalValueLocal;
            set
            {
                if (IsSpawned && IsServer) n_TimeCrystalValue.Value = value;
                else _timeCrystalValueLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnTimeCrystalValueChanged);
            }
        }

        public int SkimmerShipCollisions
        {
            get => IsSpawned ? n_SkimmerShipCollisions.Value : _skimmerShipCollisionsLocal;
            set
            {
                if (IsSpawned && IsServer) n_SkimmerShipCollisions.Value = value;
                else _skimmerShipCollisionsLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnSkimmerShipCollisionsChanged);
            }
        }

        public float FullSpeedStraightAbilityActiveTime
        {
            get => IsSpawned ? n_FullSpeedStraightAbilityActiveTime.Value : _fullSpeedStraightAbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_FullSpeedStraightAbilityActiveTime.Value = value;
                else _fullSpeedStraightAbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnFullSpeedStraightAbilityActiveTimeChanged);
            }
        }

        public float RightStickAbilityActiveTime
        {
            get => IsSpawned ? n_RightStickAbilityActiveTime.Value : _rightStickAbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_RightStickAbilityActiveTime.Value = value;
                else _rightStickAbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnRightStickAbilityActiveTimeChanged);
            }
        }

        public float LeftStickAbilityActiveTime
        {
            get => IsSpawned ? n_LeftStickAbilityActiveTime.Value : _leftStickAbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_LeftStickAbilityActiveTime.Value = value;
                else _leftStickAbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnLeftStickAbilityActiveTimeChanged);
            }
        }

        public float FlipAbilityActiveTime
        {
            get => IsSpawned ? n_FlipAbilityActiveTime.Value : _flipAbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_FlipAbilityActiveTime.Value = value;
                else _flipAbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnFlipAbilityActiveTimeChanged);
            }
        }

        public float Button1AbilityActiveTime
        {
            get => IsSpawned ? n_Button1AbilityActiveTime.Value : _button1AbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_Button1AbilityActiveTime.Value = value;
                else _button1AbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnButton1AbilityActiveTimeChanged);
            }
        }

        public float Button2AbilityActiveTime
        {
            get => IsSpawned ? n_Button2AbilityActiveTime.Value : _button2AbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_Button2AbilityActiveTime.Value = value;
                else _button2AbilityActiveTimeLocal = value;

                if (!IsSpawned)
                RaiseSpecific(OnButton2AbilityActiveTimeChanged);
            }
        }

        public float Button3AbilityActiveTime
        {
            get => IsSpawned ? n_Button3AbilityActiveTime.Value : _button3AbilityActiveTimeLocal;
            set
            {
                if (IsSpawned && IsServer) n_Button3AbilityActiveTime.Value = value;
                else _button3AbilityActiveTimeLocal = value;

                if (!IsSpawned)
                    RaiseSpecific(OnButton3AbilityActiveTimeChanged);
            }
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // NETWORK EVENT HOOKS
        //–––––––––––––––––––––––––––––––––––––––––

        public override void OnNetworkSpawn()
        {
            n_Score.OnValueChanged += (_, __) =>
            {
                OnScoreChanged?.Invoke();
                OnAnyStatChanged?.Invoke(this);
            };

            n_VolumeCreated.OnValueChanged          += (_, __) => RaiseSpecific(OnVolumeCreatedChanged);
            n_TotalVolumeDestroyed.OnValueChanged   += (_, __) => RaiseSpecific(OnTotalVolumeDestroyedChanged);
            n_HostileVolumeDestroyed.OnValueChanged += (_, __) => RaiseSpecific(OnHostileVolumeDestroyedChanged);
            n_FriendlyVolumeDestroyed.OnValueChanged+= (_, __) => RaiseSpecific(OnFriendlyVolumeDestroyedChanged);

            n_BlocksCreated.OnValueChanged          += (_, __) => RaiseSpecific(OnBlocksCreatedChanged);
            n_BlocksDestroyed.OnValueChanged        += (_, __) => RaiseSpecific(OnBlocksDestroyedChanged);
            n_BlocksRestored.OnValueChanged         += (_, __) => RaiseSpecific(OnBlocksRestoredChanged);

            n_PrismStolen.OnValueChanged            += (_, __) => RaiseSpecific(OnPrismsStolenChanged);
            n_PrismsRemaining.OnValueChanged        += (_, __) => RaiseSpecific(OnPrismsRemainingChanged);

            n_VolumeRestored.OnValueChanged         += (_, __) => RaiseSpecific(OnVolumeRestoredChanged);
            n_VolumeStolen.OnValueChanged           += (_, __) => RaiseSpecific(OnVolumeStolenChanged);
            n_VolumeRemaining.OnValueChanged        += (_, __) => RaiseSpecific(OnVolumeRemainingChanged);

            n_CrystalsCollected.OnValueChanged          += (_,_) => RaiseSpecific(OnCrystalsCollectedChanged);
            n_OmniCrystalsCollected.OnValueChanged      += (_, __) => RaiseSpecific(OnOmniCrystalsCollectedChanged);
            n_ElementalCrystalsCollected.OnValueChanged += (_, __) => RaiseSpecific(OnElementalCrystalsCollectedChanged);

            n_ChargeCrystalValue.OnValueChanged         += (_, __) => RaiseSpecific(OnChargeCrystalValueChanged);
            n_MassCrystalValue.OnValueChanged           += (_, __) => RaiseSpecific(OnMassCrystalValueChanged);
            n_SpaceCrystalValue.OnValueChanged          += (_, __) => RaiseSpecific(OnSpaceCrystalValueChanged);
            n_TimeCrystalValue.OnValueChanged           += (_, __) => RaiseSpecific(OnTimeCrystalValueChanged);

            n_SkimmerShipCollisions.OnValueChanged      += (_, __) => RaiseSpecific(OnSkimmerShipCollisionsChanged);

            n_FullSpeedStraightAbilityActiveTime.OnValueChanged += (_, __) => RaiseSpecific(OnFullSpeedStraightAbilityActiveTimeChanged);
            n_RightStickAbilityActiveTime.OnValueChanged        += (_, __) => RaiseSpecific(OnRightStickAbilityActiveTimeChanged);
            n_LeftStickAbilityActiveTime.OnValueChanged         += (_, __) => RaiseSpecific(OnLeftStickAbilityActiveTimeChanged);
            n_FlipAbilityActiveTime.OnValueChanged              += (_, __) => RaiseSpecific(OnFlipAbilityActiveTimeChanged);
            n_Button1AbilityActiveTime.OnValueChanged           += (_, __) => RaiseSpecific(OnButton1AbilityActiveTimeChanged);
            n_Button2AbilityActiveTime.OnValueChanged           += (_, __) => RaiseSpecific(OnButton2AbilityActiveTimeChanged);
            n_Button3AbilityActiveTime.OnValueChanged           += (_, __) => RaiseSpecific(OnButton3AbilityActiveTimeChanged);
        }

        public override void OnNetworkDespawn()
        {
            // Optional: explicit -= here if you want, but Netcode clears handlers on destroy.
        }
    }
}
