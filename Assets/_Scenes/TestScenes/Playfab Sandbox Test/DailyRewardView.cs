using System;
using CosmicShore.Integrations.PlayFab.CloudScripts;
using UnityEngine;

namespace CosmicShore.TestScenes.PlayfabSandboxTest
{
    [Serializable]
    public enum MenuSelection
    {
        RootMenu,
        DailyReward
    }
    public class DailyRewardView : MonoBehaviour
    {
        // Menu selection
        private MenuSelection _selection = MenuSelection.RootMenu;

        private DailyRewardHandler _dailyRewardHandler;
        void Start()
        {
            _dailyRewardHandler = GameObject.Find("DailyRewardHandler").GetComponent<DailyRewardHandler>();
        }

        private void OnGUI()
        {
            switch (_selection)
            {
                case MenuSelection.RootMenu:
                    AddWindow(_selection, OptionsWindow);
                    break;
                case MenuSelection.DailyReward:
                    AddWindow(_selection, DailyRewardWindow);
                    break;
                default:
                    Debug.LogWarning("ControlPanel - Not a valid login method.");
                    break;
            }
        }

        private void OptionsWindow(int windowID)
        {
            GUILayout.Label("Root Menu");
            GUILayout.Space(10);
            
            AddButton(GetText(MenuSelection.DailyReward));
            // TODO: Add additional buttons
            
            GUILayout.Space(10);
        }

        private void DailyRewardWindow(int windowID)
        {
            GUILayout.Label(GetText(MenuSelection.DailyReward));
            GUILayout.Space(10);
            
            if (GUILayout.Button("Claim"))
            {
                _dailyRewardHandler.Claim();
            }
            GUILayout.Space(10);
            
            AddButton(GetText(MenuSelection.RootMenu));
        }
        
        private void AddWindow(MenuSelection selection, GUI.WindowFunction windowFunc)
        {
            GUILayout.Window(0, new Rect(0, 0, 300, 0), windowFunc, GetText(selection));
        }
        
        private void AddButton(string buttonText)
        {
            if (GUILayout.Button(buttonText))
            {
                _selection = GetSelection(buttonText);
            }
        }
        
        private static MenuSelection GetSelection(string buttonText)
        {
            return buttonText switch
            {
                "Daily Reward" => MenuSelection.DailyReward,
                _ => MenuSelection.RootMenu
            };
        }

        private static string GetText(MenuSelection selection)
        {
            return selection switch
            {
                MenuSelection.DailyReward => "Daily Reward",
                MenuSelection.RootMenu => "Cancel",
                _ => string.Empty
            };
        }

    }
}
