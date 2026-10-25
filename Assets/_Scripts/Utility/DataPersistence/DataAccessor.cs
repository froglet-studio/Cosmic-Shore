using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;
using System.Text;
using System;
using CosmicShore.Utility;

namespace CosmicShore.Utility
{
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
            string path = GetFilePath(fileName);
            // UTF-8, not ASCII: ASCII turned every non-ASCII character in a saved string into '?'.
            // A file written as ASCII is valid UTF-8, so existing saves still load.
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            }));

            // Write a temp file and swap it in. Opening the real file with FileMode.Create
            // truncated it FIRST, so a kill or crash mid-write (painting progress saves after every
            // stroke) left a truncated file - which Load then failed to parse and deleted.
            string tmp = path + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is PlatformNotSupportedException)
            {
                // The swap can be refused (another MPPM virtual player holding the file open, a
                // platform without File.Replace). Fall back to the direct write, which is no worse
                // than before.
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            }

            // FileShare.ReadWrite so a concurrent reader (e.g. another MPPM virtual
            // player sharing the same persistentDataPath) does not hit a sharing violation.
            using FileStream dataStream = new FileStream(
                path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            dataStream.Write(bytes, 0, bytes.Length);
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
                // File exists. Open read-only with FileShare.ReadWrite so concurrent
                // access (e.g. two MPPM virtual players sharing one persistentDataPath,
                // or a comparator re-reading during List.Sort) does not throw a
                // sharing violation - the IOException would otherwise escape into
                // callers like FavoriteSystem.IsFavorited inside a sort comparator.
                using FileStream dataStream = new FileStream(
                    FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                try
                {
                    //BinaryFormatter converter = new BinaryFormatter();
                    //Data = (T)converter.Deserialize(dataStream);

                    byte[] data = new byte[dataStream.Length];
                    dataStream.Read(data, 0, (int)dataStream.Length);

                    string json = Encoding.UTF8.GetString(data);

                    Data = (T)JsonConvert.DeserializeObject(json, typeof(T));

                    dataStream.Close();
                }
                catch (Exception ex)
                {
                    // This likely indicates that the file format has changed across builds
                    // For now, let's just recreate the file as a poor version of self healing
                    // Once the app is in the wild, we will need a strategy for updating these data models
                    // Maybe it's enough to just make additive changes?
                    CSDebug.LogError($"Issue encountered while deserializing a save file :( {FilePath}");
                    CSDebug.LogError($"Exception Message: {ex.Message}");

                    dataStream.Close();

                    // Set the unreadable file ASIDE rather than deleting it: a truncated or
                    // half-migrated save is still somebody's progress, and a copy is what makes it
                    // recoverable by hand. The fresh default below then saves over the original.
                    try { File.Copy(FilePath, FilePath + ".corrupt", true); }
                    catch (IOException) { }
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
}
