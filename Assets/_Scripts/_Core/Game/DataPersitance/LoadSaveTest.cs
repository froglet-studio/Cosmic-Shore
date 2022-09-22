using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace StarWriter.Core
{
    public class LoadSaveTest : MonoBehaviour, IDataPersistence
        {
        public TextMeshProUGUI testText;
        public int testNumber = 0;

        private void Awake()
        {
            testText = this.GetComponent<TextMeshProUGUI>();
        }

        //public Dictionary<string, PlayerBuild> TestDictionary;


        //private void Awake()
        //{
        //    //testText = this.GetComponent<TextMeshProUGUI>();
        //}

        private void Start()
        {
        //    PlayerBuild build1 = new PlayerBuild();
        //    PlayerBuild build2 = new PlayerBuild();

        //    build2.Pilot = "Zak";

        //    TestDictionary = new Dictionary<string, PlayerBuild>();
        //    TestDictionary.Add("Test1", build1);
        //    TestDictionary.Add("Test2", build2);

        //    var SD = JsonConvert.SerializeObject(TestDictionary);

        //    Debug.Log(SD);

        //    Dictionary<string, PlayerBuild> animals = new Dictionary<string, PlayerBuild>();

        //    animals = JsonConvert.DeserializeObject<Dictionary<string, PlayerBuild>>(SD);

        //    PlayerBuild b1 = new PlayerBuild();
        //    PlayerBuild b2 = new PlayerBuild();

        //    animals.TryGetValue("Test1", out b1);
        //    animals.TryGetValue("Test2", out b2);

        //    Debug.Log("Pet 1 " + build1.Pilot);
        //    Debug.Log("Pet 2 " + build2.Pilot);
        }
            public void LoadData(GameData data)
        {
            this.testNumber = data.testNumber;
        }

        public void SaveData(ref GameData data)
        {
            data.testNumber = this.testNumber;
        }



        // Update is called once per frame
        void Update()
        {
            //if (Input.GetKeyDown(KeyCode.Space))
            //{
            //    testNumber++;
            //    testText.text = testNumber.ToString();
            //}
            //if (Input.GetKeyDown(KeyCode.L))
            //{
            //    DataPersistenceManager.Instance.LoadGame();
            //    testText.text = testNumber.ToString();
            //}
            //if (Input.GetKeyDown(KeyCode.S))
            //{
            //    DataPersistenceManager.Instance.SaveGame();
            //    testText.text = testNumber.ToString();
            //}

        }

        public void LoadData(HangarData data)
        {
            // Not used here
        }

        public void SaveData(ref HangarData data)
        {
            // Not used here
        }


    }
}

