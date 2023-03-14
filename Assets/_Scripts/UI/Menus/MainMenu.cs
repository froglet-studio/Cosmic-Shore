using UnityEngine;

namespace StarWriter.Core.UI
{
    /// <summary>
    /// Provides high level functionality to panels in the main menu scene
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public GameObject Game_Options_Panel;
        public GameObject Hangar_Panel;
        public GameObject Main_Menu_Panel;
        public GameObject Minigames_Panel;
        public GameObject Minigames_Settings_Panel;
        public GameObject Records_Panel;
        public GameObject Ship_Select;
        public GameObject Minigame_Settings;

        GameManager gameManager;

        public void Start()
        {
            gameManager = GameManager.Instance;
        }
        public void OnClickPlayGame()
        {
            gameManager.OnClickPlayButton();
        }
        public void OnClickGameModeOne()
        {
            gameManager.OnClickTestGameModeOne();
        }
        public void OnClickGameModeTwo()
        {
            gameManager.OnClickTestGameModeTwo();
        }
        public void OnClickGameModeThree()
        {
            gameManager.OnClickTestGameModeThree();
        }
        public void OnClickGameModeFour()
        {
            gameManager.OnClickTestGameModeFour();
        }
        public void OnClickGameTestDesign()
        {
            gameManager.OnClickGameTestDesign();
        }
        public void OnClickHangar()
        {
            Hangar_Panel.SetActive(true);
            Game_Options_Panel.SetActive(false);
            Main_Menu_Panel.SetActive(false);
            Minigames_Panel.SetActive(false);
            Minigames_Settings_Panel.SetActive(false);
            Records_Panel.SetActive(false);
            Ship_Select.SetActive(false);
            Minigame_Settings.SetActive(false);
        }
        public void OnClickRecords()
        {
            Hangar_Panel.SetActive(false);
            Game_Options_Panel.SetActive(false);
            Main_Menu_Panel.SetActive(false);
            Minigames_Panel.SetActive(false);
            Minigames_Settings_Panel.SetActive(false);
            Records_Panel.SetActive(true);
            Ship_Select.SetActive(false);
            Minigame_Settings.SetActive(false);
        }
        public void OnClickMinigames()
        {
            Minigames_Panel.SetActive(true);
            Game_Options_Panel.SetActive(false);
            Main_Menu_Panel.SetActive(false);
            Minigames_Settings_Panel.SetActive(false);
            Hangar_Panel.SetActive(false);
            Records_Panel.SetActive(false);
            Ship_Select.SetActive(false);
            Minigame_Settings.SetActive(false);
        }   
        public void OnClickOptionsMenuButton()
        {
            Game_Options_Panel.SetActive(true);
            Main_Menu_Panel.SetActive(false);
            Hangar_Panel.SetActive(false);
            Minigames_Panel.SetActive(false);
            Minigames_Settings_Panel.SetActive(false);
            Records_Panel.SetActive(false);
            Ship_Select.SetActive(false);
            Minigame_Settings.SetActive(false);
        }
        public void OnClickHome()
        {
            Main_Menu_Panel.SetActive(true);
            Game_Options_Panel.SetActive(false);
            Hangar_Panel.SetActive(false);
            Minigames_Panel.SetActive(false);
            Minigames_Settings_Panel.SetActive(false);
            Records_Panel.SetActive(false);
            Ship_Select.SetActive(false);
            Minigame_Settings.SetActive(false);
        }
    }
}