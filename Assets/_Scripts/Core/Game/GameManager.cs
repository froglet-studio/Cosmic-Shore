using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amoebius.Utility.Singleton;
using StarWriter.Core.Audio;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(GameSetting))]
    public class GameManager : SingletonPersistent<GameManager>
    {
        private GameSetting gameSettings;

        // Start is called before the first frame update
        void Start()
        {
          
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}

