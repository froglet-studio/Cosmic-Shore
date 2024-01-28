using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.Integrations.Newton
{
    public class JsonPresenter : IStartable
    {
        private readonly ISerializeFactory _serializeFactory;
        private readonly ISerializeServices _customSerialize;

        public JsonPresenter(ISerializeServices customSerialize, ISerializeFactory serializeFactory)
        {
            _customSerialize = customSerialize;
            _serializeFactory = serializeFactory;
        }
        
        public void Start()
        {
            Debug.LogFormat("Serialize Object - Profile: {0}", 
                _customSerialize.SerializeObject(_serializeFactory.CreateObject() as Profile));
            Debug.LogFormat("Serialize Collection - Profile list {0}", 
                _customSerialize.SerializeCollection(_serializeFactory.CreateCollection()));
            Debug.LogFormat("Serialize Dictionary - Profile set {0}", 
                _customSerialize.SerializeDictionary(_serializeFactory.CreateDictionary()));
        }
    }
}