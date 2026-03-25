namespace CosmicShore.Game.Arcade
{
    public interface ICrystalRaceController
    {
        void SetCrystalsToFinishServer(int value);
        void NotifyCrystalsCollected(string playerName, int crystalsCollected);
    }
}
