using Lofelt.NiceVibrations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HapticController : MonoBehaviour
{
    HapticPatterns.PresetType ButtonFeedback = HapticPatterns.PresetType.MediumImpact;
    HapticPatterns.PresetType ShipCollisionFeedback = HapticPatterns.PresetType.RigidImpact;
    HapticPatterns.PresetType GameOverFeedback = HapticPatterns.PresetType.Success;

    public void PlayPreset(int option)
    {
        HapticPatterns.PlayPreset((HapticPatterns.PresetType) option);
    }
}
