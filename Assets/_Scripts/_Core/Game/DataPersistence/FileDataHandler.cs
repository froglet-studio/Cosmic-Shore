using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace StarWriter.Core
{
    public class FileDataHandler<T>
    {
        private string dataDirPath = "";
        private string dataFileName;

        public FileDataHandler(string dataDirPath, string dataFileName)
        {
            this.dataDirPath = dataDirPath;
            this.dataFileName = dataFileName;
        }

        public T Load()
        {
            string fullPath = Path.Combine(dataDirPath, dataFileName);
            Debug.Log("Load Path: " + fullPath);

            T loadedData = default(T);

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
                    loadedData = JsonUtility.FromJson<T>(dataToLoad);
                }
                catch (Exception e)
                {

                    Debug.Log("Error occured while loading file from " + fullPath + "\n" + e);
                }
            }

            return loadedData;
        }

        public void Save(T data)
        {
            //string fullPath = Path.Join(dataDirPath, gameDataFileName);
            string fullPath = Path.Combine(dataDirPath, dataFileName);
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
}