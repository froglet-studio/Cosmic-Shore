using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A mode's own tuning for the Manta's Soar autopilot (<see cref="MantaAnalogTurnBoostExecutor"/>): the stick band an
    /// AI holds both triggers flat inside, and the yaw band past which it splits them to pivot. The executor's serialized
    /// bands stay the default; a mode that has a <c>Resources/AIBoostPolicies/MantaSoarAITuning_&lt;mode&gt;</c> asset - written
    /// by <c>Tools/Build/learn_drives_from_flights.py</c> from people's editor flights - is flown with it instead, so a
    /// mode's learned numbers never retune the Manta in every other mode (the <see cref="AIBoostPolicySO.ForMode"/> rule).
    /// </summary>
    [CreateAssetMenu(fileName = "MantaSoarAITuning", menuName = "ScriptableObjects/AI/Manta Soar AI Tuning")]
    public sealed class MantaSoarAITuningSO : ScriptableObject
    {
        public const string ResourceName = "MantaSoarAITuning";

        [Tooltip("The autopilot holds both triggers flat (full Soar) while its stick deflection is under this, easing " +
                 "the boost off linearly above it. 0 disables the drive.")]
        [Range(0f, 1f)] public float BoostStickBand = 0.35f;

        [Tooltip("Past this yaw stick deflection the autopilot also Yastri-pivots (the trigger on the turn's side rises " +
                 "above the other). Keep it above Boost Stick Band. 1 disables the pivot.")]
        [Range(0f, 1f)] public float YastriStickBand = 0.75f;

        /// <summary>This mode's tuning, or null when the mode has none (the executor's own bands then apply).</summary>
        public static MantaSoarAITuningSO LoadForMode(GameModes mode) =>
            Resources.Load<MantaSoarAITuningSO>($"{AIBoostPolicySO.ModeVariantFolder}/{ResourceName}_{mode}");
    }
}
