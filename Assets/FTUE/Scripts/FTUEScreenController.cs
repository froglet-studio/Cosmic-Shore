using UnityEngine;

namespace CosmicShore.FTUE
{
    public class FTUEScreenController : MonoBehaviour
    {
        public GameObject captainDialogPanel;
        public GameObject inGameInstructionPanel;
        // Add other UI references as needed (text fields, buttons, etc.)

        public void ShowCaptainDialog(string text)
        {
            captainDialogPanel.SetActive(true);
            inGameInstructionPanel.SetActive(false);
            // Set text on panel...
        }

        public void ShowInstruction(string text)
        {
            inGameInstructionPanel.SetActive(true);
            captainDialogPanel.SetActive(false);
            // Set text...
        }

        public void HideAll()
        {
            captainDialogPanel.SetActive(false);
            inGameInstructionPanel.SetActive(false);
        }
    }
}
