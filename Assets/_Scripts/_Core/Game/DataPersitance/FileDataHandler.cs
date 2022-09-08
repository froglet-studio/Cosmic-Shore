using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class FileDataHandler 
{
    private string dataDirPath = "";

    private string gameDataFileName = "";

    private string hangerDataFileName = "";

    private string playerDataFileName = "";

    public FileDataHandler(string dataDirPath, string gameDataFileName, string hangerDataFileName, string playerDataFileName)
    {
        this.dataDirPath = dataDirPath;
        this.gameDataFileName = gameDataFileName;
        this.hangerDataFileName = hangerDataFileName;
        this.playerDataFileName = playerDataFileName;
    }

    public GameData LoadGame()
    {
        //string fullPath = Path.Join(dataDirPath, gameDataFileName);
        string fullPath = Path.Combine(dataDirPath, gameDataFileName);
        Debug.Log("Load Path: " + fullPath);
        
        GameData loadedData = null;

        if (File.Exists(fullPath))
        {
            try
            {
                // Load the serialized data from the file
                string dataToLoad = "";

                using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                {

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        dataToLoad = reader.ReadToEnd();
                        Debug.Log(dataToLoad);
                    }
                }
                // Deserialize the data from Json back into the C# object
                loadedData = JsonUtility.FromJson<GameData>(dataToLoad);
            }
            catch (Exception e)
            {

            Debug.Log("Error occured while loading file from " + fullPath + "\n" + e);
            }
        }

        return loadedData;
    }

    public void SaveGame(GameData data)
    {
        //string fullPath = Path.Join(dataDirPath, gameDataFileName);
        string fullPath = Path.Combine(dataDirPath, gameDataFileName);
        Debug.Log("Save Path: " + fullPath);
        try
        {
            //Create Directory if it is null
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            //Serialize data into Json form C# Object
            string dataToStore = JsonUtility.ToJson(data);
            //write data to file
            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(dataToStore);
                }
            }

        }
        catch(Exception e)
        {
            Debug.Log("Error saving to file " + fullPath + "\n" + e);
        }
    }

    public HangerData LoadHanger()
    {
        string fullPath = Path.Combine(dataDirPath, hangerDataFileName);
        Debug.Log("Load Path: " + fullPath);

        HangerData loadedData = null;

        if (File.Exists(fullPath))
        {
            try
            {
                // Load the serialized data from the file
                string dataToLoad = "";

                using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                {

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        dataToLoad = reader.ReadToEnd();
                        Debug.Log(dataToLoad);
                    }
                }
                // Deserialize the data from Json back into the C# object
                loadedData = JsonUtility.FromJson<HangerData>(dataToLoad);
            }
            catch (Exception e)
            {

                Debug.Log("Error occured while loading file from " + fullPath + "\n" + e);
            }
        }

        return loadedData;
    }

    public void SaveHanger(HangerData data)
    {
        string fullPath = Path.Combine(dataDirPath, hangerDataFileName);
        Debug.Log("Save Path: " + fullPath);
        try
        {
            //Create Directory if it is null
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            //Serialize data into Json form C# Object
            string dataToStore = JsonUtility.ToJson(data);
            //write data to file
            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(dataToStore);
                }
            }

        }
        catch (Exception e)
        {
            Debug.Log("Error saving to file " + fullPath + "\n" + e);
        }
    }

    public void SaveCurrentPlayer(PlayerData data)  //TODO Add string playerName and GUID to save this player
    {
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Create(Application.persistentDataPath + "/" + playerDataFileName);

        PlayerData dataToSave = new PlayerData();
        dataToSave = data;
        dataToSave.playerName = data.playerName;
        dataToSave.highestScore = data.highestScore;
        dataToSave.playerBuild = data.playerBuild;

        bf.Serialize(file, dataToSave);
        file.Close();
    }

    public PlayerData LoadCurrentPlayer()  //TODO Add string playerName and GUID to locate and load this player
    {
        PlayerData dataToLoad = new PlayerData();

        BinaryFormatter bf = new BinaryFormatter();
        FileStream file;

        if (File.Exists(Application.persistentDataPath + playerDataFileName))
        {
            file = File.Open(Application.persistentDataPath + "/" + playerDataFileName, FileMode.Open);

            dataToLoad = (PlayerData)bf.Deserialize(file);

        }
        else
        {
            file = File.Create(Application.persistentDataPath + playerDataFileName);

            bf.Serialize(file, dataToLoad);
        }

        file.Close();

        return dataToLoad;
    }
}
