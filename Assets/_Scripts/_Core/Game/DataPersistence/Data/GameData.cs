using StarWriter.Core;

[System.Serializable]
public class GameData : DataPersistenceBase<GameData>
{
    public int testNumber; // testing only
    
    //Scoring Data
    public int firstLifeHighScore;
    public int highScore;

    // the constructor will provide the default values before a GamaData.data files exists
    public GameData()
    {
        testNumber = 42; //testing only
        firstLifeHighScore = 0;    //Score before watching extended life ad
        highScore = 0;             //All time highest SinglePlayerScore on this device
    }

    public override GameData LoadData()
    {
        var loadedData = DataPersistenceManager.Instance.LoadGameData();
        testNumber = loadedData.testNumber;
        firstLifeHighScore = loadedData.firstLifeHighScore;
        highScore = loadedData.highScore;
        return loadedData;
    }

    public override void SaveData()
    {
        DataPersistenceManager.Instance.SaveGame(this);
    }
}