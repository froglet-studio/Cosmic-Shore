using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tuning for the Grizzly's BOMB PUMP — both triggers blow a bomb behind the hull that
    /// kicks the ship forward. See GRIZZLY_BOMB_PUMP.md.
    ///
    /// The one rule the whole feature is built on: <b>a bomb's SIZE comes from how hard the
    /// trigger was squeezed (peak analog pressure over the pull), never from time held</b>, so
    /// size and cooldown are independent — a full squeeze and a feather tap cost the same
    /// recharge. Each trigger owns its own cooldown, which is what makes alternating LT → RT
    /// the rhythm: two triggers pump twice as often as one.
    ///
    /// Wired directly on <see cref="GrizzlyBombPumpExecutor"/> (shared by both trigger
    /// actions) so a missing wire shows in the inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "GrizzlyBombPumpConfig", menuName = "ScriptableObjects/Vessel Actions/Grizzly Bomb Pump Config")]
    public class GrizzlyBombPumpConfigSO : ScriptableObject
    {
        [Header("Pressure → size")]
        [SerializeField, Tooltip("Trigger pressure (0-1) that yields the SMALLEST bomb. Anything at or below it is a minimum bomb.")]
        [Range(0f, 1f)] float pressureForMinBomb = 0.1f;
        [SerializeField, Tooltip("Trigger pressure (0-1) that yields the BIGGEST bomb. Set under 1 because many triggers never report a full 1.0.")]
        [Range(0f, 1f)] float pressureForMaxBomb = 0.95f;
        [SerializeField, Tooltip("Shape of the pressure → size curve. 1 = linear, >1 makes the top of the pull matter more.")]
        [Range(0.25f, 4f)] float pressureExponent = 1f;

        [Header("Cooldown (independent of size)")]
        [SerializeField, Tooltip("Seconds before the SAME trigger can blow another bomb. Each trigger has its own clock, so alternating doubles the pump rate.")]
        float cooldownPerTrigger = 0.45f;

        [Header("Kick")]
        [SerializeField, Tooltip("Forward velocity (u/s) a minimum bomb adds along the nose.")]
        float minKick = 10f;
        [SerializeField, Tooltip("Forward velocity (u/s) a maximum bomb adds along the nose. Stacked kicks share the vessel's velocity-modifier ceiling (100 u/s).")]
        float maxKick = 40f;
        [SerializeField, Tooltip("Seconds each kick persists (cosine ease-out).")]
        float kickDuration = 0.8f;

        [Header("Blast")]
        [SerializeField, Tooltip("Explosion prefab(s) spawned for each bomb. Spared: the pilot's own domain (own trail shields instead of breaking, teammates untouched). Everything else in the blast is hit.")]
        AOEExplosion[] aoePrefabs;
        [SerializeField, Tooltip("Blast scale of a minimum bomb.")]
        float minBlastScale = 15f;
        [SerializeField, Tooltip("Blast scale of a maximum bomb.")]
        float maxBlastScale = 60f;
        [SerializeField, Tooltip("How far behind the hull centre the bomb goes off.")]
        float spawnBehindDistance = 6f;
        [SerializeField, Tooltip("Sideways offset per trigger (LT left, RT right) so alternating bombs read as two thrusters.")]
        float spawnSideOffset = 3f;

        [Header("Audio")]
        [SerializeField, Tooltip("FMOD event played when a bomb blows. Leave empty for silence.")]
        EventReference bombEvent;

        public float PressureForMinBomb => pressureForMinBomb;
        public float PressureForMaxBomb => pressureForMaxBomb;
        public float PressureExponent => pressureExponent;
        public float CooldownPerTrigger => cooldownPerTrigger;
        public float MinKick => minKick;
        public float MaxKick => maxKick;
        public float KickDuration => kickDuration;
        public AOEExplosion[] AoePrefabs => aoePrefabs;
        public float MinBlastScale => minBlastScale;
        public float MaxBlastScale => maxBlastScale;
        public float SpawnBehindDistance => spawnBehindDistance;
        public float SpawnSideOffset => spawnSideOffset;
        public EventReference BombEvent => bombEvent;

        /// <summary>Peak trigger pressure (0-1) → bomb size (0-1). Pure; edit-mode tested.</summary>
        public float SizeForPressure(float pressure)
        {
            float lo = Mathf.Min(pressureForMinBomb, pressureForMaxBomb);
            float hi = Mathf.Max(pressureForMinBomb, pressureForMaxBomb);
            float t = hi - lo <= 1e-5f ? (pressure >= hi ? 1f : 0f) : Mathf.Clamp01((pressure - lo) / (hi - lo));
            return Mathf.Pow(t, pressureExponent);
        }

        public float KickForSize(float size01) => Mathf.Lerp(minKick, maxKick, Mathf.Clamp01(size01));
        public float BlastScaleForSize(float size01) => Mathf.Lerp(minBlastScale, maxBlastScale, Mathf.Clamp01(size01));
    }
}
