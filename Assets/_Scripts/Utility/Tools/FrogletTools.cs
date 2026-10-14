#if UNITY_EDITOR

using UnityEditor;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Restores the Froglet Toolbox's persisted logging preferences when the editor loads.
    /// Every menu item lives on its own tool now (Docs/TOOLING.md); nothing is declared here.
    /// </summary>
    [InitializeOnLoad]
    public static class FrogletTools
    {
        static FrogletTools()
        {
            LogControlWindow.LoadPrefs();
        }
    }
}

#endif
