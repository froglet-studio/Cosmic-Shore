using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using System.Text;

class DataAccessor
{
    public string FilePath { get; set; }

    public DataAccessor(string fileName)
    {
        FilePath = Application.persistentDataPath + "/" + fileName;
    }

    public bool SaveFileExists()
    {
        return File.Exists(FilePath);
    }

    public void Save<T>(T data) where T : new()
    {

        using FileStream dataStream = new FileStream(FilePath, FileMode.Create);
        BinaryFormatter converter = new BinaryFormatter();
        //converter.Serialize(dataStream, data);

        dataStream.Write(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        })));

        dataStream.Close();
    }

    public T Load<T>() where T : new()
    {
        T Data;

        if (File.Exists(FilePath))
        {
            // File exists 
            using FileStream dataStream = new FileStream(FilePath, FileMode.Open);

            try
            {
                BinaryFormatter converter = new BinaryFormatter();
                //Data = (T)converter.Deserialize(dataStream);

                byte[] data = new byte[dataStream.Length];
                dataStream.Read(data, 0, (int)dataStream.Length);

                Debug.Log(data.Length);
                Debug.Log(data[0]);
                Debug.Log(Encoding.ASCII.GetString(data));

                Data = (T)JsonConvert.DeserializeObject(Encoding.ASCII.GetString(data), typeof(T));

                dataStream.Close();
            }
            catch (SerializationException ex)
            {
                // This likely indicates that the file format has changed across builds
                // For now, let's just recreate the file as a poor version of self healing
                // Once the app is in the wild, we will need a strategy for updating these data models
                // Maybe it's enough to just make additive changes?
                Debug.LogError($"Could not deserialize Save file :( {FilePath}");
                Debug.LogError($"Exception Message: {ex.Message}");

                File.Delete(FilePath);

                Data = new T();
                return Data;
            }
            finally
            {
                if (dataStream != null)
                    dataStream.Close();
            }
        }
        else
        {
            // File does not exist
            Debug.LogWarning("Save file not found in " + FilePath);
            Data = new T();
            return Data;
        }

        return Data;
    }
}