using System;

namespace CosmicShore.Data
{
    public interface IRoundStats
    {
        //──────────────────────────────────────────
        // EVENTS
        //──────────────────────────────────────────

        event Action<IRoundStats> OnAnyStatChanged;
        event Action OnScoreChanged;

        // Prism count events
        event Action<IRoundStats> OnBlocksCreatedChanged;
        event Action<IRoundStats> OnBlocksDestroyedChanged;
        event Action<IRoundStats> OnBlocksRestoredChanged;
        event Action<IRoundStats> OnPrismsStolenChanged;
        event Action<IRoundStats> OnPrismsRemainingChanged;
        event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
        event Action<IRoundStats> OnHostilePrismsDestroyedChanged;

        // Volume events
        event Action<IRoundStats> OnVolumeCreatedChanged;
        event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
        event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
        event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
        event Action<IRoundStats> OnVolumeRestoredChanged;
        event Action<IRoundStats> OnVolumeStolenChanged;
        event Action<IRoundStats> OnVolumeRemainingChanged;

        // Crystal events
        event Action<IRoundStats> OnCrystalsCollectedChanged;
        event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
        event Action<IRoundStats> OnElementalCrystalsCollectedChanged;

        event Action<IRoundStats> OnChargeCrystalValueChanged;
        event Action<IRoundStats> OnMassCrystalValueChanged;
        event Action<IRoundStats> OnSpaceCrystalValueChanged;
        event Action<IRoundStats> OnTimeCrystalValueChanged;

        // Misc events
        event Action<IRoundStats> OnSkimmerShipCollisionsChanged;
        event Action<IRoundStats> OnJoustCollisionChanged;
        event Action<IRoundStats> OnLivesChanged;
        event Action<IRoundStats> OnEliminatedChanged;
        event Action<IRoundStats> OnGoalsScoredChanged;
        event Action<IRoundStats> OnLifeformsKilledChanged;
        event Action<IRoundStats> OnBulletHitsLandedChanged;
        event Action<IRoundStats> OnMissileHitsLandedChanged;
        event Action<IRoundStats> OnDebuffHitsLandedChanged;
        event Action<IRoundStats> OnCombatPointsChanged;

        // Ability time events
        event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

        //──────────────────────────────────────────
        // PROPERTIES
        //──────────────────────────────────────────

        string Name { get; set; }
        Domains Domain { get; set; }

        float Score { get; set; }

        // Prism counts
        int BlocksCreated { get; set; }
        int BlocksDestroyed { get; set; }
        int BlocksRestored { get; set; }
        int PrismStolen { get; set; }
        int PrismsRemaining { get; set; }
        int FriendlyPrismsDestroyed { get; set; }
        int HostilePrismsDestroyed { get; set; }

        // Volumes
        float VolumeCreated { get; set; }
        float TotalVolumeDestroyed { get; set; }
        float VolumeRestored { get; set; }
        float VolumeStolen { get; set; }
        float VolumeRemaining { get; set; }
        float FriendlyVolumeDestroyed { get; set; }
        float HostileVolumeDestroyed { get; set; }

        // Crystals
        int CrystalsCollected { get; set; }
        int OmniCrystalsCollected { get; set; }
        int ElementalCrystalsCollected { get; set; }

        float ChargeCrystalValue { get; set; }
        float MassCrystalValue { get; set; }
        float SpaceCrystalValue { get; set; }
        float TimeCrystalValue { get; set; }

        // Other stats
        int SkimmerShipCollisions { get; set; }
        int JoustCollisions { get; set; }
        int Lives { get; set; }
        bool IsEliminated { get; set; }

        /// <summary>Goals this player has scored - the domain-race metric shared by AstroLeague,
        /// NucleusRush and ScarabScramble.</summary>
        int GoalsScored { get; set; }

        /// <summary>
        /// Fauna this player has KILLED - an attributed creature death (body prisms shot out,
        /// or a crystal joust), never a starvation or predation death. The scoring metric of
        /// Wildlife Liberation; fed by CellRuntimeDataSO.OnFaunaKilled -> StatsManager.
        /// </summary>
        int LifeformsKilled { get; set; }

        /// <summary>
        /// Direct projectile hits this player has LANDED on an opposing vessel - the bullet
        /// half of vessel-vs-vessel gunnery. A raw count, deliberately unweighted: what a hit
        /// is WORTH is a mode's business (see <see cref="CombatPoints"/>).
        /// </summary>
        int BulletHitsLanded { get; set; }

        /// <summary>
        /// Missile hits this player has LANDED on an opposing vessel - a direct strike OR
        /// being caught in the blast, counted ONCE per missile per victim.
        /// </summary>
        int MissileHitsLanded { get; set; }

        /// <summary>
        /// Area DEBUFFS this player has LANDED on an opposing pilot - today the Dolphin's
        /// crystal blast catching someone in its cone and stripping their element levels.
        /// </summary>
        int DebuffHitsLanded { get; set; }

        /// <summary>
        /// Weighted combat score - the sum of what this mode paid for each landed hit
        /// (<c>ScoringRuleSO.PointsForCombatHit</c>). Zero in every mode whose rule pays
        /// nothing for combat.
        /// </summary>
        int CombatPoints { get; set; }

        // Ability active times
        float FullSpeedStraightAbilityActiveTime { get; set; }
        float RightStickAbilityActiveTime { get; set; }
        float LeftStickAbilityActiveTime { get; set; }
        float FlipAbilityActiveTime { get; set; }
        float Button1AbilityActiveTime { get; set; }
        float Button2AbilityActiveTime { get; set; }
        float Button3AbilityActiveTime { get; set; }

        //──────────────────────────────────────────
        // RESET
        //──────────────────────────────────────────

        public void Cleanup()
        {
            Score = 0f;

            BlocksCreated = 0;
            BlocksDestroyed = 0;
            BlocksRestored = 0;
            PrismStolen = 0;
            PrismsRemaining = 0;
            FriendlyPrismsDestroyed = 0;
            HostilePrismsDestroyed = 0;

            VolumeCreated = 0f;
            TotalVolumeDestroyed = 0f;
            VolumeRestored = 0f;
            VolumeStolen = 0f;
            VolumeRemaining = 0f;
            FriendlyVolumeDestroyed = 0f;
            HostileVolumeDestroyed = 0f;

            CrystalsCollected = 0;
            OmniCrystalsCollected = 0;
            ElementalCrystalsCollected = 0;

            ChargeCrystalValue = 0f;
            MassCrystalValue = 0f;
            SpaceCrystalValue = 0f;
            TimeCrystalValue = 0f;

            SkimmerShipCollisions = 0;
            JoustCollisions = 0;
            Lives = 0;
            IsEliminated = false;
            GoalsScored = 0;
            LifeformsKilled = 0;
            BulletHitsLanded = 0;
            MissileHitsLanded = 0;
            DebuffHitsLanded = 0;
            CombatPoints = 0;

            FullSpeedStraightAbilityActiveTime = 0f;
            RightStickAbilityActiveTime = 0f;
            LeftStickAbilityActiveTime = 0f;
            FlipAbilityActiveTime = 0f;
            Button1AbilityActiveTime = 0f;
            Button2AbilityActiveTime = 0f;
            Button3AbilityActiveTime = 0f;
        }
    }

    public struct DomainStats
    {
        public Domains Domain;
        public float Score;
    }
}
