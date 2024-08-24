namespace CosmicShore.Core
{
    [System.Serializable]
    public struct RoundStats
    {
        public int blocksCreated;
        public int blocksDestroyed;
        public int blocksRestored;
        public int blocksStolen;
        public int blocksRemaining;
        public int friendlyBlocksDestroyed;
        public int hostileBlocksDestroyed;
        public float volumeCreated;
        public float volumeDestroyed;
        public float volumeRestored;
        public float volumeStolen;
        public float volumeRemaining;
        public float friendlyVolumeDestroyed;
        public float hostileVolumeDestroyed;
        public int crystalsCollected;
        public int omniCrystalsCollected;
        public int elementalCrystalsCollected;
        public int skimmerShipCollisions;
        public float fullSpeedStraightAbilityActiveTime;
        public float rightStickAbilityActiveTime;
        public float leftStickAbilityActiveTime;
        public float flipAbilityActiveTime;
        public float button1AbilityActiveTime;
        public float button2AbilityActiveTime;
        public float button3AbilityActiveTime;
        

        public RoundStats(bool dummy = false)
        {
            blocksCreated = 0;
            blocksDestroyed = 0;
            blocksRestored = 0;
            blocksStolen = 0;
            blocksRemaining = 0;
            friendlyBlocksDestroyed = 0;
            hostileBlocksDestroyed = 0;
            volumeCreated = 0;
            volumeDestroyed = 0;
            volumeRestored = 0;
            volumeStolen = 0;
            volumeRemaining = 0;
            friendlyVolumeDestroyed = 0;
            hostileVolumeDestroyed = 0;
            crystalsCollected = 0;
            omniCrystalsCollected = 0;
            elementalCrystalsCollected = 0;
            skimmerShipCollisions = 0;
            fullSpeedStraightAbilityActiveTime = 0;
            rightStickAbilityActiveTime = 0;
            leftStickAbilityActiveTime = 0;
            flipAbilityActiveTime = 0;
            button1AbilityActiveTime = 0;
            button2AbilityActiveTime = 0;
            button3AbilityActiveTime = 0;
        }
    }
}