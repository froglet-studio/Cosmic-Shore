using Lofelt.NiceVibrations;
using UnityEngine;

public class HapticController : MonoBehaviour
{
    static HapticPatterns.PresetType ButtonPattern = HapticPatterns.PresetType.LightImpact;
    static HapticPatterns.PresetType MutonCollisionPattern = HapticPatterns.PresetType.MediumImpact;
    static HapticPatterns.PresetType BlockCollisionPattern = HapticPatterns.PresetType.Success;

    public void PlayPreset(int option)
    {
        HapticPatterns.PlayPreset((HapticPatterns.PresetType) option);
    }

    public static void PlayButtonPressHaptics()
    {
        HapticPatterns.PlayPreset(ButtonPattern);
    }

    public static void PlayMutonCollisionHaptics()
    {
        HapticPatterns.PlayPreset(MutonCollisionPattern);
    }

    public static void PlayBlockCollisionHaptics()
    {
        HapticPatterns.PlayPreset(BlockCollisionPattern);
    }
}
/*
haptic preset notes:

0, 1, 4, 8  = would good for UI use - feedback for correct input on tutorial
2, 5, 7 - might be good for running through stuff (positive)
3 - Not in use (negative) crash? odd pattern - I wouldn't use it unless its going to match an animation cause it might seem out of place otherwise

5 - Muton
4 - UI
6 - crash into blocks - intense (negative feedback) 
*/
