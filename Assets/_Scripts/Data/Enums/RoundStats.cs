using System;
using CosmicShore.Data;
using Unity.Netcode;
using Unity.Collections;
namespace CosmicShore.Data
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
        public event Action<IRoundStats> OnJoustCollisionChanged;

        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

        //–––––––––––––––––––––––––––––––––––––––––
        // LOCAL STORAGE (single source of truth for reads)
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
        int _skimmerShipCollisionsLocal, _joustCollisionsLocal;

        float _fullSpeedStraightAbilityActiveTimeLocal,
            _rightStickAbilityActiveTimeLocal,
            _leftStickAbilityActiveTimeLocal;

        float _flipAbilityActiveTimeLocal,
            _button1AbilityActiveTimeLocal,
            _button2AbilityActiveTimeLocal,
            _button3AbilityActiveTimeLocal;

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

        readonly NetworkVariable<int> n_JoustCollisions =
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

        /// <summary>
        /// Allows external callers (e.g. StatsManager) to fire OnJoustCollisionChanged
        /// without needing access to the private event backing field.
        /// </summary>
        public void InvokeOnJoustCollisionChanged() => RaiseSpecific(OnJoustCollisionChanged);

        //–––––––––––––––––––––––––––––––––––––––––
        // PROPERTIES
        //
        // Local fields are the single source of truth for reads.
        // Setters always update the local field first, then push
        // to the NetworkVariable when running on the server.
        // OnNetworkSpawn syncs local fields from replication.
        //–––––––––––––––––––––––––––––––––––––––––

        public string Name
        {
            get => _nameLocal;
            set
            {
                _nameLocal = value;
                if (IsSpawned && IsServer) n_Name.Value = value;
            }
        }

        public Domains Domain
        {
            get => _domainLocal;
            set
            {
                _domainLocal = value;
                if (IsSpawned && IsServer) n_Domain.Value = value;
            }
        }

        public float Score
        {
            get => _scoreLocal;
            set
            {
                _scoreLocal = value;
                if (IsSpawned && IsServer)
                    n_Score.Value = value;

                if (IsSpawned) return;
                OnScoreChanged?.Invoke();
                OnAnyStatChanged?.Invoke(this);
            }
        }

        public float VolumeCreated
        {
            get => _volumeCreatedLocal;
            set
            {
                _volumeCreatedLocal = value;
                if (IsSpawned && IsServer) n_VolumeCreated.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnVolumeCreatedChanged);
            }
        }

        public int BlocksCreated
        {
            get => _blocksCreatedLocal;
            set
            {
                _blocksCreatedLocal = value;
                if (IsSpawned && IsServer) n_BlocksCreated.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnBlocksCreatedChanged);
            }
        }

        public int BlocksDestroyed
        {
            get => _blocksDestroyedLocal;
            set
            {
                _blocksDestroyedLocal = value;
                if (IsSpawned && IsServer) n_BlocksDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnBlocksDestroyedChanged);
            }
        }

        public int BlocksRestored
        {
            get => _blocksRestoredLocal;
            set
            {
                _blocksRestoredLocal = value;
                if (IsSpawned && IsServer) n_BlocksRestored.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnBlocksRestoredChanged);
            }
        }

        public int PrismStolen
        {
            get => _prismStolenLocal;
            set
            {
                _prismStolenLocal = value;
                if (IsSpawned && IsServer) n_PrismStolen.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnPrismsStolenChanged);
            }
        }

        public int PrismsRemaining
        {
            get => _prismsRemainingLocal;
            set
            {
                _prismsRemainingLocal = value;
                if (IsSpawned && IsServer) n_PrismsRemaining.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnPrismsRemainingChanged);
            }
        }

        public int FriendlyPrismsDestroyed
        {
            get => _friendlyPrismsDestroyedLocal;
            set
            {
                _friendlyPrismsDestroyedLocal = value;
                if (IsSpawned && IsServer) n_FriendlyPrismsDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnFriendlyPrismsDestroyedChanged);
            }
        }

        public int HostilePrismsDestroyed
        {
            get => _hostilePrismsDestroyedLocal;
            set
            {
                _hostilePrismsDestroyedLocal = value;
                if (IsSpawned && IsServer) n_HostilePrismsDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnHostilePrismsDestroyedChanged);
            }
        }

        public float TotalVolumeDestroyed
        {
            get => _totalVolumeDestroyedLocal;
            set
            {
                _totalVolumeDestroyedLocal = value;
                if (IsSpawned && IsServer) n_TotalVolumeDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnTotalVolumeDestroyedChanged);
            }
        }

        public float VolumeRestored
        {
            get => _volumeRestoredLocal;
            set
            {
                _volumeRestoredLocal = value;
                if (IsSpawned && IsServer) n_VolumeRestored.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnVolumeRestoredChanged);
            }
        }

        public float VolumeStolen
        {
            get => _volumeStolenLocal;
            set
            {
                _volumeStolenLocal = value;
                if (IsSpawned && IsServer) n_VolumeStolen.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnVolumeStolenChanged);
            }
        }

        public float VolumeRemaining
        {
            get => _volumeRemainingLocal;
            set
            {
                _volumeRemainingLocal = value;
                if (IsSpawned && IsServer) n_VolumeRemaining.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnVolumeRemainingChanged);
            }
        }

        public float FriendlyVolumeDestroyed
        {
            get => _friendlyVolumeDestroyedLocal;
            set
            {
                _friendlyVolumeDestroyedLocal = value;
                if (IsSpawned && IsServer) n_FriendlyVolumeDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnFriendlyVolumeDestroyedChanged);
            }
        }

        public float HostileVolumeDestroyed
        {
            get => _hostileVolumeDestroyedLocal;
            set
            {
                _hostileVolumeDestroyedLocal = value;
                if (IsSpawned && IsServer) n_HostileVolumeDestroyed.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnHostileVolumeDestroyedChanged);
            }
        }

        public int CrystalsCollected
        {
            get => _crystalsCollectedLocal;
            set
            {
                _crystalsCollectedLocal = value;
                if (IsSpawned && IsServer) n_CrystalsCollected.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnCrystalsCollectedChanged);
            }
        }

        public int OmniCrystalsCollected
        {
            get => _omniCrystalsCollectedLocal;
            set
            {
                _omniCrystalsCollectedLocal = value;
                if (IsSpawned && IsServer) n_OmniCrystalsCollected.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnOmniCrystalsCollectedChanged);
            }
        }

        public int ElementalCrystalsCollected
        {
            get => _elementalCrystalsCollectedLocal;
            set
            {
                _elementalCrystalsCollectedLocal = value;
                if (IsSpawned && IsServer) n_ElementalCrystalsCollected.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnElementalCrystalsCollectedChanged);
            }
        }

        public float ChargeCrystalValue
        {
            get => _chargeCrystalValueLocal;
            set
            {
                _chargeCrystalValueLocal = value;
                if (IsSpawned && IsServer) n_ChargeCrystalValue.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnChargeCrystalValueChanged);
            }
        }

        public float MassCrystalValue
        {
            get => _massCrystalValueLocal;
            set
            {
                _massCrystalValueLocal = value;
                if (IsSpawned && IsServer) n_MassCrystalValue.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnMassCrystalValueChanged);
            }
        }

        public float SpaceCrystalValue
        {
            get => _spaceCrystalValueLocal;
            set
            {
                _spaceCrystalValueLocal = value;
                if (IsSpawned && IsServer) n_SpaceCrystalValue.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnSpaceCrystalValueChanged);
            }
        }

        public float TimeCrystalValue
        {
            get => _timeCrystalValueLocal;
            set
            {
                _timeCrystalValueLocal = value;
                if (IsSpawned && IsServer) n_TimeCrystalValue.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnTimeCrystalValueChanged);
            }
        }

        public int SkimmerShipCollisions
        {
            get => _skimmerShipCollisionsLocal;
            set
            {
                _skimmerShipCollisionsLocal = value;
                if (IsSpawned && IsServer) n_SkimmerShipCollisions.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnSkimmerShipCollisionsChanged);
            }
        }

        public int JoustCollisions
        {
            get => _joustCollisionsLocal;
            set
            {
                _joustCollisionsLocal = value;
                if (IsSpawned && IsServer) n_JoustCollisions.Value = value;

                RaiseSpecific(OnJoustCollisionChanged);
            }
        }

        public float FullSpeedStraightAbilityActiveTime
        {
            get => _fullSpeedStraightAbilityActiveTimeLocal;
            set
            {
                _fullSpeedStraightAbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_FullSpeedStraightAbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnFullSpeedStraightAbilityActiveTimeChanged);
            }
        }

        public float RightStickAbilityActiveTime
        {
            get => _rightStickAbilityActiveTimeLocal;
            set
            {
                _rightStickAbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_RightStickAbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnRightStickAbilityActiveTimeChanged);
            }
        }

        public float LeftStickAbilityActiveTime
        {
            get => _leftStickAbilityActiveTimeLocal;
            set
            {
                _leftStickAbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_LeftStickAbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnLeftStickAbilityActiveTimeChanged);
            }
        }

        public float FlipAbilityActiveTime
        {
            get => _flipAbilityActiveTimeLocal;
            set
            {
                _flipAbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_FlipAbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnFlipAbilityActiveTimeChanged);
            }
        }

        public float Button1AbilityActiveTime
        {
            get => _button1AbilityActiveTimeLocal;
            set
            {
                _button1AbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_Button1AbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnButton1AbilityActiveTimeChanged);
            }
        }

        public float Button2AbilityActiveTime
        {
            get => _button2AbilityActiveTimeLocal;
            set
            {
                _button2AbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_Button2AbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnButton2AbilityActiveTimeChanged);
            }
        }

        public float Button3AbilityActiveTime
        {
            get => _button3AbilityActiveTimeLocal;
            set
            {
                _button3AbilityActiveTimeLocal = value;
                if (IsSpawned && IsServer) n_Button3AbilityActiveTime.Value = value;

                if (!IsSpawned)
                    RaiseSpecific(OnButton3AbilityActiveTimeChanged);
            }
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // NETWORK EVENT HOOKS
        //
        // Each callback syncs the local field from
        // the replicated NetworkVariable value, then
        // fires the corresponding game event.
        //–––––––––––––––––––––––––––––––––––––––––

        public override void OnNetworkSpawn()
        {
            // --- Initial sync from current NetworkVariable state ---
            _nameLocal   = n_Name.Value.ToString();
            _domainLocal = n_Domain.Value;
            _scoreLocal  = n_Score.Value;

            _volumeCreatedLocal           = n_VolumeCreated.Value;
            _blocksCreatedLocal           = n_BlocksCreated.Value;
            _blocksDestroyedLocal         = n_BlocksDestroyed.Value;
            _blocksRestoredLocal          = n_BlocksRestored.Value;
            _prismStolenLocal             = n_PrismStolen.Value;
            _prismsRemainingLocal         = n_PrismsRemaining.Value;
            _friendlyPrismsDestroyedLocal = n_FriendlyPrismsDestroyed.Value;
            _hostilePrismsDestroyedLocal  = n_HostilePrismsDestroyed.Value;

            _totalVolumeDestroyedLocal    = n_TotalVolumeDestroyed.Value;
            _volumeRestoredLocal          = n_VolumeRestored.Value;
            _volumeStolenLocal            = n_VolumeStolen.Value;
            _volumeRemainingLocal         = n_VolumeRemaining.Value;
            _friendlyVolumeDestroyedLocal = n_FriendlyVolumeDestroyed.Value;
            _hostileVolumeDestroyedLocal  = n_HostileVolumeDestroyed.Value;

            _crystalsCollectedLocal          = n_CrystalsCollected.Value;
            _omniCrystalsCollectedLocal      = n_OmniCrystalsCollected.Value;
            _elementalCrystalsCollectedLocal = n_ElementalCrystalsCollected.Value;
            _chargeCrystalValueLocal         = n_ChargeCrystalValue.Value;
            _massCrystalValueLocal           = n_MassCrystalValue.Value;
            _spaceCrystalValueLocal          = n_SpaceCrystalValue.Value;
            _timeCrystalValueLocal           = n_TimeCrystalValue.Value;

            _skimmerShipCollisionsLocal = n_SkimmerShipCollisions.Value;
            _joustCollisionsLocal       = n_JoustCollisions.Value;

            _fullSpeedStraightAbilityActiveTimeLocal = n_FullSpeedStraightAbilityActiveTime.Value;
            _rightStickAbilityActiveTimeLocal        = n_RightStickAbilityActiveTime.Value;
            _leftStickAbilityActiveTimeLocal         = n_LeftStickAbilityActiveTime.Value;
            _flipAbilityActiveTimeLocal              = n_FlipAbilityActiveTime.Value;
            _button1AbilityActiveTimeLocal           = n_Button1AbilityActiveTime.Value;
            _button2AbilityActiveTimeLocal           = n_Button2AbilityActiveTime.Value;
            _button3AbilityActiveTimeLocal           = n_Button3AbilityActiveTime.Value;

            // --- Replication callbacks: sync local field, then fire event ---

            n_Name.OnValueChanged   += (_, v) => _nameLocal = v.ToString();
            n_Domain.OnValueChanged += (_, v) => _domainLocal = v;

            n_Score.OnValueChanged += (_, v) =>
            {
                _scoreLocal = v;
                OnScoreChanged?.Invoke();
                OnAnyStatChanged?.Invoke(this);
            };

            n_VolumeCreated.OnValueChanged += (_, v) =>
            {
                _volumeCreatedLocal = v;
                RaiseSpecific(OnVolumeCreatedChanged);
            };

            n_TotalVolumeDestroyed.OnValueChanged += (_, v) =>
            {
                _totalVolumeDestroyedLocal = v;
                RaiseSpecific(OnTotalVolumeDestroyedChanged);
            };

            n_HostileVolumeDestroyed.OnValueChanged += (_, v) =>
            {
                _hostileVolumeDestroyedLocal = v;
                RaiseSpecific(OnHostileVolumeDestroyedChanged);
            };

            n_FriendlyVolumeDestroyed.OnValueChanged += (_, v) =>
            {
                _friendlyVolumeDestroyedLocal = v;
                RaiseSpecific(OnFriendlyVolumeDestroyedChanged);
            };

            n_BlocksCreated.OnValueChanged += (_, v) =>
            {
                _blocksCreatedLocal = v;
                RaiseSpecific(OnBlocksCreatedChanged);
            };

            n_BlocksDestroyed.OnValueChanged += (_, v) =>
            {
                _blocksDestroyedLocal = v;
                RaiseSpecific(OnBlocksDestroyedChanged);
            };

            n_BlocksRestored.OnValueChanged += (_, v) =>
            {
                _blocksRestoredLocal = v;
                RaiseSpecific(OnBlocksRestoredChanged);
            };

            n_PrismStolen.OnValueChanged += (_, v) =>
            {
                _prismStolenLocal = v;
                RaiseSpecific(OnPrismsStolenChanged);
            };

            n_PrismsRemaining.OnValueChanged += (_, v) =>
            {
                _prismsRemainingLocal = v;
                RaiseSpecific(OnPrismsRemainingChanged);
            };

            n_FriendlyPrismsDestroyed.OnValueChanged += (_, v) =>
            {
                _friendlyPrismsDestroyedLocal = v;
                RaiseSpecific(OnFriendlyPrismsDestroyedChanged);
            };

            n_HostilePrismsDestroyed.OnValueChanged += (_, v) =>
            {
                _hostilePrismsDestroyedLocal = v;
                RaiseSpecific(OnHostilePrismsDestroyedChanged);
            };

            n_VolumeRestored.OnValueChanged += (_, v) =>
            {
                _volumeRestoredLocal = v;
                RaiseSpecific(OnVolumeRestoredChanged);
            };

            n_VolumeStolen.OnValueChanged += (_, v) =>
            {
                _volumeStolenLocal = v;
                RaiseSpecific(OnVolumeStolenChanged);
            };

            n_VolumeRemaining.OnValueChanged += (_, v) =>
            {
                _volumeRemainingLocal = v;
                RaiseSpecific(OnVolumeRemainingChanged);
            };

            n_CrystalsCollected.OnValueChanged += (_, v) =>
            {
                _crystalsCollectedLocal = v;
                RaiseSpecific(OnCrystalsCollectedChanged);
            };

            n_OmniCrystalsCollected.OnValueChanged += (_, v) =>
            {
                _omniCrystalsCollectedLocal = v;
                RaiseSpecific(OnOmniCrystalsCollectedChanged);
            };

            n_ElementalCrystalsCollected.OnValueChanged += (_, v) =>
            {
                _elementalCrystalsCollectedLocal = v;
                RaiseSpecific(OnElementalCrystalsCollectedChanged);
            };

            n_ChargeCrystalValue.OnValueChanged += (_, v) =>
            {
                _chargeCrystalValueLocal = v;
                RaiseSpecific(OnChargeCrystalValueChanged);
            };

            n_MassCrystalValue.OnValueChanged += (_, v) =>
            {
                _massCrystalValueLocal = v;
                RaiseSpecific(OnMassCrystalValueChanged);
            };

            n_SpaceCrystalValue.OnValueChanged += (_, v) =>
            {
                _spaceCrystalValueLocal = v;
                RaiseSpecific(OnSpaceCrystalValueChanged);
            };

            n_TimeCrystalValue.OnValueChanged += (_, v) =>
            {
                _timeCrystalValueLocal = v;
                RaiseSpecific(OnTimeCrystalValueChanged);
            };

            n_SkimmerShipCollisions.OnValueChanged += (_, v) =>
            {
                _skimmerShipCollisionsLocal = v;
                RaiseSpecific(OnSkimmerShipCollisionsChanged);
            };

            n_JoustCollisions.OnValueChanged += (_, v) =>
            {
                _joustCollisionsLocal = v;
                // Server already raised OnJoustCollisionChanged from the setter;
                // only clients need the replication-driven event.
                if (!IsServer)
                    RaiseSpecific(OnJoustCollisionChanged);
            };

            n_FullSpeedStraightAbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _fullSpeedStraightAbilityActiveTimeLocal = v;
                RaiseSpecific(OnFullSpeedStraightAbilityActiveTimeChanged);
            };

            n_RightStickAbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _rightStickAbilityActiveTimeLocal = v;
                RaiseSpecific(OnRightStickAbilityActiveTimeChanged);
            };

            n_LeftStickAbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _leftStickAbilityActiveTimeLocal = v;
                RaiseSpecific(OnLeftStickAbilityActiveTimeChanged);
            };

            n_FlipAbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _flipAbilityActiveTimeLocal = v;
                RaiseSpecific(OnFlipAbilityActiveTimeChanged);
            };

            n_Button1AbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _button1AbilityActiveTimeLocal = v;
                RaiseSpecific(OnButton1AbilityActiveTimeChanged);
            };

            n_Button2AbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _button2AbilityActiveTimeLocal = v;
                RaiseSpecific(OnButton2AbilityActiveTimeChanged);
            };

            n_Button3AbilityActiveTime.OnValueChanged += (_, v) =>
            {
                _button3AbilityActiveTimeLocal = v;
                RaiseSpecific(OnButton3AbilityActiveTimeChanged);
            };
        }

        public override void OnNetworkDespawn()
        {
        }
    }
}
