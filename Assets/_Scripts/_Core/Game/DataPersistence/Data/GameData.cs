using StarWriter.Core;

[System.Serializable]
public class GameData : DataPersistenceBase<GameData>
{
    public int testNumber; // testing only
    
    //Scoring Data
    public int firstLifeHighScore;
    public int highScore;
    public int score;

    // the constructor will provide the default values before a GamaData.data files exists
    public GameData()
    {
        testNumber = 42; //testing only

        // SCORES
        firstLifeHighScore = 0;    //Score before watching extended life ad
        highScore = 0;             //All time highest SinglePlayerScore on this device
        score = 0;                 //current SinglePlayerScore saved off for use in scoreboard  //TODO Determine if this is even needed
    }

    public override GameData LoadData()
    {
        var loadedData = DataPersistenceManager.Instance.LoadGameData();
        testNumber = loadedData.testNumber;
        firstLifeHighScore = loadedData.firstLifeHighScore;
        highScore = loadedData.highScore;
        score = loadedData.score;
        return loadedData;
    }

    public override void SaveData()
    {
        DataPersistenceManager.Instance.SaveGame(this);
    }
}