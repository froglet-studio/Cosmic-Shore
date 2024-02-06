using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.App.Systems.RewindSystem
{
    public class RewindSystem : IInitializable
    {
        private List<ObjectData> _timeSnapshot;
        
        public void Initialize()
        {
            // Throw something to start here
        }
    }
}
