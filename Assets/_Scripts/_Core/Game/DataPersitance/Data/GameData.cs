using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Settings
{
    public bool adsEnabled;             //TODO
    public bool invertYEnabled;         //TODO 
    public bool isAudioEnabled;         //TODO
    public bool isGyroEnabled;          //TODO
    public bool isTutorialEnabled;      //TODO

    //public Settings(bool adsEnabled, bool invertYEnabled, bool isGyroEnabled, bool isTutorialEnabled, bool isAudioEnabled)
    //{
    //    AdsEnabled = adsEnabled;
    //    InvertYEnabled = invertYEnabled;
    //    IsGyroEnabled = isGyroEnabled;
    //    IsTutorialEnabled = isTutorialEnabled;
    //    IsAudioEnabled = isAudioEnabled;
    //}
}

[System.Serializable]

public class GameData
{
    public int testNumber; //testing only
    //bools
    public bool adsEnabled;             //TODO
    public bool invertYEnabled;         //TODO 
    public bool isAudioEnabled;         //TODO
    public bool isGyroEnabled;          //TODO
    public bool isTutorialEnabled;      //TODO
    //SCORES
    public int firstLifeHighScore;      //TODO
    public int highScore;               //TODO
    public int score;                   //TODO


    // the constructor will provide the default values before a GamaData.data files exists
    public GameData()
    {
        this.testNumber = 0; //testing only 
        //bools
        this.adsEnabled = true;
        this.invertYEnabled = false;  //TODO check value
        this.isAudioEnabled = true;
        this.isTutorialEnabled = true;
        //SCORES
        this.firstLifeHighScore = 0;
        this.highScore = 0;
        this.score = 0;
        
    }
}
