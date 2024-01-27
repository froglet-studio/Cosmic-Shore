using System.Collections.Generic;

namespace CosmicShore.Integrations.Newton
{
    public interface ISerializeFactory
    {
        IProfile CreateObject();
        List<Profile> CreateCollection();
        Dictionary<int, Profile> CreateDictionary();
    }
}