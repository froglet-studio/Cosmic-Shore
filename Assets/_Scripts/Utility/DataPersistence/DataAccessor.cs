using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using System.Text;
using System;

/// <summary>
/// Serializes objects to binary and saves to disk
/// </summary>
static class DataAccessor
{
    /// <summary>
    /// Helper method to convert a filename into a full path
    /// </summary>
    /// <param name="fileName">Filename to convert to a full path</param>
    /// <returns></returns>
    static string GetFilePath(string fileName)
    {
        return Application.persistentDataPath + "/" + fileName;
    }

    /// <summary>
    /// Save a serializable object of type T to disk
    /// </summary>
    /// <typeparam name="T">Generic type of a serializable object</typeparam>
    /// <param name="fileName">Filename to store the serialized object into</param>
    /// <param name="data">Instance of the object to save</param>
    public static void Save<T>(string fileName, T data) where T : new ()
    {
        using FileStream dataStream = new FileStream(GetFilePath(fileName), FileMode.Create);
        BinaryFormatter converter = new BinaryFormatter();
        //converter.Serialize(dataStream, data);

        dataStream.Write(Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        })));

        dataStream.Close();
    }

    /// <summary>
    /// Load a serializable object of type T to disk
    /// </summary>
    /// <typeparam name="T">Generic type of a serializable object</typeparam>
    /// <param name="fileName">Filename to store the serialized object into</param>
    /// <returns>The deserialized object data loaded from disk</returns>
    public static T Load<T>(string fileName) where T : new()
    {
        T Data;
        string FilePath = GetFilePath(fileName);

        if (File.Exists(FilePath))
        {
            // File exists 
            using FileStream dataStream = new FileStream(FilePath, FileMode.Open);

            try
            {
                //BinaryFormatter converter = new BinaryFormatter();
                //Data = (T)converter.Deserialize(dataStream);

                byte[] data = new byte[dataStream.Length];
                dataStream.Read(data, 0, (int)dataStream.Length);

                Debug.Log($"DataAccessor.Load -  Type:{typeof(T)}, Data:{Encoding.ASCII.GetString(data)}");

                Data = (T)JsonConvert.DeserializeObject(Encoding.ASCII.GetString(data), typeof(T));

                dataStream.Close();
            }
            catch (Exception ex)
            {
                // This likely indicates that the file format has changed across builds
                // For now, let's just recreate the file as a poor version of self healing
                // Once the app is in the wild, we will need a strategy for updating these data models
                // Maybe it's enough to just make additive changes?
                Debug.LogError($"Issue encountered while deserializing a save file :( {FilePath}");
                Debug.LogError($"Exception Message: {ex.Message}");

                dataStream.Close();

                File.Delete(FilePath);

                Data = new T();
                return Data;
            }
            /*catch (SerializationException ex)
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
            }*/
            finally
            {
                if (dataStream != null)
                    dataStream.Close();
            }
        }
        else
        {
            // File does not exist
            Data = new T();
            return Data;
        }

        return Data;
    }

    /// <summary>
    /// Nuke the saved file.
    /// </summary>
    /// <param name="fileName">File to nuke</param>
    public static void Flush(string fileName)
    {
        if (File.Exists(GetFilePath(fileName)))
        {
            File.Delete(GetFilePath(fileName));
        }
    }
}