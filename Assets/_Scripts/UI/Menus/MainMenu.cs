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
            //gameManager.OnClickHangar();
            Hangar_Panel.SetActive(true);
            Game_Options_Panel.SetActive(false);
            Main_Menu_Panel.SetActive(false);
            //gameObject.SetActive(false);
        }
        public void OnClickOptionsMenuButton()
        {
            Game_Options_Panel.SetActive(true);
            Main_Menu_Panel.SetActive(false);
            Hangar_Panel.SetActive(false);
            //gameObject.SetActive(false);
        }
        public void OnClickHome()
        {
            Main_Menu_Panel.SetActive(true);
            Game_Options_Panel.SetActive(false);
            Hangar_Panel.SetActive(false);
            //gameObject.SetActive(false);
        }

    }
}