using UnityEngine;
using System;
using System.IO;

public class FileDataHandler 
{
    private string dataDirPath = "";

    private string gameDataFileName = "";

    private string hangerDataFileName = "";

    public FileDataHandler(string dataDirPath, string gameDataFileName, string hangerDataFileName)
    {
        this.dataDirPath = dataDirPath;
        this.gameDataFileName = gameDataFileName;
        this.hangerDataFileName = hangerDataFileName;
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
}
