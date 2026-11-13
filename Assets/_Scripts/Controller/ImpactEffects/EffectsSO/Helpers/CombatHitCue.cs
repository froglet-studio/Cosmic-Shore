using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE ON-SCREEN HALF OF A SCORED HIT: who hit whom, and what it was worth, announced on every
    /// machine so it can be DRAWN (Docs/ElementalAbilitySystem/PETAL_THEFT_CUE.md).
    ///
    /// <para><b>Why it exists.</b> PvP is petals only (Garrett, 2026-10-10), so every scored hit is a
    /// theft. Until this cue the theft was invisible: a hit drew a small star near the victim, its
    /// petals left as tiny free-for-all crystals, and nothing on screen tied the victim to the pilot
    /// who scored. A muted replay could not say who hit whom (footage critique, 2026-11-13, weak link 3).
    /// This class carries only the FACT; <c>PetalTheftFeedback</c> draws it.</para>
    ///
    /// <para><b>Raised where the hit is admitted.</b> <see cref="CombatHitDrain.TryAdmit"/> and
    /// <see cref="CombatHitDrain.TryAdmitPetals"/> call <see cref="OnAdmitted"/> for every hit they let
    /// through, so a cue and a score are one event: a warded victim, a silenced attacker and a latched
    /// repeat draw nothing, exactly as they score nothing. That runs on the machine that DECIDES the
    /// hit (the attacker's owner). The decider raises <see cref="Landed"/> locally and sends the same
    /// cue to every other peer through the victim hull's <see cref="ICombatHitCueRelay"/>, the shape
    /// silence uses (<see cref="VesselSilence.OnPetalsAdmitted"/>), so every screen draws the theft.</para>
    ///
    /// <para><b>Nothing reads this to decide anything.</b> It is a picture. The petals, the score and
    /// the silence are settled by the systems that own them, whether or not anything listens here.</para>
    /// </summary>
    public static class CombatHitCue
    {
        /// <summary>
        /// Raised on every machine for every admitted hit: the victim, the attacker, and what the hit
        /// is worth in PETALS (ten points to the petal on each of four elements, the same price silence
        /// is measured in). Worth, not petals that came loose: a fresh pilot holds no petals, and a hit
        /// on one still has to read as a hit.
        /// </summary>
        public static event Action<IVesselStatus, IVesselStatus, float> Landed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Landed = null;

        /// <summary>
        /// The decider's call, from <see cref="CombatHitDrain"/> once a hit is admitted: raise the cue
        /// here and publish it to every other peer.
        /// </summary>
        public static void OnAdmitted(IVesselStatus victim, IVesselStatus attacker, float petals)
        {
            if (victim == null || attacker == null || petals <= 0f) return;

            Raise(victim, attacker, petals);

            var victimRelay = RelayOf(victim);
            if (victimRelay != null && victimRelay.IsNetworked)
                victimRelay.PublishHitCue(RelayOf(attacker), petals);
        }

        /// <summary>Raises <see cref="Landed"/> on THIS machine only. The decider's own call goes
        /// through <see cref="OnAdmitted"/>; a peer calls this when the decider's publish arrives.</summary>
        public static void Raise(IVesselStatus victim, IVesselStatus attacker, float petals)
        {
            if (victim == null || attacker == null || petals <= 0f) return;
            Landed?.Invoke(victim, attacker, petals);
        }

        static ICombatHitCueRelay RelayOf(IVesselStatus status)
        {
            // NOT status.Transform: see ElementalCrystalEjector. That property throws rather than
            // answering null on a status whose vessel is gone.
            var hull = status?.Vessel?.Transform;
            return hull && hull.TryGetComponent(out ICombatHitCueRelay relay) ? relay : null;
        }
    }

    /// <summary>
    /// The network half of <see cref="CombatHitCue"/>, implemented by <c>NetworkVesselImpactor</c> on
    /// every vessel hull. Declared here so the cue stays free of Netcode types and keeps compiling in
    /// <c>Tools/Build/elemental_transfer_harness</c>.
    /// </summary>
    public interface ICombatHitCueRelay
    {
        /// <summary>This hull is a spawned network object.</summary>
        bool IsNetworked { get; }

        /// <summary>Tells every OTHER peer that this hull's pilot was hit by <paramref name="attacker"/>'s
        /// pilot for <paramref name="petals"/>, so each one raises <see cref="CombatHitCue.Landed"/>.
        /// Called by the machine that decided the hit. Cosmetic, so a lost packet costs one picture.</summary>
        void PublishHitCue(ICombatHitCueRelay attacker, float petals);
    }
}
