using System.Collections.Generic;

namespace CosmicShore.Integrations.Newton
{
    public interface ISerializeServices
    {
        string SerializeObject(Profile profile);
        string SerializeCollection(List<Profile> profiles);
        string SerializeDictionary(Dictionary<int, Profile> profileSet);
        void SerializeJsonToFile(Profile profile, string filePath);
    }
}