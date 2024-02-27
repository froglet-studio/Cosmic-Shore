#if !QC_DISABLED && !QC_DISABLE_BUILTIN_ALL && !QC_DISABLE_BUILTIN_EXTRA
using UnityEngine;

namespace QFSW.QC.Extras
{
    public static class ApplicationCommands
    {
        [Command("quit", "Quits the player application")]
        [CommandPlatform(Platform.AllPlatforms ^ (Platform.EditorPlatforms | Platform.WebGLPlayer))]
        private static void Quit()
        {
            Application.Quit();
        }
    }
}
#endif