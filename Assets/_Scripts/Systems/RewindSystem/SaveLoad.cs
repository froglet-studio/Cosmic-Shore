using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

namespace CosmicShore.App.Systems.RewindSystem
{
    public static class SaveLoad {
        public static List<TransformValues> SavedGames = new();
    
        //it's static so we can call it from anywhere 
        public static void Save(TransformValues transformValues) {
            SavedGames.Add(transformValues);
            var bf = new BinaryFormatter();
            //Application.persistentDataPath is a string, so if you wanted you can put that into debug.log if you want to know where save games are located 
            var file = File.Create (Application.persistentDataPath + "/savedGames.gd"); //you can call it anything you want 
            bf.Serialize(file, SavedGames);
            file.Close();
        }	
	
        public static void Load() {
            if(File.Exists(Application.persistentDataPath + "/savedGames.gd")) {
                var bf = new BinaryFormatter();
                var file = File.Open(Application.persistentDataPath + "/savedGames.gd", FileMode.Open);
                SavedGames = (List<TransformValues>)bf.Deserialize(file);
                file.Close();
            }
        }
    }
}