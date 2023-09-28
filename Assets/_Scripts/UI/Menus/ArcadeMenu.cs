using StarWriter.Core.HangerBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ArcadeMenu : MonoBehaviour
{
    

    //[SerializeField] SO_GameList GameList;

    /*[SerializeField] TMPro.TMP_Text SelectedGameName;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription;
    [SerializeField] GameObject SelectedGamePreviewWindow;

    [SerializeField] TMPro.TMP_Text SelectedGameName2;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription2;
    [SerializeField] GameObject SelectedGamePreviewWindow2;
*/
    [SerializeField] Transform ShipSelectionContainer; //
    [SerializeField] Transform GameSelectionContainer; //

    [SerializeField] GameObject PlayerCountButtonContainer; //

    [FormerlySerializedAs("DifficultyButtonContainer")]
    [SerializeField] GameObject IntensityButtonContainer; //

    //[SerializeField] MenuAnimator ShipSelectAnimator;

    List<Sprite> IntensityIcons = new(); //
    List<Sprite> PlayerCountIcons = new(); //
    SO_Ship SelectedShip;//
    SO_ArcadeGame SelectedGame;//
  

    // Start is called before the first frame update
    void Start()
    {
        /*SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
        GameTitle.text = game.Name;
        BackgroundImage.sprite = game.CardBackground;

        SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipType).FirstOrDefault();
        ShipImage.sprite = ship.TrailPreviewImage;

        PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];
        IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];*/

        /*// Adds player count button images
        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            PlayerCountIcons.Add(PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite);
        // Adds intensity button images
        for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
            IntensityIcons.Add(IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite);*/

        //PopulateGameSelectionList();
    }

    /*IEnumerator SelectShipCoroutine(int index) //TODO leave on arcade?
    {
        yield return new WaitForEndOfFrame();
        SelectShip(index);
    }

    IEnumerator SelectGameCoroutine(int index) //TODO leave on arcade?
    {
        yield return new WaitForEndOfFrame();
        SelectGame(index);
    }*/

    public void SelectShip(int index) //TODO leave on arcade?
    {
        /*Debug.Log($"SelectShip: {index}");
        Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
        Debug.Log($"Ships.Count: {SelectedGame.Pilots.Count}");

        // Deselect them all
        for (var i = 0; i < SelectedGame.Pilots.Count; i++)
            ShipSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedGame.Pilots[i].Ship.Icon;

        // Select the one
        SelectedShip = SelectedGame.Pilots[index].Ship;
        ShipSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedShip.SelectedIcon;

        // notify the mini game engine that this is the ship to play
        MiniGame.PlayerShipType = SelectedShip.Class;
        MiniGame.PlayerPilot = SelectedGame.Pilots[index];*/
    }

    public void SelectGame(int index) //TODO leave on arcade?
    {
        /*Debug.Log($"SelectGame: {index}");

        // Deselect them all
        for (var i = 0; i < GameList.GameList.Count; i++)
            GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = GameList.GameList[i].Icon;

        // Select the one
        SelectedGame = GameList.GameList[index];
        GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;

        Debug.Log($"SelectGame, PlayerCountButtonContainer.transform.childCount: {PlayerCountButtonContainer.transform.childCount}");

        // Setup player count and difficulty buttons

        // TODO: this is kludgy
        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
        {
            Debug.Log($"SelectGame - SelectedGame.MaxPlayers:{SelectedGame.MaxPlayers}, i:{i}, i < SelectedGame.MaxPlayers:{i < SelectedGame.MaxPlayers}");
            var playerCount = i+1;
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers);
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetPlayerCount(playerCount));
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => PlayerCountButtonContainer.GetComponent<MenuAudio>().PlayAudio());

        }
        SetPlayerCount(1);*/

        // TODO: this is kludgy
       /* for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
        {
            var intensity = i + 1;
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetIntensity(intensity));
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => IntensityButtonContainer.GetComponent<MenuAudio>().PlayAudio());
        }
        SetIntensity(1);

        PopulateGameDetails();
        PopulateShipSelectionList();*/
    }

    public void PlaySelectedGame() 
    {
        //SceneManager.LoadScene(SelectedGame.SceneName);
    }

    public void SetPlayerCount(int playerCount) 
    {
        /*Debug.Log($"SetPlayerCount: {playerCount}");

        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = PlayerCountIcons[i];

        PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Image>().sprite = PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        // notify the mini game engine that this is the number of players
        MiniGame.NumberOfPlayers = playerCount;*/
    }

    public void SetIntensity(int intensity) 
    {
        /*Debug.Log($"ArcadeMenu - SetIntensity: {intensity}");

        for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = IntensityIcons[i];

        IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Image>().sprite = IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        Hangar.Instance.SetAiDifficultyLevel(intensity);// minigame handles on launch

        // notify the mini game engine that this is the difficulty
        MiniGame.IntensityLevel = intensity;*/
    }

    void PopulateShipSelectionList() 
    {
        /*// Deactivate All
        for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            ShipSelectionContainer.GetChild(i).gameObject.SetActive(false);
        
        // Reactivate based on the number of ships
        for (var i = 0; i < SelectedGame.Pilots.Count; i++)
        {
            var selectionIndex = i;
            var ship = SelectedGame.Pilots[i].Ship;

            Debug.Log($"MiniGamesMenu - Populating Ship Select List: {ship.Name}");
            var shipSelection = ShipSelectionContainer.GetChild(i).gameObject;
            shipSelection.SetActive(true);
            shipSelection.GetComponent<Image>().sprite = ship.Icon;
            shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            shipSelection.GetComponent<Button>().onClick.AddListener(() => SelectShip(selectionIndex));
            shipSelection.GetComponent<Button>().onClick.AddListener(() => ShipSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
        }

        ShipSelectAnimator?.Animate();
        StartCoroutine(SelectShipCoroutine(0));*/
    }

    /*void PopulateGameSelectionList()  
    {
        // Deactivate All
        for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
            GameSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of games for the given ship
        for (var i = 0; i < GameList.GameList.Count; i++) {
            var selectionIndex = i;
            var game = GameList.GameList[i];
            Debug.Log($"Populating Game Select List: {game.Name}");
            var gameSelection = GameSelectionContainer.GetChild(i).gameObject;
            gameSelection.SetActive(true);

            gameSelection.GetComponent<Image>().sprite = game.CardBackground;

            gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
            gameSelection.GetComponent<Button>().onClick.AddListener(() => GameSelectionContainer.GetComponent<MenuAudio>().PlayAudio());
        }*/

        //StartCoroutine(SelectGameCoroutine(0));
   // }

    /*void PopulateGameDetails() 
    {
        Debug.Log($"Populating Game Details List: {SelectedGame.Name}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Description}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Icon}");
        Debug.Log($"Populating Game Details List: {SelectedGame.PreviewClip}");
*/
        //SelectedGameName.text = SelectedGame.Name;
        //SelectedGameDescription.text= SelectedGame.Description;
        
        //for (var i=2; i< SelectedGamePreviewWindow.transform.childCount; i++)
            //Destroy(SelectedGamePreviewWindow.transform.GetChild(i).gameObject);

       /* var preview = Instantiate(SelectedGame.PreviewClip);
        preview.transform.SetParent(SelectedGamePreviewWindow.transform, false);

        SelectedGameName2.text = SelectedGame.Name;
        SelectedGameDescription2.text = SelectedGame.Description;

        for (var i = 2; i < SelectedGamePreviewWindow2.transform.childCount; i++)
            Destroy(SelectedGamePreviewWindow2.transform.GetChild(i).gameObject);

        preview = Instantiate(SelectedGame.PreviewClip);
        preview.transform.SetParent(SelectedGamePreviewWindow2.transform, false);*/
    //}

    


}