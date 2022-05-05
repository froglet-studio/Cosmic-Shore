using Lofelt.NiceVibrations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HapticController : MonoBehaviour
{
    HapticPatterns.PresetType ButtonPattern = HapticPatterns.PresetType.MediumImpact;
    HapticPatterns.PresetType ShipCollisionPattern = HapticPatterns.PresetType.RigidImpact;
    HapticPatterns.PresetType GameOverPattern = HapticPatterns.PresetType.Success;

    public void PlayPreset(int option)
    {
        HapticPatterns.PlayPreset((HapticPatterns.PresetType) option);
    }

    public void PlayButtonHaptics()
    {
        HapticPatterns.PlayPreset(ButtonPattern);
    }

    public void PlayCollisionHaptics()
    {
        HapticPatterns.PlayPreset(ShipCollisionPattern);
    }

    public void PlayGameOverHaptics()
    {
        HapticPatterns.PlayPreset(GameOverPattern);
    }
}
