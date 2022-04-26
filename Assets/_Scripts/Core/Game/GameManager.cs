using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Amoebius.Utility.Singleton;

namespace StarWriter.Core
{
    [DefaultExecutionOrder(0)]
    [RequireComponent(typeof(GameSetting))]
    public class GameManager : SingletonPersistent<GameManager>
    {
        private GameSetting gameSettings;

        //TODO get AudioMaster dl
        //set background music volume
        // play music

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

