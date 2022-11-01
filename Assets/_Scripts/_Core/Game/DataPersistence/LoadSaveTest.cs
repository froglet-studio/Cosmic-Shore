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

        //public Dictionary<string, ShipConfiguration> TestDictionary;


        //private void Awake()
        //{
        //    //testText = this.GetComponent<TextMeshProUGUI>();
        //}

        private void Start()
        {
        //    ShipConfiguration build1 = new ShipConfiguration();
        //    ShipConfiguration build2 = new ShipConfiguration();

        //    build2.Pilot = "Zak";

        //    TestDictionary = new Dictionary<string, ShipConfiguration>();
        //    TestDictionary.Add("Test1", build1);
        //    TestDictionary.Add("Test2", build2);

        //    var SD = JsonConvert.SerializeObject(TestDictionary);

        //    Debug.Log(SD);

        //    Dictionary<string, ShipConfiguration> animals = new Dictionary<string, ShipConfiguration>();

        //    animals = JsonConvert.DeserializeObject<Dictionary<string, ShipConfiguration>>(SD);

        //    ShipConfiguration b1 = new ShipConfiguration();
        //    ShipConfiguration b2 = new ShipConfiguration();

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

