namespace Scenes.TestScenes.Firebase_Tests
{
    public enum UpdateResult
    {
        Red,
        Green,
        Blue
    }
    
    public class PlayerData
    {
        public string PlayerName { get; set; }
        public UpdateResult PlayerUpdateResult { get; set; }
    }
}
