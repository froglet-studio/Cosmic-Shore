using System;
using System.Collections.Generic;

namespace CosmicShore.Integrations.Newton
{
    public class SerializeFactory : ISerializeFactory
    {
        public IProfile CreateObject() => 
            new Profile{Id = "11", IsLoggedIn = true, LoginContext = "dandy", LoginDate = DateTime.Today};

        public List<Profile> CreateCollection() => new()
        {
            new() { Id = "22", IsLoggedIn = true, LoginContext = "welp", LoginDate = DateTime.Now },
            new() { Id = "33", IsLoggedIn = false, LoginContext = "ciao", LoginDate = DateTime.Now }
        };

        public Dictionary<int, Profile> CreateDictionary()
        {
            return new Dictionary<int, Profile>
            {
                { 1, new Profile { Id = "44", IsLoggedIn = false, LoginContext = "dah", LoginDate = DateTime.Today } },
                { 2, new Profile { Id = "55", IsLoggedIn = false, LoginContext = "adios", LoginDate = DateTime.Now } }
            };
        } 
        
        
    }
}