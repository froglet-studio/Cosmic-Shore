using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Riposte's tunables: Jackstraw's (the crystals, the rival choice, the lead and the miss) plus the Swordfish
    /// duelist's own (<see cref="RiposteDuelist"/>, RIPOSTE.md section 3): how it fuels its ramp on the sticks, how far
    /// past a rival it carries a charge and how far it runs out before turning back in.
    /// Authored by <c>Tools/Build/author_riposte_assets.py</c>, never by hand.
    ///
    /// <para><b>Difficulty is the seat's AI level</b> (Easy, Medium, Hard, Legendary - index = level - 1), never the
    /// intensity. Every ladder is monotone toward harder; the generator asserts it.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "RiposteSettings",
        menuName = "ScriptableObjects/Arcade/RiposteSettings")]
    public class RiposteSettingsSO : JackstrawSettingsSO
    {
        [Header("AI duelists - per AI difficulty (index = Easy 1, Medium 2, Hard 3, Legendary 4)")]
        [Tooltip("The least throttle the AI flies, 0..1. The platform's skill throttle is 0.6, which is 30 u/s on this " +
                 "hull: a duel flown there is a slow knot.")]
        public float[] duelThrottleFloor = { 0.7f, 0.85f, 1f, 1f };

        [Tooltip("The AI goes to cut sticks for ramp fuel when its tank is under this share (0 = it never goes on " +
                 "purpose). The Swordfish's ramp runs on what its bill slices; an empty tank tops out at 125 u/s.")]
        public float[] duelReapBelowFuel = { 0f, 0.03f, 0.06f, 0.1f };

        [Tooltip("Once cutting, the AI keeps at it until its tank holds this share (a full ramp burns 0.1 a second).")]
        public float[] duelReapUntilFuel = { 0f, 0.08f, 0.15f, 0.25f };

        [Tooltip("The AI aims this far (u) past the rival on a charge, so the leg is long enough for the ramp to wind " +
                 "up and the hull is still the faster as the bill arrives.")]
        public float[] duelChargeOvershoot = { 150f, 250f, 350f, 450f };

        [Tooltip("After a pass the AI runs straight out this far (u) before it turns back in: the room its next " +
                 "wind-up needs. Short = it turns in slow and often arrives the slower hull.")]
        public float[] duelExtendDistance = { 120f, 220f, 320f, 420f };

        [Header("AI duelists - shared")]
        [Tooltip("A charge is over once the rival is behind the nose and within this distance (u): the AI runs out " +
                 "instead of circling. It also breaks off inside a third of it when it is not the faster hull, since " +
                 "a sword lands only on a slower victim.")]
        [Min(10f)] public float duelPassRadius = 240f;

        [Tooltip("Seconds a charge, a run-out or a cutting line may last before the AI plans again.")]
        [Min(1f)] public float duelPhaseSeconds = 6f;

        [Tooltip("A cutting line crosses the heap to its far side, off the centre by up to this many degrees.")]
        [Range(0f, 80f)] public float duelReapSpreadDegrees = 35f;

        [Tooltip("The AI counts a run-out or a cutting line as flown this close (u) to its end.")]
        [Min(10f)] public float duelArriveRadius = 90f;
    }
}
