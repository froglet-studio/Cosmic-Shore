using UnityEngine;

namespace CosmicShore.TestScenes.ArchitectureSandbox.Scripts
{
    public class ButtonCollections : MonoBehaviour
    {
        private CommandButton[] buttons => LocateButtons();
        CommandButton[] LocateButtons()
        {
            return GetComponentsInChildren<CommandButton>();
        }

        private void Awake()
        {
            buttons[0].SetCommand(ICommand.Create<CommandOne>());
            buttons[1].SetCommand(ICommand.Create<CommandTwo>());
        }
    }
}