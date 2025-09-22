using Unity.Collections;
using Unity.Netcode;

namespace CosmicShore.Core
{
    public class NetworkRoundStats : NetworkBehaviour, IRoundStats
    {
        private readonly NetworkVariable<FixedString64Bytes> n_Name = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public string Name
        {
            get => n_Name.Value.ToString();
            set => n_Name.Value = value;
        }
        
        private readonly NetworkVariable<Domains> n_Team = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public Domains Domain
        {
            get => n_Team.Value;
            set => n_Team.Value = value;
        }
        
        private readonly NetworkVariable<float> n_Score = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

        public float Score
        {
            get => n_Score.Value;
            set => n_Score.Value = value;
        }
        
        private readonly NetworkVariable<int> n_BlocksCreated = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int BlocksCreated
        {
            get => n_BlocksCreated.Value;
            set => n_BlocksCreated.Value = value;
        }

        private readonly NetworkVariable<int> n_BlocksDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int BlocksDestroyed
        {
            get => n_BlocksDestroyed.Value;
            set => n_BlocksDestroyed.Value = value;
        }

        private readonly NetworkVariable<int> n_BlocksRestored = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int BlocksRestored
        {
            get => n_BlocksRestored.Value;
            set => n_BlocksRestored.Value = value;
        }

        private readonly NetworkVariable<int> n_BlocksStolen = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int BlocksStolen
        {
            get => n_BlocksStolen.Value;
            set => n_BlocksStolen.Value = value;
        }

        private readonly NetworkVariable<int> n_BlocksRemaining = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int PrismsRemaining
        {
            get => n_BlocksRemaining.Value;
            set => n_BlocksRemaining.Value = value;
        }

        private readonly NetworkVariable<int> n_FriendlyBlocksDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int FriendlyPrismsDestroyed
        {
            get => n_FriendlyBlocksDestroyed.Value;
            set => n_FriendlyBlocksDestroyed.Value = value;
        }

        private readonly NetworkVariable<int> n_HostileBlocksDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int HostilePrismsDestroyed
        {
            get => n_HostileBlocksDestroyed.Value;
            set => n_HostileBlocksDestroyed.Value = value;
        }

        private readonly NetworkVariable<float> n_VolumeCreated = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float VolumeCreated
        {
            get => n_VolumeCreated.Value;
            set => n_VolumeCreated.Value = value;
        }

        private readonly NetworkVariable<float> n_VolumeDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float VolumeDestroyed
        {
            get => n_VolumeDestroyed.Value;
            set => n_VolumeDestroyed.Value = value;
        }

        private readonly NetworkVariable<float> n_VolumeRestored = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float VolumeRestored
        {
            get => n_VolumeRestored.Value;
            set => n_VolumeRestored.Value = value;
        }

        private readonly NetworkVariable<float> n_VolumeStolen = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float VolumeStolen
        {
            get => n_VolumeStolen.Value;
            set => n_VolumeStolen.Value = value;
        }

        private readonly NetworkVariable<float> n_VolumeRemaining = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float VolumeRemaining
        {
            get => n_VolumeRemaining.Value;
            set => n_VolumeRemaining.Value = value;
        }

        private readonly NetworkVariable<float> n_FriendlyVolumeDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float FriendlyVolumeDestroyed
        {
            get => n_FriendlyVolumeDestroyed.Value;
            set => n_FriendlyVolumeDestroyed.Value = value;
        }

        private readonly NetworkVariable<float> n_HostileVolumeDestroyed = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float HostileVolumeDestroyed
        {
            get => n_HostileVolumeDestroyed.Value;
            set => n_HostileVolumeDestroyed.Value = value;
        }

        private readonly NetworkVariable<int> n_CrystalsCollected = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int CrystalsCollected
        {
            get => n_CrystalsCollected.Value;
            set => n_CrystalsCollected.Value = value;
        }

        private readonly NetworkVariable<int> n_OmniCrystalsCollected = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int OmniCrystalsCollected
        {
            get => n_OmniCrystalsCollected.Value;
            set => n_OmniCrystalsCollected.Value = value;
        }

        private readonly NetworkVariable<int> n_ElementalCrystalsCollected = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int ElementalCrystalsCollected
        {
            get => n_ElementalCrystalsCollected.Value;
            set => n_ElementalCrystalsCollected.Value = value;
        }

        private readonly NetworkVariable<float> n_ChargeCrystalValue = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float ChargeCrystalValue
        {
            get => n_ChargeCrystalValue.Value;
            set => n_ChargeCrystalValue.Value = value;
        }

        private readonly NetworkVariable<float> n_MassCrystalValue = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float MassCrystalValue
        {
            get => n_MassCrystalValue.Value;
            set => n_MassCrystalValue.Value = value;
        }

        private readonly NetworkVariable<float> n_SpaceCrystalValue = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float SpaceCrystalValue
        {
            get => n_SpaceCrystalValue.Value;
            set => n_SpaceCrystalValue.Value = value;
        }

        private readonly NetworkVariable<float> n_TimeCrystalValue = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float TimeCrystalValue
        {
            get => n_TimeCrystalValue.Value;
            set => n_TimeCrystalValue.Value = value;
        }

        private readonly NetworkVariable<int> n_SkimmerShipCollisions = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public int SkimmerShipCollisions
        {
            get => n_SkimmerShipCollisions.Value;
            set => n_SkimmerShipCollisions.Value = value;
        }

        private readonly NetworkVariable<float> n_FullSpeedStraightAbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float FullSpeedStraightAbilityActiveTime
        {
            get => n_FullSpeedStraightAbilityActiveTime.Value;
            set => n_FullSpeedStraightAbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_RightStickAbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float RightStickAbilityActiveTime
        {
            get => n_RightStickAbilityActiveTime.Value;
            set => n_RightStickAbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_LeftStickAbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float LeftStickAbilityActiveTime
        {
            get => n_LeftStickAbilityActiveTime.Value;
            set => n_LeftStickAbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_FlipAbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float FlipAbilityActiveTime
        {
            get => n_FlipAbilityActiveTime.Value;
            set => n_FlipAbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_Button1AbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float Button1AbilityActiveTime
        {
            get => n_Button1AbilityActiveTime.Value;
            set => n_Button1AbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_Button2AbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float Button2AbilityActiveTime
        {
            get => n_Button2AbilityActiveTime.Value;
            set => n_Button2AbilityActiveTime.Value = value;
        }

        private readonly NetworkVariable<float> n_Button3AbilityActiveTime = new(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);
        public float Button3AbilityActiveTime
        {
            get => n_Button3AbilityActiveTime.Value;
            set => n_Button3AbilityActiveTime.Value = value;
        }
    }
}