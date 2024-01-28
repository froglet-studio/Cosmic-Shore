using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace CosmicShore.Integrations.Newton
{
    public class CustomSerialize : ISerializeServices
    {
        private readonly JsonSerializer _jsonSerializer = new();
        
        public string SerializeObject(Profile profile)
        {
            return JsonConvert.SerializeObject(profile, Formatting.Indented);
        }

        public string SerializeCollection(List<Profile> profiles)
        {
            return JsonConvert.SerializeObject(profiles,Formatting.Indented);
        }

        public string SerializeDictionary(Dictionary<int, Profile> profileSet)
        {
            return JsonConvert.SerializeObject(profileSet, Formatting.Indented);
        }

        public void SerializeJsonToFile(Profile profile, string filePath)
        {
            File.WriteAllText(@filePath, JsonConvert.SerializeObject(profile));

            using var file = File.CreateText(@filePath);
            _jsonSerializer.Serialize(file, profile);
        }
    }
}