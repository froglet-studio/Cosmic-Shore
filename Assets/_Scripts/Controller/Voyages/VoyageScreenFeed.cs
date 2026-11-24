using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a voyage under way tells the in-game screens beyond the objective (Frogma Rev 134 / Rev 137, picked by Shombith
    /// 2026-11-24): where the Ark is on its leg and what the leg's places are called (the pause chart), what happens next
    /// door, the rival crews, and the detour vote. The screens read it a few times a second; it never takes a press except
    /// <see cref="CastVote"/>. Pure reads: the data shapes live in <c>Data/Voyages/VoyageScreens.cs</c>.
    /// </summary>
    public interface IVoyageScreenSource
    {
        /// <summary>The leg under way (false when none is).</summary>
        bool TryGetCourseFix(out VoyageCourseFix fix);

        /// <summary>The run's difficulty, 1-9 (how dangerous the sea is; 0 when the job has none).</summary>
        int SeaDifficulty { get; }

        /// <summary>About how long the Ark takes to cross one cell, in seconds (0 when the leg keeps no cell clock).</summary>
        float CellSeconds { get; }

        /// <summary>The events in the cells beside the lane right now, nearest first.</summary>
        void GetNeighbours(List<VoyageNeighbourFix> into);

        /// <summary>The rival crews the squad can see right now (next door or in its cell).</summary>
        void GetRivals(List<VoyageRivalFix> into);

        /// <summary>The detour vote open now, or null.</summary>
        VoyageVote OpenVote { get; }

        /// <summary>Each seat's name and vessel, for the vote's ballots (index = seat).</summary>
        void GetSeats(List<VoyageScreenSeat> into);

        /// <summary>The local pilot presses an option of the open vote (0 detour, 1 press on).</summary>
        void CastVote(int option);
    }

    /// <summary>One seat of the squad as the screens name it.</summary>
    public struct VoyageScreenSeat
    {
        /// <summary>The pilot's callsign.</summary>
        public string Pilot;
        /// <summary>The party friend flying this seat (their player name); empty for the local pilot or an AI.</summary>
        public string Player;
        public VesselClassType Vessel;
        public bool You;
        public bool Human;
        /// <summary>The pilot's portrait, when the run has one (a <c>Sprite</c>; object to keep this file Unity-free).</summary>
        public object Portrait;
    }

    /// <summary>The one voyage the screens read (null when none is under way).</summary>
    public static class VoyageScreenFeed
    {
        public static IVoyageScreenSource Source { get; set; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Source = null;

        /// <summary>Clear <see cref="Source"/> when it is still <paramref name="source"/>.</summary>
        public static void Release(IVoyageScreenSource source)
        {
            if (ReferenceEquals(Source, source)) Source = null;
        }
    }
}
