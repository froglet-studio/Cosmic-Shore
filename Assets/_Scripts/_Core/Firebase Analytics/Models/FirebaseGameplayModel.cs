namespace _Scripts._Core.Firebase_Models
{
    public class FirebaseGameplayModel
    {
        // MiniGame Modes enum 
        public MiniGame MiniGame { get; set; }
        // Game Stages enum
        public GameplayStages GameplayStages { get; set; }
        // Ship Types enum
        public ShipTypes ShipTypes { get; set; }
        // Player Count
        public int PlayerCount { get; set; }
        // Intensity (Difficulty level)
        public int Intensity { get; set; }
    }
}