using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LoadSaveTest : MonoBehaviour, IDataPersistence
{
    public TextMeshProUGUI testText;
    public int testNumber = 0;

    private void Awake()
    {
        testText = this.GetComponent<TextMeshProUGUI>();
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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            testNumber++;
            testText.text = testNumber.ToString();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            DataPersistenceManager.Instance.LoadGame();
            testText.text = testNumber.ToString();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            DataPersistenceManager.Instance.SaveGame();
            testText.text = testNumber.ToString();
        }
        
    }

    public void LoadData(HangerData data)
    {
        // Not used here
    }

    public void SaveData( ref HangerData data)
    {
        // Not used here
    }
}
